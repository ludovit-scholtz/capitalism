import { createHash } from 'node:crypto'
import { JSDOM } from 'jsdom'

export function extractAddHeaderValue(nginxConf, headerName) {
  const escapedHeaderName = headerName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const headerRegex = new RegExp(`add_header\\s+${escapedHeaderName}\\s+"([^"]+)"\\s+always;`)
  const match = nginxConf.match(headerRegex)
  return match?.[1] ?? null
}

export function getCspDirective(csp, directiveName) {
  const directives = csp
    .split(';')
    .map((directive) => directive.trim())
    .filter(Boolean)

  const directive = directives.find((item) => item.startsWith(`${directiveName} `) || item === directiveName)
  if (!directive) {
    return []
  }

  return directive.split(/\s+/).slice(1)
}

export function extractInlineScriptsFromHtml(html) {
  const dom = new JSDOM(html)
  return Array.from(dom.window.document.querySelectorAll('script'))
    .filter((script) => !script.src && script.textContent?.trim())
    .map((script) => script.textContent ?? '')
}

export function computeCspSha256(scriptContent) {
  return `sha256-${createHash('sha256').update(scriptContent).digest('base64')}`
}
