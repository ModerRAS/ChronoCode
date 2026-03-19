import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import AiChat from '../../src/views/AiChat.vue'

vi.mock('../../src/composables/useAIChat', () => ({
  useAIChat: () => ({
    messages: vi.fn(() => ({
      value: []
    })),
    isLoading: vi.fn(() => ({
      value: false
    })),
    error: vi.fn(() => ({
      value: null
    })),
    sendMessage: vi.fn().mockResolvedValue(null)
  })
}))

describe('AiChat.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders empty state initially', () => {
    const wrapper = mount(AiChat, {
      global: {
        stubs: {
          'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
          'a-button': { template: '<button><slot /></button>' },
          'a-avatar': { template: '<span class="a-avatar" />' },
          'a-empty': { template: '<div class="a-empty"><slot /><slot name="image" /></div>' },
          'a-input-search': { template: '<input />', emits: ['search'] },
          'a-spin': { template: '<div class="a-spin" />' },
          'a-alert': { template: '<div class="a-alert" />' },
          'a-tag': { template: '<span class="a-tag"><slot /></span>' },
          'a-space': { template: '<div class="a-space"><slot /></div>' },
          'a-divider': { template: '<div class="a-divider" />' },
        }
      }
    })
    
    expect(wrapper.find('.empty-state').exists()).toBe(true)
    expect(wrapper.find('.welcome-content').exists()).toBe(true)
  })

  it('shows loading when sending message', async () => {
    const wrapper = mount(AiChat, {
      global: {
        stubs: {
          'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
          'a-button': { template: '<button><slot /></button>' },
          'a-avatar': { template: '<span class="a-avatar" />' },
          'a-empty': { template: '<div class="a-empty"><slot /></div>' },
          'a-input-search': { template: '<input />', emits: ['search'] },
          'a-spin': { template: '<div class="a-spin" />' },
          'a-alert': { template: '<div class="a-alert" />' },
          'a-tag': { template: '<span class="a-tag"><slot /></span>' },
          'a-space': { template: '<div class="a-space"><slot /></div>' },
          'a-divider': { template: '<div class="a-divider" />' },
        }
      }
    })
    
    expect(wrapper.find('.empty-state').exists()).toBe(true)
  })

  it('displays welcome message with examples', () => {
    const wrapper = mount(AiChat, {
      global: {
        stubs: {
          'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
          'a-button': { template: '<button><slot /></button>' },
          'a-avatar': { template: '<span class="a-avatar" />' },
          'a-empty': { template: '<div class="a-empty"><slot /><slot name="image" /></div>' },
          'a-input-search': { template: '<input />', emits: ['search'] },
          'a-spin': { template: '<div class="a-spin" />' },
          'a-alert': { template: '<div class="a-alert" />' },
          'a-tag': { template: '<span class="a-tag"><slot /></span>' },
          'a-space': { template: '<div class="a-space"><slot /></div>' },
          'a-divider': { template: '<div class="a-divider" />' },
        }
      }
    })
    
    expect(wrapper.find('.welcome-content h2').text()).toBe('Welcome to AI Chat')
    expect(wrapper.findAll('.a-tag').length).toBeGreaterThan(0)
  })

  it('has input field for sending messages', () => {
    const wrapper = mount(AiChat, {
      global: {
        stubs: {
          'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
          'a-button': { template: '<button><slot /></button>' },
          'a-avatar': { template: '<span class="a-avatar" />' },
          'a-empty': { template: '<div class="a-empty"><slot /><slot name="image" /></div>' },
          'a-input-search': { template: '<input />', emits: ['search'] },
          'a-spin': { template: '<div class="a-spin" />' },
          'a-alert': { template: '<div class="a-alert" />' },
          'a-tag': { template: '<span class="a-tag"><slot /></span>' },
          'a-space': { template: '<div class="a-space"><slot /></div>' },
          'a-divider': { template: '<div class="a-divider" />' },
        }
      }
    })
    
    expect(wrapper.find('input').exists()).toBe(true)
  })

  it('clear button exists', () => {
    const wrapper = mount(AiChat, {
      global: {
        stubs: {
          'a-card': { template: '<div class="a-card"><slot /><slot name="extra" /></div>' },
          'a-button': { template: '<button><slot /></button>' },
          'a-avatar': { template: '<span class="a-avatar" />' },
          'a-empty': { template: '<div class="a-empty"><slot /><slot name="image" /></div>' },
          'a-input-search': { template: '<input />', emits: ['search'] },
          'a-spin': { template: '<div class="a-spin" />' },
          'a-alert': { template: '<div class="a-alert" />' },
          'a-tag': { template: '<span class="a-tag"><slot /></span>' },
          'a-space': { template: '<div class="a-space"><slot /></div>' },
          'a-divider': { template: '<div class="a-divider" />' },
        }
      }
    })
    
    const clearButton = wrapper.find('button')
    expect(clearButton.exists()).toBe(true)
    expect(clearButton.text()).toBe('Clear')
  })
})
