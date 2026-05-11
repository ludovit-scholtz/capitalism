/**
 * Unit tests for .github/scripts/audit-board.mjs
 *
 * Run with:  node --test .github/scripts/__tests__/audit-board.test.mjs
 */

import { describe, it } from 'node:test'
import assert from 'node:assert/strict'
import { writeFileSync, mkdirSync, rmSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { tmpdir } from 'node:os'

import {
  slugify,
  extractIssueRefs,
  parseAuditFile,
  loadOwners,
  runGateCheck,
  buildBoard,
  collectAllFindings,
} from '../audit-board.mjs'

const __dirname = dirname(fileURLToPath(import.meta.url))

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeTmpDir() {
  const dir = resolve(tmpdir(), `audit-board-test-${Math.random().toString(36).slice(2)}`)
  mkdirSync(dir, { recursive: true })
  return dir
}

function writeFile(dir, name, content) {
  writeFileSync(resolve(dir, name), content, 'utf8')
}

// ---------------------------------------------------------------------------
// slugify
// ---------------------------------------------------------------------------

describe('slugify', () => {
  it('converts spaces to hyphens', () => {
    assert.equal(slugify('Hello World'), 'hello-world')
  })

  it('strips special characters', () => {
    assert.equal(slugify('Foo/Bar (baz)'), 'foobar-baz')
  })

  it('handles numbers', () => {
    assert.equal(slugify('Finding 1: SQL injection'), 'finding-1-sql-injection')
  })
})

// ---------------------------------------------------------------------------
// extractIssueRefs
// ---------------------------------------------------------------------------

describe('extractIssueRefs', () => {
  it('extracts a single issue ref', () => {
    assert.deepEqual(extractIssueRefs('- **Status:** Open <!-- issue: #123 -->'), [123])
  })

  it('extracts multiple issue refs from issues: annotation', () => {
    assert.deepEqual(extractIssueRefs('<!-- issues: #10, #20, #30 -->'), [10, 20, 30])
  })

  it('returns empty array when no refs present', () => {
    assert.deepEqual(extractIssueRefs('- **Status:** Open'), [])
  })

  it('deduplicates refs', () => {
    assert.deepEqual(extractIssueRefs('<!-- issue: #5 --> <!-- issue: #5 -->'), [5])
  })

  it('ignores invalid refs', () => {
    assert.deepEqual(extractIssueRefs('<!-- issue: #abc -->'), [])
  })
})

// ---------------------------------------------------------------------------
// loadOwners
// ---------------------------------------------------------------------------

describe('loadOwners', () => {
  it('returns empty object for missing file', () => {
    assert.deepEqual(loadOwners('/nonexistent/owners.yml'), {})
  })

  it('parses key: value pairs', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      'owners.yml',
      `# owners
2026-W19-security-audit/unauthenticated-loan-offer-intelligence-leak: @alice
2026-W19-security-audit/fx-execution-fairness: @bob
`,
    )
    const owners = loadOwners(resolve(dir, 'owners.yml'))
    assert.equal(
      owners['2026-W19-security-audit/unauthenticated-loan-offer-intelligence-leak'],
      '@alice',
    )
    assert.equal(owners['2026-W19-security-audit/fx-execution-fairness'], '@bob')
    rmSync(dir, { recursive: true })
  })

  it('skips comment lines and blank lines', () => {
    const dir = makeTmpDir()
    writeFile(dir, 'owners.yml', `# this is a comment\n\nfoo: @bar\n`)
    const owners = loadOwners(resolve(dir, 'owners.yml'))
    assert.equal(owners['foo'], '@bar')
    assert.equal(Object.keys(owners).length, 1)
    rmSync(dir, { recursive: true })
  })
})

// ---------------------------------------------------------------------------
// parseAuditFile — no findings
// ---------------------------------------------------------------------------

