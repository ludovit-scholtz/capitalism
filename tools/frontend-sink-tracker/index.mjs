#!/usr/bin/env node
/**
 * Frontend Rich-Content Sink Tracker
 *
 * Scans Vue/TS source files for dangerous rich-content sinks:
 *   - v-html directives
 *   - innerHTML assignments
 *   - document.write calls
 *
 * For each sink, it checks whether DOMPurify.sanitize() is called
 * in the same component file (same-file sanitization heuristic).
 *
 * Outputs a JSON inventory and exits non-zero when unsanitized sinks are found
 * in --gate mode.
 *
 * Usage:
 *   node index.mjs --dirs <dir1> [<dir2>...] [--output <file>] [--gate]
 */

import { readFileSync, writeFileSync, readdirSync, statSync } from 'fs'
import { join, relative } from 'path'
import { parseArgs } from 'util'

// ---------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------

const SINK_PATTERNS = [
  { type: 'v-html', source: /\bv-html\s*=/g.source },
  { type: 'innerHTML', source: /\.innerHTML\s*=/g.source },
  { type: 'document.write', source: /\bdocument\.write\s*\(/g.source },
]

const SANITIZER_PATTERN = /\bDOMPurify\s*\.\s*sanitize\s*\(/

/** Extensions to scan */
const SCAN_EXTENSIONS = new Set(['.vue', '.ts', '.js', '.mjs'])

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Walk a directory tree and yield file paths */
function* walkDir(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const fullPath = join(dir, entry.name)
    if (entry.isDirectory()) {
      // Skip node_modules and dist/build outputs
      if (['node_modules', 'dist', '.git', 'coverage'].includes(entry.name)) continue
      yield* walkDir(fullPath)
    } else if (entry.isFile()) {
      const ext = entry.name.slice(entry.name.lastIndexOf('.'))
      if (SCAN_EXTENSIONS.has(ext)) yield fullPath
    }
  }
}

/**
 * Scan a single file for sinks, returning an array of findings.
 * @param {string} filePath
 * @param {string} baseDir - used to compute relative paths
 * @returns {Array<SinkEntry>}
 */
export function scanFile(filePath, baseDir) {
  let content
  try {
    content = readFileSync(filePath, 'utf-8')
  } catch {
    return []
}

  const lines = content.split('\n')
  const hasSanitizerInFile = SANITIZER_PATTERN.test(content)
  const findings = []

  for (const { type, source } of SINK_PATTERNS) {
    // Create a fresh RegExp per file to avoid global lastIndex state pollution
    const pattern = new RegExp(source, 'g')
    let match
    while ((match = pattern.exec(content)) !== null) {
      // Find line number (1-based)
      const before = content.slice(0, match.index)
      const lineNumber = before.split('\n').length

      // For innerHTML, do a quick check if DOMPurify.sanitize is on the same line
      // to detect inline sanitization patterns like:
      //   el.innerHTML = DOMPurify.sanitize(html)
      const lineText = lines[lineNumber - 1] || ''
      const inlineSanitized = SANITIZER_PATTERN.test(lineText)

      findings.push({
        filePath: relative(baseDir, filePath),
        lineNumber,
        sinkType: type,
        sanitized: hasSanitizerInFile || inlineSanitized,
        sanitizationNote: hasSanitizerInFile
          ? 'DOMPurify.sanitize() found in same file'
          : inlineSanitized
            ? 'DOMPurify.sanitize() found on same line'
            : 'No DOMPurify sanitization detected',
      })
    }
  }

  return findings
}

/**
 * Scan all source directories and return the full inventory.
 * @param {string[]} dirs
 * @param {string} baseDir
 */
export function scanDirectories(dirs, baseDir) {
  const allFindings = []

  for (const dir of dirs) {
    let resolvedDir = dir
    try {
      statSync(resolvedDir)
    } catch {
      console.warn(`[sink-tracker] Directory not found, skipping: ${dir}`)
      continue
    }

    for (const filePath of walkDir(resolvedDir)) {
      const findings = scanFile(filePath, baseDir)
      allFindings.push(...findings)
    }
  }

  return allFindings
}

// ---------------------------------------------------------------------------
// Report generation
// ---------------------------------------------------------------------------

/**
 * Build the JSON inventory object.
 */
export function buildInventory(findings, scannedDirs) {
  const unsanitized = findings.filter((f) => !f.sanitized)
  return {
    generatedAt: new Date().toISOString(),
    scannedDirectories: scannedDirs,
    totalSinks: findings.length,
    sanitizedSinks: findings.filter((f) => f.sanitized).length,
    unsanitizedSinks: unsanitized.length,
    sinks: findings,
  }
}

// ---------------------------------------------------------------------------
// Audit parsing helpers
// ---------------------------------------------------------------------------

/**
 * Parse npm audit JSON output and return high/critical production advisories.
 * @param {object} auditJson - parsed JSON from `npm audit --json`
 * @returns {Array} array of advisory objects with name, severity, url, via
 */
export function parseHighCriticalAdvisories(auditJson) {
  const vulns = auditJson?.vulnerabilities ?? {}
  return Object.values(vulns).filter((v) =>
    ['high', 'critical'].includes(v.severity?.toLowerCase()),
  )
}

// ---------------------------------------------------------------------------
// CLI entry point
// ---------------------------------------------------------------------------

async function main() {
  const { values, positionals } = parseArgs({
    options: {
      dirs: { type: 'string', multiple: true, short: 'd' },
      output: { type: 'string', short: 'o' },
      gate: { type: 'boolean', default: false },
      'base-dir': { type: 'string', default: process.cwd() },
    },
    allowPositionals: true,
    strict: false,
  })

  const dirs = values.dirs ?? positionals
  if (dirs.length === 0) {
    console.error('Usage: node index.mjs --dirs <dir1> [<dir2>...] [--output <file>] [--gate]')
    process.exit(1)
  }

  const baseDir = values['base-dir'] ?? process.cwd()

  console.log(`[sink-tracker] Scanning ${dirs.length} director${dirs.length === 1 ? 'y' : 'ies'}...`)

  const findings = scanDirectories(dirs, baseDir)
  const inventory = buildInventory(findings, dirs)

  console.log(`[sink-tracker] Found ${inventory.totalSinks} sink(s):`)
  console.log(`  Sanitized  : ${inventory.sanitizedSinks}`)
  console.log(`  Unsanitized: ${inventory.unsanitizedSinks}`)

  if (inventory.sinks.length > 0) {
    console.log('\n[sink-tracker] Sink details:')
    for (const sink of inventory.sinks) {
      const status = sink.sanitized ? '✅' : '⚠️ '
      console.log(`  ${status} ${sink.sinkType.padEnd(14)} ${sink.filePath}:${sink.lineNumber}`)
      if (!sink.sanitized) {
        console.log(`          → ${sink.sanitizationNote}`)
      }
    }
  }

  if (values.output) {
    writeFileSync(values.output, JSON.stringify(inventory, null, 2))
    console.log(`\n[sink-tracker] Inventory written to: ${values.output}`)
  }

  if (values.gate && inventory.unsanitizedSinks > 0) {
    console.error(
      `\n[sink-tracker] GATE FAILED: ${inventory.unsanitizedSinks} unsanitized rich-content sink(s) found.`,
    )
    console.error('[sink-tracker] Ensure all v-html / innerHTML usages are protected by DOMPurify.sanitize().')
    process.exit(1)
  }

  console.log('\n[sink-tracker] Done.')
}

// Only run CLI entry point when executed directly (not when imported as a module)
const isMain =
  process.argv[1] &&
  (process.argv[1].endsWith('/index.mjs') ||
    import.meta.url === new URL(`file://${process.argv[1]}`).href)

if (isMain) {
  main().catch((err) => {
    console.error(err)
    process.exit(1)
  })
}
