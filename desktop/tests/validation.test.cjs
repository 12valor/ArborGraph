const { test } = require('node:test')
const assert = require('node:assert/strict')
const { validateScanRoots } = require('../dist/main/validation.js')

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
