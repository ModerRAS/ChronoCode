import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import SetupView from '../../src/views/SetupView.vue';

// Mock ant-design-vue message
vi.mock('ant-design-vue', () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    warning: vi.fn(),
  },
}));

// Mock setupApi
const mockGetStatus = vi.fn();
const mockInitialize = vi.fn();
vi.mock('../../src/api/setup', () => ({
  setupApi: {
    getStatus: (...a: unknown[]) => mockGetStatus(...a),
    initialize: (...a: unknown[]) => mockInitialize(...a),
  },
}));

const stubs = {
  'a-card': { template: '<div class="a-card">{{ title }}<slot /></div>', props: ['title'] },
  'a-form': { template: '<form class="a-form" @submit.prevent="$emit(\'finish\')"><slot /></form>', emits: ['finish'] },
  'a-form-item': { template: '<div class="a-form-item">{{ label }}<slot /></div>', props: ['label'] },
  'a-input': { template: '<input class="a-input" />', props: ['value'] },
  'a-input-password': { template: '<input class="a-input-password" type="password" />', props: ['value'] },
  'a-input-number': { template: '<input type="number" class="a-input-number" />', props: ['value'] },
  'a-radio-group': { template: '<div class="a-radio-group"><slot /></div>', props: ['value'] },
  'a-radio-button': { template: '<label class="a-radio-button"><slot /></label>' },
  'a-button': { template: '<button class="a-btn" @click="$emit(\'click\')"><slot /></button>', emits: ['click'] },
  'a-alert': { template: '<div class="a-alert">{{ message }}<slot /></div>', props: ['message'] },
  'a-space': { template: '<span class="a-space"><slot /></span>' },
  'a-spin': { template: '<div class="a-spin"><slot /></div>' },
  'a-row': { template: '<div class="a-row"><slot /></div>' },
  'a-col': { template: '<div class="a-col"><slot /></div>' },
  'a-descriptions': { template: '<div class="a-descriptions"><slot /></div>' },
  'a-descriptions-item': { template: '<div class="a-desc-item">{{ label }}<slot /></div>', props: ['label'] },
};

function mountSetup() {
  return mount(SetupView, { global: { stubs } });
}

describe('SetupView.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetStatus.mockResolvedValue({ initialized: false });
  });

  it('loads status on mount', async () => {
    mountSetup();
    await flushPromises();

    expect(mockGetStatus).toHaveBeenCalledTimes(1);
  });

  it('renders setup form when not initialized', async () => {
    const wrapper = mountSetup();
    await flushPromises();

    expect(wrapper.find('.a-form').exists()).toBe(true);
  });

  it('renders database provider radio group', async () => {
    const wrapper = mountSetup();
    await flushPromises();

    expect(wrapper.find('.a-radio-group').exists()).toBe(true);
  });

  it('renders submit button with Initialize text', async () => {
    const wrapper = mountSetup();
    await flushPromises();

    const buttons = wrapper.findAll('.a-btn');
    expect(buttons.some(b => b.text().includes('Initialize'))).toBe(true);
  });

  it('renders SQLite path field by default', async () => {
    const wrapper = mountSetup();
    await flushPromises();

    // Default provider is SQLite, so SQLite path field should be visible
    expect(wrapper.text()).toContain('SQLite');
  });

  it('renders PostgreSQL option', async () => {
    const wrapper = mountSetup();
    await flushPromises();

    expect(wrapper.text()).toContain('PostgreSQL');
  });

  it('shows initialized message when already initialized', async () => {
    mockGetStatus.mockResolvedValue({ initialized: true });
    const wrapper = mountSetup();
    await flushPromises();

    expect(wrapper.text().toLowerCase()).toContain('initialized');
  });

  it('submit calls setupApi.initialize', async () => {
    mockInitialize.mockResolvedValue({ initialized: true });
    const wrapper = mountSetup();
    await flushPromises();

    // Trigger form finish event (ant-design-vue uses @finish)
    await wrapper.find('.a-form').trigger('submit');
    await flushPromises();

    expect(mockInitialize).toHaveBeenCalledTimes(1);
  });

  it('shows error on initialize failure', async () => {
    mockInitialize.mockRejectedValue({
      response: { data: { error: { message: 'DB connection failed' } } },
    });
    const wrapper = mountSetup();
    await flushPromises();

    await wrapper.find('.a-form').trigger('submit');
    await flushPromises();

    expect(wrapper.text()).toContain('DB connection failed');
  });

  it('emits initialized event on success', async () => {
    mockInitialize.mockResolvedValue({ initialized: true });
    const wrapper = mountSetup();
    await flushPromises();

    await wrapper.find('.a-form').trigger('submit');
    await flushPromises();

    expect(wrapper.emitted('initialized')).toBeTruthy();
  });
});
