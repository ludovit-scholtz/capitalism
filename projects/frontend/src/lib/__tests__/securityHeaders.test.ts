import { readFileSync } from 'node:fs'
import path from 'node:path'

import { describe, expect, it } from 'vitest'

import {
  computeCspSha256,
  extractAddHeaderValue,
  extractInlineScriptsFromHtml,
  getCspDirective,
} from '../../../scripts/security-headers-utils.mjs'

const projectRoot = path.resolve(import.meta.dirname, '../../..')

describe('frontend security headers', () => {
  it('configures HSTS for one year across subdomains', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    expect(extractAddHeaderValue(nginxConf, 'Strict-Transport-Security')).toBe(
      'max-age=31536000; includeSubDomains',
    )
  })

  it('uses hash-based script-src instead of unsafe-inline for the theme bootstrap script', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')
    const indexHtml = readFileSync(path.join(projectRoot, 'index.html'), 'utf8')

    const csp = extractAddHeaderValue(nginxConf, 'Content-Security-Policy')
    expect(csp).toBeTruthy()

    const scriptSrc = getCspDirective(csp ?? '', 'script-src')
    expect(scriptSrc).toContain("'self'")
    expect(scriptSrc).not.toContain("'unsafe-inline'")

    const inlineScripts = extractInlineScriptsFromHtml(indexHtml)
    expect(inlineScripts).toHaveLength(1)
    expect(scriptSrc).toContain(`'${computeCspSha256(inlineScripts[0])}'`)
  })
})
