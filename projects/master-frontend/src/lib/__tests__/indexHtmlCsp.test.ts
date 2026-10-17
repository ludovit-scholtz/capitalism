import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

// The nginx CSP for the master portal is `script-src 'self' https://www.googletagmanager.com` with no inline
// allowance, so any inline <script> body in index.html is blocked by browsers (this silently broke analytics).
describe('master-frontend index.html and CSP', () => {
  const root = resolve(__dirname, '../../..')
  const html = readFileSync(resolve(root, 'index.html'), 'utf-8')
  const nginx = readFileSync(resolve(root, 'nginx.conf'), 'utf-8')

  it('has no inline script bodies (the CSP allows no inline scripts)', () => {
    const inlineBodies = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)]
      .map((match) => match[1] ?? '')
      .filter((body) => body.trim().length > 0)
    expect(inlineBodies, 'inline scripts are blocked by the CSP; move them to /public').toEqual([])
    expect(nginx).toMatch(/script-src 'self'(?! 'unsafe-inline')/)
  })

  it('serves the analytics bootstrap from a same-origin file', () => {
    expect(html).toContain('src="/gtag-init.js"')
    const init = readFileSync(resolve(root, 'public/gtag-init.js'), 'utf-8')
    expect(init).toContain("gtag('config', 'G-QQWP6LQH27')")
  })
})
