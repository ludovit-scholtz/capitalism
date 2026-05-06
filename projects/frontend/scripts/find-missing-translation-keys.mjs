import { readFile } from 'node:fs/promises'
import path from 'node:path'
import vm from 'node:vm'
import { createRequire } from 'node:module'
import ts from 'typescript'

const localeDir = path.resolve(process.cwd(), 'src/i18n/locales')
const localeFiles = ['en.ts', 'sk.ts', 'de.ts']
const cjsRequire = createRequire(import.meta.url)
const allowedSharedValues = new Set(['Capitalism 5', 'English', 'Slovenčina', 'Deutsch', 'N/A', 'Forex', 'Chat', 'Pro', 'Cloud', 'MW', 'XAU'])
const ignoredUntranslatedKeys = new Set(['admin.globalAdminPlaceholder', 'buildingDetail.sourcingComparison.distanceKm'])

function isPlainObject(value) {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value)
}

function flattenKeys(source, prefix = '', output = new Map()) {
  if (!isPlainObject(source)) {
    return output
  }

  for (const [key, value] of Object.entries(source)) {
    const keyPath = prefix ? `${prefix}.${key}` : key
    if (isPlainObject(value)) {
      flattenKeys(value, keyPath, output)
      continue
    }

    output.set(keyPath, value)
  }

  return output
}

async function loadLocaleObject(filePath) {
  const source = await readFile(filePath, 'utf8')
  const transpiled = ts.transpileModule(source, {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2020,
    },
    fileName: filePath,
    reportDiagnostics: true,
  })

  const diagnostics = transpiled.diagnostics ?? []
  if (diagnostics.length > 0) {
    const errors = diagnostics.map((diag) => {
      const message = ts.flattenDiagnosticMessageText(diag.messageText, '\n')
      return `- ${message}`
    })
    throw new Error(`Failed to transpile ${path.basename(filePath)}:\n${errors.join('\n')}`)
  }

  const module = { exports: {} }
  const context = vm.createContext({
    module,
    exports: module.exports,
    require: cjsRequire,
  })
  const script = new vm.Script(transpiled.outputText, { filename: filePath })
  script.runInContext(context)

  if (!module.exports || !module.exports.default || !isPlainObject(module.exports.default)) {
    throw new Error(`Locale ${path.basename(filePath)} does not export a default object.`)
  }

  return module.exports.default
}

function printMissing(localeName, missingKeys) {
  if (missingKeys.length === 0) {
    console.log(`- ${localeName}: no missing keys`)
    return
  }

  console.log(`- ${localeName}: ${missingKeys.length} missing key(s)`)
  for (const key of missingKeys) {
    console.log(`  - ${key}`)
  }
}

function printShapeMismatches(mismatches) {
  if (mismatches.length === 0) {
    return
  }

  console.log('\nShape mismatches detected:')
  for (const item of mismatches) {
    console.log(`- ${item.locale}: ${item.key} is ${item.actual}, expected ${item.expected}`)
  }
}

function isLikelyTranslatableEnglishValue(value) {
  if (typeof value !== 'string') {
    return false
  }

  const trimmed = value.trim()
  if (trimmed.length < 2) {
    return false
  }

  if (allowedSharedValues.has(trimmed)) {
    return false
  }

  const withoutPlaceholders = trimmed.replace(/\{[^}]+\}/g, '').trim()

  // Skip placeholders and mostly symbolic strings.
  if (/^[\d\s{}@._:/\-+%|(),#→↔–]+$/.test(withoutPlaceholders)) {
    return false
  }

  // Skip short units/tickers.
  if (/^[A-Z]{2,5}$/.test(withoutPlaceholders)) {
    return false
  }

  // Require at least one ASCII letter to avoid flagging language-independent values.
  return /[A-Za-z]/.test(withoutPlaceholders)
}

function printUntranslated(untranslatedByLocale) {
  console.log('\nLikely untranslated values (same as English):')
  for (const [localeName, items] of untranslatedByLocale.entries()) {
    if (items.length === 0) {
      console.log(`- ${localeName}: none`)
      continue
    }

    console.log(`- ${localeName}: ${items.length} key(s)`)
    for (const item of items) {
      console.log(`  - ${item.key}: "${item.value}"`)
    }
  }
}

async function main() {
  const localeEntries = await Promise.all(
    localeFiles.map(async (fileName) => {
      const filePath = path.join(localeDir, fileName)
      const locale = await loadLocaleObject(filePath)
      const flat = flattenKeys(locale)
      return {
        fileName,
        locale,
        flat,
      }
    }),
  )

  const allKeys = new Set()
  for (const entry of localeEntries) {
    for (const key of entry.flat.keys()) {
      allKeys.add(key)
    }
  }

  const shapeMismatches = []
  const missingByLocale = new Map()
  const untranslatedByLocale = new Map()

  const englishEntry = localeEntries.find((entry) => entry.fileName === 'en.ts')
  if (!englishEntry) {
    throw new Error('en.ts locale file is required for untranslated-value checks.')
  }

  for (const entry of localeEntries) {
    const missing = []
    for (const key of allKeys) {
      if (!entry.flat.has(key)) {
        missing.push(key)
      }
    }
    missingByLocale.set(entry.fileName, missing.sort())
  }

  for (const key of allKeys) {
    const shapeByLocale = localeEntries.map((entry) => {
      const segments = key.split('.')
      let cursor = entry.locale
      for (const segment of segments) {
        if (!isPlainObject(cursor) || !(segment in cursor)) {
          return { locale: entry.fileName, shape: 'missing' }
        }
        cursor = cursor[segment]
      }
      return { locale: entry.fileName, shape: isPlainObject(cursor) ? 'object' : 'value' }
    })

    const expected = shapeByLocale.find((item) => item.shape !== 'missing')?.shape
    if (!expected) {
      continue
    }

    for (const item of shapeByLocale) {
      if (item.shape !== 'missing' && item.shape !== expected) {
        shapeMismatches.push({
          locale: item.locale,
          key,
          actual: item.shape,
          expected,
        })
      }
    }
  }

  for (const entry of localeEntries) {
    if (entry.fileName === 'en.ts') {
      untranslatedByLocale.set(entry.fileName, [])
      continue
    }

    const untranslated = []
    for (const [key, englishValue] of englishEntry.flat.entries()) {
      if (ignoredUntranslatedKeys.has(key)) {
        continue
      }

      const localValue = entry.flat.get(key)
      if (typeof englishValue !== 'string' || typeof localValue !== 'string') {
        continue
      }

      if (englishValue === localValue && isLikelyTranslatableEnglishValue(englishValue)) {
        untranslated.push({ key, value: englishValue })
      }
    }

    untranslatedByLocale.set(entry.fileName, untranslated)
  }

  console.log('Missing translation keys by locale:')
  for (const fileName of localeFiles) {
    printMissing(fileName, missingByLocale.get(fileName) ?? [])
  }

  printShapeMismatches(shapeMismatches)
  printUntranslated(untranslatedByLocale)

  const hasMissing = [...missingByLocale.values()].some((missing) => missing.length > 0)
  const hasShapeMismatch = shapeMismatches.length > 0
  const hasUntranslated = [...untranslatedByLocale.values()].some((items) => items.length > 0)
  if (hasMissing || hasShapeMismatch || hasUntranslated) {
    process.exitCode = 1
    return
  }

  console.log('\nAll locale files are aligned.')
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error))
  process.exitCode = 1
})
