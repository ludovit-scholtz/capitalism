import { describe, expect, it } from 'vitest'
import { generatePersonalAccountName } from '../personalAccountName'

describe('personalAccountName', () => {
  it('returns non-empty name for MALE', () => {
    expect(generatePersonalAccountName('MALE').length).toBeGreaterThan(0)
  })

  it('returns non-empty name for FEMALE', () => {
    expect(generatePersonalAccountName('FEMALE').length).toBeGreaterThan(0)
  })

  it('two calls with same gender can produce different values', () => {
    const first = generatePersonalAccountName('FEMALE')
    const second = generatePersonalAccountName('FEMALE')
    expect(first).not.toBe(second)
  })
})
