import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ref } from 'vue';
import { mount, flushPromises } from '@vue/test-utils';
import TaskDetail from '../../src/views/TaskDetail.vue';

// Mock router
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useRoute: () => ({ params: { id: 'task-123' } }),
}));

// Mock ant-design-vue message
vi.mock('ant-design-vue', () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock taskApi
const mockGetById = vi.fn();
const mockGetExecutions = vi.fn();
const mockGetNodes = vi.fn();
const mockTrigger = vi.fn();
const mockApproveNode = vi.fn();
vi.mock('../../src/api/tasks', () => ({
  taskApi: {
    getById: (...a: unknown[]) => mockGetById(...a),
    getExecutions: (...a: unknown[]) => mockGetExecutions(...a),
    getNodes: (...a: unknown[]) => mockGetNodes(...a),
    trigger: (...a: unknown[]) => mockTrigger(...a),
    approveNode: (...a: unknown[]) => mockApproveNode(...a),
  },
}));

// Mock useIsMobile
vi.mock('../../src/composables/useIsMobile', () => ({
  useIsMobile: () => ({ isMobile: ref(false) }),
}));

// Stub WorkflowGraph
vi.mock('../../src/components/WorkflowGraph.vue', () => ({
  default: {
    name: 'WorkflowGraph',
    props: ['definition', 'nodeExecutions'],
    template: '<div class="workflow-graph-stub" />',
  },
}));

const stubs = {
  'a-page-header': { template: '<div class="page-header"><slot name="extra" /><slot /></div>' },
  'a-button': { template: '<button class="a-btn" @click="$emit(\'click\')"><slot /></button>', emits: ['click'] },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-table': { template: '<table class="a-table" />', props: ['dataSource', 'columns', 'loading', 'rowClassName'] },
  'a-tag': { template: '<span class="a-tag"><slot /></span>' },
  'a-card': { template: '<div class="a-card"><slot /></div>' },
  'a-space': { template: '<span class="a-space"><slot /></span>' },
  'a-descriptions': { template: '<div class="a-descriptions"><slot /></div>' },
  'a-descriptions-item': { template: '<div class="a-desc-item"><slot /></div>' },
  'a-modal': { template: '<div class="a-modal"><slot /></div>' },
  'a-badge': { template: '<span class="a-badge"><slot /></span>' },
  'a-tooltip': { template: '<span class="a-tooltip"><slot /></span>' },
  'a-row': { template: '<div class="a-row"><slot /></div>' },
  'a-col': { template: '<div class="a-col"><slot /></div>' },
};

function mountDetail() {
  return mount(TaskDetail, { global: { stubs } });
}

const fakeTask = {
  id: 'task-123',
  name: 'Test Task',
  cronExpression: '0 0 * * *',
  repositoryUrl: 'https://github.com/test/repo',
  baseBranch: 'main',
  workflowDefinitionJson: JSON.stringify({ version: 1, startNodeId: 'start', nodes: {} }),
  lastStatus: 0,
  schedulerStatus: 'idle',
  isEnabled: true,
  createdAt: '2024-01-01T00:00:00Z',
};

describe('TaskDetail.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetById.mockResolvedValue(fakeTask);
    mockGetExecutions.mockResolvedValue([]);
    mockGetNodes.mockResolvedValue([]);
    mockTrigger.mockResolvedValue({});
    mockApproveNode.mockResolvedValue({});
  });

  it('loads task on mount', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(mockGetById).toHaveBeenCalledWith('task-123');
  });

  it('loads executions on mount', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(mockGetExecutions).toHaveBeenCalledWith('task-123');
  });

  it('renders workflow graph preview', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(wrapper.find('.workflow-graph-stub').exists()).toBe(true);
  });

  it('renders page header', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(wrapper.find('.page-header').exists()).toBe(true);
  });

  it('has Run Now button', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Run Now'))).toBe(true);
  });

  it('trigger button calls taskApi.trigger', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    const runBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Run Now'));
    await runBtn!.trigger('click');
    await flushPromises();

    expect(mockTrigger).toHaveBeenCalledWith('task-123');
  });

  it('renders descriptions card for task info', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(wrapper.find('.a-descriptions').exists()).toBe(true);
  });

  it('renders execution table', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(wrapper.findAll('.a-table').length).toBeGreaterThanOrEqual(1);
  });

  it('renders task name in header', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(wrapper.text()).toContain('Test Task');
  });

  it('renders cron expression', async () => {
    const wrapper = mountDetail();
    await flushPromises();

    expect(wrapper.text()).toContain('0 0 * * *');
  });
});
