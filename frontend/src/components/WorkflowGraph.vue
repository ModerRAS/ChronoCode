<template>
  <div class="workflow-graph" :class="{ 'workflow-graph--empty': !hasDefinition }">
    <a-empty
      v-if="!hasDefinition"
      description="No workflow definition"
      :image="undefined"
    />
    <a-alert
      v-else-if="parseError"
      type="error"
      show-icon
      message="Invalid workflow definition"
      :description="parseError"
    />
    <div v-else class="workflow-graph__canvas">
      <VueFlow
        :nodes="nodes"
        :edges="edges"
        :nodes-draggable="false"
        :nodes-connectable="false"
        :elements-selectable="true"
        :zoom-on-scroll="true"
        :pan-on-scroll="false"
        fit-view-on-init
      >
        <template #node-default="props">
          <div :class="['wf-node', props.data.statusClass]">
            <div class="wf-node__type">{{ props.data.typeLabel }}</div>
            <div class="wf-node__label">{{ props.data.label }}</div>
            <div v-if="props.data.statusLabel" class="wf-node__status">
              {{ props.data.statusLabel }}
            </div>
          </div>
        </template>
      </VueFlow>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { VueFlow } from '@vue-flow/core'
import type { Edge, Node } from '@vue-flow/core'
import type { NodeExecution } from '../api/tasks'

interface WorkflowNode {
  type: string
  nodeId: string
  name?: string
  nextNodeId?: string
  trueNodeId?: string
  falseNodeId?: string
  branchStartNodeIds?: string[]
  bodyStartNodeId?: string
}

interface WorkflowDefinition {
  version?: number
  startNodeId?: string
  nodes?: WorkflowNode[]
}

const props = defineProps<{
  definition: object | null
  nodeExecutions?: NodeExecution[]
}>()

const NODE_TYPE_LABELS: Record<string, string> = {
  start: 'Start',
  prepare_workspace: 'Workspace',
  agent: 'Agent',
  parallel: 'Parallel',
  condition: 'Condition',
  for_each: 'For Each',
  while: 'While',
  approval_gate: 'Approval',
  commit_changes: 'Commit',
  create_pull_request: 'PR',
  end: 'End',
}

const STATUS_CLASSES: Record<string, string> = {
  completed: 'wf-node--completed',
  running: 'wf-node--running',
  failed: 'wf-node--failed',
  waiting_approval: 'wf-node--approval',
  retrying: 'wf-node--retrying',
  schema_validation_failed: 'wf-node--failed',
  pending: 'wf-node--pending',
  skipped: 'wf-node--pending',
}

const STATUS_LABELS: Record<string, string> = {
  completed: 'completed',
  running: 'running',
  failed: 'failed',
  waiting_approval: 'awaiting approval',
  retrying: 'retrying',
  schema_validation_failed: 'schema failed',
  pending: 'pending',
  skipped: 'skipped',
}

/** Map nodeId -> latest NodeExecution status (by startedAt, then completedAt). */
const statusByNode = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  const latest: Record<string, string> = {}
  for (const ex of props.nodeExecutions ?? []) {
    const prev = latest[ex.nodeId]
    const curTime = ex.startedAt || ex.completedAt || ''
    if (prev === undefined || curTime >= prev) {
      latest[ex.nodeId] = curTime
      map[ex.nodeId] = ex.status
    }
  }
  return map
})

const hasDefinition = computed(() => props.definition !== null && props.definition !== undefined)

const definition = computed<WorkflowDefinition | null>(() => {
  const def = props.definition as WorkflowDefinition | null
  if (!def || !Array.isArray(def.nodes) || def.nodes.length === 0) return null
  return def
})

const parseError = computed(() => {
  if (!hasDefinition.value) return ''
  if (!definition.value) return 'Workflow has no nodes.'
  return ''
})

/** Build edges from control-flow semantics. */
function buildEdges(nodes: WorkflowNode[]): Edge[] {
  const edges: Edge[] = []
  const push = (source: string, target: string, label?: string) => {
    if (!target) return
    edges.push({
      id: `${source}->${target}${label ? `:${label}` : ''}`,
      source,
      target,
      label,
      type: 'default',
      animated: false,
    })
  }
  for (const node of nodes) {
    switch (node.type) {
      case 'start':
      case 'prepare_workspace':
      case 'agent':
      case 'approval_gate':
      case 'commit_changes':
      case 'create_pull_request':
        push(node.nodeId, node.nextNodeId ?? '')
        break
      case 'condition':
        push(node.nodeId, node.trueNodeId ?? '', 'true')
        push(node.nodeId, node.falseNodeId ?? '', 'false')
        break
      case 'parallel':
        for (const branch of node.branchStartNodeIds ?? []) {
          push(node.nodeId, branch)
        }
        push(node.nodeId, node.nextNodeId ?? '', 'join')
        break
      case 'for_each':
      case 'while':
        push(node.nodeId, node.bodyStartNodeId ?? '', 'body')
        push(node.nodeId, node.nextNodeId ?? '', 'next')
        break
      case 'end':
        break
      default:
        break
    }
  }
  return edges
}

