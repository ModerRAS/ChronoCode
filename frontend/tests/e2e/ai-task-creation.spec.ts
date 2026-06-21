import { test, expect } from '@playwright/test';

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
        prompt: 'Run the daily check',
        max_runtime_seconds: 600,
        max_file_changes: 50,
        require_plan_review: true,
        is_enabled: true,
      },
      error: null,
    };

    let executedBody: unknown;

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
