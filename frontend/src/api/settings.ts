import api from './tasks'

export interface RuntimeSettings {
  agentRuntime: {
    backend: string
  }
  opencode: {
    host: string
    port: number
    username: string
    hasPassword: boolean
  }
  pi: {
    provider: string
    model: string
    thinking: string
  }
}

export interface UpdateRuntimeSettingsDto {
  agentRuntime: {
    backend: string
  }
  opencode: {
    host: string
    port: number
    username: string
    password?: string
  }
  pi: {
    provider: string
    model: string
    thinking: string
  }
}

export const settingsApi = {
  get: () => api.get<RuntimeSettings>('/settings').then(r => r.data),
  update: (data: UpdateRuntimeSettingsDto) => api.put<RuntimeSettings>('/settings', data).then(r => r.data),
}
