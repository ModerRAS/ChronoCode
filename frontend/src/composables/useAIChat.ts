import { ref } from 'vue';
import type { AIStructuredResponse } from '../utils/aiParser';

interface Message {
  id: string;
  role: 'user' | 'ai';
  content: string;
  timestamp: Date;
}

export function useAIChat(opencodeApiBase: string) {
  const messages = ref<Message[]>([]);
  const isLoading = ref(false);
  const error = ref<string | null>(null);

  const sendMessage = async (content: string): Promise<AIStructuredResponse | null> => {
    isLoading.value = true;
    error.value = null;

    messages.value.push({
      id: crypto.randomUUID(),
      role: 'user',
      content,
      timestamp: new Date()
    });

    try {
      const response = await fetch(`${opencodeApiBase}/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: content })
      });

      const data = await response.text();
      
      messages.value.push({
        id: crypto.randomUUID(),
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
