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

  it('ignores non-.md files in the audits directory', () => {
    const dir = makeTmpDir()
    writeFile(dir, 'owners.yml', '# no findings here\n')
    writeFile(dir, 'README.txt', 'This is not a markdown audit file\n')
    writeFile(
      dir,
      '2026-W06-audit.md',
      `# Audit W06\n\n## Risk register\n\n### 1) Real finding\n\n- **Severity:** High\n- **Status:** Open\n`,
    )
    const findings = collectAllFindings(dir, resolve(dir, 'owners.yml'))
    assert.equal(findings.length, 1)
    assert.equal(findings[0].fileStem, '2026-W06-audit')
    rmSync(dir, { recursive: true })
  })

  it('gate checks only latest file when multiple audits exist', () => {
    const dir = makeTmpDir()
    // Older audit with unlinked High finding — gate should ignore it
    writeFile(
      dir,
      '2026-W01-audit.md',
      `# Old\n\n## Risk register\n\n### 1) Old finding\n\n- **Severity:** High\n- **Status:** Open\n`,
    )
    // Latest audit with all findings linked
    writeFile(
      dir,
      '2026-W20-audit.md',
      `# New\n\n## Risk register\n\n### 1) New finding\n\n- **Severity:** Critical\n- **Status:** Open <!-- issue: #99 -->\n`,
    )
    const findings = collectAllFindings(dir, resolve(dir, 'owners.yml'))
    const { failing, latestStem } = runGateCheck(findings)
    assert.equal(latestStem, '2026-W20-audit')
    assert.equal(failing.length, 0, 'Gate should pass because latest audit has all findings linked')
    rmSync(dir, { recursive: true })
  })
})

// ---------------------------------------------------------------------------
// parseAuditFile — inline issue annotation on Status line
// ---------------------------------------------------------------------------

describe('parseAuditFile - inline issue annotation on Status line', () => {
  it('strips HTML comment from status text but keeps issue ref', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W10-audit.md',
      `# Audit\n\n## Risk register\n\n### 1) Race condition\n\n- **Severity:** High\n- **Status:** In-Progress <!-- issue: #389 -->\n`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W10-audit.md'))
    assert.equal(findings.length, 1)
    assert.equal(findings[0].status, 'In-Progress')
    assert.deepEqual(findings[0].issues, [389])
    rmSync(dir, { recursive: true })
  })

  it('strips HTML comment from Open status text but keeps issue ref', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W11-audit.md',
      `# Audit\n\n## Risk register\n\n### 1) Token boundary\n\n- **Severity:** Critical\n- **Status:** Open <!-- issue: #313 -->\n`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W11-audit.md'))
    assert.equal(findings.length, 1)
    assert.equal(findings[0].status, 'Open', 'Status should be cleaned of HTML comment')
    assert.deepEqual(findings[0].issues, [313])
    rmSync(dir, { recursive: true })
  })

  it('In-Progress finding with issue ref passes gate', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W12-audit.md',
      `# Audit\n\n## Risk register\n\n### 1) Race condition\n\n- **Severity:** High\n- **Status:** In-Progress <!-- issue: #389 -->\n`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W12-audit.md'))
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 0, 'In-Progress + linked issue should pass gate')
    rmSync(dir, { recursive: true })
  })

  it('In-Progress finding WITHOUT issue ref fails gate', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W13-audit.md',
      `# Audit\n\n## Risk register\n\n### 1) Race condition\n\n- **Severity:** High\n- **Status:** In-Progress\n`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W13-audit.md'))
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 1, 'In-Progress without linked issue should still fail gate')
    rmSync(dir, { recursive: true })
  })

  it('extracts multiple issues from single finding', () => {
    const dir = makeTmpDir()
    writeFile(
      dir,
      '2026-W14-audit.md',
      `# Audit\n\n## Risk register\n\n### 1) Complex finding\n\n- **Severity:** High\n- **Status:** Open <!-- issues: #10, #20 -->\n`,
    )
    const findings = parseAuditFile(resolve(dir, '2026-W14-audit.md'))
    assert.equal(findings.length, 1)
    assert.deepEqual(findings[0].issues, [10, 20])
    rmSync(dir, { recursive: true })
  })
})

// ---------------------------------------------------------------------------
// runGateCheck — owner presence does not bypass issue requirement
// ---------------------------------------------------------------------------

