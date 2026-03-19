import { test, expect } from '@playwright/test';

test.describe('Network Retry', () => {
  test('shows retry button on network failure', async ({ page }) => {
    let shouldFail = true;
    await page.route('**/api/tasks', route => {
      if (shouldFail) {
        route.abort('failed');
      } else {
        route.continue();
      }
    });

    await page.goto('/tasks/new');
    await page.fill('input[placeholder="Enter task name"]', 'RetryTest');
    await page.fill('input[placeholder="e.g., 0 * * * *"]', '0 9 * * *');
    await page.fill('input[placeholder="https://github.com/owner/repo"]', 'https://github.com/test/repo');
    await page.fill('textarea[placeholder="What should the AI do?"]', 'Test');
    await page.click('button[type="submit"]');
    await expect(page.locator('.retry-button')).toBeVisible({ timeout: 5000 });
    shouldFail = false;
    await page.click('.retry-button');
    await expect(page.locator('.success-message')).toBeVisible({ timeout: 10000 });
  });
});
