import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import { ref } from 'vue';
import TaskCreate from '../../src/views/TaskCreate.vue';
import { DEFAULT_WORKFLOW_DEFINITION_JSON } from '../../src/utils/aiParser';

// Mock router
vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
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

// Mock taskApi
vi.mock('../../src/api/tasks', () => ({
  taskApi: {
    create: vi.fn().mockResolvedValue({ id: 'new-task-id' }),
  },
}));

// Mock useIsMobile
vi.mock('../../src/composables/useIsMobile', () => ({
  useIsMobile: () => ({ isMobile: ref(false) }),
}));

// Stub WorkflowGraph to avoid vue-flow complexity
vi.mock('../../src/components/WorkflowGraph.vue', () => ({
  default: {
    name: 'WorkflowGraph',
    props: ['definition', 'nodeExecutions'],
    template: '<div class="workflow-graph-stub" />',
  },
}));

const stubs = {
  'a-page-header': { template: '<div class="page-header"><slot /></div>' },
  'a-form': { template: '<div class="a-form"><slot /></div>' },
  'a-form-item': { template: '<div class="a-form-item"><slot /></div>' },
  'a-input': { template: '<input class="a-input" />', props: ['value'], emits: ['update:value'] },
  'a-input-number': { template: '<input type="number" class="a-input-number" />', props: ['value'], emits: ['update:value'] },
  'a-input-search': { template: '<input class="a-input-search" />', props: ['value'], emits: ['update:value'] },
  'a-textarea': {
    template: '<textarea class="a-textarea" @input="$emit(\'update:value\', $event.target.value)" />',
    props: ['value'],
    emits: ['update:value'],
  },
  'a-select': { template: '<select class="a-select"><slot /></select>', props: ['value'], emits: ['update:value'] },
  'a-select-option': { template: '<option><slot /></option>' },
  'a-switch': { template: '<button class="a-switch" />', props: ['checked'] },
  'a-button': { template: '<button class="a-btn"><slot /></button>' },
  'a-space': { template: '<span class="a-space"><slot /></span>' },
  'a-divider': { template: '<div class="a-divider" />' },
  'a-row': { template: '<div class="a-row"><slot /></div>' },
  'a-col': { template: '<div class="a-col"><slot /></div>' },
  'a-card': { template: '<div class="a-card"><slot /></div>' },
};

function mountCreate() {
  return mount(TaskCreate, { global: { stubs } });
}

describe('TaskCreate.vue', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders form with default workflow definition', () => {
    const wrapper = mountCreate();
    expect(wrapper.find('.a-form').exists()).toBe(true);
    expect(wrapper.find('.a-textarea').exists()).toBe(true);
  });

  it('initializes with default workflow JSON', () => {
    const wrapper = mountCreate();
    // The workflow graph preview should receive a parsed definition (non-null)
    // which means the default workflow JSON was initialized correctly
    const graph = wrapper.findComponent({ name: 'WorkflowGraph' });
    expect(graph.exists()).toBe(true);
    expect(graph.props('definition')).not.toBeNull();
  });

  it('shows workflow graph preview', () => {
    const wrapper = mountCreate();
    expect(wrapper.find('.workflow-graph-stub').exists()).toBe(true);
  });

  it('has Load default workflow button', () => {
    const wrapper = mountCreate();
    const buttons = wrapper.findAll('.a-btn');
    const loadBtn = buttons.find(b => b.text().includes('Load default workflow'));
    expect(loadBtn).toBeDefined();
  });

  it('has Format JSON button', () => {
    const wrapper = mountCreate();
    const buttons = wrapper.findAll('.a-btn');
    const formatBtn = buttons.find(b => b.text().includes('Format JSON'));
    expect(formatBtn).toBeDefined();
  });

  it('has runtime backend select with pi option', () => {
    const wrapper = mountCreate();
    const select = wrapper.find('.a-select');
    expect(select.exists()).toBe(true);
    // Should have pi option
    const options = wrapper.findAll('option');
    const piOption = options.find(o => o.text().includes('pi'));
    expect(piOption).toBeDefined();
  });

  it('has submit button', () => {
    const wrapper = mountCreate();
    const buttons = wrapper.findAll('.a-btn');
    const submitBtn = buttons.find(b => b.text().includes('Create Task') || b.text().includes('Submit'));
    expect(submitBtn).toBeDefined();
  });

  it('has name input field', () => {
    const wrapper = mountCreate();
    const inputs = wrapper.findAll('.a-input');
    // At least name field should exist
    expect(inputs.length).toBeGreaterThanOrEqual(1);
  });

  it('has cron expression field', () => {
    const wrapper = mountCreate();
    // Cron should be an input
    const inputs = wrapper.findAll('.a-input');
    expect(inputs.length).toBeGreaterThanOrEqual(1);
  });

  it('has max concurrent runs field', () => {
    const wrapper = mountCreate();
    const numberInputs = wrapper.findAll('.a-input-number');
    expect(numberInputs.length).toBeGreaterThanOrEqual(1);
  });

  it('has workflow textarea for JSON editing', () => {
    const wrapper = mountCreate();
    const textarea = wrapper.find('.a-textarea');
    expect(textarea.exists()).toBe(true);
  });

  it('renders two-section layout (schedule + workflow)', () => {
    const wrapper = mountCreate();
    // Should have form-section cards for schedule and workflow
    const sections = wrapper.findAll('.form-section');
    expect(sections.length).toBeGreaterThanOrEqual(2);
  });
});
