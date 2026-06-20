using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    public static class Theme
    {
        // ── 字體選擇 ──────────────────────────────────────────────
        private static readonly string _ff = PickFontFamily(
            "Microsoft JhengHei UI", "Microsoft JhengHei", "Segoe UI");

        private static string PickFontFamily(params string[] candidates)
        {
            var installed = new System.Drawing.Text.InstalledFontCollection()
                                .Families.Select(f => f.Name).ToHashSet();
            foreach (var n in candidates)
                if (installed.Contains(n)) return n;
            return "Segoe UI";
        }

        // ── 深色主題 v4（層次：底 → 卡片 → 工具列，避免整片糊成一格）──
        public static readonly Color BgSidebar = Color.FromArgb( 15,  20,  32); // 側欄（最深）
        public static readonly Color BgDark    = Color.FromArgb( 24,  30,  42); // 頂欄／工具列
        public static readonly Color BgPage    = Color.FromArgb( 28,  34,  48); // 主工作區底
        public static readonly Color BgMid     = Color.FromArgb( 33,  40,  56); // 交錯列／次區
        public static readonly Color BgCard    = Color.FromArgb( 38,  45,  62); // 卡片（略亮於 BgPage）
        public static readonly Color BgLight   = Color.FromArgb( 52,  60,  82); // 輸入框
        public static readonly Color BgInput   = BgLight;
        public static readonly Color CardBg    = BgCard;

        public static readonly Color Border      = Color.FromArgb( 58,  72, 102);
        public static readonly Color BorderHov   = Color.FromArgb( 59, 130, 246);
        public static readonly Color SidebarEdge = Color.FromArgb( 42,  95, 168);

        // 對話框／內嵌標題帶
        public static readonly Color BgDialogHeader = Color.FromArgb( 22,  28,  40);
        public static readonly Color BgInset        = Color.FromArgb( 36,  42,  58);
        public static readonly Color AccentLineSubtle = Color.FromArgb( 56, 120, 220);

        /// <summary>全站內邊距（8px 網格，v5 加寬避免擠在一起）</summary>
        public static readonly int UiPadXl = 40;
        public static readonly int UiPadLg = 32;
        public static readonly int UiPadMd = 24;
        public static readonly int UiPadSm = 18;
        public static readonly int GapXs   = 6;
        public static readonly int GapSm   = 10;
        public static readonly int GapMd   = 16;
        public static readonly int GapLg   = 24;
        public static readonly int BtnHeight    = 44;
        public static readonly int BtnHeightSm  = 36;
        public static readonly int ToolbarHeight = 68;
        public static readonly int PageHeaderHeight = 104;
        public static readonly int GridRowHeight      = 48;
        public static readonly int GridHeaderHeight   = 50;
        public static readonly int GridRowHeightCompact = 42;

        /// <summary>對話框外殼：雙緩衝、底色與字體一致。</summary>
        public static void ApplyDialogShell(Form form)
        {
            if (form == null) return;
            form.BackColor = BgPage;
            form.ForeColor = TextPrimary;
            form.Font      = FontBody;
            EnableSmoothPaint(form);
        }

        /// <summary>主視窗與大型工具視窗：與 ApplyDialogShell 相同，語意上標示「整體改版」入口。</summary>
        public static void ApplyMainWindowChrome(Form form) => ApplyDialogShell(form);

        public const int HubTabBarHeight    = 48;
        public const int HubSearchBarHeight = 64;
        public const int HubFooterHeight    = 52;
        public const int HubKpiPanelHeight  = 88;

        /// <summary>嵌入主視窗的 Hub（Form 或 UserControl）— 底色、字體、留白、雙緩衝。</summary>
        public static void ApplyHubForm(Control hub)
        {
            if (hub == null) return;
            hub.BackColor = BgPage;
            hub.ForeColor = TextPrimary;
            hub.Font      = FontBody;
            EnableSmoothPaint(hub);
            if (hub is Form f && f.FormBorderStyle != FormBorderStyle.None && f.Dock != DockStyle.Fill)
                return;
            hub.Padding = new Padding(UiPadMd, UiPadSm, UiPadMd, UiPadSm);
        }

        /// <summary>Hub 分頁頂部標題列（固定高度、左色條）</summary>
        public static Panel MakeHubPageHeader(string title, Color accent, string subtitle = null)
        {
            var hdr = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = ToolbarHeight,
                BackColor = BgDialogHeader,
                Padding   = new Padding(UiPadLg, GapMd, UiPadLg, GapSm)
            };
            hdr.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });
            hdr.Controls.Add(new Label
            {
                Text      = title,
                ForeColor = accent,
                Font      = FontPageTitle,
                AutoSize  = true,
                Location  = new Point(UiPadLg + 8, 8),
                BackColor = Color.Transparent
            });
            if (!string.IsNullOrEmpty(subtitle))
            {
                hdr.Controls.Add(new Label
                {
                    Text      = subtitle,
                    ForeColor = TextMuted,
                    Font      = FontPageSubtitle,
                    AutoSize  = true,
                    Location  = new Point(UiPadLg + 8, 38),
                    BackColor = Color.Transparent
                });
            }
            hdr.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Border });
            EnableSmoothPaint(hdr);
            return hdr;
        }

        // 強調色（與 WebApp CSS 變數對齊）
        public static readonly Color AccentBlue   = Color.FromArgb( 59, 130, 246);
        public static readonly Color AccentCyan    = Color.FromArgb(  6, 182, 212);
        public static readonly Color AccentGreen   = Color.FromArgb( 34, 197,  94);
        public static readonly Color AccentRed     = Color.FromArgb(239,  68,  68);
        public static readonly Color AccentOrange  = Color.FromArgb(245, 158,  11);
        public static readonly Color AccentPurple  = Color.FromArgb(139,  92, 246);

        // 文字（深底上確保高對比；Secondary/Muted 略提亮避免「看不到字」）
        public static readonly Color TextPrimary   = Color.FromArgb(235, 240, 250);
        public static readonly Color TextSecondary = Color.FromArgb(205, 212, 228);
        public static readonly Color TextMuted     = Color.FromArgb(175, 185, 205);

        // ── 字體（略放大，長時間閱讀較舒適）────────────────────────
        public static readonly Font FontTitle      = new Font(_ff, 16f,    FontStyle.Bold);
        public static readonly Font FontHeader     = new Font(_ff, 11.5f,  FontStyle.Bold);
        public static readonly Font FontBody       = new Font(_ff, 11.5f);
        public static readonly Font FontSmall      = new Font(_ff, 10.5f);
        public static readonly Font FontMono       = new Font("Consolas", 10.5f);
        public static readonly Font FontNav        = new Font(_ff, 11f);
        public static readonly Font FontNavBold    = new Font(_ff, 11f, FontStyle.Bold);
        public static readonly Font FontSection    = new Font(_ff, 9.5f, FontStyle.Bold);
        public static readonly Font FontLogo       = new Font(_ff, 13.5f, FontStyle.Bold);
        public static readonly Font FontPageTitle  = new Font(_ff, 15.5f, FontStyle.Bold);
        public static readonly Font FontPageSubtitle = new Font(_ff, 10.5f);
        // CellFormatting & 迴圈中的共享字體（頻繁呼叫，必須共享避免 GDI 洩漏）
        public static readonly Font FontCell9Bold  = new Font(_ff,  9f, FontStyle.Bold);
        public static readonly Font FontCell9      = new Font(_ff,  9f);
        public static readonly Font FontCell95     = new Font(_ff,  9.5f);
        public static readonly Font FontCell11     = new Font(_ff, 11f);
        public static readonly Font FontSmallBold  = new Font(_ff,  8.5f, FontStyle.Bold);
        public static readonly Font FontXSmall    = new Font(_ff,  8.5f);
        public static readonly Font FontTiny       = new Font(_ff,  7.5f);

        // ── 基本控制項工廠 ──────────────────────────────────────

        /// <summary>減少 Panel／Form 等重繪閃爍（DataGridView 亦適用）。DoubleBuffered 為 protected，需反射設定。</summary>
        public static void EnableSmoothPaint(Control c)
        {
            if (c == null) return;
            try
            {
                typeof(Control).InvokeMember(
                    "DoubleBuffered",
                    BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                    null, c, new object[] { true });
            }
            catch
            {
                // 極少數宿主環境下略過
            }
        }

        /// <summary>Soft UI 風格按鈕：淺灰底 + 淺邊框模擬凸起</summary>
        public static Button MakeButton(string text, Color bg, Color fg, int w = 120, int h = 44)
        {
            var btn = new Button
            {
                Text      = text,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font      = FontBody,
                Size      = new Size(w, h),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            // 深色主題：藍灰邊框（與網頁邊框色一致）
            btn.FlatAppearance.BorderColor        = Border;
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.08f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.05f);
            return btn;
        }

        /// <summary>Apple 風格主要按鈕（藍底白字，圓角感）</summary>
        public static Button MakePrimaryButton(string text, int w = 120, int h = 44)
            => MakeButton(text, AccentBlue, Color.White, w, h);

        /// <summary>次要按鈕（Soft UI 灰底）</summary>
        public static Button MakeSecondaryButton(string text, int w = 120, int h = 44)
            => MakeButton(text, BgLight, TextPrimary, w, h);

        public static TextBox MakeTextBox(int w = 200)
        {
            return new TextBox
            {
                BackColor   = BgLight,
                ForeColor   = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = FontBody,
                Width       = w
            };
        }

        public static Label MakeLabel(string text, Color? color = null)
        {
            return new Label
            {
                Text      = text,
                ForeColor = color ?? TextPrimary,
                Font      = FontBody,
                AutoSize  = true
            };
        }

        // 每個 DGV 對應一個 ToolTip（在 DGV Dispose 時同步 Dispose，避免 ObjectDisposedException）

        // ── DataGridView 深色樣式 ────────────────────────────────
        const string StyledDgvTag = "gmtool_theme_dgv";

        /// <summary>Hub 切換後對整棵控制項樹套用表格／頁籤疏朗樣式（可重複呼叫）。</summary>
        public static void ApplyComfortableControls(Control root)
        {
            if (root == null || root.IsDisposed) return;
            if (root is DataGridView dgv)
                StyleDataGridView(dgv);
            else if (root is TabControl tc)
                StyleTabControl(tc);
            foreach (Control child in root.Controls)
                ApplyComfortableControls(child);
        }

        public static void StyleDataGridView(DataGridView dgv)
        {
            if (dgv == null || dgv.IsDisposed) return;
            if (dgv.Tag as string == StyledDgvTag) return;
            dgv.Tag = StyledDgvTag;

            // 每個 DGV 一個 ToolTip，跟 DGV 生命週期綁定，避免共享 ToolTip 在 DGV 已 Dispose 後仍嘗試 Hide()
            var copyTip = new ToolTip { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 1400 };
            dgv.Disposed += (s, e) => { try { copyTip.Dispose(); } catch { } };
            // 欄位加入時自動啟用「點標題排序」（僅文字欄，按鈕欄不影響）
            dgv.ColumnAdded += (s, e) =>
            {
                if (e.Column is DataGridViewTextBoxColumn)
                {
                    e.Column.SortMode   = DataGridViewColumnSortMode.Automatic;
                    e.Column.ToolTipText = "↑↓ 點擊標題可排序，再次點擊反向";
                }
            };
            dgv.BackgroundColor           = BgPage;
            dgv.GridColor                 = Border;
            dgv.BorderStyle               = BorderStyle.None;
            dgv.CellBorderStyle           = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible         = false;
            dgv.AutoSizeColumnsMode       = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.AllowUserToAddRows        = false;
            dgv.AllowUserToDeleteRows     = false;
            dgv.ReadOnly                  = false;
            dgv.SelectionMode             = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect               = true;   // 允許多選以便複製多列
            dgv.EnableHeadersVisualStyles = false;

            // ── 啟用鍵盤 Ctrl+C 複製 ──────────────────────────────────
            dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;

            // ── 右鍵「複製」快捷選單 ──────────────────────────────────
            var ctxMenu = new ContextMenuStrip
            {
                BackColor = BgCard,
                ForeColor = TextPrimary,
                RenderMode = ToolStripRenderMode.System
            };
            var copyCell = new ToolStripMenuItem("📋  複製此格內容");
            copyCell.Font = FontSmall;
            copyCell.Click += (s, e) =>
            {
                if (dgv.CurrentCell?.Value is string sv && sv.Length > 0)
                    Clipboard.SetText(sv);
                else if (dgv.CurrentCell?.Value != null)
                    Clipboard.SetText(dgv.CurrentCell.Value.ToString() ?? "");
            };
            var copyRow = new ToolStripMenuItem("📄  複製整列（Tab 分隔）");
            copyRow.Font = FontSmall;
            copyRow.Click += (s, e) =>
            {
                if (dgv.CurrentRow == null) return;
                var parts = new System.Collections.Generic.List<string>();
                foreach (DataGridViewCell cell in dgv.CurrentRow.Cells)
                    if (cell.Value != null) parts.Add(cell.Value.ToString() ?? "");
                if (parts.Count > 0) Clipboard.SetText(string.Join("\t", parts));
            };
            ctxMenu.Items.AddRange(new ToolStripItem[] { copyCell, copyRow });

            // 右鍵先選中目標列再顯示選單
            dgv.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var hit = dgv.HitTest(e.X, e.Y);
                if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0)
                {
                    dgv.CurrentCell = dgv.Rows[hit.RowIndex].Cells[hit.ColumnIndex];
                    dgv.Rows[hit.RowIndex].Selected = true;
                }
            };
            dgv.ContextMenuStrip = ctxMenu;

            // ── 雙擊複製格內容（顯示提示 tooltip）────────────────────
            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                // 勾選器 DGV 不做複製（避免複製 True/False）
                if (dgv.Tag as string == "picker_no_copy") return;
                var cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
                string text = cell.Value?.ToString() ?? "";
                if (string.IsNullOrEmpty(text)) return;
                Clipboard.SetText(text);
                // 短暫顯示複製提示（使用與 DGV 生命週期綁定的 ToolTip）
                if (dgv.IsDisposed) return;
                try
                {
                    var pt = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                    copyTip.Show($"✓ 已複製：{(text.Length > 30 ? text[..30] + "…" : text)}",
                        dgv, pt.X, pt.Y - 22, 1400);
                }
                catch (ObjectDisposedException) { }
            };

            // 資料列
            dgv.DefaultCellStyle.BackColor          = BgCard;
            dgv.DefaultCellStyle.ForeColor          = TextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb( 32,  92, 168);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Font               = FontBody;
            dgv.DefaultCellStyle.Padding            = new Padding(16, 10, 16, 10);

            dgv.AlternatingRowsDefaultCellStyle.BackColor          = BgMid;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor          = TextPrimary;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb( 36, 105, 188);
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.Padding            = new Padding(16, 10, 16, 10);

            // 欄位標題列
            dgv.ColumnHeadersDefaultCellStyle.BackColor = BgDark;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            dgv.ColumnHeadersDefaultCellStyle.Font      = FontSmall;
            dgv.ColumnHeadersDefaultCellStyle.Padding   = new Padding(14, 10, 14, 10);
            dgv.ColumnHeadersHeight                     = GridHeaderHeight;
            dgv.RowTemplate.Height                      = GridRowHeight;

            EnableSmoothPaint(dgv);
        }

        /// <summary>嵌入對話框的表格：沿用全站樣式，略收表頭／列高與內距以適合彈窗。</summary>
        public static void StyleDataGridViewDialog(DataGridView dgv)
        {
            StyleDataGridView(dgv);
            dgv.ColumnHeadersHeight = 44;
            dgv.RowTemplate.Height  = GridRowHeightCompact;
            dgv.DefaultCellStyle.Padding = new Padding(14, 9, 14, 9);
            dgv.AlternatingRowsDefaultCellStyle.Padding = new Padding(14, 9, 14, 9);
        }

        public static string FontFamily => _ff;

        /// <summary>是／否確認；預設焦點在「否」降低誤觸（destructive 操作請用此）</summary>
        public static bool Confirm(string message, string title = "確認", bool defaultButtonNo = true)
        {
            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                defaultButtonNo ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1) == DialogResult.Yes;
        }

        /// <summary>WinForms TabControl 預設標籤在深色底會變黑字；改為自繪可讀標籤。</summary>
        public static void StyleTabControl(TabControl tc)
        {
            if (tc == null) return;
            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            tc.ItemSize = new Size(Math.Max(120, tc.ItemSize.Width), 42);
            tc.SizeMode = TabSizeMode.Fixed;
            tc.BackColor = BgPage;
            tc.ForeColor = TextPrimary;
            tc.Padding = new Point(14, 8);

            void OnDrawItem(object? sender, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
                var r = e.Bounds;
                bool sel = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (var br = new SolidBrush(sel ? Color.FromArgb(40, 75, 130) : BgMid))
                    e.Graphics.FillRectangle(br, r);
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                var txt = tc.TabPages[e.Index].Text;
                TextRenderer.DrawText(e.Graphics, txt, FontNav,
                    Rectangle.Inflate(r, -6, -3),
                    sel ? Color.White : TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            tc.DrawItem -= OnDrawItem;
            tc.DrawItem += OnDrawItem;
        }

        // ── 原生控件深色樣式 ─────────────────────────────────────

        /// <summary>套用深色主題到 ComboBox（背景/前景/平面邊框）</summary>
        public static void StyleComboBox(ComboBox cb)
        {
            cb.BackColor   = BgLight;
            cb.ForeColor   = TextPrimary;
            cb.FlatStyle   = FlatStyle.Flat;
            cb.Font        = FontBody;
        }

        /// <summary>套用深色主題到 NumericUpDown</summary>
        public static void StyleNumericUpDown(NumericUpDown nud)
        {
            nud.BackColor  = BgLight;
            nud.ForeColor  = TextPrimary;
            nud.BorderStyle = BorderStyle.FixedSingle;
            nud.Font       = FontBody;
        }

        /// <summary>套用深色主題到 CheckBox</summary>
        public static void StyleCheckBox(CheckBox cb, Color? fg = null)
        {
            cb.ForeColor   = fg ?? TextPrimary;
            cb.BackColor   = Color.Transparent;
            cb.FlatStyle   = FlatStyle.Flat;
            cb.Font        = FontBody;
            cb.Cursor      = Cursors.Hand;
        }

        /// <summary>套用深色主題到 RadioButton</summary>
        public static void StyleRadioButton(RadioButton rb, Color? fg = null)
        {
            rb.ForeColor   = fg ?? TextPrimary;
            rb.BackColor   = Color.Transparent;
            rb.FlatStyle   = FlatStyle.Flat;
            rb.Font        = FontBody;
            rb.Cursor      = Cursors.Hand;
        }

        /// <summary>
        /// 單選改為「分段按鈕」外觀：已選取有明顯底色，避免系統圓點在深色底對比過低、看不出選了哪一項。
        /// </summary>
        public static void StyleRadioButtonSegment(RadioButton rb, int widthPx, Color? fg = null)
        {
            rb.Appearance = Appearance.Button;
            rb.FlatStyle  = FlatStyle.Flat;
            rb.UseVisualStyleBackColor = false;
            rb.AutoSize   = false;
            rb.Width      = Math.Max(48, widthPx);
            rb.Height     = 36;
            rb.TextAlign  = ContentAlignment.MiddleLeft;
            rb.Padding    = new Padding(12, 0, 8, 0);
            rb.Font       = FontBody;
            rb.Cursor     = Cursors.Hand;
            rb.ForeColor  = fg ?? TextPrimary;
            rb.BackColor  = BgMid;
            rb.FlatAppearance.BorderSize         = 1;
            rb.FlatAppearance.BorderColor        = Border;
            rb.FlatAppearance.MouseOverBackColor = BgLight;
            rb.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 50, 88);
            rb.FlatAppearance.CheckedBackColor   = Color.FromArgb(36, 58, 96);
        }

        /// <summary>Soft UI 次要按鈕（取消/清除類）</summary>
        public static Button MakeGhostButton(string text, int w = 96, int h = 40)
        {
            var btn = new Button
            {
                Text      = text,
                BackColor = BgLight,
                ForeColor = TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font      = FontBody,
                Size      = new Size(w, h),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btn.FlatAppearance.BorderColor        = Border;
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(BgLight, 0.12f);
            return btn;
        }

        /// <summary>危險按鈕（刪除/封號類）</summary>
        public static Button MakeDangerButton(string text, int w = 120, int h = 40)
            => MakeButton(text, AccentRed, Color.White, w, h);

        // ── 數值感知排序（供各歷史表單使用）────────────────────────
        /// <summary>
        /// 為已填入資料的 DataGridView 添加「點標題列排序」功能。
        /// numericColumnNames: 需要以數值（而非字串）比較的欄位名稱集合。
        /// </summary>
        public static void AddNumericAwareSort(DataGridView dgv, params string[] numericColumnNames)
        {
            var numericSet = new System.Collections.Generic.HashSet<string>(numericColumnNames);
            bool sortAsc   = true;
            int  sortColIdx = -1;

            dgv.ColumnHeaderMouseClick += (sender, e) =>
            {
                var dg  = (DataGridView)sender;
                int ci  = e.ColumnIndex;
                if (ci < 0 || dg.Columns[ci] is DataGridViewButtonColumn) return;

                string colName = dg.Columns[ci].Name;
                sortAsc   = (sortColIdx == ci) ? !sortAsc : true;
                sortColIdx = ci;

                // 收集所有列的（排序鍵, 列）對
                var rows = new System.Collections.Generic.List<(string key, double numKey, DataGridViewRow row)>();
                foreach (DataGridViewRow row in dg.Rows)
                {
                    if (row.IsNewRow) continue;
                    string raw = row.Cells[ci].Value?.ToString() ?? "";
                    // 嘗試從文字中擷取數字（去掉千分位逗號和非數字前綴）
                    string digits = System.Text.RegularExpressions.Regex.Replace(raw, @"[^0-9.\-]", "");
                    double num    = double.TryParse(digits, out double d) ? d : 0;
                    rows.Add((raw, num, row));
                }

                // 排序
                rows.Sort((a, b) =>
                {
                    if (numericSet.Contains(colName))
                        return sortAsc ? a.numKey.CompareTo(b.numKey) : b.numKey.CompareTo(a.numKey);
                    return sortAsc
                        ? string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase)
                        : string.Compare(b.key, a.key, StringComparison.OrdinalIgnoreCase);
                });

                // 重排列順序
                dg.SuspendLayout();
                for (int i = 0; i < rows.Count; i++)
                    dg.Rows.Remove(rows[i].row);
                foreach (var (_, _, row) in rows)
                    dg.Rows.Add(row);
                dg.ResumeLayout();

                // 更新排序箭頭
                foreach (DataGridViewColumn col in dg.Columns)
                    col.HeaderCell.SortGlyphDirection = SortOrder.None;
                dg.Columns[ci].HeaderCell.SortGlyphDirection =
                    sortAsc ? SortOrder.Ascending : SortOrder.Descending;
            };
        }

        // ══════════════════════════════════════════════════════════
        // 郵件範本按鈕（可套用/儲存範本，供三個發送介面共用）
        // ══════════════════════════════════════════════════════════
        /// <summary>建立「📋 範本 ▾」按鈕，點擊彈出選單套用/儲存範本。可選：儲存/載入購物車</summary>
        public static Button MakeTemplateButton(TextBox titleBox, TextBox contentBox,
            Func<List<MailTemplateCartItem>> getCart = null,
            Action<MailTemplate> onApplyTemplate = null)
        {
            var btn = new Button
            {
                Text     = "📋 範本 ▾",
                Size     = new Size(140, 40),
                BackColor = Color.FromArgb(50, 80, 130),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = FontBody,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 120, 180);

            btn.Click += (s, e) =>
            {
                var menu = new ContextMenuStrip
                {
                    BackColor = BgCard,
                    ForeColor = TextPrimary,
                    Font = FontBody,
                    Padding = new Padding(8, 6, 8, 6),
                    ShowImageMargin = false,
                    MinimumSize = new Size(260, 0)
                };

                // ── 標題 ──
                var hdr = new ToolStripLabel("  📋  郵件範本")
                {
                    ForeColor = AccentBlue,
                    Font      = FontSection
                };
                menu.Items.Add(hdr);
                menu.Items.Add(new ToolStripSeparator());

                var templates = TemplateManager.Instance.Templates;
                if (templates.Count == 0)
                {
                    var empty = menu.Items.Add("  （尚無範本，請先儲存）");
                    empty.Enabled = false;
                }
                else
                {
                    foreach (var t in templates)
                    {
                        var localT = t;
                        var item = (ToolStripMenuItem)menu.Items.Add($"  {localT.Name}");
                        item.ToolTipText = $"標題：{localT.Buff1}\n內容：{localT.Buff2}";
                        item.ForeColor = TextPrimary;
                        item.Click += (_, __) =>
                        {
                            if (!string.IsNullOrEmpty(localT.Buff1)) titleBox.Text = localT.Buff1;
                            if (!string.IsNullOrEmpty(localT.Buff2)) contentBox.Text = localT.Buff2;
                            onApplyTemplate?.Invoke(localT);
                        };
                    }
                    menu.Items.Add(new ToolStripSeparator());
                }

                // ── 儲存目前 ──
                var saveItem = menu.Items.Add("💾  儲存目前標題/內容為範本…");
                saveItem.Click += (_, __) =>
                {
                    var dlg = new Form
                    {
                        Text            = "儲存郵件範本",
                        Size            = new Size(380, 175),
                        StartPosition   = FormStartPosition.CenterParent,
                        BackColor       = BgCard,
                        ForeColor       = TextPrimary,
                        FormBorderStyle = FormBorderStyle.FixedDialog,
                        MaximizeBox     = false,
                        MinimizeBox     = false,
                        Font            = FontBody
                    };
                    dlg.Controls.Add(new Label
                    {
                        Text = "範本名稱：", ForeColor = TextSecondary,
                        AutoSize = true, Location = new Point(14, 16)
                    });
                    var txtName = new TextBox
                    {
                        Location        = new Point(14, 36), Width = 340,
                        BackColor       = BgLight, ForeColor = TextPrimary,
                        PlaceholderText = "例如：活動獎勵、VIP禮包、系統通知…"
                    };
                    var preview = new Label
                    {
                        Text      = $"標題：{titleBox.Text.Trim()}　內容：{contentBox.Text.Trim()}",
                        ForeColor = TextMuted, Font = FontSmall,
                        AutoSize  = false, Size = new Size(340, 18),
                        Location  = new Point(14, 68), AutoEllipsis = true
                    };
                    var btnOk  = MakePrimaryButton("✓ 儲存", 80, 28);
                    btnOk.Location = new Point(258, 96);
                    var btnNo  = MakeButton("取消", BgMid, TextMuted, 70, 28);
                    btnNo.Location = new Point(180, 96);
                    btnOk.Click += (_, __) =>
                    {
                        string name = txtName.Text.Trim();
                        if (string.IsNullOrEmpty(name))
                        { MessageBox.Show("請輸入範本名稱", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                        var t = new MailTemplate
                        {
                            Name      = name,
                            Buff1     = titleBox.Text.Trim(),
                            Buff2     = contentBox.Text.Trim(),
                            CreatedAt = DateTime.Now
                        };
                        if (getCart != null)
                        {
                            try { t.Cart = getCart() ?? new List<MailTemplateCartItem>(); }
                            catch { t.Cart = new List<MailTemplateCartItem>(); }
                        }
                        TemplateManager.Instance.Add(t);
                        MessageBox.Show($"✅ 範本「{name}」已儲存！", "儲存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        dlg.Close();
                    };
                    btnNo.Click += (_, __) => dlg.Close();
                    dlg.Controls.AddRange(new Control[] { txtName, preview, btnOk, btnNo });
                    dlg.ShowDialog(btn.FindForm());
                };

                // ── 管理/刪除 ──
                if (templates.Count > 0)
                {
                    var manageItem = menu.Items.Add("🗑  管理 / 刪除範本…");
                    manageItem.Click += (_, __) => ShowTemplateManager(btn.FindForm());
                }

                // 選單在按鈕右側展開，避免被左側面板裁切或遮住
                var pt = btn.Parent?.PointToScreen(new Point(btn.Right, btn.Top)) ?? btn.PointToScreen(new Point(btn.Width, 0));
                if (pt.X + 220 > Screen.FromControl(btn).WorkingArea.Right)
                    pt = btn.Parent?.PointToScreen(new Point(btn.Left, btn.Bottom)) ?? btn.PointToScreen(new Point(0, btn.Height));
                menu.Show(pt);
            };

            return btn;
        }

        // ══════════════════════════════════════════════════════════
        // 通用輸入對話框
        // ══════════════════════════════════════════════════════════
        /// <summary>顯示文字輸入對話框，返回輸入值；取消則返回 null</summary>
        public static string ShowInputDialog(string title, string prompt, string defaultValue = "", Form owner = null)
        {
            var dlg = new Form
            {
                Text            = title,
                Size            = new Size(400, 160),
                StartPosition   = FormStartPosition.CenterParent,
                BackColor       = BgCard,
                ForeColor       = TextPrimary,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                Font            = FontBody
            };
            dlg.Controls.Add(new Label
            {
                Text = prompt, ForeColor = TextSecondary,
                AutoSize = false, Size = new Size(370, 36),
                Location = new Point(12, 12)
            });
            var txt = new TextBox
            {
                Text = defaultValue, Location = new Point(12, 52), Width = 370,
                BackColor = BgLight, ForeColor = TextPrimary
            };
            var btnOk = MakePrimaryButton("確定", 80, 28);
            btnOk.Location = new Point(302, 86);
            var btnNo = MakeButton("取消", BgMid, TextMuted, 70, 28);
            btnNo.Location = new Point(224, 86);
            string result = null;
            btnOk.Click += (_, __) => { result = txt.Text; dlg.Close(); };
            btnNo.Click += (_, __) => dlg.Close();
            txt.KeyDown += (_, e)  => { if (e.KeyCode == Keys.Enter) { result = txt.Text; dlg.Close(); } };
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnNo;
            dlg.Controls.AddRange(new Control[] { txt, btnOk, btnNo });
            dlg.ShowDialog(owner);
            return result;
        }

        /// <summary>與 GM 網頁版同步範本（下載／上傳至伺服器 mail_templates）</summary>
        private static void ShowWebSyncDialog(Form parent, Action refreshDgv)
        {
            var dlg = new Form
            {
                Text = "與 GM 網頁版同步範本",
                Size = new Size(440, 300),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = BgCard,
                ForeColor = TextPrimary,
                Font = FontBody,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
            };
            var lblBase = new Label { Text = "伺服器網址（不需結尾 /）", Location = new Point(12, 12), ForeColor = TextSecondary, AutoSize = true };
            var txtBase = new TextBox { Text = "https://gm.sodasa.org", Location = new Point(12, 32), Width = 400, BackColor = BgLight, ForeColor = TextPrimary };
            var lblUser = new Label { Text = "GM 帳號", Location = new Point(12, 62), ForeColor = TextSecondary, AutoSize = true };
            var txtUser = new TextBox { Location = new Point(12, 82), Width = 400, BackColor = BgLight, ForeColor = TextPrimary };
            var lblPass = new Label { Text = "GM 密碼", Location = new Point(12, 112), ForeColor = TextSecondary, AutoSize = true };
            var txtPass = new TextBox { Location = new Point(12, 132), Width = 400, PasswordChar = '*', BackColor = BgLight, ForeColor = TextPrimary };
            var lblHint = new Label
            {
                Text = "🔄 一鍵同步：合併伺服器＋本機（網頁優先），再寫回兩端\r\n⬇ 下載：只覆蓋本機　⬆ 上傳：只覆蓋網頁（整包取代）",
                Location = new Point(12, 160),
                ForeColor = TextMuted,
                Font = FontSmall,
                Size = new Size(400, 44),
            };
            var btnMerge = MakePrimaryButton("🔄 一鍵同步", 120, 32);
            btnMerge.Location = new Point(12, 218);
            var btnDown = MakeButton("⬇ 僅下載", AccentBlue, Color.White, 88, 32);
            btnDown.Location = new Point(138, 218);
            var btnUp = MakeButton("⬆ 僅上傳", AccentGreen, Color.White, 88, 32);
            btnUp.Location = new Point(232, 218);
            var btnClose = MakeButton("關閉", BgMid, TextMuted, 72, 32);
            btnClose.Location = new Point(326, 218);

            async void OnMerge(object s, EventArgs e)
            {
                try
                {
                    var u = MailTemplateWebSync.NormalizeBaseUrl(txtBase.Text);
                    var tok = await MailTemplateWebSync.LoginAsync(u, txtUser.Text?.Trim(), txtPass.Text);
                    if (string.IsNullOrEmpty(tok))
                    {
                        MessageBox.Show("登入失敗，請確認帳號密碼與伺服器網址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (MessageBox.Show(
                            "一鍵同步會：\n" +
                            "1）從伺服器讀取網頁範本\n" +
                            "2）合併「僅本機有」的範本\n" +
                            "3）整包寫回本機與網頁（兩端一致）\n\n" +
                            "（已存在於網頁的範本以伺服器內容為準）\n\n確定執行？",
                            "一鍵同步", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    await MailTemplateWebSync.SyncMergeAsync(u, tok);
                    refreshDgv();
                    MessageBox.Show("一鍵同步完成，本機 templates.json 與網頁範例已對齊。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dlg.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("一鍵同步失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            async void OnDownload(object s, EventArgs e)
            {
                try
                {
                    var u = MailTemplateWebSync.NormalizeBaseUrl(txtBase.Text);
                    var tok = await MailTemplateWebSync.LoginAsync(u, txtUser.Text?.Trim(), txtPass.Text);
                    if (string.IsNullOrEmpty(tok))
                    {
                        MessageBox.Show("登入失敗，請確認帳號密碼與伺服器網址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var list = await MailTemplateWebSync.DownloadTemplatesAsync(u, tok);
                    if (MessageBox.Show($"即將以伺服器上的 {list.Count} 筆範本取代本機 templates.json。\n\n確定？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    TemplateManager.Instance.Save(list);
                    refreshDgv();
                    MessageBox.Show("已從網頁伺服器下載範本（僅更新本機）。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dlg.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("下載失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            async void OnUpload(object s, EventArgs e)
            {
                try
                {
                    var u = MailTemplateWebSync.NormalizeBaseUrl(txtBase.Text);
                    var tok = await MailTemplateWebSync.LoginAsync(u, txtUser.Text?.Trim(), txtPass.Text);
                    if (string.IsNullOrEmpty(tok))
                    {
                        MessageBox.Show("登入失敗，請確認帳號密碼與伺服器網址。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var list = TemplateManager.Instance.Templates.ToList();
                    if (MessageBox.Show($"即將上傳 {list.Count} 筆範本到伺服器，覆寫網頁版現有範本。\n\n確定？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    await MailTemplateWebSync.UploadTemplatesAsync(u, tok, list);
                    refreshDgv();
                    MessageBox.Show("已上傳至網頁伺服器（僅覆寫網頁）。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dlg.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("上傳失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            btnMerge.Click += OnMerge;
            btnDown.Click += OnDownload;
            btnUp.Click += OnUpload;
            btnClose.Click += (_, __) => dlg.Close();
            dlg.Controls.AddRange(new Control[] { lblBase, txtBase, lblUser, txtUser, lblPass, txtPass, lblHint, btnMerge, btnDown, btnUp, btnClose });
            dlg.ShowDialog(parent);
        }

        private static void ShowTemplateManager(Form parent)
        {
            var dlg = new Form
            {
                Text            = "管理郵件範本",
                Size            = new Size(520, 380),
                StartPosition   = FormStartPosition.CenterParent,
                BackColor       = BgCard,
                ForeColor       = TextPrimary,
                FormBorderStyle = FormBorderStyle.Sizable,
                Font            = FontBody
            };
            var dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = true,
                RowTemplate           = { Height = 26 },
                ColumnHeadersHeight   = 28,
            };
            StyleDataGridView(dgv);
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",  HeaderText = "範本名稱",   Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTitle", HeaderText = "標題",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cBody",  HeaderText = "內容",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDate",  HeaderText = "建立時間",  Width = 130, SortMode = DataGridViewColumnSortMode.NotSortable });

            void RefreshDgv()
            {
                dgv.Rows.Clear();
                foreach (var t in TemplateManager.Instance.Templates)
                    dgv.Rows.Add(t.Name, t.Buff1, t.Buff2, t.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            }
            RefreshDgv();

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = BgDark };
            var btnWeb = MakeButton("🌐 與網頁同步…", AccentBlue, Color.White, 130, 30);
            btnWeb.Location = new Point(230, 8);
            btnWeb.Click += (_, __) => ShowWebSyncDialog(parent, RefreshDgv);
            var btnEdit = MakeButton("✎  編輯選取", AccentBlue, Color.White, 100, 30);
            btnEdit.Location = new Point(12, 8);
            btnEdit.Click   += (_, __) =>
            {
                int idx = dgv.CurrentRow?.Index ?? -1;
                if (idx < 0) return;
                var list = TemplateManager.Instance.Templates.ToList();
                if (idx >= list.Count) return;
                var old = list[idx];
                var editDlg = new Form
                {
                    Text = "編輯範例",
                    Size = new Size(420, 220),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = BgCard, ForeColor = TextPrimary, Font = FontBody,
                    FormBorderStyle = FormBorderStyle.FixedDialog
                };
                var lblName  = new Label { Text = "範本名稱：", Location = new Point(12, 14), ForeColor = TextSecondary, Font = FontBody };
                var txtName  = new TextBox { Location = new Point(100, 12), Width = 300, Text = old.Name, BackColor = BgLight, ForeColor = TextPrimary };
                var lblTitle = new Label { Text = "標題：",      Location = new Point(12, 46), ForeColor = TextSecondary, Font = FontBody };
                var txtTitle = new TextBox { Location = new Point(100, 44), Width = 300, Text = old.Buff1 ?? "", BackColor = BgLight, ForeColor = TextPrimary };
                var lblBody  = new Label { Text = "內容：",      Location = new Point(12, 78), ForeColor = TextSecondary, Font = FontBody };
                var txtBody  = new TextBox { Location = new Point(100, 76), Width = 300, Text = old.Buff2 ?? "", BackColor = BgLight, ForeColor = TextPrimary };
                var btnOk2   = MakePrimaryButton("儲存", 80, 28);
                btnOk2.Location = new Point(220, 118);
                var btnNo2   = MakeButton("取消", BgMid, TextMuted, 70, 28);
                btnNo2.Location = new Point(130, 118);
                btnOk2.Click += (_, __) =>
                {
                    if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("請輸入範本名稱", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    var updated = new MailTemplate
                    {
                        WebId = old.WebId ?? "",
                        Name = txtName.Text.Trim(),
                        Buff1 = txtTitle.Text?.Trim() ?? "",
                        Buff2 = txtBody.Text?.Trim() ?? "",
                        Cart = old.Cart ?? new List<MailTemplateCartItem>(),
                        CreatedAt = old.CreatedAt
                    };
                    TemplateManager.Instance.Replace(old, updated);
                    editDlg.Close();
                    RefreshDgv();
                };
                btnNo2.Click += (_, __) => editDlg.Close();
                editDlg.Controls.AddRange(new Control[] { lblName, txtName, lblTitle, txtTitle, lblBody, txtBody, btnOk2, btnNo2 });
                editDlg.ShowDialog(parent);
            };
            var btnDel = MakeButton("🗑  刪除選取", AccentRed, Color.White, 110, 30);
            btnDel.Location = new Point(118, 8);
            btnDel.Click   += (_, __) =>
            {
                int idx = dgv.CurrentRow?.Index ?? -1;
                if (idx < 0) return;
                var list = TemplateManager.Instance.Templates.ToList();
                if (idx >= list.Count) return;
                if (MessageBox.Show($"確定刪除範本「{list[idx].Name}」？",
                    "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                TemplateManager.Instance.Remove(list[idx]);
                RefreshDgv();
            };
            bottom.Controls.Add(btnWeb);
            bottom.Controls.Add(btnEdit);
            bottom.Controls.Add(btnDel);
            bottom.Controls.Add(new Label
            {
                Text = "本機 templates.json · 可按「與網頁同步」雙向同步",
                ForeColor = TextMuted, Font = FontSmall,
                AutoSize = true, Location = new Point(368, 14)
            });

            dlg.Controls.Add(dgv);
            dlg.Controls.Add(bottom);
            dlg.ShowDialog(parent);
        }

        /// <summary>STEP 標題列（色條 + 粗體字）</summary>
        public static Label MakeStepLabel(string step, string title, Color accent, int x, int y)
        {
            return new Label
            {
                Text      = $"  {step}   {title}",
                ForeColor = accent,
                Font      = new Font(FontFamily, 10f, FontStyle.Bold),
                AutoSize  = false,
                Size      = new Size(600, 22),
                Location  = new Point(x, y),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        /// <summary>Hub 分頁頂部標題列（固定高度、左右留白）</summary>
        public static Panel MakeHubTopBar(string title, Color accent, out Panel innerHost)
        {
            var bar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = ToolbarHeight,
                BackColor = BgDialogHeader,
                Padding   = new Padding(UiPadLg, GapSm, UiPadLg, GapSm)
            };
            bar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Border });
            bar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = accent });
            bar.Controls.Add(new Label
            {
                Text      = title,
                ForeColor = accent,
                Font      = FontHeader,
                AutoSize  = true,
                Location  = new Point(UiPadLg + 8, 10),
                BackColor = Color.Transparent
            });
            innerHost = new Panel
            {
                Dock      = DockStyle.Right,
                Width     = 520,
                BackColor = Color.Transparent,
                Padding   = new Padding(0, 8, 0, 0)
            };
            bar.Controls.Add(innerHost);
            EnableSmoothPaint(bar);
            return bar;
        }

        /// <summary>底部操作列（確認／取消等）</summary>
        public static Panel MakeFooterBar(int height = 56)
        {
            var footer = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = height,
                BackColor = BgDialogHeader,
                Padding   = new Padding(UiPadLg, GapSm, UiPadLg, GapSm)
            };
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Border });
            EnableSmoothPaint(footer);
            return footer;
        }

        /// <summary>內嵌輸入區塊（略深背景 + 左側色條）</summary>
        public static Panel MakeInsetBox(int x, int y, int w, int h, Color accentLeft)
        {
            var box = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(w, h),
                BackColor = BgInset
            };
            EnableSmoothPaint(box);
            box.Controls.Add(new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(4, h),
                BackColor = accentLeft
            });
            return box;
        }
    }
}
