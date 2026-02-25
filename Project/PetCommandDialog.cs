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

        // ── petmakeabi 欄位 ───────────────────────────────────────
        private NumericUpDown _abiHp  = null!;
        private NumericUpDown _abiAtk = null!;
        private NumericUpDown _abiDef = null!;
        private NumericUpDown _abiSpd = null!;
        private NumericUpDown _abiLv  = null!;
        private NumericUpDown _abiReb = null!;
        private TextBox       _abiOut = null!;

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

            _abiHp  = MakeNud(1, 99999, 1314);
            _abiAtk = MakeNud(1, 99999, 327);
            _abiDef = MakeNud(1, 99999, 221);
            _abiSpd = MakeNud(1, 99999, 195);
            _abiLv  = MakeNud(1, 999,   140);
            _abiReb = MakeNud(0, 99,    0);
            _abiOut = MakeOut();

            AddRow(card2, ref cy, "血量(HP)", _abiHp);
            AddRow(card2, ref cy, "攻　　擊", _abiAtk);
            AddRow(card2, ref cy, "防　　禦", _abiDef);
            AddRow(card2, ref cy, "速度(敏)", _abiSpd);
            AddRow(card2, ref cy, "等　　級", _abiLv);
            AddRow(card2, ref cy, "轉　　數", _abiReb);

            var btnCopy2 = MakeCopyBtn();
            btnCopy2.Location = new Point(card2.Width - 100, cy);
            btnCopy2.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _abiOut.Location  = new Point(10, cy);
            _abiOut.Width     = card2.Width - 118;
            _abiOut.Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cy += 30;
            card2.Controls.AddRange(new Control[] { _abiOut, btnCopy2 });
            card2.Height = cy + 10;

            foreach (var n in new[] { _abiHp, _abiAtk, _abiDef, _abiSpd, _abiLv, _abiReb })
                n.ValueChanged += (_, __) => RefreshAbi();

            btnCopy2.Click += (_, __) => DoCopy(_abiOut.Text, btnCopy2);

            // ── 排版 ──────────────────────────────────────────────
            cardPet.Location = new Point(0, bodyY); bodyY += cardPet.Height + 8;
            card1.Location   = new Point(0, bodyY); bodyY += card1.Height + 8;
            card2.Location   = new Point(0, bodyY);

            body.Controls.AddRange(new Control[] { cardPet, card1, card2 });
            body.Resize += (_, __) =>
            {
                int w = body.ClientSize.Width - 4;
                cardPet.Width = w; card1.Width = w; card2.Width = w;
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

        private void RefreshAll()  { RefreshMk(); RefreshAbi(); }
        private void RefreshMk()   => _mkOut.Text  = BuildMk();
        private void RefreshAbi()  => _abiOut.Text = BuildAbi();

        private string BuildMk() =>
            $"[gm petmake {CurrentPetId} {(int)_mkLv.Value} {(int)_mkReb.Value}]";

        private string BuildAbi() =>
            $"[gm petmakeabi {CurrentPetId} {(int)_abiHp.Value} {(int)_abiAtk.Value} " +
            $"{(int)_abiDef.Value} {(int)_abiSpd.Value} {(int)_abiLv.Value} {(int)_abiReb.Value}]";

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
