import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { useAIChat } from '../../src/composables/useAIChat';

const mockCreateConversation = vi.fn();
const mockGetConversation = vi.fn();
const mockSendMessage = vi.fn();
const mockDeleteConversation = vi.fn();

vi.mock('../../src/api/chat', () => ({
  chatApi: {
    createConversation: (...args: unknown[]) => mockCreateConversation(...args),
    getConversation: (...args: unknown[]) => mockGetConversation(...args),
    sendMessage: (...args: unknown[]) => mockSendMessage(...args),
    deleteConversation: (...args: unknown[]) => mockDeleteConversation(...args),
  },
}));

const mockLocalStorage = {
  getItem: vi.fn(() => null),
  setItem: vi.fn(),
  removeItem: vi.fn(),
};
vi.stubGlobal('localStorage', mockLocalStorage);

describe('useAIChat', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockLocalStorage.getItem.mockReturnValue(null);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts with empty messages and no loading', () => {
    const { messages, isLoading, error } = useAIChat();

    expect(messages.value).toEqual([]);
    expect(isLoading.value).toBe(false);
    expect(error.value).toBeNull();
  });

  it('creates a new conversation when none is stored', async () => {
    mockCreateConversation.mockResolvedValue({
      id: 'conv-1',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [],
    });

    const { messages, loadConversation } = useAIChat();
    await loadConversation();

    expect(mockCreateConversation).toHaveBeenCalledTimes(1);
    expect(messages.value).toEqual([]);
  });

  it('loads an existing conversation from the backend', async () => {
    mockLocalStorage.getItem.mockReturnValue('conv-1');
    mockGetConversation.mockResolvedValue({
      id: 'conv-1',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [
        { id: 'm1', role: 'user', content: 'hello', createdAt: new Date().toISOString() },
        { id: 'm2', role: 'ai', content: 'hi', createdAt: new Date().toISOString() },
      ],
    });

    const { messages, loadConversation } = useAIChat();
    await loadConversation();

    expect(mockGetConversation).toHaveBeenCalledWith('conv-1');
    expect(messages.value.length).toBe(2);
    expect(messages.value[0].role).toBe('user');
    expect(messages.value[1].role).toBe('ai');
  });

  it('recovers from a missing stored conversation by creating a new one', async () => {
    mockLocalStorage.getItem.mockReturnValue('dead-conv');
    const axiosError = new Error('Not found');
    (axiosError as any).isAxiosError = true;
    (axiosError as any).response = { status: 404 };
    mockGetConversation.mockRejectedValue(axiosError);
    mockCreateConversation.mockResolvedValue({
      id: 'conv-new',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [],
    });

    const { messages, loadConversation } = useAIChat();
    await loadConversation();

    expect(mockGetConversation).toHaveBeenCalledWith('dead-conv');
    expect(mockCreateConversation).toHaveBeenCalledTimes(1);
    expect(messages.value).toEqual([]);
  });

  it('sends a message and appends the assistant response', async () => {
    mockCreateConversation.mockResolvedValue({
      id: 'conv-1',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [],
    });
    mockSendMessage.mockResolvedValue({
      id: 'm2',
      role: 'ai',
      content: 'Created a daily build task for you.',
      createdAt: new Date().toISOString(),
    });

    const { messages, sendMessage } = useAIChat();
    await sendMessage('Create a task');

    expect(messages.value.length).toBe(2);
    expect(messages.value[0].role).toBe('user');
    expect(messages.value[0].content).toBe('Create a task');
    expect(messages.value[1].role).toBe('ai');
    expect(messages.value[1].content).toBe('Created a daily build task for you.');
  });

  it('sets isLoading during request', async () => {
    let resolveFn: (value: { id: string; role: 'ai'; content: string; createdAt: string }) => void;
    mockSendMessage.mockReturnValueOnce(new Promise(resolve => { resolveFn = resolve; }));
    mockCreateConversation.mockResolvedValue({
      id: 'conv-1',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [],
    });

    const { isLoading, sendMessage } = useAIChat();

    const sendPromise = sendMessage('test');
    expect(isLoading.value).toBe(true);

    resolveFn!({ id: 'm1', role: 'ai', content: 'ok', createdAt: new Date().toISOString() });
    await sendPromise;

    expect(isLoading.value).toBe(false);
  });

  it('sets error on API failure', async () => {
    mockCreateConversation.mockResolvedValue({
      id: 'conv-1',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [],
    });
    mockSendMessage.mockRejectedValue(new Error('Network error'));

    const { error, sendMessage } = useAIChat();
    await sendMessage('test');

    expect(error.value).toBe('Network error');
  });

  it('clears conversation and creates a new one', async () => {
    mockLocalStorage.getItem.mockReturnValue('conv-1');
    mockCreateConversation.mockResolvedValue({
      id: 'conv-2',
      title: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      messages: [],
    });
    mockDeleteConversation.mockResolvedValue(undefined);

    const { messages, clearChat } = useAIChat();
    await clearChat();

    expect(mockDeleteConversation).toHaveBeenCalledWith('conv-1');
    expect(mockLocalStorage.removeItem).toHaveBeenCalledWith('chronocode-ai-chat-conversation-id');
    expect(messages.value).toEqual([]);
  });
});
