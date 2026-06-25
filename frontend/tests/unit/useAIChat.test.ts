import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { useAIChat } from '../../src/composables/useAIChat';

// Mock global fetch
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

// Mock crypto.randomUUID for deterministic IDs
vi.stubGlobal('crypto', {
  randomUUID: () => 'test-uuid-0001',
});

describe('useAIChat', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('starts with empty messages and no loading', () => {
    const { messages, isLoading, error } = useAIChat('/api');

    expect(messages.value).toEqual([]);
    expect(isLoading.value).toBe(false);
    expect(error.value).toBeNull();
  });

  it('sends message and displays the natural language response', async () => {
    const aiResponse = JSON.stringify({
      action: '',
      task: null,
      error: { code: 'INFO', message: 'Created a daily build task for you.' },
    });

    mockFetch.mockResolvedValueOnce({
      text: () => Promise.resolve(aiResponse),
    });

    const { messages, isLoading, sendMessage } = useAIChat('/api');

    await sendMessage('Create a task');

    expect(messages.value.length).toBe(2);
    expect(messages.value[0].role).toBe('user');
    expect(messages.value[0].content).toBe('Create a task');
    expect(messages.value[1].role).toBe('ai');
    expect(messages.value[1].content).toBe('Created a daily build task for you.');
  });

  it('summarizes actionable legacy responses without confirmation buttons', async () => {
    const aiResponse = JSON.stringify({
      action: 'create_task',
      task: { name: 'Test', cron: '0 9 * * *', repository: 'https://github.com/test/repo' },
    });

    mockFetch.mockResolvedValueOnce({
      text: () => Promise.resolve(aiResponse),
    });

    const { messages, sendMessage } = useAIChat('/api');

    await sendMessage('Create a task');

    expect(messages.value[1].content).toBe('Action: Create task\nTask: Test');
  });

  it('sets isLoading during request', async () => {
    let resolveFn: (value: { text: () => Promise<string> }) => void;
    mockFetch.mockReturnValueOnce(new Promise(resolve => { resolveFn = resolve; }));

    const { isLoading, sendMessage } = useAIChat('/api');

    const sendPromise = sendMessage('test');
    expect(isLoading.value).toBe(true);

    resolveFn!({ text: () => Promise.resolve('{}') });
    await sendPromise;

    expect(isLoading.value).toBe(false);
  });

  it('sets error on fetch failure', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'));

    const { error, sendMessage } = useAIChat('/api');

    await sendMessage('test');

    expect(error.value).toBe('Network error');
  });

  it('sets error on non-Error exception', async () => {
    mockFetch.mockRejectedValueOnce('string error');

    const { error, sendMessage } = useAIChat('/api');

    await sendMessage('test');

    expect(error.value).toBe('Network error');
  });

  it('calls fetch with correct URL and body', async () => {
    mockFetch.mockResolvedValueOnce({ text: () => Promise.resolve('{}') });

    const { sendMessage } = useAIChat('/api');

    await sendMessage('Create a build task');

    expect(mockFetch).toHaveBeenCalledTimes(1);
    const [url, options] = mockFetch.mock.calls[0];
    expect(url).toBe('/api/ai/message');
    expect(options.method).toBe('POST');
    expect(options.headers['Content-Type']).toBe('application/json');
    const body = JSON.parse(options.body);
    expect(body.message).toBe('Create a build task');
  });

  it('clears error on new message', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'));

    const { error, sendMessage } = useAIChat('/api');

    await sendMessage('first');
    expect(error.value).toBe('Network error');

    mockFetch.mockResolvedValueOnce({ text: () => Promise.resolve('{}') });
    await sendMessage('second');
    expect(error.value).toBeNull();
  });

  it('generates unique message IDs', async () => {
    // Reset mock to return different UUIDs each call
    let uuidCounter = 0;
    vi.stubGlobal('crypto', {
      randomUUID: () => `test-uuid-${String(++uuidCounter).padStart(4, '0')}`,
    });

    mockFetch.mockResolvedValueOnce({ text: () => Promise.resolve('{}') });

    const { messages, sendMessage } = useAIChat('/api');

    await sendMessage('test');

    expect(messages.value[0].id).toBe('test-uuid-0001');
    expect(messages.value[1].id).toBe('test-uuid-0002');
    expect(messages.value[0].id).not.toBe(messages.value[1].id);
  });
});
