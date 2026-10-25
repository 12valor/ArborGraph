const { test } = require('node:test')
const assert = require('node:assert/strict')
const { validateDirectoryQuery, validateDuplicatePage, validateFileInsightsQuery, validateFileQuery, validateFolderLimit, validateHistoryLimit, validateScanIds, validateScanRoots } = require('../dist/main/validation.js')

test('scan targets must come from a trusted picker or drive list', () => {
  const allowed = new Set(['c:\\', 'c:\\chosen'])
  assert.deepEqual(validateScanRoots(['C:\\chosen'], allowed), ['C:\\chosen'])
  assert.throws(() => validateScanRoots(['C:\\private'], allowed))
  assert.throws(() => validateScanRoots(['.\\chosen'], allowed))
  assert.throws(() => validateScanRoots(['C:\\chosen', 'C:\\CHOSEN'], allowed))
  assert.throws(() => validateScanRoots([], allowed))
  assert.throws(() => validateScanRoots(Array(17).fill('C:\\chosen'), allowed))
  assert.throws(() => validateScanRoots([42], allowed))
})

test('scan history comparisons require ordered positive IDs', () => {
  assert.deepEqual(validateScanIds(1, 2), { olderId: 1, newerId: 2 })
  assert.throws(() => validateScanIds(2, 1))
  assert.throws(() => validateScanIds(1, 1))
  assert.throws(() => validateScanIds(-1, 2))
  assert.equal(validateHistoryLimit(30), 30)
  assert.throws(() => validateHistoryLimit(101))
})

test('file insights are bounded before reaching the engine', () => {
  assert.deepEqual(validateFileInsightsQuery(180, 100), { daysOld: 180, limit: 100 })
  assert.throws(() => validateFileInsightsQuery(0, 100))
  assert.throws(() => validateFileInsightsQuery(180, 201))
})

test('duplicate result pages stay within protocol bounds', () => {
  assert.deepEqual(validateDuplicatePage(0, 25), { offset: 0, limit: 25 })
  assert.throws(() => validateDuplicatePage(-1, 25))
  assert.throws(() => validateDuplicatePage(0, 51))
})

test('indexed queries accept bounded filters and indexed directory paths', () => {
  assert.deepEqual(validateFileQuery({ limit: 25, search: 'project', sortBy: 'name' }).limit, 25)
  assert.throws(() => validateFileQuery({ limit: 201 }))
  assert.throws(() => validateFileQuery({ search: 'x'.repeat(201) }))
  assert.throws(() => validateFileQuery({ sortBy: 'DROP TABLE files' }))
  assert.throws(() => validateFileQuery({ extra: true }))
  assert.deepEqual(validateDirectoryQuery({ path: 'C:\\indexed', limit: 100 }), { path: 'C:\\indexed', limit: 100 })
  assert.throws(() => validateDirectoryQuery({ path: '..\\private' }))
  assert.throws(() => validateDirectoryQuery({ path: 'C:\\indexed', limit: 151 }))
  assert.equal(validateFolderLimit(20), 20)
  assert.throws(() => validateFolderLimit(101))
})
