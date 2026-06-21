import axios from 'axios'
import { isActionableAIResponse, type AIStructuredResponse } from './aiParser'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
})

export interface ExecuteResult {
  success: boolean
  message: string
  data?: unknown
}

export async function executeAIResponse(response: AIStructuredResponse): Promise<ExecuteResult> {
  if (!isActionableAIResponse(response)) {
    return {
      success: false,
      message: response.error.message,
    }
  }

  try {
    const result = await api.post('/ai/ai', response)
    const successMessages: Record<AIStructuredResponse['action'], string> = {
      create_task: 'Task created successfully',
      update_task: 'Task updated successfully',
      delete_task: 'Task deleted successfully',
      trigger_task: 'Task triggered successfully',
      '': '',
    }

    return {
      success: true,
      message: successMessages[response.action] || 'Action executed successfully',
      data: result.data,
    }
  } catch (err) {
    if (axios.isAxiosError(err)) {
      return {
        success: false,
        message:
          err.response?.data?.error?.message ||
          err.response?.data?.message ||
          err.message ||
          'API request failed',
      }
    }
    return { success: false, message: 'An unexpected error occurred' }
  }
}
