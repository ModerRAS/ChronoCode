import { test, expect } from '@playwright/test';

test.describe('AI Task Creation', () => {
  test('AI creates task successfully', async ({ page }) => {
    await page.goto('/chat');
    await page.fill('.ant-input-search-input', 'Create a task called DailyTest that runs every day at 9am');
    await page.click('.ant-input-search-button:has-text("Send")');
    await page.waitForSelector('.message.ai .message-body', { timeout: 30000 });
    const responseText = await page.textContent('.message.ai .message-body');
    expect(responseText).toContain('create_task');
    const confirmButton = page.locator('.action-buttons .ant-btn-primary');
    if (await confirmButton.isVisible()) {
      await confirmButton.click();
    }
    await page.goto('/');
    await expect(page.locator('.ant-table')).toContainText('DailyTest');
  });
});
