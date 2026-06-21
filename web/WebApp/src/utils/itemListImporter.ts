import * as XLSX from 'xlsx'

// ─────────────────────────────────────────────
// 從 CSV / TXT / TSV / XLSX 匯入「道具清單」
//
// 自動偵測欄位：
//   - 編號 (id):  id / itemid / 編號 / 道具編號 / 物品編號 …
//   - 數量 (qty): qty / quantity / 數量 / amount / count …
//   - 類型 (type): type / 類型 / 種類 …
// 找不到表頭時 → 第 1 欄 = id, 第 2 欄 = qty, 第 3 欄 = type
// ─────────────────────────────────────────────
export interface ImportedItem {
  itemId: number
  qty: number
  type: number
  name?: string
}
export interface SkippedRow {
  lineNo: number
  reason: string
  raw: string
}
export interface ImportItemResult {
  rows: ImportedItem[]
  totalRead: number
  skipped: number
  skippedDetails: SkippedRow[]
  detectedSource: string
  detectedColumns: string
}

/** UI 點擊加入購物車的預設 Type（道具郵件，與 BatchOpsPage 一致） */
export const DEFAULT_MAIL_TYPE = 1

/** 寬鬆整數解析：接受 "1,234,567"、"1001.0"、前置 '、科學記號 */
function looseInt(raw: unknown): number | null {
  if (raw === null || raw === undefined) return null
  let s = String(raw).trim()
  if (!s) return null
  if (s.startsWith("'")) s = s.slice(1).trim()
  s = s.replace(/[\u3000\s,_]/g, '')
  if (!s) return null
  // 純整數
  if (/^-?\d+$/.test(s)) {
    const n = Number(s)
    return Number.isSafeInteger(n) ? n : null
  }
  // 浮點 / 科學記號
  const f = Number(s)
  if (!Number.isFinite(f)) return null
  const r = Math.round(f)
  if (Math.abs(f - r) > 1e-6) return null
  if (!Number.isSafeInteger(r)) return null
  return r
}

export const ITEM_FILE_INPUT_ACCEPT =
  '.csv,.txt,.tsv,.xlsx,.xls,text/csv,text/plain,text/tab-separated-values,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.ms-excel'

const ID_KEYS   = ['id', 'itemid', '編號', '道具編號', '道具id', '物品編號', '物品id']
const QTY_KEYS  = ['qty', 'quantity', '數量', '個數', 'amount', 'count']
const TYPE_KEYS = ['type', '類型', '種類']
const NAME_KEYS = ['name', 'itemname', '名稱', '道具名稱', '物品名稱']

function detectColumns(header: string[]): { id: number; qty: number; type: number; name: number } {
  const cols = { id: -1, qty: -1, type: -1, name: -1 }
  const norm = header.map(h => (h || '').toLowerCase().replace(/\s+/g, ''))
  // Pass 1: exact match
  for (let i = 0; i < norm.length; i++) {
    const h = norm[i]
    if (!h) continue
    if (cols.id   < 0 && ID_KEYS  .some(k => h === k)) cols.id   = i
    if (cols.qty  < 0 && QTY_KEYS .some(k => h === k)) cols.qty  = i
    if (cols.type < 0 && TYPE_KEYS.some(k => h === k)) cols.type = i
    if (cols.name < 0 && NAME_KEYS.some(k => h === k)) cols.name = i
  }
  // Pass 2: contains
  if (cols.id   < 0) cols.id   = norm.findIndex(h => h && ID_KEYS  .some(k => h.includes(k)))
  if (cols.qty  < 0) cols.qty  = norm.findIndex(h => h && QTY_KEYS .some(k => h.includes(k)))
  if (cols.type < 0) cols.type = norm.findIndex(h => h && TYPE_KEYS.some(k => h.includes(k)))
  if (cols.name < 0) cols.name = norm.findIndex(h => h && NAME_KEYS.some(k => h.includes(k)))
  return cols
}

function emptyResult(src: string): ImportItemResult {
  return { rows: [], totalRead: 0, skipped: 0, skippedDetails: [], detectedSource: src, detectedColumns: '' }
}

