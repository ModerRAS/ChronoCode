import { test, expect } from '@playwright/test';

test.describe('Error Handling', () => {
  test('renders invalid AI response as plain text', async ({ page }) => {
    await page.route('**/api/ai/message', route => {
      route.fulfill({
        status: 200,
        contentType: 'text/plain',
        body: 'This is not JSON'
      });
    });

    await page.goto('/chat');
    await page.getByPlaceholder('Ask the AI to create or manage tasks...').fill('Create a task');
    await page.getByRole('button', { name: 'Send' }).click();

    await expect(page.locator('.message.ai .message-body pre').last()).toContainText('This is not JSON');
    await expect(page.locator('.action-buttons')).toHaveCount(0);
  });
});
