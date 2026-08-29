import { readFile, readdir } from 'node:fs/promises'
import { extname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const outputDirectory = new URL('../dist/', import.meta.url)
const outputDirectoryPath = fileURLToPath(outputDirectory)
const indexPath = new URL('index.html', outputDirectory)
const indexHtml = await readFile(indexPath, 'utf8')
const noticesPath = new URL('THIRD_PARTY_NOTICES.md', outputDirectory)
const notices = await readFile(noticesPath, 'utf8')

if (!notices.includes('# Web Report Production Third-Party Notices') || !notices.includes('Production packages:')) {
  throw new Error('Production third-party notices are missing or invalid.')
}

const requiredCspDirectives = [
  "script-src 'self'",
  "connect-src 'none'",
  "object-src 'none'",
  "frame-ancestors 'none'",
  "form-action 'none'",
]

for (const directive of requiredCspDirectives) {
  if (!indexHtml.includes(directive)) {
    throw new Error(`Production CSP is missing: ${directive}`)
  }
}

if (/\b(?:src|href)=["']https?:\/\//i.test(indexHtml)) {
  throw new Error('Production index contains an external asset reference.')
}

async function collectTextAssets(directory) {
  const entries = await readdir(directory, { withFileTypes: true })
  const assets = []

  for (const entry of entries) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) {
      assets.push(...await collectTextAssets(path))
      continue
    }

    if (['.html', '.js', '.css'].includes(extname(entry.name))) assets.push(path)
  }

  return assets
}

const textAssets = await collectTextAssets(outputDirectoryPath)
const productionText = (await Promise.all(textAssets.map((path) => readFile(path, 'utf8')))).join('\n')

const fixtureSentinels = [
  'Anteprima di sviluppo',
  'dati dimostrativi',
  '2026-07-23',
  'Visual Studio',
]

for (const sentinel of fixtureSentinels) {
  if (productionText.includes(sentinel)) {
    throw new Error(`Development fixture leaked into production assets: ${sentinel}`)
  }
}

for (const runtimeContract of [
  'report.ready',
  'report.snapshot',
  'report.language.state',
  'report.theme.set',
  'report.theme.state',
  'report.theme.error',
]) {
  if (!productionText.includes(runtimeContract)) {
    throw new Error(`Production runtime contract is missing: ${runtimeContract}`)
  }
}

for (const locale of [
  'en-US',
  'it-IT',
  'fr-FR',
  'de-DE',
  'es-ES',
  'zh-Hans',
  'vi-VN',
  'ko-KR',
  'pt-PT',
  'pt-BR',
]) {
  if (!productionText.includes(locale)) {
    throw new Error(`Production report locale is missing: ${locale}`)
  }
}

if (productionText.includes('trackmeup.reports.theme.v1')) {
  throw new Error('The production bundle still contains the legacy WebView-local theme preference.')
}

console.log(`Verified ${textAssets.length} production text assets: CSP, offline assets, runtime contract, and fixture isolation.`)
