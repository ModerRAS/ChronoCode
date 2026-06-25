import { createRouter, createWebHistory } from 'vue-router'
import TaskList from '../views/TaskList.vue'
import TaskDetail from '../views/TaskDetail.vue'
import TaskCreate from '../views/TaskCreate.vue'
import TaskUpdate from '../views/TaskUpdate.vue'
import AiChat from '../views/AiChat.vue'
import SettingsView from '../views/SettingsView.vue'

const routes = [
  {
    path: '/',
    name: 'Tasks',
    component: TaskList,
    meta: { menuKey: '/', title: 'Tasks' },
  },
  {
    path: '/tasks/new',
    name: 'CreateTask',
    component: TaskCreate,
    meta: { menuKey: '/', title: 'Create Task' },
  },
  {
    path: '/tasks/:id/edit',
    name: 'EditTask',
    component: TaskUpdate,
    meta: { menuKey: '/', title: 'Edit Task' },
  },
  {
    path: '/tasks/:id',
    name: 'TaskDetail',
    component: TaskDetail,
    meta: { menuKey: '/', title: 'Task Details' },
  },
  {
    path: '/chat',
    name: 'AiChat',
    component: AiChat,
    meta: { menuKey: '/chat', title: 'AI Chat' },
  },
  {
    path: '/settings',
    name: 'Settings',
    component: SettingsView,
    meta: { menuKey: '/settings', title: 'Settings' },
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
