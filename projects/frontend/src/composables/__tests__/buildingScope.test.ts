import { describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { createBuildingScope } from '../buildingScope'

describe('createBuildingScope', () => {
  it('runs onReset synchronously when the watched id changes and bumps the token', () => {
    const id = ref('a')
    const onReset = vi.fn()
    const scope = createBuildingScope(() => id.value, onReset)
    const before = scope.token()
    id.value = 'b'
    expect(onReset).toHaveBeenCalledOnce()
    expect(scope.isStale(before)).toBe(true)
    expect(scope.isStale(scope.token())).toBe(false)
  })

  it('does nothing without a watched id', () => {
    const scope = createBuildingScope()
    expect(scope.isStale(scope.token())).toBe(false)
  })

  it('settle returns the result while the token is current', async () => {
    const scope = createBuildingScope()
    await expect(scope.settle(scope.token(), Promise.resolve(5))).resolves.toBe(5)
  })

  it('settle rejects with the stale marker when the building changed during the request', async () => {
    const id = ref('a')
    const scope = createBuildingScope(() => id.value)
    const token = scope.token()
    const pending = scope.settle(token, new Promise((resolve) => setTimeout(() => resolve(1), 0)))
    id.value = 'b'
    await expect(pending).rejects.toSatisfy((e: unknown) => scope.isStaleError(e))
  })

  it('settle turns a late failure into the stale marker, but keeps real errors when current', async () => {
    const id = ref('a')
    const scope = createBuildingScope(() => id.value)
    const failing = Promise.reject(new Error('real'))
    await expect(scope.settle(scope.token(), failing)).rejects.toThrow('real')
    const token = scope.token()
    const late = scope.settle(token, new Promise((_, reject) => setTimeout(() => reject(new Error('late')), 0)))
    id.value = 'b'
    await expect(late).rejects.toSatisfy((e: unknown) => scope.isStaleError(e))
  })
})
