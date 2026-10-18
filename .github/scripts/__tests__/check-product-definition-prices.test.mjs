import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { test } from 'node:test'
import { countTutorialMilestones, extractCodePrices, extractGramAmounts, findDrift } from '../check-product-definition-prices.mjs'

const CODE = `
    internal const decimal MonthlyProPriceGold = 0.137m;
    internal const decimal StartupPackPriceGold = 0.274m;
`
const MILESTONES = `
    public const string A = "A";
    public const string B = "B";
`
const DOC = [
  '- Monthly Pro costs `$20` equivalent, charged as `0.137` grams of tokenized gold.',
  '- The Startup pack costs `$40` equivalent, charged as `0.274` grams. It does not grant in-game currency.',
  '- The TutorialProgress entity records completion of two milestones.',
].join('\n')

test('extracts the price constants from C#', () => {
  assert.deepEqual(extractCodePrices(CODE), { MonthlyProPriceGold: '0.137', StartupPackPriceGold: '0.274' })
})

test('extracts gram amounts in several spellings', () => {
  assert.deepEqual(extractGramAmounts('`0.137` grams, 0.15 g, **0.2** grams, 0.3g'), ['0.137', '0.15', '0.2', '0.3'])
})

test('counts milestone constants', () => {
  assert.equal(countTutorialMilestones(MILESTONES), 2)
})

test('no drift when document and code agree', () => {
  assert.deepEqual(findDrift(CODE, DOC, MILESTONES), [])
})

test('ties each price to its own line: swapping the two prices is caught', () => {
  const swapped = DOC.replace('`0.274` grams', '`0.137` grams')
  const problems = findDrift(CODE, swapped, MILESTONES)
  assert.ok(problems.some((p) => p.includes('Startup pack is documented as 0.137 g')))
})

test('catches a stale price in any spelling', () => {
  assert.ok(findDrift(CODE, DOC.replace('`0.137` grams', '0.15 g'), MILESTONES).some((p) => p.includes('Monthly Pro is documented as 0.15 g')))
  assert.ok(findDrift(CODE, DOC.replace('`0.274` grams', '**0.5** grams'), MILESTONES).some((p) => p.includes('0.5 g')))
})

test('reports a missing price in the document', () => {
  assert.ok(findDrift(CODE, DOC.replace('`0.137` grams of tokenized gold', 'gold'), MILESTONES).some((p) => p.includes('has no gold amount')))
})

test('reports a renamed constant instead of passing silently', () => {
  assert.ok(findDrift(CODE.replace('MonthlyProPriceGold', 'ProPriceGold'), DOC, MILESTONES).some((p) => p.includes('Could not find constant MonthlyProPriceGold')))
})

test('rejects stale wording', () => {
  assert.ok(findDrift(CODE, `${DOC}\nPro will cost $10/month.`, MILESTONES).some((p) => p.includes('$10/month')))
  assert.ok(findDrift(CODE, `${DOC}\nPro costs $10 per month.`, MILESTONES).some((p) => p.includes('$10/month')))
  assert.ok(findDrift(CODE, `${DOC}\nIt costs $20 in real money.`, MILESTONES).some((p) => p.includes('real money')))
  assert.ok(findDrift(CODE, `${DOC}\nThe Startup pack includes in-game currency.`, MILESTONES).some((p) => p.includes('in-game currency')))
})

test('catches a stale tutorial milestone count', () => {
  const more = `${MILESTONES}\n    public const string C = "C";`
  assert.ok(findDrift(CODE, DOC, more).some((p) => p.includes('says 2 tutorial milestones but TutorialMilestoneCodes.cs defines 3')))
  assert.ok(findDrift(CODE, DOC.replace('records completion of two milestones', 'records things'), MILESTONES).some((p) => p.includes('no longer states')))
})

test('the real document matches the real code', () => {
  const root = join(fileURLToPath(import.meta.url), '..', '..', '..', '..')
  const read = (...p) => readFileSync(join(root, ...p), 'utf8')
  const markdown = read('PRODUCT-DEFINITION.md').replace(/\r\n/g, '\n')
  assert.deepEqual(findDrift(read('projects', 'MasterApi', 'Types', 'Mutation.cs'), markdown, read('projects', 'Shared', 'Tutorial', 'TutorialMilestoneCodes.cs')), [])
})
