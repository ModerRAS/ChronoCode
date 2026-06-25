import { ref, watch } from 'vue';
import { parseAIResponse } from '../utils/aiParser';

interface Message {
  id: string;
  role: 'user' | 'ai';
  content: string;
  timestamp: Date;
}

interface HistoryMessage {
  role: 'user' | 'ai';
  content: string;
}

const STORAGE_KEY = 'chronocode-ai-chat-messages';

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

function loadMessages(): Message[] {
  if (typeof window === 'undefined') {
    return [];
  }

  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw) as Array<Omit<Message, 'timestamp'> & { timestamp: string }>;
    return Array.isArray(parsed)
      ? parsed.map((m) => ({ ...m, timestamp: new Date(m.timestamp) }))
      : [];
  } catch {
    return [];
  }
}

function saveMessages(items: Message[]): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch {
    // Ignore storage errors (e.g. quota exceeded, private mode).
  }
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
  const messages = ref<Message[]>(loadMessages());
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  watch(
    messages,
    (items) => {
      saveMessages(items);
    },
    { deep: true }
  );

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
      const history: HistoryMessage[] = messages.value
        .slice(0, -1)
        .map(({ role, content }) => ({ role, content }));

      const response = await fetch(`${opencodeApiBase}/ai/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: content, history })
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

  const clearChat = (): void => {
    messages.value = [];
    if (typeof window !== 'undefined') {
      try {
        localStorage.removeItem(STORAGE_KEY);
      } catch {
        // Ignore storage errors.
      }
    }
  };

  return { messages, isLoading, error, sendMessage, clearChat };
}
