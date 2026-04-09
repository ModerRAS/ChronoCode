import { ref } from 'vue';
import type { AIStructuredResponse } from '../utils/aiParser';

interface Message {
  id: string;
  role: 'user' | 'ai';
  content: string;
  timestamp: Date;
}

function generateId(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return generateId();
  }
  // Fallback for older browsers
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function useAIChat(opencodeApiBase: string) {
  const messages = ref<Message[]>([]);
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const sendMessage = async (content: string): Promise<AIStructuredResponse | null> => {
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
      
      messages.value.push({
        id: generateId(),
        role: 'ai',
        content: data,
        timestamp: new Date()
      });

      return null;
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Network error';
      return null;
    } finally {
      isLoading.value = false;
    }
  };

  return { messages, isLoading, error, sendMessage };
}
