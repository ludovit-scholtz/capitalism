// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { createApp, defineComponent, h, nextTick, ref } from 'vue'
import GenderPicker from '../GenderPicker.vue'

function mountPicker() {
  const selected = ref<'UNSPECIFIED' | 'FEMALE' | 'MALE'>('UNSPECIFIED')
  const emitted: Array<'FEMALE' | 'MALE'> = []
  const container = document.createElement('div')

  const App = defineComponent({
    setup() {
      return () =>
        h(GenderPicker, {
          modelValue: selected.value,
          femaleLabel: 'Select female',
          maleLabel: 'Select male',
          'onUpdate:modelValue': (value: 'FEMALE' | 'MALE') => {
            emitted.push(value)
            selected.value = value
          },
        })
    },
  })

  createApp(App).mount(container)
  return { container, selected, emitted }
}

describe('GenderPicker', () => {
  it('renders female and male icon options', () => {
    const { container } = mountPicker()
    expect(container.querySelector('button[aria-label="Select female"]')?.textContent).toContain('♀')
    expect(container.querySelector('button[aria-label="Select male"]')?.textContent).toContain('♂')
  })

  it('emits FEMALE when female option is clicked', async () => {
    const { container, emitted } = mountPicker()
    const button = container.querySelector('button[aria-label="Select female"]') as HTMLButtonElement
    button.click()
    await nextTick()
    expect(emitted[0]).toBe('FEMALE')
  })

  it('emits MALE when male option is clicked', async () => {
    const { container, emitted } = mountPicker()
    const button = container.querySelector('button[aria-label="Select male"]') as HTMLButtonElement
    button.click()
    await nextTick()
    expect(emitted[0]).toBe('MALE')
  })
})
