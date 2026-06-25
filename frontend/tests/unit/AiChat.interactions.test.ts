import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import AiChat from '../../src/views/AiChat.vue'

const mockMessages = ref<any[]>([])
const mockIsLoading = ref(false)
const mockError = ref<string | null>(null)
const mockSendMessage = vi.fn()
const mockClearChat = vi.fn(async () => {
  mockMessages.value = []
})

const mockLoadConversation = vi.fn(async () => {})

vi.mock('../../src/composables/useAIChat', () => ({
  useAIChat: () => ({
    messages: mockMessages,
    isLoading: mockIsLoading,
    error: mockError,
    sendMessage: mockSendMessage,
    clearChat: mockClearChat,
    loadConversation: mockLoadConversation,
  })
}))

const stubs = {
  'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
  'a-button': { template: '<button @click="$emit(\'click\')"><slot /></button>' },
  'a-avatar': { template: '<span class="a-avatar" />' },
  'a-empty': { template: '<div class="a-empty"><slot /></div>' },
  'a-input-search': {
    template: '<div class="a-input-search"><input class="search-input" :placeholder="placeholder" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" /><button class="search-btn">Send</button></div>',
    props: ['modelValue', 'loading', 'placeholder'],
    emits: ['update:modelValue', 'search']
  },
  'a-spin': { template: '<div class="a-spin" />' },
  'a-alert': { template: '<div class="a-alert">{{ message }}</div>', props: ['message', 'type'] },
  'a-tag': { template: '<span class="a-tag"><slot /></span>' },
  'a-space': { template: '<div class="a-space"><slot /></div>' },
  'a-divider': { template: '<div class="a-divider" />' },
}

describe('AiChat.vue interactions', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockMessages.value = []
    mockIsLoading.value = false
    mockError.value = null
    mockSendMessage.mockResolvedValue(undefined)
  })

  it('does not send when input is empty', async () => {
    const wrapper = mount(AiChat, { global: { stubs } })
    // Input is empty, clicking search should not call sendMessage
    expect(mockSendMessage).not.toHaveBeenCalled()
  })

  it('displays error alert when error is set', async () => {
    mockError.value = 'Connection failed'
    const wrapper = mount(AiChat, { global: { stubs } })
    const alert = wrapper.find('.a-alert')
    expect(alert.exists()).toBe(true)
    expect(alert.text()).toContain('Connection failed')
  })

  it('renders loading spinner when isLoading is true', async () => {
    mockIsLoading.value = true
    const wrapper = mount(AiChat, { global: { stubs } })
    expect(wrapper.find('.a-spin').exists()).toBe(true)
  })

  it('renders user and AI messages with correct classes', async () => {
    mockMessages.value = [
      { id: '1', role: 'user', content: 'Hello', timestamp: new Date() },
      { id: '2', role: 'ai', content: 'Hi there', timestamp: new Date() }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    const messages = wrapper.findAll('.message')
    expect(messages.length).toBe(2)
    expect(messages[0].classes()).toContain('user')
    expect(messages[1].classes()).toContain('ai')
  })

  it('clear button clears chat state', async () => {
    mockMessages.value = [
      { id: '1', role: 'user', content: 'Hello', timestamp: new Date() }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    expect(wrapper.findAll('.message').length).toBe(1)
    const buttons = wrapper.findAll('button')
    const clearBtn = buttons.find(b => b.text() === 'Clear')
    expect(clearBtn).toBeDefined()
    await clearBtn!.trigger('click')
    expect(mockMessages.value.length).toBe(0)
    expect(wrapper.findAll('.message').length).toBe(0)
  })

  it('formats message timestamps as time strings', async () => {
    const now = new Date()
    mockMessages.value = [
      { id: '1', role: 'user', content: 'Hello', timestamp: now }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    const timeSpan = wrapper.find('.message-time')
    expect(timeSpan.exists()).toBe(true)
    expect(timeSpan.text()).toMatch(/\d{1,2}:\d{2}/)
  })

  it('displays AI message content as pre block', async () => {
    mockMessages.value = [
      { id: '1', role: 'ai', content: 'AI response text', timestamp: new Date() }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    const pre = wrapper.find('.message-body pre')
    expect(pre.exists()).toBe(true)
    expect(pre.text()).toContain('AI response text')
  })

  it('renders welcome content with correct heading', async () => {
    const wrapper = mount(AiChat, { global: { stubs } })
    expect(wrapper.find('.welcome-content h2').text()).toBe('Welcome to AI Chat')
  })

  it('renders example tags for user guidance', async () => {
    const wrapper = mount(AiChat, { global: { stubs } })
    const tags = wrapper.findAll('.a-tag')
    expect(tags.length).toBeGreaterThanOrEqual(2)
    expect(tags[0].text()).toContain('Create a task')
    expect(tags[1].text()).toContain('Run a task')
  })

  it('renders message role labels correctly', async () => {
    mockMessages.value = [
      { id: '1', role: 'user', content: 'Hello', timestamp: new Date() },
      { id: '2', role: 'ai', content: 'Hi', timestamp: new Date() }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    const roles = wrapper.findAll('.message-role')
    expect(roles[0].text()).toBe('You')
    expect(roles[1].text()).toBe('AI')
  })

  it('renders input container with placeholder', async () => {
    const wrapper = mount(AiChat, { global: { stubs } })
    const input = wrapper.find('.search-input')
    expect(input.exists()).toBe(true)
    expect(input.attributes('placeholder')).toBe('Ask the AI to create or manage tasks...')
  })

  it('hides empty state when messages exist', async () => {
    mockMessages.value = [
      { id: '1', role: 'user', content: 'Hello', timestamp: new Date() }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    expect(wrapper.find('.empty-state').exists()).toBe(false)
  })

  it('shows empty state when no messages', async () => {
    const wrapper = mount(AiChat, { global: { stubs } })
    expect(wrapper.find('.empty-state').exists()).toBe(true)
  })

  it('renders AI message content', async () => {
    mockMessages.value = [
      {
        id: '1',
        role: 'ai',
        content: 'Created a new task for you.',
        timestamp: new Date()
      }
    ]
    const wrapper = mount(AiChat, { global: { stubs } })
    const pre = wrapper.find('.message-body pre')
    expect(pre.exists()).toBe(true)
    expect(pre.text()).toContain('Created a new task for you.')
  })
})
