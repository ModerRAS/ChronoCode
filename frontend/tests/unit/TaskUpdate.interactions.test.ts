import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ref } from 'vue';
import { mount, flushPromises } from '@vue/test-utils';
import TaskUpdate from '../../src/views/TaskUpdate.vue';

const mockPush = vi.fn();
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
  useRoute: () => ({ params: { id: 'task-456' } }),
}));

const { mockMessage } = vi.hoisted(() => ({
  mockMessage: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

vi.mock('ant-design-vue', () => ({
  message: mockMessage,
}));

const { mockTaskApi } = vi.hoisted(() => ({
  mockTaskApi: {
    getById: vi.fn(),
    update: vi.fn(),
  },
}));

vi.mock('../../src/api/tasks', () => ({
  taskApi: mockTaskApi,
}));

vi.mock('../../src/composables/useIsMobile', () => ({
  useIsMobile: () => ({ isMobile: ref(false) }),
}));

vi.mock('../../src/components/WorkflowGraph.vue', () => ({
  default: {
    name: 'WorkflowGraph',
    template: '<div class="workflow-graph-stub" />',
    props: ['definition'],
  },
}));

import { message } from 'ant-design-vue';

const fakeTask = {
  id: 'task-456',
  name: 'Existing Task',
  cronExpression: '0 0 * * *',
  repositoryUrl: 'https://github.com/test/repo',
  baseBranch: 'main',
  branchStrategy: 0,
  maxRuntimeSeconds: 600,
  maxFileChanges: 50,
  isEnabled: true,
  workflowDefinitionJson: JSON.stringify({ version: 1, startNodeId: 'start', nodes: [] }),
  defaultInputsJson: null,
  runtimeBackend: 'pi',
  maxConcurrentRuns: 1,
  nodeFailurePolicyJson: '{}',
  createdAt: '2024-01-01T00:00:00Z',
  lastStatus: 0,
  schedulerStatus: 'idle',
};

const stubs = {
  'a-form': { template: '<form class="a-form" @submit.prevent="$emit(\'finish\')"><slot /></form>', emits: ['finish'] },
  'a-form-item': { template: '<div class="a-form-item"><slot /></div>', props: ['label', 'name'] },
  'a-input': { template: '<input class="a-input" />', props: ['value'], emits: ['update:value'] },
  'a-input-number': { template: '<input class="a-input-number" type="number" />', props: ['value'], emits: ['update:value'] },
  'a-textarea': { template: '<textarea class="a-textarea" />', props: ['value'], emits: ['update:value'] },
  'a-select': { template: '<select class="a-select"><slot /></select>', props: ['value'], emits: ['update:value'] },
  'a-select-option': { template: '<option class="a-select-option"><slot /></option>', props: ['value'] },
  'a-switch': { template: '<span class="a-switch" />', props: ['checked'] },
  'a-button': { template: '<button class="a-btn" @click="$emit(\'click\')"><slot /></button>', emits: ['click'] },
  'a-card': { template: '<div class="a-card"><slot /></div>' },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-input-search': { template: '<input class="a-input-search" />', props: ['value'], emits: ['update:value'] },
};

function mountUpdate() {
  return mount(TaskUpdate, { global: { stubs, mocks: { $router: { push: mockPush } } } });
}

describe('TaskUpdate.vue interactions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockTaskApi.getById.mockResolvedValue(fakeTask);
    mockTaskApi.update.mockResolvedValue(fakeTask);
  })

  it('shows error and redirects when task load fails', async () => {
    mockTaskApi.getById.mockRejectedValue(new Error('not found'))

    mountUpdate()
    await flushPromises()

    expect(mockMessage.error).toHaveBeenCalledWith('Failed to load task')
    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('format JSON button formats valid JSON', async () => {
    const wrapper = mountUpdate()
    await flushPromises()

    const formatBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Format JSON'))
    await formatBtn!.trigger('click')

    expect(mockMessage.success).toHaveBeenCalledWith('JSON formatted')
  })

  it('load default workflow button loads default', async () => {
    const wrapper = mountUpdate()
    await flushPromises()

    const loadBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Load default workflow'))
    await loadBtn!.trigger('click')

    expect(mockMessage.info).toHaveBeenCalledWith('Loaded default workflow')
  })

  it('Cancel button navigates to home', async () => {
    const wrapper = mountUpdate()
    await flushPromises()

    const cancelBtn = wrapper.findAll('.a-btn').find(b => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')

    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('Update button calls taskApi.update', async () => {
    const wrapper = mountUpdate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockTaskApi.update).toHaveBeenCalled()
  })

  it('shows success message after update', async () => {
    const wrapper = mountUpdate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockMessage.success).toHaveBeenCalledWith('Task updated successfully')
  })

  it('navigates to home after successful update', async () => {
    const wrapper = mountUpdate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('shows error on update failure', async () => {
    mockTaskApi.update.mockRejectedValue(new Error('server error'))

    const wrapper = mountUpdate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockMessage.error).toHaveBeenCalledWith('Failed to update task')
  })

  it('loads task data with correct id from route', async () => {
    mountUpdate()
    await flushPromises()

    expect(mockTaskApi.getById).toHaveBeenCalledWith('task-456')
  })
})
