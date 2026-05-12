import { readFileSync } from 'node:fs'
import path from 'node:path'

import { describe, expect, it } from 'vitest'

const projectRoot = path.resolve(import.meta.dirname, '../../..')

function extractAddHeaderValue(nginxConf: string, headerName: string): string | null {
  const escapedHeaderName = headerName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const headerRegex = new RegExp(`add_header\\s+${escapedHeaderName}\\s+"([^"]+)"\\s+always;`)
  const match = nginxConf.match(headerRegex)
  return match?.[1] ?? null
}

function getCspDirective(csp: string, directiveName: string): string[] {
  const directives = csp
    .split(';')
    .map((directive) => directive.trim())
    .filter(Boolean)

  const directive = directives.find(
    (item) => item.startsWith(`${directiveName} `) || item === directiveName,
  )
  if (!directive) {
    return []
  }

  return directive.split(/\s+/).slice(1)
}

describe('master-frontend security headers', () => {
  it('nginx.conf exists and is readable', () => {
    expect(() => readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')).not.toThrow()
  })

  it('configures HSTS for one year across subdomains', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    expect(extractAddHeaderValue(nginxConf, 'Strict-Transport-Security')).toBe(
      'max-age=31536000; includeSubDomains',
    )
  })

  it('configures CSP with self-only script-src and no unsafe-inline', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    const csp = extractAddHeaderValue(nginxConf, 'Content-Security-Policy')
    expect(csp).toBeTruthy()

    const scriptSrc = getCspDirective(csp ?? '', 'script-src')
    expect(scriptSrc).toContain("'self'")
    expect(scriptSrc).not.toContain("'unsafe-inline'")
  })

  it('sets X-Frame-Options to DENY', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    expect(nginxConf).toMatch(/add_header\s+X-Frame-Options\s+DENY\s+always;/)
  })

  it('sets X-Content-Type-Options to nosniff', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    expect(extractAddHeaderValue(nginxConf, 'X-Content-Type-Options')).toBe('nosniff')
  })

  it('sets Referrer-Policy to strict-origin-when-cross-origin', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    expect(extractAddHeaderValue(nginxConf, 'Referrer-Policy')).toBe(
      'strict-origin-when-cross-origin',
    )
  })

  it('sets Permissions-Policy with restrictive defaults', () => {
    const nginxConf = readFileSync(path.join(projectRoot, 'nginx.conf'), 'utf8')

    const permissionsPolicy = extractAddHeaderValue(nginxConf, 'Permissions-Policy')
    expect(permissionsPolicy).toBeTruthy()
    expect(permissionsPolicy).toContain('camera=()')
    expect(permissionsPolicy).toContain('microphone=()')
    expect(permissionsPolicy).toContain('geolocation=()')
  })
})
