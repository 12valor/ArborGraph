import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import { EventEmitter } from 'node:events'

type Pending = { resolve: (value: unknown) => void; reject: (error: Error) => void; timer: NodeJS.Timeout }
type EngineResponse = { type: 'response'; id: string; ok: boolean; data?: unknown; error?: { code: string; message: string } }

export class EngineClient extends EventEmitter {
  private process: ChildProcessWithoutNullStreams | null = null
  private pending = new Map<string, Pending>()
  private nextId = 0
  private buffer = ''

  constructor(private readonly executable: string, private readonly arguments_: string[] = []) { super() }

  async start(): Promise<void> {
    if (this.process) return
    if (!existsSync(this.executable)) throw new Error(`Engine host is missing: ${this.executable}`)
    const child = spawn(this.executable, this.arguments_, { stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true })
    this.process = child
    child.stdout.setEncoding('utf8')
    child.stdout.on('data', (chunk: string) => this.onData(chunk))
    child.stderr.setEncoding('utf8')
    child.stderr.on('data', (chunk: string) => console.error(`[engine] ${chunk.trim()}`))
    child.on('error', (error) => this.failAll(error))
    child.on('exit', (code) => {
      this.process = null
      this.failAll(new Error(`Engine host exited (${code ?? 'unknown'}).`))
      this.emit('event', { eventName: 'engineExited', message: `Engine host exited (${code ?? 'unknown'}).` })
    })
    await this.request('ping')
  }

  request(command: string, args: Record<string, unknown> = {}, timeoutMs = 20_000): Promise<unknown> {
    if (!this.process) return Promise.reject(new Error('Engine host is not running.'))
    if (this.pending.size >= 32) return Promise.reject(new Error('Too many pending engine requests.'))
    const id = String(++this.nextId)
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id)
        reject(new Error(`Engine request timed out: ${command}`))
      }, timeoutMs)
      this.pending.set(id, { resolve, reject, timer })
      this.process!.stdin.write(JSON.stringify({ id, command, ...args }) + '\n', (error) => {
        if (!error) return
        const pending = this.pending.get(id)
        if (pending) { clearTimeout(pending.timer); this.pending.delete(id); pending.reject(error) }
      })
    })
  }

  async stop(): Promise<void> {
    const child = this.process
    if (!child) return
    const exited = new Promise<void>((resolve) => child.once('exit', () => resolve()))
    try { await this.request('shutdown') } catch { /* the exit path still closes it */ }
    let timer: NodeJS.Timeout | undefined
    const deadline = new Promise<void>((resolve) => { timer = setTimeout(resolve, 5000) })
    await Promise.race([exited, deadline])
    if (timer) clearTimeout(timer)
    if (child.exitCode === null && !child.killed) child.kill()
  }

  private onData(chunk: string): void {
    this.buffer += chunk
    if (this.buffer.length > 2_000_000) { this.process?.kill(); return }
    let end: number
    while ((end = this.buffer.indexOf('\n')) !== -1) {
      const line = this.buffer.slice(0, end)
      this.buffer = this.buffer.slice(end + 1)
      try {
        const message: unknown = JSON.parse(line)
        if (!message || typeof message !== 'object') continue
        const record = message as Record<string, unknown>
        if (record.type === 'event' && typeof record.eventName === 'string') {
          this.emit('event', record)
        } else if (record.type === 'response' && typeof record.id === 'string') {
          const pending = this.pending.get(record.id)
          if (!pending) continue
          clearTimeout(pending.timer)
          this.pending.delete(record.id)
          const response = record as EngineResponse
          if (response.ok) pending.resolve(response.data)
          else pending.reject(new Error(response.error?.message ?? 'Engine request failed.'))
        }
      } catch (error) { console.error('Invalid engine message:', error) }
    }
  }

  private failAll(error: Error): void {
    for (const pending of this.pending.values()) { clearTimeout(pending.timer); pending.reject(error) }
    this.pending.clear()
  }
}

export function findEngineExecutable(appRoot: string, resourcesPath: string, packaged: boolean): string {
  return packaged
    ? path.join(resourcesPath, 'engine', 'ArborGraph.EngineHost.exe')
    : path.resolve(appRoot, '..', 'engine', 'bin', 'Release', 'net8.0-windows', 'ArborGraph.EngineHost.exe')
}
