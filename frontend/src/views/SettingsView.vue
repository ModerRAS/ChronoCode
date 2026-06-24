<template>
  <div class="settings-page">
    <a-row :gutter="16">
      <a-col :xs="24" :xl="10">
        <a-card title="Runtime Status" :loading="statusLoading">
          <a-space class="settings-actions" wrap>
            <a-button @click="refreshRuntimeStatus" :loading="statusRefreshing">Refresh</a-button>
            <a-button type="primary" @click="startRuntime" :loading="runtimeStarting">Start Runtime</a-button>
            <a-button danger @click="stopRuntime" :loading="runtimeStopping">Stop Runtime</a-button>
          </a-space>

          <a-descriptions :column="1" size="small" bordered>
            <a-descriptions-item label="Backend">{{ runtimeStatus.backend }}</a-descriptions-item>
            <a-descriptions-item label="Running">{{ runtimeStatus.running ? 'Yes' : 'No' }}</a-descriptions-item>
            <a-descriptions-item label="URL">{{ runtimeStatus.url || 'Not available' }}</a-descriptions-item>
            <a-descriptions-item label="Persistent Sessions">
              {{ runtimeStatus.supportsPersistentSessions ? 'Yes' : 'No' }}
            </a-descriptions-item>
            <a-descriptions-item label="Supplemental Messages">
              {{ runtimeStatus.supportsSupplementalMessages ? 'Yes' : 'No' }}
            </a-descriptions-item>
          </a-descriptions>
        </a-card>
      </a-col>

      <a-col :xs="24" :xl="14">
        <a-card title="AI Runtime Settings" :loading="settingsLoading">
          <a-alert
            message="Database setup stays in the initial setup flow and is not editable here."
            type="info"
            show-icon
            class="settings-alert"
          />

          <a-form layout="vertical">
            <a-form-item label="Backend">
              <a-radio-group v-model:value="form.agentRuntime.backend" button-style="solid">
                <a-radio-button value="pi">pi</a-radio-button>
                <a-radio-button value="opencode">opencode</a-radio-button>
              </a-radio-group>
            </a-form-item>

            <template v-if="form.agentRuntime.backend === 'opencode'">
              <a-row :gutter="16">
                <a-col :xs="24" :md="12">
                  <a-form-item label="Host">
                    <a-input v-model:value="form.opencode.host" />
                  </a-form-item>
                </a-col>
                <a-col :xs="24" :md="12">
                  <a-form-item label="Port">
                    <a-input-number v-model:value="form.opencode.port" :min="1" :max="65535" class="full-width-input" />
                  </a-form-item>
                </a-col>
              </a-row>

              <a-form-item label="Username">
                <a-input v-model:value="form.opencode.username" />
              </a-form-item>

              <a-form-item label="Password">
                <a-input-password v-model:value="opencodePassword" autocomplete="new-password" />
                <template v-if="settings?.opencode.hasPassword" #help>
                  <span>Current password is configured. Leave this field blank to keep it unchanged.</span>
                </template>
              </a-form-item>
            </template>

            <template v-else>
              <a-form-item label="Provider">
                <a-input v-model:value="form.pi.provider" />
              </a-form-item>

              <a-form-item label="Model">
                <a-input v-model:value="form.pi.model" />
              </a-form-item>

              <a-form-item label="Thinking">
                <a-input v-model:value="form.pi.thinking" />
              </a-form-item>
            </template>

            <a-form-item>
              <a-button type="primary" html-type="button" :loading="settingsSaving" @click="saveSettings">Save Settings</a-button>
            </a-form-item>
          </a-form>
        </a-card>
      </a-col>
    </a-row>

    <a-card title="System Info" class="system-info-card" :loading="systemInfoLoading">
      <a-descriptions :column="1" size="small" bordered>
        <a-descriptions-item label="Initialized">{{ setupStatus?.initialized ? 'Yes' : 'No' }}</a-descriptions-item>
        <a-descriptions-item label="Database Provider">{{ setupStatus?.databaseProvider || 'Not configured' }}</a-descriptions-item>
        <a-descriptions-item label="Config File Path">{{ setupStatus?.configFilePath || 'Unknown' }}</a-descriptions-item>
        <a-descriptions-item label="Default SQLite Path">{{ setupStatus?.defaultSqlitePath || 'storage/chronocode.db' }}</a-descriptions-item>
      </a-descriptions>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { settingsApi, type RuntimeSettings, type UpdateRuntimeSettingsDto } from '../api/settings'
import { setupApi, type SetupStatus } from '../api/setup'
import { taskApi } from '../api/tasks'

