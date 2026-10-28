import { cpSync, existsSync, mkdirSync, rmSync, copyFileSync } from 'node:fs'
import { resolve, join, relative, sep } from 'node:path'

const repo = resolve(import.meta.dirname, '..')
const site = join(repo, 'site')
const output = join(repo, '.pages')
if (!output.startsWith(repo + sep) || !existsSync(join(site, 'index.html'))) throw new Error('Invalid Pages build paths')
rmSync(output, { recursive: true, force: true })
mkdirSync(output, { recursive: true })
cpSync(site, join(output, 'site'), {
  recursive: true,
  filter: source => {
    const path = relative(site, source).replaceAll('\\', '/')
    return !path.startsWith('downloads/') || path === 'downloads/SHA256SUMS.txt'
  }
})
copyFileSync(join(repo, '.github/pages-root.html'), join(output, 'index.html'))
console.log('Prepared GitHub Pages artifact at .pages/ with /site/ and root redirect')
