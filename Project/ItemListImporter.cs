using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 從 CSV / TXT / XLSX 解析「道具清單」。
    /// 可辨識的欄位：道具編號 (id) / 數量 (qty) / 類型 (type)
    ///   - 標題列偵測：包含 編號/Id/ItemId 等視為 id
    ///                包含 數量/Qty/Quantity 視為 qty
    ///                包含 Type/類型 視為 type
    ///   - 找不到標題 → 第 1 欄=Id，第 2 欄=Qty（缺則 1），第 3 欄=Type（缺則 0）
    /// </summary>
    internal static class ItemListImporter
    {
        public class Row
        {
            public int Id   { get; set; }
            public int Qty  { get; set; } = 1;
            public int Type { get; set; } = 0;
            /// <summary>檔案中提供的名稱（僅供顯示／除錯，不用於對應）</summary>
            public string Name { get; set; }
        }

        public class ParseResult
        {
            public List<Row> Rows { get; } = new();
            public int TotalRead { get; set; }
            public int Skipped   { get; set; }
            public string DetectedSource { get; set; } = "";
        }

        public const string DialogFilter =
            "支援格式 (*.csv;*.txt;*.tsv;*.xlsx;*.xls)|*.csv;*.txt;*.tsv;*.xlsx;*.xls" +
            "|Excel (*.xlsx;*.xls)|*.xlsx;*.xls" +
            "|CSV (*.csv)|*.csv" +
            "|文字檔 (*.txt;*.tsv)|*.txt;*.tsv" +
            "|所有檔案 (*.*)|*.*";

        public static ParseResult ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("找不到檔案", path);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".xlsx" or ".xls" => ParseXlsx(path),
                _                 => ParseDelimited(path),
            };
        }

        // ─────────────────────────────────────────────
        // CSV / TXT / TSV
        // ─────────────────────────────────────────────
        private static ParseResult ParseDelimited(string path)
        {
            string content = ReadTextSmart(path);
            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length == 0) return new ParseResult { DetectedSource = "空檔案" };

            char[] delimiters = { ',', '\t', ';', '|' };
            string sample = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
            char delim = delimiters
                .Select(d => (d, count: sample.Count(c => c == d)))
                .OrderByDescending(t => t.count)
                .First().d;

            var rows = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(delim).Select(c => c.Trim().Trim('"')).ToArray())
                .ToList();

            int colId, colQty, colType, colName;
            int dataStart = DetectHeader(rows[0], out colId, out colQty, out colType, out colName) ? 1 : 0;
            if (dataStart == 0)
            {
                colId   = 0;
                colQty  = rows[0].Length >= 2 ? 1 : -1;
                colType = rows[0].Length >= 3 ? 2 : -1;
                colName = -1;
            }

            var result = new ParseResult { DetectedSource = $"分隔符 '{(delim == '\t' ? "\\t" : delim.ToString())}'，共 {rows.Count} 行" };
            for (int i = dataStart; i < rows.Count; i++)
            {
                result.TotalRead++;
                var cells = rows[i];
                string idStr = colId >= 0 && colId < cells.Length ? cells[colId] : "";
                if (!int.TryParse(idStr, out int id)) { result.Skipped++; continue; }

                int qty = 1;
                if (colQty >= 0 && colQty < cells.Length && int.TryParse(cells[colQty], out int q)) qty = Math.Max(1, q);

                int type = 0;
                if (colType >= 0 && colType < cells.Length && int.TryParse(cells[colType], out int t)) type = Math.Max(0, t);

                string nm = colName >= 0 && colName < cells.Length ? cells[colName] : null;

                result.Rows.Add(new Row { Id = id, Qty = qty, Type = type, Name = nm });
            }
            return result;
        }

        private static string ReadTextSmart(string path)
        {
            try
            {
                using var sr = new StreamReader(path, Encoding.UTF8, true);
                string txt = sr.ReadToEnd();
                if (!txt.Contains('\uFFFD')) return txt;
            }
            catch { /* fall through */ }

            try
            {
                var enc = Encoding.GetEncoding(950);
                return File.ReadAllText(path, enc);
            }
            catch
            {
                return File.ReadAllText(path, Encoding.Default);
            }
        }

        // ─────────────────────────────────────────────
        // XLSX
        // ─────────────────────────────────────────────
        private static ParseResult ParseXlsx(string path)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var pkg = new ExcelPackage(new FileInfo(path));
            var ws = pkg.Workbook.Worksheets.FirstOrDefault();
            if (ws == null || ws.Dimension == null)
                return new ParseResult { DetectedSource = "Excel 無資料" };

            int rowCnt = ws.Dimension.End.Row;
            int colCnt = ws.Dimension.End.Column;

            var headerRow = Enumerable.Range(1, colCnt)
                .Select(c => ws.Cells[1, c].Text?.Trim() ?? "")
                .ToArray();
            int colId, colQty, colType, colName;
            int dataStart = DetectHeader(headerRow, out colId, out colQty, out colType, out colName) ? 2 : 1;
            if (dataStart == 1)
            {
                colId   = 0;
                colQty  = colCnt >= 2 ? 1 : -1;
                colType = colCnt >= 3 ? 2 : -1;
                colName = -1;
            }

            var result = new ParseResult { DetectedSource = $"Excel 工作表 '{ws.Name}'，共 {rowCnt} 列" };
            for (int r = dataStart; r <= rowCnt; r++)
            {
                result.TotalRead++;
                string idStr = ws.Cells[r, colId + 1].Text?.Trim() ?? "";
                if (!int.TryParse(idStr, out int id)) { result.Skipped++; continue; }

                int qty = 1;
                if (colQty >= 0)
                {
                    string qStr = ws.Cells[r, colQty + 1].Text?.Trim() ?? "";
                    if (int.TryParse(qStr, out int q)) qty = Math.Max(1, q);
                }

                int type = 0;
                if (colType >= 0)
                {
                    string tStr = ws.Cells[r, colType + 1].Text?.Trim() ?? "";
                    if (int.TryParse(tStr, out int t)) type = Math.Max(0, t);
                }

                string nm = colName >= 0 ? (ws.Cells[r, colName + 1].Text?.Trim()) : null;

                result.Rows.Add(new Row { Id = id, Qty = qty, Type = type, Name = nm });
            }
            return result;
        }

        // ─────────────────────────────────────────────
        // 標題偵測
        // ─────────────────────────────────────────────
        private static readonly string[] IdKeys   = { "id", "itemid", "編號", "道具編號", "道具id", "物品編號", "物品id" };
        private static readonly string[] QtyKeys  = { "qty", "quantity", "數量", "個數", "amount", "count" };
        private static readonly string[] TypeKeys = { "type", "類型", "種類" };
        private static readonly string[] NameKeys = { "name", "itemname", "名稱", "道具名稱", "物品名稱" };

        private static bool DetectHeader(string[] header, out int colId, out int colQty, out int colType, out int colName)
        {
            colId = colQty = colType = colName = -1;
            if (header == null || header.Length == 0) return false;

            for (int i = 0; i < header.Length; i++)
            {
                string h = (header[i] ?? "").ToLowerInvariant().Replace(" ", "");
                if (string.IsNullOrEmpty(h)) continue;

                if (colId   < 0 && IdKeys  .Any(k => h == k))                   colId   = i;
                if (colQty  < 0 && QtyKeys .Any(k => h == k))                   colQty  = i;
                if (colType < 0 && TypeKeys.Any(k => h == k))                   colType = i;
                if (colName < 0 && NameKeys.Any(k => h == k))                   colName = i;
            }
            // 二輪：包含關鍵字
            if (colId < 0)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    string h = (header[i] ?? "").ToLowerInvariant();
                    if (IdKeys.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    { colId = i; break; }
                }
            }
            if (colQty < 0)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    string h = (header[i] ?? "").ToLowerInvariant();
                    if (QtyKeys.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    { colQty = i; break; }
                }
            }
            if (colType < 0)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    string h = (header[i] ?? "").ToLowerInvariant();
                    if (TypeKeys.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    { colType = i; break; }
                }
            }
            if (colName < 0)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    string h = (header[i] ?? "").ToLowerInvariant();
                    if (NameKeys.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    { colName = i; break; }
                }
            }
            return colId >= 0;
        }
    }
}
