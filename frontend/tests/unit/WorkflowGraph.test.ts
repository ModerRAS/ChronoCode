import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import WorkflowGraph from '../../src/components/WorkflowGraph.vue'

// Mock @vue-flow/core — we only care about the props/slots passed to it
vi.mock('@vue-flow/core', () => ({
  VueFlow: {
    name: 'VueFlow',
    props: ['nodes', 'edges', 'nodesDraggable', 'nodesConnectable', 'elementsSelectable', 'zoomOnScroll', 'panOnScroll', 'fitViewOnInit'],
    template: '<div class="vue-flow-stub"><slot name="node-default" v-for="n in nodes" :key="n.id" v-bind="n" /></div>',
  },
}))

const simpleDefinition = {
  version: 1,
  startNodeId: 'start',
  nodes: [
    { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'prepare' },
    { type: 'prepare_workspace', nodeId: 'prepare', name: 'Prepare', nextNodeId: 'agent' },
    { type: 'agent', nodeId: 'agent', name: 'Agent', nextNodeId: 'end' },
    { type: 'end', nodeId: 'end', name: 'End' },
  ],
}

const conditionDefinition = {
  version: 1,
  startNodeId: 'start',
  nodes: [
    { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'cond' },
    { type: 'condition', nodeId: 'cond', name: 'Check', trueNodeId: 'agentT', falseNodeId: 'agentF' },
    { type: 'agent', nodeId: 'agentT', name: 'True', nextNodeId: 'end' },
    { type: 'agent', nodeId: 'agentF', name: 'False', nextNodeId: 'end' },
    { type: 'end', nodeId: 'end', name: 'End' },
  ],
}

const parallelDefinition = {
  version: 1,
  startNodeId: 'start',
  nodes: [
    { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'par' },
    { type: 'parallel', nodeId: 'par', name: 'Par', branchStartNodeIds: ['b1', 'b2'], nextNodeId: 'end' },
    { type: 'agent', nodeId: 'b1', name: 'B1', nextNodeId: 'end' },
    { type: 'agent', nodeId: 'b2', name: 'B2', nextNodeId: 'end' },
    { type: 'end', nodeId: 'end', name: 'End' },
  ],
}

const stubs = {
  'a-empty': { template: '<div class="a-empty"><slot /></div>' },
  'a-alert': { template: '<div class="a-alert" />' },
}

describe('WorkflowGraph.vue', () => {
  it('renders empty state when definition is null', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: null },
      global: { stubs },
    })

    expect(wrapper.find('.a-empty').exists()).toBe(true)
    expect(wrapper.find('.vue-flow-stub').exists()).toBe(false)
  })

  it('renders error when definition has no nodes', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: { version: 1, startNodeId: 'start', nodes: [] } },
      global: { stubs },
    })

    expect(wrapper.find('.a-alert').exists()).toBe(true)
    expect(wrapper.find('.vue-flow-stub').exists()).toBe(false)
  })

  it('renders graph canvas for valid definition', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: simpleDefinition },
      global: { stubs },
    })

    expect(wrapper.find('.vue-flow-stub').exists()).toBe(true)
    expect(wrapper.find('.a-empty').exists()).toBe(false)
    expect(wrapper.find('.a-alert').exists()).toBe(false)
  })

  it('renders node labels from definition', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: simpleDefinition },
      global: { stubs },
    })

    const nodeLabels = wrapper.findAll('.wf-node__label')
    expect(nodeLabels.length).toBe(4)
    expect(nodeLabels[0].text()).toBe('Start')
    expect(nodeLabels[1].text()).toBe('Prepare')
    expect(nodeLabels[2].text()).toBe('Agent')
    expect(nodeLabels[3].text()).toBe('End')
  })

  it('renders node type labels', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: simpleDefinition },
      global: { stubs },
    })

    const typeLabels = wrapper.findAll('.wf-node__type')
    expect(typeLabels[0].text()).toBe('Start')
    expect(typeLabels[1].text()).toBe('Workspace')
    expect(typeLabels[2].text()).toBe('Agent')
    expect(typeLabels[3].text()).toBe('End')
  })

  it('maps node execution statuses to status classes', () => {
    const wrapper = mount(WorkflowGraph, {
      props: {
        definition: simpleDefinition,
        nodeExecutions: [
          { id: 'n1', executionId: 'e1', nodeId: 'agent', nodeType: 'agent', scopeKey: 'root', attempt: 0, status: 'completed', startedAt: '2024-01-01T00:00:00Z', retryCount: 0 },
          { id: 'n2', executionId: 'e1', nodeId: 'start', nodeType: 'start', scopeKey: 'root', attempt: 0, status: 'completed', startedAt: '2024-01-01T00:00:00Z', retryCount: 0 },
        ],
      },
      global: { stubs },
    })

    const nodes = wrapper.findAll('.wf-node')
    const agentNode = nodes.find(n => n.find('.wf-node__label').text() === 'Agent')
    expect(agentNode?.classes()).toContain('wf-node--completed')
    expect(agentNode?.find('.wf-node__status').text()).toBe('completed')
  })

  it('renders waiting_approval status', () => {
    const wrapper = mount(WorkflowGraph, {
      props: {
        definition: {
          version: 1,
          startNodeId: 'start',
          nodes: [
            { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'gate' },
            { type: 'approval_gate', nodeId: 'gate', name: 'Gate', nextNodeId: 'end' },
            { type: 'end', nodeId: 'end', name: 'End' },
          ],
        },
        nodeExecutions: [
          { id: 'n1', executionId: 'e1', nodeId: 'gate', nodeType: 'approval_gate', scopeKey: 'root', attempt: 0, status: 'waiting_approval', startedAt: '2024-01-01T00:00:00Z', retryCount: 0 },
        ],
      },
      global: { stubs },
    })

    const gateNode = wrapper.findAll('.wf-node').find(n => n.find('.wf-node__label').text() === 'Gate')
    expect(gateNode?.classes()).toContain('wf-node--approval')
    expect(gateNode?.find('.wf-node__status').text()).toBe('awaiting approval')
  })

  it('handles condition node with true/false branches', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: conditionDefinition },
      global: { stubs },
    })

    // Should render all 5 nodes
    const labels = wrapper.findAll('.wf-node__label')
    expect(labels.length).toBe(5)
    expect(labels.some(l => l.text() === 'True')).toBe(true)
    expect(labels.some(l => l.text() === 'False')).toBe(true)
  })

  it('handles parallel node with branches', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: parallelDefinition },
      global: { stubs },
    })

    const labels = wrapper.findAll('.wf-node__label')
    expect(labels.length).toBe(5)
    expect(labels.some(l => l.text() === 'B1')).toBe(true)
    expect(labels.some(l => l.text() === 'B2')).toBe(true)
  })

  it('renders failed status class', () => {
    const wrapper = mount(WorkflowGraph, {
      props: {
        definition: simpleDefinition,
        nodeExecutions: [
          { id: 'n1', executionId: 'e1', nodeId: 'agent', nodeType: 'agent', scopeKey: 'root', attempt: 0, status: 'failed', startedAt: '2024-01-01T00:00:00Z', retryCount: 0 },
        ],
      },
      global: { stubs },
    })

    const agentNode = wrapper.findAll('.wf-node').find(n => n.find('.wf-node__label').text() === 'Agent')
    expect(agentNode?.classes()).toContain('wf-node--failed')
  })

  it('renders retrying status class', () => {
    const wrapper = mount(WorkflowGraph, {
      props: {
        definition: simpleDefinition,
        nodeExecutions: [
          { id: 'n1', executionId: 'e1', nodeId: 'agent', nodeType: 'agent', scopeKey: 'root', attempt: 1, status: 'retrying', startedAt: '2024-01-01T00:00:00Z', retryCount: 1 },
        ],
      },
      global: { stubs },
    })

    const agentNode = wrapper.findAll('.wf-node').find(n => n.find('.wf-node__label').text() === 'Agent')
    expect(agentNode?.classes()).toContain('wf-node--retrying')
  })
})

