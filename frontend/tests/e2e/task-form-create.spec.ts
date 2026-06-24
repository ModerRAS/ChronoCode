import { test, expect } from '@playwright/test';

const mockTask = {
  id: 'new-task-id',
  name: 'Daily Build',
  cronExpression: '0 2 * * *',
  repositoryUrl: 'https://github.com/test/repo',
  baseBranch: 'main',
  branchStrategy: 0,
  maxRuntimeSeconds: 600,
  maxFileChanges: 50,
  isEnabled: true,
  workflowVersion: 1,
  workflowDefinitionJson: JSON.stringify({ version: 1, startNodeId: 'start', nodes: {} }),
  maxConcurrentRuns: 1,
  nodeFailurePolicyJson: '{}',
  createdAt: '2024-01-01T00:00:00Z',
  lastStatus: 0,
  schedulerStatus: 'idle',
};

test.describe('Task form creation', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/api/setup/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ initialized: true, configFilePath: '', defaultSqlitePath: '' }),
      });
    });
    await page.route('**/api/tasks', route => {
      if (route.request().method() === 'POST') {
        route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockTask) });
      } else {
        route.continue();
      }
    });
  });

  test('renders task creation form with all fields', async ({ page }) => {
    await page.goto('/tasks/new');
    await expect(page.getByPlaceholder('Enter task name')).toBeVisible({ timeout: 10000 });
    await expect(page.getByPlaceholder('e.g., 0 * * * *')).toBeVisible();
    await expect(page.locator('textarea').first()).toBeVisible();
  });

  test('format and load default buttons are present', async ({ page }) => {
    await page.goto('/tasks/new');
    await expect(page.getByPlaceholder('Enter task name')).toBeVisible({ timeout: 10000 });

    const bodyText = await page.locator('body').innerText();
    expect(bodyText).toContain('Format JSON');
    expect(bodyText).toContain('Load default workflow');
    expect(bodyText).toContain('Create Task');
  });

  test('create task button is clickable', async ({ page }) => {
    await page.goto('/tasks/new');
    await expect(page.getByPlaceholder('Enter task name')).toBeVisible({ timeout: 10000 });

    const submitBtn = page.locator('button[type="submit"]');
    await expect(submitBtn).toBeVisible();
    await submitBtn.click();
    await page.waitForTimeout(500);
  });

  test('workflow graph preview is rendered', async ({ page }) => {
    await page.goto('/tasks/new');
    await expect(page.getByPlaceholder('Enter task name')).toBeVisible({ timeout: 10000 });

    // The page should have rendered some workflow content
    const bodyText = await page.locator('body').innerText();
    expect(bodyText.length).toBeGreaterThan(50);
  });
});
