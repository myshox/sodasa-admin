using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 從 CSV / TXT / XLSX 解析批量發送的目標玩家清單。
    /// 自動偵測標題列（Name/cdkey/識別編號 / OnlineName/角色名/名稱），
    /// 找不到標題則使用第 1、2 欄 (cdkey, OnlineName) 順序。
    /// </summary>
    internal static class PlayerListImporter
    {
        public class Row
        {
            /// <summary>識別編號（DB csalogin.Name / cdkey）</summary>
            public string Cdkey { get; set; } = "";
            /// <summary>顯示名稱（DB csalogin.OnlineName）</summary>
            public string OnlineName { get; set; } = "";
        }

        public class ParseResult
        {
            public List<Row> Rows { get; } = new();
            public int TotalRead { get; set; }
            public int Skipped { get; set; }
            public string DetectedSource { get; set; } = "";
        }

        /// <summary>常見副檔名 Filter（給 OpenFileDialog 用）。</summary>
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
            // 偵測編碼：UTF-8 (帶 BOM 或不帶) → fallback Big5/系統預設
            string content = ReadTextSmart(path);
            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            if (lines.Length == 0) return new ParseResult { DetectedSource = "空檔案" };

            // 偵測分隔符：以第一行非空字元判斷
            char[] delimiters = { ',', '\t', ';', '|' };
            string sample = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
            char delim = delimiters
                .Select(d => (d, count: sample.Count(c => c == d)))
                .OrderByDescending(t => t.count)
                .First().d;

            // 將每行 split
            var rows = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Split(delim).Select(c => c.Trim().Trim('"')).ToArray())
                .ToList();

            // 偵測標題列
            int colCdkey, colName;
            int dataStart = DetectHeader(rows[0], out colCdkey, out colName) ? 1 : 0;
            if (dataStart == 0)
            {
                // 沒標題 → 預設 第 1 欄=cdkey, 第 2 欄=OnlineName（若只有一欄則只有 cdkey）
                colCdkey = 0;
                colName  = rows[0].Length >= 2 ? 1 : -1;
            }

            var result = new ParseResult { DetectedSource = $"分隔符 '{(delim == '\t' ? "\\t" : delim.ToString())}'，共 {rows.Count} 行" };
            for (int i = dataStart; i < rows.Count; i++)
            {
                result.TotalRead++;
                var cells = rows[i];
                string cdkey = colCdkey < cells.Length ? cells[colCdkey] : "";
                string name  = colName  >= 0 && colName  < cells.Length ? cells[colName]  : "";
                if (string.IsNullOrWhiteSpace(cdkey) && string.IsNullOrWhiteSpace(name))
                { result.Skipped++; continue; }
                result.Rows.Add(new Row { Cdkey = cdkey, OnlineName = name });
            }
            return result;
        }

        private static string ReadTextSmart(string path)
        {
            // 先試 UTF-8（含 BOM 自動偵測）
            try
            {
                using var sr = new StreamReader(path, Encoding.UTF8, true);
                string txt = sr.ReadToEnd();
                if (!txt.Contains('\uFFFD')) return txt;   // 沒亂碼 → 回傳
            }
            catch { /* fall through */ }

            // 退回 Big5（CodePagesEncodingProvider 已於 EXE Program 啟動時註冊）
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
        // XLSX / XLS（透過 EPPlus）
        // ─────────────────────────────────────────────
        private static ParseResult ParseXlsx(string path)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var pkg = new ExcelPackage(new FileInfo(path));
            var ws = pkg.Workbook.Worksheets.FirstOrDefault();
            if (ws == null || ws.Dimension == null)
                return new ParseResult { DetectedSource = "Excel 檔案無資料" };

            int rowCnt = ws.Dimension.End.Row;
            int colCnt = ws.Dimension.End.Column;

            // 把第一列轉成字串陣列以偵測標題
            var headerRow = Enumerable.Range(1, colCnt)
                .Select(c => ws.Cells[1, c].Text?.Trim() ?? "")
                .ToArray();
            int colCdkey, colName;
            int dataStart = DetectHeader(headerRow, out colCdkey, out colName) ? 2 : 1;
            if (dataStart == 1)
            {
                colCdkey = 0;
                colName  = colCnt >= 2 ? 1 : -1;
            }

            var result = new ParseResult { DetectedSource = $"Excel 工作表 '{ws.Name}'，共 {rowCnt} 列" };
            for (int r = dataStart; r <= rowCnt; r++)
            {
                result.TotalRead++;
                string cdkey = ws.Cells[r, colCdkey + 1].Text?.Trim() ?? "";
                string name  = colName >= 0 ? (ws.Cells[r, colName + 1].Text?.Trim() ?? "") : "";
                if (string.IsNullOrWhiteSpace(cdkey) && string.IsNullOrWhiteSpace(name))
                { result.Skipped++; continue; }
                result.Rows.Add(new Row { Cdkey = cdkey, OnlineName = name });
            }
            return result;
        }

        // ─────────────────────────────────────────────
        // 標題偵測：常見關鍵字（不分大小寫，模糊匹配）
        // ─────────────────────────────────────────────
        private static readonly string[] CdkeyKeys = {
            "name", "cdkey", "account", "uid", "識別編號", "識別", "編號", "帳號", "主帳號"
        };
        private static readonly string[] NameKeys = {
            "onlinename", "char", "charname", "character", "nickname", "名稱", "角色名", "角色名稱", "暱稱", "名字"
        };

        private static bool DetectHeader(string[] header, out int colCdkey, out int colName)
        {
            colCdkey = -1;
            colName  = -1;
            if (header == null || header.Length == 0) return false;

            for (int i = 0; i < header.Length; i++)
            {
                string h = (header[i] ?? "").ToLowerInvariant().Replace(" ", "");
                if (string.IsNullOrEmpty(h)) continue;

                // 完全匹配優先
                if (colCdkey < 0 && CdkeyKeys.Any(k => h.Equals(k, StringComparison.OrdinalIgnoreCase)))
                    colCdkey = i;
                if (colName < 0 && NameKeys.Any(k => h.Equals(k, StringComparison.OrdinalIgnoreCase)))
                    colName = i;
            }
            // 二輪：包含關鍵字
            if (colCdkey < 0)
            {
                for (int i = 0; i < header.Length; i++)
                {
                    string h = (header[i] ?? "").ToLowerInvariant();
                    if (CdkeyKeys.Any(k => h.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    { colCdkey = i; break; }
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
            return colCdkey >= 0 || colName >= 0;
        }
    }
}
