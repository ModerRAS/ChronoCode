<template>
  <div>
    <a-card title="Edit Task">
      <a-spin :spinning="loading">
        <a-form :model="form" :rules="rules" layout="vertical" @finish="onSubmit">
          <a-row :gutter="24">
            <a-col :xs="24" :lg="14">
              <a-card size="small" title="Schedule & Repository" :bordered="true" class="form-section">
                <a-form-item label="Task Name" name="name">
                  <a-input v-model:value="form.name" placeholder="Enter task name" />
                </a-form-item>

                <a-form-item label="Cron Expression" name="cronExpression">
                  <a-input v-model:value="form.cronExpression" placeholder="e.g., 0 * * * *" />
                  <template #help>
                    <span class="field-help">Format: minute hour day month weekday</span>
                  </template>
                </a-form-item>

                <a-form-item label="Repository URL" name="repositoryUrl">
                  <a-input v-model:value="form.repositoryUrl" placeholder="https://github.com/owner/repo" />
                </a-form-item>

                <a-row :gutter="12">
                  <a-col :span="12">
                    <a-form-item label="Base Branch" name="baseBranch">
                      <a-input v-model:value="form.baseBranch" placeholder="main" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="12">
                    <a-form-item label="Branch Strategy" name="branchStrategy">
                      <a-select v-model:value="form.branchStrategy">
                        <a-select-option :value="0">New Branch (recommended)</a-select-option>
                        <a-select-option :value="1">Reuse Branch</a-select-option>
                      </a-select>
                    </a-form-item>
                  </a-col>
                </a-row>

                <a-row :gutter="12">
                  <a-col :span="8">
                    <a-form-item label="Max Runtime (s)" name="maxRuntimeSeconds">
                      <a-input-number v-model:value="form.maxRuntimeSeconds" :min="60" class="full-width-input" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="8">
                    <a-form-item label="Max File Changes" name="maxFileChanges">
                      <a-input-number v-model:value="form.maxFileChanges" :min="1" :max="1000" class="full-width-input" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="8">
                    <a-form-item label="Max Concurrent Runs" name="maxConcurrentRuns">
                      <a-input-number v-model:value="form.maxConcurrentRuns" :min="1" class="full-width-input" />
                    </a-form-item>
                  </a-col>
                </a-row>

                <a-row :gutter="12">
                  <a-col :span="12">
                    <a-form-item label="Runtime Backend" name="runtimeBackend" help="Workflow agent nodes only support 'pi' (v1).">
                      <a-select v-model:value="runtimeBackendValue">
                        <a-select-option value="pi">pi</a-select-option>
                        <a-select-option value="">(default / null)</a-select-option>
                      </a-select>
                    </a-form-item>
                  </a-col>
                  <a-col :span="12">
                    <a-form-item name="isEnabled">
                      <a-checkbox v-model:checked="form.isEnabled">
                        Enable Task
                      </a-checkbox>
                    </a-form-item>
                  </a-col>
                </a-row>
              </a-card>

              <a-card size="small" title="Workflow Definition" :bordered="true" class="form-section">
                <a-space class="workflow-toolbar">
                  <a-button size="small" @click="formatWorkflow">Format JSON</a-button>
                  <a-button size="small" @click="loadDefaultWorkflow">Load default workflow</a-button>
                </a-space>
                <a-form-item name="workflowDefinitionJson" :validate-status="workflowParseError ? 'error' : ''" :help="workflowParseError || 'Workflow node graph JSON.'">
                  <a-textarea
                    v-model:value="form.workflowDefinitionJson"
                    :rows="14"
                    class="workflow-textarea"
                    spellcheck="false"
                  />
                </a-form-item>

                <a-row :gutter="12">
                  <a-col :span="12">
                    <a-form-item label="Default Inputs JSON (optional)" name="defaultInputsJson">
                      <a-textarea
                        v-model:value="defaultInputsValue"
                        :rows="3"
                        spellcheck="false"
                        placeholder="{}"
                      />
                    </a-form-item>
                  </a-col>
                  <a-col :span="12">
                    <a-form-item label="Node Failure Policy JSON" name="nodeFailurePolicyJson">
                      <a-textarea
                        v-model:value="form.nodeFailurePolicyJson"
                        :rows="3"
                        spellcheck="false"
                      />
                    </a-form-item>
                  </a-col>
                </a-row>
              </a-card>

              <a-form-item>
                <div :class="['task-form-actions', { mobile: isMobile }]">
                  <a-button type="primary" html-type="submit" :loading="submitting">Update Task</a-button>
                  <a-button @click="$router.push('/')">Cancel</a-button>
                </div>
              </a-form-item>
            </a-col>

            <a-col :xs="24" :lg="10">
              <a-card size="small" title="Workflow Preview" :bordered="true">
                <WorkflowGraph :definition="parsedDefinition" />
              </a-card>
            </a-col>
          </a-row>
        </a-form>
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import { taskApi, type CreateTaskDto } from '../api/tasks'
import { useIsMobile } from '../composables/useIsMobile'
import WorkflowGraph from '../components/WorkflowGraph.vue'
import { DEFAULT_WORKFLOW_DEFINITION_JSON } from '../utils/aiParser'