function colsToText(cols: { id: number; qty: number; type: number; name: number }): string {
  const fmt = (i: number) => i < 0 ? '—' : `第${i + 1}欄`
  return `Id=${fmt(cols.id)}, Qty=${fmt(cols.qty)}, Type=${fmt(cols.type)}, Name=${fmt(cols.name)}`
}

// 簡易 CSV 切欄：支援雙引號包夾與 "" 轉義
function splitCsvLine(line: string, delim: string): string[] {
  if (line.indexOf('"') < 0) return line.split(delim).map(c => c.trim())
  const out: string[] = []
  let cur = ''
  let inQ = false
  for (let i = 0; i < line.length; i++) {
    const c = line[i]
    if (inQ) {
      if (c === '"' && line[i + 1] === '"') { cur += '"'; i++ }
      else if (c === '"') inQ = false
      else cur += c
    } else {
      if (c === '"') inQ = true
      else if (c === delim) { out.push(cur.trim()); cur = '' }
      else cur += c
    }
  }
  out.push(cur.trim())
  return out
}

function parseDelimited(text: string): ImportItemResult {
  // 移除 BOM
  if (text.charCodeAt(0) === 0xFEFF) text = text.slice(1)
  const allLines = text.split(/\r\n|\r|\n/)
  const linesWithNo: { no: number; raw: string }[] = []
  for (let i = 0; i < allLines.length; i++) {
    if (allLines[i].trim()) linesWithNo.push({ no: i + 1, raw: allLines[i] })
  }
  if (linesWithNo.length === 0) return emptyResult('空檔案')

  // 用「全檔總計」找出最常見的分隔符
  const candidates: string[] = [',', '\t', ';', '|']
  const totals: Record<string, number> = {}
  for (const ch of candidates) totals[ch] = 0
  for (const { raw } of linesWithNo) {
    for (const ch of candidates) {
      let n = 0
      for (let i = 0; i < raw.length; i++) if (raw[i] === ch) n++
      totals[ch] += n
    }
  }
  const delim = candidates.reduce((best, ch) => totals[ch] > totals[best] ? ch : best, ',')

  const matrix = linesWithNo.map(({ no, raw }) => ({ no, raw, cells: splitCsvLine(raw, delim) }))
  let cols = detectColumns(matrix[0].cells)
  let dataStart = cols.id >= 0 ? 1 : 0
  if (dataStart === 0) {
    cols = {
      id: 0,
      qty:  matrix[0].cells.length >= 2 ? 1 : -1,
      type: matrix[0].cells.length >= 3 ? 2 : -1,
      name: -1,
    }
  }

  const rows: ImportedItem[] = []
  const skippedDetails: SkippedRow[] = []
  let totalRead = 0
  for (let i = dataStart; i < matrix.length; i++) {
    totalRead++
    const { no, raw, cells } = matrix[i]
    const idStr = cols.id >= 0 ? (cells[cols.id] || '') : ''
    const id = looseInt(idStr)
    if (id === null || id <= 0) {
      skippedDetails.push({ lineNo: no, reason: !idStr.trim() ? 'ID 欄為空' : `ID '${idStr}' 無法解析為整數`, raw })
      continue
    }
    const qty  = cols.qty  >= 0 ? Math.max(1, looseInt(cells[cols.qty])  ?? 1) : 1
    const type = cols.type >= 0 ? Math.max(0, looseInt(cells[cols.type]) ?? DEFAULT_MAIL_TYPE) : DEFAULT_MAIL_TYPE
    const name = cols.name >= 0 ? cells[cols.name] : undefined
    rows.push({ itemId: id, qty, type, name })
  }
  return {
    rows, totalRead, skipped: skippedDetails.length, skippedDetails,
    detectedSource:  `分隔符 '${delim === '\t' ? '\\t' : delim}'，共 ${linesWithNo.length} 行（含標題 ${dataStart}）`,
    detectedColumns: colsToText(cols),
  }
}

/** 取得 Excel 檔案所有分頁名稱（非 Excel 回空陣列） */
export async function getXlsxSheetNames(file: File): Promise<string[]> {
  const ext = (file.name.split('.').pop() || '').toLowerCase()
  if (ext !== 'xlsx' && ext !== 'xls') return []
  try {
    const buf = await file.arrayBuffer()
    const wb  = XLSX.read(buf, { type: 'array' })
    return wb.SheetNames
  } catch { return [] }
}

