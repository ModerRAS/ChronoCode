import axios from 'axios'
import { ref } from 'vue'
import { chatApi } from '../api/chat'

interface Message {
  id: string
  role: 'user' | 'ai'
  content: string
  timestamp: Date
}

const STORAGE_KEY = 'chronocode-ai-chat-conversation-id'

function loadConversationId(): string | null {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    return window.localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

function saveConversationId(id: string | null): void {
  if (typeof window === 'undefined' || id === null) {
    return
  }

  try {
    window.localStorage.setItem(STORAGE_KEY, id)
  } catch {
    // Ignore storage errors (e.g. quota exceeded, private mode).
  }
}

function removeConversationId(): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.removeItem(STORAGE_KEY)
  } catch {
    // Ignore storage errors.
  }
}

function mapApiMessage(message: import('../api/chat').ChatMessage): Message {
  return {
    id: message.id,
    role: message.role,
    content: message.content,
    timestamp: new Date(message.createdAt),
  }
}

export function useAIChat() {
  const conversationId = ref<string | null>(loadConversationId())
  const messages = ref<Message[]>([])
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const isInitialized = ref(false)

  const ensureConversation = async (): Promise<void> => {
    if (conversationId.value) {
      return
    }

    const conversation = await chatApi.createConversation()
    conversationId.value = conversation.id
    saveConversationId(conversation.id)
    messages.value = []
  }

  const loadConversation = async (): Promise<void> => {
    if (isInitialized.value) {
      return
    }

    try {
      if (!conversationId.value) {
        await ensureConversation()
        isInitialized.value = true
        return
      }

      try {
        const conversation = await chatApi.getConversation(conversationId.value)
        messages.value = conversation.messages.map(mapApiMessage)
      } catch (e) {
        // If the stored conversation no longer exists, create a fresh one.
        if (axios.isAxiosError(e) && e.response?.status === 404) {
          conversationId.value = null
          await ensureConversation()
        } else {
          throw e
        }
      }

      isInitialized.value = true
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load chat history'
    }
  }

  const sendMessage = async (content: string): Promise<void> => {
    isLoading.value = true
    error.value = null

    try {
      await ensureConversation()
      if (!conversationId.value) {
        throw new Error('No active conversation')
      }

      messages.value.push({
        id: `local-${Date.now()}`,
        role: 'user',
        content,
        timestamp: new Date(),
      })

      const response = await chatApi.sendMessage(conversationId.value, content)

      messages.value.push(mapApiMessage(response))
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Network error'
    } finally {
      isLoading.value = false
    }
  }

  const clearChat = async (): Promise<void> => {
    if (conversationId.value) {
      try {
        await chatApi.deleteConversation(conversationId.value)
      } catch {
        // Ignore delete failures; just start fresh locally.
      }
    }

    removeConversationId()
    conversationId.value = null
    messages.value = []
    isInitialized.value = false
    await ensureConversation()
    isInitialized.value = true
  }

  return { messages, isLoading, error, sendMessage, clearChat, loadConversation }
}
