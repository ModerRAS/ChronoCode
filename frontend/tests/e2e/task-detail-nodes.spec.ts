import { test, expect } from '@playwright/test';

const WORKFLOW_DEFINITION_JSON = JSON.stringify({
  version: 1,
  startNodeId: 'start',
  nodes: [
    { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'prepare' },
    {
      type: 'prepare_workspace',
      nodeId: 'prepare',
      name: 'Prepare Workspace',
      nextNodeId: 'agent',
    },
    {
      type: 'agent',
      nodeId: 'agent',
      name: 'Agent',
      backend: 'pi',
      promptTemplate: 'Do work',
      dataContract: { fields: [{ name: 'summary', type: 'string', required: true }] },
      nextNodeId: 'end',
    },
    { type: 'end', nodeId: 'end', name: 'End' },
  ],
});

test.describe('Task Detail with workflow nodes', () => {
  test('displays workflow graph and node execution list', async ({ page }) => {
    const TASK_ID = '22222222-2222-2222-2222-222222222222';
    const EXEC_ID = '33333333-3333-3333-3333-333333333333';
    const NODE_EXEC_ID = '44444444-4444-4444-4444-444444444444';

    // Setup status mock
    await page.route('**/api/setup/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ initialized: true, configFilePath: '', defaultSqlitePath: '' }),
      });
    });

    // Mock task detail
    await page.route(`**/api/tasks/${TASK_ID}`, route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TASK_ID,
          name: 'Workflow Test Task',
          cronExpression: '0 0 * * *',
          repositoryUrl: 'https://github.com/test/repo',
          baseBranch: 'main',
          branchStrategy: 0,
          maxRuntimeSeconds: 600,
          maxFileChanges: 50,
          isEnabled: true,
          workflowVersion: 1,
          workflowDefinitionJson: WORKFLOW_DEFINITION_JSON,
          runtimeBackend: 'pi',
          maxConcurrentRuns: 1,
          nodeFailurePolicyJson: '{}',
          createdAt: '2024-01-01T00:00:00Z',
          lastStatus: 2,
          schedulerStatus: 'idle',
        }),
      });
    });

    // Mock executions list
    await page.route(`**/api/tasks/${TASK_ID}/executions`, route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: EXEC_ID,
            taskId: TASK_ID,
            startedAt: '2024-01-01T10:00:00Z',
            completedAt: '2024-01-01T10:05:00Z',
            status: 2,
            workflowVersion: 1,
            triggerSource: 'manual',
            filesChanged: 3,
            branchName: 'chronocode/exec-1',
          },
        ]),
      });
    });

    // Mock node executions
    await page.route('**/api/tasks/executions/*/nodes', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: NODE_EXEC_ID,
            executionId: EXEC_ID,
            nodeId: 'start',
            nodeType: 'start',
            scopeKey: 'root',
            attempt: 0,
            status: 'completed',
            startedAt: '2024-01-01T10:00:00Z',
            completedAt: '2024-01-01T10:00:01Z',
            retryCount: 0,
          },
          {
            id: 'node-exec-2',
            executionId: EXEC_ID,
            nodeId: 'prepare',
            nodeType: 'prepare_workspace',
            scopeKey: 'root',
            attempt: 0,
            status: 'completed',
            startedAt: '2024-01-01T10:00:01Z',
            completedAt: '2024-01-01T10:00:10Z',
            retryCount: 0,
          },
          {
            id: 'node-exec-3',
            executionId: EXEC_ID,
            nodeId: 'agent',
            nodeType: 'agent',
            scopeKey: 'root',
            attempt: 0,
            status: 'completed',
            startedAt: '2024-01-01T10:00:10Z',
            completedAt: '2024-01-01T10:04:00Z',
            agentBackend: 'pi',
            agentSessionId: 'sess-123',
            retryCount: 0,
          },
          {
            id: 'node-exec-4',
            executionId: EXEC_ID,
            nodeId: 'end',
            nodeType: 'end',
            scopeKey: 'root',
            attempt: 0,
            status: 'completed',
            startedAt: '2024-01-01T10:04:00Z',
            completedAt: '2024-01-01T10:05:00Z',
            retryCount: 0,
          },
        ]),
      });
    });

    await page.goto(`/tasks/${TASK_ID}`);

    // Task details should be visible
    await expect(page.getByText('Workflow Test Task')).toBeVisible();
    await expect(page.getByText('Workflow Version')).toBeVisible();

    // Workflow graph section should be visible
    await expect(page.getByText('Workflow').first()).toBeVisible();

    // Execution history should show the execution
    await expect(page.getByText('Execution History')).toBeVisible();

    // Click "View Nodes" to load node executions
    await page.getByRole('button', { name: 'View Nodes' }).click();

    // Node table should show node IDs
    await expect(page.getByText('start').first()).toBeVisible();
    await expect(page.getByText('prepare_workspace').first()).toBeVisible();
    await expect(page.getByText('agent').first()).toBeVisible();
    await expect(page.getByText('end').first()).toBeVisible();
  });

  test('shows approval buttons for waiting_approval nodes', async ({ page }) => {
    const TASK_ID = '55555555-5555-5555-5555-555555555555';
    const EXEC_ID = '66666666-6666-6666-6666-666666666666';
    const GATE_NODE_ID = '77777777-7777-7777-7777-777777777777';

    await page.route('**/api/setup/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ initialized: true, configFilePath: '', defaultSqlitePath: '' }),
      });
    });

    await page.route(`**/api/tasks/${TASK_ID}`, route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: TASK_ID,
          name: 'Approval Task',
          cronExpression: '0 0 * * *',
          repositoryUrl: 'https://github.com/test/repo',
          baseBranch: 'main',
          branchStrategy: 0,
          maxRuntimeSeconds: 600,
          maxFileChanges: 50,
          isEnabled: true,
          workflowVersion: 1,
          workflowDefinitionJson: JSON.stringify({
            version: 1,
            startNodeId: 'start',
            nodes: [
              { type: 'start', nodeId: 'start', name: 'Start', nextNodeId: 'gate' },
              { type: 'approval_gate', nodeId: 'gate', name: 'Gate', nextNodeId: 'end' },
              { type: 'end', nodeId: 'end', name: 'End' },
            ],
          }),
          maxConcurrentRuns: 1,
          nodeFailurePolicyJson: '{}',
          createdAt: '2024-01-01T00:00:00Z',
          lastStatus: 1,
          schedulerStatus: 'running',
        }),
      });
    });

    await page.route(`**/api/tasks/${TASK_ID}/executions`, route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: EXEC_ID,
            taskId: TASK_ID,
            startedAt: '2024-01-01T10:00:00Z',
            status: 1,
            workflowVersion: 1,
            triggerSource: 'manual',
            filesChanged: 0,
          },
        ]),
      });
    });

    await page.route('**/api/tasks/executions/*/nodes', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: GATE_NODE_ID,
            executionId: EXEC_ID,
            nodeId: 'gate',
            nodeType: 'approval_gate',
            scopeKey: 'root',
            attempt: 0,
            status: 'waiting_approval',
            startedAt: '2024-01-01T10:00:05Z',
            retryCount: 0,
          },
        ]),
      });
    });

    await page.goto(`/tasks/${TASK_ID}`);
    await page.getByRole('button', { name: 'View Nodes' }).click();

    // Approval buttons should be visible
    await expect(page.getByRole('button', { name: 'Approve' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Reject' })).toBeVisible();
  });
});
