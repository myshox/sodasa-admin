using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class StreetShopForm : Form
    {
        // ── 攤位查詢 ──────────────────────────────────────────────
        private TextBox      _vendorSearch;
        private ListBox      _vendorList;
        private Label        _vendorStatusLbl;
        private DataGridView _dgvCurrentItems;
        private DataGridView _dgvVendorSales;
        private TabControl   _vendorInnerTabs;
        private List<(string cdkey, string charName, int cnt)> _allVendors = new();

        // ── 物品反查 ──────────────────────────────────────────────
        private TextBox      _itemSearch;
        private Button       _btnItemSearch;
        private Label        _itemStatusLbl;
        private DataGridView _dgvListings;
        private DataGridView _dgvStreetBuyers;
        private DataGridView _dgvShopBuyers;

        public StreetShopForm()
        {
            Text          = "🏪 攤位 & 市場查詢";
            Size          = new Size(1280, 740);
            MinimumSize   = new Size(960, 560);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadVendorsAsync();
        }

        private void BuildUI()
        {
            // ── Header ──────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  🏪  攤位 & 市場查詢  —  攤位查詢 ／ 物品反查",
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontHeader,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 主 TabControl ────────────────────────────────────────
            var mainTabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody };
            mainTabs.TabPages.Add(BuildVendorTab());
            mainTabs.TabPages.Add(BuildItemReverseTab());

            Controls.Add(mainTabs);
            Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 1 : 攤位查詢
        // ════════════════════════════════════════════════════════════════
        private TabPage BuildVendorTab()
        {
            var page = new TabPage("🛖  攤位查詢") { BackColor = Theme.BgPage };

            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Vertical,
                BackColor     = Theme.BgMid,
                SplitterWidth = 4
                // Panel1MinSize / Panel2MinSize / SplitterDistance 必須等加入 parent 後再設定
            };

            // ── 左側：攤主清單 ──────────────────────────────────────
            var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgSidebar };

            _vendorSearch = new TextBox
            {
                Dock            = DockStyle.Top,
                PlaceholderText = "搜尋攤主帳號 / 角色名 / 主帳號…",
                BackColor       = Theme.BgLight,
                ForeColor       = Theme.TextPrimary,
                Font            = Theme.FontSmall,
                BorderStyle     = BorderStyle.FixedSingle,
                Height          = 28
            };
            _vendorSearch.TextChanged += (s, e) => FilterVendorList(_vendorSearch.Text);

            _vendorStatusLbl = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 22,
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Text      = "載入中…",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0)
            };

            _vendorList = new ListBox
            {
                Dock            = DockStyle.Fill,
                BackColor       = Theme.BgCard,
                ForeColor       = Theme.TextPrimary,
                Font            = Theme.FontSmall,
                BorderStyle     = BorderStyle.None,
                ScrollAlwaysVisible = true
            };
            _vendorList.SelectedIndexChanged += (s, e) => _ = LoadVendorDetailsAsync();

            leftPanel.Controls.Add(_vendorList);
            leftPanel.Controls.Add(_vendorStatusLbl);
            leftPanel.Controls.Add(_vendorSearch);

            // ── 右側：物品上架 / 歷史成交 ──────────────────────────
            _vendorInnerTabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody };

            _dgvCurrentItems = MakeDgv();
            _dgvCurrentItems.Columns.AddRange(
                Col("itemId",   "物品ID",   70),
                Col("itemName", "物品名稱", 200, fill: true),
                Col("num",      "數量",     60),
                Col("price",    "單價",     90));

            _dgvVendorSales = MakeDgv();
            _dgvVendorSales.Columns.AddRange(
                Col("time",     "成交時間", 150),
                Col("itemName", "物品",     180, fill: true),
                Col("num",      "數量",      60),
                Col("price",    "單價",      90),
                Col("buyCdkey", "買家帳號", 130),
                Col("buyName",  "買家角色", 110));

            var tabCurrent = new TabPage("📦 目前上架") { BackColor = Theme.BgPage };
            _dgvCurrentItems.Dock = DockStyle.Fill;
            tabCurrent.Controls.Add(_dgvCurrentItems);

            var tabSales = new TabPage("📜 歷史成交") { BackColor = Theme.BgPage };
            _dgvVendorSales.Dock = DockStyle.Fill;
            tabSales.Controls.Add(_dgvVendorSales);

            _vendorInnerTabs.TabPages.Add(tabCurrent);
            _vendorInnerTabs.TabPages.Add(tabSales);

            split.Panel1.Controls.Add(leftPanel);
            split.Panel2.Controls.Add(_vendorInnerTabs);

            page.Controls.Add(split);

            // 延遲設定 MinSize 與 SplitterDistance（等控件有實際寬度後才設定）
            split.HandleCreated += (s, e) =>
            {
                try
                {
                    split.Panel1MinSize = 180;
                    if (split.Width > 180 + 400 + split.SplitterWidth)
                    {
                        split.Panel2MinSize = 400;
                        split.SplitterDistance = 240;
                    }
                }
                catch { }
            };

            return page;
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 2 : 物品反查
        // ════════════════════════════════════════════════════════════════
        private TabPage BuildItemReverseTab()
        {
            var page = new TabPage("🔍  物品反查") { BackColor = Theme.BgPage };

            // ── 搜尋列 ──────────────────────────────────────────────
            var toolBar = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Theme.BgCard };
            toolBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });

            _itemSearch = new TextBox
            {
                PlaceholderText = "輸入物品名稱關鍵字…",
                Location = new Point(14, 12), Width = 300,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody
            };
            _itemSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = SearchItemAsync(); };

            _btnItemSearch = Theme.MakePrimaryButton("🔍 反查", 90, 26);
            _btnItemSearch.Location = new Point(322, 12);
            _btnItemSearch.Click   += (s, e) => _ = SearchItemAsync();

            _itemStatusLbl = new Label
            {
                AutoSize  = true, Location = new Point(426, 16),
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Text      = "輸入物品名稱後點擊反查"
            };
            toolBar.Controls.AddRange(new Control[] { _itemSearch, _btnItemSearch, _itemStatusLbl });

            // ── 內層 TabControl ─────────────────────────────────────
            var innerTabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.FontBody };

            _dgvListings = MakeDgv();
            _dgvListings.Columns.AddRange(
                Col("cdkey",    "攤主帳號",  130),
                Col("charName", "角色名稱",  110),
                Col("itemName", "物品名稱",  200, fill: true),
                Col("num",      "數量",       60),
                Col("price",    "單價",        90));

            _dgvStreetBuyers = MakeDgv();
            _dgvStreetBuyers.Columns.AddRange(
                Col("time",       "成交時間",  150),
                Col("sellCdkey",  "賣家帳號",  120),
                Col("sellerName", "賣家角色",  100),
                Col("buyCdkey",   "買家帳號",  120),
                Col("buyName",    "買家角色",  100),
                Col("itemName",   "物品",      160, fill: true),
                Col("num",        "數量",       60),
                Col("price",      "單價",        90));

            _dgvShopBuyers = MakeDgv();
            _dgvShopBuyers.Columns.AddRange(
                Col("time",     "購買時間",  150),
                Col("shopType", "商店",       90),
                Col("cdkey",    "帳號",      130),
                Col("charName", "角色名稱",  110),
                Col("itemName", "物品名稱",  180, fill: true),
                Col("num",      "數量",       60),
                Col("cost",     "花費",        90));

            var p1 = new TabPage("📦 目前上架") { BackColor = Theme.BgPage };
            _dgvListings.Dock = DockStyle.Fill; p1.Controls.Add(_dgvListings);

            var p2 = new TabPage("🛖 攤位成交") { BackColor = Theme.BgPage };
            _dgvStreetBuyers.Dock = DockStyle.Fill; p2.Controls.Add(_dgvStreetBuyers);

            var p3 = new TabPage("🏬 商城購買") { BackColor = Theme.BgPage };
            _dgvShopBuyers.Dock = DockStyle.Fill; p3.Controls.Add(_dgvShopBuyers);

            innerTabs.TabPages.AddRange(new[] { p1, p2, p3 });

            page.Controls.Add(innerTabs);
            page.Controls.Add(toolBar);
            return page;
        }

        // ════════════════════════════════════════════════════════════════
        //  資料載入
        // ════════════════════════════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadVendorsAsync()
        {
            _allVendors = await DatabaseManager.Instance.GetAllVendorsAsync();
            FilterVendorList("");
        }

        private void FilterVendorList(string kw)
        {
            if (InvokeRequired) { Invoke(new Action(() => FilterVendorList(kw))); return; }
            _vendorList.BeginUpdate();
            _vendorList.Items.Clear();
            kw = kw.Trim().ToLower();
            foreach (var v in _allVendors)
            {
                if (!string.IsNullOrEmpty(kw) &&
                    !v.cdkey.ToLower().Contains(kw) &&
                    !v.charName.ToLower().Contains(kw)) continue;

                string display = string.IsNullOrEmpty(v.charName)
                    ? $"{v.cdkey}  [{v.cnt}]"
                    : $"{v.cdkey} ({v.charName})  [{v.cnt}]";
                _vendorList.Items.Add(display);
            }
            _vendorList.EndUpdate();
            _vendorStatusLbl.Text = $"共 {_vendorList.Items.Count} 位攤主";
        }

        private async System.Threading.Tasks.Task LoadVendorDetailsAsync()
        {
            int idx = _vendorList.SelectedIndex;
            if (idx < 0 || idx >= _allVendors.Count) return;

            // 找到對應的 vendor（可能已被 Filter 過，需重新對應）
            string kw = _vendorSearch.Text.Trim().ToLower();
            var filtered = new List<(string cdkey, string charName, int cnt)>();
            foreach (var v in _allVendors)
            {
                if (!string.IsNullOrEmpty(kw) &&
                    !v.cdkey.ToLower().Contains(kw) &&
                    !v.charName.ToLower().Contains(kw)) continue;
                filtered.Add(v);
            }
            if (idx >= filtered.Count) return;
            string cdkey = filtered[idx].cdkey;

            _vendorStatusLbl.Text = $"載入「{cdkey}」的資料…";
            _dgvCurrentItems.Rows.Clear();
            _dgvVendorSales.Rows.Clear();

            var items = await DatabaseManager.Instance.GetVendorItemsAsync(cdkey);
            foreach (var t in items)
            {
                var row = _dgvCurrentItems.Rows[_dgvCurrentItems.Rows.Add()];
                row.Cells["itemId"].Value   = t.itemId;
                row.Cells["itemName"].Value = t.itemName;
                row.Cells["num"].Value      = t.num;
                row.Cells["price"].Value    = t.price.ToString("N0");
            }
            _vendorInnerTabs.TabPages[0].Text = $"📦 目前上架（{items.Count}）";

            var sales = await DatabaseManager.Instance.GetVendorSalesAsync(cdkey);
            foreach (var t in sales)
            {
                var row = _dgvVendorSales.Rows[_dgvVendorSales.Rows.Add()];
                row.Cells["time"].Value     = t.time;
                row.Cells["itemName"].Value = t.itemName;
                row.Cells["num"].Value      = t.num;
                row.Cells["price"].Value    = t.price.ToString("N0");
                row.Cells["buyCdkey"].Value = t.buyCdkey;
                row.Cells["buyName"].Value  = t.buyName;
            }
            _vendorInnerTabs.TabPages[1].Text = $"📜 歷史成交（{sales.Count}）";
            _vendorStatusLbl.Text = $"「{cdkey}」｜上架 {items.Count} 件 ／成交 {sales.Count} 筆";
        }

        private async System.Threading.Tasks.Task SearchItemAsync()
        {
            string kw = _itemSearch.Text.Trim();
            if (string.IsNullOrEmpty(kw)) return;

            _btnItemSearch.Enabled = false;
            SetItemStatus("反查中…");
            _dgvListings.Rows.Clear();
            _dgvStreetBuyers.Rows.Clear();
            _dgvShopBuyers.Rows.Clear();

            var db = DatabaseManager.Instance;

            var listings = await db.GetListingsByItemAsync(kw);
            foreach (var t in listings)
            {
                var row = _dgvListings.Rows[_dgvListings.Rows.Add()];
                row.Cells["cdkey"].Value    = t.cdkey;
                row.Cells["charName"].Value = t.charName;
                row.Cells["itemName"].Value = t.itemName;
                row.Cells["num"].Value      = t.num;
                row.Cells["price"].Value    = t.price.ToString("N0");
            }

            var streetBuyers = await db.GetStreetBuyersByItemAsync(kw);
            foreach (var t in streetBuyers)
            {
                var row = _dgvStreetBuyers.Rows[_dgvStreetBuyers.Rows.Add()];
                row.Cells["time"].Value       = t.time;
                row.Cells["sellCdkey"].Value  = t.sellCdkey;
                row.Cells["sellerName"].Value = t.sellerName;
                row.Cells["buyCdkey"].Value   = t.buyCdkey;
                row.Cells["buyName"].Value    = t.buyName;
                row.Cells["itemName"].Value   = t.itemName;
                row.Cells["num"].Value        = t.num;
                row.Cells["price"].Value      = t.price.ToString("N0");
            }

            var shopBuyers = await db.GetShopBuyersByItemAsync(kw);
            foreach (var t in shopBuyers)
            {
                var row = _dgvShopBuyers.Rows[_dgvShopBuyers.Rows.Add()];
                row.Cells["time"].Value     = t.time;
                row.Cells["shopType"].Value = t.shopType;
                row.Cells["cdkey"].Value    = t.cdkey;
                row.Cells["charName"].Value = t.charName;
                row.Cells["itemName"].Value = t.itemName;
                row.Cells["num"].Value      = t.num;
                row.Cells["cost"].Value     = t.cost.ToString("N0");
            }

            // 更新 Tab 標題
            var page = (TabPage)((TabControl)_dgvListings.Parent.Parent).TabPages[0];
            page.Text = $"📦 目前上架（{listings.Count}）";
            ((TabControl)_dgvListings.Parent.Parent).TabPages[1].Text = $"🛖 攤位成交（{streetBuyers.Count}）";
            ((TabControl)_dgvListings.Parent.Parent).TabPages[2].Text = $"🏬 商城購買（{shopBuyers.Count}）";

            SetItemStatus($"「{kw}」｜上架 {listings.Count} 筆 ／攤位成交 {streetBuyers.Count} 筆 ／商城購買 {shopBuyers.Count} 筆");
            _btnItemSearch.Enabled = true;
        }

        // ── 工具 ─────────────────────────────────────────────────
        private static DataGridView MakeDgv()
        {
            var dgv = new DataGridView();
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

        private void SetItemStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetItemStatus(msg))); return; }
            _itemStatusLbl.Text = msg;
        }
    }
}
