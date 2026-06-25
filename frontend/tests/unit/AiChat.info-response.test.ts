import { describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import AiChat from '../../src/views/AiChat.vue'

const messages = ref<Array<{ id: string; role: 'user' | 'ai'; content: string; timestamp: Date }>>([])
const isLoading = ref(false)
const error = ref<string | null>(null)

const sendMessage = vi.fn(async (content: string) => {
  messages.value.push({
    id: 'user-1',
    role: 'user',
    content,
    timestamp: new Date(),
  })
  messages.value.push({
    id: 'ai-1',
    role: 'ai',
    content: 'Here is some help',
    timestamp: new Date(),
  })
})

const clearChat = vi.fn(async () => {
  messages.value = []
})

const loadConversation = vi.fn(async () => {})

vi.mock('../../src/composables/useAIChat', () => ({
  useAIChat: () => ({
    messages,
    isLoading,
    error,
    sendMessage,
    clearChat,
    loadConversation,
  }),
}))

const InputSearchStub = {
  props: ['value', 'placeholder', 'enterButton', 'loading'],
  emits: ['search', 'update:value'],
  template: `
    <div>
      <input class="search-input" :value="value" @input="$emit('update:value', $event.target.value)" />
      <button class="search-button" @click="$emit('search')">Send</button>
    </div>
  `,
}

function mountChat() {
  return mount(AiChat, {
    global: {
      stubs: {
        'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
        'a-button': { template: '<button><slot /></button>' },
        'a-avatar': { template: '<span class="a-avatar" />' },
        'a-input-search': InputSearchStub,
        'a-spin': { template: '<div class="a-spin" />' },
        'a-alert': { template: '<div class="a-alert" />' },
        'a-tag': { template: '<button class="a-tag"><slot /></button>' },
        'a-space': { template: '<div class="a-space"><slot /></div>' },
        'a-divider': { template: '<div class="a-divider" />' },
      },
    },
  })
}

describe('AiChat info responses', () => {
  it('renders natural language responses without action buttons', async () => {
    messages.value = []
    error.value = null
    sendMessage.mockClear()

    const wrapper = mountChat()

    await wrapper.find('.a-tag').trigger('click')
    await wrapper.find('.search-button').trigger('click')
    await flushPromises()

    expect(sendMessage).toHaveBeenCalledTimes(1)
    expect(wrapper.find('.action-buttons').exists()).toBe(false)
    expect(wrapper.text()).toContain('Here is some help')
  })
})

describe('AiChat common behavior', () => {
  it('renders error alert when error is set', async () => {
    messages.value = []
    error.value = 'Something went wrong'

    const wrapper = mountChat()

    expect(wrapper.find('.a-alert').exists()).toBe(true)
  })

  it('clears messages when clear button is clicked', async () => {
    messages.value = [
      { id: 'm1', role: 'user', content: 'hello', timestamp: new Date() },
      { id: 'm2', role: 'ai', content: 'Hi there', timestamp: new Date() },
    ]
    error.value = null

    const wrapper = mountChat()
    const buttons = wrapper.findAll('button')
    const clearBtn = buttons.find(b => b.text().includes('Clear'))
    expect(clearBtn).toBeDefined()

    await clearBtn!.trigger('click')

    expect(messages.value.length).toBe(0)
  })

  it('shows loading spinner when isLoading is true', async () => {
    messages.value = []
    error.value = null
    isLoading.value = true

    const wrapper = mountChat()

    expect(wrapper.find('.a-spin').exists()).toBe(true)

    isLoading.value = false
  })
})
