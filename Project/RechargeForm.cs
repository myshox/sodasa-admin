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
        private Panel     _pnlSplitWrapper;    // Tab 2：分配儲值（直接建構）

        // 分配儲值狀態（直接建在 Panel，無嵌入物件）
        private List<SplitRowCtrl>? _splitRows;
        private Label?  _lblSplitTotal;
        private Button? _btnSplitOk;
        private Panel?  _splitScrollPanel;
        private Label?  _hdrSplitGold;

        private sealed class SplitRowCtrl
        {
            public PlayerInfo    Player  = null!;
            public bool          Enabled;
            public int           TierIdx = -1;
            public long          CustomTwd;
            public int           BonusIdx = 0;
            public Panel         Row     = null!;
            public CheckBox      Chk     = null!;
            public Button[]      TierBtns  = new Button[7];
            public Button[]      BonusBtns = new Button[5];
            public NumericUpDown Nud     = null!;
            public Label         Preview = null!;

            private static readonly (long Twd, long Gold)[] T =
            {
                (100,10_000),(300,32_000),(500,55_000),(1_000,115_000),
                (3_000,360_000),(5_000,625_000),(10_000,1_300_000)
            };
            private static readonly int[] B = { 0, 5, 10, 15, 20 };
            public int  BonusPct  => B[BonusIdx];
            public long EffTwd    => TierIdx >= 0 ? T[TierIdx].Twd : CustomTwd;
            public long BaseGold
            {
                get {
                    if (TierIdx >= 0) return T[TierIdx].Gold;
                    if (CustomTwd <= 0) return 0;
                    var best = T[0];
                    foreach (var t in T) if (CustomTwd >= t.Twd) best = t;
                    return (long)Math.Floor(CustomTwd * ((double)best.Gold / best.Twd));
                }
            }
            public long TotalGold => (long)Math.Round(BaseGold * (1 + BonusPct / 100.0));
        }

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
            Size            = new Size(1160, 780);
            MinimumSize     = new Size(1020, 640);
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

            // ── Tab 切換按鈕（放在搜尋列右側，永遠可見）────────────
            var tabSep = new Panel
            {
                Size = new Size(1, 28), Location = new Point(604, 14),
                BackColor = Theme.Border
            };
            topBar.Controls.Add(tabSep);

            _btnTabSingle = new Button
            {
                Text = "💰 新增儲值",
                Size = new Size(130, 28), Location = new Point(610, 14),
                BackColor = Theme.AccentBlue, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand, UseVisualStyleBackColor = false
            };
            _btnTabSingle.FlatAppearance.BorderSize = 0;
            _btnTabSingle.Click += (_, __) => SwitchTab(false);
            topBar.Controls.Add(_btnTabSingle);

            _btnTabSplit = new Button
            {
                Text = "📋 分配儲值",
                Size = new Size(130, 28), Location = new Point(744, 14),
                BackColor = Color.FromArgb(30, 38, 60), ForeColor = Color.FromArgb(120, 140, 180),
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Theme.FontFamily, 8.5f),
                Cursor = Cursors.Hand, Enabled = false, UseVisualStyleBackColor = false
            };
            _btnTabSplit.FlatAppearance.BorderSize  = 1;
            _btnTabSplit.FlatAppearance.BorderColor = Theme.Border;
            _btnTabSplit.Click += (_, __) => SwitchTab(true);
            new ToolTip().SetToolTip(_btnTabSplit, "搜尋到主帳號後，此 Tab 才會啟用");
            topBar.Controls.Add(_btnTabSplit);

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
            // 首次 Resize 時才有真實寬度，設完後立即取消事件
            void InitSplitter(object? s, EventArgs e)
            {
                if (split.Width < 400) return;
                split.Resize -= InitSplitter;
                try
                {
                    split.Panel1MinSize = 240;
                    if (split.Width > 240 + 480 + split.SplitterWidth)
                        split.Panel2MinSize = 480;
                    int d = Math.Max(270, Math.Min(split.Width - 500, (int)(split.Width * 0.27)));
                    split.SplitterDistance = Math.Max(split.Panel1MinSize, d);
                }
                catch { }
            }
            split.Resize += InitSplitter;

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

        // ── 右側：新增儲值 | 分配儲值（Tab 按鈕已移至頂部搜尋列）────
        private void BuildRightPanel(SplitterPanel panel)
        {
            panel.BackColor = Theme.BgPage;

            // 分配儲值 Panel（初始隱藏）
            _pnlSplitWrapper = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Theme.BgPage, Visible = false
            };
            panel.Controls.Add(_pnlSplitWrapper);

            // 新增儲值 Panel（初始顯示）
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

            _btnTabSingle.BackColor = !showSplit ? Theme.AccentBlue : Color.FromArgb(28, 36, 56);
            _btnTabSingle.ForeColor = !showSplit ? Color.White : Color.FromArgb(140, 160, 200);
            _btnTabSingle.Font      = new Font(Theme.FontFamily, 9.5f, !showSplit ? FontStyle.Bold : FontStyle.Regular);

            _btnTabSplit.BackColor = showSplit ? Color.FromArgb(20, 100, 30) : Color.FromArgb(28, 36, 56);
            _btnTabSplit.ForeColor = showSplit ? Color.FromArgb(140, 230, 140) : Color.FromArgb(140, 160, 200);
            _btnTabSplit.Font      = new Font(Theme.FontFamily, 9.5f, showSplit ? FontStyle.Bold : FontStyle.Regular);
        }

        // ════════════════════════════════════════════════════════════
        // 分配儲值 Tab — 直接建構在 _pnlSplitWrapper（無任何嵌入物件）
        // ════════════════════════════════════════════════════════════
        private static readonly Color _cBg        = Color.FromArgb(14, 18, 30);
        private static readonly Color _cBgRowOff  = Color.FromArgb(16, 21, 36);
        private static readonly Color _cBgRowOn   = Color.FromArgb(12, 26, 14);
        private static readonly Color _cBtnTier   = Color.FromArgb(28, 36, 58);
        private static readonly Color _cBtnSel    = Color.FromArgb(180, 100, 0);
        private static readonly Color _cBtnBonus  = Color.FromArgb(20, 60, 120);
        private static readonly Color _cBtnBonSel = Color.FromArgb(30, 100, 200);
        private static readonly Color _cBorder2   = Color.FromArgb(35, 45, 70);
        private static readonly Color _cOrange    = Color.FromArgb(255, 195, 50);
        private static readonly Color _cGreen2    = Color.FromArgb(86, 196, 118);
        private const int SPLIT_ROW_H = 66;
        private static readonly string[] SPLIT_TIER_LABELS = { "NT$100","NT$300","NT$500","NT$1K","NT$3K","NT$5K","NT$10K" };
        private static readonly string[] SPLIT_TIER_SUBS   = { "1萬","3.2萬","5.5萬","11.5萬","36萬","62.5萬","130萬" };
        private static readonly int[]    SPLIT_BONUS_VALS  = { 0, 5, 10, 15, 20 };

        private void RebuildSplitPanel()
        {
            _pnlSplitWrapper.Controls.Clear();
            _splitRows = null;

            if (_subs.Count == 0)
            {
                _btnTabSplit.Enabled = false;
                _btnTabSplit.Text    = "📋 分配儲值";
                new ToolTip().SetToolTip(_btnTabSplit, "搜尋到主帳號後，此 Tab 才會啟用");
                SwitchTab(false);
                return;
            }

            // ── 底部確認列 ─────────────────────────────────────────
            var btnBar = new Panel
            {
                Dock = DockStyle.Bottom, Height = 50,
                BackColor = Color.FromArgb(10, 14, 24)
            };
            btnBar.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = _cBorder2 });
            _lblSplitTotal = new Label
            {
                Text = "請勾選帳號後選擇套餐",
                Dock = DockStyle.Fill, ForeColor = _cOrange,
                Font = Theme.FontBody, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            _btnSplitOk = new Button
            {
                Text = "💰 確認分配儲值", Size = new Size(156, 34), Dock = DockStyle.Right,
                BackColor = Color.FromArgb(50, 55, 70), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Cursor = Cursors.Hand, Enabled = false, Margin = new Padding(0, 8, 10, 8)
            };
            _btnSplitOk.FlatAppearance.BorderColor = _cBorder2;
            _btnSplitOk.Click += async (_, __) => await DoSplitRechargeAsync();
            btnBar.Controls.Add(_lblSplitTotal);
            btnBar.Controls.Add(_btnSplitOk);
            _pnlSplitWrapper.Controls.Add(btnBar);

            // ── 快速套用列 ──────────────────────────────────────────
            var quickBar = new Panel
            {
                Dock = DockStyle.Top, Height = 40,
                BackColor = Color.FromArgb(14, 20, 36)
            };
            quickBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = _cBorder2 });
            var qFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, BackColor = Color.Transparent,
                Padding = new Padding(8, 6, 0, 0), AutoScroll = false
            };
            qFlow.Controls.Add(new Label { Text = "批次套用：", AutoSize = true,
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, BackColor = Color.Transparent,
                Margin = new Padding(0, 4, 4, 0) });
            for (int i = 0; i < SPLIT_TIER_LABELS.Length; i++)
            {
                int ii = i;
                var qb = MakeSplitQuickBtn(SPLIT_TIER_LABELS[i]);
                qb.Click += (_, __) => { foreach (var r in _splitRows!) if (r.Enabled) SetSplitTier(r, ii); RefreshSplitAll(); };
                qFlow.Controls.Add(qb);
            }
            qFlow.Controls.Add(new Label { Text = "  優惠：", AutoSize = true,
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall, BackColor = Color.Transparent,
                Margin = new Padding(6, 4, 4, 0) });
            for (int i = 0; i < SPLIT_BONUS_VALS.Length; i++)
            {
                int ii = i;
                var qb = MakeSplitQuickBtn($"+{SPLIT_BONUS_VALS[i]}%");
                qb.Click += (_, __) => { foreach (var r in _splitRows!) if (r.Enabled) SetSplitBonus(r, ii); RefreshSplitAll(); };
                qFlow.Controls.Add(qb);
            }
            var qClear = MakeSplitQuickBtn("✕ 清除");
            qClear.BackColor = Color.FromArgb(70, 22, 22);
            qClear.ForeColor = Color.FromArgb(245, 100, 100);
            qClear.Margin = new Padding(8, 3, 0, 3);
            qClear.Click += (_, __) =>
            {
                foreach (var r in _splitRows!) if (r.Enabled)
                { SetSplitTier(r, -1); r.Nud.Value = 0; SetSplitBonus(r, 0); }
                RefreshSplitAll();
            };
            qFlow.Controls.Add(qClear);
            quickBar.Controls.Add(qFlow);
            _pnlSplitWrapper.Controls.Add(quickBar);

            // ── 表頭 ────────────────────────────────────────────────
            var hdr = new Panel
            {
                Dock = DockStyle.Top, Height = 28,
                BackColor = Color.FromArgb(12, 16, 28)
            };
            hdr.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = _cBorder2 });
            void AddHdr(string t, int x, int w) =>
                hdr.Controls.Add(new Label {
                    Text = t, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = false, Location = new Point(x, 0), Size = new Size(w, 28),
                    TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent });
            AddHdr("✓",          10, 24);
            AddHdr("帳號 / 角色", 52, 140);
            AddHdr("套餐（點選即勾選）", 196, 330);
            AddHdr("自訂NT$",    530,  80);
            AddHdr("優惠%",      616, 158);
            _hdrSplitGold = new Label
            {
                Text = "金幣預覽", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = false, TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent
            };
            hdr.Controls.Add(_hdrSplitGold);
            _pnlSplitWrapper.Controls.Add(hdr);

            // ── 子帳號滾動區域 ──────────────────────────────────────
            _splitScrollPanel = new Panel
            {
                Dock = DockStyle.Fill, AutoScroll = true, BackColor = _cBg
            };
            _splitRows = new List<SplitRowCtrl>();
            int y = 0;
            foreach (var sub in _subs)
            {
                var rc = BuildSplitRow(sub, y);
                _splitScrollPanel.Controls.Add(rc.Row);
                _splitRows.Add(rc);
                y += SPLIT_ROW_H;
            }
            // 監聽寬度變化，更新每列寬度
            _splitScrollPanel.Resize += (_, __) => UpdateSplitRowWidths();
            _pnlSplitWrapper.Controls.Add(_splitScrollPanel);

            _btnTabSplit.Enabled = true;
            _btnTabSplit.Text    = $"📋 分配儲值（{_subs.Count} 位）";
            new ToolTip().SetToolTip(_btnTabSplit, $"{_masterName} 旗下 {_subs.Count} 個帳號");

            RefreshSplitAll();
        }

        private SplitRowCtrl BuildSplitRow(PlayerInfo p, int yPos)
        {
            var sr = new SplitRowCtrl { Player = p };
            sr.Row = new Panel
            {
                Location  = new Point(0, yPos),
                Height    = SPLIT_ROW_H,
                BackColor = p.IsOnline ? _cBgRowOn : _cBgRowOff,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right
            };
            sr.Row.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = _cBorder2 });

            // 勾選框
            sr.Chk = new CheckBox { Location = new Point(10, 24), Size = new Size(18, 18), BackColor = Color.Transparent };
            sr.Chk.CheckedChanged += (_, __) =>
            {
                sr.Enabled = sr.Chk.Checked;
                sr.Row.BackColor = sr.Enabled
                    ? (p.IsOnline ? Color.FromArgb(22, 34, 18) : Color.FromArgb(20, 24, 42))
                    : (p.IsOnline ? _cBgRowOn : _cBgRowOff);
                RefreshSplitTotal();
            };
            sr.Row.Controls.Add(sr.Chk);

            // 玩家資訊（左側固定區）
            string nm = !string.IsNullOrWhiteSpace(p.OnlineName) ? p.OnlineName : p.Account;
            sr.Row.Controls.Add(new Label { Text = p.IsOnline ? "🟢" : (p.IsBanned ? "🔴" : "⚫"),
                Location = new Point(32, 23), AutoSize = true,
                Font = new Font(Theme.FontFamily, 9f), BackColor = Color.Transparent });
            sr.Row.Controls.Add(new Label { Text = nm, Location = new Point(52, 6), Size = new Size(140, 20),
                ForeColor = Theme.TextPrimary, Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                BackColor = Color.Transparent, AutoEllipsis = true });
            sr.Row.Controls.Add(new Label { Text = p.Account, Location = new Point(52, 26), Size = new Size(140, 16),
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 7.5f),
                BackColor = Color.Transparent, AutoEllipsis = true });
            if (p.PayTotal > 0)
                sr.Row.Controls.Add(new Label { Text = $"NT${p.PayTotal:N0}", Location = new Point(52, 44), Size = new Size(140, 14),
                    ForeColor = _cOrange, Font = new Font(Theme.FontFamily, 7f), BackColor = Color.Transparent });

            // 套餐按鈕（7 個）
            for (int i = 0; i < SPLIT_TIER_LABELS.Length; i++)
            {
                int idx = i;
                var tb = new Button
                {
                    Text = SPLIT_TIER_LABELS[i], AutoSize = false,
                    Size = new Size(44, 22), Location = new Point(196 + idx * 47, 8),
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                    BackColor = _cBtnTier, ForeColor = Theme.TextPrimary,
                    Font = new Font(Theme.FontFamily, 7.5f)
                };
                tb.FlatAppearance.BorderColor = Color.FromArgb(55, 70, 110);
                tb.Click += (_, __) => { SetSplitTier(sr, idx); RefreshSplitAll(); };
                sr.TierBtns[i] = tb;
                sr.Row.Controls.Add(tb);
            }
            // 套餐小字說明
            sr.Row.Controls.Add(new Label
            {
                Text = string.Join("  ", SPLIT_TIER_SUBS),
                Location = new Point(196, 32), Size = new Size(330, 14),
                ForeColor = Color.FromArgb(80, 95, 115), Font = new Font(Theme.FontFamily, 6.5f),
                BackColor = Color.Transparent
            });

            // 自訂 NT$
            sr.Nud = new NumericUpDown
            {
                Location = new Point(530, 20), Size = new Size(78, 24),
                Minimum = 0, Maximum = 999_999, Value = 0,
                BackColor = Color.FromArgb(22, 28, 46), ForeColor = Theme.TextPrimary,
                Font = new Font(Theme.FontFamily, 8f), BorderStyle = BorderStyle.FixedSingle
            };
            sr.Nud.ValueChanged += (_, __) =>
            {
                sr.CustomTwd = (long)sr.Nud.Value;
                if (sr.CustomTwd > 0) SetSplitTier(sr, -1);
                if (!sr.Enabled && sr.CustomTwd > 0) sr.Chk.Checked = true;
                RefreshSplitPreview(sr); RefreshSplitTotal();
            };
            sr.Row.Controls.Add(sr.Nud);

            // 優惠按鈕（5 個）
            for (int i = 0; i < SPLIT_BONUS_VALS.Length; i++)
            {
                int bi = i;
                var bb = new Button
                {
                    Text = $"+{SPLIT_BONUS_VALS[i]}%", AutoSize = false,
                    Size = new Size(30, 22), Location = new Point(616 + bi * 32, 20),
                    FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                    BackColor = bi == 0 ? _cBtnBonSel : _cBtnBonus,
                    ForeColor = bi == 0 ? Color.White : Theme.TextPrimary,
                    Font = new Font(Theme.FontFamily, 7f)
                };
                bb.FlatAppearance.BorderColor = Color.FromArgb(40, 80, 150);
                bb.Click += (_, __) => { SetSplitBonus(sr, bi); RefreshSplitAll(); };
                sr.BonusBtns[i] = bb;
                sr.Row.Controls.Add(bb);
            }
            // Last bonus button ends at: 616 + 4*32 + 30 = 774px

            // 金幣預覽（右對齊，動態）
            sr.Preview = new Label
            {
                ForeColor = _cGreen2, Font = new Font(Theme.FontFamily, 8f),
                BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight,
                Text = "—"
            };
            sr.Row.Controls.Add(sr.Preview);

            return sr;
        }

        private void UpdateSplitRowWidths()
        {
            if (_splitRows == null || _splitScrollPanel == null) return;
            int w = Math.Max(800, _splitScrollPanel.ClientSize.Width);
            foreach (var r in _splitRows)
            {
                r.Row.Width = w;
                r.Preview.Location = new Point(w - 170, 10);
                r.Preview.Size     = new Size(162, 44);
            }
            if (_hdrSplitGold != null)
            {
                _hdrSplitGold.Location = new Point(w - 170, 0);
                _hdrSplitGold.Size     = new Size(162, 28);
            }
        }

        private void SetSplitTier(SplitRowCtrl sr, int idx)
        {
            sr.TierIdx = idx;
            for (int i = 0; i < sr.TierBtns.Length; i++)
            {
                bool sel = i == idx;
                sr.TierBtns[i].BackColor = sel ? _cBtnSel : _cBtnTier;
                sr.TierBtns[i].ForeColor = sel ? Color.White : Theme.TextPrimary;
                sr.TierBtns[i].FlatAppearance.BorderColor = sel ? Color.FromArgb(220, 130, 20) : Color.FromArgb(55, 70, 110);
                sr.TierBtns[i].Font = new Font(Theme.FontFamily, sel ? 7.5f : 7f, sel ? FontStyle.Bold : FontStyle.Regular);
            }
            if (idx >= 0 && sr.Nud.Value > 0) sr.Nud.Value = 0;
            if (idx >= 0 && !sr.Enabled) sr.Chk.Checked = true;
            RefreshSplitPreview(sr);
        }

        private void SetSplitBonus(SplitRowCtrl sr, int idx)
        {
            sr.BonusIdx = idx;
            for (int i = 0; i < sr.BonusBtns.Length; i++)
            {
                bool sel = i == idx;
                sr.BonusBtns[i].BackColor = sel ? _cBtnBonSel : _cBtnBonus;
                sr.BonusBtns[i].ForeColor = sel ? Color.White : Theme.TextPrimary;
            }
            RefreshSplitPreview(sr);
        }

        private void RefreshSplitPreview(SplitRowCtrl sr)
        {
            long twd = sr.EffTwd;
            if (twd <= 0) { sr.Preview.Text = "—"; sr.Preview.ForeColor = Theme.TextMuted; return; }
            long tg = sr.TotalGold;
            string bonus = sr.BonusPct > 0 ? $"\n+{sr.BonusPct}%" : "";
            sr.Preview.Text = $"NT${twd:N0}\n→ {tg:N0} 金{bonus}";
            sr.Preview.ForeColor = _cGreen2;
        }

        private void RefreshSplitTotal()
        {
            if (_splitRows == null) return;
            long totTwd = 0, totGold = 0; int cnt = 0;
            foreach (var r in _splitRows)
            {
                if (!r.Enabled || r.EffTwd <= 0) continue;
                totTwd += r.EffTwd; totGold += r.TotalGold; cnt++;
            }
            if (_lblSplitTotal != null)
                _lblSplitTotal.Text = cnt > 0
                    ? $"合計：NT$ {totTwd:N0}，發出 {totGold:N0} 金，{cnt} 個帳號"
                    : "請勾選帳號後選擇套餐";
            if (_btnSplitOk != null)
            {
                _btnSplitOk.Enabled  = cnt > 0;
                _btnSplitOk.BackColor = cnt > 0 ? _cBtnSel : Color.FromArgb(50, 55, 70);
            }
        }

        private void RefreshSplitAll()
        {
            if (_splitRows == null) return;
            foreach (var r in _splitRows) RefreshSplitPreview(r);
            RefreshSplitTotal();
        }

        private async Task DoSplitRechargeAsync()
        {
            if (_splitRows == null) return;
            var items = _splitRows.Where(r => r.Enabled && r.EffTwd > 0).ToList();
            if (items.Count == 0) { MessageBox.Show("請勾選至少一個有效帳號", "提示"); return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"確認為【{_masterName}】旗下帳號分配儲值？\n");
            foreach (var r in items)
            {
                string nm = !string.IsNullOrWhiteSpace(r.Player.OnlineName) ? r.Player.OnlineName : r.Player.Account;
                sb.AppendLine($"• {nm}（{r.Player.Account}）");
                sb.AppendLine($"  NT${r.EffTwd:N0}  →  {r.TotalGold:N0} 金" +
                              (r.BonusPct > 0 ? $"（+{r.BonusPct}%）" : ""));
            }
            sb.AppendLine("\n⚠ 累積儲值只計台幣，贈金不納入");
            if (MessageBox.Show(sb.ToString(), "確認分配儲值",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            _btnSplitOk!.Enabled = false; _btnSplitOk.Text = "處理中…";
            int done = 0; var fails = new List<string>();
            foreach (var r in items)
            {
                try
                {
                    bool ok = await DatabaseManager.Instance.AdjustPayDataPointAsync(
                        r.Player.Account, r.EffTwd, r.TotalGold, giveGold: true);
                    if (ok) done++;
                    else fails.Add(r.Player.Account + "（失敗）");
                }
                catch (Exception ex) { fails.Add(r.Player.Account + ": " + ex.Message); }
            }
            string msg = $"✓ 完成 {done}/{items.Count} 個帳號";
            if (fails.Count > 0) msg += $"\n\n失敗：\n{string.Join("\n", fails)}";
            MessageBox.Show(msg, "分配儲值結果", MessageBoxButtons.OK,
                fails.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            _btnSplitOk.Text = "💰 確認分配儲值"; _btnSplitOk.Enabled = true;
            if (done > 0)
            {
                if (_masterId > 0)
                    _subs = await DatabaseManager.Instance.GetSubAccountsAsync(_masterId);
                RebuildSplitPanel();
                SwitchTab(true);
            }
        }

        private static Button MakeSplitQuickBtn(string text) => new Button
        {
            Text = text, AutoSize = false, Size = new Size(54, 26),
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            BackColor = Color.FromArgb(28, 36, 58), ForeColor = Theme.TextPrimary,
            Font = new Font(Theme.FontFamily, 7.5f),
            Margin = new Padding(2, 3, 2, 3)
        };

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
                // 多選模式：主帳號展開後可勾選多個角色
                var pickedList = await PlayerPickerHelper.PickMultiAsync(this, q, multiMode: true);
                if (pickedList == null || pickedList.Count == 0) { ShowMsg("", true); return; }

                var first = pickedList[0];
                _account    = first.Account;
                _masterId   = first.MasterId;
                _masterName = first.MasterName ?? "";
                _txtSearch.Text = first.OnlineName.Length > 0 ? first.OnlineName : first.Account;
                _detail = await DatabaseManager.Instance.GetPlayerDetailAsync(first.Account);
                _bonusPct = VipHelper.BonusPercent(_detail.PayTotal);
                RefreshBonusButtons();
                SelectTier(-1);
                RebuildPlayerInfo();
                UpdatePreview();

                if (pickedList.Count > 1)
                {
                    // 多選 → 直接進分配儲值 Tab，只顯示選定的角色
                    _subs = pickedList;
                    RebuildSplitPanel();
                    SwitchTab(true);
                    ShowMsg($"✓ 已選取 {pickedList.Count} 個角色 → 切換到分配儲值", true);
                }
                else
                {
                    // 單選 → 一般模式，同時載入主帳號所有子帳號供分配儲值 Tab
                    if (_masterId > 0)
                    {
                        _subs = await DatabaseManager.Instance.GetSubAccountsAsync(_masterId);
                        if (string.IsNullOrWhiteSpace(_masterName) && _subs.Count > 0)
                            _masterName = _subs[0].MasterName ?? "";
                    }
                    else
                    {
                        _subs = new List<PlayerInfo>();
                    }
                    RebuildSplitPanel();
                    string subsInfo = _subs.Count > 1 ? $"  ·  主帳號 {_masterName}（{_subs.Count} 位子帳號，可切換分配儲值 Tab）" : "";
                    ShowMsg($"✓ 已載入玩家：{_detail.OnlineName}（{_detail.Account}）{subsInfo}", true);
                }
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

            // 大額（> NT$10,000）需二次確認
            if (twd > 10_000)
            {
                var bigConfirm = MessageBox.Show(
                    $"⚠ 本次充值金額為 NT${twd:N0}，超過 NT$10,000，\n請確認金額無誤。\n\n繼續嗎？",
                    "⚠ 大額充值警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (bigConfirm != DialogResult.Yes) return;
            }

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
