<template>
  <div class="setup-page">
    <div class="setup-shell">
      <div class="setup-header">
        <div>
          <div class="setup-badge">Initial Setup</div>
          <h1>Welcome to ChronoCode</h1>
          <p>Choose a database to finish installation. This setup is intentionally simple and product-shaped, similar to Gitea: pick a backend, confirm paths or connection details, then initialize.</p>
        </div>
      </div>

      <a-row :gutter="24">
        <a-col :xs="24" :lg="16">
          <a-card title="Database Settings" class="setup-card">
            <a-alert
              v-if="error"
              :message="error"
              type="error"
              show-icon
              style="margin-bottom: 16px"
            />

            <a-form layout="vertical" @finish="submit">
              <a-form-item label="Database Type">
                <a-radio-group v-model:value="form.databaseProvider" button-style="solid">
                  <a-radio-button value="sqlite">SQLite</a-radio-button>
                  <a-radio-button value="postgresql">PostgreSQL</a-radio-button>
                </a-radio-group>
              </a-form-item>

              <template v-if="form.databaseProvider === 'sqlite'">
                <a-form-item label="SQLite Database Path">
                  <a-input v-model:value="form.sqlitePath" placeholder="data/chronocode.db" />
                  <template #help>
                    <span>Good default for single-user or small self-hosted installs. ChronoCode will create the file and parent directory if needed.</span>
                  </template>
                </a-form-item>
              </template>

              <template v-else>
                <a-row :gutter="16">
                  <a-col :xs="24" :md="12">
                    <a-form-item label="Host">
                      <a-input v-model:value="form.postgresHost" placeholder="localhost" />
                    </a-form-item>
                  </a-col>
                  <a-col :xs="24" :md="12">
                    <a-form-item label="Port">
                      <a-input-number v-model:value="form.postgresPort" :min="1" :max="65535" style="width: 100%" />
                    </a-form-item>
                  </a-col>
                </a-row>

                <a-row :gutter="16">
                  <a-col :xs="24" :md="12">
                    <a-form-item label="Database Name">
                      <a-input v-model:value="form.postgresDatabase" placeholder="chronocode" />
                    </a-form-item>
                  </a-col>
                  <a-col :xs="24" :md="12">
                    <a-form-item label="Username">
                      <a-input v-model:value="form.postgresUsername" placeholder="postgres" />
                    </a-form-item>
                  </a-col>
                </a-row>

                <a-form-item label="Password">
                  <a-input-password v-model:value="form.postgresPassword" placeholder="Password" />
                </a-form-item>

                <a-form-item label="Or paste full connection string (optional)">
                  <a-input v-model:value="form.connectionString" placeholder="Host=localhost;Database=chronocode;Username=postgres;Password=..." />
                  <template #help>
                    <span>If you provide a connection string, it takes precedence over the individual PostgreSQL fields above.</span>
                  </template>
                </a-form-item>
              </template>

              <a-form-item>
                <a-space>
                  <a-button type="primary" html-type="submit" :loading="submitting">Initialize ChronoCode</a-button>
                  <a-button @click="reloadStatus" :disabled="submitting">Reload Status</a-button>
                </a-space>
              </a-form-item>
            </a-form>
          </a-card>
        </a-col>

        <a-col :xs="24" :lg="8">
          <a-card title="Recommended Choices" class="setup-card muted-card">
            <ul class="setup-list">
              <li><strong>SQLite</strong>: easiest local setup, zero external service, best default for one machine.</li>
              <li><strong>PostgreSQL</strong>: use when you already run Postgres or want multi-user / production-style deployment.</li>
              <li><strong>Config file</strong>: setup writes to <code>{{ status?.configFilePath || 'appsettings.Local.json' }}</code>.</li>
            </ul>
          </a-card>

          <a-card title="Current Status" class="setup-card muted-card">
            <a-descriptions :column="1" size="small" bordered>
              <a-descriptions-item label="Initialized">{{ status?.initialized ? 'Yes' : 'No' }}</a-descriptions-item>
              <a-descriptions-item label="Provider">{{ status?.databaseProvider || 'Not set' }}</a-descriptions-item>
              <a-descriptions-item label="SQLite default">{{ status?.defaultSqlitePath || 'data/chronocode.db' }}</a-descriptions-item>
            </a-descriptions>
          </a-card>
        </a-col>
      </a-row>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { setupApi, type InitializeSetupDto, type SetupStatus } from '../api/setup'

const emit = defineEmits<{
  initialized: []
}>()

const status = ref<SetupStatus | null>(null)
const submitting = ref(false)
const error = ref('')

const form = reactive<InitializeSetupDto>({
  databaseProvider: 'sqlite',
  sqlitePath: 'data/chronocode.db',
  postgresHost: 'localhost',
  postgresPort: 5432,
  postgresDatabase: 'chronocode',
  postgresUsername: 'postgres',
  postgresPassword: '',
  connectionString: '',
})

const reloadStatus = async () => {
  try {
    status.value = await setupApi.getStatus()
  } catch {
    error.value = 'Failed to load setup status.'
  }
}

const submit = async () => {
  error.value = ''
  submitting.value = true

  try {
    const result = await setupApi.initialize(form)
    status.value = result
    message.success('ChronoCode initialized successfully. Reloading application...')
    emit('initialized')
  } catch (e: any) {
    error.value = e?.response?.data?.error?.message || 'Setup failed.'
  } finally {
    submitting.value = false
  }
}

onMounted(reloadStatus)
</script>

<style scoped>
.setup-page {
  min-height: 100vh;
  background: linear-gradient(180deg, #f6f8fb 0%, #eef2f7 100%);
  padding: 40px 24px;
  box-sizing: border-box;
}

.setup-shell {
  max-width: 1180px;
  margin: 0 auto;
}

.setup-header {
  margin-bottom: 24px;
}

.setup-header h1 {
  margin: 8px 0 12px;
  font-size: 36px;
  color: #0f172a;
}

.setup-header p {
  margin: 0;
  max-width: 860px;
  color: #475569;
}

.setup-badge {
  display: inline-block;
  padding: 6px 12px;
  border-radius: 999px;
  background: #eef2ff;
  color: #4338ca;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: .04em;
  text-transform: uppercase;
}

.setup-card {
  border-radius: 16px;
  box-shadow: 0 8px 24px rgba(15, 23, 42, 0.06);
}

.muted-card :deep(.ant-card-head) {
  background: #fafafa;
}

.setup-list {
  margin: 0;
  padding-left: 18px;
  color: #475569;
}

.setup-list li + li {
  margin-top: 12px;
}
</style>
