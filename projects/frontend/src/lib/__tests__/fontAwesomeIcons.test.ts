import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { describe, expect, it } from 'vitest'

import { frontendSolidIconNames } from '../fontAwesomeIcons'

function collectVueFiles(directory: string): string[] {
  const entries = readdirSync(directory, { withFileTypes: true })

  return entries.flatMap((entry) => {
    const fullPath = path.join(directory, entry.name)

    if (entry.isDirectory()) {
      if (entry.name === '__tests__') {
        return []
      }

      return collectVueFiles(fullPath)
    }

    return entry.isFile() && entry.name.endsWith('.vue') ? [fullPath] : []
  })
}

function extractSolidIconNames(source: string): string[] {
  const names = new Set<string>()
  const solidBlocks = source.match(/\['fas'[^\]]*\]/g) ?? []

  for (const block of solidBlocks) {
    for (const match of block.matchAll(/'([^']+)'/g)) {
      if (match[1] !== 'fas') {
        names.add(match[1])
      }
    }
  }

  return [...names]
}

describe('frontendSolidIcons', () => {
  it('registers every solid icon referenced by Vue source files', () => {
    const srcDirectory = fileURLToPath(new URL('../../', import.meta.url))
    const usedIconNames = new Set<string>()

    for (const vueFile of collectVueFiles(srcDirectory)) {
      const source = readFileSync(vueFile, 'utf8')
      for (const iconName of extractSolidIconNames(source)) {
        usedIconNames.add(iconName)
      }
    }

    const missingIcons = [...usedIconNames].filter((iconName) => !frontendSolidIconNames.includes(iconName)).sort()

    expect(missingIcons).toEqual([])
  })
})
