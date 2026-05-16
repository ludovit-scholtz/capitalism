// @vitest-environment jsdom
import { createApp, defineComponent, h, nextTick } from 'vue'
import { describe, expect, it } from 'vitest'
import UnitLinkArrow from '../UnitLinkArrow.vue'

async function renderArrow(
  props: {
    state: 'none' | 'forward' | 'backward' | 'both'
    orientation: 'horizontal' | 'vertical'
    active: boolean
  },
) {
  const container = document.createElement('div')
  const App = defineComponent({
    setup() {
      return () => h(UnitLinkArrow, props)
    },
  })

  createApp(App).mount(container)
  await nextTick()

  const line = container.querySelector('.link-arrow-line') as SVGLineElement | null
  const heads = container.querySelectorAll('.link-arrow-head')
  return { line, heads }
}

describe('UnitLinkArrow', () => {
  it('renders active horizontal forward arrow with end marker', async () => {
    const { line, heads } = await renderArrow({ state: 'forward', orientation: 'horizontal', active: true })
    expect(line).not.toBeNull()
    expect(line?.getAttribute('x1')).toBe('2')
    expect(line?.getAttribute('x2')).toBe('14')
    expect(line?.classList.contains('link-arrow-line-active')).toBe(true)
    expect(heads).toHaveLength(1)
    expect(heads[0]?.getAttribute('points')).toBe('14,8 10,5 10,11')
  })

  it('renders inactive vertical backward arrow as dashed with start marker', async () => {
    const { line, heads } = await renderArrow({ state: 'backward', orientation: 'vertical', active: false })
    expect(line).not.toBeNull()
    expect(line?.getAttribute('y1')).toBe('2')
    expect(line?.getAttribute('y2')).toBe('14')
    expect(line?.classList.contains('link-arrow-line-inactive')).toBe(true)
    expect(heads).toHaveLength(1)
    expect(heads[0]?.classList.contains('inactive')).toBe(true)
  })

  it('renders dual-direction arrow for both state', async () => {
    const { heads } = await renderArrow({ state: 'both', orientation: 'horizontal', active: true })
    expect(heads).toHaveLength(2)
  })
})
