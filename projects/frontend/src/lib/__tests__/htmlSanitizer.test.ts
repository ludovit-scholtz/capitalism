// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'

import { sanitizeRichHtml } from '../htmlSanitizer'

describe('sanitizeRichHtml', () => {
  it.each([
    '<script>alert(1)</script>',
    '<svg onload=alert(1)>',
    '<a href="javascript:alert(1)">x</a>',
    '<img src=x onerror=alert(1)>',
    '<div style="expression(alert(1))">x</div>',
    '<noscript><p title="</noscript><img src=x onerror=alert(1)>">',
    '<a href="&#106;avascript:alert(1)">x</a>',
    '<scr<script>ipt>alert(1)</scr<script>ipt>',
  ])('strips dangerous payload %s', (payload) => {
    const sanitized = sanitizeRichHtml(payload)
    expect(sanitized).not.toMatch(/<script|<svg|onload\s*=|onerror\s*=|javascript:/i)
    expect(sanitized).not.toMatch(/expression\s*\(/i)
  })

  it('keeps safe formatting', () => {
    const sanitized = sanitizeRichHtml(
      '<p><strong>Bold</strong> <em>Italic</em></p><ol><li>A</li></ol><pre><code>x</code></pre><a href="https://example.com">safe</a>',
    )
    expect(sanitized).toContain('<strong>Bold</strong>')
    expect(sanitized).toContain('<em>Italic</em>')
    expect(sanitized).toContain('<ol><li>A</li></ol>')
    expect(sanitized).toContain('<pre><code>x</code></pre>')
    expect(sanitized).toContain('href="https://example.com"')
  })
})
