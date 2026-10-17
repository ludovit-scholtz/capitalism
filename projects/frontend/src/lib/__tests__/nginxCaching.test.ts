import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// Files that are not content-hashed (or are generated per environment at container start) must be revalidated:
// the generic `.js` asset rule caches for a year with `immutable`, which would pin stale config in browsers.
const nginx = readFileSync(resolve(__dirname, '../../../nginx.conf'), 'utf-8').replace(/\r\n/g, '\n')

function locationBlock(path: string): string {
  const start = nginx.indexOf(`location = ${path} {`)
  expect(start, `nginx.conf must define an exact-match location for ${path}`).toBeGreaterThan(-1)
  const open = nginx.indexOf('{', start)
  let depth = 0
  for (let i = open; i < nginx.length; i += 1) {
    if (nginx[i] === '{') depth += 1
    if (nginx[i] === '}') depth -= 1
    if (depth === 0) return nginx.slice(start, i + 1)
  }
  throw new Error(`unterminated location block for ${path}`)
}

function genericAssetRuleIndex(): number {
  const match = /location ~\* \\.\(js\|css/.exec(nginx)
  expect(match, 'generic static asset location not found').not.toBeNull()
  return match?.index ?? -1
}

describe('nginx caching of non-hashed scripts', () => {
  for (const path of ['/gtag-init.js', '/runtime-config.js']) {
    it(`revalidates ${path} and still sends the security headers`, () => {
      const block = locationBlock(path)
      expect(block).toContain('add_header Cache-Control "no-cache"')
      expect(block).not.toContain('immutable')
      expect(block).toContain('X-Content-Type-Options')
      expect(block).toContain('Content-Security-Policy')
    })

    it(`defines the ${path} rule before the generic asset rule`, () => {
      expect(nginx.indexOf(`location = ${path} {`)).toBeLessThan(genericAssetRuleIndex())
    })
  }
})
