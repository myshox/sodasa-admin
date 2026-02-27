using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    // ────────────────────────────────────────────────────────────────
    // 主帳號分配儲值對話框
    // 讓管理員為旗下每個 CDKEY 個別輸入 NT$ 金額 + 優惠% → 批次執行儲值
    // ────────────────────────────────────────────────────────────────
    public class MasterSplitRechargeDialog : Form
    {
        // ── 套餐定義（與 EXE PayTotalDialog 一致）──────────────────
        private static readonly (string Label, long Twd, long Gold)[] TIERS =
        {
            ("NT$100  |  1萬金",   100,    10_000),
            ("NT$300  |  3.2萬",   300,    32_000),
            ("NT$500  |  5.5萬",   500,    55_000),
            ("NT$1K   |  11.5萬", 1_000,  115_000),
            ("NT$3K   |  36萬",   3_000,  360_000),
            ("NT$5K   |  62.5萬", 5_000,  625_000),
            ("NT$10K  |  130萬",  10_000, 1_300_000),
        };
        private static readonly int[] BONUSES = { 0, 5, 10, 15, 20 };

        // 每個 CDKEY 的狀態
        private class SplitRow
        {
            public PlayerInfo Player;
            public CheckBox   ChkEnabled;
            public ComboBox   CmbTier;
            public NumericUpDown NudCustomTwd;
            public ComboBox   CmbBonus;
            public Label      LblPreview;

            public long TierTwd  => CmbTier.SelectedIndex > 0
                                    ? TIERS[CmbTier.SelectedIndex - 1].Twd : 0;
            public long TierGold => CmbTier.SelectedIndex > 0
                                    ? TIERS[CmbTier.SelectedIndex - 1].Gold : 0;
            public long EffTwd   => TierTwd > 0 ? TierTwd : (long)NudCustomTwd.Value;
            public long BaseGold
            {
                get
                {
                    if (TierTwd > 0) return TierGold;
                    long twd = (long)NudCustomTwd.Value;
                    if (twd <= 0) return 0;
                    // 找最高適用套餐匯率（比較 gold/twd ratio）
                    var best = TIERS[0];
                    foreach (var t in TIERS) if (twd >= t.Twd) best = t;
                    return (long)Math.Floor(twd * ((double)best.Gold / best.Twd));
                }
            }
            public int  BonusPct  => CmbBonus.SelectedIndex >= 0 ? BONUSES[CmbBonus.SelectedIndex] : 0;
            public long TotalGold => (long)Math.Round(BaseGold * (1 + BonusPct / 100.0));
        }

        private readonly string           _masterName;
        private readonly List<PlayerInfo>  _subs;
        private readonly List<SplitRow>    _rows = new();
        private Label   _lblTotal;
        private Button  _btnOk;
        public  bool    AnyDone { get; private set; }

        // ── 建構子 ──────────────────────────────────────────────────
        public MasterSplitRechargeDialog(string masterName, List<PlayerInfo> subs)
        {
            _masterName = masterName;
            _subs       = subs;

            Text          = $"💰 主帳號分配儲值 — {masterName}";
            Size          = new Size(960, 680);
            MinimumSize   = new Size(800, 500);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
        }

        // ── 建立 UI ─────────────────────────────────────────────────
        private void BuildUI()
        {
            // 底部按鈕列
            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 58,
                BackColor = Color.FromArgb(12, 16, 28),
                Padding = new Padding(14, 0, 14, 0),
            };
            btnBar.Controls.Add(new Panel
            {
                Dock = DockStyle.Top, Height = 1,
                BackColor = Color.FromArgb(40, 50, 80)
            });

            _lblTotal = new Label
            {
                Text = "合計：NT$ 0，發出 0 金，0 個帳號",
                Dock = DockStyle.Left,
                AutoSize = false, Width = 480,
                ForeColor = Color.FromArgb(255, 195, 50), Font = Theme.FontBody,
                TextAlign = ContentAlignment.MiddleLeft
            };
            btnBar.Controls.Add(_lblTotal);

            var btnCancel = new Button
            {
                Text = "取消", Width = 80,
                BackColor = Color.FromArgb(50, 55, 70), ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontBody, Cursor = Cursors.Hand,
                Dock = DockStyle.Right
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 120);
            btnCancel.Click += (_, __) => Close();
            btnBar.Controls.Add(btnCancel);

            _btnOk = new Button
            {
                Text = "💰 確認分配儲值",
                Width = 150,
                BackColor = Color.FromArgb(180, 100, 0),
                ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right
            };
            _btnOk.FlatAppearance.BorderColor = Color.FromArgb(220, 130, 20);
            _btnOk.Click += async (_, __) => await DoRechargeAsync();
            btnBar.Controls.Add(_btnOk);
            Controls.Add(btnBar);

            // 快速套用工具列
            var quickBar = new Panel
            {
                Dock = DockStyle.Top, Height = 40,
                BackColor = Color.FromArgb(16, 22, 38),
                Padding = new Padding(10, 0, 10, 0)
            };
            quickBar.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Color.FromArgb(35, 45, 70)
            });
            var lblQuick = new Label
            {
                Text = "快速套用（已勾選）：", ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall, AutoSize = true, Left = 10,
                Top = 11, Height = 20
            };
            quickBar.Controls.Add(lblQuick);

            int qx = 135;
            foreach (var t in TIERS)
            {
                var tier = t;
                var btn = new Button
                {
                    Text = tier.Label.Split('|')[0].Trim(), AutoSize = false,
                    Width = 68, Height = 24, Left = qx, Top = 8,
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                    BackColor = Color.FromArgb(30, 40, 60), ForeColor = Theme.TextPrimary,
                    Font = new Font(Theme.FontFamily, 7.5f)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 120);
                btn.Click += (_, __) =>
                {
                    int idx = Array.FindIndex(TIERS, x => x.Twd == tier.Twd) + 1;
                    foreach (var r in _rows) if (r.ChkEnabled.Checked)
                    {
                        r.CmbTier.SelectedIndex = idx;
                        r.NudCustomTwd.Value = 0;
                    }
                    RefreshTotal();
                };
                quickBar.Controls.Add(btn);
                qx += 72;
            }

            var lblBonus = new Label
            {
                Text = "優惠：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Left = qx + 6, Top = 11, Height = 20
            };
            quickBar.Controls.Add(lblBonus);
            qx += 48;
            foreach (var b in BONUSES)
            {
                var bonus = b;
                var btn = new Button
                {
                    Text = $"+{bonus}%", AutoSize = false, Width = 48, Height = 24,
                    Left = qx, Top = 8, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                    BackColor = Color.FromArgb(30, 40, 60), ForeColor = Theme.TextPrimary,
                    Font = new Font(Theme.FontFamily, 7.5f)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 120);
                btn.Click += (_, __) =>
                {
                    int idx = Array.IndexOf(BONUSES, bonus);
                    foreach (var r in _rows) if (r.ChkEnabled.Checked) r.CmbBonus.SelectedIndex = idx;
                    RefreshTotal();
                };
                quickBar.Controls.Add(btn);
                qx += 52;
            }
            Controls.Add(quickBar);

            // 標題列
            var header = new Panel
            {
                Dock = DockStyle.Top, Height = 36,
                BackColor = Color.FromArgb(14, 18, 30)
            };
            header.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Color.FromArgb(40, 50, 80)
            });
            void addHdr(string text, int x, int w, ContentAlignment align = ContentAlignment.MiddleLeft)
            {
                header.Controls.Add(new Label
                {
                    Text = text, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = false, Location = new Point(x, 0), Size = new Size(w, 36),
                    TextAlign = align
                });
            }
            addHdr("✓", 10, 30);
            addHdr("狀態", 44, 55);
            addHdr("帳號 / 角色", 102, 155);
            addHdr("累積儲值", 260, 85, ContentAlignment.MiddleRight);
            addHdr("套餐選擇", 355, 165);
            addHdr("自訂 NT$", 528, 90);
            addHdr("優惠%", 626, 76);
            addHdr("金幣預覽", 706, 200, ContentAlignment.MiddleRight);
            Controls.Add(header);

            // 滾動列表（Fill）
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgMid };

            int y = 4;
            foreach (var p in _subs)
            {
                var (sr, rowPanel) = BuildSplitRow(p, y);
                scroll.Controls.Add(rowPanel);
                _rows.Add(sr);
                y += 46;
            }
            Controls.Add(scroll);
        }

        // ── 建立單行 ────────────────────────────────────────────────
        private (SplitRow sr, Panel rowPanel) BuildSplitRow(PlayerInfo p, int yPos)
        {
            var rowPanel = new Panel
            {
                Location  = new Point(0, yPos),
                Size      = new Size(940, 44),
                BackColor = p.IsOnline ? Color.FromArgb(12, 26, 14) : Color.FromArgb(14, 18, 30),
                Anchor    = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            rowPanel.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(25, 32, 52)
            });

            var sr = new SplitRow { Player = p };

            // ☑ 勾選
            sr.ChkEnabled = new CheckBox
            {
                Location = new Point(12, 13), Size = new Size(20, 18),
                Checked = false, BackColor = Color.Transparent
            };
            sr.ChkEnabled.CheckedChanged += (_, __) => RefreshTotal();
            rowPanel.Controls.Add(sr.ChkEnabled);

            // 在線狀態點
            rowPanel.Controls.Add(new Label
            {
                Text = p.IsOnline ? "🟢" : (p.IsBanned ? "🔴" : "⚫"),
                Location = new Point(44, 12), AutoSize = true,
                Font = new Font(Theme.FontFamily, 8f), ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent
            });

            // 帳號 / 角色
            rowPanel.Controls.Add(new Label
            {
                Text = !string.IsNullOrWhiteSpace(p.OnlineName) ? $"{p.OnlineName}\n{p.Account}" : p.Account,
                Location = new Point(102, 5), Size = new Size(150, 36),
                ForeColor = Theme.TextPrimary, Font = new Font(Theme.FontFamily, 8f),
                BackColor = Color.Transparent
            });

            // 累積儲值
            rowPanel.Controls.Add(new Label
            {
                Text = p.PayTotal > 0 ? $"NT${p.PayTotal:N0}" : "—",
                Location = new Point(258, 12), Size = new Size(90, 20),
                ForeColor = Color.FromArgb(255, 195, 60),
                Font = new Font(Theme.FontFamily, 8f), TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            });

            // 套餐 ComboBox
            sr.CmbTier = new ComboBox
            {
                Location = new Point(356, 11), Size = new Size(162, 22),
                BackColor = Color.FromArgb(22, 28, 46), ForeColor = Theme.TextPrimary,
                Font = new Font(Theme.FontFamily, 7.5f), DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            sr.CmbTier.Items.Add("— 未選 —");
            foreach (var t in TIERS) sr.CmbTier.Items.Add(t.Label);
            sr.CmbTier.SelectedIndex = 0;
            sr.CmbTier.SelectedIndexChanged += (_, __) =>
            {
                if (sr.CmbTier.SelectedIndex > 0) sr.NudCustomTwd.Value = 0;
                if (!sr.ChkEnabled.Checked && sr.CmbTier.SelectedIndex > 0) sr.ChkEnabled.Checked = true;
                RefreshPreview(sr);
                RefreshTotal();
            };
            rowPanel.Controls.Add(sr.CmbTier);

            // 自訂 NT$
            sr.NudCustomTwd = new NumericUpDown
            {
                Location = new Point(528, 11), Size = new Size(90, 22),
                Minimum = 0, Maximum = 999_999, Value = 0, DecimalPlaces = 0,
                BackColor = Color.FromArgb(22, 28, 46), ForeColor = Theme.TextPrimary,
                Font = new Font(Theme.FontFamily, 8f), BorderStyle = BorderStyle.FixedSingle
            };
            sr.NudCustomTwd.ValueChanged += (_, __) =>
            {
                if (sr.NudCustomTwd.Value > 0)
                {
                    sr.CmbTier.SelectedIndex = 0;
                    if (!sr.ChkEnabled.Checked) sr.ChkEnabled.Checked = true;
                }
                RefreshPreview(sr);
                RefreshTotal();
            };
            rowPanel.Controls.Add(sr.NudCustomTwd);

            // 優惠% ComboBox
            sr.CmbBonus = new ComboBox
            {
                Location = new Point(628, 11), Size = new Size(70, 22),
                BackColor = Color.FromArgb(22, 28, 46), ForeColor = Theme.TextPrimary,
                Font = new Font(Theme.FontFamily, 7.5f), DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var b in BONUSES) sr.CmbBonus.Items.Add($"+{b}%");
            sr.CmbBonus.SelectedIndex = 0;
            sr.CmbBonus.SelectedIndexChanged += (_, __) => { RefreshPreview(sr); RefreshTotal(); };
            rowPanel.Controls.Add(sr.CmbBonus);

            // 金幣預覽 Label
            sr.LblPreview = new Label
            {
                Location = new Point(706, 6), Size = new Size(220, 34),
                ForeColor = Color.FromArgb(86, 196, 118), Font = new Font(Theme.FontFamily, 7.5f),
                BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight
            };
            rowPanel.Controls.Add(sr.LblPreview);

            RefreshPreview(sr);
            return (sr, rowPanel);
        }

        // ── 刷新單行預覽 ────────────────────────────────────────────
        private void RefreshPreview(SplitRow sr)
        {
            long twd = sr.EffTwd;
            if (twd <= 0) { sr.LblPreview.Text = "—"; sr.LblPreview.ForeColor = Theme.TextMuted; return; }
            long bg = sr.BaseGold, tg = sr.TotalGold;
            string bonus = sr.BonusPct > 0 ? $" +{sr.BonusPct}%" : "";
            sr.LblPreview.Text = $"NT${twd:N0}{bonus}  →  {tg:N0} 金";
            sr.LblPreview.ForeColor = Color.FromArgb(86, 196, 118);
        }

        // ── 刷新合計 ────────────────────────────────────────────────
        private void RefreshTotal()
        {
            long totTwd = 0, totGold = 0; int cnt = 0;
            foreach (var r in _rows)
            {
                if (!r.ChkEnabled.Checked) continue;
                long t = r.EffTwd; if (t <= 0) continue;
                totTwd += t; totGold += r.TotalGold; cnt++;
            }
            _lblTotal.Text = $"合計：NT$ {totTwd:N0}，發出 {totGold:N0} 金，{cnt} 個帳號";
            _btnOk.Enabled = cnt > 0;
        }

        // ── 執行分配儲值 ─────────────────────────────────────────────
        private async Task DoRechargeAsync()
        {
            var items = _rows.Where(r => r.ChkEnabled.Checked && r.EffTwd > 0).ToList();
            if (items.Count == 0) { MessageBox.Show("請勾選至少一個有效帳號", "提示"); return; }

            // 建立確認訊息
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"確認為【{_masterName}】旗下帳號分配儲值？\n");
            foreach (var r in items)
            {
                string name = !string.IsNullOrWhiteSpace(r.Player.OnlineName) ? r.Player.OnlineName : r.Player.Account;
                sb.AppendLine($"• {name} ({r.Player.Account})");
                sb.AppendLine($"  NT${r.EffTwd:N0}  →  {r.TotalGold:N0} 金" +
                              (r.BonusPct > 0 ? $"（含+{r.BonusPct}%優惠）" : "") +
                              $"  累積+NT${r.EffTwd:N0}");
            }
            sb.AppendLine($"\n⚠ 累積儲值進度只計算台幣金額，優惠贈金不納入");

            if (MessageBox.Show(sb.ToString(), "確認分配儲值",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            _btnOk.Enabled = false;
            _btnOk.Text    = "處理中…";

            int done = 0; var failList = new List<string>();
            foreach (var r in items)
            {
                try
                {
                    bool ok = await DatabaseManager.Instance.AdjustPayDataPointAsync(
                        r.Player.Account, r.EffTwd, r.TotalGold, giveGold: true);
                    if (ok) { done++; AnyDone = true; }
                    else failList.Add(r.Player.Account + "（修改失敗）");
                }
                catch (Exception ex)
                {
                    failList.Add(r.Player.Account + "：" + ex.Message);
                }
            }

            string msg = $"✓ 完成 {done}/{items.Count} 個帳號";
            if (failList.Count > 0) msg += $"\n\n失敗：\n{string.Join("\n", failList)}";
            MessageBox.Show(msg, "分配儲值結果", MessageBoxButtons.OK,
                failList.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            _btnOk.Text    = "💰 確認分配儲值";
            _btnOk.Enabled = true;

            if (done > 0) Close();
        }
    }
}
