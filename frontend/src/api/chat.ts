import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: `${API_BASE_URL}/ai`,
})

export interface ChatMessage {
  id: string
  role: 'user' | 'ai'
  content: string
  createdAt: string
}

export interface ChatConversation {
  id: string
  title: string | null
  createdAt: string
  updatedAt: string
  messages: ChatMessage[]
}

export const chatApi = {
  createConversation: () => api.post<ChatConversation>('/conversations').then(r => r.data),
  getConversation: (id: string) => api.get<ChatConversation>(`/conversations/${id}`).then(r => r.data),
  sendMessage: (conversationId: string, message: string) =>
    api.post<ChatMessage>(`/conversations/${conversationId}/messages`, { message }).then(r => r.data),
  deleteConversation: (id: string) => api.delete(`/conversations/${id}`),
}
