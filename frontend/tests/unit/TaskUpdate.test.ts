import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ref } from 'vue';
import { mount, flushPromises } from '@vue/test-utils';
import TaskUpdate from '../../src/views/TaskUpdate.vue';

// Mock router
const mockPush = vi.fn();
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
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
const mockUpdate = vi.fn();
vi.mock('../../src/api/tasks', () => ({
  taskApi: {
    getById: (...args: unknown[]) => mockGetById(...args),
    update: (...args: unknown[]) => mockUpdate(...args),
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
  'a-page-header': { template: '<div class="page-header"><slot /></div>' },
  'a-form': { template: '<div class="a-form"><slot /></div>' },
  'a-form-item': { template: '<div class="a-form-item"><slot /></div>' },
  'a-input': { template: '<input class="a-input" />', props: ['value'] },
  'a-input-number': { template: '<input type="number" class="a-input-number" />', props: ['value'] },
  'a-textarea': { template: '<textarea class="a-textarea" />', props: ['value'] },
  'a-select': { template: '<select class="a-select"><slot /></select>', props: ['value'] },
  'a-select-option': { template: '<option><slot /></option>' },
  'a-switch': { template: '<button class="a-switch" />', props: ['checked'] },
  'a-button': { template: '<button class="a-btn"><slot /></button>' },
  'a-space': { template: '<span class="a-space"><slot /></span>' },
  'a-card': { template: '<div class="a-card"><slot /></div>' },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-row': { template: '<div class="a-row"><slot /></div>' },
  'a-col': { template: '<div class="a-col"><slot /></div>' },
};

function mountUpdate() {
  return mount(TaskUpdate, { global: { stubs } });
}

const fakeTask = {
  id: 'task-123',
  name: 'Existing Task',
  cronExpression: '0 0 * * *',
  repositoryUrl: 'https://github.com/test/repo',
  baseBranch: 'main',
  branchStrategy: 0,
  maxRuntimeSeconds: 600,
  maxFileChanges: 50,
  isEnabled: true,
  workflowVersion: 1,
  workflowDefinitionJson: JSON.stringify({ version: 1, startNodeId: 'start', nodes: [] }),
  maxConcurrentRuns: 1,
  nodeFailurePolicyJson: '{}',
  runtimeBackend: 'pi',
  createdAt: '2024-01-01T00:00:00Z',
  lastStatus: 0,
  schedulerStatus: 'idle',
};

describe('TaskUpdate.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetById.mockResolvedValue(fakeTask);
  });

  it('loads task data on mount', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    expect(mockGetById).toHaveBeenCalledWith('task-123');
  });

  it('renders form after loading', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    expect(wrapper.find('.a-form').exists()).toBe(true);
  });

  it('renders workflow graph preview', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    expect(wrapper.find('.workflow-graph-stub').exists()).toBe(true);
  });

  it('has Load default workflow button', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Load default workflow'))).toBe(true);
  });

  it('has Format JSON button', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Format JSON'))).toBe(true);
  });

  it('renders two-section layout (form-section cards)', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    const sections = wrapper.findAll('.form-section');
    expect(sections.length).toBeGreaterThanOrEqual(2);
  });

  it('has submit button', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Update') || b.text().includes('Save'))).toBe(true);
  });

  it('has workflow textarea', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    expect(wrapper.find('.a-textarea').exists()).toBe(true);
  });

  it('has runtime backend select', async () => {
    const wrapper = mountUpdate();
    await flushPromises();

    expect(wrapper.find('.a-select').exists()).toBe(true);
  });
});
