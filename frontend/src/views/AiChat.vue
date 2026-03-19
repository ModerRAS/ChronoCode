<template>
  <div class="ai-chat-container">
    <a-card title="AI Chat" class="chat-card">
      <template #extra>
        <a-button type="link" @click="clearChat">Clear</a-button>
      </template>
      
      <div class="messages-container" ref="messagesContainer">
        <div v-if="messages.length === 0" class="empty-state">
          <a-empty description="Start a conversation with the AI" />
        </div>
        
        <div
          v-for="msg in messages"
          :key="msg.id"
          :class="['message', msg.role]"
        >
          <div class="message-avatar">
            <a-avatar :class="msg.role" :icon="msg.role === 'user' ? 'user' : 'robot'" />
          </div>
          <div class="message-content">
            <div class="message-header">
              <span class="message-role">{{ msg.role === 'user' ? 'You' : 'AI' }}</span>
              <span class="message-time">{{ formatTime(msg.timestamp) }}</span>
            </div>
            <div class="message-body">
              <pre v-if="parsedResponses[msg.id]">{{ JSON.stringify(parsedResponses[msg.id], null, 2) }}</pre>
              <pre v-else>{{ msg.content }}</pre>
            </div>
          </div>
        </div>
        
        <div v-if="isLoading" class="message ai loading">
          <div class="message-avatar">
            <a-avatar class="ai" icon="robot" />
          </div>
          <div class="message-content">
            <a-spin size="small" />
          </div>
        </div>
      </div>
      
      <div class="input-container">
        <a-alert v-if="error" :message="error" type="error" show-icon class="error-alert" />
        <a-input-search
          v-model:value="inputMessage"
          placeholder="Ask the AI to create or manage tasks..."
          enter-button="Send"
          :loading="isLoading"
          @search="handleSend"
        />
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, nextTick, watch } from 'vue'
import { useAIChat } from '../composables/useAIChat'
import { parseAIResponse, type AIStructuredResponse } from '../utils/aiParser'

const OPENCODE_API_BASE = 'http://localhost:5000/api'

const { messages, isLoading, error, sendMessage } = useAIChat(OPENCODE_API_BASE)

const inputMessage = ref('')
const messagesContainer = ref<HTMLElement | null>(null)
const parsedResponses = reactive<Record<string, AIStructuredResponse | null>>({})

const handleSend = async () => {
  if (!inputMessage.value.trim()) return
  
  const content = inputMessage.value
  inputMessage.value = ''
  
  await sendMessage(content)
  
  const lastMsg = messages.value[messages.value.length - 1]
  if (lastMsg && lastMsg.role === 'ai') {
    parsedResponses[lastMsg.id] = parseAIResponse(lastMsg.content)
  }
}

const clearChat = () => {
  messages.value = []
  Object.keys(parsedResponses).forEach(key => delete parsedResponses[key])
}

const formatTime = (date: Date) => {
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

watch(messages, async () => {
  await nextTick()
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
})
</script>

<style scoped>
.ai-chat-container {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.chat-card {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.chat-card :deep(.ant-card-body) {
  display: flex;
  flex-direction: column;
  height: calc(100% - 56px);
  padding: 0;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.message {
  display: flex;
  gap: 12px;
  max-width: 80%;
}

.message.user {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.message.ai {
  align-self: flex-start;
}

.message-avatar {
  flex-shrink: 0;
}

.message-avatar :deep(.ant-avatar) {
  background: var(--ant-primary-color, #1890ff);
}

.message-avatar :deep(.ant-avatar.ai) {
  background: #722ed1;
}

.message-content {
  flex: 1;
  min-width: 0;
}

.message-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 4px;
  font-size: 12px;
}

.message-role {
  font-weight: 500;
  color: var(--ant-text-color, rgba(0, 0, 0, 0.85));
}

.message-time {
  color: var(--ant-text-color-secondary, rgba(0, 0, 0, 0.45));
}

.message-body {
  background: var(--ant-component-background, #fff);
  border: 1px solid var(--ant-border-color, #f0f0f0);
  border-radius: 8px;
  padding: 12px;
}

.message.user .message-body {
  background: var(--ant-primary-color, #1890ff);
  border-color: var(--ant-primary-color, #1890ff);
  color: #fff;
}

.message-body pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 14px;
  line-height: 1.5;
}

.message.ai.loading .message-content {
  display: flex;
  align-items: center;
}

.input-container {
  padding: 16px;
  border-top: 1px solid var(--ant-border-color, #f0f0f0);
}

.error-alert {
  margin-bottom: 12px;
}
</style>
