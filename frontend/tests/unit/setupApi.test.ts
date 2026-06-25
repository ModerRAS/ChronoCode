import { describe, it, expect, vi, beforeEach } from 'vitest';
import { setupApi } from '../../src/api/setup';

vi.mock('../../src/api/tasks', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

// Mock the default axios instance
vi.mock('axios', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

// Import the actual axios instance used by setup.ts
import api from '../../src/api/tasks';

describe('setupApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getStatus calls GET /setup/status', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { initialized: true, configFilePath: '/app/appsettings.Local.json' },
    });

    const result = await setupApi.getStatus();

    expect(api.get).toHaveBeenCalledWith('/setup/status');
    expect(result.initialized).toBe(true);
  });

  it('getStatus returns data from response', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { initialized: false, configFilePath: '', defaultSqlitePath: 'storage/chronocode.db' },
    });

    const result = await setupApi.getStatus();

    expect(result.initialized).toBe(false);
    expect(result.defaultSqlitePath).toBe('storage/chronocode.db');
  });

  it('initialize calls POST /setup/initialize with data', async () => {
    (api.post as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { initialized: true, configFilePath: '/app/appsettings.Local.json' },
    });

    const payload = {
      databaseProvider: 'sqlite' as const,
      sqlitePath: 'storage/chronocode.db',
      postgresHost: 'localhost',
      postgresPort: 5432,
      postgresDatabase: 'chronocode',
      postgresUsername: 'postgres',
      postgresPassword: '',
      connectionString: '',
    };

    const result = await setupApi.initialize(payload);

    expect(api.post).toHaveBeenCalledWith('/setup/initialize', payload);
    expect(result.initialized).toBe(true);
  });

  it('initialize with postgresql provider', async () => {
    (api.post as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { initialized: true, configFilePath: '/app/appsettings.Local.json' },
    });

    const payload = {
      databaseProvider: 'postgresql' as const,
      sqlitePath: '',
      postgresHost: 'db.example.com',
      postgresPort: 5432,
      postgresDatabase: 'mydb',
      postgresUsername: 'admin',
      postgresPassword: 'secret',
      connectionString: '',
    };

    const result = await setupApi.initialize(payload);

    expect(api.post).toHaveBeenCalledWith('/setup/initialize', payload);
    expect(result.initialized).toBe(true);
  });

  it('initialize with connection string', async () => {
    (api.post as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { initialized: true, configFilePath: '/app/appsettings.Local.json' },
    });

    const payload = {
      databaseProvider: 'postgresql' as const,
      sqlitePath: '',
      postgresHost: '',
      postgresPort: 5432,
      postgresDatabase: '',
      postgresUsername: '',
      postgresPassword: '',
      connectionString: 'Host=db;Database=app;Username=u;Password=p',
    };

    await setupApi.initialize(payload);

    expect(api.post).toHaveBeenCalledWith('/setup/initialize', payload);
  });

  it('getStatus propagates error on failure', async () => {
    (api.get as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Network error'));

    await expect(setupApi.getStatus()).rejects.toThrow('Network error');
  });

  it('initialize propagates error on failure', async () => {
    (api.post as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('Setup failed'));

    await expect(
      setupApi.initialize({
        databaseProvider: 'sqlite' as const,
        sqlitePath: '',
        postgresHost: '',
        postgresPort: 5432,
        postgresDatabase: '',
        postgresUsername: '',
        postgresPassword: '',
        connectionString: '',
      }),
    ).rejects.toThrow('Setup failed');
  });
});
