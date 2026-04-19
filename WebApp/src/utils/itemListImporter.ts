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
export interface ImportItemResult {
  rows: ImportedItem[]
  totalRead: number
  skipped: number
  detectedSource: string
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

function parseDelimited(text: string): ImportItemResult {
  const lines = text.split(/\r\n|\r|\n/).filter(l => l.trim())
  if (lines.length === 0) return { rows: [], totalRead: 0, skipped: 0, detectedSource: '空檔案' }
  const sample = lines[0]
  const counts: Record<string, number> = {}
  for (const ch of [',', '\t', ';', '|']) counts[ch] = (sample.match(new RegExp(ch === '\\t' ? '\t' : `\\${ch}`, 'g')) || []).length
  const delim = Object.entries(counts).sort(([, a], [, b]) => b - a)[0][0]
  const matrix = lines.map(l => l.split(delim).map(c => c.trim().replace(/^"|"$/g, '')))

  let cols = detectColumns(matrix[0])
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
  let skipped = 0
  for (let i = dataStart; i < matrix.length; i++) {
    const cells = matrix[i]
    const idStr = cols.id >= 0 ? (cells[cols.id] || '') : ''
    const id = parseInt(idStr, 10)
    if (!Number.isFinite(id) || id <= 0) { skipped++; continue }
    const qty  = cols.qty  >= 0 ? Math.max(1, parseInt(cells[cols.qty]  || '1', 10) || 1) : 1
    const type = cols.type >= 0 ? Math.max(0, parseInt(cells[cols.type] || '0', 10) || 0) : 0
    const name = cols.name >= 0 ? cells[cols.name] : undefined
    rows.push({ itemId: id, qty, type, name })
  }
  return { rows, totalRead: matrix.length - dataStart, skipped, detectedSource: `分隔符 '${delim === '\t' ? '\\t' : delim}'，共 ${lines.length} 行` }
}

async function parseXlsx(file: File): Promise<ImportItemResult> {
  const buf = await file.arrayBuffer()
  const wb  = XLSX.read(buf, { type: 'array' })
  const ws  = wb.Sheets[wb.SheetNames[0]]
  if (!ws) return { rows: [], totalRead: 0, skipped: 0, detectedSource: 'Excel 無資料' }
  const matrix = XLSX.utils.sheet_to_json<string[]>(ws, { header: 1, defval: '' }) as string[][]
  if (matrix.length === 0) return { rows: [], totalRead: 0, skipped: 0, detectedSource: 'Excel 空白' }

  let cols = detectColumns(matrix[0].map(c => String(c ?? '')))
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
  let skipped = 0
  for (let i = dataStart; i < matrix.length; i++) {
    const cells = matrix[i].map(c => String(c ?? '').trim())
    const idStr = cols.id >= 0 ? (cells[cols.id] || '') : ''
    const id = parseInt(idStr, 10)
    if (!Number.isFinite(id) || id <= 0) { skipped++; continue }
    const qty  = cols.qty  >= 0 ? Math.max(1, parseInt(cells[cols.qty]  || '1', 10) || 1) : 1
    const type = cols.type >= 0 ? Math.max(0, parseInt(cells[cols.type] || '0', 10) || 0) : 0
    const name = cols.name >= 0 ? cells[cols.name] : undefined
    rows.push({ itemId: id, qty, type, name })
  }
  return { rows, totalRead: matrix.length - dataStart, skipped, detectedSource: `Excel 工作表 '${wb.SheetNames[0]}'，共 ${matrix.length} 列` }
}

export async function importItemListFromFile(file: File): Promise<ImportItemResult> {
  const ext = (file.name.split('.').pop() || '').toLowerCase()
  if (ext === 'xlsx' || ext === 'xls') return parseXlsx(file)
  const text = await file.text()
  return parseDelimited(text)
}
