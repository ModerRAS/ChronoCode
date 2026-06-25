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
      nextNodeId: 'plan',
    },
    {
      type: 'agent',
      nodeId: 'plan',
      name: 'Plan',
      backend: 'pi',
      promptTemplate: 'Analyze the repository and produce a plan.',
      dataContract: { fields: [{ name: 'summary', type: 'string', required: true }] },
      nextNodeId: 'commit',
    },
    {
      type: 'commit_changes',
      nodeId: 'commit',
      name: 'Commit Changes',
      commitMessageTemplate: 'AI: {{$.task.name}}',
      nextNodeId: 'pr',
    },
    {
      type: 'create_pull_request',
      nodeId: 'pr',
      name: 'Create Pull Request',
      titleTemplate: '{{$.task.name}}',
      bodyTemplate: '{{$.nodes.plan.output.summary}}',
      nextNodeId: 'end',
    },
    { type: 'end', nodeId: 'end', name: 'End' },
  ],
});

test.describe('AI Task Creation', () => {
  test('executes confirmed AI task creation through backend endpoint', async ({ page }) => {
    const aiResponse = {
      action: 'create_task',
      task_id: null,
      task: {
        name: 'DailyTest',
        cron: '0 9 * * *',
        repository: 'https://github.com/test/repo',
        base_branch: 'main',
        branch_strategy: 'new',
        max_runtime_seconds: 600,
        max_file_changes: 50,
        is_enabled: true,
        workflow_definition_json: WORKFLOW_DEFINITION_JSON,
        default_inputs_json: null,
        runtime_backend: 'pi',
        max_concurrent_runs: 1,
        node_failure_policy_json: '{}',
      },
      error: null,
    };

    let executedBody: unknown;

    await page.route('**/api/setup/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ initialized: true, configFilePath: '', defaultSqlitePath: '' }),
      });
    });

    await page.route('**/api/ai/message', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(aiResponse),
      });
    });

    await page.route('**/api/ai/ai', route => {
      executedBody = route.request().postDataJSON();
      route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ id: '11111111-1111-1111-1111-111111111111', name: 'DailyTest' }),
      });
    });

    await page.goto('/chat');
    await page.getByPlaceholder('Ask the AI to create or manage tasks...').fill(
      'Create a task called DailyTest that runs every day at 9am',
    );
    await page.getByRole('button', { name: 'Send' }).click();

    await expect(page.locator('.message.ai .message-body pre').last()).toContainText('create_task');
    await expect(page.getByText('Would you like me to execute this action?')).toBeVisible();

    await page.getByRole('button', { name: 'Confirm' }).click();

    expect(executedBody).toEqual(aiResponse);
    await expect(page.getByText('Task created successfully')).toBeVisible();
  });
});
