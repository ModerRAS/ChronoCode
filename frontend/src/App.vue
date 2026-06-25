<template>
  <div v-if="setupLoading" class="setup-loading">
    <a-spin size="large" />
  </div>

  <SetupView v-else-if="!initialized" @initialized="handleInitialized" />

  <a-layout v-else class="app-layout">
    <a-layout-sider
      v-if="!isMobile"
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      theme="dark"
      class="app-sider"
    >
      <div class="logo">
        <RobotOutlined v-if="collapsed" />
        <span v-else>ChronoCode</span>
      </div>
      <a-menu
        :selectedKeys="selectedKeys"
        theme="dark"
        mode="inline"
        @click="handleMenuClick"
      >
        <a-menu-item v-for="item in menuItems" :key="item.key">
          <component :is="item.icon" />
          <span>{{ item.label }}</span>
        </a-menu-item>
      </a-menu>
    </a-layout-sider>

    <a-layout>
      <a-layout-header class="app-header">
        <a-button v-if="isMobile" type="text" class="header-trigger" @click="mobileDrawerOpen = true">
          <MenuUnfoldOutlined />
        </a-button>
        <component
          :is="collapsed ? MenuUnfoldOutlined : MenuFoldOutlined"
          v-else
          class="trigger"
          @click="collapsed = !collapsed"
        />
        <h2 class="header-title">{{ pageTitle }}</h2>
      </a-layout-header>

      <a-layout-content class="app-content">
        <router-view />
      </a-layout-content>
    </a-layout>

    <a-drawer v-model:open="mobileDrawerOpen" placement="left" width="280" title="ChronoCode">
      <a-menu :selectedKeys="selectedKeys" mode="inline" @click="handleMenuClick">
        <a-menu-item v-for="item in menuItems" :key="item.key">
          <component :is="item.icon" />
          <span>{{ item.label }}</span>
        </a-menu-item>
      </a-menu>
    </a-drawer>
  </a-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  MenuUnfoldOutlined,
  MenuFoldOutlined,
  HomeOutlined,
  CommentOutlined,
  RobotOutlined,
  SettingOutlined,
} from '@ant-design/icons-vue'
import SetupView from './views/SetupView.vue'
import { setupApi } from './api/setup'
import { useIsMobile } from './composables/useIsMobile'

const router = useRouter()
const route = useRoute()
const { isMobile } = useIsMobile()

const collapsed = ref(false)
const mobileDrawerOpen = ref(false)
const initialized = ref(true)
const setupLoading = ref(true)

const selectedKeys = computed(() => [String(route.meta.menuKey ?? route.path)])
const pageTitle = computed(() => String(route.meta.title ?? 'ChronoCode'))

const menuItems = [
  { key: '/', label: 'Tasks', icon: HomeOutlined },
  { key: '/chat', label: 'AI Chat', icon: CommentOutlined },
  { key: '/settings', label: 'Settings', icon: SettingOutlined },
]

const handleMenuClick = (e: { key: string }) => {
  router.push(e.key)
  mobileDrawerOpen.value = false
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
  font-weight: 700;
  white-space: nowrap;
  overflow: hidden;
}

.app-header {
  background: #fff;
  padding: 0 24px;
  display: flex;
  align-items: center;
  gap: 16px;
  border-bottom: 1px solid #f0f0f0;
}

.app-content {
  padding: 24px;
  min-height: 280px;
}

.header-title {
  margin: 0;
  color: #001529;
  flex: 1;
}

.trigger,
.header-trigger {
  font-size: 18px;
  cursor: pointer;
  transition: color 0.3s;
}

.trigger:hover,
.header-trigger:hover {
  color: #1890ff;
}

.header-trigger {
  padding-inline: 0;
}

@media (max-width: 1024px) {
  .app-header {
    padding: 0 16px;
  }

  .app-content {
    padding: 16px;
  }

  .header-title {
    font-size: 20px;
  }
}
</style>
