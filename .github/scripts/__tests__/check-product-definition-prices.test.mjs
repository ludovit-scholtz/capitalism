import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { test } from 'node:test'
import { extractCodePrices, extractDocumentedGramAmounts, findPriceDrift } from '../check-product-definition-prices.mjs'

const CODE = `
    internal const decimal MonthlyProPriceGold = 0.137m;
    internal const decimal StartupPackPriceGold = 0.274m;
`
const DOC = 'Monthly Pro is charged as `0.137` grams of tokenized gold. Startup pack: `0.274` grams.'

test('extracts the price constants from C#', () => {
  assert.deepEqual(extractCodePrices(CODE), { MonthlyProPriceGold: '0.137', StartupPackPriceGold: '0.274' })
})

test('extracts documented gram amounts', () => {
  assert.deepEqual(extractDocumentedGramAmounts(DOC), ['0.137', '0.274'])
})

test('no drift when document and code agree', () => {
  assert.deepEqual(findPriceDrift(CODE, DOC), [])
})

test('reports a document price that is not in the code', () => {
  const problems = findPriceDrift(CODE, DOC.replace('0.274', '0.5'))
  assert.ok(problems.some((p) => p.includes('0.5 grams')))
  assert.ok(problems.some((p) => p.includes('StartupPackPriceGold')))
})

test('reports a code price missing from the document', () => {
  const problems = findPriceDrift(CODE.replace('0.137m', '0.15m'), DOC)
  assert.ok(problems.some((p) => p.includes('0.137 grams')))
  assert.ok(problems.some((p) => p.includes('MonthlyProPriceGold = 0.15')))
})

test('reports a renamed constant instead of passing silently', () => {
  const problems = findPriceDrift(CODE.replace('MonthlyProPriceGold', 'ProPriceGold'), DOC)
  assert.ok(problems.some((p) => p.includes('Could not find constant MonthlyProPriceGold')))
})

test('rejects the stale $10/month price', () => {
  assert.ok(findPriceDrift(CODE, `${DOC} Pro will cost $10/month.`).some((p) => p.includes('$10/month')))
})

test('the real document matches the real code', () => {
  const root = join(fileURLToPath(import.meta.url), '..', '..', '..', '..')
  const csharp = readFileSync(join(root, 'projects', 'MasterApi', 'Types', 'Mutation.cs'), 'utf8')
  const markdown = readFileSync(join(root, 'PRODUCT-DEFINITION.md'), 'utf8')
  assert.deepEqual(findPriceDrift(csharp, markdown), [])
})
