import { createRouter, createWebHistory } from 'vue-router'
import TaskList from '../views/TaskList.vue'
import TaskDetail from '../views/TaskDetail.vue'
import TaskCreate from '../views/TaskCreate.vue'

const routes = [
  { path: '/', name: 'Tasks', component: TaskList },
  { path: '/tasks/new', name: 'CreateTask', component: TaskCreate },
  { path: '/tasks/:id', name: 'TaskDetail', component: TaskDetail },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

export default router
