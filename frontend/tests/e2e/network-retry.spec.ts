import { test, expect } from '@playwright/test';

test.describe('Task creation failures', () => {
  test('keeps form data when the create request fails', async ({ page }) => {
    await page.route('**/api/setup/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ initialized: true, configFilePath: '', defaultSqlitePath: '' }),
      });
    });

    await page.route('**/api/tasks', route => {
      if (route.request().method() === 'POST') {
        route.abort('failed');
        return;
      }

      route.continue();
    });

    await page.goto('/tasks/new');
    await page.getByPlaceholder('Enter task name').fill('RetryTest');
    await page.getByPlaceholder('e.g., 0 * * * *').fill('0 9 * * *');
    await page.getByPlaceholder('https://github.com/owner/repo').fill('https://github.com/test/repo');
    await page.locator('button[type="submit"]').click();

    await expect(page).toHaveURL(/\/tasks\/new$/);
    await expect(page.getByPlaceholder('Enter task name')).toHaveValue('RetryTest');
    await expect(page.getByPlaceholder('https://github.com/owner/repo')).toHaveValue('https://github.com/test/repo');
  });
});
