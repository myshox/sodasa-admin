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
        private NumericUpDown _abiHp  = null!;
        private NumericUpDown _abiAtk = null!;
        private NumericUpDown _abiDef = null!;
        private NumericUpDown _abiSpd = null!;
        private NumericUpDown _abiLv  = null!;
        private NumericUpDown _abiReb = null!;
        private TextBox       _abiOut = null!;

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

            _abiHp  = MakeNud(1, 99999, 1000, 100);
            _abiAtk = MakeNud(0, 99999,  200,  80);
            _abiDef = MakeNud(0, 99999,  100,  80);
            _abiSpd = MakeNud(0, 99999,  100,  80);
            _abiLv  = MakeNud(1,   200,    1,  80);
            _abiReb = MakeNud(0,    20,    0,  80);

            foreach (var n in new[] { _abiHp, _abiAtk, _abiDef, _abiSpd, _abiLv, _abiReb })
                n.ValueChanged += (_, __) => RefreshAbi();

            // 血量
            _abiHp.Location = new Point(110, cy);
            _abiHp.Width    = 120;
            card.Controls.Add(MakeLabel("血量(HP)：", new Point(10, cy + 2)));
            card.Controls.Add(_abiHp);
            cy += 30;

            // 攻擊
            _abiAtk.Location = new Point(110, cy);
            _abiAtk.Width    = 120;
            card.Controls.Add(MakeLabel("攻　　擊：", new Point(10, cy + 2)));
            card.Controls.Add(_abiAtk);
            cy += 30;

            // 防禦
            _abiDef.Location = new Point(110, cy);
            _abiDef.Width    = 120;
            card.Controls.Add(MakeLabel("防　　禦：", new Point(10, cy + 2)));
            card.Controls.Add(_abiDef);
            cy += 30;

            // 速度
            _abiSpd.Location = new Point(110, cy);
            _abiSpd.Width    = 120;
            card.Controls.Add(MakeLabel("速　　度：", new Point(10, cy + 2)));
            card.Controls.Add(_abiSpd);
            cy += 30;

            // 等級
            _abiLv.Location = new Point(110, cy);
            _abiLv.Width    = 120;
            card.Controls.Add(MakeLabel("等　　級：", new Point(10, cy + 2)));
            card.Controls.Add(_abiLv);
            cy += 30;

            // 轉數
            _abiReb.Location = new Point(110, cy);
            _abiReb.Width    = 120;
            card.Controls.Add(MakeLabel("轉　　數：", new Point(10, cy + 2)));
            card.Controls.Add(_abiReb);
            cy += 30;

            // 輸出
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
                Text      = "※ petmakeabi 只能生成給自己（無 CDKEY 欄位）",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Location  = new Point(110, cy),
                AutoSize  = true
            });
            cy += 22;

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

        private void RefreshAll() { RefreshMk(); RefreshAbi(); }

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
            _abiOut.Text =
                $"[gm petmakeabi {CurrentPetId} {(int)_abiHp.Value} {(int)_abiAtk.Value} " +
                $"{(int)_abiDef.Value} {(int)_abiSpd.Value} {(int)_abiLv.Value} {(int)_abiReb.Value}]";
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
    }
}
