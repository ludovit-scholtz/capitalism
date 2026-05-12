import { createHash } from 'node:crypto'

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
  const inlineScripts = []
  const scriptRegex = /<script([^>]*)>([\s\S]*?)<\/script>/gi

  for (const match of html.matchAll(scriptRegex)) {
    const attributes = match[1] ?? ''
    if (/\ssrc\s*=/.test(attributes)) {
      continue
    }

    const contents = match[2] ?? ''
    if (contents.trim()) {
      inlineScripts.push(contents)
    }
  }

  return inlineScripts
}

export function computeCspSha256(scriptContent) {
  return `sha256-${createHash('sha256').update(scriptContent).digest('base64')}`
}
