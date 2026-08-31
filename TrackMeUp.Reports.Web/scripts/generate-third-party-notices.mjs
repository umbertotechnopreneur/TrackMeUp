import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const projectDirectory = fileURLToPath(new URL('../', import.meta.url))
const lockPath = join(projectDirectory, 'package-lock.json')
const outputPath = join(projectDirectory, 'dist', 'THIRD_PARTY_NOTICES.md')
const lock = JSON.parse(await readFile(lockPath, 'utf8'))

if (lock.lockfileVersion !== 3 || typeof lock.packages !== 'object' || lock.packages === null) {
  throw new Error('Expected a package-lock.json v3 packages map.')
}

const productionPackages = Object.entries(lock.packages)
  .filter(([packagePath, metadata]) => (
    packagePath.startsWith('node_modules/')
    && !metadata.dev
    && !metadata.devOptional
    && !(metadata.optional && (metadata.os || metadata.cpu))
  ))
  .map(([packagePath, metadata]) => ({ packagePath, metadata }))
  .sort((left, right) => left.packagePath.localeCompare(right.packagePath, 'en'))

if (productionPackages.length === 0) {
  throw new Error('The package-lock.json production dependency closure is empty.')
}

function markdownFence(text) {
  const longestRun = Math.max(0, ...(text.match(/`+/g) ?? []).map((value) => value.length))
  return '`'.repeat(Math.max(3, longestRun + 1))
}

const notices = []

for (const { packagePath, metadata } of productionPackages) {
  if (typeof metadata.version !== 'string' || typeof metadata.license !== 'string') {
    throw new Error(`Production package '${packagePath}' has no version or SPDX license in package-lock.json.`)
  }

  const packageDirectory = join(projectDirectory, packagePath)
  const packageJson = JSON.parse(await readFile(join(packageDirectory, 'package.json'), 'utf8'))

  if (
    packageJson.version !== metadata.version
    || packageJson.license !== metadata.license
    || typeof packageJson.name !== 'string'
  ) {
    throw new Error(`Installed metadata for '${packagePath}' does not match package-lock.json.`)
  }

  const noticeFiles = (await readdir(packageDirectory, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && /^(?:licen[cs]e|notice|copyright)(?:\.|$)/i.test(entry.name))
    .map((entry) => entry.name)
    .sort((left, right) => left.localeCompare(right, 'en'))

  if (noticeFiles.length === 0) {
    throw new Error(`Production package '${packageJson.name}' has no top-level license or notice file.`)
  }

  const files = []
  for (const fileName of noticeFiles) {
    const text = (await readFile(join(packageDirectory, fileName), 'utf8'))
      .replace(/\r\n?/g, '\n')
      .trimEnd()

    files.push({ fileName, text })
  }

  notices.push({
    name: packageJson.name,
    version: metadata.version,
    license: metadata.license,
    files,
  })
}

const lines = [
  '# Web Report Production Third-Party Notices',
  '',
  'This file is generated. Do not edit it manually.',
  '',
  '- Regenerate with `npm run notices:production` from `TrackMeUp.Reports.Web`.',
  '- Source of truth: `package-lock.json` plus the installed packages\' top-level license and notice files.',
  '- Scope: the complete production dependency closure resolved for the tracked web report bundle; development-only tooling is excluded.',
  '- Platform-specific optional packages are excluded from the generated report distribution notices.',
  '',
  `Production packages: ${notices.length}.`,
  '',
  '| Package | Version | Declared license | Included files |',
  '| --- | --- | --- | --- |',
  ...notices.map((notice) => `| \`${notice.name}\` | \`${notice.version}\` | \`${notice.license}\` | ${notice.files.map((file) => `\`${file.fileName}\``).join(', ')} |`),
  '',
]

for (const notice of notices) {
  lines.push(`## ${notice.name} ${notice.version}`, '', `Declared license: \`${notice.license}\``, '')

  for (const file of notice.files) {
    const fence = markdownFence(file.text)
    lines.push(`### ${file.fileName}`, '', `${fence}text`, file.text, fence, '')
  }
}

await mkdir(dirname(outputPath), { recursive: true })
await writeFile(outputPath, `${lines.join('\n').trimEnd()}\n`, 'utf8')

console.log(`Generated production notices for ${notices.length} packages at ${outputPath}.`)
