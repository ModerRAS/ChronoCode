import api from './tasks'

export interface SetupStatus {
  initialized: boolean
  databaseProvider?: string
  configFilePath: string
  defaultSqlitePath: string
}

export interface InitializeSetupDto {
  databaseProvider: 'sqlite' | 'postgresql'
  sqlitePath?: string
  connectionString?: string
  postgresHost?: string
  postgresPort?: number
  postgresDatabase?: string
  postgresUsername?: string
  postgresPassword?: string
}

export const setupApi = {
  getStatus: () => api.get<SetupStatus>('/setup/status').then(r => r.data),
  initialize: (data: InitializeSetupDto) => api.post<SetupStatus>('/setup/initialize', data).then(r => r.data),
}