/** Top-down layout: y = depth * 130, x spread within each depth level. */
function layoutNodes(nodes: WorkflowNode[], edges: Edge[]): Node[] {
  const ids = nodes.map(n => n.nodeId)
  const adjacency = new Map<string, string[]>()
  for (const id of ids) adjacency.set(id, [])
  for (const edge of edges) {
    adjacency.get(edge.source)?.push(edge.target)
  }

  const depth = new Map<string, number>()
  const startId =
    nodes.find(n => n.type === 'start')?.nodeId ?? ids[0] ?? ''
  if (startId) {
    depth.set(startId, 0)
    const queue = [startId]
    while (queue.length > 0) {
      const cur = queue.shift()!
      const d = depth.get(cur) ?? 0
      for (const next of adjacency.get(cur) ?? []) {
        const nextD = depth.get(next)
        if (nextD === undefined || nextD < d + 1) {
          depth.set(next, d + 1)
          queue.push(next)
        }
      }
    }
  }
  for (const id of ids) if (!depth.has(id)) depth.set(id, 0)

  // Group by depth, then spread x.
  const byDepth = new Map<number, string[]>()
  for (const id of ids) {
    const d = depth.get(id) ?? 0
    if (!byDepth.has(d)) byDepth.set(d, [])
    byDepth.get(d)!.push(id)
  }

  const positions = new Map<string, { x: number; y: number }>()
  const maxDepth = Math.max(0, ...byDepth.keys())
  for (let d = 0; d <= maxDepth; d++) {
    const row = byDepth.get(d) ?? []
    const rowWidth = row.length
    row.forEach((id, col) => {
      const x = (col - (rowWidth - 1) / 2) * 220
      const y = d * 130
      positions.set(id, { x, y })
    })
  }

  return nodes.map(node => {
    const pos = positions.get(node.nodeId) ?? { x: 0, y: 0 }
    const status = statusByNode.value[node.nodeId]
    const statusClass = status ? STATUS_CLASSES[status] ?? '' : ''
    const statusLabel = status ? STATUS_LABELS[status] ?? status : ''
    return {
      id: node.nodeId,
      position: pos,
      data: {
        label: node.name || node.nodeId,
        typeLabel: NODE_TYPE_LABELS[node.type] ?? node.type,
        statusClass,
        statusLabel,
      },
      type: 'default',
      class: ['wf-node-wrapper', statusClass].filter(Boolean).join(' '),
      selectable: true,
    } as Node
  })
}

const nodes = computed<Node[]>(() => {
  const def = definition.value
  if (!def || !def.nodes) return []
  const edges = buildEdges(def.nodes)
  return layoutNodes(def.nodes, edges)
})

const edges = computed<Edge[]>(() => {
  const def = definition.value
  if (!def || !def.nodes) return []
  return buildEdges(def.nodes)
})
</script>

<style scoped>
.workflow-graph {
  width: 100%;
  height: 420px;
  border: 1px solid var(--ant-border-color, #f0f0f0);
  border-radius: 8px;
  background: var(--ant-component-background, #fafafa);
  overflow: hidden;
}

.workflow-graph--empty {
  display: flex;
  align-items: center;
  justify-content: center;
}

.workflow-graph__canvas {
  width: 100%;
  height: 100%;
}

.wf-node {
  min-width: 140px;
  padding: 8px 12px;
  border-radius: 8px;
  border: 1px solid #d9d9d9;
  background: #fff;
  text-align: center;
  font-size: 12px;
  line-height: 1.4;
}

.wf-node__type {
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: rgba(0, 0, 0, 0.45);
}

.wf-node__label {
  font-weight: 600;
  color: rgba(0, 0, 0, 0.85);
}

.wf-node__status {
  margin-top: 2px;
  font-size: 11px;
  color: rgba(0, 0, 0, 0.65);
}

.wf-node--completed {
  border-color: #52c41a;
  background: #f6ffed;
}
.wf-node--running {
  border-color: #1890ff;
  background: #e6f7ff;
}
.wf-node--failed {
  border-color: #ff4d4f;
  background: #fff1f0;
}
.wf-node--approval {
  border-color: #fa8c16;
  background: #fff7e6;
}
.wf-node--retrying {
  border-color: #faad14;
  background: #fffbe6;
}
.wf-node--pending {
  border-color: #bfbfbf;
  background: #f5f5f5;
}
</style>
