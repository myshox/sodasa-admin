using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 充值管理表單（對應網頁版 RechargePage）：
    ///   STEP 1 搜尋玩家 → STEP 2 選套餐 → STEP 3 選優惠% → 預覽 → 確認
    ///   + 台幣換算計算機  + 充值記錄查詢
    /// </summary>
    public class RechargeForm : Form
    {
        // ── 套餐（與 PayTotalDialog / AdjustRechargeDialog 一致）──
        private static readonly (string Label, string Sub, long Twd, long Gold)[] TIERS =
        {
            ("NT$100",  "1萬金",   100,    10_000),
            ("NT$300",  "3.2萬",   300,    32_000),
            ("NT$500",  "5.5萬",   500,    55_000),
            ("NT$1K",   "11.5萬", 1_000, 115_000),
            ("NT$3K",   "36萬",   3_000, 360_000),
            ("NT$5K",   "62.5萬", 5_000, 625_000),
            ("NT$10K",  "130萬", 10_000, 1_300_000),
        };
        private static readonly int[] BONUSES = { 0, 5, 10, 15, 20 };
        private const long CYCLE = 25_000L;

        // ── 玩家狀態 ───────────────────────────────────────────────
        private PlayerDetail _detail;           // null 表示尚未搜尋
        private string       _account;
        private int          _masterId   = 0;
        private string       _masterName = "";
        private List<PlayerInfo> _subs   = new();

        // ── STEP 2 套餐選擇狀態 ────────────────────────────────────
        private int  _selectedTierIdx = -1;     // -1 = 未選
        private int  _bonusPct = 0;
        private bool _giveGold = true;
        private bool _syncingGold = false;      // 防止 NT$↔金幣 雙向觸發
        private Label _lblTwdHint;              // 台幣旁動態提示（→ X 金幣）
        private Label _lblGoldHint;             // 金幣旁動態提示（→ 最少 NT$X）

        // ── UI 控件 ────────────────────────────────────────────────
        private TextBox   _txtSearch;
        private Button    _btnSearch;

        // 右側 tab 切換
        private Button    _btnTabSingle;
        private Button    _btnTabSplit;
        private Panel     _pnlSingleContent;   // Tab 1：新增儲值
        private Panel     _pnlSplitWrapper;    // Tab 2：分配儲值（嵌入）
        private MasterSplitRechargeDialog? _embeddedSplit;

        // 左：玩家資訊
        private Panel       _pnlPlayerInfo;
        private Label       _lblPlayerName, _lblPlayerAcct;
        private Label       _lblGold, _lblCrystal, _lblPayTotal, _lblVip;
        private Panel       _barCycleFill;
        private Label       _lblCyclePct, _lblCycleNum, _lblHistoryTotal;
        private Button      _btnClaim, _btnFix, _btnReset;
        private Panel       _pnlClaimRow;

        // 中：給予儲值
        private Button[]    _tierBtns;
        private Button[]    _bonusBtns;
        private NumericUpDown _nudTwd;
        private NumericUpDown _nudGold;
        private CheckBox    _chkGiveGold;
        private Panel       _pnlPreview;
        private Label       _lblPreviewTwd, _lblPreviewGold, _lblPreviewCycle;
        private Panel       _barBeforeFill, _barAfterFill;
        private Label       _lblBarBeforePct, _lblBarAfterPct;
        private Button      _btnConfirm;
        private Label       _lblMsg;

        // 下：充值記錄
        private TextBox     _txtHistQ;
        private DataGridView _dgvHistory;
        private Label       _lblHistStatus;

        public RechargeForm()
        {
            Text            = "💰 充值管理";
            Size            = new Size(1080, 780);
            MinimumSize     = new Size(900, 640);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            StartPosition   = FormStartPosition.CenterParent;
            BuildUI();
        }

        // ══════════════════════════════════════════════════════════════
        // UI 建構
        // ══════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── 頂部搜尋列 ──────────────────────────────────────────
            var topBar = new Panel
            {
                Dock = DockStyle.Top, Height = 56,
                BackColor = Color.FromArgb(22, 24, 36),
                Padding = new Padding(14, 0, 14, 0)
            };
            topBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            topBar.Controls.Add(new Label
            {
                Text = "💰  充值管理",
                ForeColor = Theme.AccentOrange,
                Font = new Font(Theme.FontFamily, 12f, FontStyle.Bold),
                AutoSize = true, Left = 14, Top = 15
            });
            _txtSearch = new TextBox
            {
                Width = 320, Height = 28, Left = 175, Top = 14,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody, BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "主帳號 / 角色名 / UID（主帳號可帶出全部子帳號）"
            };
            _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = SearchPlayerAsync(); } };
            topBar.Controls.Add(_txtSearch);
            _btnSearch = new Button
            {
                Text = "🔍 搜尋", Width = 90, Height = 28, Left = 502, Top = 14,
                BackColor = Theme.AccentBlue, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = Theme.FontBody
            };
            _btnSearch.FlatAppearance.BorderSize = 0;
            _btnSearch.Click += (s, e) => _ = SearchPlayerAsync();
            topBar.Controls.Add(_btnSearch);
            Controls.Add(topBar);

            // ── 狀態欄 ───────────────────────────────────────────────
            _lblMsg = new Label
            {
                Dock = DockStyle.Bottom, Height = 28,
                Text = "請輸入玩家帳號或角色名稱後按搜尋",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                BackColor = Color.FromArgb(16, 18, 28)
            };
            Controls.Add(_lblMsg);

            // ── 主體（SplitContainer：左玩家資訊 | 右操作區）────────
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
                BackColor = Theme.BgMid, SplitterWidth = 6
                // Panel1MinSize / Panel2MinSize 不能在建構時設定（此時 Width=0 會拋例外）
            };
            Controls.Add(split);
            split.HandleCreated += (_, __) =>
            {
                try
                {
                    split.Panel1MinSize = 240;
                    // Panel2MinSize 必須在寬度足夠時才設定，否則會拋 InvalidOperationException
                    if (split.Width > 240 + 480 + split.SplitterWidth)
                        split.Panel2MinSize = 480;
                    int d = Math.Max(270, Math.Min(split.Width - 500, (int)(split.Width * 0.27)));
                    if (d > split.Panel1MinSize) split.SplitterDistance = d;
                }
                catch { }
            };

            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);
        }

        // ── 左側：玩家資訊 ──────────────────────────────────────────
        private void BuildLeftPanel(SplitterPanel panel)
        {
            panel.BackColor = Color.FromArgb(20, 22, 34);
            panel.AutoScroll = true;

            _pnlPlayerInfo = new Panel
            {
                Dock = DockStyle.Top, Height = 400, Padding = new Padding(12),
                BackColor = Color.Transparent
            };
            panel.Controls.Add(_pnlPlayerInfo);

            // 預設提示
            _pnlPlayerInfo.Controls.Add(new Label
            {
                Text = "← 請先搜尋玩家",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Left = 12, Top = 14
            });
        }

        private void RebuildPlayerInfo()
        {
            _pnlPlayerInfo.Controls.Clear();
            if (_detail == null) { _pnlPlayerInfo.Controls.Add(new Label { Text = "← 請先搜尋玩家", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Left = 12, Top = 14 }); return; }

            const int x = 12;
            int y = 12;
            int W = _pnlPlayerInfo.Width - 24;

            // 在線狀態 + 姓名
            var dot = new Panel { Location = new Point(x, y + 8), Size = new Size(10, 10), BackColor = _detail.IsOnline ? Theme.AccentGreen : Theme.TextMuted };
            dot.Region = new Region(new Rectangle(0, 0, 10, 10));
            _pnlPlayerInfo.Controls.Add(dot);
            _lblPlayerName = new Label
            {
                Text = _detail.OnlineName, Location = new Point(x + 15, y + 1),
                ForeColor = Theme.TextPrimary, Font = new Font(Theme.FontFamily, 12f, FontStyle.Bold),
                AutoSize = true
            };
            _pnlPlayerInfo.Controls.Add(_lblPlayerName);
            y += 26;
            _lblPlayerAcct = new Label
            {
                Text = _detail.Account, Location = new Point(x + 15, y),
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true
            };
            _pnlPlayerInfo.Controls.Add(_lblPlayerAcct);
            y += 22;

            Div(x, y, W); y += 10;

            // 貨幣欄位
            InfoRow("💰 金幣（元寶）",  $"{_detail.Gold:N0}",     Theme.AccentOrange, x, ref y);
            InfoRow("💎 水晶",           $"{_detail.Crystal:N0}",  Theme.AccentBlue,   x, ref y);
            InfoRow("💳 累積儲值",       $"NT${_detail.PayTotal:N0}", Color.FromArgb(255, 200, 80), x, ref y);

            // VIP
            var (vipLv, vipEmoji, vipLbl, _) = VipHelper.GetTier(_detail.PayTotal);
            Color vipColor = vipLv == 2 ? Color.FromArgb(100, 180, 255)
                           : vipLv == 1 ? Theme.AccentOrange
                           : Theme.TextMuted;
            string vipLabel = vipLv == 2 ? "💎 鑽石 VIP"
                            : vipLv == 1 ? "🥇 黃金 VIP"
                            : "一般玩家";
            InfoRow("⭐ VIP 等級", vipLabel, vipColor, x, ref y);
            y += 4;

            Div(x, y, W); y += 8;

            // 循環進度
            long pt     = _detail.PayTotal;
            long inCyc  = pt % CYCLE;
            int  pct    = pt > 0 ? (int)(inCyc * 100 / CYCLE) : 0;
            long cycNum = _detail.TotalCheck;

            _pnlPlayerInfo.Controls.Add(new Label
            {
                Text = $"累積儲值進度（第 {cycNum + 1} 輪）  ·  每累積 NT$25,000 完成一輪，可領大獎",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            });
            y += 18;
            _lblCycleNum = new Label
            {
                Text = $"NT${pt:N0} / $25,000",
                ForeColor = Color.FromArgb(180, 210, 255), Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            };
            _pnlPlayerInfo.Controls.Add(_lblCycleNum);
            y += 18;

            var barBg = new Panel { Location = new Point(x, y), Size = new Size(W, 10), BackColor = Theme.BgCard };
            _barCycleFill = new Panel
            {
                Location = new Point(0, 0), Size = new Size(Math.Max(2, (int)(W * pct / 100.0)), 10),
                BackColor = Theme.AccentOrange
            };
            barBg.Controls.Add(_barCycleFill);
            _pnlPlayerInfo.Controls.Add(barBg);
            y += 14;

            _lblCyclePct = new Label
            {
                Text = $"{pct}%",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            };
            _pnlPlayerInfo.Controls.Add(_lblCyclePct);
            y += 18;

            _lblHistoryTotal = new Label
            {
                Text = $"歷史總計 NT${(_detail.LifetimePayTotal > 0 ? _detail.LifetimePayTotal : pt):N0}  ·  完成 {cycNum} 輪",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            };
            _pnlPlayerInfo.Controls.Add(_lblHistoryTotal);
            y += 22;

            // 🎁 發放獎勵按鈕（claimReady）
            _pnlClaimRow = new Panel { Location = new Point(x, y), Size = new Size(W, 34), BackColor = Color.Transparent };
            if (_detail.ClaimReady)
            {
                _btnClaim = Theme.MakeButton($"🎁 發放第 {_detail.TotalCheck} 輪累積獎勵", Color.FromArgb(180, 130, 20), Color.White, W, 30);
                _btnClaim.Font     = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
                _btnClaim.Location = new Point(0, 2);
                _btnClaim.Click += async (s, e) =>
                {
                    if (MessageBox.Show($"確定要發放「{_detail.OnlineName}」第 {_detail.TotalCheck} 輪的累積獎勵？\n\n  · check 設為 1（已領）\n  · 下次達成 NT$25,000 才能再領",
                        "🎁 發放累積獎勵", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    var (status, cycle) = await DatabaseManager.Instance.ClaimPaydataRewardAsync(_account);
                    if (status == "ok") { ShowMsg($"✅ 第 {cycle} 輪獎勵已發放", true); await RefreshDetailAsync(); }
                    else ShowMsg($"⚠ {status}", false);
                };
                _pnlClaimRow.Controls.Add(_btnClaim);
            }
            else if (_detail.TotalCheck > 0)
            {
                _pnlClaimRow.Controls.Add(new Label
                {
                    Text = $"✓ 第 {_detail.TotalCheck} 輪獎勵已發放",
                    ForeColor = Theme.AccentGreen, Font = Theme.FontSmall,
                    AutoSize = true, Location = new Point(0, 8)
                });
            }
            _pnlPlayerInfo.Controls.Add(_pnlClaimRow);
            y += 36;

            // 🔧 修復循環 + 🗑 清0進度
            var btnFix = Theme.MakeButton("🔧 修復進度顯示", Color.FromArgb(30, 90, 160), Color.White, (W - 4) / 2, 26);
            btnFix.Font     = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
            btnFix.Location = new Point(x, y);
            var tip = new ToolTip(); tip.SetToolTip(btnFix, "進度條顯示異常時使用，不會更動儲值金額，只修復顯示數值");
            btnFix.Click += async (s, e) =>
            {
                if (MessageBox.Show($"根據目前累積 NT${pt:N0} 修復進度顯示。\n此操作不會更動儲值金額，只修正進度顯示異常。確認？",
                    "🔧 修復進度顯示", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                bool ok = await DatabaseManager.Instance.FixPaydataCheckAsync(_account);
                if (ok) { ShowMsg("✅ check 欄位已修復", true); await RefreshDetailAsync(); }
                else ShowMsg("⚠ 修復失敗", false);
            };
            var btnReset = Theme.MakeButton("🗑 清0進度", Theme.AccentRed, Color.White, (W - 4) / 2, 26);
            btnReset.Font     = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold);
            btnReset.Location = new Point(x + (W - 4) / 2 + 4, y);
            btnReset.Click += async (s, e) =>
            {
                if (MessageBox.Show($"⚠ 確定要將「{_detail.OnlineName}」的累積充值進度歸零？\n\n  · paydata.point → 0\n  · check / totalcheck → 0\n  ✅ 歷史總累儲保留不動\n\n此操作無法復原。",
                    "⚠ 清0累儲確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                bool ok = await DatabaseManager.Instance.ResetPaydataProgressAsync(_account);
                if (ok) { ShowMsg("✅ 累儲進度已歸零", true); await RefreshDetailAsync(); }
                else ShowMsg("⚠ 清0失敗", false);
            };
            _pnlPlayerInfo.Controls.AddRange(new Control[] { btnFix, btnReset });
            y += 30;

            _pnlPlayerInfo.Height = y + 16;
        }

        private void InfoRow(string label, string value, Color valColor, int x, ref int y)
        {
            _pnlPlayerInfo.Controls.Add(new Label { Text = label, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(x, y) });
            _pnlPlayerInfo.Controls.Add(new Label { Text = value, ForeColor = valColor, Font = Theme.FontSmall, AutoSize = true, Location = new Point(x + 100, y) });
            y += 20;
        }

        private void Div(int x, int y, int w) =>
            _pnlPlayerInfo.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(w, 1), BackColor = Theme.Border });

        // ── 右側：分頁切換 → 新增儲值 | 分配儲值 ────────────────────
        private void BuildRightPanel(SplitterPanel panel)
        {
            panel.BackColor = Theme.BgPage;

            // ── Tab 切換列 ─────────────────────────────────────────
            var tabBar = new Panel
            {
                Dock = DockStyle.Top, Height = 44,
                BackColor = Color.FromArgb(16, 20, 34)
            };
            tabBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });

            _btnTabSingle = new Button
            {
                Text = "💰 新增儲值",
                Size = new Size(148, 34), Location = new Point(10, 5),
                BackColor = Theme.AccentBlue, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontBody, Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            _btnTabSingle.FlatAppearance.BorderSize = 0;
            _btnTabSingle.Click += (_, __) => SwitchTab(false);

            _btnTabSplit = new Button
            {
                Text = "📋 分配儲值",
                Size = new Size(148, 34), Location = new Point(162, 5),
                BackColor = Theme.BgCard, ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontBody, Cursor = Cursors.Hand,
                Enabled = false, UseVisualStyleBackColor = false
            };
            _btnTabSplit.FlatAppearance.BorderSize = 0;
            _btnTabSplit.Click += (_, __) => SwitchTab(true);
            new ToolTip().SetToolTip(_btnTabSplit, "搜尋到主帳號後，此 Tab 才會啟用");

            tabBar.Controls.AddRange(new Control[] { _btnTabSingle, _btnTabSplit });
            panel.Controls.Add(tabBar);

            // ── 分配儲值 Panel（後加，DockStyle.Fill 先到者先填）────
            _pnlSplitWrapper = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgPage, Visible = false
            };
            panel.Controls.Add(_pnlSplitWrapper);

            // ── 新增儲值 Panel ────────────────────────────────────
            _pnlSingleContent = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgPage
            };
            panel.Controls.Add(_pnlSingleContent);

            BuildSingleRechargeContent(_pnlSingleContent);
        }

        // ── Tab 切換 ────────────────────────────────────────────────
        private void SwitchTab(bool showSplit)
        {
            _pnlSingleContent.Visible = !showSplit;
            _pnlSplitWrapper.Visible  = showSplit;

            _btnTabSingle.BackColor = !showSplit ? Theme.AccentBlue : Theme.BgCard;
            _btnTabSingle.ForeColor = !showSplit ? Color.White : Theme.TextMuted;
            _btnTabSplit.BackColor  = showSplit  ? Theme.AccentBlue : Theme.BgCard;
            _btnTabSplit.ForeColor  = showSplit  ? Color.White : Theme.TextMuted;
        }

        // ── 重建嵌入式分配儲值 Panel ────────────────────────────────
        private void RebuildSplitPanel()
        {
            // 清除舊的嵌入表單
            if (_embeddedSplit != null)
            {
                _pnlSplitWrapper.Controls.Remove(_embeddedSplit);
                _embeddedSplit.Dispose();
                _embeddedSplit = null;
            }

            if (_subs.Count == 0)
            {
                _btnTabSplit.Enabled = false;
                _btnTabSplit.Text    = "📋 分配儲值";
                new ToolTip().SetToolTip(_btnTabSplit, "搜尋到主帳號後，此 Tab 才會啟用");
                SwitchTab(false);
                return;
            }

            // 建立嵌入式分配儲值表單
            _embeddedSplit = new MasterSplitRechargeDialog(_masterName, _subs, embedded: true)
            {
                TopLevel        = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock            = DockStyle.Fill
            };
            _embeddedSplit.OnAfterRecharge = async () =>
            {
                // 儲值後刷新子帳號資料
                if (_masterId > 0)
                    _subs = await DatabaseManager.Instance.GetSubAccountsAsync(_masterId);
                RebuildSplitPanel();
                SwitchTab(true);
            };
            _pnlSplitWrapper.Controls.Add(_embeddedSplit);
            _embeddedSplit.Show();

            _btnTabSplit.Enabled = true;
            _btnTabSplit.Text    = $"📋 分配儲值（{_subs.Count} 位）";
            new ToolTip().SetToolTip(_btnTabSplit, $"主帳號 {_masterName} 旗下 {_subs.Count} 個子帳號批次儲值");
        }

        // ── 新增儲值內容（原 BuildRightPanel 主體移至此）────────────
        private void BuildSingleRechargeContent(Panel container)
        {
            container.AutoScroll = true;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.BgPage };
            container.Controls.Add(scroll);

            int y = 14;
            const int x = 14;
            int W() => scroll.ClientSize.Width - 28;

            // ─── STEP 2：套餐選擇 ────────────────────────────────────
            SectionLabel(scroll, "STEP 2 — 選擇充值套餐", x, y); y += 22;
            scroll.Controls.Add(new Label
            {
                Text = "1台幣 = 100金幣（大額有加成）",
                ForeColor = Color.FromArgb(100, 190, 100), Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            });
            y += 20;

            _tierBtns = new Button[TIERS.Length];
            int bx = x;
            for (int i = 0; i < TIERS.Length; i++)
            {
                int idx = i;
                var (label, sub, _, _) = TIERS[i];
                var btn = new Button
                {
                    Text = label + "\n" + sub,
                    BackColor = Theme.BgCard, ForeColor = Color.FromArgb(200, 215, 255),
                    FlatStyle = FlatStyle.Flat, Font = new Font(Theme.FontFamily, 7.5f),
                    Size = new Size(80, 52), Location = new Point(bx, y),
                    Cursor = Cursors.Hand, UseVisualStyleBackColor = false
                };
                btn.FlatAppearance.BorderColor = Theme.Border;
                btn.Click += (s, e) => { SelectTier(idx); UpdatePreview(); UpdateManualHints(); };
                scroll.Controls.Add(btn);
                _tierBtns[i] = btn;
                bx += 84;
            }
            y += 58;

            // ─── STEP 3：優惠加成 ────────────────────────────────────
            scroll.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(640, 1), BackColor = Theme.Border });
            y += 10;
            SectionLabel(scroll, "STEP 3 — 額外贈金加成（選填・一般補單選「無加成」即可）", x, y); y += 22;
            scroll.Controls.Add(new Label
            {
                Text = "💡 贈金是活動獎勵，不計入累積儲值進度。VIP 玩家的加成已自動套用。",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = false, Size = new Size(600, 34), Location = new Point(x, y)
            });
            y += 36;
            _bonusBtns = new Button[BONUSES.Length];
            bx = x;
            for (int i = 0; i < BONUSES.Length; i++)
            {
                int pct = BONUSES[i];
                var btn = new Button
                {
                    Text = pct == 0 ? "無加成" : $"+{pct}%",
                    BackColor = Theme.BgInput, ForeColor = Theme.TextSecondary,
                    FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall,
                    Size = new Size(84, 28), Location = new Point(bx, y),
                    Cursor = Cursors.Hand, UseVisualStyleBackColor = false, Tag = pct
                };
                btn.FlatAppearance.BorderColor = Theme.Border;
                btn.Click += (s, e) => { _bonusPct = pct; RefreshBonusButtons(); UpdatePreview(); UpdateManualHints(); };
                scroll.Controls.Add(btn);
                _bonusBtns[i] = btn;
                bx += 88;
            }
            RefreshBonusButtons();
            y += 36;

            // ─── 或自訂金額（雙向同步）──────────────────────────────
            scroll.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(600, 1), BackColor = Theme.Border });
            y += 10;
            SectionLabel(scroll, "或自訂金額（輸入台幣 ↔ 自動換算金幣，雙向同步）", x, y); y += 22;

            // 第一行：台幣輸入
            scroll.Controls.Add(new Label
            {
                Text = "台幣 NT$", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y + 5)
            });
            _nudTwd = new NumericUpDown
            {
                Location = new Point(x + 76, y), Size = new Size(130, 26),
                Minimum = 0, Maximum = 9_999_999, Value = 0,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody, ThousandsSeparator = true
            };
            _nudTwd.ValueChanged += (s, e) =>
            {
                if (_syncingGold) return;
                SelectTier(-1);
                _syncingGold = true;
                var (bg, _, _) = TwdToGold((long)_nudTwd.Value);
                _nudGold.Value = _nudTwd.Value > 0 ? bg : 0;
                _syncingGold = false;
                UpdatePreview(); UpdateManualHints();
            };
            scroll.Controls.Add(_nudTwd);
            _lblTwdHint = new Label
            {
                Text = "← 輸入台幣，自動換算金幣",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = false, Size = new Size(370, 18),
                Location = new Point(x + 214, y + 5)
            };
            scroll.Controls.Add(_lblTwdHint);
            y += 32;

            // 第二行：金幣輸入（反推台幣）
            scroll.Controls.Add(new Label
            {
                Text = "金幣 元寶", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y + 5)
            });
            _nudGold = new NumericUpDown
            {
                Location = new Point(x + 76, y), Size = new Size(130, 26),
                Minimum = 0, Maximum = 999_999_999, Value = 0,
                BackColor = Theme.BgInput, ForeColor = Color.FromArgb(251, 191, 36),
                Font = Theme.FontBody, ThousandsSeparator = true
            };
            _nudGold.ValueChanged += (s, e) =>
            {
                if (_syncingGold) return;
                SelectTier(-1);
                _syncingGold = true;
                if (_nudGold.Value > 0)
                {
                    var opts = GoldToTwd((long)_nudGold.Value, _bonusPct);
                    _nudTwd.Value = opts.Count > 0 ? Math.Min(opts[0].Item1, _nudTwd.Maximum) : 0;
                }
                else _nudTwd.Value = 0;
                _syncingGold = false;
                UpdatePreview(); UpdateManualHints();
            };
            scroll.Controls.Add(_nudGold);
            _lblGoldHint = new Label
            {
                Text = "← 輸入金幣，自動反推最低台幣",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = false, Size = new Size(370, 18),
                Location = new Point(x + 214, y + 5)
            };
            scroll.Controls.Add(_lblGoldHint);
            y += 36;

            // ─── 是否發放金幣 ────────────────────────────────────────
            _chkGiveGold = new CheckBox
            {
                Text = "同時發放金幣（將金幣加入玩家帳戶）",
                Checked = true, Location = new Point(x, y),
                ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                AutoSize = true, Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
            };
            _chkGiveGold.CheckedChanged += (s, e) => { _giveGold = _chkGiveGold.Checked; UpdatePreview(); };
            scroll.Controls.Add(_chkGiveGold);
            y += 30;

            // ─── 預覽區 ──────────────────────────────────────────────
            _pnlPreview = new Panel
            {
                Location = new Point(x, y), Size = new Size(640, 130),
                BackColor = Color.FromArgb(18, 38, 22),
                BorderStyle = BorderStyle.FixedSingle, Visible = false
            };
            BuildPreviewPanel();
            scroll.Controls.Add(_pnlPreview);
            y += 138;

            // ─── 確認按鈕 ────────────────────────────────────────────
            _btnConfirm = Theme.MakeButton("💰 確認給予儲值", Theme.AccentGreen, Color.White, 260, 40);
            _btnConfirm.Location = new Point(x, y);
            _btnConfirm.Font = new Font(Theme.FontFamily, 11f, FontStyle.Bold);
            _btnConfirm.Enabled = false;
            _btnConfirm.Click += async (s, e) => await DoRechargeAsync();
            scroll.Controls.Add(_btnConfirm);
            y += 52;

            // ─── 充值記錄查詢 ────────────────────────────────────────
            scroll.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(640, 1), BackColor = Theme.Border });
            y += 10;
            SectionLabel(scroll, "💳 充值記錄（訂單查詢）", x, y); y += 22;

            _txtHistQ = new TextBox
            {
                Location = new Point(x, y), Width = 280, Height = 26,
                BackColor = Theme.BgInput, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "角色名稱、帳號或商品（空=全部）"
            };
            _txtHistQ.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoadHistoryAsync(); };
            scroll.Controls.Add(_txtHistQ);

            var btnHistSearch = Theme.MakeButton("🔍 查詢", Theme.AccentBlue, Color.White, 80, 26);
            btnHistSearch.Location = new Point(x + 286, y);
            btnHistSearch.Click += (s, e) => _ = LoadHistoryAsync();
            scroll.Controls.Add(btnHistSearch);

            var btnHistThis = Theme.MakeButton("查此玩家", Theme.BgLight, Theme.TextPrimary, 80, 26);
            btnHistThis.Location = new Point(x + 372, y);
            btnHistThis.Click += (s, e) =>
            {
                if (_account == null) return;
                _txtHistQ.Text = _account;
                _ = LoadHistoryAsync();
            };
            scroll.Controls.Add(btnHistThis);
            y += 32;

            _lblHistStatus = new Label
            {
                Text = "點「查詢」載入充值記錄",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, Location = new Point(x, y)
            };
            scroll.Controls.Add(_lblHistStatus);
            y += 22;

            _dgvHistory = new DataGridView { Location = new Point(x, y), Size = new Size(640, 200) };
            Theme.StyleDataGridView(_dgvHistory);
            _dgvHistory.RowTemplate.Height = 32;
            _dgvHistory.ReadOnly = true;
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTime",    HeaderText = "時間",      Width = 130 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAccount", HeaderText = "帳號",      Width = 120 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色",      Width = 100 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cProduct", HeaderText = "商品",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 100 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTwd",     HeaderText = "台幣",      Width = 75 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cGold",    HeaderText = "金幣",      Width = 90 });
            _dgvHistory.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",  HeaderText = "狀態",      Width = 70 });
            scroll.Controls.Add(_dgvHistory);
            y += 210;

            scroll.Controls.Add(new Panel { Location = new Point(x, y), Size = new Size(640, 20) }); // 底部空白
        }

        // ── 預覽 Panel 內部控件 ────────────────────────────────────
        private void BuildPreviewPanel()
        {
            _pnlPreview.Controls.Clear();
            _pnlPreview.Controls.Add(new Label
            {
                Text = "📋 確認預覽",
                ForeColor = Theme.AccentGreen, Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                AutoSize = true, Location = new Point(10, 6)
            });

            _lblPreviewTwd = new Label { Text = "", ForeColor = Theme.TextPrimary, Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 26) };
            _lblPreviewGold = new Label { Text = "", ForeColor = Theme.AccentOrange, Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 44) };
            _lblPreviewCycle = new Label { Text = "", ForeColor = Color.FromArgb(160, 210, 255), Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 62) };

            // 進度條（前）
            var lblBefore = new Label { Text = "前", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 82) };
            var barBgBefore = new Panel { Location = new Point(28, 83), Size = new Size(520, 8), BackColor = Theme.BgCard };
            _barBeforeFill = new Panel { Location = new Point(0, 0), Size = new Size(2, 8), BackColor = Theme.TextMuted };
            _lblBarBeforePct = new Label { Text = "0%", ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 7.5f), AutoSize = true, Location = new Point(554, 76) };
            barBgBefore.Controls.Add(_barBeforeFill);

            // 進度條（後）
            var lblAfter = new Label { Text = "後", ForeColor = Theme.AccentOrange, Font = Theme.FontSmall, AutoSize = true, Location = new Point(10, 98) };
            var barBgAfter = new Panel { Location = new Point(28, 99), Size = new Size(520, 8), BackColor = Theme.BgCard };
            _barAfterFill = new Panel { Location = new Point(0, 0), Size = new Size(2, 8), BackColor = Theme.AccentOrange };
            _lblBarAfterPct = new Label { Text = "0%", ForeColor = Theme.AccentOrange, Font = new Font(Theme.FontFamily, 7.5f), AutoSize = true, Location = new Point(554, 92) };
            barBgAfter.Controls.Add(_barAfterFill);

            _pnlPreview.Controls.AddRange(new Control[]
            {
                _lblPreviewTwd, _lblPreviewGold, _lblPreviewCycle,
                lblBefore, barBgBefore, _lblBarBeforePct,
                lblAfter, barBgAfter, _lblBarAfterPct
            });
        }

        private static void SectionLabel(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(100, 180, 255),
                Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize = true, Location = new Point(x, y)
            });
        }

        // ══════════════════════════════════════════════════════════════
        // 邏輯
        // ══════════════════════════════════════════════════════════════
        private async Task SearchPlayerAsync()
        {
            string q = _txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(q)) return;
            _btnSearch.Enabled = false; _btnSearch.Text = "搜尋中…";
            ShowMsg("搜尋中…", true);
            try
            {
                // 支援主帳號展開：若底下有多個角色，跳出選擇視窗
                var picked = await PlayerPickerHelper.PickAsync(this, q);
                if (picked == null) { ShowMsg("", true); return; }

                _account  = picked.Account;
                _masterId = picked.MasterId;
                _masterName = picked.MasterName ?? "";
                _txtSearch.Text = picked.OnlineName.Length > 0 ? picked.OnlineName : picked.Account;
                _detail   = await DatabaseManager.Instance.GetPlayerDetailAsync(picked.Account);
                // 預設依 VIP 套用加成
                _bonusPct = VipHelper.BonusPercent(_detail.PayTotal);
                RefreshBonusButtons();
                SelectTier(-1);
                RebuildPlayerInfo();
                UpdatePreview();

                // ── 載入子帳號（供分配儲值 Tab 使用）────────────────
                if (_masterId > 0)
                {
                    _subs = await DatabaseManager.Instance.GetSubAccountsAsync(_masterId);
                    // 若 MasterName 未從 picked 取得，從第一筆 sub 取
                    if (string.IsNullOrWhiteSpace(_masterName) && _subs.Count > 0)
                        _masterName = _subs[0].MasterName ?? "";
                }
                else
                {
                    _subs = new List<PlayerInfo>();
                }
                RebuildSplitPanel();

                string subsInfo = _subs.Count > 1 ? $"  ·  主帳號 {_masterName}（{_subs.Count} 位子帳號）" : "";
                ShowMsg($"✓ 已載入玩家：{_detail.OnlineName}（{_detail.Account}）{subsInfo}", true);
            }
            catch (Exception ex) { ShowMsg("找不到玩家：" + ex.Message, false); _detail = null; _account = null; RebuildPlayerInfo(); }
            finally { _btnSearch.Enabled = true; _btnSearch.Text = "🔍 搜尋"; }
        }

        private async Task RefreshDetailAsync()
        {
            if (_account == null) return;
            try
            {
                _detail = await DatabaseManager.Instance.GetPlayerDetailAsync(_account);
                RebuildPlayerInfo();
                UpdatePreview();
            }
            catch { }
        }

        private void SelectTier(int idx)
        {
            _selectedTierIdx = idx;
            foreach (var (btn, i) in _tierBtns.Select((b, i) => (b, i)))
            {
                bool sel = i == idx;
                btn.BackColor = sel ? Theme.AccentBlue : Theme.BgCard;
                btn.ForeColor = sel ? Color.White : Color.FromArgb(200, 215, 255);
                btn.FlatAppearance.BorderColor = sel ? Theme.AccentBlue : Theme.Border;
            }
            if (idx >= 0)
            {
                _nudTwd.Value  = Math.Min(TIERS[idx].Twd, _nudTwd.Maximum);
                _nudGold.Value = Math.Min(TIERS[idx].Gold, _nudGold.Maximum);
            }
        }

        private void RefreshBonusButtons()
        {
            foreach (var btn in _bonusBtns)
            {
                int pct = (int)btn.Tag;
                bool sel = pct == _bonusPct;
                btn.BackColor = sel ? (pct > 0 ? Color.FromArgb(20, 60, 25) : Theme.BgCard) : Theme.BgInput;
                btn.ForeColor = sel ? (pct > 0 ? Theme.AccentGreen : Theme.TextPrimary) : Theme.TextSecondary;
                btn.FlatAppearance.BorderColor = sel ? (pct > 0 ? Theme.AccentGreen : Color.FromArgb(80, 100, 140)) : Theme.Border;
            }
        }

        private void UpdateManualHints()
        {
            if (_lblTwdHint == null || _lblGoldHint == null) return;
            long twd  = (long)_nudTwd.Value;
            long goldB = (long)_nudGold.Value;

            // 台幣旁提示：顯示對應金幣
            if (twd > 0)
            {
                var (bg, rate, tierLbl) = TwdToGold(twd);
                long total = (long)Math.Round(bg * (1 + _bonusPct / 100.0));
                _lblTwdHint.Text = _bonusPct > 0
                    ? $"→ {bg:N0} 基礎，+{_bonusPct}% 後共 {total:N0} 元寶（{tierLbl} {rate:F1}x/NT$）"
                    : $"→ {bg:N0} 元寶（{tierLbl}，{rate:F1}x/NT$）";
                _lblTwdHint.ForeColor = Color.FromArgb(80, 220, 130);
            }
            else { _lblTwdHint.Text = "← 輸入台幣，自動換算金幣"; _lblTwdHint.ForeColor = Theme.TextMuted; }

            // 金幣旁提示：反推最低台幣
            if (goldB > 0)
            {
                var opts = GoldToTwd(goldB, _bonusPct);
                if (opts.Count > 0)
                {
                    var (minTwd, actual, tierLbl2, rate2) = opts[0];
                    _lblGoldHint.Text = _bonusPct > 0
                        ? $"→ 最少 NT${minTwd:N0}（{tierLbl2}，+{_bonusPct}% 後可得 {actual:N0} 金）"
                        : $"→ 最少 NT${minTwd:N0}（{tierLbl2}，{rate2:F1}x，可得 {actual:N0} 金）";
                    _lblGoldHint.ForeColor = Color.FromArgb(251, 191, 36);
                }
                else { _lblGoldHint.Text = "無法估算（金額過小）"; _lblGoldHint.ForeColor = Theme.AccentRed; }
            }
            else { _lblGoldHint.Text = "← 輸入金幣，自動反推最低台幣"; _lblGoldHint.ForeColor = Theme.TextMuted; }
        }

        private void UpdatePreview()
        {
            long twd  = GetFinalTwd();
            long gold = GetFinalGold();

            bool goldOk = !_giveGold || gold > 0;
            _btnConfirm.Enabled = _detail != null && twd > 0 && goldOk;
            _btnConfirm.Text    = _detail != null ? $"💰 確認給予 {_detail.OnlineName} 儲值" : "請先選擇玩家";
            if (_detail != null && twd > 0 && _giveGold && gold <= 0)
                _btnConfirm.Text = "⚠ 請輸入或選擇套餐以決定金幣數量";

            if (twd <= 0) { _pnlPreview.Visible = false; return; }

            long curPt  = _detail?.PayTotal ?? 0;
            long afterPt = curPt + twd;
            long curCyc  = curPt  > 0 ? (curPt  - 1) / CYCLE : 0;
            long aftCyc  = afterPt > 0 ? (afterPt - 1) / CYCLE : 0;
            long gained  = aftCyc - curCyc;
            long aftIn   = afterPt - aftCyc * CYCLE;
            int  bPct    = curPt > 0 ? (int)((curPt % CYCLE) * 100 / CYCLE) : 0;
            int  aPct    = afterPt > 0 ? (int)(aftIn * 100 / CYCLE) : 0;
            if (gained > 0) aPct = (int)(aftIn * 100 / CYCLE);

            _lblPreviewTwd.Text   = $"台幣金額：NT${twd:N0}（累積進度 +NT${twd:N0}，優惠贈金不納入）";
            _lblPreviewGold.Text  = _giveGold ? $"金幣入帳：+{gold:N0} 元寶" + (_bonusPct > 0 ? $"（含 +{_bonusPct}% 優惠）" : "") : "本次不發放金幣（僅更新累儲進度）";
            string cycleNote = gained > 0
                ? $"循環：{curPt:N0} → {aftIn:N0}/25,000　（完成 {gained} 輪！）"
                : $"循環：{curPt:N0} → {afterPt:N0}/25,000";
            _lblPreviewCycle.Text = cycleNote;

            _barBeforeFill.Width   = Math.Max(2, (int)(520 * bPct / 100.0));
            _barAfterFill.Width    = Math.Max(2, (int)(520 * aPct / 100.0));
            _lblBarBeforePct.Text  = $"{bPct}%";
            _lblBarAfterPct.Text   = $"{aPct}%";
            _pnlPreview.Visible    = true;
        }

        private long GetFinalTwd()
        {
            if (_selectedTierIdx >= 0) return TIERS[_selectedTierIdx].Twd;
            return (long)_nudTwd.Value;
        }

        private long GetFinalGold()
        {
            // 統一邏輯：先取基礎金幣，再套加成
            long baseGold = _selectedTierIdx >= 0
                ? TIERS[_selectedTierIdx].Gold
                : (long)_nudGold.Value;   // _nudGold 存的是基礎金幣（無加成）
            // fallback：若 nudGold 為 0 且有手動台幣，用 TwdToGold 計算
            if (baseGold <= 0 && _selectedTierIdx < 0)
            {
                long twd = (long)_nudTwd.Value;
                if (twd > 0)
                {
                    var (bg, _, _) = TwdToGold(twd);
                    baseGold = bg;
                }
            }
            return (long)Math.Round(baseGold * (1 + _bonusPct / 100.0));
        }

        private async Task DoRechargeAsync()
        {
            if (_detail == null) { ShowMsg("請先搜尋並選定玩家", false); return; }
            long twd  = GetFinalTwd();
            long gold = GetFinalGold();
            if (twd <= 0) { ShowMsg("請選擇套餐或輸入台幣金額", false); return; }
            if (_giveGold && gold <= 0) { ShowMsg("請輸入金幣數量", false); return; }

            long baseGold  = _selectedTierIdx >= 0 ? TIERS[_selectedTierIdx].Gold : (long)_nudGold.Value;
            string goldLine = _giveGold
                ? (_bonusPct > 0
                    ? $"  金幣入帳：+{baseGold:N0}（套餐）＋ +{gold - baseGold:N0}（+{_bonusPct}%）＝ 共 {gold:N0} 元寶"
                    : $"  金幣入帳：+{gold:N0} 元寶")
                : "  本次不發放金幣（僅更新累積儲值進度）";

            if (MessageBox.Show(
                $"確認給予以下儲值？\n\n" +
                $"  玩家：{_detail.OnlineName}（{_detail.Account}）\n" +
                $"  台幣金額：NT${twd:N0}（累積進度 +NT${twd:N0}，優惠贈金不納入）\n" +
                goldLine,
                "確認給予儲值", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _btnConfirm.Enabled = false; _btnConfirm.Text = "處理中…";
            try
            {
                bool ok = await DatabaseManager.Instance.AdjustPayDataPointAsync(
                    _detail.Account, twd, _giveGold ? gold : 0, _giveGold);
                if (ok)
                {
                    ShowMsg($"✅ 已給予 {_detail.OnlineName} 儲值 NT${twd:N0}" + (_giveGold ? $"，金幣 +{gold:N0}" : "（僅累儲）"), true);
                    SelectTier(-1); _nudTwd.Value = 0; _nudGold.Value = 0; _pnlPreview.Visible = false;
                    await RefreshDetailAsync();
                }
                else ShowMsg("修改失敗，請確認資料庫連線", false);
            }
            catch (Exception ex) { ShowMsg("❌ 發生錯誤：" + ex.Message, false); }
            finally { UpdatePreview(); }
        }

        private async Task LoadHistoryAsync()
        {
            _lblHistStatus.Text = "查詢中…";
            _dgvHistory.Rows.Clear();
            try
            {
                var rows = await DatabaseManager.Instance.GetRechargeOrdersAsync(_txtHistQ.Text.Trim());
                foreach (var r in rows)
                {
                    _dgvHistory.Rows.Add(
                        r.CreatedAt,
                        r.RoleName,
                        r.CharName,
                        r.ProductName,
                        r.Amount > 0 ? r.TwdText : "—",
                        r.Amount > 0 ? r.YuanbaoText : "—",
                        r.StatusText);
                }
                _lblHistStatus.Text = rows.Count > 0 ? $"共 {rows.Count} 筆" : "無資料";
            }
            catch (Exception ex) { _lblHistStatus.Text = "查詢失敗：" + ex.Message; }
        }

        private void ShowMsg(string text, bool ok)
        {
            _lblMsg.Text      = text;
            _lblMsg.ForeColor = ok ? Theme.AccentGreen : Theme.AccentRed;
        }

        private static (long baseGold, double rate, string tierLabel) TwdToGold(long twd)
        {
            if (twd <= 0) return (0, 100, "—");
            var best = TIERS[0];
            foreach (var t in TIERS) if (twd >= t.Twd) best = t;
            double rate = (double)best.Gold / best.Twd;
            return ((long)Math.Floor(twd * rate), rate, best.Label);
        }

        /// <summary>
        /// 金幣反推台幣：輸入目標金幣量（含加成後的實際金幣），
        /// 找出各套餐方案中所需的最少台幣金額。
        /// </summary>
        private static List<(long twd, long actualGold, string tierLabel, double rate)> GoldToTwd(long targetGold, int bonusPct)
        {
            var results = new List<(long, long, string, double)>();
            if (targetGold <= 0) return results;

            // 若有加成，實際需要的「基礎金幣」= ceil(targetGold / (1 + bonus%))
            double divisor = 1 + bonusPct / 100.0;

            foreach (var t in TIERS)
            {
                double tierRate = (double)t.Gold / t.Twd;
                // 在此套餐匯率下，基礎金幣需要 baseNeeded 個：baseNeeded = ceil(targetGold / divisor / ... )
                // 實際：baseGold = floor(NT$ * tierRate)，加成後 = floor(baseGold * divisor)
                // 反推：NT$ = ceil(targetGold / divisor / tierRate)
                long needed = (long)Math.Ceiling(targetGold / divisor / tierRate);
                needed = Math.Max(needed, t.Twd); // 至少達到此套餐門檻

                // 用 TwdToGold 確認 needed 台幣時實際適用的匯率
                var (realBase, realRate, realLabel) = TwdToGold(needed);
                long realTotal = (long)Math.Floor(realBase * divisor);

                if (realTotal >= targetGold)
                    results.Add((needed, realTotal, realLabel, realRate));
            }

            // 若全部套餐都找不到（極小金幣量），用基礎匯率 100金/NT$
            if (results.Count == 0)
            {
                long needed = (long)Math.Ceiling(targetGold / divisor / 100.0);
                results.Add((needed, (long)Math.Floor(needed * 100 * divisor), "基礎", 100));
            }

            // 去重並排序（最少台幣的排最前）
            return results
                .GroupBy(r => r.Item1)
                .Select(g => g.First())
                .OrderBy(r => r.Item1)
                .ToList();
        }

    }
}
