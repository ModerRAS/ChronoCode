<template>
  <div class="ai-chat-container">
    <a-card title="AI Chat" class="chat-card">
      <template #extra>
        <a-button type="link" @click="clearChat">Clear</a-button>
      </template>
      
      <div class="messages-container" ref="messagesContainer">
        <div v-if="messages.length === 0" class="empty-state">
          <div class="welcome-content">
            <a-avatar size="large" icon="robot" class="welcome-avatar" />
            <h2>Welcome to AI Chat</h2>
            <p>Ask me to create, update, delete, or trigger tasks for you.</p>
            <p class="examples">
              <span>Try: </span>
              <a-tag @click="prefillMessage('Create a task to run every Monday at 9am')">Create a task</a-tag>
              <a-tag @click="prefillMessage('Run task [task-id]')">Run a task</a-tag>
            </p>
          </div>
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
            
            <div v-if="parsedResponses[msg.id] && !confirmedResponses[msg.id]" class="action-buttons">
              <a-divider />
              <p class="action-prompt">Would you like me to execute this action?</p>
              <a-space>
                <a-button type="primary" @click="confirmAction(msg.id)">Confirm</a-button>
                <a-button @click="dismissAction(msg.id)">Dismiss</a-button>
              </a-space>
            </div>
            
            <div v-if="actionResults[msg.id]" :class="['action-result', actionResults[msg.id].success ? 'success' : 'error']">
              <a-alert
                :message="actionResults[msg.id].message"
                :type="actionResults[msg.id].success ? 'success' : 'error'"
                show-icon
              />
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
import { executeAIResponse, type ExecuteResult } from '../utils/taskApiIntegration'

const OPENCODE_API_BASE = '/api'

const { messages, isLoading, error, sendMessage } = useAIChat(OPENCODE_API_BASE)

const inputMessage = ref('')
const messagesContainer = ref<HTMLElement | null>(null)
const parsedResponses = reactive<Record<string, AIStructuredResponse | null>>({})
const confirmedResponses = reactive<Record<string, boolean>>({})
const actionResults = reactive<Record<string, ExecuteResult>>({})

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

const confirmAction = async (msgId: string) => {
  const parsed = parsedResponses[msgId]
  if (!parsed) return
  
  confirmedResponses[msgId] = true
  actionResults[msgId] = { success: false, message: 'Executing...' }
  
  const result = await executeAIResponse(parsed)
  actionResults[msgId] = result
}

const dismissAction = (msgId: string) => {
  confirmedResponses[msgId] = true
  actionResults[msgId] = { success: false, message: 'Action dismissed' }
}

const prefillMessage = (text: string) => {
  inputMessage.value = text
}

const clearChat = () => {
  messages.value = []
  Object.keys(parsedResponses).forEach(key => delete parsedResponses[key])
  Object.keys(confirmedResponses).forEach(key => delete confirmedResponses[key])
  Object.keys(actionResults).forEach(key => delete actionResults[key])
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

.welcome-content {
  text-align: center;
  padding: 32px;
}

.welcome-avatar {
  background: #722ed1;
  margin-bottom: 16px;
}

.welcome-content h2 {
  margin: 0 0 8px;
  color: var(--ant-text-color, rgba(0, 0, 0, 0.85));
}

.welcome-content p {
  margin: 0 0 12px;
  color: var(--ant-text-color-secondary, rgba(0, 0, 0, 0.45));
}

.welcome-content .examples {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  flex-wrap: wrap;
}

.welcome-content .examples span {
  color: var(--ant-text-color-secondary, rgba(0, 0, 0, 0.45));
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

.action-buttons {
  margin-top: 12px;
}

.action-prompt {
  margin: 0 0 8px;
  font-size: 13px;
  color: var(--ant-text-color-secondary, rgba(0, 0, 0, 0.45));
}

.action-result {
  margin-top: 12px;
}

.input-container {
  padding: 16px;
  border-top: 1px solid var(--ant-border-color, #f0f0f0);
}

.error-alert {
  margin-bottom: 12px;
}
</style>
