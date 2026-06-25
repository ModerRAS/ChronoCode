import { expect, test } from '@playwright/test'

test.describe('Task list mobile layout', () => {
  test('shows cards instead of desktop table on narrow screens', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })

    await page.route('**/api/setup/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          initialized: true,
          databaseProvider: 'sqlite',
          configFilePath: 'appsettings.Local.json',
          defaultSqlitePath: 'storage/chronocode.db',
        }),
      })
    })

    await page.route('**/api/tasks', route => {
      if (route.request().method() !== 'GET') {
        return route.fallback()
      }

      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: '11111111-1111-1111-1111-111111111111',
            name: 'Mobile Task',
            cronExpression: '0 0 * * *',
            repositoryUrl: 'https://github.com/example/mobile-task',
            baseBranch: 'main',
            branchStrategy: 0,
            workflowVersion: 1,
            workflowDefinitionJson: '{"version":1,"startNodeId":"start","nodes":[]}',
            maxRuntimeSeconds: 600,
            maxFileChanges: 5,
            runtimeBackend: 'pi',
            maxConcurrentRuns: 1,
            nodeFailurePolicyJson: '{}',
            createdAt: '2026-06-21T00:00:00Z',
            lastRunAt: '2026-06-21T01:00:00Z',
            lastStatus: 2,
            isEnabled: true,
            lastError: null,
          },
        ]),
      })
    })

    await page.goto('/')

    await expect(page.getByTestId('task-mobile-list')).toBeVisible()
    await expect(page.getByTestId('task-desktop-table')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Edit' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Run' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Delete' })).toBeVisible()
  })
})
