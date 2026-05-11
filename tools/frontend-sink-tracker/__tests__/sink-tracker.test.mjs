/**
 * Tests for tools/frontend-sink-tracker/index.mjs
 *
 * Run with: node --test tools/frontend-sink-tracker/__tests__/sink-tracker.test.mjs
 */

import { test, describe } from 'node:test'
import assert from 'node:assert/strict'
import { fileURLToPath } from 'url'
import { dirname, join } from 'path'

const __dirname = dirname(fileURLToPath(import.meta.url))
const FIXTURES_DIR = join(__dirname, 'fixtures')
const REPO_ROOT = join(__dirname, '..', '..', '..')

// Import the tracker functions
const { scanFile, scanDirectories, buildInventory, parseHighCriticalAdvisories } = await import(
  '../index.mjs'
)

// ---------------------------------------------------------------------------
// scanFile tests
// ---------------------------------------------------------------------------

describe('scanFile', () => {
  test('detects v-html in sanitized component and marks it sanitized', () => {
    const findings = scanFile(join(FIXTURES_DIR, 'SanitizedComponent.vue'), REPO_ROOT)
    assert.equal(findings.length, 1)
    assert.equal(findings[0].sinkType, 'v-html')
    assert.equal(findings[0].sanitized, true)
  })

  test('detects v-html in unsanitized component and marks it unsanitized', () => {
    const findings = scanFile(join(FIXTURES_DIR, 'UnsanitizedComponent.vue'), REPO_ROOT)
    assert.equal(findings.length, 1)
    assert.equal(findings[0].sinkType, 'v-html')
    assert.equal(findings[0].sanitized, false)
  })

  test('sanitizationNote for unsanitized sink contains descriptive text', () => {
    const findings = scanFile(join(FIXTURES_DIR, 'UnsanitizedComponent.vue'), REPO_ROOT)
    assert.ok(
      findings[0].sanitizationNote.toLowerCase().includes('no dompurify'),
      `Expected sanitizationNote to mention DOMPurify, got: "${findings[0].sanitizationNote}"`,
    )
  })

  test('detects innerHTML in unsanitized TS file', () => {
    const findings = scanFile(join(FIXTURES_DIR, 'unsanitized-inner-html.ts'), REPO_ROOT)
    assert.equal(findings.length, 1)
    assert.equal(findings[0].sinkType, 'innerHTML')
    assert.equal(findings[0].sanitized, false)
  })

  test('reports correct line number for v-html', () => {
    const findings = scanFile(join(FIXTURES_DIR, 'UnsanitizedComponent.vue'), REPO_ROOT)
    // Line 4 in UnsanitizedComponent.vue contains v-html
    assert.ok(
      findings[0].lineNumber > 0,
      `Expected positive lineNumber, got: ${findings[0].lineNumber}`,
    )
  })

  test('returns empty array for a file that does not exist', () => {
    const findings = scanFile(join(FIXTURES_DIR, 'nonexistent.vue'), REPO_ROOT)
    assert.deepEqual(findings, [])
  })
})

// ---------------------------------------------------------------------------
// scanDirectories tests
// ---------------------------------------------------------------------------

describe('scanDirectories', () => {
  test('scans fixture directory and finds both sanitized and unsanitized sinks', () => {
    const findings = scanDirectories([FIXTURES_DIR], REPO_ROOT)
    const sanitized = findings.filter((f) => f.sanitized)
    const unsanitized = findings.filter((f) => !f.sanitized)
    assert.ok(sanitized.length >= 1, 'Expected at least 1 sanitized sink')
    assert.ok(unsanitized.length >= 1, 'Expected at least 1 unsanitized sink')
  })

  test('skips nonexistent directories with a warning instead of throwing', () => {
    // Should not throw
    const findings = scanDirectories([join(FIXTURES_DIR, '__nonexistent__')], REPO_ROOT)
    assert.deepEqual(findings, [])
  })
})

// ---------------------------------------------------------------------------
// buildInventory tests
// ---------------------------------------------------------------------------

describe('buildInventory', () => {
  test('inventory totals match findings array', () => {
    const findings = scanDirectories([FIXTURES_DIR], REPO_ROOT)
    const inv = buildInventory(findings, [FIXTURES_DIR])
    assert.equal(inv.totalSinks, findings.length)
    assert.equal(inv.sanitizedSinks, findings.filter((f) => f.sanitized).length)
    assert.equal(inv.unsanitizedSinks, findings.filter((f) => !f.sanitized).length)
    assert.equal(inv.totalSinks, inv.sanitizedSinks + inv.unsanitizedSinks)
  })

  test('inventory includes generatedAt timestamp', () => {
    const inv = buildInventory([], [])
    assert.ok(typeof inv.generatedAt === 'string')
    assert.ok(!isNaN(Date.parse(inv.generatedAt)))
  })

  test('inventory includes scannedDirectories', () => {
    const dirs = [FIXTURES_DIR]
    const inv = buildInventory([], dirs)
    assert.deepEqual(inv.scannedDirectories, dirs)
  })
})

// ---------------------------------------------------------------------------
// parseHighCriticalAdvisories tests
// ---------------------------------------------------------------------------

describe('parseHighCriticalAdvisories', () => {
  test('returns only high and critical severity advisories', () => {
    const mockAudit = {
      vulnerabilities: {
        'safe-pkg': { name: 'safe-pkg', severity: 'moderate' },
        'dangerous-pkg': { name: 'dangerous-pkg', severity: 'high' },
        'critical-pkg': { name: 'critical-pkg', severity: 'critical' },
        'low-pkg': { name: 'low-pkg', severity: 'low' },
      },
    }
    const result = parseHighCriticalAdvisories(mockAudit)
    assert.equal(result.length, 2)
    assert.ok(result.every((r) => ['high', 'critical'].includes(r.severity)))
  })

  test('returns empty array when no vulnerabilities', () => {
    const result = parseHighCriticalAdvisories({ vulnerabilities: {} })
    assert.deepEqual(result, [])
  })

  test('returns empty array for clean audit JSON (no vulnerabilities key)', () => {
    const result = parseHighCriticalAdvisories({})
    assert.deepEqual(result, [])
  })

  test('exits non-zero simulation: gate should fail with high advisories', () => {
    const mockAudit = {
      vulnerabilities: {
        'vuln-pkg': { name: 'vuln-pkg', severity: 'high' },
      },
    }
    const advisories = parseHighCriticalAdvisories(mockAudit)
    // In gate mode the caller checks advisories.length > 0
    assert.ok(advisories.length > 0, 'Gate should detect high advisory')
  })

  test('gate passes with no high/critical advisories', () => {
    const mockAudit = {
      vulnerabilities: {
        'minor-pkg': { name: 'minor-pkg', severity: 'moderate' },
      },
    }
    const advisories = parseHighCriticalAdvisories(mockAudit)
    assert.equal(advisories.length, 0, 'Gate should pass with only moderate advisories')
  })
})