describe('parseAuditFile - no findings', () => {
  it('returns empty array for file with no risk register', () => {
    const dir = makeTmpDir()
    writeFile(dir, '2026-W01-audit.md', '# Audit\n\nNo findings here.\n')
    const findings = parseAuditFile(resolve(dir, '2026-W01-audit.md'))
    assert.equal(findings.length, 0)
    rmSync(dir, { recursive: true })
  })

  it('returns empty array for missing file', () => {
    const findings = parseAuditFile('/nonexistent/file.md')
    assert.deepEqual(findings, [])
  })

  it('returns empty array when only Low/Medium findings', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W01-audit.md',
      `# Audit

## Risk register

### 1) Minor info leak

- **Severity:** Low
- **Status:** Open

### 2) Another issue

- **Severity:** Medium
- **Status:** Open
`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W01-audit.md'))
    // Still parsed — but gate filter is applied separately
    assert.equal(findings.length, 2)
    assert.equal(findings[0].severity, 'Low')
    assert.equal(findings[1].severity, 'Medium')
    rmSync(dir, { recursive: true })
  })
})

// ---------------------------------------------------------------------------
// parseAuditFile — linked finding (gate should pass)
// ---------------------------------------------------------------------------

describe('parseAuditFile - linked Critical finding', () => {
  it('parses linked issue from status line', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W02-audit.md',
      `# Audit

## Risk register

### 1) Token boundary confusion

- **Severity:** Critical
- **Affected endpoint or mechanic:** auth service
- **Status:** Open <!-- issue: #42 -->
`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W02-audit.md'))
    assert.equal(findings.length, 1)
    assert.equal(findings[0].severity, 'Critical')
    assert.deepEqual(findings[0].issues, [42])
    assert.equal(findings[0].status, 'Open')
    rmSync(dir, { recursive: true })
  })
})

// ---------------------------------------------------------------------------
// parseAuditFile — unlinked High finding (gate should fail)
// ---------------------------------------------------------------------------

describe('parseAuditFile - unlinked High finding', () => {
  it('returns finding with no issues', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W03-audit.md',
      `# Audit

## Risk register

### 1) API key full scope

- **Severity:** High
- **Status:** Open
`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W03-audit.md'))
    assert.equal(findings.length, 1)
    assert.equal(findings[0].severity, 'High')
    assert.deepEqual(findings[0].issues, [])
    rmSync(dir, { recursive: true })
  })
})

// ---------------------------------------------------------------------------
// parseAuditFile — malformed markdown
// ---------------------------------------------------------------------------

describe('parseAuditFile - malformed markdown', () => {
  it('gracefully handles malformed sections', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W04-audit.md',
      `# Audit\n\n## Risk register\n\nNo valid headings here - just some text\n### no-number-format Title\n- **Severity:** High\n`,
    )
    // Should not throw
    const findings = parseAuditFile(resolve(dir, '2026-W04-audit.md'))
    // 'no-number-format' doesn't match the ### N) Title pattern
    assert.equal(findings.length, 0)
  })
})

// ---------------------------------------------------------------------------
// runGateCheck
// ---------------------------------------------------------------------------

describe('runGateCheck', () => {
  function makeFinding(overrides = {}) {
    return {
      slug: 'test-audit/finding',
      fileStem: 'test-audit',
      filePath: '/audits/test-audit.md',
      number: 1,
      title: 'Test Finding',
      severity: 'High',
      status: 'Open',
      issues: [],
      owner: '',
      ...overrides,
    }
  }

  it('returns empty failing when no findings', () => {
    const { failing } = runGateCheck([])
    assert.equal(failing.length, 0)
  })

  it('returns empty failing when all High/Critical findings are Resolved', () => {
    const findings = [
      makeFinding({ severity: 'High', status: 'Resolved', issues: [] }),
      makeFinding({ severity: 'Critical', status: 'Resolved', issues: [] }),
    ]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 0)
  })

  it('returns empty failing when all High/Critical findings have linked issues', () => {
    const findings = [
      makeFinding({ severity: 'High', status: 'Open', issues: [10] }),
      makeFinding({ severity: 'Critical', status: 'Open', issues: [11] }),
    ]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 0)
  })

  it('fails gate when unlinked High finding exists', () => {
    const findings = [makeFinding({ severity: 'High', status: 'Open', issues: [] })]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 1)
    assert.equal(failing[0].severity, 'High')
  })

  it('fails gate when unlinked Critical finding exists', () => {
    const findings = [makeFinding({ severity: 'Critical', status: 'In-Progress', issues: [] })]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 1)
  })

  it('only checks the LATEST audit file', () => {
    const old = makeFinding({ fileStem: '2026-W01-audit', severity: 'High', status: 'Open', issues: [] })
    const latest = makeFinding({
      fileStem: '2026-W02-audit',
      severity: 'High',
      status: 'Open',
      issues: [99],
    })
    const { failing, latestStem } = runGateCheck([old, latest])
    assert.equal(latestStem, '2026-W02-audit')
    assert.equal(failing.length, 0)
  })

  it('does not fail gate for Low or Medium unlinked findings', () => {
    const findings = [
      makeFinding({ severity: 'Low', status: 'Open', issues: [] }),
      makeFinding({ severity: 'Medium', status: 'Open', issues: [] }),
    ]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 0)
  })
})

