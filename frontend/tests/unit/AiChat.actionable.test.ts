import { describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { ref } from 'vue'
import AiChat from '../../src/views/AiChat.vue'

const messages = ref<Array<{ id: string; role: 'user' | 'ai'; content: string; timestamp: Date }>>([])
const isLoading = ref(false)
const error = ref<string | null>(null)

const sendMessage = vi.fn(async (content: string) => {
  messages.value.push({ id: 'user-' + Date.now(), role: 'user', content, timestamp: new Date() })
  messages.value.push({ id: 'ai-' + Date.now(), role: 'ai', content: '{"action":"","task":null,"error":{"code":"INFO","message":"help"}}', timestamp: new Date() })
  return null
})

vi.mock('../../src/composables/useAIChat', () => ({
  useAIChat: () => ({ messages, isLoading, error, sendMessage }),
}))

const InputSearchStub = {
  props: ['value', 'placeholder', 'enterButton', 'loading'],
  emits: ['search', 'update:value'],
  template: `<div><input class="search-input" :value="value" @input="$emit('update:value', $event.target.value)" /><button class="search-button" @click="$emit('search')">Send</button></div>`,
}

function mountChat() {
  return mount(AiChat, {
    global: {
      stubs: {
        'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
        'a-button': { template: '<button class="a-btn"><slot /></button>' },
        'a-avatar': { template: '<span class="a-avatar" />' },
        'a-input-search': InputSearchStub,
        'a-spin': { template: '<div class="a-spin" />' },
        'a-alert': { template: '<div class="a-alert"><slot /></div>' },
        'a-tag': { template: '<button class="a-tag"><slot /></button>' },
        'a-space': { template: '<div class="a-space"><slot /></div>' },
        'a-divider': { template: '<div class="a-divider" />' },
      },
    },
  })
}

describe('AiChat actionable response edge cases', () => {
  it('renders update_task action button', async () => {
    messages.value = []
    error.value = null

    const updateResponse = JSON.stringify({
      action: 'update_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: { name: 'Updated Task', cron: '0 10 * * *', repository: 'https://github.com/test/repo' },
      error: null,
    })

    sendMessage.mockImplementationOnce(async (content: string) => {
      messages.value.push({ id: 'u1', role: 'user', content, timestamp: new Date() })
      messages.value.push({ id: 'a1', role: 'ai', content: updateResponse, timestamp: new Date() })
      return null
    })

    const wrapper = mountChat()
    await wrapper.find('.search-input').setValue('update task')
    await wrapper.find('.search-button').trigger('click')
    await flushPromises()

    expect(wrapper.find('.action-buttons').exists()).toBe(true)
    expect(wrapper.text()).toContain('update_task')
  })

  it('renders delete_task action button', async () => {
    messages.value = []
    error.value = null

    const deleteResponse = JSON.stringify({
      action: 'delete_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: null,
      error: null,
    })

    sendMessage.mockImplementationOnce(async (content: string) => {
      messages.value.push({ id: 'u1', role: 'user', content, timestamp: new Date() })
      messages.value.push({ id: 'a1', role: 'ai', content: deleteResponse, timestamp: new Date() })
      return null
    })

    const wrapper = mountChat()
    await wrapper.find('.search-input').setValue('delete task')
    await wrapper.find('.search-button').trigger('click')
    await flushPromises()

    expect(wrapper.find('.action-buttons').exists()).toBe(true)
    expect(wrapper.text()).toContain('delete_task')
  })

  it('renders trigger_task action button', async () => {
    messages.value = []
    error.value = null

    const triggerResponse = JSON.stringify({
      action: 'trigger_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: null,
      error: null,
    })

    sendMessage.mockImplementationOnce(async (content: string) => {
      messages.value.push({ id: 'u1', role: 'user', content, timestamp: new Date() })
      messages.value.push({ id: 'a1', role: 'ai', content: triggerResponse, timestamp: new Date() })
      return null
    })

    const wrapper = mountChat()
    await wrapper.find('.search-input').setValue('trigger task')
    await wrapper.find('.search-button').trigger('click')
    await flushPromises()

    expect(wrapper.find('.action-buttons').exists()).toBe(true)
    expect(wrapper.text()).toContain('trigger_task')
  })

  it('does not render action buttons for invalid JSON response', async () => {
    messages.value = []
    error.value = null

    sendMessage.mockImplementationOnce(async (content: string) => {
      messages.value.push({ id: 'u1', role: 'user', content, timestamp: new Date() })
      messages.value.push({ id: 'a1', role: 'ai', content: 'not valid json', timestamp: new Date() })
      return null
    })

    const wrapper = mountChat()
    await wrapper.find('.search-input').setValue('test')
    await wrapper.find('.search-button').trigger('click')
    await flushPromises()

    expect(wrapper.find('.action-buttons').exists()).toBe(false)
  })

  it('renders error alert when error is set', async () => {
    messages.value = []
    error.value = 'Something went wrong'

    const wrapper = mountChat()

    expect(wrapper.find('.a-alert').exists()).toBe(true)
  })

  it('clears messages when clear button is clicked', async () => {
    messages.value = [
      { id: 'm1', role: 'user', content: 'hello', timestamp: new Date() },
      { id: 'm2', role: 'ai', content: '{"action":""}', timestamp: new Date() },
    ]
    error.value = null

    const wrapper = mountChat()
    const buttons = wrapper.findAll('button')
    const clearBtn = buttons.find(b => b.text().includes('Clear'))
    expect(clearBtn).toBeDefined()

    await clearBtn!.trigger('click')

    // messages ref should be cleared by the component's clear handler
    // (the composable's messages ref is shared, so we check it was emptied)
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
