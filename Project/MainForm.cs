using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public class MainForm : Form
    {
        // ── 側邊欄 & 內容區 ──────────────────────────────────────
        private Panel _sidebar, _contentArea, _navPanel, _navContent;
        private int   _navScrollY = 0;
        private Panel   _playerPage;        // 玩家管理的所有控件放在此 Panel
        private Panel   _playerContent;     // 玩家管理右側內容區（DataGridView 等）
        private Control _currentHubPanel;   // 目前嵌入的 Hub 控件
        private Button  _btnPlayerNav;      // 玩家管理導覽按鈕（用於 Hub 關閉後還原高亮）
        private Button  _playerSubActive;   // 玩家管理左側子選單目前選中項
        private Label   _lblDbDot, _lblDbText, _lblGmName;

        // ── 玩家管理視圖 ──────────────────────────────────────────
        private TextBox      _searchBox;
        private ComboBox     _cmbLimit;   // 搜尋筆數上限
        private Button       _btnQuery;
        private DataGridView _dgv;
        private Label        _lblCount, _lblStatus;
        private List<PlayerInfo> _players = new();

        // ── 導覽按鈕 ─────────────────────────────────────────────
        private Button _activeNav;
        private Button _btnRecharge; // 充值管理（特殊綠色高亮）

        public static string ExeDir =>
            Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppDomain.CurrentDomain.BaseDirectory;

        public MainForm()
        {
            InitUI();
            TryAutoConnect();
            TryAutoLoadGameData();
        }

        // ══════════════════════════════════════════════════════════
        // 視窗初始化
        // ══════════════════════════════════════════════════════════
        private void InitUI()
        {
            Text          = "蘇打石器 GM 管理系統";
            Size          = new Size(1360, 800);
            MinimumSize   = new Size(1100, 640);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterScreen;

            BuildContentArea();
            BuildSidebar();
        }

        // ══════════════════════════════════════════════════════════
        // 左側導覽列 — Apple macOS 風格
        // ══════════════════════════════════════════════════════════
        private void BuildSidebar()
        {
            const int SW = 216;
            _sidebar = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = SW,
                BackColor = Theme.BgSidebar
            };

            const int LOGO_H     = 62;
            const int RECHARGE_H = 50;
            const int HEADER_H   = LOGO_H + RECHARGE_H; // 112
            const int BOTTOM_H   = 106;
            const int NAV_X      = 0;
            int       NAV_W      = SW - 1; // 右側 1px border

            // ── Logo（絕對定位，y=0，62px）──────────────────────
            var logoPanel = new Panel
            {
                Bounds    = new Rectangle(0, 0, NAV_W, LOGO_H),
                BackColor = Theme.BgSidebar
            };
            logoPanel.Controls.Add(new Label
            {
                Text      = "🍅  蘇打石器 GM",
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontLogo,
                AutoSize  = true,
                Location  = new Point(16, 10)
            });
            logoPanel.Controls.Add(new Label
            {
                Text      = "私服後台管理系統",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(18, 36)
            });
            logoPanel.Controls.Add(new Panel
            {
                Bounds    = new Rectangle(0, LOGO_H - 1, NAV_W, 1),
                BackColor = Theme.Border
            });

            // ── 充值管理（絕對定位，y=62，50px，永遠可見）──────
            var rechargePanel = new Panel
            {
                Bounds    = new Rectangle(0, LOGO_H, NAV_W, RECHARGE_H),
                BackColor = Theme.BgSidebar
            };
            _btnRecharge = new Button
            {
                Text      = "   💳  充值管理",
                Location  = new Point(6, 6),
                Size      = new Size(NAV_W - 12, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.AccentGreen,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = Theme.FontNavBold,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop   = false
            };
            _btnRecharge.FlatAppearance.BorderSize         = 0;
            _btnRecharge.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Theme.AccentGreen, 0.15f);
            _btnRecharge.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(Theme.AccentGreen, 0.1f);
            _btnRecharge.Click += (s, e) =>
            {
                SetActiveNav(_btnRecharge);
                if (!CheckConnected()) return;
                SwitchToHub(new RechargeForm());
            };
            rechargePanel.Controls.Add(_btnRecharge);
            rechargePanel.Controls.Add(new Panel
            {
                Bounds    = new Rectangle(0, RECHARGE_H - 1, NAV_W, 1),
                BackColor = Theme.Border
            });

            // ── 導覽 viewport（絕對定位，y=112，高度動態）───────
            _navPanel = new Panel
            {
                Bounds    = new Rectangle(NAV_X, HEADER_H, NAV_W, 400), // 高度由 Resize 更新
                BackColor = Theme.BgSidebar
            };
            _navContent = new Panel
            {
                Location  = new Point(0, 0),
                Width     = NAV_W,
                BackColor = Theme.BgSidebar
            };
            _navPanel.Controls.Add(_navContent);
            _navPanel.Resize += (s, e) =>
            {
                _navContent.Width = _navPanel.ClientSize.Width;
                NavClampScroll();
            };

            // ── 導覽項目（加入 _navContent）─────────────────────
            int y = 8;
            var navTip = new ToolTip { InitialDelay = 400, ReshowDelay = 200, AutoPopDelay = 5000 };
            _navPanel.Disposed += (_, __) => { try { navTip.Dispose(); } catch { } };
            navTip.SetToolTip(_btnRecharge, "手動補單 · 套餐選擇 · 累積儲值進度 · 匯率試算 · 充值記錄");

            // ══ 玩家帳號（含線上監控）══
            AddSectionLabel("玩家帳號", ref y);

            _btnPlayerNav = MakeNavBtn("👥", "玩家管理", ref y, isDefault: true);
            navTip.SetToolTip(_btnPlayerNav, "搜尋玩家 · 查看詳情 · 調金幣水晶 · 封禁 · 改名");
            _btnPlayerNav.Click += (s, e) =>
            {
                SetActiveNav(_btnPlayerNav);
                SwitchToPlayers();
            };

            var btnGuild = MakeNavBtn("⚔", "家族管理", ref y);
            navTip.SetToolTip(btnGuild, "家族列表、成員管理、解散家族");
            btnGuild.Click += (s, e) =>
            {
                SetActiveNav(btnGuild);
                if (!CheckConnected()) return;
                SwitchToHub(new GuildForm());
            };

            var btnDbBrowser = MakeNavBtn("🗄", "資料庫瀏覽", ref y);
            navTip.SetToolTip(btnDbBrowser, "點選任意資料表即可查看內容，支援搜尋/翻頁");
            btnDbBrowser.Click += (s, e) =>
            {
                SetActiveNav(btnDbBrowser);
                if (!CheckConnected()) return;
                var hub = new DbBrowserForm();
                SwitchToHub(hub);
                hub.TriggerLoad();
            };

            var btnIpScan = MakeNavBtn("🔍", "重複IP偵測", ref y);
            navTip.SetToolTip(btnIpScan, "掃描全服共用相同 IP 的帳號群組，找出多開/小號");
            btnIpScan.Click += (s, e) =>
            {
                SetActiveNav(btnIpScan);
                if (!CheckConnected()) return;
                SwitchToHub(new IpScanForm());
            };

            var btnMaster = MakeNavBtn("👑", "主帳號查詢", ref y);
            navTip.SetToolTip(btnMaster, "以主帳號查詢旗下所有子角色，可分帳充值");
            btnMaster.Click += (s, e) =>
            {
                SetActiveNav(btnMaster);
                if (!CheckConnected()) return;
                SwitchToHub(new MasterAccountForm());
            };

            var btnVip = MakeNavBtn("💎", "VIP 管理", ref y);
            navTip.SetToolTip(btnVip, "查看黃金 VIP / 鑽石 VIP 玩家名單（NT$5,000 / NT$15,000 門檻）");
            btnVip.Click += (s, e) =>
            {
                SetActiveNav(btnVip);
                if (!CheckConnected()) return;
                SwitchToHub(new VipForm());
            };

            var btnCostMilestone2 = MakeNavBtn("🏆", "消費里程碑", ref y);
            navTip.SetToolTip(btnCostMilestone2, "查詢玩家累積金幣消費進度，手動發放里程碑獎勵");
            btnCostMilestone2.Click += (s, e) =>
            {
                SetActiveNav(btnCostMilestone2);
                if (!CheckConnected()) return;
                SwitchToHub(new CostMilestoneForm());
            };

            var btnBan = MakeNavBtn("🔒", "封號管理", ref y);
            navTip.SetToolTip(btnBan, "搜尋玩家後快速封禁，或查看全服封禁名單、解封");
            btnBan.Click += (s, e) =>
            {
                SetActiveNav(btnBan);
                if (!CheckConnected()) return;
                SwitchToHub(new BanForm());
            };

            var btnOnline = MakeNavBtn("🟢", "線上玩家", ref y);
            navTip.SetToolTip(btnOnline, "即時顯示目前在線玩家名單，每 30 秒自動刷新");
            btnOnline.Click += (s, e) =>
            {
                SetActiveNav(btnOnline);
                if (!CheckConnected()) return;
                SwitchToHub(new OnlineMonitorForm());
            };

            var btnPlayerHistNav = MakeNavBtn("🔍", "活動歷程", ref y);
            navTip.SetToolTip(btnPlayerHistNav, "查詢單一玩家的交易、攤位、商店、加速、消費歷史記錄");
            btnPlayerHistNav.Click += (s, e) =>
            {
                SetActiveNav(btnPlayerHistNav);
                if (!CheckConnected()) return;
                SwitchToHub(new PlayerHistoryForm());
            };

            AddSideGap(ref y);

            // ══ GM 工具 ══
            AddSectionLabel("GM 工具", ref y);

            var btnBatchOps = MakeNavBtn("📦", "批量工具", ref y);
            navTip.SetToolTip(btnBatchOps, "📬 個別發送 / 📢 批量全服發送 / 🔧 維護工具 — 三個功能合一（含原全服批量發送）");
            btnBatchOps.Click += (s, e) =>
            {
                SetActiveNav(btnBatchOps);
                if (!CheckConnected()) return;
                SwitchToHub(new BatchOpsHubForm());
            };

            var btnBatchGold = MakeNavBtn("💰", "批量金幣", ref y);
            navTip.SetToolTip(btnBatchGold, "對多位玩家同時加減金幣，支援全服、在線、自訂");
            btnBatchGold.Click += (s, e) =>
            {
                SetActiveNav(btnBatchGold);
                if (!CheckConnected()) return;
                SwitchToHub(new BatchGoldForm());
            };

            var btnGmPet = MakeNavBtn("🐾", "GM 寵物指令", ref y);
            navTip.SetToolTip(btnGmPet, "產生 petmake / petmakeabi GM 指令，複製後貼到遊戲後台執行");
            btnGmPet.Click += (s, e) =>
            {
                SetActiveNav(btnGmPet);
                if (!CheckConnected()) return;
                SwitchToHub(new GmPetForm());
            };

            var btnPetRank = MakeNavBtn("🏆", "寵物排行榜", ref y);
            navTip.SetToolTip(btnPetRank, "依戰力/血量/攻擊/防禦/敏捷查詢全服寵物排行");
            btnPetRank.Click += (s, e) =>
            {
                SetActiveNav(btnPetRank);
                if (!CheckConnected()) return;
                SwitchToHub(new PetRankingForm());
            };

            var btnSpeedHack = MakeNavBtn("\u26A1", "\u52A0\u901F\u5916\u639B\u5C01\u7981", ref y);
            navTip.SetToolTip(btnSpeedHack, "\u5206\u6790\u79FB\u52D5\u901F\u5EA6\u7570\u5E38\u73A9\u5BB6\uFF0C\u6279\u91CF\u5C01\u865F");
            btnSpeedHack.Click += (s, e) =>
            {
                SetActiveNav(btnSpeedHack);
                if (!CheckConnected()) return;
                SwitchToHub(new SpeedHackForm());
            };

            AddSideGap(ref y);

            // ══ 紀錄 / 分析（原「紀錄查詢」+ 原「數據分析」合併）══
            AddSectionLabel("紀錄 / 分析", ref y);

            var btnServerStatus = MakeNavBtn("🖥", "伺服器狀態", ref y);
            navTip.SetToolTip(btnServerStatus, "各分流在線人數、主帳號統計、最新註冊名單");
            btnServerStatus.Click += (s, e) =>
            {
                SetActiveNav(btnServerStatus);
                if (!CheckConnected()) return;
                SwitchToHub(new ServerStatusForm());
            };

            var btnDashboard = MakeNavBtn("📊", "統計面板", ref y);
            navTip.SetToolTip(btnDashboard, "總玩家數、在線、封號、今日新增、全服金幣水晶等統計");
            btnDashboard.Click += (s, e) =>
            {
                SetActiveNav(btnDashboard);
                if (!CheckConnected()) return;
                SwitchToHub(new DashboardForm());
            };

            var btnRechargeHist = MakeNavBtn("📜", "充值記錄", ref y);
            navTip.SetToolTip(btnRechargeHist, "查詢全服充值訂單記錄");
            btnRechargeHist.Click += (s, e) =>
            {
                SetActiveNav(btnRechargeHist);
                if (!CheckConnected()) return;
                SwitchToHub(new RechargeHistoryForm());
            };

            var btnTradeLog = MakeNavBtn("🔄", "交易記錄", ref y);
            navTip.SetToolTip(btnTradeLog, "查詢玩家間的物品交易歷史");
            btnTradeLog.Click += (s, e) =>
            {
                SetActiveNav(btnTradeLog);
                if (!CheckConnected()) return;
                SwitchToHub(new TradeLogForm());
            };

            var btnGoldLog = MakeNavBtn("🪙", "金幣日誌", ref y);
            navTip.SetToolTip(btnGoldLog, "查詢金幣異動記錄（GM給予、消費、交易等）");
            btnGoldLog.Click += (s, e) =>
            {
                SetActiveNav(btnGoldLog);
                if (!CheckConnected()) return;
                SwitchToHub(new GoldLogForm());
            };

            var btnMailHist = MakeNavBtn("📧", "郵件記錄", ref y);
            navTip.SetToolTip(btnMailHist, "查詢郵件發送記錄");
            btnMailHist.Click += (s, e) =>
            {
                SetActiveNav(btnMailHist);
                if (!CheckConnected()) return;
                SwitchToHub(new MailHistoryForm());
            };

            var btnStreetShop = MakeNavBtn("🏪", "攤位 & 市場", ref y);
            navTip.SetToolTip(btnStreetShop, "查詢攤位商城上架物品，或根據道具 ID 反查持有者");
            btnStreetShop.Click += (s, e) =>
            {
                SetActiveNav(btnStreetShop);
                if (!CheckConnected()) return;
                SwitchToHub(new StreetShopForm());
            };

            AddSideGap(ref y);

            // ══ 系統管理 ══
            AddSectionLabel("系統管理", ref y);

            var btnGmLog = MakeNavBtn("📋", "GM 操作日誌", ref y);
            navTip.SetToolTip(btnGmLog, "查看所有 GM 帳號的操作記錄");
            btnGmLog.Click += (s, e) =>
            {
                SetActiveNav(btnGmLog);
                SwitchToHub(new GmLogForm());
            };

            var btnSql = MakeNavBtn("💻", "SQL 查詢", ref y);
            navTip.SetToolTip(btnSql, "執行唯讀 SQL（SELECT / SHOW / DESCRIBE），不能修改資料");
            btnSql.Click += (s, e) =>
            {
                SetActiveNav(btnSql);
                SwitchToHub(new SqlQueryForm());
            };

            var btnGmAdmin = MakeNavBtn("🔑", "工具帳號", ref y);
            navTip.SetToolTip(btnGmAdmin, "新增或停用 GM 工具帳號、重設密碼");
            btnGmAdmin.Click += (s, e) =>
            {
                SetActiveNav(btnGmAdmin);
                SwitchToHub(new GmAdminForm());
            };

            var btnRecycle = MakeNavBtn("🗑", "角色回收桶", ref y);
            navTip.SetToolTip(btnRecycle, "查看被刪除的角色，可一鍵還原");
            btnRecycle.Click += (s, e) =>
            {
                SetActiveNav(btnRecycle);
                SwitchToHub(new RecycleBinForm());
            };

            var btnBackup = MakeNavBtn("💾", "備份還原", ref y);
            navTip.SetToolTip(btnBackup, "立即備份資料庫，或從備份檔案還原");
            btnBackup.Click += (s, e) =>
            {
                SetActiveNav(btnBackup);
                SwitchToHub(new BackupForm());
            };

            _navContent.Height = y + 16;

            // ── 底部（絕對定位，高度固定 106px，y 由 Resize 更新）──
            var bottomPanel = new Panel
            {
                Bounds    = new Rectangle(0, 400, NAV_W, BOTTOM_H), // y 由 Resize 更新
                BackColor = Theme.BgSidebar
            };
            bottomPanel.Controls.Add(new Panel
            {
                Bounds    = new Rectangle(0, 0, NAV_W, 1),
                BackColor = Theme.Border
            });

            _lblDbDot = new Label
            {
                Text      = "●",
                ForeColor = Theme.AccentOrange,
                Font      = Theme.FontNavBold,
                AutoSize  = true,
                Location  = new Point(16, 14)
            };
            _lblDbText = new Label
            {
                Text      = "連接中…",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(32, 17)
            };
            _lblGmName = new Label
            {
                Text      = $"GM：{GmLogger.Instance.OperatorName}",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(16, 36)
            };

            var btnConnect  = MakeBottomBtn("🔗  連接資料庫");
            var btnSettings = MakeBottomBtn("⚙  資料設定");
            btnConnect.Location  = new Point(12, 58);
            btnSettings.Location = new Point(12, 82);
            btnConnect.Click  += BtnConnect_Click;
            btnSettings.Click += (s, e) => new SettingsDialog().ShowDialog(this);

            bottomPanel.Controls.AddRange(new Control[]
                { _lblDbDot, _lblDbText, _lblGmName, btnConnect, btnSettings });

            // ── 組合側邊欄（全部絕對定位，右側 1px border 用 Dock.Right）──
            var border = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Theme.Border };

            _sidebar.Controls.AddRange(new Control[]
                { border, logoPanel, rechargePanel, _navPanel, bottomPanel });

            // Resize 時更新 navPanel 和 bottomPanel 的位置/大小
            void UpdateLayout()
            {
                int h     = _sidebar.ClientSize.Height;
                int navH  = Math.Max(0, h - HEADER_H - BOTTOM_H);
                _navPanel.SetBounds(NAV_X, HEADER_H, NAV_W, navH);
                bottomPanel.SetBounds(0, h - BOTTOM_H, NAV_W, BOTTOM_H);
            }
            _sidebar.Resize += (s, e) => UpdateLayout();
            Shown += (_, __) => UpdateLayout();

            Controls.Add(_sidebar);
        }

        private void NavClampScroll()
        {
            if (_navContent == null || _navPanel == null) return;
            int maxScroll   = Math.Max(0, _navContent.Height - _navPanel.ClientSize.Height);
            _navScrollY     = Math.Max(0, Math.Min(maxScroll, _navScrollY));
            _navContent.Top = -_navScrollY;
        }

        private void SidebarMouseWheel(object sender, MouseEventArgs e)
        {
            _navScrollY += e.Delta > 0 ? -60 : 60;
            NavClampScroll();
        }

        // 攔截全 Form 的 WM_MOUSEWHEEL，游標在側邊欄上方就捲導覽列
        private const int WM_MOUSEWHEEL = 0x020A;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL && _sidebar != null)
            {
                var cursorPos = Cursor.Position;
                var sidebarRect = _sidebar.RectangleToScreen(_sidebar.ClientRectangle);
                if (sidebarRect.Contains(cursorPos))
                {
                    int delta = (int)((short)(m.WParam.ToInt64() >> 16));
                    _navScrollY += delta > 0 ? -60 : 60;
                    NavClampScroll();
                    return; // 不傳給原本有焦點的控件
                }
            }
            base.WndProc(ref m);
        }

        private void AddSectionLabel(string title, ref int y)
        {
            _navContent.Controls.Add(new Label
            {
                Text      = title.ToUpper(),
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSection,
                AutoSize  = true,
                Location  = new Point(18, y)
            });
            y += 20;
        }

        private void AddSideGap(ref int y)
        {
            _navContent.Controls.Add(new Panel
            {
                Location  = new Point(14, y + 3),
                Size      = new Size(172, 1),
                BackColor = Theme.Border
            });
            y += 14;
        }

        private Button MakeNavBtn(string icon, string text, ref int y, bool isDefault = false)
        {
            const int BH = 38;
            var bgNorm = Theme.BgSidebar;
            var bgAct  = Theme.AccentBlue;
            var fgNorm = Theme.TextSecondary;
            var fgAct  = Color.White;
            var bgHov  = ControlPaint.Light(Theme.BgSidebar, 0.15f);

            // indicator 直接畫在按鈕左邊緣（Panel 不加入 Controls，改用按鈕自行繪製）
            var btn = new Button
            {
                Text      = $"   {icon}  {text}",
                Location  = new Point(0, y),
                Size      = new Size(196, BH),
                FlatStyle = FlatStyle.Flat,
                BackColor = isDefault ? bgAct : bgNorm,
                ForeColor = isDefault ? fgAct : fgNorm,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = isDefault ? Theme.FontNavBold : Theme.FontNav,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TabStop   = false
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = bgHov;
            btn.FlatAppearance.MouseDownBackColor = bgAct;
            btn.MouseWheel += SidebarMouseWheel;
            // 用左側 border 模擬 indicator（不另加 Panel，避免 z-order 攔截點擊）
            btn.Paint += (s, pe) =>
            {
                if (btn.Tag is bool active && active)
                    pe.Graphics.FillRectangle(new SolidBrush(Theme.AccentBlue), 0, 0, 3, btn.Height);
            };

            if (isDefault)
            {
                _activeNav  = btn;
                btn.Tag     = true;
            }

            _navContent.Controls.Add(btn);
            y += BH;
            return btn;
        }

        private Button MakeBottomBtn(string text)
        {
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(192, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = Theme.FontSmall,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Theme.BgSidebar, 0.12f);
            return btn;
        }

        private void SetActiveNav(Button btn)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetActiveNav(btn))); return; }

            if (_activeNav != null && _activeNav != btn)
            {
                if (_activeNav == _btnRecharge)
                    _activeNav.BackColor = Theme.AccentGreen;
                else
                    _activeNav.BackColor = Theme.BgSidebar;
                _activeNav.ForeColor = _activeNav == _btnRecharge
                    ? Color.FromArgb(134, 239, 172) : Theme.TextSecondary;
                _activeNav.Font = Theme.FontNav;
                _activeNav.Tag  = false;
                _activeNav.Invalidate();
            }

            if (btn == _btnRecharge)
            {
                btn.BackColor = Theme.AccentGreen;
                btn.ForeColor = Color.White;
            }
            else
            {
                btn.BackColor = Theme.AccentBlue;
                btn.ForeColor = Color.White;
            }
            btn.Font = Theme.FontNavBold;
            btn.Tag  = true;
            btn.Invalidate();

            _activeNav = btn;
        }

        private bool CheckConnected()
        {
            if (DatabaseManager.Instance.IsConnected) return true;
            var r = MessageBox.Show("尚未連接資料庫，是否立即連接？", "未連接",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes) BtnConnect_Click(null, null);
            // 連接後回傳最新狀態，避免使用者需要點兩次
            return DatabaseManager.Instance.IsConnected;
        }

        // ══════════════════════════════════════════════════════════
        // 主內容區域
        // ══════════════════════════════════════════════════════════
        private void BuildContentArea()
        {
            _contentArea = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage };

            // 所有玩家管理控件放入 _playerPage，方便切換 Hub 時整體顯示/隱藏
            _playerPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage };
            _playerContent = _playerPage; // 相容舊有 Build* 方法

            BuildStatusBar();
            BuildPlayerGrid();
            BuildHintBar();
            BuildSearchBar();
            BuildContentHeader();

            _contentArea.Controls.Add(_playerPage);
            Controls.Add(_contentArea);
        }

        // ── Hub 切換 ───────────────────────────────────────────
        private void SwitchToHub(Control hub)
        {
            // 移除舊 Hub
            if (_currentHubPanel != null)
            {
                _contentArea.Controls.Remove(_currentHubPanel);
                _currentHubPanel.Dispose();
                _currentHubPanel = null;
            }

            // 直接從 contentArea 移除 playerPage，避免 Z-order 問題
            if (_playerPage.Parent == _contentArea)
                _contentArea.Controls.Remove(_playerPage);

            // 若是 Form 子類別（其他頁面），需要設定 TopLevel=false 才能嵌入
            if (hub is Form hubForm)
            {
                hubForm.TopLevel        = false;
                hubForm.FormBorderStyle = FormBorderStyle.None;
                hubForm.MinimumSize     = Size.Empty;
                hubForm.Location        = Point.Empty;
            }

            hub.Dock      = DockStyle.Fill;
            hub.BackColor = Theme.BgPage;

            _contentArea.Controls.Add(hub);
            _currentHubPanel = hub;
            hub.Show();

            if (hub is GuildForm gf) gf.TriggerLoad();
        }

        private void SwitchToPlayers()
        {
            if (_currentHubPanel != null)
            {
                _contentArea.Controls.Remove(_currentHubPanel);
                _currentHubPanel.Dispose();
                _currentHubPanel = null;
            }
            // 把 playerPage 加回來
            if (_playerPage.Parent != _contentArea)
                _contentArea.Controls.Add(_playerPage);
            _playerPage.Visible = true;
        }

        private void BuildContentHeader()
        {
            var hdr = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };

            hdr.Controls.Add(new Label
            {
                Text      = "👥  玩家管理",
                ForeColor = Theme.TextPrimary,
                Font      = Theme.FontPageTitle,
                AutoSize  = true,
                Location  = new Point(20, 14)
            });

            hdr.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });

            _playerContent.Controls.Add(hdr);
        }

        private void BuildSearchBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.BgCard };

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1,
                BackColor = Color.Transparent
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));           // 0: 搜尋 Label
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));      // 1: 搜尋框
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88f));      // 2: 筆數下拉
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104f));     // 3: 查詢按鈕
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72f));      // 4: 清除按鈕
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tbl.Padding = new Padding(16, 0, 16, 0);

            tbl.Controls.Add(new Label
            {
                Text      = "搜尋",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontBody,
                AutoSize  = true,
                Anchor    = AnchorStyles.Left,
                Margin    = new Padding(0, 0, 10, 0)
            }, 0, 0);

            _searchBox = new TextBox
            {
                BackColor       = Theme.BgPage,
                ForeColor       = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = Theme.FontBody,
                Dock            = DockStyle.Fill,
                Margin          = new Padding(0, 12, 10, 12),
                PlaceholderText = "角色名稱 / 帳號 / 主帳號（空白=全部）"
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) DoSearch(); };
            tbl.Controls.Add(_searchBox, 1, 0);

            // 筆數下拉
            _cmbLimit = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary,
                Font = Theme.FontSmall, Width = 80,
                Dock = DockStyle.Fill, Margin = new Padding(0, 12, 6, 12)
            };
            _cmbLimit.Items.AddRange(new object[] { "300 筆", "500 筆", "1000 筆", "不限" });
            _cmbLimit.SelectedIndex = 0;
            tbl.Controls.Add(_cmbLimit, 2, 0);

            _btnQuery = Theme.MakePrimaryButton("🔍  查詢", 96, 30);
            _btnQuery.Margin = new Padding(0, 11, 6, 11);
            _btnQuery.Dock   = DockStyle.Fill;
            _btnQuery.Click += (s, e) => DoSearch();
            tbl.Controls.Add(_btnQuery, 3, 0);

            var btnClear = Theme.MakeSecondaryButton("清除", 62, 30);
            btnClear.Margin = new Padding(0, 11, 0, 11);
            btnClear.Dock   = DockStyle.Fill;
            btnClear.Click += (s, e) =>
            {
                _searchBox.Clear();
                _players.Clear();
                RefreshGrid();
                _lblCount.Text  = "共 0 筆";
                _lblStatus.Text = "";
            };
            tbl.Controls.Add(btnClear, 4, 0);

            bar.Controls.Add(tbl);
            bar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            _playerContent.Controls.Add(bar);
        }

        private void BuildHintBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Theme.BgMid };
            bar.Controls.Add(new Label
            {
                Text         = "  💡 雙擊列 = 發送道具    👤 資料 = 詳情/改名/充值    💰 貨幣 = 修改金幣    🗑 刪除 = 需二次確認",
                ForeColor    = Theme.TextSecondary,
                Font         = Theme.FontSmall,
                Dock         = DockStyle.Fill,
                TextAlign    = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            });
            bar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            _playerContent.Controls.Add(bar);
        }

        private void BuildStatusBar()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.BgCard };
            bar.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border });
            _lblCount  = new Label { Text = "共 0 筆", ForeColor = Theme.TextMuted,      Font = Theme.FontSmall, AutoSize = true, Location = new Point(16, 8) };
            _lblStatus = new Label { Text = "",       ForeColor = Theme.TextSecondary,  Font = Theme.FontSmall, AutoSize = true, Location = new Point(88, 8) };
            bar.Controls.AddRange(new Control[] { _lblCount, _lblStatus });
            _playerContent.Controls.Add(bar);
        }

        private void BuildPlayerGrid()
        {
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.RowTemplate.Height = 36; // 更高的列高，更好閱讀
            _dgv.ReadOnly = true;

            // 文字欄
            AddTextCol("colOnline",  "狀態",          56);
            AddTextCol("colName",    "角色名稱",       120);
            AddTextCol("colAcc",     "帳號(cdkey)",    118);
            AddTextCol("colMaster",  "👑 主帳號",       105);
            AddTextCol("colVip",     "VIP",            58);
            AddTextCol("colPets",    "🐾 寵物",         56);
            AddTextCol("colPay",     "💳 儲值(NT$)",    105);
            AddTextCol("colLogin",   "最後登入",        110);

            // 功能按鈕（Apple 風格：柔和色調）
            AddBtnCol("colProfile", "👤 資料",  Color.FromArgb(  0, 113, 227), 72);  // Apple blue
            AddBtnCol("colSend",    "✉ 發送",   Color.FromArgb( 48, 176, 199), 64);  // teal
            AddBtnCol("colGold",    "💰 貨幣",   Color.FromArgb(255, 149,   0), 66);  // Apple orange
            AddBtnCol("colPayEdit", "💳 充值",   Color.FromArgb(255, 204,   0), 64);  // yellow (dark text)
            AddBtnCol("colBan",     "🚫 封禁",   Color.FromArgb(255,  59,  48), 64);  // Apple red
            AddBtnCol("colMute",    "🔇 禁言",   Color.FromArgb(175,  82, 222), 64);  // Apple purple
            AddBtnCol("colDelete",  "🗑 刪除",   Color.FromArgb(142,  14,   0), 60);  // dark red

            _dgv.Columns["colName"].ToolTipText = "來自資料庫；若應為英文（如 ying）卻顯示中文，請在「👤 資料」→「編輯角色名稱」修正。↑↓ 點標題可排序。";

            _dgv.CellClick       += Dgv_CellClick;
            _dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var p = _dgv.Rows[e.RowIndex].Tag as PlayerInfo;
                if (p != null) new SendForm(p).ShowDialog(this);
            };

            // ── 點標題排序（依 PlayerInfo 物件做型別正確的比較）──
            bool _sortAsc = true;
            int  _sortCol = -1;
            _dgv.ColumnHeaderMouseClick += (s, e) =>
            {
                int ci = e.ColumnIndex;
                if (_dgv.Columns[ci] is DataGridViewButtonColumn) return;
                _sortAsc = (_sortCol == ci) ? !_sortAsc : true;
                _sortCol = ci;
                string colName = _dgv.Columns[ci].Name;

                // 依欄位選擇排序鍵
                switch (colName)
                {
                    case "colPay":
                        _players.Sort((a, b) => _sortAsc
                            ? a.PayTotal.CompareTo(b.PayTotal)
                            : b.PayTotal.CompareTo(a.PayTotal));
                        break;
                    case "colPets":
                        _players.Sort((a, b) => _sortAsc
                            ? a.PetCount.CompareTo(b.PetCount)
                            : b.PetCount.CompareTo(a.PetCount));
                        break;
                    case "colOnline":
                        _players.Sort((a, b) =>
                        {
                            int av = a.IsBanned ? -1 : a.IsOnline ? 1 : 0;
                            int bv = b.IsBanned ? -1 : b.IsOnline ? 1 : 0;
                            return _sortAsc ? bv.CompareTo(av) : av.CompareTo(bv);
                        });
                        break;
                    default:
                        // 其他欄：字串排序
                        _players.Sort((a, b) =>
                        {
                            string av = colName switch
                            {
                                "colName"   => a.OnlineName,
                                "colAcc"    => a.Account,
                                "colMaster" => a.MasterName,
                                "colLogin"  => a.LoginTime,
                                _           => ""
                            };
                            string bv = colName switch
                            {
                                "colName"   => b.OnlineName,
                                "colAcc"    => b.Account,
                                "colMaster" => b.MasterName,
                                "colLogin"  => b.LoginTime,
                                _           => ""
                            };
                            return _sortAsc
                                ? string.Compare(av, bv, StringComparison.OrdinalIgnoreCase)
                                : string.Compare(bv, av, StringComparison.OrdinalIgnoreCase);
                        });
                        break;
                }
                RefreshGrid();
                // 更新欄位標題排序箭頭
                foreach (DataGridViewColumn col in _dgv.Columns)
                    col.HeaderCell.SortGlyphDirection = SortOrder.None;
                _dgv.Columns[ci].HeaderCell.SortGlyphDirection =
                    _sortAsc ? SortOrder.Ascending : SortOrder.Descending;
            };

            _dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var p = _dgv.Rows[e.RowIndex].Tag as PlayerInfo;
                if (p == null) return;
                var col = _dgv.Columns[e.ColumnIndex].Name;
                if (col == "colOnline")
                {
                    e.CellStyle.ForeColor = p.IsOnline ? Theme.AccentGreen
                        : p.IsBanned ? Theme.AccentRed
                        : Theme.TextMuted;
                    e.CellStyle.Font        = p.IsOnline ? Theme.FontCell9Bold : Theme.FontCell9;
                    e.FormattingApplied     = true;
                }
                if (col == "colVip" && p.PayTotal >= VipHelper.GoldThreshold)
                {
                    e.CellStyle.ForeColor = p.PayTotal >= VipHelper.DiamondThreshold
                        ? Color.FromArgb(100, 180, 255)   // 鑽石藍
                        : Color.FromArgb(255, 200, 60);   // 黃金黃
                    e.CellStyle.Font        = Theme.FontCell11;
                    e.FormattingApplied     = true;
                }
                if (col == "colPay" && p.PayTotal > 0)
                {
                    e.CellStyle.ForeColor = Theme.AccentOrange;
                    e.FormattingApplied   = true;
                }
                if (col == "colMaster" && !string.IsNullOrEmpty(p.MasterName))
                {
                    e.CellStyle.ForeColor = Theme.AccentPurple;
                    e.FormattingApplied   = true;
                }
                if (col == "colPets" && p.PetCount > 0)
                {
                    e.CellStyle.ForeColor = Theme.AccentGreen;
                    e.FormattingApplied   = true;
                }
                if (p.IsBanned)
                {
                    e.CellStyle.BackColor = Color.FromArgb(80, 18, 18);
                    e.CellStyle.ForeColor = Color.White;
                }
            };

            _dgv.Paint += (s, e) =>
            {
                if (_dgv.Rows.Count > 0) return;
                string msg = DatabaseManager.Instance.IsConnected
                    ? "輸入角色名稱後按 Enter 或點「查詢」；留空可顯示全部玩家"
                    : "請先點左側「🔗 連接資料庫」";
                using var br = new SolidBrush(Theme.TextMuted);
                var sz = e.Graphics.MeasureString(msg, Theme.FontHeader);
                e.Graphics.DrawString(msg, Theme.FontHeader, br,
                    (_dgv.Width - sz.Width) / 2f, _dgv.Height / 2f - 12);
            };

            _playerContent.Controls.Add(_dgv);
        }

        private void AddTextCol(string name, string header, int w) =>
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name, HeaderText = header, Width = w, ReadOnly = true,
                DefaultCellStyle = { Padding = new Padding(6, 0, 0, 0) }
            });

        private void AddBtnCol(string name, string text, Color bg, int w) =>
            _dgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = name, HeaderText = "", Width = w, FlatStyle = FlatStyle.Flat,
                UseColumnTextForButtonValue = true, Text = text,
                DefaultCellStyle =
                {
                    BackColor           = bg,
                    ForeColor           = Color.White,
                    SelectionBackColor  = bg,
                    SelectionForeColor  = Color.White,
                    Font                = Theme.FontCell95,
                    Alignment           = DataGridViewContentAlignment.MiddleCenter
                }
            });

        // ══════════════════════════════════════════════════════════
        // 搜尋 & 顯示
        // ══════════════════════════════════════════════════════════
        private async void DoSearch()
        {
            if (!DatabaseManager.Instance.IsConnected)
            {
                var r = MessageBox.Show("尚未連接資料庫，是否立即連接？", "未連接",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes) BtnConnect_Click(null, null);
                return;
            }
            _btnQuery.Enabled = false;
            _btnQuery.Text    = "查詢中…";
            _dgv.Rows.Clear();
            _lblStatus.Text = "查詢中…";
            try
            {
                string q     = _searchBox.Text.Trim();
                int[]  limits = { 300, 500, 1000, 0 };
                int    limit  = limits[Math.Max(0, Math.Min(_cmbLimit?.SelectedIndex ?? 0, limits.Length - 1))];
                _players = await DatabaseManager.Instance.SearchPlayersAsync(q, limit);
                await CheckBanStatusAsync();
                RefreshGrid();
                _lblCount.Text  = $"共 {_players.Count} 筆";
                int online  = _players.FindAll(p => p.IsOnline).Count;
                int banned  = _players.FindAll(p => p.IsBanned).Count;
                _lblStatus.Text = _players.Count == 0
                    ? $"查無「{q}」的角色"
                    : $"✓  在線 {online} 人  ·  封禁 {banned} 人  ·  共 {_players.Count} 筆";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "✗ 查詢失敗：" + ex.Message;
            }
            finally
            {
                _btnQuery.Enabled = true;
                _btnQuery.Text    = "🔍  查  詢";
            }
        }

        private async Task CheckBanStatusAsync()
        {
            try
            {
                foreach (var p in _players)
                {
                    var (banned, endTime) = await DatabaseManager.Instance.GetBanStatusAsync(p.Account);
                    p.IsBanned   = banned;
                    p.BanEndTime = endTime;
                }
            }
            catch { }
        }

        private void RefreshGrid()
        {
            _dgv.Rows.Clear();
            foreach (var p in _players)
            {
                string status = p.IsBanned ? "🔴 封禁" : p.IsOnline ? "🟢 在線" : "⚫ 離線";
                string master = string.IsNullOrEmpty(p.MasterName) ? "—" : p.MasterName;
                var (_, vipEmoji, _, _) = VipHelper.GetTier(p.PayTotal);
                string pets   = p.PetCount > 0 ? $"{p.PetCount} 隻" : "—";
                string pay    = p.PayTotal > 0 ? $"NT$ {p.PayTotal:N0}" : "—";
                int i = _dgv.Rows.Add(status, p.OnlineName, p.Account, master, vipEmoji, pets, pay, p.LoginTime);
                _dgv.Rows[i].Tag = p;
            }
            _dgv.Invalidate();
        }

        private void SetDbStatus(bool ok, string msg = null)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetDbStatus(ok, msg))); return; }
            if (ok)
            {
                _lblDbDot.ForeColor  = Theme.AccentGreen;
                _lblDbText.Text      = "已連接";
                _lblDbText.ForeColor = Theme.AccentGreen;
            }
            else
            {
                bool connecting = msg?.Contains("中") == true;
                _lblDbDot.ForeColor  = connecting ? Theme.AccentOrange : Theme.AccentRed;
                _lblDbText.Text      = connecting ? "連接中…" : (msg ?? "未連接");
                _lblDbText.ForeColor = connecting ? Theme.AccentOrange : Theme.TextMuted;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 表格按鈕事件
        // ══════════════════════════════════════════════════════════
        private async void Dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var player = _dgv.Rows[e.RowIndex].Tag as PlayerInfo;
            if (player == null) return;

            switch (_dgv.Columns[e.ColumnIndex].Name)
            {
                case "colProfile":
                    new PlayerProfileForm(player).ShowDialog(this);
                    break;
                case "colSend":
                    new SendForm(player).ShowDialog(this);
                    break;
                case "colGold":
                    new GoldDialog(player).ShowDialog(this);
                    break;
                case "colPayEdit":
                    // 與玩家詳情一致：同一對話框（含修復循環、發放獎勵、清0、STEP 3）
                    using (var dlg = new AdjustRechargeDialog(player.OnlineName, player.PayTotal, player.PayTotal, player.Account, false, 0))
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK)
                        {
                            bool ok2;
                            if (dlg.IsResetRequest)
                            {
                                ok2 = await DatabaseManager.Instance.ResetPaydataProgressAsync(player.Account);
                                if (ok2)
                                {
                                    player.PayTotal = 0;
                                    _dgv.Rows[e.RowIndex].Cells["colPay"].Value = "—";
                                    _dgv.Rows[e.RowIndex].Cells["colPay"].Style.ForeColor = Theme.TextMuted;
                                    _lblStatus.Text = $"✓  已重置「{player.OnlineName}」累儲進度（歸零）";
                                }
                                else MessageBox.Show("重置失敗（玩家可能無 paydata 記錄）", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                            else
                            {
                                ok2 = await DatabaseManager.Instance.AdjustPayDataPointAsync(
                                    player.Account, dlg.TwdAmount, dlg.GoldAmount, dlg.GiveGold);
                                if (ok2)
                                {
                                    player.PayTotal += dlg.TwdAmount;
                                    long newTotal = player.PayTotal;
                                    _dgv.Rows[e.RowIndex].Cells["colPay"].Value =
                                        newTotal > 0 ? $"NT$ {newTotal:N0}" : "—";
                                    _dgv.Rows[e.RowIndex].Cells["colPay"].Style.ForeColor =
                                        newTotal > 0 ? Color.FromArgb(255, 200, 80) : Theme.TextMuted;
                                    _lblStatus.Text = $"✓  已更新「{player.OnlineName}」充值 +NT${dlg.TwdAmount:N0}" +
                                        (dlg.GiveGold ? $"（金幣 +{dlg.GoldAmount:N0}）" : "（僅累儲進度）");
                                }
                                else MessageBox.Show("修改失敗", "錯誤");
                            }
                            if (dlg.NeedsRefresh)
                            {
                                // 對話框內執行了修復循環或發放獎勵，刷新該列顯示
                                _dgv.Rows[e.RowIndex].Cells["colPay"].Value = player.PayTotal > 0 ? $"NT$ {player.PayTotal:N0}" : "—";
                            }
                        }
                    }
                    break;
                case "colBan":
                    new BanDialog(player).ShowDialog(this);
                    var (banned, endTime) = await DatabaseManager.Instance.GetBanStatusAsync(player.Account);
                    player.IsBanned = banned; player.BanEndTime = endTime;
                    RefreshPlayerRow(e.RowIndex, player);
                    break;
                case "colMute":
                    bool isMuted = await DatabaseManager.Instance.GetMuteStatusAsync(player.Account);
                    string muteAction = isMuted ? "解除禁言" : "禁言";
                    if (MessageBox.Show($"對「{player.OnlineName}」執行【{muteAction}】？", "確認",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        bool ok = await DatabaseManager.Instance.MutePlayerAsync(player.Account, !isMuted);
                        _lblStatus.Text = ok ? $"✓  已{muteAction}「{player.OnlineName}」" : "✗ 操作失敗";
                    }
                    break;
                case "colDelete":
                    await DeletePlayerAsync(_dgv.Rows[e.RowIndex], player);
                    break;
            }
        }

        private void RefreshPlayerRow(int rowIndex, PlayerInfo player)
        {
            string status = player.IsBanned ? "🔴 封禁" : player.IsOnline ? "🟢 在線" : "⚫ 離線";
            _dgv.Rows[rowIndex].Cells["colOnline"].Value = status;
            _dgv.Rows[rowIndex].DefaultCellStyle.BackColor =
                player.IsBanned ? Color.FromArgb(55, 18, 18) : Color.Empty;
        }

        private async Task DeletePlayerAsync(DataGridViewRow row, PlayerInfo player)
        {
            var r1 = MessageBox.Show(
                $"⚠  確定刪除以下角色的帳號資料？\n\n" +
                $"  角色名稱：{player.OnlineName}\n" +
                $"  帳號（cdkey）：{player.Account}\n\n" +
                $"此操作不可復原！請先確保已備份資料。",
                "⚠  確認刪除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (r1 != DialogResult.Yes) return;

            string typedName = Theme.ShowInputDialog("再次確認",
                $"請輸入角色名稱「{player.OnlineName}」以確認刪除：", "", this);
            if (typedName != player.OnlineName)
            {
                if (typedName != null) MessageBox.Show("名稱輸入不符，已取消。", "已取消");
                return;
            }

            try
            {
                if (await DatabaseManager.Instance.DeletePlayerAsync(player.Account, player.OnlineName))
                {
                    _players.Remove(player);
                    _dgv.Rows.Remove(row);
                    _lblCount.Text  = $"共 {_players.Count} 筆";
                    _lblStatus.Text = $"✓  已刪除「{player.OnlineName}」";
                }
                else MessageBox.Show("刪除失敗（可能已不存在）", "錯誤");
            }
            catch (Exception ex) { MessageBox.Show("刪除失敗：" + ex.Message, "錯誤"); }
        }

        // ══════════════════════════════════════════════════════════
        // 連接 & 初始化
        // ══════════════════════════════════════════════════════════
        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            using var dlg = new ConnectDialog(DatabaseManager.Instance.LoadSavedConnectionString());
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            SetDbStatus(false, "● 連接中…");
            try
            {
                var (ok, err) = await DatabaseManager.Instance.ConnectAsync(dlg.ConnectionString);
                SetDbStatus(ok);
                _lblStatus.Text = ok ? "✓  資料庫連接成功！" : "✗ 連接失敗";
                if (!ok) MessageBox.Show("連接失敗：\n" + (err ?? "未知"), "失敗",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex) { SetDbStatus(false); MessageBox.Show(ex.Message, "錯誤"); }
        }

        private async void TryAutoConnect()
        {
            string cs = DatabaseManager.Instance.LoadSavedConnectionString();
            SetDbStatus(false, "● 連接中…");
            var (ok, _) = await DatabaseManager.Instance.ConnectAsync(cs);
            SetDbStatus(ok, ok ? null : "連接失敗");
            // 連線成功後自動載入全部玩家，免得使用者不知道要先查詢
            if (ok)
            {
                // 確保 paydata 表存在 lifetime_total 欄位（歷史總累積儲值）
                await DatabaseManager.Instance.EnsurePaydataLifetimeColumnAsync();
                DoSearch();
            }
        }

        private void TryAutoLoadGameData()
        {
            var gm       = GameDataManager.Instance;
            // items.xlsx = 道具清單
            // pets.xlsx  = 寵物清單
            string items = Path.Combine(ExeDir, "items.xlsx");
            string pets  = Path.Combine(ExeDir, "pets.xlsx");
            if (File.Exists(items)) gm.LoadItems(items);
            if (File.Exists(pets))  gm.LoadPets(pets);
            string info = "";
            if (gm.ItemsLoaded) info += $"道具 {gm.ItemCount} 筆  ";
            if (gm.PetsLoaded)  info += $"寵物 {gm.PetCount} 筆";
            if (info.Length > 0)
            {
                _lblStatus.Text = "✓  已載入：" + info;
            }
            else
            {
                _lblStatus.Text = "⚠  道具資料未載入";
                // 找不到時自動嘗試從同目錄的 GMTool 資料夾複製
                string gmToolDir = Path.Combine(Path.GetDirectoryName(ExeDir) ?? ExeDir, "GMTool");
                bool copied = false;
                try
                {
                    string srcItems = Path.Combine(gmToolDir, "items.xlsx");
                    string srcPets  = Path.Combine(gmToolDir, "pets.xlsx");
                    if (File.Exists(srcItems)) { File.Copy(srcItems, items, true); gm.LoadItems(items); copied = true; }
                    if (File.Exists(srcPets))  { File.Copy(srcPets,  pets,  true); gm.LoadPets(pets);   copied = true; }
                }
                catch { }
                if (copied)
                {
                    string info2 = "";
                    if (gm.ItemsLoaded) info2 += $"道具 {gm.ItemCount} 筆  ";
                    if (gm.PetsLoaded)  info2 += $"寵物 {gm.PetCount} 筆";
                    _lblStatus.Text = "✓  已自動載入：" + info2;
                }
                else
                {
                    _lblStatus.Text = "⚠  請將 items.xlsx 和 pets.xlsx 放到程式同目錄";
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 輔助對話框
    // ══════════════════════════════════════════════════════════════
    public class EditNameDialog : Form
    {
        private TextBox _box;
        public string NewName => _box.Text.Trim();
        public EditNameDialog(string current)
        {
            Text = "編輯角色名稱"; Size = new Size(400, 160);
            BackColor = Theme.BgCard; ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(new Label { Text = "新名稱：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, 18) });
            _box = Theme.MakeTextBox(350); _box.Text = current; _box.Location = new Point(20, 40); Controls.Add(_box);
            var ok = Theme.MakeButton("確 定", Theme.AccentBlue, Color.White, 100, 32);
            ok.Location = new Point(145, 80); ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); }; Controls.Add(ok);
            var cancel = Theme.MakeButton("取 消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            cancel.Location = new Point(255, 80); cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); }; Controls.Add(cancel);
        }
    }

    public class ConnectDialog : Form
    {
        private TextBox _box;
        public string ConnectionString => _box.Text.Trim();
        public ConnectDialog(string current)
        {
            Text = "資料庫連線設定"; Size = new Size(560, 185);
            BackColor = Theme.BgCard; ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(new Label { Text = "連線字串：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, 18) });
            _box = Theme.MakeTextBox(510); _box.Text = current; _box.Location = new Point(20, 40); Controls.Add(_box);
            Controls.Add(new Label { Text = "格式：Server=IP;Database=db;User ID=user;Password=pass;", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, 70) });
            var ok = Theme.MakeButton("🔗 連 接", Theme.AccentBlue, Color.White, 110, 32);
            ok.Location = new Point(320, 100); ok.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); }; Controls.Add(ok);
            var cancel = Theme.MakeButton("取 消", Theme.BgLight, Theme.TextSecondary, 80, 32);
            cancel.Location = new Point(440, 100); cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); }; Controls.Add(cancel);
        }
    }

    public class SettingsDialog : Form
    {
        public SettingsDialog()
        {
            Text = "道具 / 寵物資料設定"; Size = new Size(580, 460);
            BackColor = Theme.BgCard; ForeColor = Theme.TextPrimary;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var gm = GameDataManager.Instance;
            int y = 20;
            AddFileRow("📦 道具列表 (items.xlsx)", gm.ItemsLoaded, gm.ItemCount, gm.PreviewItems(), false, ref y);
            AddFileRow("🐾 寵物列表 (pets.xlsx)",  gm.PetsLoaded,  gm.PetCount,  gm.PreviewPets(),  true,  ref y);

            // ── 同步道具資料到 WebApp ──────────────────────────────
            var syncLbl = new Label
            {
                Text      = "將本機 xlsx 資料同步到 WebApi/Data/*.json，再執行 update-server.ps1 即可更新網頁工具的道具清單。",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Location  = new Point(20, y), Size = new Size(540, 32), AutoSize = false
            };
            Controls.Add(syncLbl); y += 34;

            var syncBtn = Theme.MakeButton("📤 同步道具資料到 WebApp", Theme.AccentGreen, Color.White, 210, 30);
            syncBtn.Location = new Point(20, y);
            syncBtn.Click += (s, e) => SyncItemsToWebApp();
            Controls.Add(syncBtn); y += 44;

            var sep = new Panel { Location = new Point(0, y), Size = new Size(580, 1), BackColor = Theme.Border }; Controls.Add(sep); y += 14;
            Controls.Add(new Label { Text = "GM 操作員名稱：", ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, y + 4) });
            var gmBox = Theme.MakeTextBox(200); gmBox.Text = GmLogger.Instance.OperatorName; gmBox.Location = new Point(145, y); Controls.Add(gmBox);
            var gmSave = Theme.MakeButton("儲存", Theme.AccentBlue, Color.White, 70, 28);
            gmSave.Location = new Point(355, y);
            gmSave.Click += (s, e) => { GmLogger.Instance.OperatorName = gmBox.Text.Trim(); MessageBox.Show("✓ 已儲存"); };
            Controls.Add(gmSave);
            y += 44;

            var closeBtn = Theme.MakeButton("關 閉", Theme.BgLight, Theme.TextSecondary, 80, 30);
            closeBtn.Location = new Point(480, y); closeBtn.Click += (s, e) => Close(); Controls.Add(closeBtn);
        }

        private void AddFileRow(string title, bool loaded, int count, System.Collections.Generic.List<ItemInfo> preview, bool isPet, ref int y)
        {
            Controls.Add(new Label { Text = title, ForeColor = Theme.AccentBlue, Font = Theme.FontHeader, AutoSize = true, Location = new Point(20, y) });
            var st = new Label
            {
                Text = loaded ? $"✓ 已載入 {count} 筆" : "✗ 未載入",
                ForeColor = loaded ? Theme.AccentGreen : Theme.AccentRed,
                Font = Theme.FontSmall, AutoSize = true, Location = new Point(340, y + 4)
            };
            Controls.Add(st);
            string pvText = loaded && preview.Count > 0
                ? "預覽：" + string.Join("、", preview.ConvertAll(i => $"{i.Name}#{i.Id}")) : "";
            Controls.Add(new Label { Text = pvText, ForeColor = Theme.TextMuted, Font = Theme.FontSmall, AutoSize = true, Location = new Point(20, y + 24) });

            var btn = Theme.MakeButton("選擇檔案", Theme.AccentBlue, Color.White, 100, 28);
            btn.Location = new Point(20, y + 44);
            btn.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Excel|*.xlsx" };
                if (ofd.ShowDialog() != DialogResult.OK) return;
                string err = isPet ? GameDataManager.Instance.LoadPets(ofd.FileName) : GameDataManager.Instance.LoadItems(ofd.FileName);
                if (err != null) MessageBox.Show("載入失敗：" + err, "錯誤");
                else
                {
                    var g = GameDataManager.Instance; int cnt = isPet ? g.PetCount : g.ItemCount;
                    st.Text = $"✓ 已載入 {cnt} 筆"; st.ForeColor = Theme.AccentGreen;
                    MessageBox.Show($"載入成功！共 {cnt} 筆");
                }
            };
            Controls.Add(btn);
            y += 92;
        }

        private void SyncItemsToWebApp()
        {
            var gm = GameDataManager.Instance;
            if (!gm.ItemsLoaded && !gm.PetsLoaded)
            {
                MessageBox.Show("尚未載入任何道具資料，請先選擇 items.xlsx / pets.xlsx。", "提示");
                return;
            }

            // 往上兩層找 WebApi/Data（EXE 在 GMTool/ 或 Project/bin/...）
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath) ?? "";
            // 向上最多找 4 層，尋找包含 WebApi 資料夾的目錄
            string? repoRoot = null;
            string cur = exeDir;
            for (int i = 0; i < 5; i++)
            {
                if (Directory.Exists(Path.Combine(cur, "WebApi"))) { repoRoot = cur; break; }
                var parent = Directory.GetParent(cur)?.FullName;
                if (parent == null) break;
                cur = parent;
            }

            if (repoRoot == null)
            {
                MessageBox.Show("找不到 WebApi 資料夾，請確認目錄結構正確。", "錯誤");
                return;
            }

            string dataDir = Path.Combine(repoRoot, "WebApi", "Data");
            Directory.CreateDirectory(dataDir);

            var opts = new JsonSerializerOptions
            {
                WriteIndented          = false,
                PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
            };

            int itemCnt = 0, petCnt = 0;
            if (gm.ItemsLoaded)
            {
                var list = gm.GetAllItems().Select(i => new { id = i.Id, name = i.Name, desc = i.Description, isPet = false });
                File.WriteAllText(Path.Combine(dataDir, "items.json"), JsonSerializer.Serialize(list, opts));
                itemCnt = gm.ItemCount;
            }
            if (gm.PetsLoaded)
            {
                var list = gm.GetAllPets().Select(p => new { id = p.Id, name = p.Name, desc = p.Description, isPet = true });
                File.WriteAllText(Path.Combine(dataDir, "pets.json"), JsonSerializer.Serialize(list, opts));
                petCnt = gm.PetCount;
            }

            MessageBox.Show(
                $"✓ 已寫入 {dataDir}\n\n" +
                $"  道具：{itemCnt} 筆\n  寵物：{petCnt} 筆\n\n" +
                "請執行 .\\update-server.ps1 推送到伺服器，\n網頁工具即可自動使用最新道具清單。",
                "同步完成");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 修復重複角色名稱
    // 查詢所有 OnlineName 重複的角色，從歷史資料推算原始名稱並還原
    // ══════════════════════════════════════════════════════════════
    public class DuplicateNameFixForm : Form
    {
        private DataGridView _dgv;
        private Label        _statusLbl;
        private Button       _btnRefresh, _btnFixAll;

        // 每列的「建議名稱」（由 GM 可修改後套用）
        private readonly System.Collections.Generic.Dictionary<int, TextBox> _nameCells = new();

        public DuplicateNameFixForm()
        {
            Text            = "🔧 修復重複角色名稱";
            Size            = new Size(860, 620);
            MinimumSize     = new Size(720, 480);
            BackColor       = Theme.BgPage;
            ForeColor       = Theme.TextPrimary;
            Font            = Theme.FontBody;
            StartPosition   = FormStartPosition.CenterParent;

            BuildUI();
            _ = LoadDataAsync();
        }

        private void BuildUI()
        {
            // ── 說明列 ──────────────────────────────────────────
            var infoBox = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Theme.BgCard };
            infoBox.Controls.Add(new Label
            {
                Text      = "🔧  修復重複角色名稱",
                ForeColor = Theme.AccentOrange,
                Font      = Theme.FontTitle,
                AutoSize  = true,
                Location  = new Point(14, 8)
            });
            infoBox.Controls.Add(new Label
            {
                Text      = "系統會從「寵物捕捉記錄 (capturepet.author)」和「交易記錄 (tradelog)」自動推算原始名稱。\n" +
                            "若無歷史記錄，請在「新名稱」欄位手動輸入，確認後點「套用」按鈕。",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(14, 36)
            });
            Controls.Add(infoBox);

            // ── 按鈕列 ──────────────────────────────────────────
            var btnRow = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Theme.BgCard };
            _btnRefresh = Theme.MakeButton("🔄 重新查詢", Theme.AccentBlue, Color.White, 110, 30);
            _btnRefresh.Location = new Point(12, 8);
            _btnRefresh.Click   += (s, e) => _ = LoadDataAsync();

            _btnFixAll = Theme.MakeButton("✅ 全部套用", Theme.AccentGreen, Color.White, 110, 30);
            _btnFixAll.Location = new Point(130, 8);
            _btnFixAll.Enabled  = false;
            _btnFixAll.Click   += async (s, e) => await ApplyAllAsync();

            _statusLbl = new Label
            {
                Text      = "",
                ForeColor = Theme.TextMuted,
                Font      = Theme.FontSmall,
                AutoSize  = true,
                Location  = new Point(256, 14)
            };
            btnRow.Controls.AddRange(new Control[] { _btnRefresh, _btnFixAll, _statusLbl });
            Controls.Add(btnRow);

            // ── DataGridView ─────────────────────────────────────
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly            = false;
            _dgv.AllowUserToAddRows  = false;
            _dgv.RowTemplate.Height  = 34;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colAcc",  HeaderText = "帳號 (cdkey)", Width = 150, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colCur",  HeaderText = "目前名稱（重複）", Width = 130, ReadOnly = true,
                  DefaultCellStyle = { ForeColor = Color.FromArgb(230, 80, 80) } });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colSrc",  HeaderText = "來源", Width = 100, ReadOnly = true,
                  DefaultCellStyle = { ForeColor = Theme.TextMuted } });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colNew",  HeaderText = "✏ 新名稱（可直接編輯）",
                  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = false,
                  DefaultCellStyle = { BackColor = Color.FromArgb(18, 36, 18), ForeColor = Color.FromArgb(120, 240, 120) } });
            _dgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name    = "colApply", HeaderText = "", Width = 72,
                FlatStyle = FlatStyle.Flat, UseColumnTextForButtonValue = true, Text = "✅ 套用",
                DefaultCellStyle = {
                    BackColor = Color.FromArgb(30, 100, 30), ForeColor = Color.White,
                    SelectionBackColor = Color.FromArgb(30, 100, 30), SelectionForeColor = Color.White,
                    Font = new Font(Theme.FontFamily, 8.5f),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            _dgv.CellClick += async (s, e) =>
            {
                if (e.RowIndex < 0 || _dgv.Columns[e.ColumnIndex].Name != "colApply") return;
                await ApplyRowAsync(e.RowIndex);
            };

            Controls.Add(_dgv);
        }

        private async Task LoadDataAsync()
        {
            _btnRefresh.Enabled = false;
            _btnFixAll.Enabled  = false;
            _statusLbl.Text     = "查詢中…";
            _statusLbl.ForeColor = Theme.AccentOrange;
            _dgv.Rows.Clear();

            try
            {
                var dupes = await DatabaseManager.Instance.GetDuplicateNamesAsync();
                if (dupes.Count == 0)
                {
                    _statusLbl.Text      = "✅ 目前無重複名稱，資料正常！";
                    _statusLbl.ForeColor = Theme.AccentGreen;
                    _btnRefresh.Enabled  = true;
                    return;
                }

                // 對每個重複帳號查歷史名稱
                foreach (var (account, curName, masterId) in dupes)
                {
                    string guess  = await DatabaseManager.Instance.GuessOriginalNameAsync(account);
                    string source = guess != null ? "🐾 寵物/交易記錄" : "❓ 查無記錄";
                    string newName = guess ?? "";

                    int ri = _dgv.Rows.Add(account, curName, source, newName, "✅ 套用");
                    _dgv.Rows[ri].Tag = (account, curName);

                    // 若推算名稱與當前名稱相同（本來就是小巴），清空建議
                    if (guess == curName)
                    {
                        _dgv.Rows[ri].Cells["colNew"].Value = "";
                        _dgv.Rows[ri].Cells["colSrc"].Value = "⚠ 推算名=目前名（可能是原本就叫此名）";
                    }
                }

                _statusLbl.Text      = $"找到 {dupes.Count} 筆重複名稱，請確認並套用";
                _statusLbl.ForeColor = Theme.AccentOrange;
                _btnFixAll.Enabled   = true;
            }
            catch (Exception ex)
            {
                _statusLbl.Text      = "✗ 查詢失敗：" + ex.Message;
                _statusLbl.ForeColor = Theme.AccentRed;
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        private async Task ApplyRowAsync(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count) return;
            var row      = _dgv.Rows[rowIndex];
            var (account, curName) = ((string, string))row.Tag;
            string newName = row.Cells["colNew"].Value?.ToString()?.Trim() ?? "";

            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("請先在「新名稱」欄位輸入要還原的名稱。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (newName == curName)
            {
                MessageBox.Show("新名稱與目前名稱相同，無需修改。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(
                $"確定將「{account}」的名稱\n從「{curName}」→「{newName}」？",
                "確認還原名稱", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                bool ok = await DatabaseManager.Instance.UpdatePlayerNameAsync(account, curName, newName);
                if (ok)
                {
                    row.Cells["colCur"].Value = newName;
                    row.Cells["colSrc"].Value = "✅ 已還原";
                    row.DefaultCellStyle.BackColor = Color.FromArgb(12, 30, 12);
                    _statusLbl.Text      = $"✅ 已還原：{account} → {newName}";
                    _statusLbl.ForeColor = Theme.AccentGreen;
                }
                else
                    MessageBox.Show("修改失敗，請確認資料庫連線。", "失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("錯誤：" + ex.Message, "錯誤");
            }
        }

        private async Task ApplyAllAsync()
        {
            int ok = 0, skip = 0;
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                string newName = row.Cells["colNew"].Value?.ToString()?.Trim() ?? "";
                var (account, curName) = ((string, string))row.Tag;
                if (string.IsNullOrEmpty(newName) || newName == curName) { skip++; continue; }
                try
                {
                    bool success = await DatabaseManager.Instance.UpdatePlayerNameAsync(account, curName, newName);
                    if (success)
                    {
                        row.Cells["colCur"].Value = newName;
                        row.Cells["colSrc"].Value = "✅ 已還原";
                        row.DefaultCellStyle.BackColor = Color.FromArgb(12, 30, 12);
                        ok++;
                    }
                }
                catch { skip++; }
            }
            _statusLbl.Text      = $"套用完成：成功 {ok} 筆，略過 {skip} 筆";
            _statusLbl.ForeColor = ok > 0 ? Theme.AccentGreen : Theme.AccentOrange;
            if (ok > 0) await LoadDataAsync();
        }
    }

    // ══════════════════════════════════════════════════════════
    // 🗑 角色回收桶（刪除還原）
    // ══════════════════════════════════════════════════════════
    internal class RecycleBinForm : Form
    {
        private DataGridView _dgv;
        private Label        _statusLbl;
        private Button       _btnRestore, _btnRefresh;

        public RecycleBinForm()
        {
            Text          = "🗑  角色回收桶";
            Size          = new Size(820, 520);
            MinimumSize   = new Size(700, 420);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            BuildUI();
        }

        private void BuildUI()
        {
            var lblHint = new Label
            {
                Text      = "所有透過 GM 工具刪除的角色都會先備份到這裡，可隨時一鍵還原。",
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Location  = new Point(16, 12),
                Size      = new Size(780, 24)
            };

            _dgv = new DataGridView
            {
                Location              = new Point(16, 44),
                Size                  = new Size(780, 380),
                BackgroundColor       = Theme.BgCard,
                ForeColor             = Theme.TextPrimary,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                MultiSelect           = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersDefaultCellStyle = { BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary }
            };
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colId",    HeaderText = "ID",       Width = 50,  AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",  HeaderText = "角色名稱", ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAcc",   HeaderText = "帳號",     ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBy",    HeaderText = "刪除者",   Width = 80,  AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTime",  HeaderText = "刪除時間", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });

            _statusLbl = new Label
            {
                Text      = "載入中…",
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(16, 440)
            };

            _btnRestore = Theme.MakeButton("↩ 還原選取角色", Theme.AccentGreen, Color.White, 140, 34);
            _btnRestore.Location = new Point(530, 434);
            _btnRestore.Enabled  = false;
            _btnRestore.Click   += async (s, e) => await RestoreSelectedAsync();

            _btnRefresh = Theme.MakeButton("🔄 重新整理", Theme.BgLight, Theme.TextPrimary, 100, 34);
            _btnRefresh.Location = new Point(420, 434);
            _btnRefresh.Click   += async (s, e) => await LoadAsync();

            var btnClose = Theme.MakeButton("關  閉", Theme.BgLight, Theme.TextSecondary, 80, 34);
            btnClose.Location = new Point(678, 434);
            btnClose.Click   += (s, e) => Close();

            Controls.AddRange(new Control[] { lblHint, _dgv, _statusLbl, _btnRestore, _btnRefresh, btnClose });

            _dgv.SelectionChanged += (s, e) => _btnRestore.Enabled = _dgv.SelectedRows.Count > 0;
            _dgv.CellDoubleClick  += async (s, e) => await RestoreSelectedAsync();

            Load += async (s, e) =>
            {
                await LoadAsync();
                // 自動選中第一行，讓還原按鈕馬上可以點
                if (_dgv.Rows.Count > 0)
                {
                    _dgv.Rows[0].Selected = true;
                    _btnRestore.Enabled   = true;
                }
            };
        }

        private async Task LoadAsync()
        {
            _btnRefresh.Enabled = false;
            _statusLbl.Text     = "載入中…";
            _dgv.Rows.Clear();
            try
            {
                var entries = await DatabaseManager.Instance.GetRecycleBinAsync();
                foreach (var e in entries)
                {
                    int row = _dgv.Rows.Add();
                    _dgv.Rows[row].Cells["colId"].Value    = e.RecycleId;
                    _dgv.Rows[row].Cells["colName"].Value  = e.OnlineName;
                    _dgv.Rows[row].Cells["colAcc"].Value   = e.Account;
                    _dgv.Rows[row].Cells["colBy"].Value    = e.DeletedBy;
                    _dgv.Rows[row].Cells["colTime"].Value  = e.DeletedAt.ToString("yyyy/MM/dd HH:mm:ss");
                    _dgv.Rows[row].Tag = e.RecycleId;
                }
                _statusLbl.Text = entries.Count > 0
                    ? $"共 {entries.Count} 筆已刪除角色（點選後可還原）"
                    : "回收桶是空的。（舊版刪除的角色不在此列）";
                _btnRestore.Enabled = false;
            }
            catch (Exception ex) { _statusLbl.Text = "載入失敗：" + ex.Message; }
            finally { _btnRefresh.Enabled = true; }
        }

        private async Task RestoreSelectedAsync()
        {
            if (_dgv.SelectedRows.Count == 0) return;
            var row = _dgv.SelectedRows[0];
            int rid  = (int)row.Tag;
            string name = row.Cells["colName"].Value?.ToString() ?? "";

            if (MessageBox.Show(
                $"確定要還原角色「{name}」嗎？\n還原後角色會重新出現在玩家列表中。",
                "確認還原", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _btnRestore.Enabled = false;
            var (ok, msg) = await DatabaseManager.Instance.RestoreFromRecycleAsync(rid);
            MessageBox.Show(msg, ok ? "還原成功" : "還原失敗",
                MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);

            if (ok) await LoadAsync();
            else _btnRestore.Enabled = true;
        }
    }

    // ══════════════════════════════════════════════════════════
    // 🔑 修復登入問題
    // 比對 DB 角色名稱 vs 磁碟資料夾，找出不匹配並一鍵修復
    // ══════════════════════════════════════════════════════════
    internal class FixLoginForm : Form
    {
        private TextBox      _txtRolePath;
        private DataGridView _dgv;
        private Label        _statusLbl;
        private Button       _btnScan, _btnFix;

        public FixLoginForm()
        {
            Text          = "🔑  修復角色登入問題";
            Size          = new Size(900, 620);
            MinimumSize   = new Size(780, 520);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── 說明 ─────────────────────────────────────────
            var lblHint = new Label
            {
                Text =
                    "改名後角色無法登入，通常是因為伺服器磁碟上的角色資料夾名稱和資料庫不一致。\n" +
                    "設定角色資料夾路徑後，工具會自動掃描並列出需要修復的資料夾，一鍵重命名即可。",
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Location  = new Point(16, 10),
                Size      = new Size(860, 38)
            };

            // ── 路徑設定 ──────────────────────────────────────
            var lblPath = new Label { Text = "角色資料夾路徑：", ForeColor = Theme.TextPrimary, AutoSize = true, Location = new Point(16, 56) };

            _txtRolePath = new TextBox
            {
                Text        = ServerSettings.Instance.RoleDataPath,
                Location    = new Point(16, 76),
                Size        = new Size(680, 28),
                BackColor   = Theme.BgCard,
                ForeColor   = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            if (string.IsNullOrEmpty(_txtRolePath.Text))
                _txtRolePath.Text = @"（範例：C:\GameServer\role 或 \\伺服器IP\分享\role）";

            var btnBrowse = Theme.MakeButton("瀏覽", Theme.BgLight, Theme.TextPrimary, 60, 28);
            btnBrowse.Location = new Point(704, 76);
            btnBrowse.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "選擇角色資料夾根目錄" };
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtRolePath.Text = dlg.SelectedPath;
            };

            _btnScan = Theme.MakeButton("🔍 掃描", Theme.AccentBlue, Color.White, 80, 28);
            _btnScan.Location = new Point(772, 76);
            _btnScan.Click   += async (s, e) => await ScanAsync();

            // ── 表格 ──────────────────────────────────────────
            var lblCol = new Label
            {
                Text      = "綠色 = 資料夾存在（正常）　紅色 = 找不到資料夾（需修復）　黃色 = 磁碟有但 DB 沒有的資料夾（孤立）",
                ForeColor = Theme.TextMuted,
                AutoSize  = false,
                Location  = new Point(16, 108),
                Size      = new Size(860, 34)
            };

            _dgv = new DataGridView
            {
                Location              = new Point(16, 136),
                Size                  = new Size(860, 380),
                BackgroundColor       = Theme.BgCard,
                ForeColor             = Theme.TextPrimary,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                MultiSelect           = true,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersDefaultCellStyle = { BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary }
            };
            _dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSel",    HeaderText = "選取",       Width = 50, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colStatus", HeaderText = "狀態",       Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colDB",     HeaderText = "資料庫名稱（目標）", ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colFolder", HeaderText = "磁碟資料夾（來源）", ReadOnly = false });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colAcc",    HeaderText = "帳號",       ReadOnly = true });

            // ── 底部 ──────────────────────────────────────────
            _statusLbl = new Label
            {
                Text      = "請設定路徑後點「掃描」",
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(16, 528)
            };

            _btnFix = Theme.MakeButton("🔧 一鍵修復選取項目", Theme.AccentGreen, Color.White, 170, 34);
            _btnFix.Location = new Point(580, 522);
            _btnFix.Enabled  = false;
            _btnFix.Click   += (s, e) => FixSelected();

            var btnClose = Theme.MakeButton("關  閉", Theme.BgLight, Theme.TextSecondary, 80, 34);
            btnClose.Location = new Point(758, 522);
            btnClose.Click   += (s, e) => Close();

            Controls.AddRange(new Control[]
                { lblHint, lblPath, _txtRolePath, btnBrowse, _btnScan, lblCol, _dgv, _statusLbl, _btnFix, btnClose });

            // 若已有設定路徑，自動掃描
            Load += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(ServerSettings.Instance.RoleDataPath))
                    await ScanAsync();
            };
        }

        private async Task ScanAsync()
        {
            string rolePath = _txtRolePath.Text.Trim();
            if (string.IsNullOrEmpty(rolePath) || rolePath.StartsWith("（"))
            {
                MessageBox.Show("請先設定角色資料夾路徑。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Directory.Exists(rolePath))
            {
                MessageBox.Show($"路徑不存在或無法存取：\n{rolePath}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 儲存路徑設定
            ServerSettings.Instance.RoleDataPath = rolePath;
            ServerSettings.Instance.Save();

            _btnScan.Enabled = false;
            _btnFix.Enabled  = false;
            _statusLbl.Text  = "掃描中…";
            _dgv.Rows.Clear();

            try
            {
                // 取得磁碟上所有子資料夾名稱
                var diskFolders = new HashSet<string>(
                    Directory.GetDirectories(rolePath)
                             .Select(d => Path.GetFileName(d)),
                    StringComparer.OrdinalIgnoreCase);

                // 取得 DB 所有角色
                var dbPlayers = await DatabaseManager.Instance.SearchPlayersAsync("");

                int needFix = 0;
                var usedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in dbPlayers)
                {
                    string dbName = p.OnlineName;
                    bool exists   = diskFolders.Contains(dbName);
                    usedFolders.Add(dbName);

                    string status = exists ? "✅ 正常" : "❌ 找不到";
                    Color  bg     = exists
                        ? Color.FromArgb(10, 40, 10)
                        : Color.FromArgb(50, 10, 10);

                    if (!exists) needFix++;

                    int row = _dgv.Rows.Add();
                    _dgv.Rows[row].Cells["colSel"].Value    = !exists;  // 有問題才預選
                    _dgv.Rows[row].Cells["colStatus"].Value = status;
                    _dgv.Rows[row].Cells["colDB"].Value     = dbName;
                    _dgv.Rows[row].Cells["colFolder"].Value = exists ? dbName : "（找不到，請從下方孤立資料夾填入）";
                    _dgv.Rows[row].Cells["colAcc"].Value    = p.Account;
                    _dgv.Rows[row].Tag = p.Account;
                    _dgv.Rows[row].DefaultCellStyle.BackColor = bg;
                }

                // 列出磁碟上有但 DB 沒有的資料夾（孤立，可能是舊名稱）
                var orphans = diskFolders.Except(usedFolders, StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var orphan in orphans)
                {
                    int row = _dgv.Rows.Add();
                    _dgv.Rows[row].Cells["colSel"].Value    = false;
                    _dgv.Rows[row].Cells["colStatus"].Value = "⚠ 孤立";
                    _dgv.Rows[row].Cells["colDB"].Value     = "（DB 中無此角色）";
                    _dgv.Rows[row].Cells["colFolder"].Value = orphan;
                    _dgv.Rows[row].Cells["colAcc"].Value    = "";
                    _dgv.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(40, 35, 0);
                }

                _statusLbl.Text   = needFix > 0
                    ? $"發現 {needFix} 個角色的資料夾名稱不符合，需要修復。在「磁碟資料夾（來源）」填入對應的孤立資料夾名稱後，點一鍵修復。"
                    : $"✅ 所有 {dbPlayers.Count} 個角色的資料夾均正常！";
                _btnFix.Enabled   = needFix > 0;
            }
            catch (Exception ex) { _statusLbl.Text = "掃描失敗：" + ex.Message; }
            finally { _btnScan.Enabled = true; }
        }

        private void FixSelected()
        {
            string rolePath = ServerSettings.Instance.RoleDataPath;
            int ok = 0, fail = 0;

            foreach (DataGridViewRow row in _dgv.Rows)
            {
                bool sel = row.Cells["colSel"].Value is true;
                if (!sel) continue;

                string status    = row.Cells["colStatus"].Value?.ToString() ?? "";
                if (status == "✅ 正常" || status == "⚠ 孤立") continue; // 只處理「找不到」的

                string dbName     = row.Cells["colDB"].Value?.ToString() ?? "";
                string folderName = row.Cells["colFolder"].Value?.ToString()?.Trim() ?? "";

                if (string.IsNullOrEmpty(folderName) || folderName.StartsWith("（")) { fail++; continue; }

                string oldPath = Path.Combine(rolePath, folderName);
                string newPath = Path.Combine(rolePath, dbName);

                try
                {
                    if (!Directory.Exists(oldPath) && !File.Exists(oldPath))
                    { row.Cells["colStatus"].Value = "✗ 來源不存在"; fail++; continue; }

                    if (Directory.Exists(oldPath))
                        Directory.Move(oldPath, newPath);
                    else
                        File.Move(oldPath, newPath);

                    row.Cells["colStatus"].Value    = "✅ 已修復";
                    row.Cells["colFolder"].Value    = dbName;
                    row.DefaultCellStyle.BackColor  = Color.FromArgb(10, 40, 10);
                    ok++;
                }
                catch (Exception ex) { row.Cells["colStatus"].Value = "✗ " + ex.Message; fail++; }
            }

            _statusLbl.Text = $"修復完成：成功 {ok} 筆，失敗 {fail} 筆";
            if (ok > 0)
                MessageBox.Show(
                    $"✅ 已成功修復 {ok} 個角色的資料夾。\n\n請重啟遊戲伺服器，角色即可正常登入。",
                    "修復完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ══════════════════════════════════════════════════════════
    // 💻 SQL 查詢（SELECT only，保護資料安全）
    // ══════════════════════════════════════════════════════════
    internal class SqlQueryForm : Form
    {
        private TextBox      _txtSql;
        private DataGridView _dgv;
        private Label        _statusLbl;
        private Button       _btnRun;

        // 預設快捷查詢
        private static readonly (string label, string sql)[] _presets =
        {
            ("回收桶內容",
             "SELECT recycle_id, deleted_at, deleted_by,\n" +
             "  JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name'))       AS 帳號,\n" +
             "  JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.OnlineName')) AS 角色名稱\n" +
             "FROM csalogin_recycle ORDER BY deleted_at DESC LIMIT 20;"),

            ("所有角色",
             "SELECT Name AS 帳號, OnlineName AS 角色名稱, MasterId, Online AS 在線\n" +
             "FROM csalogin ORDER BY MasterId, Name LIMIT 100;"),

            ("查特定帳號",
             "SELECT * FROM csalogin WHERE Name = 'fa3g6388a845';"),

            ("myshox 旗下角色",
             "SELECT c.Name, c.OnlineName, c.MasterId\n" +
             "FROM csalogin c\n" +
             "JOIN csaloginmaster m ON m.Id = c.MasterId\n" +
             "WHERE m.Name = 'myshox';"),
        };

        public SqlQueryForm()
        {
            Text          = "💻  SQL 查詢";
            Size          = new Size(960, 620);
            MinimumSize   = new Size(800, 500);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            BuildUI();
        }

        private void BuildUI()
        {
            var lblHint = new Label
            {
                Text      = "⚠ 僅允許 SELECT 查詢（資料只讀，不會修改任何資料）",
                ForeColor = Theme.AccentOrange,
                AutoSize  = true,
                Location  = new Point(16, 10)
            };

            // 快捷按鈕列
            int bx = 16;
            int by = 34;
            foreach (var (label, sql) in _presets)
            {
                var btn = Theme.MakeButton(label, Theme.BgLight, Theme.TextPrimary, 0, 26);
                btn.AutoSize = true;
                btn.Location = new Point(bx, by);
                string capSql = sql;
                btn.Click += (s, e) =>
                {
                    _txtSql.Text = capSql;
                    _ = RunQueryAsync();
                };
                Controls.Add(btn);
                bx += btn.Width + 6;
            }

            // SQL 輸入
            _txtSql = new TextBox
            {
                Multiline   = true,
                ScrollBars  = ScrollBars.Vertical,
                Location    = new Point(16, 68),
                Size        = new Size(840, 80),
                BackColor   = Theme.BgCard,
                ForeColor   = Color.FromArgb(180, 230, 180),
                Font        = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                Text        =
                    "SELECT recycle_id, deleted_at, deleted_by,\n" +
                    "  JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name'))       AS 帳號,\n" +
                    "  JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.OnlineName')) AS 角色名稱\n" +
                    "FROM csalogin_recycle ORDER BY deleted_at DESC LIMIT 20;"
            };

            _btnRun = Theme.MakeButton("▶ 執行", Theme.AccentGreen, Color.White, 80, 80);
            _btnRun.Location = new Point(862, 68);
            _btnRun.Font     = new Font(Theme.FontFamily, 11f, FontStyle.Bold);
            _btnRun.Click   += async (s, e) => await RunQueryAsync();

            // 結果表格
            _dgv = new DataGridView
            {
                Location              = new Point(16, 158),
                Size                  = new Size(926, 380),
                BackgroundColor       = Theme.BgCard,
                ForeColor             = Theme.TextPrimary,
                BorderStyle           = BorderStyle.None,
                RowHeadersVisible     = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = true,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.AllCells,
                ColumnHeadersDefaultCellStyle = { BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary }
            };

            _statusLbl = new Label
            {
                Text      = "選擇左上方的快捷查詢，或自行輸入 SQL 後點「▶ 執行」",
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(16, 548)
            };

            var btnClose = Theme.MakeButton("關  閉", Theme.BgLight, Theme.TextSecondary, 80, 30);
            btnClose.Location = new Point(862, 548);
            btnClose.Click   += (s, e) => Close();

            Controls.AddRange(new Control[] { lblHint, _txtSql, _btnRun, _dgv, _statusLbl, btnClose });

            // 啟動時自動執行預設查詢（查回收桶）
            Load += async (s, e) => await RunQueryAsync();
        }

        private async Task RunQueryAsync()
        {
            string sql = _txtSql.Text.Trim();
            if (string.IsNullOrEmpty(sql)) return;

            // 安全性：只允許 SELECT
            string upper = sql.TrimStart().ToUpperInvariant();
            if (!upper.StartsWith("SELECT") && !upper.StartsWith("SHOW") && !upper.StartsWith("DESCRIBE"))
            {
                MessageBox.Show("只允許 SELECT / SHOW / DESCRIBE 查詢，不可執行修改或刪除操作。",
                    "安全限制", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnRun.Enabled = false;
            _statusLbl.Text = "查詢中…";
            _dgv.Columns.Clear();
            _dgv.Rows.Clear();

            try
            {
                var (cols, rows, elapsed) = await DatabaseManager.Instance.RunSelectQueryAsync(sql);

                foreach (var col in cols)
                    _dgv.Columns.Add(col, col);

                foreach (var row in rows)
                {
                    int ri = _dgv.Rows.Add();
                    for (int i = 0; i < row.Count && i < _dgv.Columns.Count; i++)
                        _dgv.Rows[ri].Cells[i].Value = row[i];
                }

                _statusLbl.Text      = $"共 {rows.Count} 筆結果　耗時 {elapsed} ms";
                _statusLbl.ForeColor = Theme.AccentGreen;
            }
            catch (Exception ex)
            {
                _statusLbl.Text      = "錯誤：" + ex.Message;
                _statusLbl.ForeColor = Theme.AccentRed;
            }
            finally { _btnRun.Enabled = true; }
        }
    }

    // ══════════════════════════════════════════════════════════
    // ⚙ 伺服器設定對話框
    // ══════════════════════════════════════════════════════════
    internal class ServerSettingsDialog : Form
    {
        public ServerSettingsDialog()
        {
            Text          = "⚙  伺服器設定";
            Size          = new Size(600, 340);
            MinimumSize   = new Size(500, 300);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            var settings = ServerSettings.Instance;

            // ── 說明標籤 ───────────────────────────────────────
            var lblTitle = new Label
            {
                Text      = "角色存檔資料夾路徑",
                Font      = new Font(Theme.FontFamily, 11f, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                AutoSize  = true,
                Location  = new Point(20, 20)
            };

            var lblHint = new Label
            {
                Text =
                    "石器私服的角色資料通常存放在伺服器的一個資料夾中，\n" +
                    "每個角色是一個以「角色名稱」命名的子資料夾。\n" +
                    "設定此路徑後，GM 工具改名時會自動同步重命名磁碟資料夾。\n\n" +
                    "範例（本機）：C:\\GameServer\\role\\\n" +
                    "範例（網路分享）：\\\\192.168.1.100\\GameShare\\role\\\n" +
                    "若留空：改名只更新資料庫，需手動重命名伺服器資料夾。",
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Location  = new Point(20, 50),
                Size      = new Size(550, 130)
            };

            var txtPath = new TextBox
            {
                Text      = settings.RoleDataPath,
                Location  = new Point(20, 190),
                Size      = new Size(450, 28),
                BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };

            var btnBrowse = Theme.MakeButton("瀏覽…", Theme.BgLight, Theme.TextPrimary, 80, 28);
            btnBrowse.Location = new Point(478, 190);
            btnBrowse.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog
                {
                    Description  = "選擇伺服器角色資料夾根目錄",
                    SelectedPath = txtPath.Text
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtPath.Text = dlg.SelectedPath;
            };

            var btnTest = Theme.MakeButton("測試路徑", Theme.AccentBlue, Color.White, 90, 32);
            btnTest.Location = new Point(20, 232);
            btnTest.Click += (s, e) =>
            {
                string p = txtPath.Text.Trim();
                if (string.IsNullOrEmpty(p)) { MessageBox.Show("路徑為空。", "提示"); return; }
                if (Directory.Exists(p))
                    MessageBox.Show($"✓ 路徑存在！\n共有 {Directory.GetDirectories(p).Length} 個子資料夾（角色）。",
                        "路徑測試", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show($"✗ 路徑不存在或無法存取：\n{p}", "路徑測試", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };

            var btnSave = Theme.MakeButton("儲 存", Theme.AccentGreen, Color.White, 80, 32);
            btnSave.Location = new Point(420, 232);
            btnSave.Click += (s, e) =>
            {
                settings.RoleDataPath = txtPath.Text.Trim();
                settings.Save();
                MessageBox.Show("✓ 設定已儲存。", "儲存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            };

            var btnCancel = Theme.MakeButton("取 消", Theme.BgLight, Theme.TextSecondary, 70, 32);
            btnCancel.Location = new Point(500, 232);
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { lblTitle, lblHint, txtPath, btnBrowse, btnTest, btnSave, btnCancel });
        }
    }

    // ══════════════════════════════════════════════════════════
    // 🚑 緊急還原角色名稱
    // 自動從歷史記錄（寵物/交易）找回原始名稱並還原
    // ══════════════════════════════════════════════════════════
    internal class EmergencyRestoreForm : Form
    {
        private DataGridView _dgv;
        private Label        _statusLbl;
        private Button       _btnScan, _btnApply;
        private TextBox      _txtAccounts;

        public EmergencyRestoreForm()
        {
            Text          = "🚑  緊急還原角色名稱";
            Size          = new Size(780, 580);
            MinimumSize   = new Size(680, 480);
            BackColor     = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── 說明 ───────────────────────────────────────────
            var lblHint = new Label
            {
                Text =
                    "輸入無法登入的角色帳號（每行一個），或直接點「一鍵還原」。\n" +
                    "工具會自動查歷史記錄找回原始名稱並立即還原，無需其他步驟。",
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Location  = new Point(16, 12),
                Size      = new Size(740, 40)
            };

            // ── 帳號輸入框 ─────────────────────────────────────
            _txtAccounts = new TextBox
            {
                Multiline   = true,
                ScrollBars  = ScrollBars.Vertical,
                Location    = new Point(16, 58),
                Size        = new Size(540, 72),
                BackColor   = Theme.BgCard,
                ForeColor   = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text        = "dfegdaddc64h\r\nbffa476be5cg\r\nh62he2dhghg"
            };

            _btnScan = Theme.MakeButton("🔍 重新掃描", Theme.AccentBlue, Color.White, 120, 32);
            _btnScan.Location = new Point(566, 58);
            _btnScan.Click   += async (s, e) => await ScanAsync();

            _btnApply = Theme.MakeButton("✅ 一鍵還原", Theme.AccentGreen, Color.White, 120, 34);
            _btnApply.Location = new Point(566, 96);
            _btnApply.Enabled  = false;
            _btnApply.Font     = new Font(Theme.FontFamily, 10f, FontStyle.Bold);
            _btnApply.Click   += async (s, e) => await ApplyAsync();

            // ── 結果表格 ───────────────────────────────────────
            var lblTip = new Label
            {
                Text      = "💡 「還原為」欄位可手動修改名稱",
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(16, 138)
            };

            _dgv = new DataGridView
            {
                Location          = new Point(16, 158),
                Size              = new Size(740, 330),
                BackgroundColor   = Theme.BgCard,
                ForeColor         = Theme.TextPrimary,
                BorderStyle       = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect       = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersDefaultCellStyle = { BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary }
            };
            _dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSel", HeaderText = "選取", Width = 50, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colAcc", HeaderText = "帳號",     ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colCur", HeaderText = "目前名稱", ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colOrig",HeaderText = "還原為",   ReadOnly = false });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colSrc", HeaderText = "來源",     ReadOnly = true });

            // ── 狀態列 ────────────────────────────────────────
            _statusLbl = new Label
            {
                Text      = "正在查詢歷史記錄…",
                ForeColor = Theme.TextMuted,
                AutoSize  = true,
                Location  = new Point(16, 500)
            };

            var btnClose = Theme.MakeButton("關  閉", Theme.BgLight, Theme.TextSecondary, 80, 34);
            btnClose.Location = new Point(673, 494);
            btnClose.Click   += (s, e) => Close();

            Controls.AddRange(new Control[]
                { lblHint, _txtAccounts, _btnScan, _btnApply, lblTip, _dgv, _statusLbl, btnClose });

            // ── 開啟視窗就自動掃描 ────────────────────────────
            Load += async (s, e) => await ScanAsync();
        }

        private async Task ScanAsync()
        {
            var valid = new List<string>();
            foreach (var a in _txtAccounts.Lines)
            {
                var t = a.Trim();
                if (!string.IsNullOrEmpty(t)) valid.Add(t);
            }
            if (valid.Count == 0) return;

            _btnScan.Enabled  = false;
            _btnApply.Enabled = false;
            _statusLbl.Text   = "查詢歷史記錄中…";
            _dgv.Rows.Clear();

            try
            {
                foreach (var acc in valid)
                {
                    string curName  = await DatabaseManager.Instance.GetCurrentOnlineNameAsync(acc);
                    string origName = await DatabaseManager.Instance.GuessOriginalNameAsync(acc);
                    string src      = origName != null ? "✓ 找到歷史記錄" : "⚠ 查無記錄，請手動填入";
                    origName      ??= curName;

                    int row = _dgv.Rows.Add();
                    _dgv.Rows[row].Cells["colSel"].Value  = origName != curName; // 有不同才預設勾選
                    _dgv.Rows[row].Cells["colAcc"].Value  = acc;
                    _dgv.Rows[row].Cells["colCur"].Value  = curName;
                    _dgv.Rows[row].Cells["colOrig"].Value = origName;
                    _dgv.Rows[row].Cells["colSrc"].Value  = src;
                    _dgv.Rows[row].Tag = (acc, curName);

                    _dgv.Rows[row].DefaultCellStyle.BackColor =
                        src.StartsWith("✓") ? Color.FromArgb(10, 40, 10) : Color.FromArgb(40, 30, 0);
                }

                _statusLbl.Text    = $"找到 {_dgv.Rows.Count} 筆。勾選要還原的項目後點「✅ 一鍵還原」。";
                _btnApply.Enabled  = true;
            }
            catch (Exception ex)
            {
                _statusLbl.Text = "查詢失敗：" + ex.Message;
            }
            finally { _btnScan.Enabled = true; }
        }

        private async Task ApplyAsync()
        {
            int ok = 0, skip = 0, fail = 0;
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                bool sel = row.Cells["colSel"].Value is true;
                if (!sel) { skip++; continue; }

                var (acc, curName) = ((string, string))row.Tag;
                string newName = row.Cells["colOrig"].Value?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(newName) || newName == curName) { skip++; continue; }

                try
                {
                    bool success = await DatabaseManager.Instance.UpdatePlayerNameAsync(acc, curName, newName);
                    if (success)
                    {
                        row.Cells["colCur"].Value = newName;
                        row.Cells["colSrc"].Value = "✅ 已還原";
                        row.DefaultCellStyle.BackColor = Color.FromArgb(10, 40, 10);
                        row.Tag = (acc, newName);
                        ok++;
                    }
                    else { row.Cells["colSrc"].Value = "✗ 失敗"; fail++; }
                }
                catch (Exception ex) { row.Cells["colSrc"].Value = "✗ " + ex.Message; fail++; }
            }
            _statusLbl.Text      = $"還原完成：成功 {ok} 筆，略過 {skip} 筆，失敗 {fail} 筆";
            _statusLbl.ForeColor = ok > 0 ? Theme.AccentGreen : Theme.AccentOrange;

            if (ok > 0)
                MessageBox.Show(
                    $"✅ 已成功還原 {ok} 個角色名稱。\n\n" +
                    "⚠ 注意：如果伺服器磁碟上的角色資料夾名稱不同，\n" +
                    "還需要手動將資料夾改名為對應的角色名稱，\n" +
                    "然後重啟遊戲伺服器，角色才能正常登入。",
                    "還原完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // 📬 道具信箱發送（透過 maildata 給予道具，介面同批量發送）
    // ══════════════════════════════════════════════════════════════
    internal class ItemQueueForm : Form
    {
        // ── 購物車 ──
        private class CartEntry
        {
            public ItemInfo Item { get; set; }
            public int Qty  { get; set; } = 1;
        }
        private readonly List<CartEntry> _cart = new();

        // ── 左側（道具選擇）──
        private TextBox      _searchBox;
        private Label        _itemCountLbl;
        private DataGridView _itemDgv;
        private Label        _pageLabel;
        private Button       _btnPrev, _btnNext;

        // ── 右側（玩家+設定）──
        private DataGridView  _cartDgv;
        private TextBox       _playerSearchBox;
        private ListBox       _playerResultList;
        private Label         _playerCard;
        private TextBox       _txtTitle, _txtContent;
        private CheckBox      _chkSchedule;
        private DateTimePicker _dtStart, _dtEnd;
        private Button        _sendBtn;
        private Label         _statusLbl;

        // ── 狀態 ──
        private PlayerInfo   _selectedPlayer;
        private List<ItemInfo> _filteredItems = new();
        private int          _currentPage = 0;
        private const int    PageSize     = 50;
        private int MaxPage => Math.Max(1, (_filteredItems.Count + PageSize - 1) / PageSize);

        public ItemQueueForm()
        {
            InitUI();
            ApplyFilter();
        }

        // ═══════════════════════════════════════════════════════════
        // 主佈局（與 BatchSendForm 相同結構）
        // ═══════════════════════════════════════════════════════════
        private void InitUI()
        {
            // ── ① Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  📬  道具給予（個別玩家）— 發送道具郵件至指定玩家信箱，玩家開信箱即可領取",
                ForeColor = Theme.AccentOrange,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── ② 搜尋列（道具搜尋）──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text      = "STEP 1 — 輸入名稱、編號或說明關鍵字搜尋道具，或直接翻頁瀏覽",
                ForeColor = Theme.TextMuted,
                Font      = new Font(Theme.FontFamily, 8.5f),
                AutoSize  = true,
                Location  = new Point(12, 4)
            });

            var searchIcon = new Label
            {
                Text = "🔍", Font = new Font("Segoe UI Emoji", 14f),
                AutoSize = true, Location = new Point(12, 22)
            };

            _searchBox = new TextBox
            {
                BackColor       = Theme.BgPage,
                ForeColor       = Theme.TextPrimary,
                BorderStyle     = BorderStyle.FixedSingle,
                Font            = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "道具名稱 / 編號 / 說明關鍵字",
                Location        = new Point(42, 22),
                Height          = 28,
                Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.TextChanged += (s, e) => { _currentPage = 0; ApplyFilter(); };
            _searchBox.KeyDown     += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { _currentPage = 0; ApplyFilter(); e.Handled = true; }
                if (e.KeyCode == Keys.Down && _itemDgv.Rows.Count > 0)
                    { _itemDgv.Focus(); _itemDgv.CurrentCell = _itemDgv.Rows[0].Cells[1]; }
            };

            var itemSearchBtn = Theme.MakePrimaryButton("搜尋", 80, 28);
            itemSearchBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            itemSearchBtn.Click += (s, e) => { _currentPage = 0; ApplyFilter(); };

            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, itemSearchBtn });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                itemSearchBtn.Left   = pw - 12 - itemSearchBtn.Width;
                itemSearchBtn.Top    = 32;
                _searchBox.Width = Math.Max(100, itemSearchBtn.Left - _searchBox.Left - 8);
            };

            // ── ③ SplitContainer（Fill）──
            var split = new SplitContainer
            {
                Dock          = DockStyle.Fill,
                Orientation   = Orientation.Vertical,
                BackColor     = Theme.Border,
                SplitterWidth = 3
            };
            split.Panel1.BackColor = Theme.BgMid;
            split.Panel2.BackColor = Theme.BgMid;
            Load += (s, e) =>
            {
                try
                {
                    split.Panel1MinSize    = 320;
                    split.Panel2MinSize    = 320;
                    split.SplitterDistance = Math.Max(320, Math.Min(split.Width - 320, 480));
                }
                catch { }
            };
            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            Controls.Add(split);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        // ═══════════════════════════════════════════════════════════
        // 左側：道具清單（同 BatchSendForm）
        // ═══════════════════════════════════════════════════════════
        private void BuildLeftPanel(Panel p)
        {
            var layout = new TableLayoutPanel
            {
                Dock            = DockStyle.Fill,
                ColumnCount     = 1,
                RowCount        = 3,
                Margin          = Padding.Empty,
                Padding         = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var titleBar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgDark };
            _itemCountLbl = new Label
            {
                Text      = "📦  道具清單  ←  雙擊加入購物車",
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0)
            };
            titleBar.Controls.Add(_itemCountLbl);

            _itemDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_itemDgv);
            _itemDgv.ReadOnly              = true;
            _itemDgv.RowTemplate.Height    = 26;
            _itemDgv.ColumnHeadersHeight   = 28;
            _itemDgv.AllowUserToResizeRows = false;
            _itemDgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _itemDgv.MultiSelect           = false;
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cId",   HeaderText = "編號",   Width = 66,  SortMode = DataGridViewColumnSortMode.NotSortable, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName", HeaderText = "道具名稱", Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _itemDgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDesc", HeaderText = "說明",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            _itemDgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _itemDgv.Rows[e.RowIndex].Tag is not ItemInfo item) return;
                SelectItem(item);
            };
            _itemDgv.KeyDown += (s, e) =>
            {
                if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) &&
                    _itemDgv.CurrentRow?.Tag is ItemInfo item)
                    SelectItem(item);
            };

            // 翻頁列
            var pageBar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgCard };
            _btnPrev = Theme.MakeButton("◀", Theme.BgMid, Theme.TextPrimary, 40, 30);
            _btnNext = Theme.MakeButton("▶", Theme.BgMid, Theme.TextPrimary, 40, 30);
            _pageLabel = new Label { ForeColor = Theme.TextSecondary, Font = Theme.FontSmall, AutoSize = true };
            _btnPrev.Location  = new Point(12, 9);
            _btnNext.Location  = new Point(56, 9);
            _pageLabel.Location = new Point(104, 13);
            _btnPrev.Click += (s, e) => { if (_currentPage > 0) { _currentPage--; FillPage(); } };
            _btnNext.Click += (s, e) => { if (_currentPage < MaxPage - 1) { _currentPage++; FillPage(); } };
            pageBar.Controls.AddRange(new Control[] { _btnPrev, _btnNext, _pageLabel });

            layout.Controls.Add(titleBar, 0, 0);
            layout.Controls.Add(_itemDgv, 0, 1);
            layout.Controls.Add(pageBar,  0, 2);
            p.Controls.Add(layout);
        }

        // ═══════════════════════════════════════════════════════════
        // 右側：玩家選擇 + 發送設定
        // ═══════════════════════════════════════════════════════════
        private void BuildRightPanel(Panel p)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14, 12, 14, 12) };
            p.Controls.Add(scroll);

            int y = 12, x = 14;

            // ── 購物車標題列 ──
            var cartHdrPanel = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(460, 30),
                BackColor = Theme.BgDark
            };
            cartHdrPanel.Controls.Add(new Label
            {
                Text      = "  🛒  道具購物車 — 雙擊左側加入（可多種道具）",
                ForeColor = Color.FromArgb(100, 180, 255),
                Font      = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            });
            var btnClear = Theme.MakeButton("🗑 清空", Theme.AccentRed, Color.White, 62, 24);
            btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClear.Font   = Theme.FontSmall;
            btnClear.Click += (s, e) => { _cart.Clear(); RefreshCartDgv(); };
            cartHdrPanel.Controls.Add(btnClear);
            cartHdrPanel.Resize += (s, e) => btnClear.Left = cartHdrPanel.ClientSize.Width - 4 - btnClear.Width;
            scroll.Controls.Add(cartHdrPanel);
            y += 32;

            // ── 購物車 DGV ──
            _cartDgv = new DataGridView
            {
                Location              = new Point(x, y),
                Size                  = new Size(460, 130),
                ReadOnly              = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowTemplate           = { Height = 26 },
                ColumnHeadersHeight   = 26,
                MultiSelect           = false,
                BackgroundColor       = Theme.BgCard,
                GridColor             = Theme.Border,
                BorderStyle           = BorderStyle.None
            };
            Theme.StyleDataGridView(_cartDgv);
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cName", HeaderText = "道具名稱",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cId", HeaderText = "編號", Width = 66, ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _cartDgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cQty", HeaderText = "數量", Width = 60,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _cartDgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "cRemove", HeaderText = "", Width = 42,
                Text = "✕", UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.Flat,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = {
                    BackColor = Theme.AccentRed, ForeColor = Color.White,
                    SelectionBackColor = Theme.AccentRed,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });
            _cartDgv.CellEndEdit += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _cart.Count) return;
                if (_cartDgv.Columns[e.ColumnIndex].Name == "cQty")
                {
                    var raw = _cartDgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "1";
                    if (int.TryParse(raw, out int q)) _cart[e.RowIndex].Qty = Math.Max(1, q);
                    RefreshCartDgv();
                }
            };
            _cartDgv.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || _cartDgv.Columns[e.ColumnIndex].Name != "cRemove") return;
                _cart.RemoveAt(e.RowIndex);
                RefreshCartDgv();
            };
            scroll.Controls.Add(_cartDgv);
            y += 138;

            // ── STEP 2：玩家搜尋 ──
            var playerPanel = new Panel
            {
                Location    = new Point(x, y),
                Size        = new Size(440, 116),
                BackColor   = Theme.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };
            playerPanel.Controls.Add(new Label
            {
                Text = "STEP 2 — 搜尋要發送給的玩家（角色名稱 / 帳號 / 主帳號）",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(8, 6)
            });

            _playerSearchBox = new TextBox
            {
                Location        = new Point(8, 26), Height = 26,
                BackColor       = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "輸入角色名稱、帳號或主帳號…",
                Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var playerSearchBtn = Theme.MakePrimaryButton("搜尋", 60, 26);
            playerSearchBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _playerResultList = new ListBox
            {
                Location    = new Point(8, 58),
                Height      = 0,
                BackColor   = Theme.BgLight,
                ForeColor   = Theme.TextPrimary,
                Font        = Theme.FontSmall,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Visible     = false
            };
            _playerCard = new Label
            {
                Text      = "尚未選取玩家",
                ForeColor = Theme.TextMuted, Font = Theme.FontBody,
                AutoSize  = false, AutoEllipsis = true,
                Location  = new Point(8, 58), Height = 24,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            async void DoPlayerSearch()
            {
                string q = _playerSearchBox.Text.Trim();
                if (string.IsNullOrEmpty(q)) return;
                playerSearchBtn.Enabled = false;
                try
                {
                    var results = await DatabaseManager.Instance.SearchPlayersAsync(q);
                    _playerResultList.Items.Clear();
                    foreach (var pi in results) _playerResultList.Items.Add(pi);
                    _playerResultList.Visible = results.Count > 0;
                    _playerResultList.Height  = results.Count > 0 ? Math.Min(results.Count * 20 + 4, 84) : 0;
                    _playerCard.Visible       = results.Count == 0;
                    if (results.Count == 0)
                    {
                        _playerCard.Text      = "找不到玩家，請換關鍵字再試";
                        _playerCard.ForeColor = Theme.AccentOrange;
                    }
                    int newH = 58 + _playerResultList.Height + (_playerCard.Visible ? 28 : 0) + 10;
                    playerPanel.Height = Math.Max(90, newH);
                }
                catch { }
                finally { if (!IsDisposed) playerSearchBtn.Enabled = true; }
            }

            playerSearchBtn.Click  += (s, e) => DoPlayerSearch();
            _playerSearchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { DoPlayerSearch(); e.Handled = true; } };

            _playerResultList.DrawMode   = DrawMode.OwnerDrawFixed;
            _playerResultList.ItemHeight = 20;
            _playerResultList.DrawItem  += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                e.Graphics.FillRectangle(new System.Drawing.SolidBrush(sel ? Theme.AccentBlue : Theme.BgLight), e.Bounds);
                if (_playerResultList.Items[e.Index] is PlayerInfo pi)
                {
                    string dot = pi.IsOnline ? "🟢 " : "";
                    string txt = $"{dot}{pi.OnlineName}  ({pi.Account})";
                    e.Graphics.DrawString(txt, e.Font,
                        new System.Drawing.SolidBrush(sel ? Color.White : Theme.TextPrimary),
                        e.Bounds.X + 4, e.Bounds.Y + 2);
                }
            };
            _playerResultList.SelectedIndexChanged += (s, e) =>
            {
                if (_playerResultList.SelectedItem is not PlayerInfo pi) return;
                _selectedPlayer               = pi;
                _playerCard.Text              = $"✓  {pi.OnlineName}  （帳號：{pi.Account}）{(pi.IsOnline ? "  🟢 在線" : "")}";
                _playerCard.ForeColor         = Color.FromArgb(100, 210, 255);
                _playerCard.Visible           = true;
                _playerResultList.Visible     = false;
                _playerResultList.Height      = 0;
                playerPanel.Height            = 92;
                UpdateSendBtn();
            };

            playerPanel.Controls.AddRange(new Control[] { _playerSearchBox, playerSearchBtn, _playerResultList, _playerCard });
            playerPanel.Resize += (s, e) =>
            {
                int w = playerPanel.Width - 16;
                playerSearchBtn.Location = new Point(8 + Math.Max(60, w - 68), 26);
                _playerSearchBox.Width   = Math.Max(60, playerSearchBtn.Left - 12);
                _playerResultList.Width  = w;
                _playerCard.Width        = w;
            };
            scroll.Controls.Add(playerPanel);
            y += playerPanel.Height + 10;

            // ── 發送設定面板 ──
            var settingPanel = new Panel
            {
                Location    = new Point(x, y),
                Size        = new Size(440, 110),
                BackColor   = Theme.BgCard,
                BorderStyle = BorderStyle.FixedSingle
            };

            int sy = 10;
            void AddRow(string lblTxt, Control ctrl, int ctrlW, string hint = null)
            {
                settingPanel.Controls.Add(new Label
                {
                    Text = lblTxt, ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 72, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                ctrl.Location = new Point(84, sy);
                ctrl.Width    = ctrlW;
                settingPanel.Controls.Add(ctrl);
                if (hint != null)
                    settingPanel.Controls.Add(new Label
                    {
                        Text = hint, ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                        AutoSize = true, Location = new Point(84 + ctrlW + 6, sy + 4)
                    });
                sy += ctrl.Height + 8;
            }

            _txtTitle = new TextBox
            {
                Width = 300, Height = 28, MaxLength = 60,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "預設使用道具名稱"
            };
            _txtContent = new TextBox
            {
                Width = 300, Height = 28, MaxLength = 120,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary, Font = Theme.FontBody,
                PlaceholderText = "預設使用道具名稱"
            };
            _dtEnd = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short, Width = 140, Height = 28,
                Value  = DateTime.Now.AddDays(30)
            };
            _chkSchedule = new CheckBox
            {
                Text = "預約發送時間", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                AutoSize = true, Checked = false, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
            };
            _dtStart = new DateTimePicker
            {
                Format       = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy/MM/dd  HH:mm",
                Width        = 180, Height = 28,
                Value        = DateTime.Now.AddHours(1),
                Enabled      = false
            };
            _chkSchedule.CheckedChanged += (s, e) => _dtStart.Enabled = _chkSchedule.Checked;

            // 範本按鈕
            var tplBtn = Theme.MakeTemplateButton(_txtTitle, _txtContent);

            AddRow("標      題：", _txtTitle,   260);
            tplBtn.Location = new Point(84 + 266, sy - 28 - 8 + 2);
            settingPanel.Controls.Add(tplBtn);

            AddRow("信件內容：", _txtContent, 260);
            AddRow("到期日期：", _dtEnd,       140, "（預設 30 天）");

            {
                settingPanel.Controls.Add(new Label
                {
                    Text = "發送時間：", ForeColor = Theme.TextSecondary, Font = Theme.FontBody,
                    Width = 72, Location = new Point(8, sy + 3), TextAlign = ContentAlignment.MiddleRight
                });
                _chkSchedule.Location = new Point(84, sy + 2);
                _dtStart.Location     = new Point(84 + _chkSchedule.Width + 6, sy);
                settingPanel.Controls.AddRange(new Control[] { _chkSchedule, _dtStart });
                settingPanel.Controls.Add(new Label
                {
                    Text = "（不勾選 = 立即）", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                    AutoSize = true, Location = new Point(_dtStart.Right + 6, sy + 4)
                });
                sy += 36;
            }

            settingPanel.Height = sy + 10;
            scroll.Controls.Add(settingPanel);
            y += settingPanel.Height + 10;

            // ── 發送按鈕 ──
            _sendBtn = new Button
            {
                Text      = "🛒  請加入道具至購物車並選取玩家",
                BackColor = Color.FromArgb(60, 62, 78),
                ForeColor = Theme.TextMuted,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontHeader,
                Size      = new Size(460, 44),
                Location  = new Point(x, y),
                Cursor    = Cursors.Hand,
                Enabled   = false,
                UseVisualStyleBackColor = false
            };
            _sendBtn.FlatAppearance.BorderSize = 0;
            _sendBtn.Click += SendBtn_Click;
            scroll.Controls.Add(_sendBtn);
            y += 52;

            _statusLbl = new Label
            {
                Text = "", ForeColor = Theme.AccentGreen, Font = Theme.FontBody,
                AutoSize = true, Location = new Point(x, y)
            };
            scroll.Controls.Add(_statusLbl);

            p.Resize += (s, e) =>
            {
                int w = Math.Max(260, p.Width - 28);
                cartHdrPanel.Width  = w;
                _cartDgv.Width      = w;
                playerPanel.Width   = w;
                settingPanel.Width  = w;
                _sendBtn.Width      = w;
            };
        }

        // ═══════════════════════════════════════════════════════════
        // 篩選 & 翻頁
        // ═══════════════════════════════════════════════════════════
        private void ApplyFilter()
        {
            var gm = GameDataManager.Instance;
            if (!gm.ItemsLoaded)
            {
                _itemCountLbl.Text      = "📦  道具清單  ⚠ 請至「⚙ 資料設定」載入 items.xlsx";
                _itemCountLbl.ForeColor = Theme.AccentOrange;
                return;
            }
            string q = _searchBox.Text.Trim().ToLower();
            _filteredItems = string.IsNullOrEmpty(q) ? gm.GetAllItems() : gm.SearchItems(q);
            _currentPage = 0;
            FillPage();
        }

        private void FillPage()
        {
            _itemDgv.Rows.Clear();
            int start = _currentPage * PageSize;
            int end   = Math.Min(start + PageSize, _filteredItems.Count);
            for (int i = start; i < end; i++)
            {
                var item = _filteredItems[i];
                int ri = _itemDgv.Rows.Add(item.Id, item.Name, item.Description);
                _itemDgv.Rows[ri].Tag = item;
            }
            _pageLabel.Text      = $"第 {_currentPage + 1} / {MaxPage} 頁  （共 {_filteredItems.Count} 筆）";
            _btnPrev.Enabled     = _currentPage > 0;
            _btnNext.Enabled     = _currentPage < MaxPage - 1;
            _itemCountLbl.Text   = $"📦  道具清單  ←  雙擊加入購物車  （{_filteredItems.Count} 筆）";
            _itemCountLbl.ForeColor = Theme.AccentBlue;
        }

        private void SelectItem(ItemInfo item)
        {
            var existing = _cart.FirstOrDefault(c => c.Item.Id == item.Id);
            if (existing != null)
                existing.Qty++;
            else
                _cart.Add(new CartEntry { Item = item, Qty = 1 });
            RefreshCartDgv();
        }

        private void RefreshCartDgv()
        {
            _cartDgv.Rows.Clear();
            foreach (var e in _cart)
                _cartDgv.Rows.Add(e.Item.Name, e.Item.Id, e.Qty, "✕");

            UpdateSendBtn();
        }

        private void UpdateSendBtn()
        {
            bool ready = _cart.Count > 0 && _selectedPlayer != null;
            _sendBtn.Enabled   = ready;
            if (ready)
            {
                int types = _cart.Count;
                int total = _cart.Sum(c => c.Qty);
                _sendBtn.Text      = $"📬  發送 {types} 種道具 × {total} 件  →  {_selectedPlayer.OnlineName}";
                _sendBtn.BackColor = Theme.AccentGreen;
                _sendBtn.ForeColor = Color.White;
            }
            else
            {
                _sendBtn.Text      = _cart.Count == 0
                    ? "🛒  請加入道具至購物車並選取玩家"
                    : "🛒  購物車已就緒，請選取玩家";
                _sendBtn.BackColor = Color.FromArgb(60, 62, 78);
                _sendBtn.ForeColor = Theme.TextMuted;
            }
        }

        private async void SendBtn_Click(object sender, EventArgs e)
        {
            if (_cart.Count == 0 || _selectedPlayer == null) return;

            string title   = _txtTitle.Text.Trim();
            string content = _txtContent.Text.Trim();
            long   endTs   = new DateTimeOffset(_dtEnd.Value.Date.AddDays(1)).ToUnixTimeSeconds();
            long   startTs = _chkSchedule.Checked
                ? new DateTimeOffset(_dtStart.Value).ToUnixTimeSeconds()
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string scheduleNote = _chkSchedule.Checked
                ? $"預約時間：{_dtStart.Value:yyyy/MM/dd HH:mm}"
                : "立即發送";

            string itemsSummary = string.Join("\n", _cart.Select(c =>
                $"  • {c.Item.Name}（#{c.Item.Id}）× {c.Qty} 份"));

            if (MessageBox.Show(
                $"確定發送道具至玩家信箱？\n\n" +
                $"  玩家：{_selectedPlayer.OnlineName}（{_selectedPlayer.Account}）\n\n" +
                $"【道具清單】（共 {_cart.Count} 種）\n{itemsSummary}\n\n" +
                $"  到期日：{_dtEnd.Value:yyyy/MM/dd}\n" +
                $"  {scheduleNote}\n\n" +
                "玩家開信箱即可領取道具。",
                "確認發送", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _sendBtn.Enabled     = false;
            _statusLbl.Text      = "發送中…";
            _statusLbl.ForeColor = Theme.AccentOrange;

            int successCount = 0, failCount = 0;
            try
            {
                foreach (var entry in _cart.ToList())
                {
                    string itemTitle   = string.IsNullOrEmpty(title)   ? entry.Item.Name : title;
                    string itemContent = string.IsNullOrEmpty(content) ? entry.Item.Name : content;
                    var req = new SendMailRequest
                    {
                        Cdkey     = _selectedPlayer.Account,
                        Type      = 1,
                        Buff1     = itemTitle,
                        Buff2     = itemContent,
                        Data      = entry.Item.Id,
                        StartTime = (int)startTs,
                        EndTime   = (int)endTs,
                        Buff3     = entry.Item.Name,
                        Quantity  = entry.Qty
                    };
                    bool ok = await DatabaseManager.Instance.SendMailAsync(req);
                    if (ok) successCount++; else failCount++;
                }

                if (successCount > 0 && failCount == 0)
                {
                    int totalItems = _cart.Sum(c => c.Qty);
                    _statusLbl.Text      = $"✅ 成功發送 {_cart.Count} 種道具（共 {totalItems} 件）→ {_selectedPlayer.OnlineName}";
                    _statusLbl.ForeColor = Theme.AccentGreen;
                    MessageBox.Show(
                        $"✅ 成功發送 {_cart.Count} 種道具！\n\n" +
                        $"  玩家：{_selectedPlayer.OnlineName}（{_selectedPlayer.Account}）\n" +
                        itemsSummary + "\n\n" +
                        "📬 玩家進入遊戲開信箱即可領取。",
                        "發送成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _cart.Clear();
                    RefreshCartDgv();
                }
                else
                {
                    _statusLbl.Text      = $"⚠ 完成（成功 {successCount} / 失敗 {failCount}），請確認資料庫連線";
                    _statusLbl.ForeColor = Theme.AccentOrange;
                }
            }
            catch (Exception ex)
            {
                _statusLbl.Text      = "✗ " + ex.Message;
                _statusLbl.ForeColor = Theme.AccentRed;
            }
            finally { if (!IsDisposed) UpdateSendBtn(); }
        }
    }

    // 防止 AutoScroll 在子控件獲得焦點時自動捲動
    internal sealed class NoScrollPanel : Panel
    {
        protected override Point ScrollToControl(Control activeControl) => AutoScrollPosition;
    }
}