// ---------------------------------------------------------------------------
// buildBoard
// ---------------------------------------------------------------------------

describe('buildBoard', () => {
  it('renders an all-clear banner when no unlinked findings', () => {
    const findings = [
      {
        slug: 'audit/finding-one',
        fileStem: 'audit',
        filePath: '/audits/audit.md',
        number: 1,
        title: 'Finding One',
        severity: 'High',
        status: 'Resolved',
        issues: [1],
        owner: '@alice',
      },
    ]
    const board = buildBoard(findings)
    assert.ok(board.includes('All clear'), `Expected 'All clear' in board:\n${board}`)
  })

  it('renders a warning when unlinked High finding exists', () => {
    const findings = [
      {
        slug: 'audit/finding-two',
        fileStem: 'audit',
        filePath: '/audits/audit.md',
        number: 2,
        title: 'Finding Two',
        severity: 'High',
        status: 'Open',
        issues: [],
        owner: '',
      },
    ]
    const board = buildBoard(findings)
    assert.ok(board.includes('unlinked'), `Expected 'unlinked' warning in board:\n${board}`)
  })

  it('sorts Critical before High', () => {
    const findings = [
      {
        slug: 'a/high',
        fileStem: 'a',
        filePath: '/audits/a.md',
        number: 1,
        title: 'High One',
        severity: 'High',
        status: 'Open',
        issues: [],
        owner: '',
      },
      {
        slug: 'a/crit',
        fileStem: 'a',
        filePath: '/audits/a.md',
        number: 2,
        title: 'Crit Two',
        severity: 'Critical',
        status: 'Open',
        issues: [],
        owner: '',
      },
    ]
    const board = buildBoard(findings)
    const critIdx = board.indexOf('Crit Two')
    const highIdx = board.indexOf('High One')
    assert.ok(critIdx < highIdx, 'Critical should appear before High in the board')
  })
})

// ---------------------------------------------------------------------------
// collectAllFindings (integration)
// ---------------------------------------------------------------------------

describe('collectAllFindings', () => {
  it('returns empty when audits dir is empty', () => {
    const dir = makeTmpDir()
    const findings = collectAllFindings(dir, resolve(dir, 'owners.yml'))
    assert.equal(findings.length, 0)
    rmSync(dir, { recursive: true })
  })

  it('parses multiple audit files', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W01-audit.md',
      `# Audit W01\n\n## Risk register\n\n### 1) Old issue\n\n- **Severity:** High\n- **Status:** Resolved\n`,
    )
    writeFile(
      dir,
      '2026-W02-audit.md',
      `# Audit W02\n\n## Risk register\n\n### 1) New issue\n\n- **Severity:** Critical\n- **Status:** Open\n`,
    )
    const findings = collectAllFindings(dir, resolve(dir, 'owners.yml'))
    assert.equal(findings.length, 2)
    const stems = findings.map((f) => f.fileStem)
    assert.ok(stems.includes('2026-W01-audit'))
    assert.ok(stems.includes('2026-W02-audit'))
    rmSync(dir, { recursive: true })
  })

  it('applies owner mappings from owners.yml', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W05-audit.md',
      `# Audit\n\n## Risk register\n\n### 1) Ownership issue\n\n- **Severity:** High\n- **Status:** Open\n`,
    )
    writeFile(
      dir,
      'owners.yml',
      `2026-W05-audit/ownership-issue: @charlie\n`,
    )
    const findings = collectAllFindings(dir, resolve(dir, 'owners.yml'))
    assert.equal(findings.length, 1)
    assert.equal(findings[0].owner, '@charlie')
    rmSync(dir, { recursive: true })
  })
})
