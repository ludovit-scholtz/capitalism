#!/usr/bin/env node
/**
 * audit-board.mjs
 *
 * Parses every /audits/*.md file, extracts High and Critical findings from the
 * Risk register section, checks for linked GitHub issue annotations, and either:
 *   - Outputs a structured JSON report to stdout (--report mode)
 *   - Writes docs/security-board.md (--write-board mode)
 *   - Fails (exit 1) if unlinked High/Critical findings exist in the latest audit (--gate mode)
 *
 * Issue annotation convention in an audit file:
 *   <!-- issue: #123 -->
 *   <!-- issues: #123, #456 -->
 *
 * Usage:
 *   node .github/scripts/audit-board.mjs --gate   [--audits-dir audits]
 *   node .github/scripts/audit-board.mjs --report  [--audits-dir audits]
 *   node .github/scripts/audit-board.mjs --write-board [--audits-dir audits] [--board-file docs/security-board.md]
 *   node .github/scripts/audit-board.mjs --write-report [--audits-dir audits] [--report-file docs/security-board-report.json]
 */

import { readFileSync, writeFileSync, readdirSync, existsSync } from 'fs'
import { resolve, basename, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = resolve(__dirname, '../..')

// ---------------------------------------------------------------------------
// Argument parsing
// ---------------------------------------------------------------------------

const args = process.argv.slice(2)
const mode = args.includes('--gate')
  ? 'gate'
  : args.includes('--write-board')
    ? 'write-board'
    : args.includes('--write-report')
      ? 'write-report'
      : 'report'

const auditsDirArg = args.indexOf('--audits-dir')
const auditsDir = auditsDirArg >= 0 ? resolve(args[auditsDirArg + 1]) : resolve(REPO_ROOT, 'audits')

const boardFileArg = args.indexOf('--board-file')
const boardFile =
  boardFileArg >= 0
    ? resolve(args[boardFileArg + 1])
    : resolve(REPO_ROOT, 'docs/security-board.md')

const reportFileArg = args.indexOf('--report-file')
const reportFile =
  reportFileArg >= 0
    ? resolve(args[reportFileArg + 1])
    : resolve(REPO_ROOT, 'docs/security-board-report.json')

const ownersFile = resolve(auditsDir, 'owners.yml')

// ---------------------------------------------------------------------------
// Owner loading (simple YAML key: value parser — no external deps)
// ---------------------------------------------------------------------------

/**
 * @param {string} filePath
 * @returns {Record<string, string>}
 */
export function loadOwners(filePath) {
  if (!existsSync(filePath)) return {}
  const text = readFileSync(filePath, 'utf8')
  /** @type {Record<string, string>} */
  const owners = {}
  for (const line of text.split('\n')) {
    const trimmed = line.trim()
    if (!trimmed || trimmed.startsWith('#')) continue
    const colonIdx = trimmed.indexOf(':')
    if (colonIdx < 0) continue
    const key = trimmed.slice(0, colonIdx).trim()
    const value = trimmed.slice(colonIdx + 1).trim()
    if (key && value) owners[key] = value
  }
  return owners
}

// ---------------------------------------------------------------------------
// Markdown parsing
// ---------------------------------------------------------------------------

/** Converts a heading text to a GitHub-style anchor slug */
export function slugify(text) {
  return text
    .toLowerCase()
    .replace(/[^\w\s-]/g, '')
    .trim()
    .replace(/[\s]+/g, '-')
}

/**
 * Extracts issue numbers from <!-- issue: #NNN --> or <!-- issues: #NNN, #NNN --> annotations.
 * @param {string} text
 * @returns {number[]}
 */
export function extractIssueRefs(text) {
  const issues = []
  const pattern = /<!--\s*issues?:\s*([\s\S]*?)-->/gi
  let match
  while ((match = pattern.exec(text)) !== null) {
    const refs = match[1].split(',')
    for (const ref of refs) {
      const num = parseInt(ref.replace(/[^0-9]/g, ''), 10)
      if (!isNaN(num) && num > 0) issues.push(num)
    }
  }
  return [...new Set(issues)]
}

/**
 * @typedef {Object} AuditFinding
 * @property {string} slug       - stable identifier: <file-stem>/<heading-anchor>
 * @property {string} fileStem   - audit file stem e.g. '2026-W19-security-audit'
 * @property {string} filePath   - full path to the audit file
 * @property {number} number     - finding number (1-based)
 * @property {string} title      - finding title
 * @property {'Critical'|'High'|'Medium'|'Low'|string} severity
 * @property {string} status     - finding status string
 * @property {number[]} issues   - linked GitHub issue numbers
 * @property {string} owner      - GitHub username or '' if not assigned
 * @property {string} section    - raw markdown section text
 */

/**
 * Parses all findings from an audit markdown file.
 * @param {string} filePath
 * @param {Record<string,string>} owners
 * @returns {AuditFinding[]}
 */
export function parseAuditFile(filePath, owners = {}) {
  let text
  try {
    text = readFileSync(filePath, 'utf8')
  } catch {
    return []
  }

  const fileStem = basename(filePath, '.md')
  const findings = []

  // Split on ### headings that look like risk register findings: ### N) Title
  // We first isolate the Risk register section if present, falling back to full text
  let riskText = text
  const riskRegisterStart = text.search(/^##\s+Risk register\s*$/im)
  if (riskRegisterStart >= 0) {
    // Find the start of the next level-2 (##) heading after the risk register
    const afterHeader = riskRegisterStart + text.slice(riskRegisterStart).search(/\n/)
    const nextSectionMatch = text.slice(afterHeader + 1).search(/^##\s/im)
    riskText = nextSectionMatch >= 0
      ? text.slice(afterHeader + 1, afterHeader + 1 + nextSectionMatch)
      : text.slice(afterHeader + 1)
  }

  // Match each finding heading and the text that follows until the next ### or end
  const findingRe = /^###\s+(\d+)\)\s+(.+?)\s*$/gim
  let match
  const positions = []
  while ((match = findingRe.exec(riskText)) !== null) {
    positions.push({ idx: match.index, num: parseInt(match[1], 10), title: match[2].trim() })
  }

  for (let i = 0; i < positions.length; i++) {
    const { idx, num, title } = positions[i]
    const end = i + 1 < positions.length ? positions[i + 1].idx : riskText.length
    const section = riskText.slice(idx, end)

    // Extract severity
    const severityMatch = section.match(/\*\*Severity:\*\*\s*(.+)/i)
    const severity = severityMatch ? severityMatch[1].trim() : 'Unknown'

    // Only care about High and Critical for gate purposes; still include all
    // Extract status
    const statusMatch = section.match(/\*\*Status:\*\*\s*(.+)/i)
    const rawStatus = statusMatch ? statusMatch[1].trim() : 'Unknown'
    // Strip trailing issue annotation from status line
    const status = rawStatus.replace(/<!--.*?-->/g, '').trim()

    // Extract linked issues from the section
    const issues = extractIssueRefs(section)

    // Build a stable slug
    const anchor = slugify(title)
    const slug = `${fileStem}/${anchor}`

    const owner = owners[slug] ?? owners[`${num}`] ?? ''

    findings.push({ slug, fileStem, filePath, number: num, title, severity, status, issues, owner, section })
  }

  return findings
}

// ---------------------------------------------------------------------------
// Board generation
// ---------------------------------------------------------------------------

const SEVERITY_ORDER = { Critical: 0, High: 1, Medium: 2, Low: 3, Unknown: 4 }

/** @param {AuditFinding[]} findings */
export function buildBoard(findings) {
  const sorted = [...findings].sort((a, b) => {
    const sa = SEVERITY_ORDER[a.severity] ?? 4
    const sb = SEVERITY_ORDER[b.severity] ?? 4
    if (sa !== sb) return sa - sb
    return a.fileStem.localeCompare(b.fileStem)
  })

  const rows = sorted
    .map((f) => {
      const issueLinks =
        f.issues.length > 0
          ? f.issues.map((n) => `[#${n}](https://github.com/ludovit-scholtz/capitalism/issues/${n})`).join(', ')
          : '—'
      const auditLink = `[${f.fileStem}](../../audits/${f.fileStem}.md#${slugify(f.title)})`
      return `| ${f.severity} | ${f.title} | ${f.status} | ${f.owner || '—'} | ${issueLinks} | ${auditLink} |`
    })
    .join('\n')

  const table = `| Severity | Finding | Status | Owner | Issues | Source |
|----------|---------|--------|-------|--------|--------|
${rows}`

  const now = new Date().toISOString().slice(0, 10)
  const open = findings.filter((f) => !['Resolved'].includes(f.status))
  const openHighCrit = open.filter(
    (f) => f.severity === 'High' || f.severity === 'Critical',
  )
  const unlinked = openHighCrit.filter((f) => f.issues.length === 0)

  const summaryLine =
    unlinked.length === 0
      ? '✅ **All clear** — No unlinked High/Critical open findings.'
      : `⚠️ **${unlinked.length} unlinked High/Critical finding(s) require linked implementation issues.**`

  return `# Security Action Board

> Auto-generated from \`/audits/*.md\` on ${now}.  
> Add \`<!-- issue: #NNN -->\` in a finding's **Status** line to link an implementation issue.

${summaryLine}

## All findings

${table}
`
}

// ---------------------------------------------------------------------------
// Gate check
// ---------------------------------------------------------------------------

/**
 * Returns findings that fail the gate:
 * High/Critical, not Resolved, and no linked issue — from the LATEST audit only.
 * @param {AuditFinding[]} allFindings
 * @returns {{ failing: AuditFinding[], latestStem: string }}
 */
export function runGateCheck(allFindings) {
  // Group by fileStem and find the latest
  const stems = [...new Set(allFindings.map((f) => f.fileStem))].sort()
  if (stems.length === 0) return { failing: [], latestStem: '' }

  const latestStem = stems[stems.length - 1]
  const latest = allFindings.filter((f) => f.fileStem === latestStem)

  const failing = latest.filter(
    (f) =>
      (f.severity === 'High' || f.severity === 'Critical') &&
      f.status !== 'Resolved' &&
      f.issues.length === 0,
  )

  return { failing, latestStem }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

export function collectAllFindings(auditsDirectory, ownersFilePath) {
  const owners = loadOwners(ownersFilePath)

  let files
  try {
    files = readdirSync(auditsDirectory)
  } catch {
    return []
  }

  const mdFiles = files
    .filter((f) => f.endsWith('.md') && !f.endsWith('owners.md'))
    .map((f) => resolve(auditsDirectory, f))
    .sort()

  const all = []
  for (const f of mdFiles) {
    all.push(...parseAuditFile(f, owners))
  }
  return all
}

// Only run main when invoked directly (not when imported by tests)
const isMain = process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))

if (isMain) {
  const findings = collectAllFindings(auditsDir, ownersFile)

  if (mode === 'gate') {
    const { failing, latestStem } = runGateCheck(findings)
    if (failing.length === 0) {
      console.log(`✅ Security gate passed (latest audit: ${latestStem})`)
      process.exit(0)
    }

    console.error(`\n❌ Security gate FAILED — ${failing.length} High/Critical finding(s) in "${latestStem}" have no linked GitHub issue.\n`)
    console.error('Add  <!-- issue: #NNN -->  to the finding\'s Status line in the audit file to link an implementation issue.\n')
    console.error('Failing findings:')
    console.error()
    for (const f of failing) {
      const auditUrl = `https://github.com/ludovit-scholtz/capitalism/blob/main/audits/${f.fileStem}.md#${slugify(f.title)}`
      console.error(`  [${f.severity}] ${f.title}`)
      console.error(`    Status: ${f.status}`)
      console.error(`    Audit:  ${auditUrl}`)
      console.error()
    }
    process.exit(1)
  }

  if (mode === 'write-board') {
    const board = buildBoard(findings)
    writeFileSync(boardFile, board, 'utf8')
    console.log(`✅ Security board written to ${boardFile}`)
    process.exit(0)
  }

  if (mode === 'write-report') {
    const { failing } = runGateCheck(findings)
    const report = {
      generatedAt: new Date().toISOString(),
      totalFindings: findings.length,
      gateStatus: failing.length === 0 ? 'pass' : 'fail',
      failingCount: failing.length,
      findings: findings.map(({ section: _s, ...rest }) => rest),
    }
    writeFileSync(reportFile, JSON.stringify(report, null, 2), 'utf8')
    console.log(`✅ Security board JSON report written to ${reportFile}`)
    process.exit(0)
  }

  // report
  const { failing } = runGateCheck(findings)
  const report = {
    generatedAt: new Date().toISOString(),
    totalFindings: findings.length,
    gateStatus: failing.length === 0 ? 'pass' : 'fail',
    failingCount: failing.length,
    findings: findings.map(({ section: _s, ...rest }) => rest),
  }
  console.log(JSON.stringify(report, null, 2))
}