const router = useRouter()
const route = useRoute()
const { isMobile } = useIsMobile()
const taskId = route.params.id as string
const submitting = ref(false)
const loading = ref(false)

const form = reactive<CreateTaskDto>({
  name: '',
  cronExpression: '',
  repositoryUrl: '',
  baseBranch: 'main',
  branchStrategy: 0,
  maxRuntimeSeconds: 600,
  maxFileChanges: 50,
  isEnabled: true,
  workflowDefinitionJson: DEFAULT_WORKFLOW_DEFINITION_JSON,
  defaultInputsJson: null,
  runtimeBackend: 'pi',
  maxConcurrentRuns: 1,
  nodeFailurePolicyJson: '{}',
})

const runtimeBackendValue = computed<string>({
  get: () => form.runtimeBackend ?? '',
  set: (v: string) => {
    form.runtimeBackend = v === '' ? null : v
  },
})

const defaultInputsValue = computed<string>({
  get: () => form.defaultInputsJson ?? '',
  set: (v: string) => {
    form.defaultInputsJson = v === '' ? null : v
  },
})

const rules = {
  name: [{ required: true, message: 'Please enter task name' }],
  cronExpression: [{ required: true, message: 'Please enter cron expression' }],
  repositoryUrl: [{ required: true, message: 'Please enter repository URL' }],
  workflowDefinitionJson: [{ required: true, message: 'Workflow definition is required' }],
}

const parsedDefinition = computed<object | null>(() => {
  if (!form.workflowDefinitionJson) return null
  try {
    const parsed = JSON.parse(form.workflowDefinitionJson)
    return typeof parsed === 'object' && parsed !== null ? parsed : null
  } catch {
    return null
  }
})

const workflowParseError = computed<string>(() => {
  if (!form.workflowDefinitionJson) return ''
  try {
    JSON.parse(form.workflowDefinitionJson)
    return ''
  } catch (e) {
    return e instanceof Error ? e.message : 'Invalid JSON'
  }
})

const formatWorkflow = () => {
  try {
    const parsed = JSON.parse(form.workflowDefinitionJson)
    form.workflowDefinitionJson = JSON.stringify(parsed, null, 2)
    message.success('JSON formatted')
  } catch {
    message.error('Cannot format: invalid JSON')
  }
}

const loadDefaultWorkflow = () => {
  form.workflowDefinitionJson = DEFAULT_WORKFLOW_DEFINITION_JSON
  message.info('Loaded default workflow')
}

const loadTask = async () => {
  loading.value = true
  try {
    const task = await taskApi.getById(taskId)
    form.name = task.name
    form.cronExpression = task.cronExpression
    form.repositoryUrl = task.repositoryUrl
    form.baseBranch = task.baseBranch
    form.branchStrategy = task.branchStrategy
    form.maxRuntimeSeconds = task.maxRuntimeSeconds
    form.maxFileChanges = task.maxFileChanges
    form.isEnabled = task.isEnabled
    form.workflowDefinitionJson = task.workflowDefinitionJson || DEFAULT_WORKFLOW_DEFINITION_JSON
    form.defaultInputsJson = task.defaultInputsJson ?? null
    form.runtimeBackend = task.runtimeBackend ?? null
    form.maxConcurrentRuns = task.maxConcurrentRuns
    form.nodeFailurePolicyJson = task.nodeFailurePolicyJson || '{}'
  } catch {
    message.error('Failed to load task')
    router.push('/')
  } finally {
    loading.value = false
  }
}

const onSubmit = async () => {
  if (workflowParseError.value) {
    message.error('Fix workflow JSON errors before submitting')
    return
  }
  submitting.value = true
  try {
    await taskApi.update(taskId, form)
    message.success('Task updated successfully')
    router.push('/')
  } catch {
    message.error('Failed to update task')
  } finally {
    submitting.value = false
  }
}

onMounted(loadTask)
</script>

<style scoped>
.field-help {
  color: #888;
}

.full-width-input {
  width: 100%;
}

.form-section {
  margin-bottom: 16px;
}

.workflow-toolbar {
  margin-bottom: 8px;
}

.workflow-textarea {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
}

.task-form-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-start;
  margin-top: 12px;
}

.task-form-actions.mobile {
  flex-direction: column;
}

.task-form-actions.mobile :deep(.ant-btn) {
  width: 100%;
}
</style>
