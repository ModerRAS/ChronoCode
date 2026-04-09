import { createRouter, createWebHistory } from 'vue-router'
import TaskList from '../views/TaskList.vue'
import TaskDetail from '../views/TaskDetail.vue'
import TaskCreate from '../views/TaskCreate.vue'
import TaskUpdate from '../views/TaskUpdate.vue'
import AiChat from '../views/AiChat.vue'

const routes = [
  { path: '/', name: 'Tasks', component: TaskList },
  { path: '/tasks/new', name: 'CreateTask', component: TaskCreate },
  { path: '/tasks/:id/edit', name: 'EditTask', component: TaskUpdate },
  { path: '/tasks/:id', name: 'TaskDetail', component: TaskDetail },
  { path: '/chat', name: 'AiChat', component: AiChat },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
