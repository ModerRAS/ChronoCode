import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import TaskCreate from '../../src/views/TaskCreate.vue'

const mockPush = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: mockPush }),
}))

const { mockMessage } = vi.hoisted(() => ({
  mockMessage: { success: vi.fn(), error: vi.fn(), info: vi.fn() },
}))
vi.mock('ant-design-vue', () => ({ message: mockMessage }))

const { mockTaskApi } = vi.hoisted(() => ({
  mockTaskApi: { create: vi.fn() },
}))
vi.mock('../../src/api/tasks', () => ({ taskApi: mockTaskApi }))

vi.mock('../../src/composables/useIsMobile', () => ({
  useIsMobile: () => ({ isMobile: { value: false } }),
}))

vi.mock('../../src/components/WorkflowGraph.vue', () => ({
  default: { name: 'WorkflowGraph', template: '<div class="workflow-graph-stub" />', props: ['definition'] },
}))

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
}

function mountCreate() {
  return mount(TaskCreate, { global: { stubs, mocks: { $router: { push: mockPush } } } })
}

describe('TaskCreate.vue interactions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockTaskApi.create.mockResolvedValue({})
  })

  it('format JSON button shows success on valid JSON', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    const formatBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Format JSON'))
    await formatBtn!.trigger('click')

    expect(mockMessage.success).toHaveBeenCalledWith('JSON formatted')
  })

  it('load default workflow button shows info message', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    const loadBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Load default workflow'))
    await loadBtn!.trigger('click')

    expect(mockMessage.info).toHaveBeenCalledWith('Loaded default workflow')
  })

  it('Cancel button navigates to home', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    const cancelBtn = wrapper.findAll('.a-btn').find(b => b.text() === 'Cancel')
    await cancelBtn!.trigger('click')

    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('form submit calls taskApi.create', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockTaskApi.create).toHaveBeenCalled()
  })

  it('shows success message after creating task', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockMessage.success).toHaveBeenCalledWith('Task created successfully')
  })

  it('navigates to home after creating task', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockPush).toHaveBeenCalledWith('/')
  })

  it('shows error on create failure', async () => {
    mockTaskApi.create.mockRejectedValue(new Error('server error'))

    const wrapper = mountCreate()
    await flushPromises()

    await wrapper.find('.a-form').trigger('submit')
    await flushPromises()

    expect(mockMessage.error).toHaveBeenCalledWith('Failed to create task')
  })

  it('has Create Task submit button', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    const submitBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Create Task'))
    expect(submitBtn).toBeDefined()
  })

  it('renders form sections for schedule and workflow', async () => {
    const wrapper = mountCreate()
    await flushPromises()

    const sections = wrapper.findAll('.form-section')
    expect(sections.length).toBeGreaterThanOrEqual(2)
  })
})
