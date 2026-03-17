using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════════════
    //  石器時代 GM 寵物指令產生器
    //  [gm petmake    寵物編號 等級 轉數]
    //  [gm petmakeabi 寵物編號 血 攻 防 敏 等級 轉數]
    // ══════════════════════════════════════════════════════════════════
    internal class PetCommandDialog : Form
    {
        private readonly string _cdkey;
        private readonly string _charName;
        private List<ItemInfo> _petTypes = new();

        // ── 共用寵物搜尋器 ─────────────────────────────────────────
        private TextBox       _txtSearch = null!;  // 搜尋框
        private ListBox       _lstPet    = null!;  // 搜尋結果清單
        private NumericUpDown _nudPetId  = null!;  // 手動輸入編號
        private Label         _lblPetInfo = null!; // 顯示選中寵物資訊

        // ── petmake 欄位 ──────────────────────────────────────────
        private NumericUpDown _mkLv  = null!;
        private NumericUpDown _mkReb = null!;
        private TextBox       _mkOut = null!;

        // ── petmakeabi 欄位（目標面板值）─────────────────────────
        private NumericUpDown _abiHp  = null!;
        private NumericUpDown _abiAtk = null!;
        private NumericUpDown _abiDef = null!;
        private NumericUpDown _abiSpd = null!;
        private TextBox       _abiOut = null!;
        // 預測成長率
        private Label _lblPredAtk   = null!;
        private Label _lblPredDef   = null!;
        private Label _lblPredAgi   = null!;
        private Label _lblPredTotal = null!;

        // ── 成長率反推 ────────────────────────────────────────────
        private NumericUpDown _growHp      = null!;
        private NumericUpDown _growAtk     = null!;
        private NumericUpDown _growDef     = null!;
        private NumericUpDown _growAgi     = null!;
        private TextBox       _growOut     = null!;
        private Label         _lblGrowCalc = null!;

        // ── 精準三圍反推 ─────────────────────────────────────────
        private NumericUpDown _tgHp       = null!;
        private NumericUpDown _tgGrowAtk  = null!;
        private NumericUpDown _tgGrowDef  = null!;
        private NumericUpDown _tgGrowAgi  = null!;
        private TextBox       _tgOut      = null!;
        private Label         _lblTgSum   = null!;
        private Label         _lblTgCalc  = null!;

        public PetCommandDialog(string cdkey, string charName)
        {
            _cdkey    = cdkey;
            _charName = charName;

            Text          = $"🎮 GM 寵物指令 — {charName}";
            Size          = new Size(640, 620);
            MinimumSize   = new Size(560, 560);
            MaximizeBox   = false;
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            Load += (_, __) => LoadPetTypes();
        }

        // ── 載入寵物種類 ──────────────────────────────────────────
        private void LoadPetTypes()
        {
            _petTypes = GameDataManager.Instance.SearchPets("");
            RefreshList("");
            if (_petTypes.Count == 0)
            {
                _lstPet.Items.Add("（未載入寵物資料，請手動輸入編號）");
                _lstPet.Enabled = false;
            }
            else if (_lstPet.Items.Count > 0)
            {
                _lstPet.SelectedIndex = 0;
            }
        }

        private void RefreshList(string kw)
        {
            var results = string.IsNullOrWhiteSpace(kw)
                ? GameDataManager.Instance.SearchPets("")
                : GameDataManager.Instance.SearchPets(kw);
            _petTypes = results;
            _lstPet.BeginUpdate();
            _lstPet.Items.Clear();
            foreach (var p in results)
                _lstPet.Items.Add($"{p.Id}  {p.Name}");
            _lstPet.EndUpdate();
        }

        // ── UI 建構 ───────────────────────────────────────────────
        private void BuildUI()
        {
            // ── 標題列 ──────────────────────────────────────────────
            var hdr = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Theme.BgCard };
            hdr.Controls.Add(new Label
            {
                Text      = $"🎮  {_charName}  ▸  GM 寵物指令產生器",
                ForeColor = Color.FromArgb(210, 160, 255),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0)
            });

            // ── 主體 ─────────────────────────────────────────────────
            var body = new Panel
            {
                Dock       = DockStyle.Fill,
                Padding    = new Padding(12, 8, 12, 8),
                BackColor  = Theme.BgPage,
                AutoScroll = true
            };

            int bodyY = 0;

            // ── 寵物選擇卡片（共用）──────────────────────────────────
            var cardPet = MakeCard("🐾  選擇寵物種類");
            int cy = 36;

            // 搜尋框
            _txtSearch = new TextBox
            {
                PlaceholderText = "輸入名稱或編號搜尋…",
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                Location  = new Point(110, cy),
                Width     = 340,
                Height    = 26
            };
            _txtSearch.TextChanged += (_, __) => RefreshList(_txtSearch.Text.Trim());

            cardPet.Controls.Add(new Label
            {
                Text      = "搜尋寵物：",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontBody,
                Location  = new Point(10, cy + 2),
                Width     = 96,
                TextAlign = ContentAlignment.MiddleRight
            });
            cardPet.Controls.Add(_txtSearch);
            cy += 32;

            // 搜尋結果清單
            _lstPet = new ListBox
            {
                BackColor     = Theme.BgLight,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontBody,
                Location      = new Point(110, cy),
                Width         = 340,
                Height        = 100,
                ScrollAlwaysVisible = false
            };
            _lstPet.SelectedIndexChanged += OnPetSelected;
            cardPet.Controls.Add(_lstPet);
            cy += 106;

            // 目前選中的寵物資訊
            _lblPetInfo = new Label
            {
                Text      = "",
                ForeColor = Color.FromArgb(150, 240, 170),
                Font      = Theme.FontSmall,
                Location  = new Point(110, cy),
                AutoSize  = true
            };
            cardPet.Controls.Add(_lblPetInfo);
            cy += 22;

            // 手動輸入寵物編號
            _nudPetId = new NumericUpDown
            {
                Minimum   = 1,
                Maximum   = 99999,
                Value     = 1,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                Location  = new Point(110, cy),
                Width     = 120,
                Height    = 26
            };
            _nudPetId.ValueChanged += (_, __) => RefreshAll();

            cardPet.Controls.Add(new Label
            {
                Text      = "或手動輸入：",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontBody,
                Location  = new Point(10, cy + 2),
                Width     = 96,
                TextAlign = ContentAlignment.MiddleRight
            });
            cardPet.Controls.Add(_nudPetId);
            cardPet.Controls.Add(new Label
            {
                Text      = "（清單找不到時使用）",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(238, cy + 5),
                AutoSize  = true
            });
            cy += 34;
            cardPet.Height = cy + 8;

            // ── 卡片 1：petmake ────────────────────────────────────
            var card1 = MakeCard("📦  [gm petmake]  快速給予（等級 + 轉數）");
            cy = 36;

            _mkLv  = MakeNud(1, 999,  140);
            _mkReb = MakeNud(0, 99,   0);
            _mkOut = MakeOut();

            AddRow(card1, ref cy, "等　　級", _mkLv);
            AddRow(card1, ref cy, "轉　　數", _mkReb);

            var btnCopy1 = MakeCopyBtn();
            btnCopy1.Location = new Point(card1.Width - 100, cy);
            btnCopy1.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _mkOut.Location   = new Point(10, cy);
            _mkOut.Width      = card1.Width - 118;
            _mkOut.Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cy += 30;
            card1.Controls.AddRange(new Control[] { _mkOut, btnCopy1 });
            card1.Height = cy + 10;

            _mkLv.ValueChanged  += (_, __) => RefreshMk();
            _mkReb.ValueChanged += (_, __) => RefreshMk();

            btnCopy1.Click += (_, __) => DoCopy(_mkOut.Text, btnCopy1);

            // ── 卡片 2：petmakeabi ─────────────────────────────────
            var card2 = MakeCard("⚙️  [gm petmakeabi]  完整四維指定");
            cy = 36;

            // 說明
            card2.Controls.Add(new Label
            {
                Text      = "  請輸入「目標面板數值」，系統自動換算實際寫入參數（尾數固定 140 1）",
                ForeColor = Color.FromArgb(110, 190, 130),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            _abiHp  = MakeNud(1, 999999, 1314);
            _abiAtk = MakeNud(1, 99999,  327);
            _abiDef = MakeNud(1, 99999,  221);
            _abiSpd = MakeNud(1, 99999,  195);
            _abiOut = MakeOut();

            AddRow(card2, ref cy, "目標血量 HP",  _abiHp);
            AddRow(card2, ref cy, "目標攻擊 ATK", _abiAtk);
            AddRow(card2, ref cy, "目標防禦 DEF", _abiDef);
            AddRow(card2, ref cy, "目標敏捷 AGI", _abiSpd);

            var btnCopy2 = MakeCopyBtn();
            btnCopy2.Location = new Point(card2.Width - 100, cy);
            btnCopy2.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _abiOut.Location  = new Point(10, cy);
            _abiOut.Width     = card2.Width - 118;
            _abiOut.Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cy += 30;
            card2.Controls.AddRange(new Control[] { _abiOut, btnCopy2 });

            // 換算公式提示
            card2.Controls.Add(new Label
            {
                Text      = "※ 1:1 直接對應：輸入面板數值即為 GM 指令參數，無任何補償係數",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            // ── 預測成長率面板 ─────────────────────────────────────
            var pnlPred2 = new Panel
            {
                Location    = new Point(10, cy),
                Size        = new Size(card2.Width - 20, 88),
                BackColor   = Color.FromArgb(12, 32, 18),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            pnlPred2.Controls.Add(new Label
            {
                Text      = "📊  預測成長率（估算：Lv1初值 攻擊 19、防禦 12、敏捷 12，共升 139 次）",
                ForeColor = Color.FromArgb(80, 200, 100),
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(8, 6)
            });
            _lblPredAtk   = new Label { ForeColor = Color.FromArgb(255, 185, 60),  Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold), AutoSize = true, Location = new Point(8,   28), Text = "攻擊成長：—" };
            _lblPredDef   = new Label { ForeColor = Color.FromArgb(100, 185, 255), Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold), AutoSize = true, Location = new Point(185, 28), Text = "防禦成長：—" };
            _lblPredAgi   = new Label { ForeColor = Color.FromArgb(185, 130, 255), Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold), AutoSize = true, Location = new Point(362, 28), Text = "敏捷成長：—" };
            _lblPredTotal = new Label { ForeColor = Color.FromArgb(255, 230, 60),  Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), AutoSize = true, Location = new Point(8,   56), Text = "預測總成長：—" };
            pnlPred2.Controls.AddRange(new Control[] { _lblPredAtk, _lblPredDef, _lblPredAgi, _lblPredTotal });
            card2.Controls.Add(pnlPred2);
            cy += 96;

            card2.Height = cy + 10;

            foreach (var n in new[] { _abiHp, _abiAtk, _abiDef, _abiSpd })
                n.ValueChanged += (_, __) => RefreshAbi();

            btnCopy2.Click += (_, __) => DoCopy(_abiOut.Text, btnCopy2);

            // ── 卡片 3：成長率反推 ─────────────────────────────────
            var card3 = MakeCard("🔢  成長率反推指令（成長率 → 面板目標 → GM 指令）");
            cy = 36;

            card3.Controls.Add(new Label
            {
                Text      = "  輸入預期成長率與最終血量，自動反推目標面板並產生指令",
                ForeColor = Color.FromArgb(140, 200, 255),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            _growHp  = MakeNud(1, 999999, 1000);
            _growAtk = MakeGrowNud(7.266m);
            _growDef = MakeGrowNud(3.158m);
            _growAgi = MakeGrowNud(2.878m);
            _growOut = MakeOut();

            AddRow(card3, ref cy, "最終血量 HP",  _growHp);
            AddRow(card3, ref cy, "攻擊成長率",    _growAtk);
            AddRow(card3, ref cy, "防禦成長率",    _growDef);
            AddRow(card3, ref cy, "敏捷成長率",    _growAgi);

            _lblGrowCalc = new Label
            {
                Text      = "推導目標面板：（請輸入成長率後自動計算）",
                ForeColor = Color.FromArgb(160, 220, 180),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            };
            card3.Controls.Add(_lblGrowCalc);
            cy += 22;

            var btnCopy3 = MakeCopyBtn();
            btnCopy3.Location = new Point(card3.Width - 100, cy);
            btnCopy3.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _growOut.Location = new Point(10, cy);
            _growOut.Width    = card3.Width - 118;
            _growOut.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _growOut.ForeColor = Color.FromArgb(160, 200, 255);
            cy += 30;
            card3.Controls.AddRange(new Control[] { _growOut, btnCopy3 });

            card3.Controls.Add(new Label
            {
                Text      = "※ ATK目標=round(成長×139+19)　DEF目標=round(成長×139+12)　AGI目標=round(成長×139+12)",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;
            card3.Height = cy + 10;

            foreach (var n in new NumericUpDown[] { _growHp, _growAtk, _growDef, _growAgi })
                n.ValueChanged += (_, __) => RefreshGrow();
            btnCopy3.Click += (_, __) => DoCopy(_growOut.Text, btnCopy3);

            // ── 卡片 4：精準三圍反推 ───────────────────────────────
            var card4 = MakeCard("✅  精準三圍反推指令（直接輸入各成長率，兩段式計算）");
            cy = 36;

            card4.Controls.Add(new Label
            {
                Text      = "  直接輸入三圍成長率與目標血量 → 精準還原 140 等面板 → 生成 GM 指令",
                ForeColor = Color.FromArgb(130, 220, 130),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            _tgHp = MakeNud(1, 999999, 2050);
            AddRow(card4, ref cy, "最終血量 HP", _tgHp);

            _tgGrowAtk = MakeGrowNud(3.1m);
            AddRow(card4, ref cy, "預期攻擊成長", _tgGrowAtk);

            _tgGrowDef = MakeGrowNud(2.1m);
            AddRow(card4, ref cy, "預期防禦成長", _tgGrowDef);

            _tgGrowAgi = MakeGrowNud(2.0m);
            AddRow(card4, ref cy, "預期敏捷成長", _tgGrowAgi);

            // 唯讀預期總成長
            _lblTgSum = new Label
            {
                Text      = "7.200",
                ForeColor = Color.FromArgb(255, 230, 80),
                Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(110, cy + 4)
            };
            card4.Controls.Add(new Label { Text = "預期總成長：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody, Location = new Point(10, cy + 2), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card4.Controls.Add(_lblTgSum);
            cy += 26;

            _lblTgCalc = new Label
            {
                Text      = "目標面板：（請輸入成長率後自動計算）",
                ForeColor = Color.FromArgb(160, 220, 180),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            };
            card4.Controls.Add(_lblTgCalc);
            cy += 22;

            _tgOut = MakeOut();
            _tgOut.ForeColor = Color.FromArgb(255, 210, 120);
            var btnCopy4 = MakeCopyBtn();
            btnCopy4.Location = new Point(card4.Width - 100, cy);
            btnCopy4.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _tgOut.Location   = new Point(10, cy);
            _tgOut.Width      = card4.Width - 118;
            _tgOut.Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cy += 30;
            card4.Controls.AddRange(new Control[] { _tgOut, btnCopy4 });

            card4.Controls.Add(new Label
            {
                Text      = "※ 步驟1：Target=round(成長×139+初值)  步驟2：1:1 直接寫入指令（無補償係數）",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;
            card4.Height = cy + 10;

            foreach (var n in new NumericUpDown[] { _tgHp, _tgGrowAtk, _tgGrowDef, _tgGrowAgi })
                n.ValueChanged += (_, __) => RefreshTotalGrow();
            btnCopy4.Click += (_, __) => DoCopy(_tgOut.Text, btnCopy4);

            // ── 排版 ──────────────────────────────────────────────
            cardPet.Location = new Point(0, bodyY); bodyY += cardPet.Height + 8;
            card1.Location   = new Point(0, bodyY); bodyY += card1.Height + 8;
            card2.Location   = new Point(0, bodyY); bodyY += card2.Height + 8;
            card3.Location   = new Point(0, bodyY); bodyY += card3.Height + 8;
            card4.Location   = new Point(0, bodyY);

            body.Controls.AddRange(new Control[] { cardPet, card1, card2, card3, card4 });
            body.Resize += (_, __) =>
            {
                int w = body.ClientSize.Width - 4;
                cardPet.Width = w; card1.Width = w; card2.Width = w; card3.Width = w; card4.Width = w;
                _txtSearch.Width = Math.Max(200, w - 120);
                _lstPet.Width    = Math.Max(200, w - 120);
            };

            // ── 頁尾提示 ─────────────────────────────────────────
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Theme.BgCard };
            foot.Controls.Add(new Label
            {
                Text      = "⚠  複製後貼到遊戲 GM 對話框執行（指令只能生成給自己）",
                ForeColor = Color.FromArgb(255, 200, 80),
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            Controls.Add(body);
            Controls.Add(foot);
            Controls.Add(hdr);

            RefreshAll();
        }

        // ── 寵物選擇事件（ListBox）────────────────────────────────
        private void OnPetSelected(object? sender, EventArgs e)
        {
            int idx = _lstPet.SelectedIndex;
            if (idx < 0 || idx >= _petTypes.Count) return;
            var p = _petTypes[idx];
            _nudPetId.Value  = Math.Max(1, Math.Min(99999, p.Id));
            _lblPetInfo.Text = $"✓  {p.Name}（編號 {p.Id}）";
            RefreshAll();
        }

        // ── 指令刷新 ──────────────────────────────────────────────
        private int CurrentPetId => (int)_nudPetId.Value;

        private void RefreshAll()  { RefreshMk(); RefreshAbi(); RefreshGrow(); RefreshTotalGrow(); }

        private void RefreshTotalGrow()
        {
            if (_tgOut == null) return;

            long   hp   = (long)_tgHp.Value;
            double gAtk = (double)_tgGrowAtk.Value;
            double gDef = (double)_tgGrowDef.Value;
            double gAgi = (double)_tgGrowAgi.Value;

            // 唯讀預期總成長
            if (_lblTgSum != null)
                _lblTgSum.Text = $"{gAtk + gDef + gAgi:F3}";

            // 步驟1：成長率 → 140 等目標面板
            long tAtk = (long)Math.Round(gAtk * 139 + 19);
            long tDef = (long)Math.Round(gDef * 139 + 12);
            long tAgi = (long)Math.Round(gAgi * 139 + 12);

            // 步驟2：1:1 直接寫入（無補償係數）
            _tgOut.Text = $"[gm petmakeabi {CurrentPetId} {hp} {tAtk} {tDef} {tAgi} 140 1]";
            if (_lblTgCalc != null)
                _lblTgCalc.Text = $"目標面板：ATK = {tAtk}　DEF = {tDef}　AGI = {tAgi}";
        }

        private void RefreshGrow()
        {
            if (_growOut == null) return;

            long   hp   = (long)_growHp.Value;
            double gAtk = (double)_growAtk.Value;
            double gDef = (double)_growDef.Value;
            double gAgi = (double)_growAgi.Value;

            // Step 1: 成長率 → 目標面板值
            long tAtk = (long)Math.Round(gAtk * 139 + 19);
            long tDef = (long)Math.Round(gDef * 139 + 12);
            long tAgi = (long)Math.Round(gAgi * 139 + 12);

            // Step 2: 1:1 直接寫入（無補償係數）
            _growOut.Text = $"[gm petmakeabi {CurrentPetId} {hp} {tAtk} {tDef} {tAgi} 140 1]";
            if (_lblGrowCalc != null)
                _lblGrowCalc.Text = $"推導目標面板：ATK = {tAtk}　DEF = {tDef}　AGI = {tAgi}";
        }
        private void RefreshMk()   => _mkOut.Text = $"[gm petmake {CurrentPetId} {(int)_mkLv.Value} {(int)_mkReb.Value}]";

        private void RefreshAbi()
        {
            if (_abiOut == null) return;

            long tHp  = (long)_abiHp.Value;
            long tAtk = (long)_abiAtk.Value;
            long tDef = (long)_abiDef.Value;
            long tAgi = (long)_abiSpd.Value;

            // 1:1 直接寫入（無補償係數）
            _abiOut.Text = $"[gm petmakeabi {CurrentPetId} {tHp} {tAtk} {tDef} {tAgi} 140 1]";

            // 預測成長率（平均初值估算）
            if (_lblPredAtk == null) return;
            double pAtk   = (tAtk - 19.0) / 139.0;
            double pDef   = (tDef - 12.0) / 139.0;
            double pAgi   = (tAgi - 12.0) / 139.0;
            double pTotal = pAtk + pDef + pAgi;

            _lblPredAtk.Text   = $"攻擊成長：{pAtk:F3}";
            _lblPredDef.Text   = $"防禦成長：{pDef:F3}";
            _lblPredAgi.Text   = $"敏捷成長：{pAgi:F3}";
            _lblPredTotal.Text = $"預測總成長：{pTotal:F3}";
        }

        // ── 複製並短暫變色 ────────────────────────────────────────
        private static void DoCopy(string text, Button btn)
        {
            if (string.IsNullOrEmpty(text)) return;
            Clipboard.SetText(text);
            string orig = btn.Text;
            btn.Text = "✓ 已複製";
            var t = new System.Windows.Forms.Timer { Interval = 1500 };
            t.Tick += (_, __) => { btn.Text = orig; t.Stop(); t.Dispose(); };
            t.Start();
        }

        // ── UI 輔助 ───────────────────────────────────────────────
        private Panel MakeCard(string title)
        {
            var card = new Panel
            {
                Width     = 580,
                BackColor = Theme.BgCard,
                Padding   = new Padding(10, 4, 10, 6)
            };
            card.Controls.Add(new Label
            {
                Text      = title,
                ForeColor = Color.FromArgb(200, 200, 220),
                Font      = Theme.FontSmall,
                Location  = new Point(10, 8),
                AutoSize  = true
            });
            return card;
        }

        private static NumericUpDown MakeNud(int min, int max, int val) =>
            new NumericUpDown
            {
                Minimum   = min,
                Maximum   = max,
                Value     = val,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                Width     = 130,
                Height    = 26,
                ThousandsSeparator = false
            };

        private static NumericUpDown MakeGrowNud(decimal defaultVal = 0m) =>
            new NumericUpDown
            {
                Minimum       = 0m,
                Maximum       = 99.999m,
                Value         = Math.Max(0m, Math.Min(99.999m, defaultVal)),
                DecimalPlaces = 3,
                Increment     = 0.001m,
                BackColor     = Theme.BgLight,
                ForeColor     = Color.FromArgb(255, 215, 100),
                Font          = Theme.FontBody,
                Width         = 130,
                Height        = 26,
            };

        private static TextBox MakeOut() =>
            new TextBox
            {
                ReadOnly    = true,
                BackColor   = Color.FromArgb(20, 20, 30),
                ForeColor   = Color.FromArgb(150, 255, 200),
                Font        = new Font("Consolas", 10f),
                Height      = 26,
                BorderStyle = BorderStyle.FixedSingle
            };

        private Button MakeCopyBtn()
        {
            var btn = Theme.MakeButton("📋 複製", Color.FromArgb(60, 30, 100), Color.FromArgb(210, 160, 255), 84, 26);
            btn.Font = Theme.FontSmall;
            return btn;
        }

        private void AddRow(Panel card, ref int y, string label, Control ctrl)
        {
            ctrl.Location = new Point(110, y);
            ctrl.Height   = 26;
            card.Controls.Add(new Label
            {
                Text      = label + "：",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontBody,
                Location  = new Point(10, y + 2),
                Width     = 96,
                TextAlign = ContentAlignment.MiddleRight
            });
            card.Controls.Add(ctrl);
            y += 32;
        }
    }
}
