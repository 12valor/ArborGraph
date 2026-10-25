import type { Drive, Overview, ScanEvent } from '../../main/contracts'

interface ArborApi {
  listDrives(): Promise<Drive[]>
  getOverview(): Promise<Overview>
  chooseFolder(): Promise<string | null>
  startScan(roots: string[]): Promise<{ accepted: boolean; roots: string[] }>
  cancelScan(): Promise<{ cancelling: boolean }>
  onScanEvent(callback: (event: ScanEvent) => void): () => void
}

declare global {
  interface Window { arbor: ArborApi }
}
