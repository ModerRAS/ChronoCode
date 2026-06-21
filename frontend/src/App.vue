<template>
  <div v-if="setupLoading" class="setup-loading">
    <a-spin size="large" />
  </div>

  <SetupView v-else-if="!initialized" @initialized="handleInitialized" />

  <a-layout v-else class="app-layout">
    <a-layout-sider v-model:collapsed="collapsed" :trigger="null" collapsible theme="dark" class="app-sider">
      <div class="logo">
        <RobotOutlined v-if="collapsed" />
        <span v-else>ChronoCode</span>
      </div>
      <a-menu
        v-model:selectedKeys="selectedKeys"
        theme="dark"
        mode="inline"
        @click="handleMenuClick"
      >
        <a-menu-item key="/">
          <HomeOutlined />
          <span>Tasks</span>
        </a-menu-item>
        <a-menu-item key="/chat">
          <CommentOutlined />
          <span>AI Chat</span>
        </a-menu-item>
      </a-menu>
    </a-layout-sider>
    <a-layout>
      <a-layout-header style="background: #fff; padding: 0 24px; display: flex; align-items: center; justify-content: space-between;">
        <menu-unfold-outlined v-if="collapsed" class="trigger" @click="() => (collapsed = !collapsed)" />
        <menu-fold-outlined v-else class="trigger" @click="() => (collapsed = !collapsed)" />
        <h2 style="margin: 0; color: #001529;">AI Scheduled Tasks</h2>
      </a-layout-header>
      <a-layout-content style="padding: 24px; min-height: 280px;">
        <router-view />
      </a-layout-content>
    </a-layout>
  </a-layout>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  MenuUnfoldOutlined,
  MenuFoldOutlined,
  HomeOutlined,
  CommentOutlined,
  RobotOutlined
} from '@ant-design/icons-vue'
import SetupView from './views/SetupView.vue'
import { setupApi } from './api/setup'

const router = useRouter()
const route = useRoute()
const collapsed = ref(false)
const selectedKeys = ref<string[]>([route.path])
const initialized = ref(true)
const setupLoading = ref(true)

watch(() => route.path, (newPath) => {
  selectedKeys.value = [newPath]
})

const handleMenuClick = (e: { key: string }) => {
  router.push(e.key)
}

const loadSetupStatus = async () => {
  try {
    const status = await setupApi.getStatus()
    initialized.value = status.initialized
  } catch {
    initialized.value = true
  } finally {
    setupLoading.value = false
  }
}

const handleInitialized = async () => {
  setupLoading.value = true
  await loadSetupStatus()
  if (initialized.value) {
    router.replace('/')
  }
}

onMounted(loadSetupStatus)
</script>

<style scoped>
.setup-loading {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f6f8fb;
}

.app-layout {
  min-height: 100vh;
}

.app-sider {
  box-shadow: 2px 0 8px rgba(0, 0, 0, 0.15);
}

.logo {
  height: 32px;
  margin: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 18px;
  font-weight: bold;
  white-space: nowrap;
  overflow: hidden;
}

.trigger {
  font-size: 18px;
  cursor: pointer;
  transition: color 0.3s;
}

.trigger:hover {
  color: #1890ff;
}
</style>
