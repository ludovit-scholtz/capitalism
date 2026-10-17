/**
 * Enforces the repository's 500-line source file limit.
 *
 * Tracked .cs/.ts/.vue/.dart files (excluding migrations, generated code, locales and build output) may not exceed 500 lines.
 * Existing offenders are listed in .github/file-size-allowlist.json with their current line count. The check fails when
 *   - a file that is not on the list exceeds the limit (NEW_OVER_LIMIT),
 *   - a listed file grows (GREW),
 *   - a listed file dropped under the limit but is still listed (STALE_ALLOWLIST_ENTRY).
 * The list can therefore only shrink. `--update` lowers entries for shrunk files and removes entries for files that are now
 * within the limit or deleted; it never adds a file.
 *
 * Usage:
 *   node .github/scripts/file-size-guard.mjs            # check (CI)
 *   node .github/scripts/file-size-guard.mjs --update   # tighten the allow-list after splitting files
 *   node .github/scripts/file-size-guard.mjs --init     # one-time creation of the allow-list from the current offenders
 */

import { execFileSync } from 'node:child_process'
import { readFileSync, writeFileSync, existsSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

export const LINE_LIMIT = 500

const CHECKED_EXTENSIONS = ['.cs', '.ts', '.vue', '.dart']
const EXCLUDED_PATTERNS = [
  /(^|\/)Migrations\//,
  /\.Designer\.cs$/,
  /\.g\.dart$/,
  /\.generated\./,
  /(^|\/)i18n\/locales\//,
  /(^|\/)(bin|obj|dist|node_modules|\.dart_tool|build)\//,
]

/** Number of lines like `wc -l`: a trailing newline does not add an empty line, CRLF counts once. */
export function countLines(text) {
  if (text.length === 0) return 0
  const normalized = text.replace(/\r\n/g, '\n')
  const withoutTrailingNewline = normalized.endsWith('\n') ? normalized.slice(0, -1) : normalized
  return withoutTrailingNewline.split('\n').length
}

export function isCheckedSourceFile(path) {
  const normalized = path.replace(/\\/g, '/')
  if (!CHECKED_EXTENSIONS.some((extension) => normalized.endsWith(extension))) return false
  return !EXCLUDED_PATTERNS.some((pattern) => pattern.test(normalized))
}

/**
 * @param {{path: string, lines: number}[]} files current line counts of the checked files
 * @param {Record<string, number>} allowList path -> maximum allowed lines
 */
export function evaluateFileSizes(files, allowList, limit = LINE_LIMIT) {
  const violations = []
  const shrunk = []
  const byPath = new Map(files.map((file) => [file.path, file.lines]))

  for (const { path, lines } of files) {
    const allowed = allowList[path]
    if (allowed === undefined) {
      if (lines > limit) {
        violations.push({ kind: 'NEW_OVER_LIMIT', path, lines, allowed: limit, message: `${path} has ${lines} lines (limit ${limit}). Split it instead of adding it to the allow-list.` })
      }
    } else if (lines <= limit) {
      violations.push({ kind: 'STALE_ALLOWLIST_ENTRY', path, lines, allowed, message: `${path} is down to ${lines} lines: remove it from the allow-list (node .github/scripts/file-size-guard.mjs --update).` })
    } else if (lines > allowed) {
      violations.push({ kind: 'GREW', path, lines, allowed, message: `${path} grew from ${allowed} to ${lines} lines. Extract code instead of growing an oversized file.` })
    } else if (lines < allowed) {
      shrunk.push({ path, lines, allowed })
    }
  }

  const missing = Object.keys(allowList).filter((path) => !byPath.has(path)).sort()
  return { violations, shrunk, missing }
}

/** Lowers shrunk entries and drops entries for removed or now-compliant files. Never adds new files. */
export function tightenAllowList(files, allowList, limit = LINE_LIMIT) {
  const next = {}
  for (const { path, lines } of files) {
    const allowed = allowList[path]
    if (allowed !== undefined && lines > limit) {
      next[path] = Math.min(allowed, lines)
    }
  }
  return Object.fromEntries(Object.entries(next).sort(([a], [b]) => a.localeCompare(b)))
}

function repoRoot() {
  return resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')
}

function listTrackedFiles(root) {
  return execFileSync('git', ['ls-files', '-z'], { cwd: root, maxBuffer: 64 * 1024 * 1024 })
    .toString('utf8')
    .split('\0')
    .filter((path) => path.length > 0)
}

export function measureTrackedFiles(root = repoRoot()) {
  return listTrackedFiles(root)
    .filter(isCheckedSourceFile)
    .filter((path) => existsSync(resolve(root, path)))
    .map((path) => ({ path, lines: countLines(readFileSync(resolve(root, path), 'utf8')) }))
}

function main() {
  const root = repoRoot()
  const allowListPath = resolve(root, '.github', 'file-size-allowlist.json')
  const allowList = existsSync(allowListPath) ? JSON.parse(readFileSync(allowListPath, 'utf8')) : {}
  const files = measureTrackedFiles(root)

  if (process.argv.includes('--init')) {
    if (existsSync(allowListPath)) {
      console.error('Allow-list already exists. Use --update to tighten it; it must never gain entries.')
      process.exit(1)
    }
    const initial = Object.fromEntries(files.filter((file) => file.lines > LINE_LIMIT).sort((a, b) => a.path.localeCompare(b.path)).map((file) => [file.path, file.lines]))
    writeFileSync(allowListPath, `${JSON.stringify(initial, null, 2)}
`)
    console.log(`Allow-list created with ${Object.keys(initial).length} entries.`)
    return
  }

  if (process.argv.includes('--update')) {
    const next = tightenAllowList(files, allowList)
    writeFileSync(allowListPath, `${JSON.stringify(next, null, 2)}\n`)
    console.log(`Allow-list updated: ${Object.keys(next).length} entries (was ${Object.keys(allowList).length}).`)
    return
  }

  const { violations, shrunk, missing } = evaluateFileSizes(files, allowList)
  for (const entry of shrunk) {
    console.log(`note: ${entry.path} shrank from ${entry.allowed} to ${entry.lines} lines - run with --update to lock in the gain.`)
  }
  for (const path of missing) {
    console.log(`note: ${path} no longer exists - run with --update to drop it from the allow-list.`)
  }
  if (violations.length > 0) {
    for (const violation of violations) console.error(`::error file=${violation.path}::${violation.message}`)
    console.error(`\n${violations.length} file size violation(s). The ${LINE_LIMIT}-line limit applies to every source file; the allow-list may only shrink.`)
    process.exit(1)
  }
  console.log(`File size check passed (${files.length} files checked, ${Object.keys(allowList).length} allow-listed).`)
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main()
}
