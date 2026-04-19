using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 從 CSV / TXT / TSV / XLSX / XLS 解析「道具清單」。
    /// 可辨識的欄位：id / qty / type / name
    ///   - 標題列偵測：等值或包含關鍵字（id, 編號, qty, 數量, type, 類型, name, 名稱…）
    ///   - 找不到標題 → 第 1 欄=Id，第 2 欄=Qty（缺則 1），第 3 欄=Type（缺則 0）
    /// ID/Qty/Type 解析寬鬆：可接受千分位逗號、小數點 (1001.0)、前置 '、科學記號。
    /// </summary>
    internal static class ItemListImporter
    {
        public class Row
        {
            public int Id   { get; set; }
            public int Qty  { get; set; } = 1;
            public int Type { get; set; } = 0;
            public string Name { get; set; }
        }

        public class SkippedRow
        {
            public int LineNo { get; set; }
            public string Reason { get; set; }
            public string Raw { get; set; }
        }

        public class ParseResult
        {
            public List<Row> Rows { get; } = new();
            public int TotalRead { get; set; }
            public int Skipped   { get; set; }
            public List<SkippedRow> SkippedDetails { get; } = new();
            public string DetectedSource { get; set; } = "";
            public string DetectedColumns { get; set; } = "";
        }

        public const string DialogFilter =
            "支援格式 (*.csv;*.txt;*.tsv;*.xlsx;*.xls)|*.csv;*.txt;*.tsv;*.xlsx;*.xls" +
            "|Excel (*.xlsx;*.xls)|*.xlsx;*.xls" +
            "|CSV (*.csv)|*.csv" +
            "|文字檔 (*.txt;*.tsv)|*.txt;*.tsv" +
            "|所有檔案 (*.*)|*.*";

        /// <summary>
        /// 解析檔案。Excel 多分頁時：
        ///   - sheetName == null  → 讀第一個分頁
        ///   - sheetName == "*"   → 合併所有分頁
        ///   - 其他              → 讀指定分頁
        /// </summary>
        public static ParseResult ParseFile(string path, string sheetName = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("找不到檔案", path);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".xlsx" or ".xls" => ParseXlsx(path, sheetName),
                _                 => ParseDelimited(path),
            };
        }

        /// <summary>列出 Excel 所有分頁名稱（非 Excel 回傳空陣列）</summary>
        public static string[] GetXlsxSheetNames(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls") return Array.Empty<string>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var pkg = new ExcelPackage(new FileInfo(path));
            return pkg.Workbook.Worksheets.Select(w => w.Name).ToArray();
        }

        // ─────────────────────────────────────────────
        // 寬鬆整數解析：接受 "1,234,567"、"1001.0"、前置 '、科學記號、空白
        // ─────────────────────────────────────────────
        public static bool TryParseLooseInt(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string s = raw.Trim();
            // 去掉 Excel 文字模式的前置單引號
            if (s.StartsWith("'")) s = s.Substring(1).Trim();
            // 去除常見全形空白與千分位逗號、底線
            s = s.Replace("\u3000", "").Replace(" ", "").Replace(",", "").Replace("_", "");
            if (s.Length == 0) return false;

            // 純整數
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long li))
            {
                if (li < int.MinValue || li > int.MaxValue) return false;
                value = (int)li;
                return true;
            }

            // 小數 / 科學記號 → 四捨五入
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                if (double.IsNaN(d) || double.IsInfinity(d)) return false;
                if (d < int.MinValue || d > int.MaxValue) return false;
                // 只接受實質為整數的數字（避免 1.7 變 2 之類誤差）
                double rounded = Math.Round(d);
                if (Math.Abs(d - rounded) > 1e-6) return false;
                value = (int)rounded;
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────
        // CSV / TXT / TSV
        // ─────────────────────────────────────────────
        private static ParseResult ParseDelimited(string path)
        {
            string content = ReadTextSmart(path);
            // 移除 BOM
            if (content.Length > 0 && content[0] == '\uFEFF') content = content.Substring(1);

            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length == 0) return new ParseResult { DetectedSource = "空檔案" };

            // 偵測分隔符：用「平均每行的分隔符數量」中位數最高者
            char[] candidates = { ',', '\t', ';', '|' };
            char delim = ',';
            int best = -1;
            foreach (var d in candidates)
            {
                int total = 0;
                foreach (var l in lines)
                {
                    if (string.IsNullOrWhiteSpace(l)) continue;
                    total += CountChar(l, d);
                }
                if (total > best) { best = total; delim = d; }
            }

            // 將每行切欄（保留行號，方便回報）
            var rowsWithNo = new List<(int LineNo, string Raw, string[] Cells)>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = SplitCsvLine(lines[i], delim);
                rowsWithNo.Add((i + 1, lines[i], cells));
            }
            if (rowsWithNo.Count == 0)
                return new ParseResult { DetectedSource = "檔案無有效內容" };

            int colId, colQty, colType, colName;
            int dataStart = DetectHeader(rowsWithNo[0].Cells, out colId, out colQty, out colType, out colName) ? 1 : 0;
            if (dataStart == 0)
            {
                colId   = 0;
                colQty  = rowsWithNo[0].Cells.Length >= 2 ? 1 : -1;
                colType = rowsWithNo[0].Cells.Length >= 3 ? 2 : -1;
                colName = -1;
            }

            var result = new ParseResult
            {
                DetectedSource  = $"分隔符 '{(delim == '\t' ? "\\t" : delim.ToString())}'，共 {rowsWithNo.Count} 行（含標題 {dataStart}）",
                DetectedColumns = $"Id=第{colId + 1}欄, Qty={(colQty < 0 ? "—" : (colQty + 1).ToString())}欄, Type={(colType < 0 ? "—" : (colType + 1).ToString())}欄, Name={(colName < 0 ? "—" : (colName + 1).ToString())}欄"
            };

            for (int i = dataStart; i < rowsWithNo.Count; i++)
            {
                result.TotalRead++;
                var (lineNo, raw, cells) = rowsWithNo[i];
                string idStr = colId >= 0 && colId < cells.Length ? cells[colId] : "";
                if (!TryParseLooseInt(idStr, out int id))
                {
                    result.Skipped++;
                    result.SkippedDetails.Add(new SkippedRow {
                        LineNo = lineNo,
                        Reason = string.IsNullOrWhiteSpace(idStr) ? "ID 欄為空" : $"ID '{idStr}' 無法解析為整數",
                        Raw    = raw
                    });
                    continue;
                }

                int qty = 1;
                if (colQty >= 0 && colQty < cells.Length && TryParseLooseInt(cells[colQty], out int q))
                    qty = Math.Max(1, q);

                int type = 0;
                if (colType >= 0 && colType < cells.Length && TryParseLooseInt(cells[colType], out int t))
                    type = Math.Max(0, t);

                string nm = colName >= 0 && colName < cells.Length ? cells[colName] : null;

                result.Rows.Add(new Row { Id = id, Qty = qty, Type = type, Name = nm });
            }
            return result;
        }

        // 簡易 CSV 切欄：支援雙引號包夾（含 "" 轉義）；若無引號則 Split + Trim
        private static string[] SplitCsvLine(string line, char delim)
        {
            if (line.IndexOf('"') < 0)
                return line.Split(delim).Select(c => c.Trim()).ToArray();

            var list = new List<string>();
            var cur = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuote)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else if (c == '"') inQuote = false;
                    else cur.Append(c);
                }
                else
                {
                    if (c == '"') inQuote = true;
                    else if (c == delim) { list.Add(cur.ToString().Trim()); cur.Clear(); }
                    else cur.Append(c);
                }
            }
            list.Add(cur.ToString().Trim());
            return list.ToArray();
        }

        private static int CountChar(string s, char c)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == c) n++;
            return n;
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
        private static ParseResult ParseXlsx(string path, string sheetName)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var pkg = new ExcelPackage(new FileInfo(path));

            // 多分頁合併
            if (sheetName == "*")
            {
                var combined = new ParseResult();
                int totalRows = 0;
                var srcs = new List<string>();
                foreach (var w in pkg.Workbook.Worksheets)
                {
                    if (w.Dimension == null) continue;
                    var part = ParseXlsxSheet(w);
                    combined.Rows.AddRange(part.Rows);
                    combined.TotalRead += part.TotalRead;
                    combined.Skipped   += part.Skipped;
                    foreach (var sd in part.SkippedDetails) combined.SkippedDetails.Add(sd);
                    totalRows += part.Rows.Count;
                    srcs.Add($"'{w.Name}'({part.Rows.Count})");
                    if (string.IsNullOrEmpty(combined.DetectedColumns)) combined.DetectedColumns = part.DetectedColumns;
                }
                combined.DetectedSource = $"Excel 合併 {pkg.Workbook.Worksheets.Count} 個分頁：{string.Join(", ", srcs)}";
                return combined;
            }

            ExcelWorksheet ws;
            if (!string.IsNullOrEmpty(sheetName))
                ws = pkg.Workbook.Worksheets[sheetName] ?? pkg.Workbook.Worksheets.FirstOrDefault();
            else
                ws = pkg.Workbook.Worksheets.FirstOrDefault();

            if (ws == null || ws.Dimension == null)
                return new ParseResult { DetectedSource = "Excel 無資料" };
            return ParseXlsxSheet(ws);
        }

        private static ParseResult ParseXlsxSheet(ExcelWorksheet ws)
        {

            int rowCnt = ws.Dimension.End.Row;
            int colCnt = ws.Dimension.End.Column;

            var headerRow = Enumerable.Range(1, colCnt)
                .Select(c => (ws.Cells[1, c].Text ?? "").Trim())
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

            var result = new ParseResult
            {
                DetectedSource  = $"Excel 工作表 '{ws.Name}'，共 {rowCnt} 列（含標題 {dataStart - 1}）",
                DetectedColumns = $"Id=第{colId + 1}欄, Qty={(colQty < 0 ? "—" : (colQty + 1).ToString())}欄, Type={(colType < 0 ? "—" : (colType + 1).ToString())}欄, Name={(colName < 0 ? "—" : (colName + 1).ToString())}欄"
            };

            for (int r = dataStart; r <= rowCnt; r++)
            {
                // 整列空白 → 直接略過、不計入
                if (IsXlsxRowEmpty(ws, r, colCnt)) continue;

                result.TotalRead++;
                string idStr = ReadXlsxCell(ws, r, colId + 1);
                if (!TryParseLooseInt(idStr, out int id))
                {
                    result.Skipped++;
                    string raw = string.Join(" | ",
                        Enumerable.Range(1, colCnt).Select(c => ReadXlsxCell(ws, r, c)));
                    result.SkippedDetails.Add(new SkippedRow {
                        LineNo = r,
                        Reason = string.IsNullOrWhiteSpace(idStr) ? "ID 欄為空" : $"ID '{idStr}' 無法解析為整數",
                        Raw    = raw
                    });
                    continue;
                }

                int qty = 1;
                if (colQty >= 0 && TryParseLooseInt(ReadXlsxCell(ws, r, colQty + 1), out int q))
                    qty = Math.Max(1, q);

                int type = 0;
                if (colType >= 0 && TryParseLooseInt(ReadXlsxCell(ws, r, colType + 1), out int t))
                    type = Math.Max(0, t);

                string nm = colName >= 0 ? ReadXlsxCell(ws, r, colName + 1) : null;

                result.Rows.Add(new Row { Id = id, Qty = qty, Type = type, Name = nm });
            }
            return result;
        }

        // 優先用 .Value，避免 Excel 格式（千分位、千分位+小數）讓 .Text 失準
        private static string ReadXlsxCell(ExcelWorksheet ws, int r, int c)
        {
            var cell = ws.Cells[r, c];
            object v = cell.Value;
            if (v == null) return "";
            if (v is double dv) return dv.ToString("R", CultureInfo.InvariantCulture);
            if (v is float fv)  return fv.ToString("R", CultureInfo.InvariantCulture);
            if (v is decimal mv) return mv.ToString(CultureInfo.InvariantCulture);
            if (v is int iv) return iv.ToString(CultureInfo.InvariantCulture);
            if (v is long lv) return lv.ToString(CultureInfo.InvariantCulture);
            if (v is bool bv) return bv ? "1" : "0";
            return v.ToString().Trim();
        }

        private static bool IsXlsxRowEmpty(ExcelWorksheet ws, int r, int colCnt)
        {
            for (int c = 1; c <= colCnt; c++)
            {
                var v = ws.Cells[r, c].Value;
                if (v != null && !string.IsNullOrWhiteSpace(v.ToString())) return false;
            }
            return true;
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

                if (colId   < 0 && IdKeys  .Any(k => h == k)) colId   = i;
                if (colQty  < 0 && QtyKeys .Any(k => h == k)) colQty  = i;
                if (colType < 0 && TypeKeys.Any(k => h == k)) colType = i;
                if (colName < 0 && NameKeys.Any(k => h == k)) colName = i;
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
