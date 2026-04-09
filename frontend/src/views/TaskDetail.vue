<template>
  <div>
    <a-page-header title="Task Details" @back="$router.push('/')">
      <template #extra>
        <a-space>
          <a-button @click="$router.push(`/tasks/${taskId}/edit`)">Edit</a-button>
          <a-button type="primary" @click="triggerTask" :loading="triggering">Run Now</a-button>
        </a-space>
      </template>
    </a-page-header>
    
    <a-spin :spinning="loading">
      <a-descriptions bordered :column="2">
        <a-descriptions-item label="Name">{{ task?.name }}</a-descriptions-item>
        <a-descriptions-item label="Status">
          <a-tag :color="getStatusColor(task?.lastStatus)">{{ getStatusText(task?.lastStatus) }}</a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="Cron">{{ task?.cronExpression }}</a-descriptions-item>
        <a-descriptions-item label="Enabled">
          <a-switch :checked="task?.isEnabled" disabled />
        </a-descriptions-item>
        <a-descriptions-item label="Repository" :span="2">{{ task?.repositoryUrl }}</a-descriptions-item>
        <a-descriptions-item label="Base Branch">{{ task?.baseBranch }}</a-descriptions-item>
        <a-descriptions-item label="Branch Strategy">
          {{ task?.branchStrategy === 0 ? 'New Branch' : 'Reuse Branch' }}
        </a-descriptions-item>
        <a-descriptions-item label="Max Runtime">{{ task?.maxRuntimeSeconds }}s</a-descriptions-item>
        <a-descriptions-item label="Max Files">{{ task?.maxFileChanges }}</a-descriptions-item>
        <a-descriptions-item label="Require Review">{{ task?.requirePlanReview ? 'Yes' : 'No' }}</a-descriptions-item>
        <a-descriptions-item label="Last Run">{{ task?.lastRunAt || 'Never' }}</a-descriptions-item>
        <a-descriptions-item label="Prompt" :span="2">{{ task?.prompt }}</a-descriptions-item>
        <a-descriptions-item label="Last Error" :span="2">
          <span v-if="task?.lastError" style="color: red">{{ task.lastError }}</span>
          <span v-else>None</span>
        </a-descriptions-item>
      </a-descriptions>
    </a-spin>
    
    <a-divider>Execution History</a-divider>
    
    <a-table :columns="execColumns" :data-source="executions" :loading="execLoading" row-key="id">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="getStatusColor(record.status)">
            {{ getStatusText(record.status) }}
          </a-tag>
        </template>
        <template v-if="column.key === 'actions'">
          <a-button size="small" @click="viewLogs(record.id)">View Logs</a-button>
        </template>
      </template>
    </a-table>
    
    <a-drawer v-model:open="logsVisible" title="Execution Logs" width="600" placement="right">
      <a-timeline>
        <a-timeline-item v-for="log in logs" :key="log.timestamp" :color="getLogColor(log.level)">
          <p><strong>{{ log.level }}</strong> - {{ formatTime(log.timestamp) }}</p>
          <p>{{ log.message }}</p>
        </a-timeline-item>
      </a-timeline>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import { taskApi, type ScheduledTask, type Execution, type LogEntry } from '../api/tasks'

const route = useRoute()
const taskId = route.params.id as string

const loading = ref(false)
const execLoading = ref(false)
const triggering = ref(false)
const task = ref<ScheduledTask>()
const executions = ref<Execution[]>([])
const logs = ref<LogEntry[]>([])
const logsVisible = ref(false)

const execColumns = [
  { title: 'Started', dataIndex: 'startedAt', key: 'started' },
  { title: 'Completed', dataIndex: 'completedAt', key: 'completed' },
  { title: 'Status', key: 'status' },
  { title: 'Files Changed', dataIndex: 'filesChanged', key: 'files' },
  { title: 'Branch', dataIndex: 'branchName', key: 'branch' },
  { title: 'Actions', key: 'actions' },
]

const loadTask = async () => {
  loading.value = true
  try {
    task.value = await taskApi.getById(taskId)
  } catch (e) {
    message.error('Failed to load task')
  }
  loading.value = false
}

const loadExecutions = async () => {
  execLoading.value = true
  try {
    executions.value = await taskApi.getExecutions(taskId)
  } catch (e) {
    message.error('Failed to load executions')
  }
  execLoading.value = false
}

const triggerTask = async () => {
  triggering.value = true
  try {
    await taskApi.trigger(taskId)
    message.success('Task triggered')
    setTimeout(loadExecutions, 1000)
  } catch (e) {
    message.error('Failed to trigger task')
  }
  triggering.value = false
}

const viewLogs = async (execId: string) => {
  try {
    logs.value = await taskApi.getLogs(execId)
    logsVisible.value = true
  } catch (e) {
    message.error('Failed to load logs')
  }
}

const getStatusColor = (status?: number) => {
  const colors = ['', 'blue', 'green', 'red', 'default']
  return colors[status || 0] || ''
}

const getStatusText = (status?: number) => {
  const texts = ['Pending', 'Running', 'Completed', 'Failed', 'Cancelled']
  return texts[status || 0] || 'Unknown'
}

const getLogColor = (level: string) => {
  if (level === 'Error') return 'red'
  if (level === 'Warning') return 'orange'
  return 'blue'
}

const formatTime = (timestamp: string) => {
  return new Date(timestamp).toLocaleString()
}

onMounted(() => {
  loadTask()
  loadExecutions()
})
</script>
