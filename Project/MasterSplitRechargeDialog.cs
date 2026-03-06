using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 主帳號分配儲值面板（UserControl，可直接嵌入任何 Panel）。
    /// 獨立使用時請透過 MasterSplitRechargeDialog.ShowAsDialog() 開啟。
    /// </summary>
    public class MasterSplitRechargeDialog : UserControl
    {
        private static readonly (string Label, long Twd, long Gold)[] TIERS =
        {
            ("NT$100",   100,    10_000),
            ("NT$300",   300,    32_000),
            ("NT$500",   500,    55_000),
            ("NT$1K",  1_000,  115_000),
            ("NT$3K",  3_000,  360_000),
            ("NT$5K",  5_000,  625_000),
            ("NT$10K",10_000,1_300_000),
        };
        private static readonly int[] BONUSES = { 0, 5, 10, 15, 20 };

        // ── 每列的資料狀態 ──────────────────────────────────────────
        private class SplitRow
        {
            public PlayerInfo Player;
            public bool  Enabled;
            public int   TierIndex = -1;   // -1=未選
            public long  CustomTwd;
            public int   BonusIdx  = 0;
            public long  EffTwd    => TierIndex >= 0 ? TIERS[TierIndex].Twd : CustomTwd;
            public long  BaseGold
            {
                get {
                    if (TierIndex >= 0) return TIERS[TierIndex].Gold;
                    if (CustomTwd <= 0) return 0;
                    var best = TIERS[0];
                    foreach (var t in TIERS) if (CustomTwd >= t.Twd) best = t;
                    return (long)Math.Floor(CustomTwd * ((double)best.Gold / best.Twd));
                }
            }
            public int  BonusPct  => BONUSES[BonusIdx];
            public long TotalGold => (long)Math.Round(BaseGold * (1 + BonusPct / 100.0));

            // UI 控制項
            public Panel      RowPanel;
            public CheckBox   Chk;
            public Button[]   TierBtns  = new Button[7];
            public Button[]   BonusBtns = new Button[5];
            public NumericUpDown NudCustom;
            public Label      LblPreview;
        }

        private readonly string          _masterName;
        private readonly List<PlayerInfo> _subs;
        private readonly bool            _embedded;          // true = 嵌入在父視窗中，不顯示取消按鈕
        private readonly List<SplitRow>  _rows = new();
        private Label   _lblTotal;
        private Button  _btnOk;
        private Panel   _scrollPanel;
        private Label   _hdrGoldPreview;   // 表頭「金幣預覽」隨 resize 同步
        public  bool    AnyDone { get; private set; }
        /// <summary>嵌入模式下，完成儲值後觸發（讓父視窗刷新）</summary>
        public  Action? OnAfterRecharge { get; set; }
        /// <summary>獨立視窗模式：使用者按「取消」時觸發，父 Form 負責關閉</summary>
        public event EventHandler? CloseRequested;

        // 顏色常數
        private static readonly Color ColBg        = Color.FromArgb(14, 18, 30);
        private static readonly Color ColBgRow     = Color.FromArgb(16, 21, 36);
        private static readonly Color ColBgRowOn   = Color.FromArgb(12, 26, 14);
        private static readonly Color ColBgRowSel  = Color.FromArgb(22, 28, 16);
        private static readonly Color ColBtnDef    = Color.FromArgb(28, 36, 58);
        private static readonly Color ColBtnSel    = Color.FromArgb(180, 100, 0);
        private static readonly Color ColBtnBonus  = Color.FromArgb(20, 60, 120);
        private static readonly Color ColBtnBonusSel = Color.FromArgb(30, 100, 200);
        private static readonly Color ColBorder    = Color.FromArgb(35, 45, 70);
        private static readonly Color ColOrange    = Color.FromArgb(255, 195, 50);
        private static readonly Color ColGreen     = Color.FromArgb(86, 196, 118);
        private const int ROW_H = 62;

        public MasterSplitRechargeDialog(string masterName, List<PlayerInfo> subs, bool embedded = false)
        {
            _masterName = masterName;
            _subs       = subs;
            _embedded   = embedded;
            BackColor   = ColBg;
            ForeColor   = Theme.TextPrimary;
            Font        = Theme.FontBody;
            Dock        = DockStyle.Fill;   // 預設填滿父容器
            BuildUI();
        }

        /// <summary>以獨立對話框方式開啟（供 MasterAccountForm 使用）</summary>
        public static bool ShowAsDialog(string masterName, List<PlayerInfo> subs, IWin32Window? owner = null)
        {
            var panel = new MasterSplitRechargeDialog(masterName, subs, embedded: false);
            using var frm = new Form
            {
                Text          = $"💰 主帳號分配儲值 — {masterName}",
                Size          = new Size(1040, 720),
                MinimumSize   = new Size(860, 480),
                BackColor     = ColBg,
                ForeColor     = Theme.TextPrimary,
                Font          = Theme.FontBody,
                StartPosition = FormStartPosition.CenterParent
            };
            panel.CloseRequested += (_, __) => frm.Close();
            frm.Controls.Add(panel);
            frm.ShowDialog(owner);
            return panel.AnyDone;
        }

        private void BuildUI()
        {
            // ── 底部確認列 ────────────────────────────────────────────
            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 56,
                BackColor = Color.FromArgb(10, 14, 24),
                Padding = new Padding(14, 8, 14, 8)
            };
            btnBar.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColBorder });

            _lblTotal = new Label
            {
                Text = "合計：NT$ 0，發出 0 金，0 個帳號",
                Dock = DockStyle.Fill, ForeColor = ColOrange,
                Font = Theme.FontBody, TextAlign = ContentAlignment.MiddleLeft
            };
            btnBar.Controls.Add(_lblTotal);

            if (!_embedded)
            {
                var btnCancel = new Button
                {
                    Text = "取消", Size = new Size(80, 36), Dock = DockStyle.Right,
                    BackColor = Color.FromArgb(44, 50, 68), ForeColor = Theme.TextPrimary,
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
                };
                btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 110);
                btnCancel.Click += (_, __) => CloseRequested?.Invoke(this, EventArgs.Empty);
                btnBar.Controls.Add(btnCancel);
            }

            _btnOk = new Button
            {
                Text = "💰  確認分配儲值", Size = new Size(160, 36), Dock = DockStyle.Right,
                BackColor = ColBtnSel, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold), Cursor = Cursors.Hand
            };
            _btnOk.FlatAppearance.BorderColor = Color.FromArgb(220, 130, 20);
            _btnOk.Click += async (_, __) => await DoRechargeAsync();
            btnBar.Controls.Add(_btnOk);
            Controls.Add(btnBar);

            // ── 快速套用列（FlowLayout 不會截斷）────────────────────
            var quickBar = new Panel
            {
                Dock = DockStyle.Top, Height = 42,
                BackColor = Color.FromArgb(14, 20, 36),
                Padding = new Padding(10, 0, 10, 0)
            };
            quickBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ColBorder });

            var quickFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, BackColor = Color.Transparent,
                Padding = new Padding(0, 7, 0, 0), AutoScroll = false
            };

            quickFlow.Controls.Add(MakeLabel("批次套用已勾選：", 8));
            foreach (var t in TIERS)
            {
                var tier = t;
                var b = MakeQuickBtn(tier.Label);
                b.Click += (_, __) => {
                    int idx = Array.FindIndex(TIERS, x => x.Twd == tier.Twd);
                    foreach (var r in _rows) if (r.Enabled) SetTier(r, idx);
                    RefreshAll();
                };
                quickFlow.Controls.Add(b);
            }
            quickFlow.Controls.Add(MakeLabel("  優惠：", 6));
            foreach (var bonus in BONUSES)
            {
                var b2 = bonus;
                var bb = MakeQuickBtn($"+{b2}%");
                bb.Click += (_, __) => {
                    int idx = Array.IndexOf(BONUSES, b2);
                    foreach (var r in _rows) if (r.Enabled) SetBonus(r, idx);
                    RefreshAll();
                };
                quickFlow.Controls.Add(bb);
            }
            // 清除按鈕
            var btnClear = MakeQuickBtn("✕ 清除");
            btnClear.BackColor = Color.FromArgb(80, 25, 25);
            btnClear.ForeColor = Color.FromArgb(245, 100, 100);
            btnClear.FlatAppearance.BorderColor = Color.FromArgb(140, 50, 50);
            btnClear.Margin = new Padding(10, 3, 0, 3);
            btnClear.Click += (_, __) => {
                foreach (var r in _rows) if (r.Enabled) { SetTier(r, -1); r.NudCustom.Value = 0; SetBonus(r, 0); }
                RefreshAll();
            };
            quickFlow.Controls.Add(btnClear);
            quickBar.Controls.Add(quickFlow);
            Controls.Add(quickBar);

            // ── 表頭 ─────────────────────────────────────────────────
            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 32,
                BackColor = Color.FromArgb(12, 16, 28)
            };
            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ColBorder });
            void Hdr(string t, int x, int w, ContentAlignment a = ContentAlignment.MiddleLeft) =>
                header.Controls.Add(new Label {
                    Text = t, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = false, Location = new Point(x, 0), Size = new Size(w, 32),
                    TextAlign = a, BackColor = Color.Transparent
                });
            Hdr("✓",         10,  26);
            Hdr("",          40,  20);   // status dot
            Hdr("帳號 / 角色", 64, 160);
            Hdr("套餐選擇",   228, 340);
            Hdr("自訂NT$",   574,  90);
            Hdr("優惠%",     672, 170);
            // 金幣預覽需隨 resize 同步，存為欄位
            _hdrGoldPreview = new Label
            {
                Text = "金幣預覽", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = false, Location = new Point(848, 0), Size = new Size(160, 32),
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
            };
            header.Controls.Add(_hdrGoldPreview);
            Controls.Add(header);

            // ── 滾動主體 ──────────────────────────────────────────────
            _scrollPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ColBg };
            int y = 0;
            foreach (var p in _subs)
            {
                var row = BuildRow(p, y);
                _scrollPanel.Controls.Add(row.RowPanel);
                _rows.Add(row);
                y += ROW_H;
            }
            Controls.Add(_scrollPanel);

            // Resize 時更新每列寬度；HandleCreated 後延遲初始化
            Resize       += (_, __) => UpdateRowWidths();
            HandleCreated += (_, __) => BeginInvoke((Action)UpdateRowWidths);

            RefreshAll();
        }

        // ── 建立單列 ─────────────────────────────────────────────────
        private SplitRow BuildRow(PlayerInfo p, int yPos)
        {
            var sr = new SplitRow { Player = p };
            sr.RowPanel = new Panel
            {
                Location  = new Point(0, yPos),
                Size      = new Size(990, ROW_H),
                BackColor = p.IsOnline ? ColBgRowOn : ColBgRow,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            sr.RowPanel.Controls.Add(new Panel
                { Dock = DockStyle.Bottom, Height = 1, BackColor = ColBorder });

            // ☑ 勾選
            sr.Chk = new CheckBox
            {
                Location = new Point(12, 21), Size = new Size(18, 18),
                BackColor = Color.Transparent, Checked = false
            };
            sr.Chk.CheckedChanged += (_, __) => {
                sr.Enabled = sr.Chk.Checked;
                sr.RowPanel.BackColor = sr.Enabled
                    ? (p.IsOnline ? ColBgRowSel : Color.FromArgb(20, 24, 42))
                    : (p.IsOnline ? ColBgRowOn  : ColBgRow);
                RefreshTotal();
            };
            sr.RowPanel.Controls.Add(sr.Chk);

            // 在線狀態
            sr.RowPanel.Controls.Add(new Label {
                Text = p.IsOnline ? "🟢" : (p.IsBanned ? "🔴" : "⚫"),
                Location = new Point(38, 20), AutoSize = true,
                Font = new Font(Theme.FontFamily, 9f), BackColor = Color.Transparent
            });

            // 角色 + CDKEY
            string nameStr = !string.IsNullOrWhiteSpace(p.OnlineName) ? p.OnlineName : "";
            sr.RowPanel.Controls.Add(new Label {
                Text = nameStr, Location = new Point(62, 8), Size = new Size(160, 20),
                ForeColor = Theme.TextPrimary, Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                BackColor = Color.Transparent, AutoEllipsis = true
            });
            sr.RowPanel.Controls.Add(new Label {
                Text = p.Account, Location = new Point(62, 28), Size = new Size(160, 18),
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 7.5f),
                BackColor = Color.Transparent, AutoEllipsis = true
            });
            if (p.PayTotal > 0)
                sr.RowPanel.Controls.Add(new Label {
                    Text = $"NT${p.PayTotal:N0}", Location = new Point(62, 44), Size = new Size(160, 14),
                    ForeColor = ColOrange, Font = new Font(Theme.FontFamily, 7f),
                    BackColor = Color.Transparent
                });

            // 套餐按鈕（7 個）
            int bx = 228;
            for (int i = 0; i < TIERS.Length; i++)
            {
                int idx = i;
                var tb = new Button
                {
                    Text = TIERS[i].Label, AutoSize = false,
                    Size = new Size(48, 24), Location = new Point(bx + idx * 50, 8),
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                    BackColor = ColBtnDef, ForeColor = Theme.TextPrimary,
                    Font = new Font(Theme.FontFamily, 7f), Tag = idx
                };
                tb.FlatAppearance.BorderColor = Color.FromArgb(55, 70, 110);
                tb.Click += (_, __) => { SetTier(sr, idx); RefreshAll(); };
                sr.TierBtns[i] = tb;
                sr.RowPanel.Controls.Add(tb);
            }
            // 套餐說明（小字）
            var lblSub = new Label
            {
                Location = new Point(228, 34), Size = new Size(352, 18),
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 6.5f),
                BackColor = Color.Transparent, Text = "1萬      3.2萬    5.5萬    11.5萬   36萬     62.5萬   130萬"
            };
            sr.RowPanel.Controls.Add(lblSub);

            // 自訂 NT$
            sr.NudCustom = new NumericUpDown
            {
                Location = new Point(574, 19), Size = new Size(86, 24),
                Minimum = 0, Maximum = 999_999, Value = 0, DecimalPlaces = 0,
                BackColor = Color.FromArgb(22, 28, 46), ForeColor = Theme.TextPrimary,
                Font = new Font(Theme.FontFamily, 8f), BorderStyle = BorderStyle.FixedSingle
            };
            sr.NudCustom.ValueChanged += (_, __) => {
                sr.CustomTwd = (long)sr.NudCustom.Value;
                if (sr.CustomTwd > 0) SetTier(sr, -1);
                if (!sr.Enabled && sr.CustomTwd > 0) sr.Chk.Checked = true;
                RefreshPreview(sr); RefreshTotal();
            };
            sr.RowPanel.Controls.Add(sr.NudCustom);

            // 優惠按鈕（5 個）
            for (int i = 0; i < BONUSES.Length; i++)
            {
                int bi = i;
                var bb = new Button
                {
                    Text = $"+{BONUSES[i]}%", AutoSize = false,
                    Size = new Size(34, 22), Location = new Point(672 + bi * 36, 19),
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                    BackColor = bi == 0 ? ColBtnBonusSel : ColBtnBonus,
                    ForeColor = bi == 0 ? Color.White : Theme.TextPrimary,
                    Font = new Font(Theme.FontFamily, 7f)
                };
                bb.FlatAppearance.BorderColor = Color.FromArgb(40, 80, 150);
                bb.Click += (_, __) => { SetBonus(sr, bi); RefreshAll(); };
                sr.BonusBtns[i] = bb;
                sr.RowPanel.Controls.Add(bb);
            }

            // 金幣預覽
            sr.LblPreview = new Label
            {
                Location = new Point(848, 10), Size = new Size(150, 42),
                ForeColor = ColGreen, Font = new Font(Theme.FontFamily, 8f),
                BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight
            };
            sr.RowPanel.Controls.Add(sr.LblPreview);

            return sr;
        }

        // ── 設定套餐 ──────────────────────────────────────────────────
        private void SetTier(SplitRow sr, int idx)
        {
            sr.TierIndex = idx;
            for (int i = 0; i < sr.TierBtns.Length; i++)
            {
                bool sel = (i == idx);
                sr.TierBtns[i].BackColor = sel ? ColBtnSel : ColBtnDef;
                sr.TierBtns[i].ForeColor = sel ? Color.White : Theme.TextPrimary;
                sr.TierBtns[i].FlatAppearance.BorderColor = sel
                    ? Color.FromArgb(220, 130, 20) : Color.FromArgb(55, 70, 110);
                sr.TierBtns[i].Font = new Font(Theme.FontFamily, sel ? 7.5f : 7f,
                    sel ? FontStyle.Bold : FontStyle.Regular);
            }
            if (idx >= 0 && sr.NudCustom.Value > 0) sr.NudCustom.Value = 0;
            if (idx >= 0 && !sr.Enabled) sr.Chk.Checked = true;
            RefreshPreview(sr);
        }

        // ── 設定優惠 ──────────────────────────────────────────────────
        private void SetBonus(SplitRow sr, int idx)
        {
            sr.BonusIdx = idx;
            for (int i = 0; i < sr.BonusBtns.Length; i++)
            {
                bool sel = (i == idx);
                sr.BonusBtns[i].BackColor = sel ? ColBtnBonusSel : ColBtnBonus;
                sr.BonusBtns[i].ForeColor = sel ? Color.White : Theme.TextPrimary;
            }
            RefreshPreview(sr);
        }

        // ── 刷新單列預覽 ──────────────────────────────────────────────
        private void RefreshPreview(SplitRow sr)
        {
            long twd = sr.EffTwd;
            if (twd <= 0) { sr.LblPreview.Text = "—"; sr.LblPreview.ForeColor = Theme.TextMuted; return; }
            long tg = sr.TotalGold;
            string bonus = sr.BonusPct > 0 ? $"\n+{sr.BonusPct}% 優惠" : "";
            sr.LblPreview.Text = $"NT${twd:N0} → {tg:N0} 金{bonus}";
            sr.LblPreview.ForeColor = ColGreen;
        }

        // ── 刷新合計 ─────────────────────────────────────────────────
        private void RefreshTotal()
        {
            long totTwd = 0, totGold = 0; int cnt = 0;
            foreach (var r in _rows)
            {
                if (!r.Enabled || r.EffTwd <= 0) continue;
                totTwd += r.EffTwd; totGold += r.TotalGold; cnt++;
            }
            _lblTotal.Text  = $"合計：NT$ {totTwd:N0}，發出 {totGold:N0} 金，{cnt} 個帳號";
            _btnOk.Enabled  = cnt > 0;
            _btnOk.BackColor = cnt > 0 ? ColBtnSel : Color.FromArgb(50, 55, 70);
        }

        private void RefreshAll() { foreach (var r in _rows) RefreshPreview(r); RefreshTotal(); }

        // ── 視窗 Resize 更新每列寬度 ─────────────────────────────────
        private void UpdateRowWidths()
        {
            int w = _scrollPanel.ClientSize.Width;
            foreach (var r in _rows)
            {
                r.RowPanel.Width = w;
                // 金幣預覽隨右側對齊
                if (r.LblPreview != null)
                {
                    r.LblPreview.Location = new Point(w - 170, 10);
                    r.LblPreview.Width    = 160;
                }
            }
            // 表頭「金幣預覽」同步對齊
            if (_hdrGoldPreview != null)
            {
                int hw = _scrollPanel.ClientSize.Width;
                _hdrGoldPreview.Location = new Point(hw - 170, 0);
                _hdrGoldPreview.Width    = 160;
            }
        }

        // ── 執行分配儲值 ─────────────────────────────────────────────
        private async Task DoRechargeAsync()
        {
            var items = _rows.Where(r => r.Enabled && r.EffTwd > 0).ToList();
            if (items.Count == 0) { MessageBox.Show("請勾選至少一個有效帳號", "提示"); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"確認為【{_masterName}】旗下帳號分配儲值？\n");
            foreach (var r in items)
            {
                string nm = !string.IsNullOrWhiteSpace(r.Player.OnlineName) ? r.Player.OnlineName : r.Player.Account;
                sb.AppendLine($"• {nm}（{r.Player.Account}）");
                sb.AppendLine($"  NT${r.EffTwd:N0}  →  {r.TotalGold:N0} 金" +
                              (r.BonusPct > 0 ? $"（含+{r.BonusPct}%優惠）" : ""));
            }
            sb.AppendLine($"\n⚠ 累積儲值紀錄只計算台幣金額，優惠贈金不納入");

            if (MessageBox.Show(sb.ToString(), "確認分配儲值",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            _btnOk.Enabled = false; _btnOk.Text = "處理中…";
            int done = 0; var fails = new List<string>();
            foreach (var r in items)
            {
                try
                {
                    bool ok = await DatabaseManager.Instance.AdjustPayDataPointAsync(
                        r.Player.Account, r.EffTwd, r.TotalGold, giveGold: true);
                    if (ok) { done++; AnyDone = true; }
                    else fails.Add(r.Player.Account + "（失敗）");
                }
                catch (Exception ex) { fails.Add(r.Player.Account + "：" + ex.Message); }
            }

            string msg = $"✓ 完成 {done}/{items.Count} 個帳號";
            if (fails.Count > 0) msg += $"\n\n失敗：\n{string.Join("\n", fails)}";
            MessageBox.Show(msg, "分配儲值結果", MessageBoxButtons.OK,
                fails.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            _btnOk.Text = "💰  確認分配儲值"; _btnOk.Enabled = true;
            if (done > 0)
            {
                if (_embedded) OnAfterRecharge?.Invoke();
                else CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // ── 輔助 ─────────────────────────────────────────────────────
        private static Button MakeQuickBtn(string text) => new Button
        {
            Text = text, AutoSize = false, Size = new Size(54, 26),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(28, 36, 58), ForeColor = Theme.TextPrimary,
            Font = new Font(Theme.FontFamily, 7.5f),
            Margin = new Padding(2, 3, 2, 3)
        };

        private static Label MakeLabel(string text, int topPad) => new Label
        {
            Text = text, AutoSize = true, ForeColor = Theme.TextMuted,
            Font = Theme.FontSmall, BackColor = Color.Transparent,
            Margin = new Padding(4, topPad, 0, 0)
        };
    }
}
