import fs from 'node:fs'
import path from 'node:path'
import type { BrowserContext, Page } from '@playwright/test'

export const SCREENSHOT_LOCALES = ['en', 'sk', 'de'] as const
export type ScreenshotLocale = (typeof SCREENSHOT_LOCALES)[number]

export async function openLocalizedScreenshotPage(context: BrowserContext, locale: ScreenshotLocale) {
  const page = await context.newPage()
  await page.addInitScript((localeValue: ScreenshotLocale) => {
    window.localStorage.setItem('app_locale', localeValue)
  }, locale)
  await page.setViewportSize({ width: 1920, height: 1080 })
  return page
}

function ensureDirectory(dirPath: string) {
  fs.mkdirSync(dirPath, { recursive: true })
}

export async function saveLocalizedScreenshot(
  page: Page,
  locale: ScreenshotLocale,
  fileName: string,
  primaryBaseDir: string,
  copyBaseDirs: string[] = [],
  legacyEnglishBaseDirs: string[] = [],
) {
  const savedPaths: string[] = []
  const primaryDir = path.join(primaryBaseDir, locale)
  ensureDirectory(primaryDir)

  const primaryPath = path.join(primaryDir, fileName)
  await page.screenshot({ path: primaryPath })
  savedPaths.push(primaryPath)

  for (const baseDir of copyBaseDirs) {
    const localizedDir = path.join(baseDir, locale)
    ensureDirectory(localizedDir)
    const localizedPath = path.join(localizedDir, fileName)
    fs.copyFileSync(primaryPath, localizedPath)
    savedPaths.push(localizedPath)
  }

  if (locale === 'en') {
    for (const baseDir of legacyEnglishBaseDirs) {
      ensureDirectory(baseDir)
      const legacyPath = path.join(baseDir, fileName)
      fs.copyFileSync(primaryPath, legacyPath)
      savedPaths.push(legacyPath)
    }
  }

  return savedPaths
}