async function parseXlsx(file: File, sheetName?: string | null): Promise<ImportItemResult> {
  const buf = await file.arrayBuffer()
  const wb  = XLSX.read(buf, { type: 'array' })

  // 多分頁合併
  if (sheetName === '*') {
    const combined: ImportItemResult = {
      rows: [], totalRead: 0, skipped: 0, skippedDetails: [],
      detectedSource: '', detectedColumns: '',
    }
    const srcs: string[] = []
    for (const name of wb.SheetNames) {
      const ws = wb.Sheets[name]
      if (!ws) continue
      const part = parseXlsxSheet(ws, name)
      combined.rows.push(...part.rows)
      combined.totalRead += part.totalRead
      combined.skipped   += part.skipped
      combined.skippedDetails.push(...part.skippedDetails)
      srcs.push(`'${name}'(${part.rows.length})`)
      if (!combined.detectedColumns) combined.detectedColumns = part.detectedColumns
    }
    combined.detectedSource = `Excel 合併 ${wb.SheetNames.length} 個分頁：${srcs.join(', ')}`
    return combined
  }

  const targetName = sheetName && wb.SheetNames.includes(sheetName) ? sheetName : wb.SheetNames[0]
  const ws = wb.Sheets[targetName]
  if (!ws) return emptyResult('Excel 無資料')
  return parseXlsxSheet(ws, targetName)
}

function parseXlsxSheet(ws: XLSX.WorkSheet, sheetName: string): ImportItemResult {
  // raw: true 讓數字維持為 number、不被欄位格式（千分位/小數）干擾
  const matrix = XLSX.utils.sheet_to_json<unknown[]>(ws, { header: 1, defval: '', raw: true }) as unknown[][]
  if (matrix.length === 0) return emptyResult('Excel 空白')

  const headerStrs = matrix[0].map(c => String(c ?? '').trim())
  let cols = detectColumns(headerStrs)
  let dataStart = cols.id >= 0 ? 1 : 0
  if (dataStart === 0) {
    cols = {
      id: 0,
      qty:  matrix[0].length >= 2 ? 1 : -1,
      type: matrix[0].length >= 3 ? 2 : -1,
      name: -1,
    }
  }

  const rows: ImportedItem[] = []
  const skippedDetails: SkippedRow[] = []
  let totalRead = 0
  for (let i = dataStart; i < matrix.length; i++) {
    const rawCells = matrix[i] || []
    // 整列空白略過
    if (rawCells.every(c => c === null || c === undefined || String(c).trim() === '')) continue
    totalRead++

    const idCell = cols.id >= 0 ? rawCells[cols.id] : ''
    const id = looseInt(idCell)
    const idStr = String(idCell ?? '')
    if (id === null || id <= 0) {
      skippedDetails.push({
        lineNo: i + 1,
        reason: !idStr.trim() ? 'ID 欄為空' : `ID '${idStr}' 無法解析為整數`,
        raw: rawCells.map(c => String(c ?? '')).join(' | '),
      })
      continue
    }
    const qty  = cols.qty  >= 0 ? Math.max(1, looseInt(rawCells[cols.qty])  ?? 1) : 1
    const type = cols.type >= 0 ? Math.max(0, looseInt(rawCells[cols.type]) ?? DEFAULT_MAIL_TYPE) : DEFAULT_MAIL_TYPE
    const name = cols.name >= 0 ? String(rawCells[cols.name] ?? '').trim() || undefined : undefined
    rows.push({ itemId: id, qty, type, name })
  }
  return {
    rows, totalRead, skipped: skippedDetails.length, skippedDetails,
    detectedSource:  `Excel 工作表 '${sheetName}'，共 ${matrix.length} 列（含標題 ${dataStart}）`,
    detectedColumns: colsToText(cols),
  }
}

/**
 * 解析道具清單檔案。
 *   - sheetName == null/undefined → 第一個分頁
 *   - sheetName == '*'            → 合併所有分頁
 *   - 其他                        → 指定分頁
 */
export async function importItemListFromFile(file: File, sheetName?: string | null): Promise<ImportItemResult> {
  const ext = (file.name.split('.').pop() || '').toLowerCase()
  if (ext === 'xlsx' || ext === 'xls') return parseXlsx(file, sheetName)
  const text = await file.text()
  return parseDelimited(text)
}
