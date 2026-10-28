import assert from 'node:assert/strict'
import { createServer } from 'node:http'
import { createReadStream, existsSync, readFileSync, statSync } from 'node:fs'
import { resolve, extname, join } from 'node:path'
import { Browser, sleep } from '../.agents/skills/design-taste-frontend/scripts/lib/cdp.mjs'
import './build-pages.mjs'

const repo = resolve(import.meta.dirname, '..')
const root = join(repo, '.pages')
const site = join(root, 'site')
const html = readFileSync(join(site, 'index.html'), 'utf8')
const checksum = readFileSync(join(site, 'downloads/SHA256SUMS.txt'), 'utf8').slice(0, 64)
assert.match(html, /Understand where your storage goes\./)
assert.match(html, new RegExp(checksum))
assert.match(html, /ArborGraph-Desktop-Preview-Setup-v1\.3\.1-x64\.exe/)
assert.match(html, /releases\/download\/v1\.3\.1\/ArborGraph-Desktop-Preview-Setup-v1\.3\.1-x64\.exe/)

const mime = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'text/javascript', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.txt': 'text/plain', '.woff2': 'font/woff2' }
const server = createServer((req, res) => {
  try {
    const path = decodeURIComponent(new URL(req.url, 'http://localhost').pathname)
    const relative = path.replace(/^\/ArborGraph\/?/, '') || 'index.html'
    const file = resolve(root, relative)
    if (file !== root && !file.startsWith(root + '\\')) throw new Error('Outside artifact')
    const target = statSync(file).isDirectory() ? join(file, 'index.html') : file
    res.writeHead(200, { 'Content-Type': mime[extname(target)] ?? 'application/octet-stream', 'Content-Length': statSync(target).size })
    createReadStream(target).pipe(res)
  } catch { res.writeHead(404); res.end('Not found') }
})
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve))
const base = `http://127.0.0.1:${server.address().port}/ArborGraph/site/`
assert.match(await (await fetch(base + '../')).text(), /url=\.\/site\//)
assert.ok(!existsSync(join(site, 'downloads/ArborGraph-Desktop-Preview-Setup-v1.3.1-x64.exe')))
let browser
try {
  browser = await Browser.launch({ width: 1440, height: 900 })
  const page = await browser.newPage({ width: 1440, height: 900 })
  for (const width of [1920, 1440, 1024, 768, 390, 320]) {
    await page.setViewport({ width, height: 900, mobile: width <= 390 })
    await page.goto(base, { settleMs: 500 })
    const result = await page.eval(() => ({
      overflow: document.documentElement.scrollWidth - innerWidth,
      title: document.title,
      missingAnchors: [...document.querySelectorAll('a[href^="#"]')].filter(a => !document.getElementById(a.hash.slice(1))).map(a => a.hash),
      brokenImages: [...document.images].filter(image => image.loading !== 'lazy' && (!image.complete || image.naturalWidth === 0)).map(image => image.src),
      typefaces: [...new Set([...document.querySelectorAll('h1,h2,p,button')].map(element => getComputedStyle(element).fontFamily))],
      fontReady: document.fonts.check('600 16px Inter'),
      externalFontRequests: performance.getEntriesByType('resource').filter(item => item.name.includes('fonts.googleapis') || item.name.includes('fonts.gstatic')).length,
      screenshots: [...document.querySelectorAll('.screenshot-link')].map(link => {
        const img = link.querySelector('img'), rect = img.getBoundingClientRect()
        return { sameFile: link.href === img.src, insideViewport: rect.left >= -1 && rect.right <= innerWidth + 1,
          ratio: rect.width / rect.height }
      })
    }))
    assert.ok(result.overflow <= 1, `${width}px horizontal overflow: ${result.overflow}px`)
    assert.deepEqual(result.missingAnchors, [], `${width}px missing anchors`)
    assert.deepEqual(result.brokenImages, [], `${width}px broken images`)
    assert.ok(result.typefaces.every(font => font.startsWith('Inter')), `${width}px has non-Inter text`)
    assert.ok(result.fontReady, `${width}px Inter font did not load`)
    assert.equal(result.externalFontRequests, 0, `${width}px requested third-party fonts`)
    assert.equal(result.screenshots.length, 4, `${width}px should have four expandable screenshots`)
    assert.ok(result.screenshots.every(item => item.sameFile), `${width}px screenshot links must open their images`)
    if (width <= 390) assert.ok(result.screenshots.every(item => item.insideViewport && Math.abs(item.ratio - 1360 / 900) < .03), `${width}px screenshots should fit without cropping`)
    assert.deepEqual(page.consoleErrors, [], `${width}px console errors`)
    console.log(`PASS layout and assets at ${width}px`)
  }

  await page.setViewport({ width: 390, height: 900, mobile: true })
  await page.goto(base, { settleMs: 500 })
  const menu = await page.eval(() => {
    const button = document.querySelector('.menu-toggle')
    button.click()
    const opened = button.getAttribute('aria-expanded') === 'true' && getComputedStyle(document.getElementById('primary-nav')).display !== 'none'
    document.querySelector('#primary-nav a[href="#docs"]').click()
    return { opened, closed: button.getAttribute('aria-expanded') === 'false', hash: location.hash }
  })
  assert.deepEqual(menu, { opened: true, closed: true, hash: '#docs' })
  console.log('PASS mobile navigation')

  await page.eval(() => {
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: async text => { window.__copied = text } } })
    document.querySelector('[data-copy="official-hash"]').click()
  })
  await sleep(100)
  const copied = await page.eval(() => ({ value: window.__copied, feedback: document.querySelector('[data-copy="official-hash"]').textContent }))
  assert.equal(copied.value, checksum)
  assert.equal(copied.feedback, 'Copied')
  const compared = await page.eval((digest) => {
    const input = document.getElementById('entered-hash')
    const button = document.getElementById('compare-hash')
    const result = document.getElementById('verify-result')
    input.value = digest.toLowerCase(); button.click(); const match = result.className === 'success'
    input.value = '0'.repeat(64); button.click(); const mismatch = result.className === 'error' && result.textContent.includes('No match')
    input.value = 'abc'; button.click(); const invalid = result.textContent.includes('64-character')
    return { match, mismatch, invalid }
  }, checksum)
  assert.deepEqual(compared, { match: true, mismatch: true, invalid: true })
  console.log('PASS checksum copy and comparison')

  const localLinks = await page.eval(() => [...new Set([...document.querySelectorAll('a[href]')].map(a => a.getAttribute('href')).filter(href => href.startsWith('./')))])
  for (const href of localLinks) {
    const url = new URL(href, base)
    const response = await fetch(url)
    assert.equal(response.status, 200, `${href} missing under Pages subpath`)
    if (url.hash) {
      const target = response.headers.get('content-type')?.includes('html') ? await response.text() : ''
      assert.ok(target.includes(`id="${url.hash.slice(1)}"`), `${href} missing target`)
    }
  }
  console.log(`PASS ${localLinks.length} local cross-page links under /ArborGraph/site/`)
  const imageUrls = await page.eval(() => [...new Set([...document.images].map(image => image.src))])
  for (const url of imageUrls) assert.equal((await fetch(url)).status, 200, `Missing image ${url}`)
  console.log(`PASS ${imageUrls.length} product images`)

  await page.goto(base + 'walkthrough.html', { settleMs: 1000 })
  const legacy = await page.eval((digest) => {
    document.querySelector('[data-target-doc="docSecurity"]').click()
    document.querySelector('[data-target-legal="legalPrivacy"]').click()
    const docs = document.getElementById('docSecurity').classList.contains('active') && !document.getElementById('docGettingStarted').classList.contains('active')
    const legal = document.getElementById('legalPrivacy').classList.contains('active') && !document.getElementById('legalEula').classList.contains('active')
    const nav = document.getElementById('mobileNavToggle')
    nav.click()
    const navOpen = nav.getAttribute('aria-expanded') === 'true'
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))
    const wizardNext = document.getElementById('btnWizardNext')
    wizardNext.click()
    const wizardGate = document.getElementById('wizardStep2').classList.contains('active') && wizardNext.disabled
    const agreement = document.getElementById('wizardEulaCheck')
    agreement.checked = true
    agreement.dispatchEvent(new Event('change', { bubbles: true }))
    wizardNext.click()
    const wizardContinues = document.getElementById('wizardStep3').classList.contains('active')
    document.getElementById('btnStartSim').click()
    const scannerStarted = document.getElementById('scanStateBadge').textContent.includes('Scanning')
    document.getElementById('btnStopSim').click()
    const scannerStopped = document.getElementById('scanStateBadge').textContent === 'Stopped'
    const treemap = document.getElementById('treemapCanvas').children.length > 0
    const hashInput = document.getElementById('verifyInput')
    hashInput.value = digest
    document.getElementById('verifyBtn').click()
    const checksumMatch = document.getElementById('verifyResult').classList.contains('match')
    hashInput.value = '0'.repeat(64)
    document.getElementById('verifyBtn').click()
    const checksumMismatch = document.getElementById('verifyResult').classList.contains('mismatch')
    const fonts = [...new Set([...document.querySelectorAll('h1,p,button,code')].map(element => getComputedStyle(element).fontFamily))]
    return { banner: !!document.querySelector('.legacy-banner'), docs, legal, navOpen, navClosed: nav.getAttribute('aria-expanded') === 'false', wizardGate, wizardContinues, scannerStarted, scannerStopped, treemap, checksumMatch, checksumMismatch, interOnly: fonts.every(font => font.startsWith('Inter')) }
  }, checksum)
  assert.deepEqual(legacy, { banner: true, docs: true, legal: true, navOpen: true, navClosed: true, wizardGate: true, wizardContinues: true, scannerStarted: true, scannerStopped: true, treemap: true, checksumMatch: true, checksumMismatch: true, interOnly: true })
  const legacyOverflow = await page.eval(() => document.documentElement.scrollWidth - innerWidth)
  assert.ok(legacyOverflow <= 1, `Walkthrough horizontal overflow: ${legacyOverflow}px`)
  assert.deepEqual(page.consoleErrors, [], 'Walkthrough console errors')
  console.log('PASS preserved walkthrough, wizard, scanner, treemap, and checksum')
  await page.close()

  const reduced = await browser.newPage({ width: 390, height: 900, mobile: true, reducedMotion: true })
  await reduced.goto(base, { settleMs: 300 })
  const motion = await reduced.eval(() => ({ duration: getComputedStyle(document.querySelector('.hero-visual img')).animationDuration, hidden: document.querySelectorAll('.will-reveal').length }))
  assert.ok(parseFloat(motion.duration) < 0.01, `reduced motion duration ${motion.duration}`)
  assert.equal(motion.hidden, 0)
  console.log('PASS reduced motion')
  await reduced.close()
} finally {
  if (browser) await browser.close()
  server.close()
}