interface RuntimeStatus {
  backend: string
  running: boolean
  url: string | null
  supportsPersistentSessions: boolean
  supportsSupplementalMessages: boolean
}

const settings = ref<RuntimeSettings | null>(null)
const setupStatus = ref<SetupStatus | null>(null)
const runtimeStatus = ref<RuntimeStatus>({
  backend: 'pi',
  running: false,
  url: null,
  supportsPersistentSessions: false,
  supportsSupplementalMessages: false,
})

const settingsLoading = ref(true)
const systemInfoLoading = ref(true)
const statusLoading = ref(true)
const settingsSaving = ref(false)
const runtimeStarting = ref(false)
const runtimeStopping = ref(false)
const statusRefreshing = ref(false)
const opencodePassword = ref('')

const form = reactive<UpdateRuntimeSettingsDto>({
  agentRuntime: {
    backend: 'pi',
  },
  opencode: {
    host: '127.0.0.1',
    port: 4096,
    username: '',
  },
  pi: {
    provider: '',
    model: '',
    thinking: 'medium',
  },
})

const applySettings = (value: RuntimeSettings) => {
  settings.value = value
  form.agentRuntime.backend = value.agentRuntime.backend
  form.opencode.host = value.opencode.host
  form.opencode.port = value.opencode.port
  form.opencode.username = value.opencode.username
  form.pi.provider = value.pi.provider
  form.pi.model = value.pi.model
  form.pi.thinking = value.pi.thinking
  opencodePassword.value = ''
}

const mapRuntimeStatus = (value: any): RuntimeStatus => ({
  backend: String(value.backend ?? ''),
  running: Boolean(value.running),
  url: value.url ?? null,
  supportsPersistentSessions: Boolean(value.supportsPersistentSessions),
  supportsSupplementalMessages: Boolean(value.supportsSupplementalMessages),
})

const refreshRuntimeStatus = async () => {
  statusRefreshing.value = true
  try {
    const response = await taskApi.getServerStatus()
    runtimeStatus.value = mapRuntimeStatus(response.data)
  } catch {
    message.error('Failed to load runtime status')
  } finally {
    statusRefreshing.value = false
    statusLoading.value = false
  }
}

const loadPage = async () => {
  try {
    const [settingsResult, setupResult, runtimeResult] = await Promise.all([
      settingsApi.get(),
      setupApi.getStatus(),
      taskApi.getServerStatus(),
    ])

    applySettings(settingsResult)
    setupStatus.value = setupResult
    runtimeStatus.value = mapRuntimeStatus(runtimeResult.data)
  } catch {
    message.error('Failed to load settings page')
  } finally {
    settingsLoading.value = false
    systemInfoLoading.value = false
    statusLoading.value = false
  }
}

const saveSettings = async () => {
  settingsSaving.value = true

  const request: UpdateRuntimeSettingsDto = {
    agentRuntime: {
      backend: form.agentRuntime.backend,
    },
    opencode: {
      host: form.opencode.host,
      port: form.opencode.port,
      username: form.opencode.username,
    },
    pi: {
      provider: form.pi.provider,
      model: form.pi.model,
      thinking: form.pi.thinking,
    },
  }

  if (opencodePassword.value.trim()) {
    request.opencode.password = opencodePassword.value
  }

  try {
    const updated = await settingsApi.update(request)
    applySettings(updated)
    const runtimeResponse = await taskApi.getServerStatus()
    runtimeStatus.value = mapRuntimeStatus(runtimeResponse.data)
    message.success('Settings saved. Runtime changes apply on the next start.')
  } catch (error: any) {
    message.error(error?.response?.data?.error?.message || 'Failed to save settings')
  } finally {
    settingsSaving.value = false
  }
}

const startRuntime = async () => {
  runtimeStarting.value = true
  try {
    const response = await taskApi.startServer()
    runtimeStatus.value = mapRuntimeStatus(response.data)
    runtimeStatus.value.running = true
    message.success('Runtime started')
  } catch (error: any) {
    message.error(error?.response?.data?.Error || 'Failed to start runtime')
  } finally {
    runtimeStarting.value = false
  }
}

const stopRuntime = async () => {
  runtimeStopping.value = true
  try {
    await taskApi.stopServer()
    await refreshRuntimeStatus()
    message.success('Runtime stopped')
  } catch {
    message.error('Failed to stop runtime')
  } finally {
    runtimeStopping.value = false
  }
}

onMounted(loadPage)
</script>

<style scoped>
.settings-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.settings-actions {
  margin-bottom: 16px;
}

.settings-alert {
  margin-bottom: 16px;
}

.system-info-card {
  margin-top: 16px;
}

.full-width-input {
  width: 100%;
}
</style>
