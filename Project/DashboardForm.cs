using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class DashboardForm : Form
    {
        private BentoStatCard _cardOnline, _cardTotal, _cardNew, _cardActive, _cardMails, _cardUnread;
        private BentoStatCard _cardTodayRev, _cardTotalRev, _cardTodayOrders;
        private Label         _statusLbl;
        private Button        _btnRefresh;
        private DataGridView  _logDgv;
        private DataGridView  _rankDgv;

        // ── Bento 設計 token ─────────────────────────────────────────
        private static readonly Color BgPage   = Color.FromArgb( 8,  9, 18);   // 最暗底色
        private static readonly Color BgCard   = Color.FromArgb(15, 19, 34);   // 卡片背景
        private static readonly Color BgCardLo = Color.FromArgb(12, 16, 28);   // 日誌卡背景
        private const int GAP = 12;   // 卡片間距

        // ── 八種 Accent 顏色 ─────────────────────────────────────────
        private static readonly Color AOnline    = Color.FromArgb( 22, 183, 120);
        private static readonly Color ATotal     = Color.FromArgb( 59, 130, 246);
        private static readonly Color ANew       = Color.FromArgb(251, 146,  60);
        private static readonly Color AActive    = Color.FromArgb(139,  92, 246);
        private static readonly Color AMails     = Color.FromArgb( 20, 184, 166);
        private static readonly Color AUnread    = Color.FromArgb(236,  72, 153);
        private static readonly Color ATodayRev  = Color.FromArgb(250, 204,  21);
        private static readonly Color ATotalRev  = Color.FromArgb(234, 179,   8);
        private static readonly Color AOrders    = Color.FromArgb( 74, 222, 128);

        private Action _logHandler;

        public DashboardForm()
        {
            Theme.ApplyHubForm(this);
            InitUI();
            _ = RefreshAsync();

            _logHandler = () =>
            {
                if (IsDisposed) return;
                if (InvokeRequired) { try { BeginInvoke(new Action(RefreshLog)); } catch { } }
                else RefreshLog();
            };
            GmLogger.Instance.LogUpdated += _logHandler;

            // Dispose 時取消訂閱，避免對已銷毀的 DGV 呼叫 Rows.Add()
            Disposed += (s, e) => GmLogger.Instance.LogUpdated -= _logHandler;
        }

        // ══════════════════════════════════════════════════════════════
        // Bento Box UI
        // ══════════════════════════════════════════════════════════════
        private void InitUI()
        {
            Text          = "📊 伺服器統計面板";
            Size          = new Size(980, 700);
            MinimumSize   = new Size(760, 540);
            BackColor     = BgPage;
            ForeColor     = Color.FromArgb(235, 240, 255);
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // ── 根 TableLayoutPanel（5 列：title / stats / recharge-stats / rank+log）────────
            var root = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 5,
                ColumnCount = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(GAP + 4)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));          // title
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148f + GAP));   // stat cards
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 148f + GAP));   // recharge stat cards
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  40f));          // 充值排行
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  60f));          // log card
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            // ── Row 0：標題列 ─────────────────────────────────────────
            var titlePanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            var lblTitle = new Label
            {
                Text      = "伺服器即時統計",
                ForeColor = Color.FromArgb(235, 240, 255),
                Font      = new Font(Theme.FontFamily, 17f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(2, 8)
            };
            titlePanel.Controls.Add(lblTitle);

            _statusLbl = new Label
            {
                Text      = "—",
                ForeColor = Color.FromArgb(55, 70, 110),
                Font      = new Font(Theme.FontFamily, 9.5f),
                AutoSize  = true
            };
            titlePanel.Controls.Add(_statusLbl);
            // 讓 statusLbl 跟著 lblTitle 底部對齊
            lblTitle.SizeChanged += (_, __) =>
                _statusLbl.Location = new Point(lblTitle.Left + lblTitle.Width + 14,
                                                 lblTitle.Top + lblTitle.Height - _statusLbl.Height - 2);

            _btnRefresh = Theme.MakeButton("🔄 重新整理",
                Color.FromArgb(30, 75, 160), Color.White, 110, 30);
            _btnRefresh.Anchor  = AnchorStyles.Top | AnchorStyles.Right;
            _btnRefresh.Click  += async (s, e) => await RefreshAsync();
            titlePanel.Controls.Add(_btnRefresh);
            titlePanel.SizeChanged += (_, __) =>
                _btnRefresh.Location = new Point(titlePanel.Width - _btnRefresh.Width, 14);

            root.Controls.Add(titlePanel, 0, 0);

            // ── Row 1：6 張統計卡片（TableLayoutPanel 6 欄）───────────
            var statsWrapper = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding   = new Padding(0, GAP, 0, 0)
            };
            var statsGrid = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 6,
                RowCount    = 1,
                BackColor   = Color.Transparent
            };
            for (int i = 0; i < 6; i++)
                statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
            statsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cardOnline = new BentoStatCard("🟢", "在線玩家",    AOnline,  BgCard);
            _cardTotal  = new BentoStatCard("👥", "總玩家數",    ATotal,   BgCard);
            _cardNew    = new BentoStatCard("✨", "今日新增",    ANew,     BgCard);
            _cardActive = new BentoStatCard("🕹", "今日活躍",   AActive,  BgCard);
            _cardMails  = new BentoStatCard("📬", "發送郵件",    AMails,   BgCard);
            _cardUnread = new BentoStatCard("📩", "待領取郵件",  AUnread,  BgCard);

            var allCards = new BentoStatCard[]
                { _cardOnline, _cardTotal, _cardNew, _cardActive, _cardMails, _cardUnread };

            for (int i = 0; i < allCards.Length; i++)
            {
                allCards[i].Dock   = DockStyle.Fill;
                allCards[i].Margin = new Padding(i == 0 ? 0 : GAP, 0, 0, 0);
                statsGrid.Controls.Add(allCards[i], i, 0);
            }
            statsWrapper.Controls.Add(statsGrid);
            root.Controls.Add(statsWrapper, 0, 1);

            // ── Row 2：充值統計卡片（3 欄）──────────────────────────
            var revWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, GAP, 0, 0) };
            var revGrid    = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            for (int i = 0; i < 3; i++)
                revGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            revGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _cardTodayRev  = new BentoStatCard("💰", "今日充值（元寶）",  ATodayRev, BgCard);
            _cardTodayOrders = new BentoStatCard("🧾", "今日訂單數",      AOrders,   BgCard);
            _cardTotalRev  = new BentoStatCard("💳", "累計充值（元寶）",  ATotalRev, BgCard);

            var revCards = new BentoStatCard[] { _cardTodayRev, _cardTodayOrders, _cardTotalRev };
            for (int i = 0; i < revCards.Length; i++)
            {
                revCards[i].Dock   = DockStyle.Fill;
                revCards[i].Margin = new Padding(i == 0 ? 0 : GAP, 0, 0, 0);
                revGrid.Controls.Add(revCards[i], i, 0);
            }
            revWrapper.Controls.Add(revGrid);
            root.Controls.Add(revWrapper, 0, 2);

            // ── Row 3：充值排行 ────────────────────────────────────
            var rankWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0, GAP, 0, 0) };
            var rankCard    = new BentoCard { Dock = DockStyle.Fill, CardColor = BgCard, Padding = new Padding(0) };

            var rankHdr = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = BgCard };
            rankHdr.Controls.Add(new Label
            {
                Text = "🏆  歷史充值排行 Top 10",
                ForeColor = Color.FromArgb(250, 204, 21), Font = new Font(Theme.FontFamily, 10f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0), BackColor = Color.Transparent
            });

            _rankDgv = new DataGridView { Dock = DockStyle.Fill };
            StyleLogGrid(_rankDgv, BgCard);
            _rankDgv.Columns.Clear();
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名次", Width = 46, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "角色名稱", Width = 130 });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "帳號", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "充值（元寶）", Width = 130, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "換算台幣", Width = 110, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _rankDgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "筆數", Width = 60, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            foreach (DataGridViewColumn c in _rankDgv.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

            rankCard.Controls.Add(rankHdr);
            rankCard.Controls.Add(_rankDgv);
            rankWrapper.Controls.Add(rankCard);
            root.Controls.Add(rankWrapper, 0, 3);

            // ── Row 4：GM 操作紀錄卡片 ───────────────────────────────
            var logWrapper = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding   = new Padding(0, GAP, 0, 0)
            };
            var logCard = new BentoCard
            {
                Dock      = DockStyle.Fill,
                CardColor = BgCardLo,
                Padding   = new Padding(0)
            };

            // 日誌卡片標題列
            var logHdr = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                Height      = 40,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor = Theme.BgCard,
                Padding     = new Padding(16, 0, 12, 0)
            };
            logHdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            logHdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logHdr.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            logHdr.Controls.Add(new Label
            {
                Text      = "📋  今日 GM 操作紀錄",
                ForeColor = Color.FromArgb(145, 160, 210),
                Font      = new Font(Theme.FontFamily, 10.5f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            }, 0, 0);

            var btnClear = Theme.MakeButton("清除", Color.FromArgb(60, 65, 90),
                Color.FromArgb(180, 190, 225), 52, 24);
            btnClear.Click += (_, __) => { _logDgv.Rows.Clear(); };
            logHdr.Controls.Add(btnClear, 1, 0);

            // 日誌卡片底部分隔線
            logHdr.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom, Height = 1,
                BackColor = Theme.BgCard
            });

            _logDgv = new DataGridView { Dock = DockStyle.Fill };
            StyleLogGrid(_logDgv, BgCardLo);

            logCard.Controls.Add(logHdr);
            logCard.Controls.Add(_logDgv);
            logWrapper.Controls.Add(logCard);
            root.Controls.Add(logWrapper, 0, 4);

            RefreshLog();
        }

        private static void StyleLogGrid(DataGridView dgv, Color bg)
        {
            Theme.StyleDataGridView(dgv);
            dgv.BackgroundColor       = bg;
            dgv.ReadOnly              = true;
            dgv.DefaultCellStyle.BackColor = bg;
            dgv.DefaultCellStyle.SelectionBackColor = Theme.BgCard;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Theme.BgCard;

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTime", HeaderText = "時間",   Width = 75  });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOp",   HeaderText = "操作員", Width = 85  });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAct",  HeaderText = "操作",   Width = 105 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTgt",  HeaderText = "對象",   Width = 130 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cDtl", HeaderText = "詳情", Width = 280,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSuc", HeaderText = "結果", Width = 52 });
        }

        // ══════════════════════════════════════════════════════════════
        // 資料更新（邏輯不變）
        // ══════════════════════════════════════════════════════════════
        private async Task RefreshAsync()
        {
            if (!DatabaseManager.Instance.IsConnected)
            {
                _statusLbl.Text = "⚠ 未連接資料庫"; return;
            }
            _btnRefresh.Enabled = false;
            _statusLbl.Text     = "更新中…";
            try
            {
                var s = await DatabaseManager.Instance.GetStatsAsync();
                void Set(BentoStatCard c, int v)
                {
                    if (InvokeRequired) Invoke(new Action(() => c.Value = v.ToString("N0")));
                    else c.Value = v.ToString("N0");
                }
                Set(_cardOnline,  s.OnlineCount);
                Set(_cardTotal,   s.TotalPlayers);
                Set(_cardNew,     s.TodayNewPlayers);
                Set(_cardActive,  s.TodayActive);
                Set(_cardMails,   s.TotalMails);
                Set(_cardUnread,  s.UnreadMails);

                void SetDec(BentoStatCard c, decimal v)
                {
                    string txt = v >= 10000 ? $"{v / 10000:N1}萬" : v.ToString("N0");
                    if (InvokeRequired) Invoke(new Action(() => c.Value = txt));
                    else c.Value = txt;
                }
                SetDec(_cardTodayRev,    s.TodayRevenue);
                SetDec(_cardTotalRev,    s.TotalRevenue);
                Set(_cardTodayOrders, s.TodayOrders);

                // 充值排行
                void FillRank()
                {
                    _rankDgv.Rows.Clear();
                    int rank = 1;
                    foreach (var item in s.TopRechargersAllTime)
                    {
                        string medal = rank == 1 ? "🥇" : rank == 2 ? "🥈" : rank == 3 ? "🥉" : $"#{rank}";
                        int ri = _rankDgv.Rows.Add(medal, item.CharName, item.RoleName,
                            item.YuanText, item.TwdText, item.Count);
                        if (rank <= 3)
                            _rankDgv.Rows[ri].DefaultCellStyle.ForeColor =
                                rank == 1 ? Color.FromArgb(250, 204, 21) :
                                rank == 2 ? Color.FromArgb(200, 200, 210) :
                                            Color.FromArgb(205, 127, 50);
                        rank++;
                    }
                }
                if (InvokeRequired) Invoke(new Action(FillRank));
                else FillRank();

                string ts = $"最後更新 {DateTime.Now:HH:mm:ss}";
                if (InvokeRequired) Invoke(new Action(() => _statusLbl.Text = ts));
                else _statusLbl.Text = ts;
            }
            catch (Exception ex)
            {
                string msg = "✗ " + ex.Message;
                if (InvokeRequired) Invoke(new Action(() => _statusLbl.Text = msg));
                else _statusLbl.Text = msg;
            }
            finally
            {
                if (InvokeRequired) Invoke(new Action(() => _btnRefresh.Enabled = true));
                else _btnRefresh.Enabled = true;
            }
        }

        private void RefreshLog()
        {
            _logDgv.Rows.Clear();
            var logs = GmLogger.Instance.RecentLogs;
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                var log = logs[i];
                int ri  = _logDgv.Rows.Add(
                    log.Time.ToString("HH:mm:ss"),
                    log.Operator, log.Action, log.Target,
                    log.Detail,
                    log.Success ? "✓" : "✗");
                _logDgv.Rows[ri].DefaultCellStyle.ForeColor =
                    log.Success ? Color.FromArgb(200, 210, 240) : Theme.AccentRed;
            }
        }
    }
}
