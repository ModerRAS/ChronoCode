import { describe, expect, it, vi, beforeEach } from 'vitest';

// Mock axios before importing the module under test
vi.mock('axios', () => {
  const api = {
    post: vi.fn(),
  };
  return {
    default: {
      create: vi.fn(() => api),
      isAxiosError: (err: unknown) => err && typeof err === 'object' && 'isAxiosError' in err,
    },
  };
});

import { executeAIResponse } from '../../src/utils/taskApiIntegration';

const axios = (await import('axios')).default;
const mockApi = (axios.create as ReturnType<typeof vi.fn>).mock.results[0].value;

const workflowJson = JSON.stringify({ version: 1, startNodeId: 'start', nodes: [] });

describe('executeAIResponse', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns success for create_task', async () => {
    mockApi.post.mockResolvedValue({ data: { id: 'task-1' } });

    const result = await executeAIResponse({
      action: 'create_task',
      task: {
        name: 'Test',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowJson,
        base_branch: 'main',
        branch_strategy: 'new',
        max_runtime_seconds: 600,
        max_file_changes: 50,
        is_enabled: true,
        max_concurrent_runs: 1,
        node_failure_policy_json: '{}',
      },
    } as any);

    expect(result.success).toBe(true);
    expect(result.message).toBe('Task created successfully');
    expect(mockApi.post).toHaveBeenCalledWith('/ai/ai', expect.any(Object));
  });

  it('returns success for update_task', async () => {
    mockApi.post.mockResolvedValue({ data: {} });

    const result = await executeAIResponse({
      action: 'update_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: {
        name: 'Updated',
        cron: '0 10 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowJson,
      },
    } as any);

    expect(result.success).toBe(true);
    expect(result.message).toBe('Task updated successfully');
  });

  it('returns success for delete_task', async () => {
    mockApi.post.mockResolvedValue({ data: {} });

    const result = await executeAIResponse({
      action: 'delete_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: null,
    } as any);

    expect(result.success).toBe(true);
    expect(result.message).toBe('Task deleted successfully');
  });

  it('returns success for trigger_task', async () => {
    mockApi.post.mockResolvedValue({ data: {} });

    const result = await executeAIResponse({
      action: 'trigger_task',
      task_id: '123e4567-e89b-12d3-a456-426614174000',
      task: null,
    } as any);

    expect(result.success).toBe(true);
    expect(result.message).toBe('Task triggered successfully');
  });

  it('returns failure for non-actionable response', async () => {
    const result = await executeAIResponse({
      action: '',
      task: null,
      error: { code: 'INFO', message: 'Cannot help with that' },
    } as any);

    expect(result.success).toBe(false);
    expect(result.message).toBe('Cannot help with that');
    expect(mockApi.post).not.toHaveBeenCalled();
  });

  it('returns failure message on API error', async () => {
    const error = {
      isAxiosError: true,
      response: { data: { error: { message: 'Validation failed' } } },
      message: 'Request failed',
    };
    mockApi.post.mockRejectedValue(error);

    const result = await executeAIResponse({
      action: 'create_task',
      task: {
        name: 'Test',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowJson,
      },
    } as any);

    expect(result.success).toBe(false);
    expect(result.message).toBe('Validation failed');
  });

  it('falls back to response.data.message on API error', async () => {
    const error = {
      isAxiosError: true,
      response: { data: { message: 'Server error' } },
      message: 'Request failed',
    };
    mockApi.post.mockRejectedValue(error);

    const result = await executeAIResponse({
      action: 'create_task',
      task: {
        name: 'Test',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowJson,
      },
    } as any);

    expect(result.success).toBe(false);
    expect(result.message).toBe('Server error');
  });

  it('falls back to err.message on network error', async () => {
    const error = {
      isAxiosError: true,
      response: undefined,
      message: 'Network Error',
    };
    mockApi.post.mockRejectedValue(error);

    const result = await executeAIResponse({
      action: 'create_task',
      task: {
        name: 'Test',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        workflow_definition_json: workflowJson,
      },
    } as any);

    expect(result.success).toBe(false);
    expect(result.message).toBe('Network Error');
  });
});
