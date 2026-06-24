import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import SettingsView from '../../src/views/SettingsView.vue';

// Mock router
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock ant-design-vue message
vi.mock('ant-design-vue', () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock settingsApi
const mockSettingsGet = vi.fn();
const mockSettingsUpdate = vi.fn();
vi.mock('../../src/api/settings', () => ({
  settingsApi: {
    get: (...a: unknown[]) => mockSettingsGet(...a),
    update: (...a: unknown[]) => mockSettingsUpdate(...a),
  },
}));

// Mock setupApi
const mockGetStatus = vi.fn();
vi.mock('../../src/api/setup', () => ({
  setupApi: {
    getStatus: (...a: unknown[]) => mockGetStatus(...a),
  },
}));

// Mock taskApi
const mockGetServerStatus = vi.fn();
const mockStartServer = vi.fn();
const mockStopServer = vi.fn();
vi.mock('../../src/api/tasks', () => ({
  taskApi: {
    getServerStatus: (...a: unknown[]) => mockGetServerStatus(...a),
    startServer: (...a: unknown[]) => mockStartServer(...a),
    stopServer: (...a: unknown[]) => mockStopServer(...a),
  },
}));

const stubs = {
  'a-page-header': { template: '<div class="page-header"><slot /></div>' },
  'a-card': { template: '<div class="a-card">{{ title }}<slot /></div>', props: ['title', 'loading'] },
  'a-form': { template: '<div class="a-form"><slot /></div>' },
  'a-form-item': { template: '<div class="a-form-item">{{ label }}<slot /></div>', props: ['label'] },
  'a-input': { template: '<input class="a-input" />', props: ['value'] },
  'a-input-password': { template: '<input class="a-input-password" type="password" />', props: ['value'] },
  'a-input-number': { template: '<input type="number" class="a-input-number" />', props: ['value'] },
  'a-radio-group': { template: '<div class="a-radio-group"><slot /></div>', props: ['value'] },
  'a-radio-button': { template: '<label class="a-radio-button"><slot /></label>' },
  'a-switch': { template: '<button class="a-switch" />', props: ['checked'] },
  'a-button': { template: '<button class="a-btn" @click="$emit(\'click\')"><slot /></button>', emits: ['click'] },
  'a-space': { template: '<span class="a-space"><slot /></span>' },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-descriptions': { template: '<div class="a-descriptions"><slot /></div>' },
  'a-descriptions-item': { template: '<div class="a-desc-item">{{ label }}<slot /></div>', props: ['label'] },
  'a-tag': { template: '<span class="a-tag"><slot /></span>' },
  'a-row': { template: '<div class="a-row"><slot /></div>' },
  'a-col': { template: '<div class="a-col"><slot /></div>' },
  'a-alert': { template: '<div class="a-alert"><slot /></div>' },
};

function mountSettings() {
  return mount(SettingsView, { global: { stubs } });
}

const fakeSettings = {
  runtimeBackend: 'pi',
  piPath: '/usr/local/bin/pi',
  piApiKey: '',
  piModel: 'claude-sonnet-4-20250514',
  piMaxTurns: 50,
  opencodePath: '',
  opencodePassword: '',
  workspaceBasePath: '/workspaces',
  gitUserName: 'ChronoCode',
  gitUserEmail: 'bot@chronocode.dev',
};

const fakeStatus = {
  backend: 'pi',
  isRunning: true,
  pid: 12345,
  canStart: true,
  canStop: true,
  canRestart: true,
};

describe('SettingsView.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSettingsGet.mockResolvedValue(fakeSettings);
    mockGetServerStatus.mockResolvedValue(fakeStatus);
    mockGetStatus.mockResolvedValue({ initialized: true, databaseProvider: 'sqlite' });
  });

  it('loads settings on mount', async () => {
    mountSettings();
    await flushPromises();

    expect(mockSettingsGet).toHaveBeenCalledTimes(1);
  });

  it('loads server status on mount', async () => {
    mountSettings();
    await flushPromises();

    expect(mockGetServerStatus).toHaveBeenCalledTimes(1);
  });

  it('renders Runtime Status card', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    expect(wrapper.text()).toContain('Runtime Status');
  });

  it('renders AI Runtime Settings card', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    expect(wrapper.text()).toContain('AI Runtime Settings');
  });

  it('renders Start Runtime button', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Start Runtime'))).toBe(true);
  });

  it('renders Stop Runtime button', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Stop Runtime'))).toBe(true);
  });

  it('renders Refresh button in runtime status', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Refresh'))).toBe(true);
  });

  it('renders Save Settings button', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Save Settings'))).toBe(true);
  });

  it('refresh button reloads server status', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    const refreshBtn = wrapper.findAll('.a-btn').find(b => b.text().includes('Refresh'));
    await refreshBtn!.trigger('click');
    await flushPromises();

    expect(mockGetServerStatus).toHaveBeenCalledTimes(2);
  });

  it('renders runtime backend radio group', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    expect(wrapper.find('.a-radio-group').exists()).toBe(true);
    // Should have pi option
    expect(wrapper.text()).toContain('pi');
  });

  it('renders System Info card', async () => {
    const wrapper = mountSettings();
    await flushPromises();

    expect(wrapper.text()).toContain('System Info');
  });
});
