using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════════════
    //  🐾 GM 寵物指令產生器（主頁側欄入口）
    //  petmake    : [gm petmake    編號 等級 轉數 {CDKEY可選}]
    //  petmakeabi : [gm petmakeabi 編號 血 攻 防 敏 等級 轉數]
    // ══════════════════════════════════════════════════════════════════
    internal class GmPetForm : Form
    {
        // ── 玩家搜尋 ──────────────────────────────────────────────
        private TextBox  _txtSearch    = null!;
        private Label    _lblResult    = null!;
        private string   _foundCdkey   = "";     // 搜尋到的帳號(CDKEY)
        private string   _foundName    = "";     // 搜尋到的角色名
        private List<PlayerInfo> _pickedPlayers = new(); // 多選清單

        // ── 寵物選擇 ──────────────────────────────────────────────
        private TextBox       _txtPetSearch = null!;
        private ListBox       _lstPet       = null!;
        private NumericUpDown _nudPetId     = null!;
        private Label         _lblPetInfo   = null!;
        private List<ItemInfo> _petTypes    = new();

        // ── petmake ───────────────────────────────────────────────
        private NumericUpDown _mkLv      = null!;
        private NumericUpDown _mkReb     = null!;
        private CheckBox      _chkCdkey  = null!;   // 是否附加CDKEY
        private TextBox       _txtCdkey  = null!;   // CDKEY輸入
        private TextBox       _mkOut     = null!;

        // ── petmakeabi ────────────────────────────────────────────
        private NumericUpDown _abiHp  = null!;   // 目標面板值
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
        private NumericUpDown _tgHp      = null!;   // 目標最終血量
        private NumericUpDown _tgGrowAtk = null!;   // 預期攻擊成長（3位小數）
        private NumericUpDown _tgGrowDef = null!;   // 預期防禦成長
        private NumericUpDown _tgGrowAgi = null!;   // 預期敏捷成長
        private TextBox       _tgOut     = null!;
        private Label         _lblTgSum  = null!;   // 預期總成長唯讀顯示
        private Label         _lblTgCalc = null!;   // GM 寫入參數顯示

        public GmPetForm()
        {
            Text            = "🐾  GM 寵物指令產生器";
            Size            = new Size(700, 820);
            MinimumSize     = new Size(640, 740);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            StartPosition   = FormStartPosition.CenterParent;

            BuildUI();
            LoadPetList("");
        }

        // ══════════════════════════════════════════════════════════
        //  UI 建構
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            var scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Theme.BgPage,
                Padding    = new Padding(14, 10, 14, 10)
            };
            scroll.HorizontalScroll.Enabled = false;
            scroll.HorizontalScroll.Visible = false;

            var inner = new FlowLayoutPanel
            {
                FlowDirection    = FlowDirection.TopDown,
                WrapContents     = false,
                AutoSize         = true,
                AutoSizeMode     = AutoSizeMode.GrowAndShrink,
                BackColor        = Theme.BgPage,
                Padding          = new Padding(0),
                Dock             = DockStyle.Top
            };

            inner.Controls.Add(BuildPlayerCard());
            inner.Controls.Add(Spacer(8));
            inner.Controls.Add(BuildPetSelectCard());
            inner.Controls.Add(Spacer(8));
            inner.Controls.Add(BuildMkCard());
            inner.Controls.Add(Spacer(8));
            inner.Controls.Add(BuildAbiCard());
            inner.Controls.Add(Spacer(8));
            inner.Controls.Add(BuildGrowthCard());
            inner.Controls.Add(Spacer(8));
            inner.Controls.Add(BuildTotalGrowthCard());
            inner.Controls.Add(Spacer(12));

            scroll.Controls.Add(inner);
            Controls.Add(scroll);

            // 底部提示
            var foot = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.BgCard };
            foot.Controls.Add(new Label
            {
                Text      = "⚠  複製指令後貼到遊戲內 GM 對話框執行",
                ForeColor = Color.FromArgb(255, 200, 80),
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });
            Controls.Add(foot);
        }

        // ── 玩家搜尋卡片 ──────────────────────────────────────────
        private Panel BuildPlayerCard()
        {
            var card = MakeCard("👤  指定玩家帳號（CDKEY）");
            int cw = CardWidth();

            var lblHint = new Label
            {
                Text      = "帳號 / 角色名：",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Location  = new Point(10, 36),
                Width     = 110,
                TextAlign = ContentAlignment.MiddleRight
            };
            _txtSearch = new TextBox
            {
                BackColor   = Theme.BgLight,
                ForeColor   = Theme.TextPrimary,
                Font        = Theme.FontBody,
                Location    = new Point(124, 34),
                Width       = 240,
                Height      = 26,
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = DoSearchAsync(); } };

            var btnSearch = Theme.MakeButton("🔍 查詢", Theme.AccentBlue, Color.White, 80, 26);
            btnSearch.Location = new Point(370, 34);
            btnSearch.Font     = Theme.FontSmall;
            btnSearch.Click   += async (s, e) => await DoSearchAsync();

            _lblResult = new Label
            {
                Text      = "（尚未搜尋）",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(458, 38),
                AutoSize  = true
            };

            card.Controls.AddRange(new Control[] { lblHint, _txtSearch, btnSearch, _lblResult });
            card.Height = 70;
            return card;
        }

        // ── 寵物選擇卡片 ──────────────────────────────────────────
        private Panel BuildPetSelectCard()
        {
            var card = MakeCard("🐾  選擇寵物種類");
            int cy = 36;

            // 搜尋框
            _txtPetSearch = new TextBox
            {
                PlaceholderText = "輸入名稱或編號搜尋…",
                BackColor   = Theme.BgLight,
                ForeColor   = Theme.TextPrimary,
                Font        = Theme.FontBody,
                Location    = new Point(110, cy),
                Width       = 380,
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtPetSearch.TextChanged += (_, __) => LoadPetList(_txtPetSearch.Text.Trim());

            card.Controls.Add(new Label { Text = "搜尋寵物：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(10, cy + 2), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card.Controls.Add(_txtPetSearch);
            cy += 32;

            // 結果清單
            _lstPet = new ListBox
            {
                BackColor           = Theme.BgLight,
                ForeColor           = Theme.TextPrimary,
                Font                = Theme.FontBody,
                Location            = new Point(110, cy),
                Width               = 380,
                Height              = 100,
                ScrollAlwaysVisible = false
            };
            _lstPet.SelectedIndexChanged += OnPetSelected;
            card.Controls.Add(_lstPet);
            cy += 106;

            _lblPetInfo = new Label
            {
                Text      = "",
                ForeColor = Color.FromArgb(150, 240, 170),
                Font      = Theme.FontSmall,
                Location  = new Point(110, cy),
                AutoSize  = true
            };
            card.Controls.Add(_lblPetInfo);
            cy += 20;

            // 手動輸入
            _nudPetId = new NumericUpDown
            {
                Minimum   = 0,
                Maximum   = 99999,
                Value     = 1,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                Location  = new Point(110, cy),
                Width     = 120
            };
            _nudPetId.ValueChanged += (_, __) => RefreshAll();

            card.Controls.Add(new Label { Text = "或手動輸入：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(10, cy + 2), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card.Controls.Add(_nudPetId);
            card.Controls.Add(new Label { Text = "（清單找不到時使用）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, Location = new Point(238, cy + 4), AutoSize = true });
            cy += 34;

            card.Height = cy + 8;
            return card;
        }

        // ── petmake 卡片 ──────────────────────────────────────────
        private Panel BuildMkCard()
        {
            var card = MakeCard("⚙  [gm petmake]  簡易給予");
            int cy = 36;

            // 等級
            _mkLv = MakeNud(1, 200, 1, 120);
            _mkLv.Location = new Point(110, cy);
            _mkLv.ValueChanged += (_, __) => RefreshMk();
            card.Controls.Add(MakeLabel("等　　級：", new Point(10, cy + 2)));
            card.Controls.Add(_mkLv);
            cy += 30;

            // 轉數
            _mkReb = MakeNud(0, 20, 0, 120);
            _mkReb.Location = new Point(110, cy);
            _mkReb.ValueChanged += (_, __) => RefreshMk();
            card.Controls.Add(MakeLabel("轉　　數：", new Point(10, cy + 2)));
            card.Controls.Add(_mkReb);
            cy += 30;

            // CDKEY 勾選
            _chkCdkey = new CheckBox
            {
                Text      = "指定玩家 CDKEY（可選）",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Location  = new Point(110, cy),
                AutoSize  = true,
                Checked   = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent
            };
            _chkCdkey.CheckedChanged += (_, __) =>
            {
                _txtCdkey.Visible = _chkCdkey.Checked;
                RefreshMk();
            };
            card.Controls.Add(_chkCdkey);
            cy += 26;

            _txtCdkey = new TextBox
            {
                PlaceholderText = "玩家登入帳號（CDKEY）",
                BackColor   = Theme.BgLight,
                ForeColor   = Theme.TextPrimary,
                Font        = Theme.FontBody,
                Location    = new Point(110, cy),
                Width       = 280,
                BorderStyle = BorderStyle.FixedSingle,
                Visible     = false
            };
            _txtCdkey.TextChanged += (_, __) => RefreshMk();

            var lblCdkeyHint = new Label
            {
                Text      = "← 填入玩家帳號後，指令將送給指定玩家",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(398, cy + 4),
                AutoSize  = true,
                Visible   = false
            };
            _chkCdkey.CheckedChanged += (_, __) => lblCdkeyHint.Visible = _chkCdkey.Checked;

            card.Controls.Add(_txtCdkey);
            card.Controls.Add(lblCdkeyHint);
            cy += 30;

            // 輸出
            _mkOut = new TextBox
            {
                ReadOnly    = true,
                BackColor   = Color.FromArgb(20, 40, 20),
                ForeColor   = Color.FromArgb(100, 255, 140),
                Font        = new Font("Consolas", 11f, FontStyle.Bold),
                Location    = new Point(110, cy),
                Width       = 400,
                BorderStyle = BorderStyle.FixedSingle
            };
            var btnCopyMk = Theme.MakeButton("複製", Theme.AccentBlue, Color.White, 60, 26);
            btnCopyMk.Location = new Point(518, cy);
            btnCopyMk.Font     = Theme.FontSmall;
            btnCopyMk.Click   += (_, __) => DoCopy(_mkOut.Text, btnCopyMk);

            card.Controls.Add(new Label { Text = "指令預覽：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(10, cy + 4), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card.Controls.Add(_mkOut);
            card.Controls.Add(btnCopyMk);
            cy += 34;

            card.Height = cy + 8;
            return card;
        }

        // ── petmakeabi 卡片 ───────────────────────────────────────
        private Panel BuildAbiCard()
        {
            var card = MakeCard("⚙  [gm petmakeabi]  完整四維指定");
            int cy = 36;

            card.Controls.Add(new Label
            {
                Text      = "  請輸入「目標面板數值」，系統自動換算實際寫入參數（尾數固定 140 1）",
                ForeColor = Color.FromArgb(110, 190, 130),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            _abiHp  = MakeNud(1, 999999, 1000, 120);
            _abiAtk = MakeNud(0,  99999,  200, 100);
            _abiDef = MakeNud(0,  99999,  100, 100);
            _abiSpd = MakeNud(0,  99999,  100, 100);

            foreach (var n in new[] { _abiHp, _abiAtk, _abiDef, _abiSpd })
                n.ValueChanged += (_, __) => RefreshAbi();

            _abiHp.Location  = new Point(110, cy); _abiHp.Width  = 120;
            card.Controls.Add(MakeLabel("目標血量 HP：", new Point(10, cy + 2)));
            card.Controls.Add(_abiHp);
            cy += 30;

            _abiAtk.Location = new Point(110, cy); _abiAtk.Width = 120;
            card.Controls.Add(MakeLabel("目標攻擊 ATK：", new Point(10, cy + 2)));
            card.Controls.Add(_abiAtk);
            cy += 30;

            _abiDef.Location = new Point(110, cy); _abiDef.Width = 120;
            card.Controls.Add(MakeLabel("目標防禦 DEF：", new Point(10, cy + 2)));
            card.Controls.Add(_abiDef);
            cy += 30;

            _abiSpd.Location = new Point(110, cy); _abiSpd.Width = 120;
            card.Controls.Add(MakeLabel("目標敏捷 AGI：", new Point(10, cy + 2)));
            card.Controls.Add(_abiSpd);
            cy += 30;

            _abiOut = new TextBox
            {
                ReadOnly    = true,
                BackColor   = Color.FromArgb(20, 20, 40),
                ForeColor   = Color.FromArgb(160, 180, 255),
                Font        = new Font("Consolas", 11f, FontStyle.Bold),
                Location    = new Point(110, cy),
                Width       = 400,
                BorderStyle = BorderStyle.FixedSingle
            };
            var btnCopyAbi = Theme.MakeButton("複製", Color.FromArgb(60, 30, 100), Color.FromArgb(210, 160, 255), 60, 26);
            btnCopyAbi.Location = new Point(518, cy);
            btnCopyAbi.Font     = Theme.FontSmall;
            btnCopyAbi.Click   += (_, __) => DoCopy(_abiOut.Text, btnCopyAbi);

            card.Controls.Add(new Label { Text = "指令預覽：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(10, cy + 4), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card.Controls.Add(_abiOut);
            card.Controls.Add(btnCopyAbi);
            cy += 34;

            card.Controls.Add(new Label
            {
                Text      = "※ 1:1 直接對應：輸入面板數值即為 GM 指令參數，無任何補償係數",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            // 預測成長率面板
            var pnlPred = new Panel
            {
                Location    = new Point(10, cy),
                Size        = new Size(612, 88),
                BackColor   = Color.FromArgb(12, 32, 18),
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlPred.Controls.Add(new Label
            {
                Text = "📊  預測成長率（估算：Lv1初值 攻擊 19、防禦 12、敏捷 12，共升 139 次）",
                ForeColor = Color.FromArgb(80, 200, 100), Font = Theme.FontSmall, AutoSize = true, Location = new Point(8, 6)
            });
            _lblPredAtk   = new Label { ForeColor = Color.FromArgb(255, 185, 60),  Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold), AutoSize = true, Location = new Point(8,   28), Text = "攻擊成長：—" };
            _lblPredDef   = new Label { ForeColor = Color.FromArgb(100, 185, 255), Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold), AutoSize = true, Location = new Point(195, 28), Text = "防禦成長：—" };
            _lblPredAgi   = new Label { ForeColor = Color.FromArgb(185, 130, 255), Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold), AutoSize = true, Location = new Point(382, 28), Text = "敏捷成長：—" };
            _lblPredTotal = new Label { ForeColor = Color.FromArgb(255, 230, 60),  Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold), AutoSize = true, Location = new Point(8,   56), Text = "預測總成長：—" };
            pnlPred.Controls.AddRange(new Control[] { _lblPredAtk, _lblPredDef, _lblPredAgi, _lblPredTotal });
            card.Controls.Add(pnlPred);
            cy += 96;

            card.Controls.Add(new Label
            {
                Text      = "※ petmakeabi 只能生成給自己（無 CDKEY 欄位）",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            card.Height = cy + 8;
            return card;
        }

        // ── 成長率反推卡片 ────────────────────────────────────────
        private Panel BuildGrowthCard()
        {
            var card = MakeCard("🔢  成長率反推指令（成長率 → 面板目標 → GM 指令）");
            int cy = 36;

            card.Controls.Add(new Label
            {
                Text      = "  輸入預期成長率與最終血量，自動反推目標面板並產生指令",
                ForeColor = Color.FromArgb(140, 200, 255),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            _growHp  = MakeNud(1, 999999, 1000, 120);
            _growHp.Location = new Point(110, cy); _growHp.Width = 120;
            card.Controls.Add(MakeLabel("最終血量 HP：", new Point(10, cy + 2)));
            card.Controls.Add(_growHp);
            cy += 30;

            _growAtk = MakeGrowNud(7.266m);
            _growAtk.Location = new Point(110, cy); _growAtk.Width = 110;
            card.Controls.Add(MakeLabel("攻擊成長率：", new Point(10, cy + 2)));
            card.Controls.Add(_growAtk);
            card.Controls.Add(new Label { Text = "（每升 1 級增加的攻擊）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(228, cy + 4) });
            cy += 30;

            _growDef = MakeGrowNud(3.158m);
            _growDef.Location = new Point(110, cy); _growDef.Width = 110;
            card.Controls.Add(MakeLabel("防禦成長率：", new Point(10, cy + 2)));
            card.Controls.Add(_growDef);
            card.Controls.Add(new Label { Text = "（每升 1 級增加的防禦）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(228, cy + 4) });
            cy += 30;

            _growAgi = MakeGrowNud(2.878m);
            _growAgi.Location = new Point(110, cy); _growAgi.Width = 110;
            card.Controls.Add(MakeLabel("敏捷成長率：", new Point(10, cy + 2)));
            card.Controls.Add(_growAgi);
            card.Controls.Add(new Label { Text = "（每升 1 級增加的敏捷）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(228, cy + 4) });
            cy += 30;

            _lblGrowCalc = new Label
            {
                Text      = "推導目標面板：（請輸入成長率後自動計算）",
                ForeColor = Color.FromArgb(160, 220, 180),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            };
            card.Controls.Add(_lblGrowCalc);
            cy += 22;

            _growOut = new TextBox
            {
                ReadOnly    = true,
                BackColor   = Color.FromArgb(20, 20, 40),
                ForeColor   = Color.FromArgb(160, 200, 255),
                Font        = new Font("Consolas", 11f, FontStyle.Bold),
                Location    = new Point(110, cy),
                Width       = 400,
                BorderStyle = BorderStyle.FixedSingle
            };
            var btnCopyGrow = Theme.MakeButton("複製", Color.FromArgb(40, 60, 120), Color.FromArgb(140, 200, 255), 60, 26);
            btnCopyGrow.Location = new Point(518, cy);
            btnCopyGrow.Font     = Theme.FontSmall;
            btnCopyGrow.Click   += (_, __) => DoCopy(_growOut.Text, btnCopyGrow);

            card.Controls.Add(new Label { Text = "指令預覽：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(10, cy + 4), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card.Controls.Add(_growOut);
            card.Controls.Add(btnCopyGrow);
            cy += 34;

            card.Controls.Add(new Label
            {
                Text      = "※ ATK目標=round(成長×139+19)　DEF目標=round(成長×139+12)　AGI目標=round(成長×139+12)",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            foreach (var n in new[] { _growHp, _growAtk, _growDef, _growAgi })
                n.ValueChanged += (_, __) => RefreshGrow();

            card.Height = cy + 8;
            return card;
        }

        // ══════════════════════════════════════════════════════════
        //  資料邏輯
        // ══════════════════════════════════════════════════════════
        private void LoadPetList(string kw)
        {
            var list = GameDataManager.Instance.SearchPets(kw);
            _petTypes = list;
            _lstPet.BeginUpdate();
            _lstPet.Items.Clear();
            foreach (var p in list)
                _lstPet.Items.Add($"{p.Id}  {p.Name}");
            _lstPet.EndUpdate();
            if (_lstPet.Items.Count > 0 && string.IsNullOrWhiteSpace(kw))
                _lstPet.SelectedIndex = 0;
        }

        private void OnPetSelected(object? sender, EventArgs e)
        {
            int idx = _lstPet.SelectedIndex;
            if (idx < 0 || idx >= _petTypes.Count) return;
            var p = _petTypes[idx];
            _nudPetId.Value  = Math.Max(0, Math.Min(99999, p.Id));
            _lblPetInfo.Text = $"✓  {p.Name}（編號 {p.Id}）";
            RefreshAll();
        }

        private async Task DoSearchAsync()
        {
            string kw = _txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(kw)) return;

            _lblResult.ForeColor = Theme.TextMuted;
            _lblResult.Text      = "⏳ 搜尋中…";

            try
            {
                // 支援主帳號展開 + 複選
                var picks = await PlayerPickerHelper.PickMultiAsync(this, kw, multiMode: true);
                if (picks == null || picks.Count == 0) { _lblResult.Text = ""; return; }

                _pickedPlayers = picks;
                _foundCdkey    = picks[0].Account;
                _foundName     = picks[0].OnlineName;

                if (picks.Count == 1)
                {
                    _txtSearch.Text      = _foundName.Length > 0 ? _foundName : _foundCdkey;
                    _txtCdkey.Text       = _foundCdkey;
                    _lblResult.ForeColor = Color.FromArgb(100, 255, 150);
                    _lblResult.Text      = $"✓ {_foundName}（{_foundCdkey}）";
                }
                else
                {
                    string names = string.Join("、", picks.Select(p => p.OnlineName.Length > 0 ? p.OnlineName : p.Account));
                    _txtSearch.Text      = names;
                    _txtCdkey.Text       = _foundCdkey;
                    _lblResult.ForeColor = Color.FromArgb(100, 200, 255);
                    _lblResult.Text      = $"✓ 已選取 {picks.Count} 個角色：{names}";
                }
                if (_chkCdkey.Checked) RefreshMk();
            }
            catch (Exception ex)
            {
                _lblResult.ForeColor = Theme.AccentOrange;
                _lblResult.Text      = "✗ " + ex.Message;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  指令刷新
        // ══════════════════════════════════════════════════════════
        private int CurrentPetId => (int)_nudPetId.Value;

        private void RefreshAll() { RefreshMk(); RefreshAbi(); RefreshGrow(); RefreshTotalGrow(); }

        private void RefreshMk()
        {
            if (_mkOut == null) return;
            bool useCdkey = _chkCdkey?.Checked == true;

            if (useCdkey && _pickedPlayers.Count > 1)
            {
                // 多角色：每人一行指令
                var lines = _pickedPlayers.Select(p =>
                    $"[gm petmake {CurrentPetId} {(int)_mkLv.Value} {(int)_mkReb.Value} {p.Account}]");
                _mkOut.Text = string.Join(Environment.NewLine, lines);
            }
            else
            {
                string cdkeyPart = (useCdkey && !string.IsNullOrWhiteSpace(_txtCdkey?.Text))
                    ? $" {_txtCdkey.Text.Trim()}"
                    : "";
                _mkOut.Text = $"[gm petmake {CurrentPetId} {(int)_mkLv.Value} {(int)_mkReb.Value}{cdkeyPart}]";
            }
        }

        private void RefreshAbi()
        {
            if (_abiOut == null) return;
            long tHp  = (long)_abiHp.Value;
            long tAtk = (long)_abiAtk.Value;
            long tDef = (long)_abiDef.Value;
            long tAgi = (long)_abiSpd.Value;
            _abiOut.Text = $"[gm petmakeabi {CurrentPetId} {tHp} {tAtk} {tDef} {tAgi} 140 1]";
            if (_lblPredAtk == null) return;
            double pAtk = (tAtk - 19.0) / 139.0, pDef = (tDef - 12.0) / 139.0, pAgi = (tAgi - 12.0) / 139.0;
            _lblPredAtk.Text   = $"攻擊成長：{pAtk:F3}";
            _lblPredDef.Text   = $"防禦成長：{pDef:F3}";
            _lblPredAgi.Text   = $"敏捷成長：{pAgi:F3}";
            _lblPredTotal.Text = $"預測總成長：{pAtk + pDef + pAgi:F3}";
        }

        private void RefreshGrow()
        {
            if (_growOut == null) return;
            long   hp   = (long)_growHp.Value;
            double gAtk = (double)_growAtk.Value;
            double gDef = (double)_growDef.Value;
            double gAgi = (double)_growAgi.Value;
            long iAtk = (long)Math.Round(gAtk * 139 + 19);
            long iDef = (long)Math.Round(gDef * 139 + 12);
            long iAgi = (long)Math.Round(gAgi * 139 + 12);
            long iHp  = (long)Math.Round(hp / 0.0764);
            _growOut.Text     = $"[gm petmakeabi {CurrentPetId} {iHp} {iAtk} {iDef} {iAgi} 140 1]";
            if (_lblGrowCalc != null)
                _lblGrowCalc.Text = $"GM 寫入值：HP = {iHp}　ATK = {iAtk}　DEF = {iDef}　AGI = {iAgi}";
        }

        // ── 精準三圍反推卡片（兩段式）────────────────────────────
        private Panel BuildTotalGrowthCard()
        {
            var card = MakeCard("✅  精準三圍反推指令（直接輸入各成長率，兩段式計算）");
            int cy = 36;

            card.Controls.Add(new Label
            {
                Text      = "  直接輸入三圍成長率與目標血量 → 精準還原 140 等面板 → 生成 GM 指令",
                ForeColor = Color.FromArgb(130, 220, 130),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            // 最終血量
            _tgHp = MakeNud(1, 999999, 2050, 120);
            _tgHp.Location = new Point(110, cy); _tgHp.Width = 120;
            card.Controls.Add(MakeLabel("最終血量 HP：", new Point(10, cy + 2)));
            card.Controls.Add(_tgHp);
            cy += 30;

            // 三圍成長率（直接輸入，3 位小數）
            _tgGrowAtk = MakeGrowNud(3.1m);
            _tgGrowAtk.Location = new Point(110, cy); _tgGrowAtk.Width = 110;
            card.Controls.Add(MakeLabel("預期攻擊成長：", new Point(10, cy + 2)));
            card.Controls.Add(_tgGrowAtk);
            card.Controls.Add(new Label { Text = "（每升 1 級增加的攻擊）", ForeColor = Color.FromArgb(255, 185, 60), Font = Theme.FontSmall, AutoSize = true, Location = new Point(228, cy + 4) });
            cy += 30;

            _tgGrowDef = MakeGrowNud(2.1m);
            _tgGrowDef.Location = new Point(110, cy); _tgGrowDef.Width = 110;
            card.Controls.Add(MakeLabel("預期防禦成長：", new Point(10, cy + 2)));
            card.Controls.Add(_tgGrowDef);
            card.Controls.Add(new Label { Text = "（每升 1 級增加的防禦）", ForeColor = Color.FromArgb(100, 185, 255), Font = Theme.FontSmall, AutoSize = true, Location = new Point(228, cy + 4) });
            cy += 30;

            _tgGrowAgi = MakeGrowNud(2.0m);
            _tgGrowAgi.Location = new Point(110, cy); _tgGrowAgi.Width = 110;
            card.Controls.Add(MakeLabel("預期敏捷成長：", new Point(10, cy + 2)));
            card.Controls.Add(_tgGrowAgi);
            card.Controls.Add(new Label { Text = "（每升 1 級增加的敏捷）", ForeColor = Color.FromArgb(185, 130, 255), Font = Theme.FontSmall, AutoSize = true, Location = new Point(228, cy + 4) });
            cy += 30;

            // 唯讀預期總成長
            _lblTgSum = new Label
            {
                Text      = "7.200",
                ForeColor = Color.FromArgb(255, 230, 80),
                Font      = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(110, cy)
            };
            card.Controls.Add(MakeLabel("預期總成長：", new Point(10, cy + 2)));
            card.Controls.Add(_lblTgSum);
            cy += 26;

            // GM 寫入參數顯示
            _lblTgCalc = new Label
            {
                Text      = "GM 寫入值：（請輸入成長率後自動計算）",
                ForeColor = Color.FromArgb(160, 220, 180),
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            };
            card.Controls.Add(_lblTgCalc);
            cy += 22;

            // 指令輸出
            _tgOut = new TextBox
            {
                ReadOnly    = true,
                BackColor   = Color.FromArgb(20, 20, 40),
                ForeColor   = Color.FromArgb(255, 210, 120),
                Font        = new Font("Consolas", 11f, FontStyle.Bold),
                Location    = new Point(110, cy),
                Width       = 400,
                BorderStyle = BorderStyle.FixedSingle
            };
            var btnCopyTg = Theme.MakeButton("複製", Color.FromArgb(80, 55, 10), Color.FromArgb(255, 210, 100), 60, 26);
            btnCopyTg.Location = new Point(518, cy);
            btnCopyTg.Font     = Theme.FontSmall;
            btnCopyTg.Click   += (_, __) => DoCopy(_tgOut.Text, btnCopyTg);
            card.Controls.Add(new Label { Text = "指令預覽：", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, Location = new Point(10, cy + 4), Width = 96, TextAlign = ContentAlignment.MiddleRight });
            card.Controls.Add(_tgOut);
            card.Controls.Add(btnCopyTg);
            cy += 34;

            card.Controls.Add(new Label
            {
                Text      = "※ HP = round(目標血量÷0.0764) 破防　ATK/DEF/AGI = round(成長×139+初值) 1:1 直接寫入",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(10, cy),
                AutoSize  = true
            });
            cy += 22;

            foreach (var n in new NumericUpDown[] { _tgHp, _tgGrowAtk, _tgGrowDef, _tgGrowAgi })
                n.ValueChanged += (_, __) => RefreshTotalGrow();

            card.Height = cy + 8;
            return card;
        }

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

            // 步驟1：成長率 → 面板數值（1:1）
            long iAtk = (long)Math.Round(gAtk * 139 + 19);
            long iDef = (long)Math.Round(gDef * 139 + 12);
            long iAgi = (long)Math.Round(gAgi * 139 + 12);

            // 步驟2：HP 破防公式
            long iHp = (long)Math.Round(hp / 0.0764);

            _tgOut.Text = $"[gm petmakeabi {CurrentPetId} {iHp} {iAtk} {iDef} {iAgi} 140 1]";

            if (_lblTgCalc != null)
                _lblTgCalc.Text = $"GM 寫入值：HP = {iHp}　ATK = {iAtk}　DEF = {iDef}　AGI = {iAgi}";
        }

        // ══════════════════════════════════════════════════════════
        //  工具方法
        // ══════════════════════════════════════════════════════════
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

        private static Panel MakeCard(string title)
        {
            int cw = 650;
            var card = new Panel
            {
                Width     = cw,
                BackColor = Theme.BgCard,
                Padding   = new Padding(0)
            };
            card.Controls.Add(new Label
            {
                Text      = title,
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontBody,
                Location  = new Point(10, 6),
                AutoSize  = true
            });
            return card;
        }

        private static int CardWidth() => 650;

        private static Panel Spacer(int h)
        {
            return new Panel { Width = 650, Height = h, BackColor = Theme.BgPage };
        }

        private static Label MakeLabel(string text, Point loc)
        {
            return new Label
            {
                Text      = text,
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Location  = loc,
                Width     = 96,
                TextAlign = ContentAlignment.MiddleRight
            };
        }

        private static NumericUpDown MakeNud(int min, int max, int val, int w)
        {
            return new NumericUpDown
            {
                Minimum   = min,
                Maximum   = max,
                Value     = val,
                BackColor = Theme.BgLight,
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontBody,
                Width     = w
            };
        }

        private static NumericUpDown MakeGrowNud(decimal defaultVal = 0m)
        {
            return new NumericUpDown
            {
                Minimum       = 0m,
                Maximum       = 99.999m,
                Value         = Math.Max(0m, Math.Min(99.999m, defaultVal)),
                DecimalPlaces = 3,
                Increment     = 0.001m,
                BackColor     = Theme.BgLight,
                ForeColor     = Color.FromArgb(255, 215, 100),
                Font          = Theme.FontBody,
                Width         = 110
            };
        }
    }
}
