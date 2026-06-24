import { describe, expect, it, vi, beforeEach } from 'vitest';

// Mock axios before importing the module under test
vi.mock('axios', () => {
  const api = {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  };
  return {
    default: { create: vi.fn(() => api) },
  };
});

import { taskApi } from '../../src/api/tasks';

// Get the mocked api instance (the one axios.create returns)
const axios = (await import('axios')).default;
const mockApi = (axios.create as ReturnType<typeof vi.fn>).mock.results[0].value;

describe('taskApi node-scoped wrappers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const EXEC_ID = 'exec-123';
  const NODE_ID = 'node-456';

  it('getNodes calls GET /tasks/executions/{id}/nodes', async () => {
    const fakeNodes = [
      { id: 'n1', executionId: EXEC_ID, nodeId: 'agent', nodeType: 'agent', status: 'completed' },
    ];
    mockApi.get.mockResolvedValue({ data: fakeNodes });

    const result = await taskApi.getNodes(EXEC_ID);

    expect(result).toEqual(fakeNodes);
    expect(mockApi.get).toHaveBeenCalledWith(`/tasks/executions/${EXEC_ID}/nodes`);
  });

  it('getNodeSession calls GET /tasks/executions/{id}/nodes/{nodeId}/session', async () => {
    const fakeSession = {
      executionId: EXEC_ID,
      nodeExecutionId: NODE_ID,
      backend: 'pi',
      sessionId: 'sess-1',
      isLive: true,
      supportsPersistentSessions: true,
      supportsSupplementalMessages: true,
      canResume: true,
    };
    mockApi.get.mockResolvedValue({ data: fakeSession });

    const result = await taskApi.getNodeSession(EXEC_ID, NODE_ID);

    expect(result).toEqual(fakeSession);
    expect(mockApi.get).toHaveBeenCalledWith(
      `/tasks/executions/${EXEC_ID}/nodes/${NODE_ID}/session`,
    );
  });

  it('resumeNodeSession calls POST /tasks/executions/{id}/nodes/{nodeId}/resume', async () => {
    const fakeSession = { isLive: true, canResume: true };
    mockApi.post.mockResolvedValue({ data: fakeSession });

    const result = await taskApi.resumeNodeSession(EXEC_ID, NODE_ID);

    expect(result).toEqual(fakeSession);
    expect(mockApi.post).toHaveBeenCalledWith(
      `/tasks/executions/${EXEC_ID}/nodes/${NODE_ID}/resume`,
      undefined,
    );
  });

  it('sendNodeMessage calls POST with body', async () => {
    const fakeResponse = { result: 'queued' };
    mockApi.post.mockResolvedValue({ data: fakeResponse });

    const result = await taskApi.sendNodeMessage(EXEC_ID, NODE_ID, {
      message: 'steer',
      mode: 'steer',
    });

    expect(result).toEqual(fakeResponse);
    expect(mockApi.post).toHaveBeenCalledWith(
      `/tasks/executions/${EXEC_ID}/nodes/${NODE_ID}/message`,
      { message: 'steer', mode: 'steer' },
    );
  });

  it('approveNode calls POST /tasks/executions/{id}/approval/{nodeId}', async () => {
    mockApi.post.mockResolvedValue({});

    await taskApi.approveNode(EXEC_ID, NODE_ID, { approved: true });

    expect(mockApi.post).toHaveBeenCalledWith(
      `/tasks/executions/${EXEC_ID}/approval/${NODE_ID}`,
      { approved: true },
    );
  });

  it('approveNode sends approved=false for rejection', async () => {
    mockApi.post.mockResolvedValue({});

    await taskApi.approveNode(EXEC_ID, NODE_ID, { approved: false, reason: 'rejected' });

    expect(mockApi.post).toHaveBeenCalledWith(
      `/tasks/executions/${EXEC_ID}/approval/${NODE_ID}`,
      { approved: false, reason: 'rejected' },
    );
  });

  it('getExecutions calls GET /tasks/{id}/executions', async () => {
    const fakeExecs = [{ id: 'e1', taskId: 't1', status: 2 }];
    mockApi.get.mockResolvedValue({ data: fakeExecs });

    const result = await taskApi.getExecutions('t1');

    expect(result).toEqual(fakeExecs);
    expect(mockApi.get).toHaveBeenCalledWith('/tasks/t1/executions');
  });

  it('create calls POST /tasks with workflow fields', async () => {
    const fakeTask = { id: 't1', name: 'New Task' };
    mockApi.post.mockResolvedValue({ data: fakeTask });

    const dto = {
      name: 'New Task',
      cronExpression: '0 0 * * *',
      repositoryUrl: 'https://github.com/test/repo',
      baseBranch: 'main',
      branchStrategy: 0,
      maxRuntimeSeconds: 600,
      maxFileChanges: 50,
      isEnabled: true,
      workflowDefinitionJson: '{}',
      maxConcurrentRuns: 1,
      nodeFailurePolicyJson: '{}',
    };

    const result = await taskApi.create(dto);

    expect(result).toEqual(fakeTask);
    expect(mockApi.post).toHaveBeenCalledWith('/tasks', dto);
  });
});
