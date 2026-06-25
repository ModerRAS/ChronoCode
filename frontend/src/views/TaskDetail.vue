<template>
  <div>
    <a-page-header title="Task Details" @back="$router.push('/')">
      <template #extra>
        <div class="task-detail-actions">
          <a-button @click="$router.push(`/tasks/${taskId}/edit`)">Edit</a-button>
          <a-button type="primary" @click="triggerTask" :loading="triggering">Run Now</a-button>
        </div>
      </template>
    </a-page-header>

    <a-spin :spinning="loading">
      <a-descriptions bordered :column="isMobile ? 1 : 2">
        <a-descriptions-item label="Name">{{ task?.name }}</a-descriptions-item>
        <a-descriptions-item label="Status">
          <a-tag :color="getStatusColor(task?.lastStatus)">{{ getStatusText(task?.lastStatus) }}</a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="Cron">{{ task?.cronExpression }}</a-descriptions-item>
        <a-descriptions-item label="Enabled">
          <a-switch :checked="task?.isEnabled" disabled />
        </a-descriptions-item>
        <a-descriptions-item label="Repository" :span="isMobile ? 1 : 2">{{ task?.repositoryUrl }}</a-descriptions-item>
        <a-descriptions-item label="Base Branch">{{ task?.baseBranch }}</a-descriptions-item>
        <a-descriptions-item label="Branch Strategy">
          {{ task?.branchStrategy === 0 ? 'New Branch' : 'Reuse Branch' }}
        </a-descriptions-item>
        <a-descriptions-item label="Max Runtime">{{ task?.maxRuntimeSeconds }}s</a-descriptions-item>
        <a-descriptions-item label="Max Files">{{ task?.maxFileChanges }}</a-descriptions-item>
        <a-descriptions-item label="Workflow Version">{{ task?.workflowVersion }}</a-descriptions-item>
        <a-descriptions-item label="Runtime Backend">{{ task?.runtimeBackend || '(default)' }}</a-descriptions-item>
        <a-descriptions-item label="Max Concurrent Runs">{{ task?.maxConcurrentRuns }}</a-descriptions-item>
        <a-descriptions-item label="Scheduler Status">{{ task?.schedulerStatus || '-' }}</a-descriptions-item>
        <a-descriptions-item label="Next Run">{{ task?.nextRunAt || '-' }}</a-descriptions-item>
        <a-descriptions-item label="Last Run">{{ task?.lastRunAt || 'Never' }}</a-descriptions-item>
        <a-descriptions-item label="Last Queued">{{ task?.lastQueuedAt || '-' }}</a-descriptions-item>
        <a-descriptions-item label="Last Error" :span="isMobile ? 1 : 2">
          <span v-if="task?.lastError" class="task-detail-error">{{ task.lastError }}</span>
          <span v-else>None</span>
        </a-descriptions-item>
      </a-descriptions>
    </a-spin>

    <a-divider>Workflow</a-divider>
    <a-card size="small" :bordered="true">
      <WorkflowGraph :definition="taskDefinition" :node-executions="nodes" />
    </a-card>

    <a-divider>Execution History</a-divider>

    <a-table
      :columns="execColumns"
      :data-source="executions"
      :loading="execLoading"
      :scroll="isMobile ? { x: 820 } : undefined"
      row-key="id"
      :row-class-name="executionRowClass"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="getStatusColor(record.status)">
            {{ getStatusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button size="small" @click="selectExecution(record.id)">View Nodes</a-button>
            <a-button size="small" @click="viewLogs(record.id)">View Logs</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <template v-if="selectedExecutionId">
      <a-divider>Nodes — Execution {{ selectedExecutionId }}</a-divider>
      <a-space class="nodes-toolbar">
        <a-button size="small" @click="loadNodes(selectedExecutionId)">Refresh Nodes</a-button>
        <a-tag v-if="nodesLoading" color="blue">Loading...</a-tag>
        <a-tag v-else-if="selectedExecutionRunning" color="blue">Auto-refreshing every 5s</a-tag>
      </a-space>
      <a-table
        :columns="nodeColumns"
        :data-source="nodes"
        :loading="nodesLoading"
        :scroll="isMobile ? { x: 900 } : undefined"
        row-key="id"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="getNodeStatusColor(record.status)">{{ record.status }}</a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" @click="viewNodeSession(record.id)">View Session</a-button>
              <template v-if="record.status === 'waiting_approval'">
                <a-button size="small" type="primary" @click="approveNode(record.id, true)">Approve</a-button>
                <a-button size="small" danger @click="approveNode(record.id, false)">Reject</a-button>
              </template>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <a-drawer v-model:open="logsVisible" title="Execution Logs" :width="isMobile ? '100%' : 600" placement="right">
      <a-timeline>
        <a-timeline-item v-for="log in logs" :key="log.timestamp" :color="getLogColor(log.level)">
          <p><strong>{{ log.level }}</strong> - {{ formatTime(log.timestamp) }}</p>
          <p>{{ log.message }}</p>
        </a-timeline-item>
      </a-timeline>
    </a-drawer>
    <a-drawer v-model:open="sessionVisible" title="Node Session" :width="isMobile ? '100%' : 560" placement="right">
      <a-spin :spinning="sessionLoading">
        <a-descriptions v-if="nodeSession" bordered :column="1" size="small">
          <a-descriptions-item label="Backend">{{ nodeSession.backend || '-' }}</a-descriptions-item>
          <a-descriptions-item label="Session ID">{{ nodeSession.sessionId || '-' }}</a-descriptions-item>
          <a-descriptions-item label="Session File">{{ nodeSession.sessionFile || '-' }}</a-descriptions-item>
          <a-descriptions-item label="Working Directory">{{ nodeSession.workingDirectory || '-' }}</a-descriptions-item>
          <a-descriptions-item label="Live">{{ nodeSession.isLive ? 'Yes' : 'No' }}</a-descriptions-item>
          <a-descriptions-item label="Persistent Sessions">{{ nodeSession.supportsPersistentSessions ? 'Yes' : 'No' }}</a-descriptions-item>
          <a-descriptions-item label="Supplemental Messages">{{ nodeSession.supportsSupplementalMessages ? 'Yes' : 'No' }}</a-descriptions-item>
          <a-descriptions-item label="Can Resume">{{ nodeSession.canResume ? 'Yes' : 'No' }}</a-descriptions-item>
        </a-descriptions>
        <a-empty v-else description="No session data" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  taskApi,
  type ScheduledTask,
  type Execution,
  type LogEntry,
  type NodeExecution,
  type ExecutionSession,
} from '../api/tasks'
import { useIsMobile } from '../composables/useIsMobile'
import WorkflowGraph from '../components/WorkflowGraph.vue'

const route = useRoute()
const { isMobile } = useIsMobile()
const taskId = route.params.id as string

const loading = ref(false)
const execLoading = ref(false)
const nodesLoading = ref(false)
const triggering = ref(false)
const task = ref<ScheduledTask>()
const executions = ref<Execution[]>([])
const nodes = ref<NodeExecution[]>([])
const logs = ref<LogEntry[]>([])
const logsVisible = ref(false)
const selectedExecutionId = ref<string | null>(null)
const nodeSession = ref<ExecutionSession | null>(null)
const sessionVisible = ref(false)
const sessionLoading = ref(false)

let nodesInterval: ReturnType<typeof setInterval> | null = null

const execColumns = [
  { title: 'Started', dataIndex: 'startedAt', key: 'started' },
  { title: 'Completed', dataIndex: 'completedAt', key: 'completed' },
  { title: 'Status', key: 'status' },
  { title: 'Trigger', dataIndex: 'triggerSource', key: 'trigger' },
  { title: 'Files Changed', dataIndex: 'filesChanged', key: 'files' },
  { title: 'Branch', dataIndex: 'branchName', key: 'branch' },
  { title: 'Actions', key: 'actions' },
]

const nodeColumns = [
  { title: 'Node ID', dataIndex: 'nodeId', key: 'nodeId' },
  { title: 'Type', dataIndex: 'nodeType', key: 'nodeType' },
  { title: 'Status', key: 'status' },
  { title: 'Attempt', dataIndex: 'attempt', key: 'attempt' },
  { title: 'Failure Reason', dataIndex: 'failureReason', key: 'failureReason' },
  { title: 'Next Retry', dataIndex: 'nextRetryAt', key: 'nextRetryAt' },
  { title: 'Completed', dataIndex: 'completedAt', key: 'completedAt' },
  { title: 'Actions', key: 'actions' },
]

const taskDefinition = computed<object | null>(() => {
  if (!task.value?.workflowDefinitionJson) return null
  try {
    const parsed = JSON.parse(task.value.workflowDefinitionJson)
    return typeof parsed === 'object' && parsed !== null ? parsed : null
  } catch {
    return null
  }
})

const selectedExecution = computed<Execution | undefined>(() =>
  executions.value?.find(e => e.id === selectedExecutionId.value),
)

const selectedExecutionRunning = computed(
  () => selectedExecution.value?.status === 1,
)

const executionRowClass = (record: Execution) =>
  record.id === selectedExecutionId.value ? 'selected-row' : ''

const loadTask = async () => {
  loading.value = true
  try {
    task.value = await taskApi.getById(taskId)
  } catch {
    message.error('Failed to load task')
  } finally {
    loading.value = false
  }
}

const loadExecutions = async () => {
  execLoading.value = true
  try {
    executions.value = await taskApi.getExecutions(taskId)
  } catch {
    message.error('Failed to load executions')
  } finally {
    execLoading.value = false
  }
}

const loadNodes = async (executionId: string) => {
  nodesLoading.value = true
  try {
    nodes.value = await taskApi.getNodes(executionId)
  } catch {
    message.error('Failed to load nodes')
  } finally {
    nodesLoading.value = false
  }
}

const clearNodesInterval = () => {
  if (nodesInterval !== null) {
    clearInterval(nodesInterval)
    nodesInterval = null
  }
}

const selectExecution = async (executionId: string) => {
  clearNodesInterval()
  selectedExecutionId.value = executionId
  await loadNodes(executionId)
  if (selectedExecutionRunning.value) {
    nodesInterval = setInterval(() => {
      if (selectedExecutionId.value) loadNodes(selectedExecutionId.value)
    }, 5000)
  }
}

watch(selectedExecutionRunning, running => {
  clearNodesInterval()
  if (running && selectedExecutionId.value) {
    nodesInterval = setInterval(() => {
      if (selectedExecutionId.value) loadNodes(selectedExecutionId.value)
    }, 5000)
  }
})

const triggerTask = async () => {
  triggering.value = true
  try {
    await taskApi.trigger(taskId)
    message.success('Task triggered')
    setTimeout(loadExecutions, 1000)
  } catch {
    message.error('Failed to trigger task')
  } finally {
    triggering.value = false
  }
}

const viewLogs = async (execId: string) => {
  try {
    logs.value = await taskApi.getLogs(execId)
    logsVisible.value = true
  } catch {
    message.error('Failed to load logs')
  }
}

const viewNodeSession = async (nodeExecutionId: string) => {
  if (!selectedExecutionId.value) return
  sessionLoading.value = true
  sessionVisible.value = true
  try {
    nodeSession.value = await taskApi.getNodeSession(selectedExecutionId.value, nodeExecutionId)
  } catch {
    message.error('Failed to load node session')
    nodeSession.value = null
  } finally {
    sessionLoading.value = false
  }
}

const approveNode = async (nodeExecutionId: string, approved: boolean) => {
  if (!selectedExecutionId.value) return
  try {
    await taskApi.approveNode(selectedExecutionId.value, nodeExecutionId, { approved })
    message.success(approved ? 'Node approved' : 'Node rejected')
    await loadNodes(selectedExecutionId.value)
  } catch {
    message.error('Failed to submit approval')
  }
}

const getStatusColor = (status?: number) => {
  const colors = ['', 'blue', 'green', 'red', 'default']
  return colors[status || 0] || ''
}

const getStatusText = (status?: number) => {
  const texts = ['Pending', 'Running', 'Completed', 'Failed', 'Cancelled']
  return texts[status || 0] || 'Unknown'
}

const getNodeStatusColor = (status: string) => {
  const map: Record<string, string> = {
    completed: 'green',
    running: 'blue',
    failed: 'red',
    waiting_approval: 'orange',
    retrying: 'gold',
    schema_validation_failed: 'red',
    pending: 'default',
    skipped: 'default',
  }
  return map[status] ?? 'default'
}

const getLogColor = (level: string) => {
  if (level === 'Error') return 'red'
  if (level === 'Warning') return 'orange'
  return 'blue'
}

const formatTime = (timestamp: string) => {
  return new Date(timestamp).toLocaleString()
}

onMounted(() => {
  loadTask()
  loadExecutions()
})

onBeforeUnmount(() => {
  clearNodesInterval()
})
</script>

<style scoped>
.task-detail-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.task-detail-error {
  color: red;
}

.nodes-toolbar {
  margin-bottom: 12px;
}

:deep(.selected-row) {
  background: var(--ant-primary-1, #e6f7ff);
}
</style>
