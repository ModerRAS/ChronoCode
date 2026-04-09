import axios from 'axios'
import type { AIStructuredResponse, CreateTaskInput } from './aiParser'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
})

export interface ExecuteResult {
  success: boolean
  message: string
  data?: unknown
}

function mapCreateTaskInput(input: CreateTaskInput) {
  return {
    name: input.name,
    cronExpression: input.cron,
    repositoryUrl: input.repository,
    baseBranch: input.base_branch || 'main',
    branchStrategy: 1,
    prompt: input.prompt,
    maxRuntimeSeconds: input.max_runtime_seconds,
    maxFileChanges: input.max_file_changes,
    requirePlanReview: input.require_plan_review,
    isEnabled: input.is_enabled,
  }
}

export async function executeAIResponse(response: AIStructuredResponse): Promise<ExecuteResult> {
  try {
    switch (response.action) {
      case 'create_task': {
        if (!response.task) {
          return { success: false, message: 'No task data provided' }
        }
        const taskData = mapCreateTaskInput(response.task)
        const result = await api.post('/tasks', taskData)
        return { success: true, message: 'Task created successfully', data: result.data }
      }

      case 'update_task': {
        if (!response.task_id) {
          return { success: false, message: 'Task ID is required for update' }
        }
        if (!response.task) {
          return { success: false, message: 'No task data provided' }
        }
        const taskData = mapCreateTaskInput(response.task)
        const result = await api.put(`/tasks/${response.task_id}`, taskData)
        return { success: true, message: 'Task updated successfully', data: result.data }
      }

      case 'delete_task': {
        if (!response.task_id) {
          return { success: false, message: 'Task ID is required for delete' }
        }
        await api.delete(`/tasks/${response.task_id}`)
        return { success: true, message: 'Task deleted successfully' }
      }

      case 'trigger_task': {
        if (!response.task_id) {
          return { success: false, message: 'Task ID is required for trigger' }
        }
        await api.post(`/tasks/${response.task_id}/run`)
        return { success: true, message: 'Task triggered successfully' }
      }

      default:
        return { success: false, message: `Unknown action: ${response.action}` }
    }
  } catch (err) {
    if (axios.isAxiosError(err)) {
      return {
        success: false,
        message: err.response?.data?.message || err.message || 'API request failed',
      }
    }
    return { success: false, message: 'An unexpected error occurred' }
  }
}
