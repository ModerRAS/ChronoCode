<template>
  <div>
    <div class="task-list-toolbar">
      <a-button type="primary" @click="$router.push('/tasks/new')">Create Task</a-button>
      <a-button @click="loadTasks">Refresh</a-button>
    </div>

    <a-empty v-if="!loading && tasks.length === 0" description="No tasks yet" class="empty-state">
      <template #image>
        <a-avatar size="large" class="empty-avatar">
          <template #icon>
            <RobotOutlined />
          </template>
        </a-avatar>
      </template>
      <p>Create your first task to get started</p>
      <a-button type="primary" @click="$router.push('/tasks/new')">Create Task</a-button>
    </a-empty>

    <template v-else-if="isMobile">
      <a-spin :spinning="loading">
        <div class="mobile-task-list" data-testid="task-mobile-list">
          <a-card v-for="task in tasks" :key="task.id" class="mobile-task-card">
            <template #title>
              <div class="mobile-task-title">{{ task.name }}</div>
            </template>

            <a-space direction="vertical" size="small" class="mobile-task-meta">
              <div><strong>Cron:</strong> {{ task.cronExpression }}</div>
              <div><strong>Repository:</strong> {{ task.repositoryUrl }}</div>
              <div>
                <strong>Status:</strong>
                <a-tag :color="getStatusColor(task.lastStatus)">{{ getStatusText(task.lastStatus) }}</a-tag>
              </div>
              <div><strong>Enabled:</strong> {{ task.isEnabled ? 'Yes' : 'No' }}</div>
              <div><strong>Last Run:</strong> {{ task.lastRunAt || 'Never' }}</div>
            </a-space>

            <a-space wrap class="mobile-task-actions">
              <a-button size="small" @click="$router.push(`/tasks/${task.id}/edit`)">Edit</a-button>
              <a-button size="small" type="primary" @click="triggerTask(task.id)">Run</a-button>
              <a-button size="small" danger @click="deleteTask(task.id)">Delete</a-button>
            </a-space>
          </a-card>
        </div>
      </a-spin>
    </template>

    <a-table
      v-else
      :columns="columns"
      :data-source="tasks"
      :loading="loading"
      row-key="id"
      data-testid="task-desktop-table"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="getStatusColor(record.lastStatus)">
            {{ getStatusText(record.lastStatus) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'enabled'">
          <a-switch :checked="record.isEnabled" disabled />
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button size="small" @click="$router.push(`/tasks/${record.id}/edit`)">Edit</a-button>
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
import { RobotOutlined } from '@ant-design/icons-vue'
import { taskApi, type ScheduledTask } from '../api/tasks'
import { useIsMobile } from '../composables/useIsMobile'

const { isMobile } = useIsMobile()
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
  } catch {
    message.error('Failed to load tasks')
  } finally {
    loading.value = false
  }
}

const triggerTask = async (id: string) => {
  try {
    await taskApi.trigger(id)
    message.success('Task triggered')
  } catch {
    message.error('Failed to trigger task')
  }
}

const deleteTask = async (id: string) => {
  try {
    await taskApi.delete(id)
    message.success('Task deleted')
    await loadTasks()
  } catch {
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

<style scoped>
.task-list-toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.mobile-task-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mobile-task-card {
  width: 100%;
}

.mobile-task-title {
  font-weight: 600;
}

.mobile-task-meta {
  width: 100%;
}

.mobile-task-actions {
  margin-top: 16px;
}

.empty-state {
  padding: 48px 0;
  text-align: center;
}

.empty-avatar {
  background: #722ed1;
  margin-bottom: 16px;
}

.empty-state p {
  color: var(--ant-text-color-secondary, rgba(0, 0, 0, 0.45));
  margin-bottom: 16px;
}

@media (max-width: 1024px) {
  .task-list-toolbar {
    flex-direction: column;
  }
}
</style>
