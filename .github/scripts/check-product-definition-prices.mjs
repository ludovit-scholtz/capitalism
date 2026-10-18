#!/usr/bin/env node
// Fails when facts that PRODUCT-DEFINITION.md states about money and the tutorial drift from the code:
//  - the gold prices of Pro and the Startup pack (projects/MasterApi/Types/Mutation.cs), each tied to its own context line,
//  - stale price wording ($10/month, "in real money", Startup pack "in-game currency"),
//  - the number of tutorial milestones (projects/Shared/Tutorial/TutorialMilestoneCodes.cs).
// Usage: node .github/scripts/check-product-definition-prices.mjs [--root <repo root>]
import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const PRICE_CONSTANTS = { pro: 'MonthlyProPriceGold', startup: 'StartupPackPriceGold' }
const NUMBER_WORDS = { one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8, nine: 9, ten: 10, eleven: 11, twelve: 12 }

/** Extracts `decimal NAME = 1.23m;` constants from the C# source. Returns { NAME: '1.23' }. */
export function extractCodePrices(csharp) {
  const prices = {}
  for (const name of Object.values(PRICE_CONSTANTS)) {
    const match = new RegExp(`decimal\\s+${name}\\s*=\\s*([0-9]+(?:\\.[0-9]+)?)m\\s*;`).exec(csharp)
    if (match) prices[name] = match[1]
  }
  return prices
}

/** Gold amounts written next to a gram unit: `0.137` grams, 0.137 g, **0.137** grams, 0.137g. */
export function extractGramAmounts(text) {
  return [...text.matchAll(/[`*]*([0-9]+(?:\.[0-9]+)?)[`*]*\s*(?:grams?|g)\b/gi)].map((m) => m[1])
}

/** Number of `public const string` milestone codes in TutorialMilestoneCodes.cs. */
export function countTutorialMilestones(csharp) {
  return [...csharp.matchAll(/public\s+const\s+string\s+\w+\s*=/g)].length
}

/** Returns a list of human-readable problems; empty when the document and the code agree. */
export function findDrift(mutationCs, markdown, milestonesCs) {
  const problems = []
  const codePrices = extractCodePrices(mutationCs)
  for (const name of Object.values(PRICE_CONSTANTS)) {
    if (!codePrices[name]) problems.push(`Could not find constant ${name} in Mutation.cs (renamed? update this guard).`)
  }

  const lines = markdown.split('\n')
  const contexts = [
    { label: 'Monthly Pro', constant: PRICE_CONSTANTS.pro, matches: (l) => /monthly pro/i.test(l) },
    { label: 'Startup pack', constant: PRICE_CONSTANTS.startup, matches: (l) => /startup pack/i.test(l) },
  ]
  for (const { label, constant, matches } of contexts) {
    const expected = codePrices[constant]
    const stated = lines.filter(matches).flatMap(extractGramAmounts)
    if (!expected) continue
    if (stated.length === 0) problems.push(`${label} (${constant} = ${expected} g) has no gold amount in PRODUCT-DEFINITION.md.`)
    for (const amount of stated) {
      if (amount !== expected) problems.push(`${label} is documented as ${amount} g but ${constant} = ${expected} g in Mutation.cs.`)
    }
  }

  const known = new Set(Object.values(codePrices))
  // Whole-gram statements ("1 gram of real-world gold") define the token, they are not prices; only fractional amounts are.
  for (const amount of extractGramAmounts(markdown).filter((a) => a.includes('.'))) {
    if (known.size > 0 && !known.has(amount)) {
      problems.push(`PRODUCT-DEFINITION.md states ${amount} g, which is not a price in Mutation.cs (${[...known].join(', ')}).`)
    }
  }

  if (/\$\s*10\s*(?:\/|per|a)\s*month/i.test(markdown)) problems.push('PRODUCT-DEFINITION.md still contains the stale "$10/month" Pro price.')
  if (/in real money/i.test(markdown)) problems.push('PRODUCT-DEFINITION.md still prices something "in real money"; Pro and the Startup pack are paid in tokenized gold.')
  for (const line of lines) {
    if (/startup pack/i.test(line) && /in-game currency/i.test(line) && !/(does not|no|without)\b/i.test(line)) {
      problems.push(`The Startup pack must not be documented as granting in-game currency: "${line.trim().slice(0, 100)}"`)
    }
  }

  if (milestonesCs !== undefined) {
    const actual = countTutorialMilestones(milestonesCs)
    const stated = /records completion of (\w+) milestones/i.exec(markdown)
    const documented = stated ? (NUMBER_WORDS[stated[1].toLowerCase()] ?? Number(stated[1])) : undefined
    if (documented === undefined || Number.isNaN(documented)) {
      problems.push('PRODUCT-DEFINITION.md no longer states how many tutorial milestones TutorialProgress records.')
    } else if (documented !== actual) {
      problems.push(`PRODUCT-DEFINITION.md says ${documented} tutorial milestones but TutorialMilestoneCodes.cs defines ${actual}.`)
    }
  }
  return problems
}

function main() {
  const rootArg = process.argv.indexOf('--root')
  const root = resolve(rootArg >= 0 ? process.argv[rootArg + 1] : join(fileURLToPath(import.meta.url), '..', '..', '..'))
  const problems = findDrift(
    readFileSync(join(root, 'projects', 'MasterApi', 'Types', 'Mutation.cs'), 'utf8'),
    readFileSync(join(root, 'PRODUCT-DEFINITION.md'), 'utf8').replace(/\r\n/g, '\n'),
    readFileSync(join(root, 'projects', 'Shared', 'Tutorial', 'TutorialMilestoneCodes.cs'), 'utf8'),
  )
  if (problems.length > 0) {
    console.error('Product definition drift:')
    for (const problem of problems) console.error(`  - ${problem}`)
    process.exit(1)
  }
  console.log('Product definition prices and tutorial milestone count match the code.')
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main()
