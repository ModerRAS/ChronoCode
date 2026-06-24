import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import TaskList from '../../src/views/TaskList.vue'

const { mockTaskApi, mockMessage } = vi.hoisted(() => ({
  mockTaskApi: {
    getAll: vi.fn(),
    trigger: vi.fn(),
    delete: vi.fn(),
  },
  mockMessage: { success: vi.fn(), error: vi.fn() },
}))

vi.mock('ant-design-vue', () => ({
  message: mockMessage,
}))

vi.mock('@ant-design/icons-vue', () => ({
  RobotOutlined: { template: '<span>robot</span>' },
}))

vi.mock('../../src/api/tasks', () => ({
  taskApi: mockTaskApi,
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
}))

vi.mock('../../src/composables/useIsMobile', () => ({
  useIsMobile: () => ({ isMobile: { value: false } }),
}))

import { message } from 'ant-design-vue'

const stubs = {
  'a-button': { template: '<button class="a-btn" @click="$emit(\'click\')"><slot /></button>', emits: ['click'] },
  'a-empty': { template: '<div class="a-empty"><slot name="image" /><slot /></div>' },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-table': { template: '<div class="a-table"><slot name="bodyCell" /></div>' },
  'a-tag': { template: '<span class="a-tag">{{ color }}<slot /></span>' },
  'a-switch': { template: '<span class="a-switch" />' },
  'a-card': { template: '<div class="a-card"><slot name="title" /><slot /></div>' },
  'a-space': { template: '<div class="a-space"><slot /></div>' },
  'a-avatar': { template: '<span class="a-avatar"><slot /></span>' },
}

const mockRouter = { push: vi.fn() }

function makeTask(overrides: Partial<any> = {}) {
  return {
    id: 'task-1',
    name: 'Test Task',
    cronExpression: '0 0 * * *',
    repositoryUrl: 'https://github.com/test/repo',
    lastStatus: 0,
    isEnabled: true,
    lastRunAt: null,
    ...overrides,
  }
}

describe('TaskList interactions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockTaskApi.getAll.mockResolvedValue([])
    mockTaskApi.trigger.mockResolvedValue({})
    mockTaskApi.delete.mockResolvedValue({})
  })

  it('shows error message when loading tasks fails', async () => {
    mockTaskApi.getAll.mockRejectedValue(new Error('network'))
    mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    expect(message.error).toHaveBeenCalledWith('Failed to load tasks')
  })

  it('triggerTask calls taskApi.trigger with id', async () => {
    const task = makeTask({ id: 'trigger-1' })
    mockTaskApi.getAll.mockResolvedValue([task])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    // Find the Run button in the table actions
    const buttons = wrapper.findAll('.a-btn')
    const runBtn = buttons.find(b => b.text() === 'Run')
    expect(runBtn).toBeDefined()
    await runBtn!.trigger('click')
    await flushPromises()

    expect(mockTaskApi.trigger).toHaveBeenCalledWith('trigger-1')
    expect(message.success).toHaveBeenCalledWith('Task triggered')
  })

  it('triggerTask shows error on failure', async () => {
    mockTaskApi.trigger.mockRejectedValue(new Error('fail'))
    const task = makeTask({ id: 'trigger-err' })
    mockTaskApi.getAll.mockResolvedValue([task])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    const runBtn = wrapper.findAll('.a-btn').find(b => b.text() === 'Run')
    await runBtn!.trigger('click')
    await flushPromises()

    expect(message.error).toHaveBeenCalledWith('Failed to trigger task')
  })

  it('deleteTask calls taskApi.delete and reloads', async () => {
    const task = makeTask({ id: 'delete-1' })
    mockTaskApi.getAll.mockResolvedValue([task])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    mockTaskApi.getAll.mockClear()
    mockTaskApi.getAll.mockResolvedValue([])

    const deleteBtn = wrapper.findAll('.a-btn').find(b => b.text() === 'Delete')
    await deleteBtn!.trigger('click')
    await flushPromises()

    expect(mockTaskApi.delete).toHaveBeenCalledWith('delete-1')
    expect(message.success).toHaveBeenCalledWith('Task deleted')
    expect(mockTaskApi.getAll).toHaveBeenCalled()
  })

  it('deleteTask shows error on failure', async () => {
    mockTaskApi.delete.mockRejectedValue(new Error('fail'))
    const task = makeTask({ id: 'delete-err' })
    mockTaskApi.getAll.mockResolvedValue([task])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    const deleteBtn = wrapper.findAll('.a-btn').find(b => b.text() === 'Delete')
    await deleteBtn!.trigger('click')
    await flushPromises()

    expect(message.error).toHaveBeenCalledWith('Failed to delete task')
  })

  it('edit button navigates to edit page', async () => {
    const task = makeTask({ id: 'edit-1' })
    mockTaskApi.getAll.mockResolvedValue([task])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    const editBtn = wrapper.findAll('.a-btn').find(b => b.text() === 'Edit')
    await editBtn!.trigger('click')

    expect(mockRouter.push).toHaveBeenCalledWith('/tasks/edit-1/edit')
  })

  it('renders task name in table', async () => {
    const task = makeTask({ name: 'My Custom Task' })
    mockTaskApi.getAll.mockResolvedValue([task])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    expect(wrapper.html()).toContain('My Custom Task')
  })

  it('renders empty state message', async () => {
    mockTaskApi.getAll.mockResolvedValue([])
    const wrapper = mount(TaskList, {
      global: { stubs, mocks: { $router: mockRouter } }
    })
    await flushPromises()

    expect(wrapper.html()).toContain('No tasks yet')
    expect(wrapper.html()).toContain('Create your first task to get started')
  })
})