describe('WorkflowGraph computed properties', () => {
  const parallelDefinition = {
    version: 1,
    startNodeId: 'start',
    nodes: [
      { nodeId: 'start', nodeType: 'start', name: 'Start', nextNodeId: 'agent' },
      { nodeId: 'agent', nodeType: 'agent', name: 'Agent', nextNodeId: 'commit' },
      { nodeId: 'commit', nodeType: 'commit_changes', name: 'Commit', nextNodeId: null },
    ],
  }

  const stubs = {
    '@vue-flow/core': {
      VueFlow: { template: '<div class="vue-flow"><slot /></div>' },
      Background: { template: '<div class="vue-flow-bg" />' },
      Controls: { template: '<div />' },
      MiniMap: { template: '<div />' },
      Panel: { template: '<div><slot /></div>' },
    },
  }

  it('hasDefinition returns true when definition provided', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: parallelDefinition },
      global: { stubs },
    })
    expect(wrapper.exists()).toBe(true)
  })

  it('renders correct number of nodes', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: parallelDefinition },
      global: { stubs },
    })
    const nodes = wrapper.findAll('.wf-node')
    expect(nodes.length).toBe(3)
  })

  it('renders Start node with correct label', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: parallelDefinition },
      global: { stubs },
    })
    const labels = wrapper.findAll('.wf-node__label')
    expect(labels.some(l => l.text() === 'Start')).toBe(true)
  })

  it('renders node type labels for all nodes', () => {
    const wrapper = mount(WorkflowGraph, {
      props: { definition: parallelDefinition },
      global: { stubs },
    })
    const typeLabels = wrapper.findAll('.wf-node__type')
    expect(typeLabels.length).toBeGreaterThanOrEqual(3)
  })

  it('renders multiple node executions with different statuses', () => {
    const wrapper = mount(WorkflowGraph, {
      props: {
        definition: parallelDefinition,
        nodeExecutions: [
          { id: 'e1', executionId: 'exec-1', nodeId: 'start', nodeType: 'start', scopeKey: 'root', attempt: 0, status: 'completed', startedAt: '2024-01-01T00:00:00Z' },
          { id: 'e2', executionId: 'exec-1', nodeId: 'agent', nodeType: 'agent', scopeKey: 'root', attempt: 0, status: 'running', startedAt: '2024-01-01T00:00:00Z' },
          { id: 'e3', executionId: 'exec-1', nodeId: 'commit', nodeType: 'commit_changes', scopeKey: 'root', attempt: 0, status: 'pending', startedAt: '2024-01-01T00:00:00Z' },
        ],
      },
      global: { stubs },
    })

    const nodes = wrapper.findAll('.wf-node')
    expect(nodes.length).toBe(3)
    // Start should be completed
    const startNode = nodes.find(n => n.find('.wf-node__label').text() === 'Start')
    expect(startNode?.classes()).toContain('wf-node--completed')
    // Agent should be running
    const agentNode = nodes.find(n => n.find('.wf-node__label').text() === 'Agent')
    expect(agentNode?.classes()).toContain('wf-node--running')
  })
})
