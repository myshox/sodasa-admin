using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    public class GameDataManager
    {
        private static GameDataManager _instance;
        public static GameDataManager Instance => _instance ??= new GameDataManager();

        private List<ItemInfo> _items = new List<ItemInfo>();
        private List<ItemInfo> _pets  = new List<ItemInfo>();

        public bool ItemsLoaded => _items.Count > 0;
        public bool PetsLoaded  => _pets.Count  > 0;
        public int  ItemCount   => _items.Count;
        public int  PetCount    => _pets.Count;

        // 自動偵測 Excel 欄位順序：
        //   若第 1 欄能解析為 int → 格式為 [ID, Name, ...]
        //   否則 → 格式為 [Name, ID, ...] 或 [Name, Desc, ID]
        public string LoadItems(string filePath)
        {
            try
            {
                _items.Clear();
                using var pkg = new ExcelPackage(new FileInfo(filePath));
                var ws = pkg.Workbook.Worksheets[0];
                if (ws?.Dimension == null) return "工作表為空";

                int cols    = ws.Dimension.Columns;
                bool idFirst  = IsFirstColumnNumeric(ws);
                // 3欄且第3欄為ID（如：名稱, 說明, 編號）
                bool nameDescId = !idFirst && cols >= 3 && IsColumnNumeric(ws, 3);

                for (int r = 2; r <= ws.Dimension.Rows; r++)
                {
                    string name, idStr, desc = "";
                    if (idFirst)
                    {
                        // [ID, Name, ...]
                        idStr = ws.Cells[r, 1].Text?.Trim();
                        name  = ws.Cells[r, 2].Text?.Trim();
                        desc  = cols >= 3 ? ws.Cells[r, 3].Text?.Trim() ?? "" : "";
                    }
                    else if (nameDescId)
                    {
                        // [Name, Description, ID]  ← items.xlsx 實際格式
                        name  = ws.Cells[r, 1].Text?.Trim();
                        desc  = ws.Cells[r, 2].Text?.Trim() ?? "";
                        idStr = ws.Cells[r, 3].Text?.Trim();
                    }
                    else
                    {
                        // [Name, ID]
                        name  = ws.Cells[r, 1].Text?.Trim();
                        idStr = ws.Cells[r, 2].Text?.Trim();
                    }
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!int.TryParse(idStr, out int id)) continue;
                    _items.Add(new ItemInfo { Name = name, Id = id, Description = desc, IsPet = false });
                }
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public string LoadPets(string filePath)
        {
            try
            {
                _pets.Clear();
                using var pkg = new ExcelPackage(new FileInfo(filePath));
                var ws = pkg.Workbook.Worksheets[0];
                if (ws?.Dimension == null) return "工作表為空";

                int cols     = ws.Dimension.Columns;
                bool idFirst = IsFirstColumnNumeric(ws);

                for (int r = 2; r <= ws.Dimension.Rows; r++)
                {
                    string name, desc = "", idStr;

                    if (idFirst)
                    {
                        // [ID, Name] 或 [ID, Name, Desc]
                        idStr = ws.Cells[r, 1].Text?.Trim();
                        name  = ws.Cells[r, 2].Text?.Trim();
                        desc  = cols >= 3 ? ws.Cells[r, 3].Text?.Trim() ?? "" : "";
                    }
                    else if (cols >= 3)
                    {
                        // [Name, Desc, ID]
                        name  = ws.Cells[r, 1].Text?.Trim();
                        desc  = ws.Cells[r, 2].Text?.Trim() ?? "";
                        idStr = ws.Cells[r, 3].Text?.Trim();
                    }
                    else
                    {
                        // [Name, ID] — 只有兩欄
                        name  = ws.Cells[r, 1].Text?.Trim();
                        idStr = ws.Cells[r, 2].Text?.Trim();
                    }

                    if (string.IsNullOrEmpty(name)) continue;
                    if (!int.TryParse(idStr, out int id)) continue;
                    _pets.Add(new ItemInfo { Name = name, Id = id, Description = desc, IsPet = true });
                }
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private static bool IsFirstColumnNumeric(ExcelWorksheet ws) => IsColumnNumeric(ws, 1);

        private static bool IsColumnNumeric(ExcelWorksheet ws, int col)
        {
            int numeric = 0, total = 0;
            for (int r = 2; r <= Math.Min(5, ws.Dimension.Rows); r++)
            {
                string v = ws.Cells[r, col].Text?.Trim();
                if (!string.IsNullOrEmpty(v)) { total++; if (int.TryParse(v, out _)) numeric++; }
            }
            return total > 0 && numeric * 2 >= total;
        }

        // ── 搜尋方法 ───────────────────────────────────
        // 空字串 → 顯示前 500 筆（避免清單過大造成卡頓）
        // 有關鍵字 → 名稱 / 編號 / 說明 三欄全都搜尋，顯示所有符合結果
        public List<ItemInfo> SearchItems(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _items.Take(500).ToList();
            return _items
                .Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || i.Id.ToString().Contains(query)
                         || (!string.IsNullOrEmpty(i.Description) &&
                             i.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public List<ItemInfo> SearchPets(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _pets.Take(500).ToList();
            return _pets
                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || p.Id.ToString().Contains(query)
                         || (!string.IsNullOrEmpty(p.Description) &&
                             p.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        // 相容舊呼叫（保留 maxResults 參數但不限制搜尋結果）
        public List<ItemInfo> SearchItems(string query, int maxResults) => SearchItems(query);
        public List<ItemInfo> SearchPets(string query, int maxResults)  => SearchPets(query);

        /// <summary>取得全部道具（不限筆數，供翻頁功能使用）</summary>
        public List<ItemInfo> GetAllItems() => new List<ItemInfo>(_items);

        /// <summary>取得全部寵物（不限筆數）</summary>
        public List<ItemInfo> GetAllPets() => new List<ItemInfo>(_pets);
        public ItemInfo FindItemById(int id) => _items.FirstOrDefault(i => i.Id == id)
                                             ?? _pets.FirstOrDefault(i => i.Id == id);

        public ItemInfo GetItemById(int id) => _items.FirstOrDefault(i => i.Id == id);
        public ItemInfo GetPetById(int id)  => _pets.FirstOrDefault(p => p.Id == id);

        // 取得預覽（用於設定頁顯示）
        public List<ItemInfo> PreviewItems(int count = 5) => _items.Take(count).ToList();
        public List<ItemInfo> PreviewPets(int count = 5)  => _pets.Take(count).ToList();
    }
}
