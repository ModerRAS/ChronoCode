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
  maxRuntimeSeconds: number
  maxFileChanges: number
  isEnabled: boolean
  workflowVersion: number
  workflowDefinitionJson: string
  defaultInputsJson?: string | null
  runtimeBackend?: string | null
  maxConcurrentRuns: number
  nodeFailurePolicyJson: string
  createdAt: string
  lastRunAt?: string
  lastStatus: number
  lastError?: string
  nextRunAt?: string
  lastQueuedAt?: string
  schedulerStatus: string
  schedulerHeartbeatAt?: string
}

export interface CreateTaskDto {
  name: string
  cronExpression: string
  repositoryUrl: string
  baseBranch: string
  branchStrategy: number
  maxRuntimeSeconds: number
  maxFileChanges: number
  isEnabled: boolean
  workflowDefinitionJson: string
  defaultInputsJson?: string | null
  runtimeBackend?: string | null
  maxConcurrentRuns: number
  nodeFailurePolicyJson: string
}

export interface Execution {
  id: string
  taskId: string
  startedAt: string
  completedAt?: string
  status: number
  workflowVersion: number
  currentNodeId?: string
  triggerSource: string
  branchName?: string
  commitSha?: string
  prUrl?: string
  filesChanged: number
  errorMessage?: string
}

export interface NodeExecution {
  id: string
  executionId: string
  nodeId: string
  nodeType: string
  scopeKey: string
  attempt: number
  status: string
  startedAt: string
  completedAt?: string
  outputJson?: string
  validationError?: string
  agentBackend?: string
  agentSessionId?: string
  agentSessionFile?: string
  agentWorkingDirectory?: string
  failureReason?: string
  nextRetryAt?: string
  retryCount: number
  leaseExpiresAt?: string
}

export interface ExecutionSession {
  executionId: string
  nodeExecutionId: string
  backend?: string | null
  sessionId?: string | null
  sessionFile?: string | null
  workingDirectory?: string | null
  isLive: boolean
  supportsPersistentSessions: boolean
  supportsSupplementalMessages: boolean
  canResume: boolean
}

export interface ApprovalRequest {
  approved: boolean
  reason?: string
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
  update: (id: string, data: Partial<CreateTaskDto>) =>
    api.put<ScheduledTask>(`/tasks/${id}`, data).then(r => r.data),
  delete: (id: string) => api.delete(`/tasks/${id}`),
  trigger: (id: string) => api.post(`/tasks/${id}/run`),
  getExecutions: (id: string) => api.get<Execution[]>(`/tasks/${id}/executions`).then(r => r.data),
  getLogs: (executionId: string) =>
    api.get<LogEntry[]>(`/tasks/executions/${executionId}/logs`).then(r => r.data),
  getNodes: (executionId: string) =>
    api.get<NodeExecution[]>(`/tasks/executions/${executionId}/nodes`).then(r => r.data),
  getNodeSession: (executionId: string, nodeId: string) =>
    api
      .get<ExecutionSession>(`/tasks/executions/${executionId}/nodes/${nodeId}/session`)
      .then(r => r.data),
  resumeNodeSession: (executionId: string, nodeId: string, body?: unknown) =>
    api.post(`/tasks/executions/${executionId}/nodes/${nodeId}/resume`, body).then(r => r.data),
  sendNodeMessage: (executionId: string, nodeId: string, body: unknown) =>
    api.post(`/tasks/executions/${executionId}/nodes/${nodeId}/message`, body).then(r => r.data),
  approveNode: (executionId: string, nodeId: string, body: ApprovalRequest) =>
    api.post(`/tasks/executions/${executionId}/approval/${nodeId}`, body),
  getServerStatus: () => api.get('/tasks/server/status'),
  startServer: () => api.post('/tasks/server/start'),
  stopServer: () => api.post('/tasks/server/stop'),
}

export default api
