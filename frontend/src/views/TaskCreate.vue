<template>
  <div>
    <a-card title="Create New Task">
      <a-form :model="form" :rules="rules" layout="vertical" @finish="onSubmit">
        <a-form-item label="Task Name" name="name">
          <a-input v-model:value="form.name" placeholder="Enter task name" />
        </a-form-item>
        
        <a-form-item label="Cron Expression" name="cronExpression">
          <a-input v-model:value="form.cronExpression" placeholder="e.g., 0 * * * *" />
          <template #help>
            <span style="color: #888">Format: minute hour day month weekday</span>
          </template>
        </a-form-item>
        
        <a-form-item label="Repository URL" name="repositoryUrl">
          <a-input v-model:value="form.repositoryUrl" placeholder="https://github.com/owner/repo" />
        </a-form-item>
        
        <a-form-item label="Base Branch" name="baseBranch">
          <a-input v-model:value="form.baseBranch" placeholder="main" />
        </a-form-item>
        
        <a-form-item label="Branch Strategy" name="branchStrategy">
          <a-select v-model:value="form.branchStrategy">
            <a-select-option :value="0">New Branch (recommended)</a-select-option>
            <a-select-option :value="1">Reuse Branch</a-select-option>
          </a-select>
        </a-form-item>
        
        <a-form-item label="AI Prompt" name="prompt">
          <a-textarea v-model:value="form.prompt" :rows="4" placeholder="What should the AI do?" />
        </a-form-item>
        
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="Max Runtime (seconds)" name="maxRuntimeSeconds">
              <a-input-number v-model:value="form.maxRuntimeSeconds" :min="60" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="Max File Changes" name="maxFileChanges">
              <a-input-number v-model:value="form.maxFileChanges" :min="1" :max="100" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        
        <a-form-item name="requirePlanReview">
          <a-checkbox v-model:checked="form.requirePlanReview">
            Require Plan Review before Execution
          </a-checkbox>
        </a-form-item>
        
        <a-form-item name="isEnabled">
          <a-checkbox v-model:checked="form.isEnabled">
            Enable Task immediately
          </a-checkbox>
        </a-form-item>
        
        <a-form-item>
          <a-space>
            <a-button type="primary" html-type="submit" :loading="submitting">Create Task</a-button>
            <a-button @click="$router.push('/')">Cancel</a-button>
          </a-space>
        </a-form-item>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { taskApi, type CreateTaskDto } from '../api/tasks'

const router = useRouter()
const submitting = ref(false)

const form = reactive<CreateTaskDto>({
  name: '',
  cronExpression: '0 * * * *',
  repositoryUrl: '',
  baseBranch: 'main',
  branchStrategy: 0,
  prompt: '',
  maxRuntimeSeconds: 600,
  maxFileChanges: 50,
  requirePlanReview: true,
  isEnabled: true,
})

const rules = {
  name: [{ required: true, message: 'Please enter task name' }],
  cronExpression: [{ required: true, message: 'Please enter cron expression' }],
  repositoryUrl: [{ required: true, message: 'Please enter repository URL' }],
  prompt: [{ required: true, message: 'Please enter AI prompt' }],
}

const onSubmit = async () => {
  submitting.value = true
  try {
    await taskApi.create(form)
    message.success('Task created successfully')
    router.push('/')
  } catch (e) {
    message.error('Failed to create task')
  }
  submitting.value = false
}
</script>
