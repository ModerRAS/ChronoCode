<template>
  <div>
    <div style="margin-bottom: 16px; display: flex; justify-content: space-between">
      <a-button type="primary" @click="$router.push('/tasks/new')">Create Task</a-button>
      <a-button @click="loadTasks">Refresh</a-button>
    </div>
    <a-table :columns="columns" :data-source="tasks" :loading="loading" row-key="id">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="getStatusColor(record.lastStatus)">
            {{ getStatusText(record.lastStatus) }}
          </a-tag>
        </template>
        <template v-if="column.key === 'enabled'">
          <a-switch :checked="record.isEnabled" disabled />
        </template>
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button size="small" @click="$router.push(`/tasks/${record.id}`)">View</a-button>
            <a-button size="small" type="primary" @click="triggerTask(record.id)">Run</a-button>
            <a-button size="small" danger @click="deleteTask(record.id)">Delete</a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { taskApi, type ScheduledTask } from '../api/tasks'

const tasks = ref<ScheduledTask[]>([])
const loading = ref(false)

const columns = [
  { title: 'Name', dataIndex: 'name', key: 'name' },
  { title: 'Cron', dataIndex: 'cronExpression', key: 'cron' },
  { title: 'Repository', dataIndex: 'repositoryUrl', key: 'repo', ellipsis: true },
  { title: 'Status', key: 'status' },
  { title: 'Enabled', key: 'enabled' },
  { title: 'Last Run', dataIndex: 'lastRunAt', key: 'lastRun' },
  { title: 'Actions', key: 'actions' },
]

const loadTasks = async () => {
  loading.value = true
  try {
    tasks.value = await taskApi.getAll()
  } catch (e) {
    message.error('Failed to load tasks')
  }
  loading.value = false
}

const triggerTask = async (id: string) => {
  try {
    await taskApi.trigger(id)
    message.success('Task triggered')
  } catch (e) {
    message.error('Failed to trigger task')
  }
}

const deleteTask = async (id: string) => {
  try {
    await taskApi.delete(id)
    message.success('Task deleted')
    loadTasks()
  } catch (e) {
    message.error('Failed to delete task')
  }
}

const getStatusColor = (status: number) => {
  const colors = ['', 'blue', 'green', 'red', 'default']
  return colors[status] || ''
}

const getStatusText = (status: number) => {
  const texts = ['Pending', 'Running', 'Completed', 'Failed', 'Cancelled']
  return texts[status] || 'Unknown'
}

onMounted(loadTasks)
</script>
