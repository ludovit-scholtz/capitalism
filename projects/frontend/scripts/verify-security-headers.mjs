import { readFile } from 'node:fs/promises'
import path from 'node:path'

import {
  computeCspSha256,
  extractAddHeaderValue,
  extractInlineScriptsFromHtml,
  getCspDirective,
} from './security-headers-utils.mjs'

async function main() {
  const projectRoot = process.cwd()
  const nginxConfPath = path.join(projectRoot, 'nginx.conf')
  const builtIndexPath = path.join(projectRoot, 'dist', 'index.html')

  const [nginxConf, builtIndexHtml] = await Promise.all([
    readFile(nginxConfPath, 'utf8'),
    readFile(builtIndexPath, 'utf8'),
  ])

  const hsts = extractAddHeaderValue(nginxConf, 'Strict-Transport-Security')
  if (hsts !== 'max-age=31536000; includeSubDomains') {
    throw new Error(`Expected HSTS header to equal "max-age=31536000; includeSubDomains", got ${JSON.stringify(hsts)}.`)
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

  const inlineScripts = extractInlineScriptsFromHtml(builtIndexHtml)
  const scriptSrcHashes = new Set(scriptSrc.filter((value) => value.startsWith("'sha256-") && value.endsWith("'")))

  for (const inlineScript of inlineScripts) {
    const expectedHash = `'${computeCspSha256(inlineScript)}'`
    if (!scriptSrcHashes.has(expectedHash)) {
      const preview = inlineScript.replace(/\s+/g, ' ').trim().slice(0, 80)
      throw new Error(
        `Missing CSP hash ${expectedHash} for an inline script in dist/index.html. Script preview: ${JSON.stringify(preview)}`,
      )
    }
  }

  console.log(
    `Verified frontend security headers: HSTS present, script-src excludes unsafe-inline, and ${inlineScripts.length} inline script(s) are covered by CSP hashes.`,
  )
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error)
  process.exit(1)
})
