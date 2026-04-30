import { readdir, readFile } from 'node:fs/promises'
import path from 'node:path'
import vm from 'node:vm'
import { createRequire } from 'node:module'
import ts from 'typescript'

const cjsRequire = createRequire(import.meta.url)
const localeDir = path.resolve(process.cwd(), 'src/i18n/locales')
const vueRootDir = path.resolve(process.cwd(), 'src')
const localeFiles = ['en.ts', 'sk.ts', 'de.ts']

function isPlainObject(value) {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value)
}

function flattenKeys(source, prefix = '', output = new Set()) {
  if (!isPlainObject(source)) {
    return output
  }

  for (const [key, value] of Object.entries(source)) {
    const keyPath = prefix ? `${prefix}.${key}` : key
    if (isPlainObject(value)) {
      flattenKeys(value, keyPath, output)
      continue
    }

    output.add(keyPath)
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

async function collectVueFiles(rootDir) {
  const files = []

  async function walk(dir) {
    const entries = await readdir(dir, { withFileTypes: true })
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name)
      if (entry.isDirectory()) {
        await walk(fullPath)
      } else if (entry.isFile() && entry.name.endsWith('.vue')) {
        files.push(fullPath)
      }
    }
  }

  await walk(rootDir)
  return files.sort()
}

function getLineNumber(content, index) {
  return content.slice(0, index).split('\n').length
}

function extractI18nKeysFromVue(content) {
  const keyRegexes = [
    /\$t\(\s*['"]([A-Za-z0-9_.-]+)['"]/g,
    /\bt\(\s*['"]([A-Za-z0-9_.-]+)['"]/g,
  ]

  const occurrences = []
  for (const regex of keyRegexes) {
    let match
    while ((match = regex.exec(content)) !== null) {
      occurrences.push({
        key: match[1],
        index: match.index,
      })
    }
  }

  return occurrences
}

async function main() {
  const localeEntries = await Promise.all(
    localeFiles.map(async (fileName) => {
      const filePath = path.join(localeDir, fileName)
      const locale = await loadLocaleObject(filePath)
      return {
        fileName,
        keys: flattenKeys(locale),
      }
    }),
  )

  const englishEntry = localeEntries.find((entry) => entry.fileName === 'en.ts')
  if (!englishEntry) {
    throw new Error('en.ts locale file is required.')
  }

  const vueFiles = await collectVueFiles(vueRootDir)
  const missingMap = new Map()

  for (const filePath of vueFiles) {
    const content = await readFile(filePath, 'utf8')
    const occurrences = extractI18nKeysFromVue(content)

    for (const occurrence of occurrences) {
      if (occurrence.key.endsWith('.')) {
        continue
      }

      if (englishEntry.keys.has(occurrence.key)) {
        continue
      }

      const line = getLineNumber(content, occurrence.index)
      const relPath = path.relative(process.cwd(), filePath).replace(/\\/g, '/')
      const existing = missingMap.get(occurrence.key) ?? []
      existing.push({ path: relPath, line })
      missingMap.set(occurrence.key, existing)
    }
  }

  if (missingMap.size === 0) {
    console.log('No missing translation keys referenced from Vue files.')
    return
  }

  const missingKeys = [...missingMap.keys()].sort()
  console.log(`Missing translation keys referenced from Vue files: ${missingKeys.length}`)
  for (const key of missingKeys) {
    console.log(`- ${key}`)
    const refs = missingMap.get(key) ?? []
    for (const ref of refs) {
      console.log(`  - ${ref.path}:${ref.line}`)
    }
  }

  process.exitCode = 1
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error))
  process.exitCode = 1
})
