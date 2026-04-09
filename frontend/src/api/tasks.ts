import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
})

export interface ScheduledTask {
  id: string
  name: string
  cronExpression: string
  repositoryUrl: string
  baseBranch: string
  branchStrategy: number
  prompt: string
  maxRuntimeSeconds: number
  maxFileChanges: number
  requirePlanReview: boolean
  createdAt: string
  lastRunAt?: string
  lastStatus: number
  isEnabled: boolean
  lastError?: string
}

export interface CreateTaskDto {
  name: string
  cronExpression: string
  repositoryUrl: string
  baseBranch: string
  branchStrategy: number
  prompt: string
  maxRuntimeSeconds: number
  maxFileChanges: number
  requirePlanReview: boolean
  isEnabled: boolean
}

export interface Execution {
  id: string
  taskId: string
  startedAt: string
  completedAt?: string
  status: number
  branchName?: string
  commitSha?: string
  prUrl?: string
  filesChanged: number
  errorMessage?: string
}

export interface LogEntry {
  timestamp: string
  level: string
  message: string
  details?: string
}

export const taskApi = {
  getAll: () => api.get<ScheduledTask[]>('/tasks').then(r => r.data),
  getById: (id: string) => api.get<ScheduledTask>(`/tasks/${id}`).then(r => r.data),
  create: (data: CreateTaskDto) => api.post<ScheduledTask>('/tasks', data).then(r => r.data),
  update: (id: string, data: Partial<ScheduledTask>) => api.put<ScheduledTask>(`/tasks/${id}`, data).then(r => r.data),
  delete: (id: string) => api.delete(`/tasks/${id}`),
  trigger: (id: string) => api.post(`/tasks/${id}/run`),
  getExecutions: (id: string) => api.get<Execution[]>(`/tasks/${id}/executions`).then(r => r.data),
  getLogs: (executionId: string) => api.get<LogEntry[]>(`/tasks/executions/${executionId}/logs`).then(r => r.data),
  getServerStatus: () => api.get('/tasks/server/status'),
  startServer: () => api.post('/tasks/server/start'),
  stopServer: () => api.post('/tasks/server/stop'),
}

export default api