describe('runGateCheck - owner does not bypass issue requirement', () => {
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

  it('fails gate for Open finding with owner but no issue', () => {
    const findings = [makeFinding({ owner: '@alice', issues: [] })]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 1, 'Owner alone is not enough to pass gate — issue link is required')
  })

  it('passes gate for Open finding with owner AND linked issue', () => {
    const findings = [makeFinding({ owner: '@alice', issues: [42] })]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 0, 'Owner + linked issue should pass gate')
  })

  it('passes gate for Resolved finding with no owner and no issue', () => {
    const findings = [makeFinding({ status: 'Resolved', owner: '', issues: [] })]
    const { failing } = runGateCheck(findings)
    assert.equal(failing.length, 0, 'Resolved findings always pass regardless of owner/issue')
  })
})

// ---------------------------------------------------------------------------
// buildBoard — issue links in generated Markdown
// ---------------------------------------------------------------------------

describe('buildBoard - issue links', () => {
  it('includes issue hyperlinks for linked findings', () => {
    const findings = [
      {
        slug: 'audit/linked-finding',
        fileStem: 'audit',
        filePath: '/audits/audit.md',
        number: 1,
        title: 'Linked Finding',
        severity: 'High',
        status: 'Open',
        issues: [42, 99],
        owner: '@alice',
      },
    ]
    const board = buildBoard(findings)
    assert.ok(board.includes('[#42](https://github.com/ludovit-scholtz/capitalism/issues/42)'), 'Board should contain hyperlink for issue 42')
    assert.ok(board.includes('[#99](https://github.com/ludovit-scholtz/capitalism/issues/99)'), 'Board should contain hyperlink for issue 99')
  })

  it('renders em-dash for finding with no issues', () => {
    const findings = [
      {
        slug: 'audit/unlinked',
        fileStem: 'audit',
        filePath: '/audits/audit.md',
        number: 1,
        title: 'Unlinked',
        severity: 'High',
        status: 'Open',
        issues: [],
        owner: '',
      },
    ]
    const board = buildBoard(findings)
    // The em-dash placeholder should appear in the issues column
    assert.ok(board.includes('| — |') || board.includes('|—|') || board.match(/\|\s*—\s*\|/), 'Board should render em-dash for missing issues')
  })

  it('renders all-clear banner when all findings are resolved or linked', () => {
    const findings = [
      {
        slug: 'audit/resolved',
        fileStem: 'audit',
        filePath: '/audits/audit.md',
        number: 1,
        title: 'Resolved Finding',
        severity: 'High',
        status: 'Resolved',
        issues: [10],
        owner: '@bob',
      },
    ]
    const board = buildBoard(findings)
    assert.ok(board.includes('All clear'), 'Board should show All clear banner')
    assert.ok(!board.includes('require linked implementation issues'), 'Board should not show unlinked warning')
  })

  it('sorts Medium after High in board output', () => {
    const findings = [
      {
        slug: 'a/medium',
        fileStem: 'a',
        filePath: '/audits/a.md',
        number: 2,
        title: 'Medium Issue',
        severity: 'Medium',
        status: 'Open',
        issues: [],
        owner: '',
      },
      {
        slug: 'a/high',
        fileStem: 'a',
        filePath: '/audits/a.md',
        number: 1,
        title: 'High Issue',
        severity: 'High',
        status: 'Open',
        issues: [5],
        owner: '',
      },
    ]
    const board = buildBoard(findings)
    const highIdx = board.indexOf('High Issue')
    const medIdx = board.indexOf('Medium Issue')
    assert.ok(highIdx < medIdx, 'High should appear before Medium')
  })
})

// ---------------------------------------------------------------------------
// loadOwners — edge cases
// ---------------------------------------------------------------------------

describe('loadOwners - edge cases', () => {
  it('handles value with colon in it', () => {
    const dir = makeTmpDir()
    writeFile(dir, 'owners.yml', `2026-W01-audit/some-finding: @user:alias\n`)
    const owners = loadOwners(resolve(dir, 'owners.yml'))
    // First colon splits key/value; the rest of the value (including colon) stays
    assert.equal(owners['2026-W01-audit/some-finding'], '@user:alias')
    rmSync(dir, { recursive: true })
  })

  it('returns empty for file with only comments', () => {
    const dir = makeTmpDir()
    writeFile(dir, 'owners.yml', `# comment 1\n# comment 2\n`)
    const owners = loadOwners(resolve(dir, 'owners.yml'))
    assert.equal(Object.keys(owners).length, 0)
    rmSync(dir, { recursive: true })
  })
})
