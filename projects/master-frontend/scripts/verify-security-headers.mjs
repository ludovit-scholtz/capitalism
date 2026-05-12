import { readFile } from 'node:fs/promises'
import path from 'node:path'

function extractAddHeaderValue(nginxConf, headerName) {
  const escapedHeaderName = headerName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const headerRegex = new RegExp(`add_header\\s+${escapedHeaderName}\\s+"([^"]+)"\\s+always;`)
  const match = nginxConf.match(headerRegex)
  return match?.[1] ?? null
}

function getCspDirective(csp, directiveName) {
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

async function main() {
  const projectRoot = process.cwd()
  const nginxConfPath = path.join(projectRoot, 'nginx.conf')

  const nginxConf = await readFile(nginxConfPath, 'utf8')

  const hsts = extractAddHeaderValue(nginxConf, 'Strict-Transport-Security')
  if (hsts !== 'max-age=31536000; includeSubDomains') {
    throw new Error(
      `Expected HSTS header to equal "max-age=31536000; includeSubDomains", got ${JSON.stringify(hsts)}.`,
    )
  }

  const csp = extractAddHeaderValue(nginxConf, 'Content-Security-Policy')
  if (!csp) {
    throw new Error('Content-Security-Policy header is missing from nginx.conf.')
  }

  const scriptSrc = getCspDirective(csp, 'script-src')
  if (scriptSrc.length === 0) {
    throw new Error('script-src directive is missing from the Content-Security-Policy header.')
  }

  if (scriptSrc.includes("'unsafe-inline'")) {
    throw new Error("script-src must not include 'unsafe-inline'.")
  }

  if (!scriptSrc.includes("'self'")) {
    throw new Error("script-src must include 'self'.")
  }

  const xFrameOptions = nginxConf.match(/add_header\s+X-Frame-Options\s+(\S+)\s+always;/)
  if (!xFrameOptions) {
    throw new Error('X-Frame-Options header is missing from nginx.conf.')
  }

  const xContentTypeOptions = extractAddHeaderValue(nginxConf, 'X-Content-Type-Options')
  if (xContentTypeOptions !== 'nosniff') {
    throw new Error(
      `Expected X-Content-Type-Options to equal "nosniff", got ${JSON.stringify(xContentTypeOptions)}.`,
    )
  }

  const referrerPolicy = extractAddHeaderValue(nginxConf, 'Referrer-Policy')
  if (referrerPolicy !== 'strict-origin-when-cross-origin') {
    throw new Error(
      `Expected Referrer-Policy to equal "strict-origin-when-cross-origin", got ${JSON.stringify(referrerPolicy)}.`,
    )
  }

  const permissionsPolicy = extractAddHeaderValue(nginxConf, 'Permissions-Policy')
  if (!permissionsPolicy) {
    throw new Error('Permissions-Policy header is missing from nginx.conf.')
  }

  console.log(
    'Verified master-frontend security headers: HSTS, CSP (no unsafe-inline), X-Frame-Options, X-Content-Type-Options, Referrer-Policy, and Permissions-Policy are all present.',
  )
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error)
  process.exit(1)
})
