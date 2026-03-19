import { test, expect } from '@playwright/test';

test.describe('Error Handling', () => {
  test('shows error when AI response is invalid JSON', async ({ page }) => {
    await page.route('**/message', route => {
      route.fulfill({
        status: 200,
        contentType: 'text/plain',
        body: 'This is not JSON'
      });
    });

    await page.goto('/chat');
    await page.fill('.ant-input-search-input', 'Create a task');
    await page.click('.ant-input-search-button:has-text("Send")');
    await expect(page.locator('.error-alert')).toBeVisible();
  });
});
