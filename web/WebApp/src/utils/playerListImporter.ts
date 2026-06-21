import * as XLSX from 'xlsx'

/** 從檔案解析出來的單一玩家列。 */
export interface ImportedPlayer {
  /** 識別編號（DB csalogin.Name / cdkey） */
  cdkey: string
  /** 顯示名稱（DB csalogin.OnlineName，可空） */
  onlineName: string
}

export interface ImportResult {
  rows: ImportedPlayer[]
  totalRead: number
  skipped: number
  detectedSource: string
}

/** 給 <input type="file" accept="..."> 用 */
export const FILE_INPUT_ACCEPT =
  '.csv,.txt,.tsv,.xlsx,.xls,text/csv,text/plain,text/tab-separated-values,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'

const CDKEY_KEYS = [
  'name', 'cdkey', 'account', 'uid',
  '識別編號', '識別', '編號', '帳號', '主帳號',
]
const NAME_KEYS = [
  'onlinename', 'char', 'charname', 'character', 'nickname',
  '名稱', '角色名', '角色名稱', '暱稱', '名字',
]

function detectColumns(header: string[]): { cdkey: number; name: number; hasHeader: boolean } {
  let cdkey = -1
  let name = -1
  for (let i = 0; i < header.length; i++) {
    const h = (header[i] ?? '').toString().toLowerCase().replace(/\s+/g, '')
    if (!h) continue
    if (cdkey < 0 && CDKEY_KEYS.some(k => h === k)) cdkey = i
    if (name < 0 && NAME_KEYS.some(k => h === k)) name = i
  }
  if (cdkey < 0) {
    for (let i = 0; i < header.length; i++) {
      const h = (header[i] ?? '').toString().toLowerCase()
      if (CDKEY_KEYS.some(k => h.includes(k))) { cdkey = i; break }
    }
  }
  if (name < 0) {
    for (let i = 0; i < header.length; i++) {
      const h = (header[i] ?? '').toString().toLowerCase()
      if (NAME_KEYS.some(k => h.includes(k))) { name = i; break }
    }
  }
  const hasHeader = cdkey >= 0 || name >= 0
  if (!hasHeader) {
    cdkey = 0
    name = header.length >= 2 ? 1 : -1
  }
  return { cdkey, name, hasHeader }
}

function parseDelimited(text: string): ImportResult {
  const lines = text.split(/\r\n|\n|\r/).filter(l => l.trim().length > 0)
  if (lines.length === 0) {
    return { rows: [], totalRead: 0, skipped: 0, detectedSource: '空檔案' }
  }
  // 偵測分隔符
  const delimiters = [',', '\t', ';', '|']
  const sample = lines[0]
  const delim = delimiters
    .map(d => ({ d, c: (sample.match(new RegExp(d === '\t' ? '\\t' : `\\${d}`, 'g')) || []).length }))
    .sort((a, b) => b.c - a.c)[0].d

  const rows = lines.map(l =>
    l.split(delim).map(c => c.trim().replace(/^"(.*)"$/, '$1')),
  )
  const { cdkey, name, hasHeader } = detectColumns(rows[0])
  const dataStart = hasHeader ? 1 : 0
  const out: ImportedPlayer[] = []
  let skipped = 0
  for (let i = dataStart; i < rows.length; i++) {
    const r = rows[i]
    const k = (cdkey < r.length ? r[cdkey] : '') ?? ''
    const n = name >= 0 && name < r.length ? r[name] : ''
    if (!k && !n) { skipped++; continue }
    out.push({ cdkey: k, onlineName: n })
  }
  return {
    rows: out,
    totalRead: rows.length - dataStart,
    skipped,
    detectedSource: `分隔符 '${delim === '\t' ? '\\t' : delim}'，共 ${rows.length} 行`,
  }
}

function parseXlsx(buffer: ArrayBuffer): ImportResult {
  const wb = XLSX.read(buffer, { type: 'array' })
  const wsName = wb.SheetNames[0]
  const ws = wb.Sheets[wsName]
  if (!ws) return { rows: [], totalRead: 0, skipped: 0, detectedSource: '空 Excel' }
  // 把每列轉成字串陣列
  const aoa = XLSX.utils.sheet_to_json<unknown[]>(ws, { header: 1, blankrows: false, defval: '' })
  if (aoa.length === 0) {
    return { rows: [], totalRead: 0, skipped: 0, detectedSource: `Excel 工作表 '${wsName}'，0 列` }
  }
  const header = aoa[0].map(c => (c ?? '').toString())
  const { cdkey, name, hasHeader } = detectColumns(header)
  const dataStart = hasHeader ? 1 : 0
  const out: ImportedPlayer[] = []
  let skipped = 0
  for (let i = dataStart; i < aoa.length; i++) {
    const r = aoa[i]
    const k = (cdkey < r.length ? r[cdkey] : '')?.toString() ?? ''
    const n = name >= 0 && name < r.length ? (r[name]?.toString() ?? '') : ''
    if (!k && !n) { skipped++; continue }
    out.push({ cdkey: k.trim(), onlineName: n.trim() })
  }
  return {
    rows: out,
    totalRead: aoa.length - dataStart,
    skipped,
    detectedSource: `Excel 工作表 '${wsName}'，共 ${aoa.length} 列`,
  }
}

/** 從一個 File 物件解析 */
export async function importPlayerListFromFile(file: File): Promise<ImportResult> {
  const ext = (file.name.split('.').pop() || '').toLowerCase()
  const isExcel = ext === 'xlsx' || ext === 'xls' ||
    file.type === 'application/vnd.ms-excel' ||
    file.type === 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
  if (isExcel) {
    const buf = await file.arrayBuffer()
    return parseXlsx(buf)
  }
  // CSV / TXT / TSV：用 UTF-8 讀
  const text = await file.text()
  return parseDelimited(text)
}

/**
 * 把解析結果格式化成「一行一個 cdkey」的 textarea 內容。
 * 既保留現有 customList 文本格式，也讓使用者一眼看得到。
 */
export function importedToCustomList(result: ImportResult): string {
  return result.rows
    .map(r => r.cdkey.trim())
    .filter(s => s.length > 0)
    .join('\n')
}
