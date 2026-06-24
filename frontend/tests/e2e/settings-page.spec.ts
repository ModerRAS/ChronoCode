import { expect, test } from '@playwright/test'

test.describe('Settings page', () => {
  test('loads runtime settings and omits blank password on save', async ({ page }) => {
    const settingsResponse = {
      agentRuntime: { backend: 'opencode' },
      opencode: {
        host: '127.0.0.1',
        port: 4096,
        username: 'operator',
        hasPassword: true,
      },
      pi: {
        provider: 'openrouter',
        model: 'claude-3.7-sonnet',
        thinking: 'medium',
      },
    }

    let lastPutBody: any = null
    const putRequestPromise = page.waitForRequest(request =>
      request.url().includes('/api/settings') && request.method() === 'PUT',
    )

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

    await page.route('**/api/tasks/server/status', route => {
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          backend: lastPutBody?.agentRuntime?.backend ?? 'pi',
          running: false,
          url: null,
          supportsPersistentSessions: true,
          supportsSupplementalMessages: true,
        }),
      })
    })

    await page.route('**/api/settings', route => {
      if (route.request().method() === 'GET') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(settingsResponse),
        })
      }

      lastPutBody = route.request().postDataJSON()
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ...settingsResponse,
          agentRuntime: {
            backend: lastPutBody.agentRuntime.backend,
          },
          opencode: {
            ...settingsResponse.opencode,
            host: lastPutBody.opencode.host,
            port: lastPutBody.opencode.port,
            username: lastPutBody.opencode.username,
          },
          pi: {
            ...settingsResponse.pi,
            provider: lastPutBody.pi.provider,
            model: lastPutBody.pi.model,
            thinking: lastPutBody.pi.thinking,
          },
        }),
      })
    })

    await page.goto('/settings')

    await expect(page.getByRole('heading', { name: 'Settings' })).toBeVisible()
    await expect(page.getByText('Current password is configured. Leave this field blank to keep it unchanged.')).toBeVisible()
    await expect(page.locator('.ant-form-item:has-text("Host") input')).toHaveValue('127.0.0.1')
    await expect(page.locator('.ant-form-item:has-text("Port") input')).toHaveValue('4096')
    await expect(page.locator('.ant-form-item:has-text("Username") input')).toHaveValue('operator')
    await expect(page.locator('input[type="password"]')).toHaveValue('')

    await page.locator('.ant-form-item:has-text("Host") input').fill('192.168.50.22')
    await page.getByRole('button', { name: 'Save Settings' }).click()
    const putRequest = await putRequestPromise
    lastPutBody = putRequest.postDataJSON()

    expect(lastPutBody).not.toBeNull()
    expect(lastPutBody.opencode.password).toBeUndefined()
    await expect(page.getByText('Settings saved. Runtime changes apply on the next start.')).toBeVisible()
  })
})
