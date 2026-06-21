using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>伺服器狀態：最新註冊帳號 / 各分流線上人數 / 主帳號在線統計</summary>
    public class ServerStatusForm : Form
    {
        private Label _lblMasterTotal, _lblMasterOnline, _lblMasterOffline;
        private FlowLayoutPanel _channelFlow;
        private Label             _lblIpSummary;
        private DataGridView    _ipDgv;
        private DataGridView    _regDgv;
        private Label           _lblStatus;
        private Button          _btnRefresh;
        private System.Windows.Forms.Timer _autoTimer;
        private int _regLimit = 30;

        public ServerStatusForm()
        {
            Theme.ApplyHubForm(this);
            BuildUI();
            _ = RefreshAsync();
            _autoTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
            _autoTimer.Tick += async (s, e) => await RefreshAsync();
            _autoTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _autoTimer?.Stop();
            _autoTimer?.Dispose();
            base.OnFormClosed(e);
        }

        // ══════════════════════════════════════════════════════════
        // UI
        // ══════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text        = "🖥 伺服器狀態";
            MinimumSize = new Size(860, 560);

            // ── 根 TableLayoutPanel（5 列）────────────────────────────
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5, ColumnCount = 1,
                BackColor = Color.Transparent,
                Padding   = new Padding(Theme.UiPadLg, Theme.UiPadMd, Theme.UiPadLg, Theme.UiPadLg),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));   // 0: 工具列
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f));  // 1: 主帳號 3 卡
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 158f));  // 2: 分流在線
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 236f));  // 3: 登入 IP 在線（含總人數列）
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100f));  // 4: 最新註冊
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // ────────────────────────────────────────────────────────
            // Row 0：工具列
            // ────────────────────────────────────────────────────────
            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 0, 0, 8)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var titleBlock = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var lblTitle = new Label
            {
                Text      = "伺服器即時狀態",
                ForeColor = Theme.TextPrimary,
                Font      = new Font(Theme.FontFamily, 15f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(0, 6)
            };
            _lblStatus = new Label
            {
                Text      = "載入中…",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(0, 32)
            };
            titleBlock.Controls.Add(lblTitle);
            titleBlock.Controls.Add(_lblStatus);

            _btnRefresh = Theme.MakeButton("🔄 重新整理", Color.FromArgb(30, 75, 160), Color.White, 124, 36);
            _btnRefresh.Dock   = DockStyle.Right;
            _btnRefresh.Margin = new Padding(12, 8, 0, 0);
            _btnRefresh.Click += async (s, e) => await RefreshAsync();

            toolbar.Controls.Add(titleBlock,   0, 0);
            toolbar.Controls.Add(_btnRefresh,  1, 0);
            root.Controls.Add(toolbar, 0, 0);

            // ────────────────────────────────────────────────────────
            // Row 1：主帳號統計（3 張卡片）
            // ────────────────────────────────────────────────────────
            var masterGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3, RowCount = 1,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 0, 0, 10)
            };
            for (int i = 0; i < 3; i++)
                masterGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            masterGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            masterGrid.Controls.Add(
                MakeMasterCard("👑", "主帳號總數", Color.FromArgb(59, 130, 246), out _lblMasterTotal), 0, 0);
            masterGrid.Controls.Add(
                MakeMasterCard("🟢", "目前在線",   Color.FromArgb(22, 183, 120), out _lblMasterOnline), 1, 0);
            masterGrid.Controls.Add(
                MakeMasterCard("⚫", "目前離線",   Color.FromArgb(148, 163, 184), out _lblMasterOffline), 2, 0);

            root.Controls.Add(masterGrid, 0, 1);

            // ────────────────────────────────────────────────────────
            // Row 2：各分流在線人數
            // ────────────────────────────────────────────────────────
            var chWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2, ColumnCount = 1,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 0, 0, 8)
            };
            chWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            chWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            chWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            chWrap.Controls.Add(new Label
            {
                Text = "各分流在線人數", ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _channelFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                AutoScroll    = false,
                BackColor     = Color.Transparent
            };
            chWrap.Controls.Add(_channelFlow, 0, 1);
            root.Controls.Add(chWrap, 0, 2);

            // ────────────────────────────────────────────────────────
            // Row 3：登入 IP 在線人數
            // ────────────────────────────────────────────────────────
            var ipWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3, ColumnCount = 1,
                BackColor = Color.Transparent,
                Margin    = new Padding(0, 0, 0, 8)
            };
            ipWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            ipWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            ipWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            ipWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            ipWrap.Controls.Add(new Label
            {
                Text = "登入 IP 在線人數（依目前登入 IP 彙總，含在線／該 IP 帳號總數）",
                ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            _lblIpSummary = new Label
            {
                Text      = "全服在線人數：— ／ 有在線的登入 IP：— ／ 有登入 IP 紀錄的相異 IP：— ／ 在線但無登入 IP：— ／ 下表 Top 40",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            ipWrap.Controls.Add(_lblIpSummary, 0, 1);
            _ipDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_ipDgv);
            Theme.EnableSmoothPaint(_ipDgv);
            _ipDgv.ReadOnly            = true;
            _ipDgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _ipDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ip", HeaderText = "登入 IP", FillWeight = 120,
                DefaultCellStyle = { Font = Theme.FontMono }
            });
            _ipDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "on", HeaderText = "在線", FillWeight = 45,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Color.FromArgb(22, 183, 120) }
            });
            _ipDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "tot", HeaderText = "帳號數", FillWeight = 45,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            ipWrap.Controls.Add(_ipDgv, 0, 2);
            root.Controls.Add(ipWrap, 0, 3);

            // ────────────────────────────────────────────────────────
            // Row 4：最新註冊帳號
            // ────────────────────────────────────────────────────────
            var regWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2, ColumnCount = 1,
                BackColor = Color.Transparent,
                Margin    = new Padding(0)
            };
            regWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            regWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            regWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // 標頭列
            var regHdr = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent
            };
            regHdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            regHdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            regHdr.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            regHdr.Controls.Add(new Label
            {
                Text = "最新註冊帳號", ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, 8.5f, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var cboLimit = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 95, Dock = DockStyle.Right,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat, Font = Theme.FontSmall,
                Margin = new Padding(0, 3, 0, 3)
            };
            cboLimit.Items.AddRange(new object[] { "最新 20 筆", "最新 30 筆", "最新 50 筆", "最新 100 筆" });
            cboLimit.SelectedIndex = 1;
            cboLimit.SelectedIndexChanged += async (s, e) =>
            {
                _regLimit = new[] { 20, 30, 50, 100 }[cboLimit.SelectedIndex];
                await RefreshAsync();
            };
            regHdr.Controls.Add(cboLimit, 1, 0);
            regWrap.Controls.Add(regHdr, 0, 0);

            // DataGridView
            _regDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_regDgv);
            _regDgv.ReadOnly            = true;
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "c0", HeaderText = "狀態",     FillWeight = 50,  MinimumWidth = 50  });
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "c1", HeaderText = "帳號",     FillWeight = 140, MinimumWidth = 90  });
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "c2", HeaderText = "角色名",   FillWeight = 100, MinimumWidth = 70  });
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "c3", HeaderText = "主帳號",   FillWeight = 100, MinimumWidth = 70  });
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "c4", HeaderText = "分流",     FillWeight = 60,  MinimumWidth = 45  });
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "c5", HeaderText = "註冊時間", FillWeight = 130, MinimumWidth = 95  });
            _regDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "c6", HeaderText = "註冊 IP",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 90
            });
            regWrap.Controls.Add(_regDgv, 0, 1);
            root.Controls.Add(regWrap, 0, 4);

            Controls.Add(root);
        }

        private static TableLayoutPanel MakeMasterCard(string icon, string title, Color accent, out Label valueLbl)
        {
            var card = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 2,
                ColumnCount = 2,
                BackColor   = Theme.BgCard,
                Margin      = new Padding(0, 0, 8, 0),
                Padding     = new Padding(14, 10, 12, 10),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var icoLbl = new Label
            {
                Text      = icon,
                Font      = new Font(Theme.FontFamily, 22f),
                ForeColor = accent,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.SetRowSpan(icoLbl, 2);
            card.Controls.Add(icoLbl, 0, 0);

            var titleLbl = new Label
            {
                Text      = title,
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            card.Controls.Add(titleLbl, 1, 0);

            var val = new Label
            {
                Text      = "—",
                ForeColor = accent,
                Font      = new Font(Theme.FontFamily, 22f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            card.Controls.Add(val, 1, 1);

            valueLbl = val;
            return card;
        }

        // ══════════════════════════════════════════════════════════
        // 資料更新
        // ══════════════════════════════════════════════════════════
        private async Task RefreshAsync()
        {
            if (!DatabaseManager.Instance.IsConnected) { SetStatus("⚠ 未連接資料庫"); return; }
            SetBtnEnabled(false);
            SetStatus("更新中…");
            try
            {
                await Task.WhenAll(
                    DatabaseManager.Instance.GetMasterAccountStatsAsync().ContinueWith(t => { if (!t.IsFaulted) UpdateMasterCards(t.Result); }),
                    DatabaseManager.Instance.GetChannelOnlineCountAsync().ContinueWith(t => { if (!t.IsFaulted) UpdateChannelPanel(t.Result); }),
                    DatabaseManager.Instance.GetOnlineLoginIpSummaryAsync().ContinueWith(t => { if (!t.IsFaulted) UpdateIpSummary(t.Result); }),
                    DatabaseManager.Instance.GetOnlineByLoginIpAsync(40).ContinueWith(t => { if (!t.IsFaulted) UpdateIpTable(t.Result); }),
                    DatabaseManager.Instance.GetRecentRegistrationsAsync(_regLimit).ContinueWith(t => { if (!t.IsFaulted) UpdateRegTable(t.Result); })
                );
                SetStatus($"最後更新 {DateTime.Now:HH:mm:ss}（每 30 秒自動更新）");
            }
            catch (Exception ex) { SetStatus("✗ " + ex.Message); }
            finally { SetBtnEnabled(true); }
        }

        private void UpdateMasterCards(MasterAccountStats s)
        {
            void Set(Label l, string v) { if (InvokeRequired) Invoke(() => l.Text = v); else l.Text = v; }
            Set(_lblMasterTotal,   s.TotalMasters.ToString("N0"));
            Set(_lblMasterOnline,  s.OnlineMasters.ToString("N0"));
            Set(_lblMasterOffline, s.OfflineMasters.ToString("N0"));
        }

        private void UpdateChannelPanel(List<ChannelOnlineEntry> channels)
        {
            void Update()
            {
                _channelFlow.Controls.Clear();
                if (channels.Count == 0)
                {
                    _channelFlow.Controls.Add(new Label
                    {
                        Text = "（無分流資料）", ForeColor = Theme.TextMuted,
                        Font = Theme.FontSmall, AutoSize = true
                    });
                    return;
                }
                int maxOnline = Math.Max(1, channels.Max(c => c.OnlineCount));
                int panelH    = _channelFlow.ClientSize.Height > 10 ? _channelFlow.ClientSize.Height : 124;
                int cardH     = panelH - 6;
                int cardW     = Math.Max(132, Math.Min(196,
                    (_channelFlow.ClientSize.Width - channels.Count * 10 - 6) / channels.Count));

                foreach (var ch in channels)
                {
                    string sName = string.IsNullOrEmpty(ch.ServerName) ? $"分流 {ch.ServerId}" : ch.ServerName;

                    // 水平進度條卡片（比直條更穩定）
                    var card = new TableLayoutPanel
                    {
                        BackColor   = Theme.BgCard,
                        Size        = new Size(cardW, cardH),
                        Margin      = new Padding(0, 0, 10, 0),
                        RowCount    = 4,
                        ColumnCount = 1,
                        Padding     = new Padding(12, 10, 12, 10)
                    };
                    card.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f)); // 分流名
                    card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f)); // 在線數字
                    card.RowStyles.Add(new RowStyle(SizeType.Absolute, 14f)); // 進度條
                    card.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 總計
                    card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

                    card.Controls.Add(new Label
                    {
                        Text = sName, ForeColor = Theme.TextSecondary,
                        Font = Theme.FontSmall, Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.BottomLeft
                    }, 0, 0);

                    card.Controls.Add(new Label
                    {
                        Text = ch.OnlineCount.ToString("N0"),
                        ForeColor = Color.FromArgb(22, 183, 120),
                        Font = new Font(Theme.FontFamily, 14f, FontStyle.Bold),
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleLeft
                    }, 0, 1);

                    // 進度條（外框 + 填充）
                    var barWrap = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgMid };
                    float barPct = (float)ch.OnlineCount / maxOnline;
                    var barFill = new Panel
                    {
                        BackColor = ch.OnlineCount > 0
                            ? Color.FromArgb(22, 183, 120)
                            : Color.FromArgb(55, 60, 80),
                        Dock = DockStyle.Left,
                        Width = 0
                    };
                    barWrap.Controls.Add(barFill);
                    barWrap.SizeChanged += (_, __) =>
                        barFill.Width = Math.Max(ch.OnlineCount > 0 ? 4 : 0,
                            (int)(barWrap.Width * barPct));
                    card.Controls.Add(barWrap, 0, 2);

                    card.Controls.Add(new Label
                    {
                        Text = $"總計 {ch.TotalCount:N0}",
                        ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                        Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft
                    }, 0, 3);

                    _channelFlow.Controls.Add(card);
                }
            }
            if (InvokeRequired) Invoke(new Action(Update)); else Update();
        }

        private void UpdateIpSummary(OnlineLoginIpSummary s)
        {
            void Set()
            {
                _lblIpSummary.Text =
                    $"全服在線人數：{s.TotalOnline:N0} 人　｜　有在線的登入 IP：{s.DistinctIpWithOnline:N0} 個　｜　有登入 IP 的相異 IP：{s.DistinctIpAll:N0} 個　｜　在線但無登入 IP：{s.OnlineWithoutLoginIp:N0} 人　｜　下表為 Top 40";
            }
            if (InvokeRequired) Invoke(new Action(Set)); else Set();
        }

        private void UpdateIpTable(List<OnlineIpEntry> rows)
        {
            void Update()
            {
                _ipDgv.Rows.Clear();
                if (rows.Count == 0)
                {
                    _ipDgv.Rows.Add("(無登入 IP 資料)", "—", "—");
                    return;
                }
                foreach (var x in rows)
                    _ipDgv.Rows.Add(x.Ip, x.OnlineCount.ToString("N0"), x.TotalCount.ToString("N0"));
            }
            if (InvokeRequired) Invoke(new Action(Update)); else Update();
        }

        private void UpdateRegTable(List<RecentRegAccount> accounts)
        {
            void Update()
            {
                _regDgv.Rows.Clear();
                foreach (var a in accounts)
                {
                    int ri = _regDgv.Rows.Add(
                        a.IsOnline ? "🟢 在線" : "⚫ 離線",
                        a.Account, a.CharName, a.MasterName,
                        a.ServerName, a.RegTime, a.RegIP);
                    if (a.IsOnline)
                        _regDgv.Rows[ri].DefaultCellStyle.ForeColor = Color.FromArgb(22, 183, 120);
                }
            }
            if (InvokeRequired) Invoke(new Action(Update)); else Update();
        }

        private void SetStatus(string msg)
        {
            if (InvokeRequired) Invoke(new Action(() => _lblStatus.Text = msg));
            else _lblStatus.Text = msg;
        }
        private void SetBtnEnabled(bool v)
        {
            if (InvokeRequired) Invoke(new Action(() => _btnRefresh.Enabled = v));
            else _btnRefresh.Enabled = v;
        }
    }
}
