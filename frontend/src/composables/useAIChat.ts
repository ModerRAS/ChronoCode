import { ref } from 'vue';
import { parseAIResponse } from '../utils/aiParser';

interface Message {
  id: string;
  role: 'user' | 'ai';
  content: string;
  timestamp: Date;
}

function generateId(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  // Fallback for older browsers
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

function extractDisplayText(raw: string): string {
  const parsed = parseAIResponse(raw);
  if (!parsed) {
    return raw;
  }

  if (parsed.action === '') {
    return parsed.error?.message ?? raw;
  }

  // Actionable fallback (legacy prompt mode) – summarize without asking for confirmation,
  // since the backend skill path now executes actions directly.
  const actionLabels: Record<string, string> = {
    create_task: 'Create task',
    update_task: 'Update task',
    delete_task: 'Delete task',
    trigger_task: 'Trigger task',
  };
  const taskName = parsed.task?.name;
  const taskId = parsed.task_id;
  return [
    `Action: ${actionLabels[parsed.action] ?? parsed.action}`,
    taskName ? `Task: ${taskName}` : null,
    taskId ? `Task ID: ${taskId}` : null,
  ].filter(Boolean).join('\n');
}

export function useAIChat(opencodeApiBase: string) {
  const messages = ref<Message[]>([]);
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const sendMessage = async (content: string): Promise<void> => {
    isLoading.value = true;
    error.value = null;

    messages.value.push({
      id: generateId(),
      role: 'user',
      content,
      timestamp: new Date()
    });

    try {
      const response = await fetch(`${opencodeApiBase}/ai/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: content })
      });

      const data = await response.text();
      const displayText = extractDisplayText(data);

      messages.value.push({
        id: generateId(),
        role: 'ai',
        content: displayText,
        timestamp: new Date()
      });
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Network error';
    } finally {
      isLoading.value = false;
    }
  };

  return { messages, isLoading, error, sendMessage };
}
