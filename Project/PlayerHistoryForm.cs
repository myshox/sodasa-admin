using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class PlayerHistoryForm : Form
    {
        private TextBox      _searchBox;
        private Button       _btnSearch;
        private Label        _statusLbl;
        private TabControl   _tabs;
        private DataGridView _dgvTrade, _dgvStreet, _dgvShop, _dgvSpeed, _dgvCost, _dgvStorage;
        private string       _currentAccount = "";

        public PlayerHistoryForm()
        {
            Text          = "🔍 玩家活動歷程";
            Size          = new Size(1200, 700);
            MinimumSize   = new Size(900, 520);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
        }

        // 可讓 MainForm 直接帶入帳號
        public PlayerHistoryForm(string account) : this()
        {
            _searchBox.Text = account;
            if (!string.IsNullOrWhiteSpace(account))
                _ = LoadAllAsync(account.Trim());
        }

        private void BuildUI()
        {
            // ── Header ──────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  🔍  玩家活動歷程  —  交易 / 攤位 / 商城 / 速度 / 消費 / 倉庫",
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontHeader,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 搜尋列 ──────────────────────────────────────────────
            var toolBar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Theme.BgCard };
            toolBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });

            _searchBox = new TextBox
            {
                PlaceholderText = "輸入帳號（cdkey）或角色名稱…",
                Location = new Point(14, 13), Width = 320,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = SearchAndLoadAsync(); };

            _btnSearch = Theme.MakePrimaryButton("🔍 查詢", 90, 28);
            _btnSearch.Location = new Point(342, 13);
            _btnSearch.Click   += (s, e) => _ = SearchAndLoadAsync();

            _statusLbl = new Label
            {
                AutoSize  = true, Location = new Point(446, 17),
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Text      = "請輸入帳號或角色名稱後查詢"
            };

            toolBar.Controls.AddRange(new Control[] { _searchBox, _btnSearch, _statusLbl });

            // ── TabControl ─────────────────────────────────────────
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = Theme.FontBody,
            };
            _tabs.DrawMode = TabDrawMode.Normal;

            _dgvTrade   = MakeDgv(); SetupTradeCols();
            _dgvStreet  = MakeDgv(); SetupStreetCols();
            _dgvShop    = MakeDgv(); SetupShopCols();
            _dgvSpeed   = MakeDgv(); SetupSpeedCols();
            _dgvCost    = MakeDgv(); SetupCostCols();
            _dgvStorage = MakeDgv(); SetupStorageCols();

            _tabs.TabPages.Add(MakeTabPage("📊 交易紀錄",  _dgvTrade));
            _tabs.TabPages.Add(MakeTabPage("🏪 攤位交易",  _dgvStreet));
            _tabs.TabPages.Add(MakeTabPage("🛒 商城購買",  _dgvShop));
            _tabs.TabPages.Add(MakeTabPage("⚡ 速度日誌",  _dgvSpeed));
            _tabs.TabPages.Add(MakeTabPage("💸 消費記錄",  _dgvCost));
            _tabs.TabPages.Add(MakeTabPage("🏦 倉庫",      _dgvStorage));

            // ── 組合 ────────────────────────────────────────────────
            Controls.Add(_tabs);
            Controls.Add(toolBar);
            Controls.Add(header);
        }

        private static TabPage MakeTabPage(string title, DataGridView dgv)
        {
            var p = new TabPage(title) { BackColor = Theme.BgPage, Padding = new Padding(0) };
            dgv.Dock = DockStyle.Fill;
            p.Controls.Add(dgv);
            return p;
        }

        private static DataGridView MakeDgv()
        {
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            return dgv;
        }

        private static DataGridViewTextBoxColumn Col(string name, string header, int w, bool fill = false)
        {
            var c = new DataGridViewTextBoxColumn { Name = name, HeaderText = header };
            if (fill) c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            else      c.Width = w;
            return c;
        }

        private void SetupTradeCols()
        {
            _dgvTrade.Columns.AddRange(
                Col("time",      "時間",     150),
                Col("dir",       "方向",      72),
                Col("otherAcc",  "對方帳號", 140),
                Col("otherName", "對方角色", 110),
                Col("items",     "道具",     160, fill: true),
                Col("pets",      "寵物",     120),
                Col("gold",      "金幣",      90));
        }

        private void SetupStreetCols()
        {
            _dgvStreet.Columns.AddRange(
                Col("time",      "時間",     150),
                Col("role",      "方向",      60),
                Col("itemName",  "物品名稱", 180, fill: true),
                Col("num",       "數量",      60),
                Col("price",     "價格",      90),
                Col("otherAcc",  "對方帳號", 130),
                Col("otherName", "對方角色", 110));
        }

        private void SetupShopCols()
        {
            _dgvShop.Columns.AddRange(
                Col("time",     "時間",     150),
                Col("shopType", "商店",      90),
                Col("itemName", "物品名稱", 200, fill: true),
                Col("num",      "數量",      60),
                Col("cost",     "花費",      90));
        }

        private void SetupSpeedCols()
        {
            _dgvSpeed.Columns.AddRange(
                Col("time",      "時間",     160),
                Col("speedTime", "加速次數",  100),
                Col("speedCnt",  "累計",      100));
        }

        private void SetupCostCols()
        {
            _dgvCost.Columns.AddRange(
                Col("time",  "時間",    150),
                Col("name",  "項目",    240, fill: true),
                Col("point", "點數",     90));
        }

        private void SetupStorageCols()
        {
            _dgvStorage.Columns.AddRange(
                Col("getTime",    "取得時間",  145),
                Col("expireTime", "到期時間",  145),
                Col("itemId",     "道具ID",     70),
                Col("itemName",   "道具名稱",  180, fill: true),
                Col("typecode",   "類型",        90),
                Col("pile",       "數量",         60),
                Col("atk",        "攻擊",         60),
                Col("def",        "防禦",         60),
                Col("hp",         "HP",           60),
                Col("luck",       "幸運",         60),
                Col("locked",     "鎖定",         50));
        }

        // ── 搜尋（先用關鍵字找帳號）────────────────────────────────
        private async System.Threading.Tasks.Task SearchAndLoadAsync()
        {
            string kw = _searchBox.Text.Trim();
            if (string.IsNullOrEmpty(kw)) return;

            _btnSearch.Enabled = false;
            SetStatus("查詢中…");
            try
            {
                // 支援主帳號展開：若底下有多個角色，跳出選擇視窗
                var picked = await PlayerPickerHelper.PickAsync(this, kw);
                if (picked == null) { SetStatus("已取消"); return; }

                _searchBox.Text = picked.OnlineName.Length > 0 ? picked.OnlineName : picked.Account;
                await LoadAllAsync(picked.Account);
            }
            finally { _btnSearch.Enabled = true; }
        }

        private async System.Threading.Tasks.Task LoadAllAsync(string account)
        {
            _currentAccount = account;
            SetStatus($"載入「{account}」的歷程…");
            ClearAll();

            var db = DatabaseManager.Instance;

            // 交易紀錄
            var trades = await db.GetPlayerHistoryTradesAsync(account);
            foreach (var t in trades)
            {
                var row = _dgvTrade.Rows[_dgvTrade.Rows.Add()];
                row.Cells["time"].Value      = t.time;
                row.Cells["dir"].Value       = t.dir;
                row.Cells["otherAcc"].Value  = t.otherAcc;
                row.Cells["otherName"].Value = t.otherName;
                row.Cells["items"].Value     = t.items;
                row.Cells["pets"].Value      = t.pets;
                row.Cells["gold"].Value      = t.gold > 0 ? t.gold.ToString("N0") : "";
                if (t.dir.StartsWith("→"))
                    row.DefaultCellStyle.ForeColor = Theme.AccentOrange;
            }

            // 攤位交易
            var streets = await db.GetPlayerHistoryStreetAsync(account);
            foreach (var t in streets)
            {
                var row = _dgvStreet.Rows[_dgvStreet.Rows.Add()];
                row.Cells["time"].Value      = t.time;
                row.Cells["role"].Value      = t.role;
                row.Cells["itemName"].Value  = t.itemName;
                row.Cells["num"].Value       = t.num;
                row.Cells["price"].Value     = t.price.ToString("N0");
                row.Cells["otherAcc"].Value  = t.otherAcc;
                row.Cells["otherName"].Value = t.otherName;
                if (t.role == "賣出")
                    row.DefaultCellStyle.ForeColor = Theme.AccentGreen;
            }

            // 商城購買
            var shops = await db.GetPlayerHistoryShopAsync(account);
            foreach (var t in shops)
            {
                var row = _dgvShop.Rows[_dgvShop.Rows.Add()];
                row.Cells["time"].Value     = t.time;
                row.Cells["shopType"].Value = t.shopType;
                row.Cells["itemName"].Value = t.itemName;
                row.Cells["num"].Value      = t.num;
                row.Cells["cost"].Value     = t.cost.ToString("N0");
            }

            // 速度日誌
            var speeds = await db.GetPlayerHistorySpeedAsync(account);
            foreach (var t in speeds)
            {
                var row = _dgvSpeed.Rows[_dgvSpeed.Rows.Add()];
                row.Cells["time"].Value      = t.time;
                row.Cells["speedTime"].Value = t.speedTime;
                row.Cells["speedCnt"].Value  = t.speedCnt;
            }

            // 消費記錄
            var costs = await db.GetPlayerHistoryCostAsync(account);
            foreach (var t in costs)
            {
                var row = _dgvCost.Rows[_dgvCost.Rows.Add()];
                row.Cells["time"].Value  = t.time;
                row.Cells["name"].Value  = t.name;
                row.Cells["point"].Value = t.point.ToString("N0");
            }

            // 倉庫 (poolitem 全部)
            var storageItems = await db.GetPlayerStorageAsync(account, 1000);
            foreach (var t in storageItems)
            {
                var row = _dgvStorage.Rows[_dgvStorage.Rows.Add()];
                row.Cells["getTime"].Value    = t.GetTime;
                row.Cells["expireTime"].Value = t.ExpireTime.Length > 0 ? t.ExpireTime : "永久";
                row.Cells["itemId"].Value     = t.ItemId;
                row.Cells["itemName"].Value   = t.ItemName;
                row.Cells["typecode"].Value   = t.TypeCode;
                row.Cells["pile"].Value       = t.Pile > 1 ? t.Pile.ToString() : "";
                row.Cells["atk"].Value        = t.Atk  != 0 ? t.Atk.ToString()  : "";
                row.Cells["def"].Value        = t.Def  != 0 ? t.Def.ToString()  : "";
                row.Cells["hp"].Value         = t.Hp   != 0 ? t.Hp.ToString()   : "";
                row.Cells["luck"].Value       = t.Luck != 0 ? t.Luck.ToString() : "";
                row.Cells["locked"].Value     = t.Locked ? "🔒" : "";
            }

            SetStatus($"「{account}」｜交易 {trades.Count} ／攤位 {streets.Count} ／商城 {shops.Count} ／速度 {speeds.Count} ／消費 {costs.Count} ／倉庫 {storageItems.Count} 筆");

            _tabs.TabPages[0].Text = $"📊 交易紀錄（{trades.Count}）";
            _tabs.TabPages[1].Text = $"🏪 攤位交易（{streets.Count}）";
            _tabs.TabPages[2].Text = $"🛒 商城購買（{shops.Count}）";
            _tabs.TabPages[3].Text = $"⚡ 速度日誌（{speeds.Count}）";
            _tabs.TabPages[4].Text = $"💸 消費記錄（{costs.Count}）";
            _tabs.TabPages[5].Text = $"🏦 倉庫（{storageItems.Count}）";
        }

        private void ClearAll()
        {
            _dgvTrade.Rows.Clear();
            _dgvStreet.Rows.Clear();
            _dgvShop.Rows.Clear();
            _dgvSpeed.Rows.Clear();
            _dgvCost.Rows.Clear();
            _dgvStorage.Rows.Clear();
        }

        private void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            _statusLbl.Text = msg;
        }
    }
}
