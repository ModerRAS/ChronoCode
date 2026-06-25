import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ref } from 'vue';
import { mount, flushPromises } from '@vue/test-utils';
import TaskList from '../../src/views/TaskList.vue';
import type { ScheduledTask } from '../../src/api/tasks';

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
const mockGetAll = vi.fn();
const mockDelete = vi.fn();
const mockTrigger = vi.fn();
vi.mock('../../src/api/tasks', () => ({
  taskApi: {
    getAll: (...args: unknown[]) => mockGetAll(...args),
    delete: (...args: unknown[]) => mockDelete(...args),
    trigger: (...args: unknown[]) => mockTrigger(...args),
  },
}));

// Mock useIsMobile
vi.mock('../../src/composables/useIsMobile', () => ({
  useIsMobile: () => ({ isMobile: ref(false) }),
}));

const mockPush = vi.fn();

const stubs = {
  'a-button': { template: '<button class="a-btn" @click="$emit(\'click\')"><slot /></button>', emits: ['click'] },
  'a-empty': { template: '<div class="a-empty"><slot /></div>' },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-table': { template: '<div class="a-table" />', props: ['dataSource', 'columns', 'loading'] },
  'a-tag': { template: '<span class="a-tag"><slot /></span>' },
  'a-space': { template: '<span class="a-space"><slot /></span>' },
  'a-card': { template: '<div class="a-card"><slot /></div>' },
  'a-avatar': { template: '<span class="a-avatar" />' },
  'a-popconfirm': { template: '<span class="a-popconfirm"><slot /></span>' },
  'RobotOutlined': { template: '<span class="robot-icon" />' },
};

function mountList() {
  return mount(TaskList, {
    global: {
      stubs,
      mocks: {
        $router: { push: mockPush },
      },
    },
  });
}

const fakeTask = (overrides: Partial<ScheduledTask> = {}): ScheduledTask => ({
  id: 'task-1',
  name: 'Test Task',
  cronExpression: '0 0 * * *',
  repositoryUrl: 'https://github.com/test/repo',
  baseBranch: 'main',
  branchStrategy: 0,
  maxRuntimeSeconds: 600,
  maxFileChanges: 50,
  isEnabled: true,
  workflowVersion: 1,
  workflowDefinitionJson: '{}',
  maxConcurrentRuns: 1,
  nodeFailurePolicyJson: '{}',
  createdAt: '2024-01-01T00:00:00Z',
  lastStatus: 2,
  schedulerStatus: 'idle',
  ...overrides,
});

describe('TaskList.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows empty state when no tasks', async () => {
    mockGetAll.mockResolvedValue([]);
    const wrapper = mountList();
    await flushPromises();

    expect(wrapper.find('.empty-state').exists()).toBe(true);
  });

  it('shows create task button in toolbar', () => {
    mockGetAll.mockResolvedValue([]);
    const wrapper = mountList();
    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Create Task'))).toBe(true);
  });

  it('shows refresh button', () => {
    mockGetAll.mockResolvedValue([]);
    const wrapper = mountList();
    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Refresh'))).toBe(true);
  });

  it('loads tasks on mount', async () => {
    mockGetAll.mockResolvedValue([fakeTask()]);
    const wrapper = mountList();
    await flushPromises();

    expect(mockGetAll).toHaveBeenCalledTimes(1);
    expect(wrapper.find('.a-empty').exists()).toBe(false);
  });

  it('refresh button reloads tasks', async () => {
    mockGetAll.mockResolvedValue([fakeTask()]);
    const wrapper = mountList();
    await flushPromises();

    const refreshBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Refresh'));
    await refreshBtn!.trigger('click');
    await flushPromises();

    expect(mockGetAll).toHaveBeenCalledTimes(2);
  });

  it('create task button navigates to new task page', async () => {
    mockGetAll.mockResolvedValue([]);
    const wrapper = mountList();
    await flushPromises();

    const createBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Create Task'));
    await createBtn!.trigger('click');

    expect(mockPush).toHaveBeenCalledWith('/tasks/new');
  });

  it('does not show empty state when tasks exist', async () => {
    mockGetAll.mockResolvedValue([fakeTask(), fakeTask({ id: 't2', name: 'Task 2' })]);
    const wrapper = mountList();
    await flushPromises();

    expect(wrapper.find('.empty-state').exists()).toBe(false);
  });
});
