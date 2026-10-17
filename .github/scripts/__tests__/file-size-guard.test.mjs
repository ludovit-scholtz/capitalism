/**
 * Unit tests for .github/scripts/file-size-guard.mjs
 *
 * Run with:  node --test .github/scripts/__tests__/file-size-guard.test.mjs
 */

import { describe, it } from 'node:test'
import assert from 'node:assert/strict'

import { countLines, isCheckedSourceFile, evaluateFileSizes, tightenAllowList, LINE_LIMIT } from '../file-size-guard.mjs'

describe('countLines', () => {
  it('counts newline-terminated lines like wc -l and ignores a trailing newline', () => {
    assert.equal(countLines(''), 0)
    assert.equal(countLines('a'), 1)
    assert.equal(countLines('a\n'), 1)
    assert.equal(countLines('a\nb\n'), 2)
    assert.equal(countLines('a\r\nb\r\n'), 2)
    assert.equal(countLines('a\n\nb'), 3)
  })
})

describe('isCheckedSourceFile', () => {
  it('checks hand-written cs, ts, vue and dart sources', () => {
    for (const p of ['projects/Api/Types/Query.cs', 'projects/frontend/src/views/A.vue', 'projects/frontend/src/lib/a.ts', 'projects/flutter_app/lib/main.dart']) {
      assert.equal(isCheckedSourceFile(p), true, p)
    }
  })

  it('skips migrations, generated code, locales, build output and other file types', () => {
    for (const p of [
      'projects/Api/Data/Migrations/20260101_Add.cs',
      'projects/Api/Data/Migrations/20260101_Add.Designer.cs',
      'projects/Api/Data/Migrations/AppDbContextModelSnapshot.cs',
      'projects/flutter_app/lib/l10n/app_localizations.g.dart',
      'projects/frontend/src/i18n/locales/en.ts',
      'projects/master-frontend/src/i18n/locales/de.ts',
      'projects/frontend/dist/assets/a.ts',
      'projects/Api/bin/Debug/x.cs',
      'projects/Api/obj/x.cs',
      'projects/frontend/node_modules/x/index.ts',
      'package-lock.json',
      'README.md',
      'projects/frontend/src/foo.json',
    ]) {
      assert.equal(isCheckedSourceFile(p), false, p)
    }
  })
})

describe('evaluateFileSizes', () => {
  const limit = LINE_LIMIT

  it('uses a 500 line limit', () => {
    assert.equal(limit, 500)
  })

  it('passes files at or under the limit', () => {
    const result = evaluateFileSizes([{ path: 'a.cs', lines: 500 }, { path: 'b.cs', lines: 12 }], {})
    assert.deepEqual(result.violations, [])
  })

  it('fails a new file that exceeds the limit', () => {
    const result = evaluateFileSizes([{ path: 'new.cs', lines: 501 }], {})
    assert.equal(result.violations.length, 1)
    assert.equal(result.violations[0].kind, 'NEW_OVER_LIMIT')
    assert.equal(result.violations[0].path, 'new.cs')
  })

  it('passes an allow-listed file that has not grown', () => {
    const result = evaluateFileSizes([{ path: 'big.cs', lines: 900 }], { 'big.cs': 900 })
    assert.deepEqual(result.violations, [])
  })

  it('fails an allow-listed file that grew', () => {
    const result = evaluateFileSizes([{ path: 'big.cs', lines: 901 }], { 'big.cs': 900 })
    assert.equal(result.violations.length, 1)
    assert.equal(result.violations[0].kind, 'GREW')
  })

  it('fails an allow-listed file that shrank until its entry is tightened, so the list is a real ratchet', () => {
    const result = evaluateFileSizes([{ path: 'big.cs', lines: 700 }], { 'big.cs': 900 })
    assert.equal(result.violations.length, 1)
    assert.equal(result.violations[0].kind, 'SHRUNK_TIGHTEN_ALLOWLIST')
    assert.match(result.violations[0].message, /--update/)
  })

  it('tells the author how to handle a renamed oversized file', () => {
    const result = evaluateFileSizes([{ path: 'renamed/Big.cs', lines: 900 }], { 'old/Big.cs': 900 })
    assert.equal(result.violations[0].kind, 'NEW_OVER_LIMIT')
    assert.match(result.violations[0].message, /renam/i)
    assert.deepEqual(result.missing, ['old/Big.cs'])
  })

  it('fails an allow-listed file that is now under the limit so the entry gets removed', () => {
    const result = evaluateFileSizes([{ path: 'was-big.cs', lines: 480 }], { 'was-big.cs': 900 })
    assert.equal(result.violations.length, 1)
    assert.equal(result.violations[0].kind, 'STALE_ALLOWLIST_ENTRY')
  })

  it('reports allow-listed files that no longer exist as removable without failing', () => {
    const result = evaluateFileSizes([], { 'deleted.cs': 900 })
    assert.deepEqual(result.violations, [])
    assert.deepEqual(result.missing, ['deleted.cs'])
  })
})

describe('allow-list ordering', () => {
  it('sorts by code point so the file is identical on every machine', () => {
    const next = tightenAllowList(
      [
        { path: 'projects/Api.Tests/B.cs', lines: 600 },
        { path: 'projects/Api/A.cs', lines: 600 },
        { path: 'projects/api/z.cs', lines: 600 },
      ],
      { 'projects/Api.Tests/B.cs': 600, 'projects/Api/A.cs': 600, 'projects/api/z.cs': 600 },
    )
    assert.deepEqual(Object.keys(next), ['projects/Api.Tests/B.cs', 'projects/Api/A.cs', 'projects/api/z.cs'])
  })
})

describe('tightenAllowList', () => {
  it('lowers shrunk entries, drops removed and under-limit entries, and never adds new ones', () => {
    const next = tightenAllowList(
      [
        { path: 'shrunk.cs', lines: 700 },
        { path: 'same.cs', lines: 800 },
        { path: 'small-now.cs', lines: 450 },
        { path: 'brand-new-big.cs', lines: 900 },
      ],
      { 'shrunk.cs': 900, 'same.cs': 800, 'small-now.cs': 900, 'gone.cs': 600 },
    )
    assert.deepEqual(next, { 'same.cs': 800, 'shrunk.cs': 700 })
  })
})
