import { describe, it, expect, vi, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { defineComponent, h } from 'vue';
import { useIsMobile } from '../../src/composables/useIsMobile';

// Helper: create a test component that uses the composable
function createTestComponent(query?: string) {
  return defineComponent({
    setup() {
      const { isMobile } = useIsMobile(query);
      return () => h('div', { 'data-is-mobile': isMobile.value });
    },
  });
}

describe('useIsMobile', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('returns false on desktop-width screen', () => {
    // Default matchMedia mock: does not match
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }));

    const wrapper = mount(createTestComponent());
    expect(wrapper.attributes('data-is-mobile')).toBe('false');
  });

  it('returns true on mobile-width screen', () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: true,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }));

    const wrapper = mount(createTestComponent());
    expect(wrapper.attributes('data-is-mobile')).toBe('true');
  });

  it('accepts custom media query', () => {
    let capturedQuery = '';
    vi.stubGlobal('matchMedia', (q: string) => {
      capturedQuery = q;
      return { matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() };
    });

    mount(createTestComponent('(max-width: 768px)'));

    expect(capturedQuery).toBe('(max-width: 768px)');
  });

  it('defaults to (max-width: 1024px) when no query provided', () => {
    let capturedQuery = '';
    vi.stubGlobal('matchMedia', (q: string) => {
      capturedQuery = q;
      return { matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() };
    });

    mount(createTestComponent());

    expect(capturedQuery).toBe('(max-width: 1024px)');
  });

  it('handles matchMedia not available (SSR)', () => {
    vi.stubGlobal('matchMedia', undefined);

    const wrapper = mount(createTestComponent());
    // Should default to false when matchMedia is not available
    expect(wrapper.attributes('data-is-mobile')).toBe('false');
  });

  it('removes event listener on unmount', () => {
    const removeEventListener = vi.fn();
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addEventListener: vi.fn(),
      removeEventListener,
    }));

    const wrapper = mount(createTestComponent());
    wrapper.unmount();

    expect(removeEventListener).toHaveBeenCalled();
  });

  it('uses fallback addListener/removeListener when addEventListener not available', () => {
    const addListener = vi.fn();
    const removeListener = vi.fn();
    vi.stubGlobal('matchMedia', () => ({
      matches: false,
      addListener,
      removeListener,
      // No addEventListener/removeEventListener
    }));

    const wrapper = mount(createTestComponent());
    expect(addListener).toHaveBeenCalled();

    wrapper.unmount();
    expect(removeListener).toHaveBeenCalled();
  });
});
