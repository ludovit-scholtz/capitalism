#!/usr/bin/env node
// Fails when the gold prices written in PRODUCT-DEFINITION.md drift from the constants in projects/MasterApi/Types/Mutation.cs.
// Usage: node .github/scripts/check-product-definition-prices.mjs [--root <repo root>]
import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const PRICE_CONSTANTS = ['MonthlyProPriceGold', 'StartupPackPriceGold']

/** Extracts `decimal NAME = 1.23m;` constants from the C# source. Returns { NAME: '1.23' }. */
export function extractCodePrices(csharp) {
  const prices = {}
  for (const name of PRICE_CONSTANTS) {
    const match = new RegExp(`decimal\\s+${name}\\s*=\\s*([0-9]+(?:\\.[0-9]+)?)m\\s*;`).exec(csharp)
    if (match) prices[name] = match[1]
  }
  return prices
}

/** Every gold amount the document states as "`<amount>` grams". */
export function extractDocumentedGramAmounts(markdown) {
  return [...markdown.matchAll(/`([0-9]+(?:\.[0-9]+)?)`\s+grams?/g)].map((m) => m[1])
}

/** Returns a list of human-readable problems; empty when the document and the code agree. */
export function findPriceDrift(csharp, markdown) {
  const problems = []
  const codePrices = extractCodePrices(csharp)
  for (const name of PRICE_CONSTANTS) {
    if (!codePrices[name]) problems.push(`Could not find constant ${name} in Mutation.cs (renamed? update this guard).`)
  }

  const known = new Set(Object.values(codePrices))
  const documented = extractDocumentedGramAmounts(markdown)
  for (const amount of documented) {
    if (!known.has(amount)) {
      problems.push(`PRODUCT-DEFINITION.md states ${amount} grams, which is not a price in Mutation.cs (${[...known].join(', ')}).`)
    }
  }
  for (const [name, value] of Object.entries(codePrices)) {
    if (!documented.includes(value)) {
      problems.push(`${name} = ${value} g is not mentioned in PRODUCT-DEFINITION.md.`)
    }
  }
  if (/\$10\s*\/\s*month/i.test(markdown)) {
    problems.push('PRODUCT-DEFINITION.md still contains the stale "$10/month" Pro price.')
  }
  return problems
}

function main() {
  const rootArg = process.argv.indexOf('--root')
  const root = resolve(rootArg >= 0 ? process.argv[rootArg + 1] : join(fileURLToPath(import.meta.url), '..', '..', '..'))
  const csharp = readFileSync(join(root, 'projects', 'MasterApi', 'Types', 'Mutation.cs'), 'utf8')
  const markdown = readFileSync(join(root, 'PRODUCT-DEFINITION.md'), 'utf8')
  const problems = findPriceDrift(csharp, markdown)
  if (problems.length > 0) {
    console.error('Product definition price drift:')
    for (const problem of problems) console.error(`  - ${problem}`)
    process.exit(1)
  }
  console.log('Product definition prices match Mutation.cs.')
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main()
