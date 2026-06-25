import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useAIChat } from '../../src/composables/useAIChat'

const mockFetch = vi.fn()
vi.stubGlobal('fetch', mockFetch)

describe('useAIChat edge cases', () => {
  beforeEach(() => {
    mockFetch.mockReset()
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('stores AI response content in messages', async () => {
    mockFetch.mockResolvedValue({
      text: () => Promise.resolve('AI says hello'),
    })

    const { messages, sendMessage } = useAIChat('/api')
    await sendMessage('hi')

    expect(messages.value.length).toBe(2)
    expect(messages.value[0].role).toBe('user')
    expect(messages.value[0].content).toBe('hi')
    expect(messages.value[1].role).toBe('ai')
    expect(messages.value[1].content).toBe('AI says hello')
  })

  it('supports multiple sequential messages', async () => {
    let callCount = 0
    mockFetch.mockImplementation(async () => ({
      text: () => Promise.resolve(`response-${++callCount}`),
    }))

    const { messages, sendMessage } = useAIChat('/api')
    await sendMessage('first')
    await sendMessage('second')

    expect(messages.value.length).toBe(4)
    expect(messages.value[0].content).toBe('first')
    expect(messages.value[1].content).toBe('response-1')
    expect(messages.value[2].content).toBe('second')
    expect(messages.value[3].content).toBe('response-2')
  })

  it('uses correct API base URL', async () => {
    mockFetch.mockResolvedValue({ text: () => Promise.resolve('ok') })

    const { sendMessage } = useAIChat('/custom-api')
    await sendMessage('test')

    expect(mockFetch).toHaveBeenCalledWith(
      '/custom-api/ai/message',
      expect.objectContaining({ method: 'POST' })
    )
  })

  it('sends JSON body with message field', async () => {
    mockFetch.mockResolvedValue({ text: () => Promise.resolve('ok') })

    const { sendMessage } = useAIChat('/api')
    await sendMessage('hello world')

    const callArgs = mockFetch.mock.calls[0]
    const body = JSON.parse(callArgs[1].body)
    expect(body.message).toBe('hello world')
  })

  it('sets Content-Type header to application/json', async () => {
    mockFetch.mockResolvedValue({ text: () => Promise.resolve('ok') })

    const { sendMessage } = useAIChat('/api')
    await sendMessage('test')

    const headers = mockFetch.mock.calls[0][1].headers
    expect(headers['Content-Type']).toBe('application/json')
  })

  it('timestamps are Date instances', async () => {
    mockFetch.mockResolvedValue({ text: () => Promise.resolve('ok') })

    const { messages, sendMessage } = useAIChat('/api')
    await sendMessage('test')

    expect(messages.value[0].timestamp).toBeInstanceOf(Date)
    expect(messages.value[1].timestamp).toBeInstanceOf(Date)
  })

  it('resolves without value from sendMessage', async () => {
    mockFetch.mockResolvedValue({ text: () => Promise.resolve('ok') })

    const { sendMessage } = useAIChat('/api')
    const result = await sendMessage('test')

    expect(result).toBeUndefined()
  })

  it('resets isLoading to false after error', async () => {
    mockFetch.mockRejectedValue(new Error('fail'))

    const { isLoading, sendMessage } = useAIChat('/api')
    await sendMessage('test')

    expect(isLoading.value).toBe(false)
  })

  it('preserves existing messages on error', async () => {
    mockFetch.mockResolvedValue({ text: () => Promise.resolve('ok') })

    const { messages, sendMessage } = useAIChat('/api')
    await sendMessage('first')

    mockFetch.mockRejectedValue(new Error('fail'))
    await sendMessage('second')

    // Should have: user1, ai1, user2 (no ai2 because of error)
    expect(messages.value.length).toBe(3)
    expect(messages.value[2].role).toBe('user')
    expect(messages.value[2].content).toBe('second')
  })
})
