using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        // ── 背景層次 ─────────────────────────────────────────────
        //   深色 Header / Sidebar → 中性頁面底 → 白色卡片 → 輸入框
        public static readonly Color BgDark    = Color.FromArgb( 28,  28,  30); // Header / Sidebar / 狀態列
        public static readonly Color BgMid     = Color.FromArgb( 40,  42,  54); // 次深（SplitContainer 背板）
        public static readonly Color BgCard    = Color.FromArgb( 50,  52,  65); // 卡片 / 面板
        public static readonly Color BgPage    = Color.FromArgb( 36,  37,  50); // 頁面底色
        public static readonly Color BgLight   = Color.FromArgb( 62,  64,  78); // 輸入框 / 較淺面板
        public static readonly Color BgInput   = BgLight;
        public static readonly Color BgSidebar = Color.FromArgb( 22,  22,  26); // 側邊欄（更深）
        public static readonly Color CardBg    = BgCard;

        public static readonly Color Border    = Color.FromArgb( 68,  70,  84); // 分隔線
        public static readonly Color BorderHov = Color.FromArgb(  0, 145, 255); // focus ring

        // 強調色（飽和，在深色底上清晰可見）
        public static readonly Color AccentBlue   = Color.FromArgb(  0, 145, 255);
        public static readonly Color AccentGreen  = Color.FromArgb( 52, 199,  89);
        public static readonly Color AccentRed    = Color.FromArgb(255,  69,  58);
        public static readonly Color AccentOrange = Color.FromArgb(255, 159,  10);
        public static readonly Color AccentPurple = Color.FromArgb(191, 100, 246);

        // 文字（深色底上清晰可見）
        public static readonly Color TextPrimary   = Color.FromArgb(240, 240, 245); // 主要文字（近白）
        public static readonly Color TextSecondary = Color.FromArgb(180, 180, 190); // 次要文字
        public static readonly Color TextMuted     = Color.FromArgb(120, 120, 132); // 提示文字

        // ── 字體 ────────────────────────────────────────────────
        public static readonly Font FontTitle  = new Font(_ff, 15f,  FontStyle.Bold);
        public static readonly Font FontHeader = new Font(_ff, 11f,  FontStyle.Bold);
        public static readonly Font FontBody   = new Font(_ff, 10.5f);
        public static readonly Font FontSmall  = new Font(_ff,  9.5f);
        public static readonly Font FontMono   = new Font("Consolas", 10f);

        // ── 基本控制項工廠 ──────────────────────────────────────

        public static Button MakeButton(string text, Color bg, Color fg, int w = 110, int h = 34)
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
            btn.FlatAppearance.BorderColor        = bg;
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.15f);
            return btn;
        }

        /// <summary>Apple 風格主要按鈕（藍底白字，圓角感）</summary>
        public static Button MakePrimaryButton(string text, int w = 110, int h = 32)
            => MakeButton(text, AccentBlue, Color.White, w, h);

        /// <summary>Apple 風格次要按鈕（淺灰底深字）</summary>
        public static Button MakeSecondaryButton(string text, int w = 110, int h = 32)
            => MakeButton(text, Color.FromArgb(218, 218, 223), TextPrimary, w, h);

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
                ForeColor = color ?? TextSecondary,
                Font      = FontBody,
                AutoSize  = true
            };
        }

        // ── DataGridView 深色樣式 ────────────────────────────────
        public static void StyleDataGridView(DataGridView dgv)
        {
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
            dgv.AutoSizeColumnsMode       = DataGridViewAutoSizeColumnsMode.None;
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
                BackColor = Color.FromArgb(36, 37, 50),
                ForeColor = Color.FromArgb(220, 220, 235),
                RenderMode = ToolStripRenderMode.System
            };
            var copyCell = new ToolStripMenuItem("📋  複製此格內容");
            copyCell.Font = new Font(_ff, 9f);
            copyCell.Click += (s, e) =>
            {
                if (dgv.CurrentCell?.Value is string sv && sv.Length > 0)
                    Clipboard.SetText(sv);
                else if (dgv.CurrentCell?.Value != null)
                    Clipboard.SetText(dgv.CurrentCell.Value.ToString() ?? "");
            };
            var copyRow = new ToolStripMenuItem("📄  複製整列（Tab 分隔）");
            copyRow.Font = new Font(_ff, 9f);
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
                // 短暫顯示複製提示
                var tip = new ToolTip { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 1400 };
                var pt  = dgv.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
                tip.Show($"✓ 已複製：{(text.Length > 30 ? text[..30] + "…" : text)}",
                    dgv, pt.X, pt.Y - 22, 1400);
            };

            // 資料列
            dgv.DefaultCellStyle.BackColor          = BgCard;
            dgv.DefaultCellStyle.ForeColor          = TextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb( 0, 80, 160);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Font               = FontBody;
            dgv.DefaultCellStyle.Padding            = new Padding(8, 0, 8, 0);

            // 交錯列（稍微淺一點）
            dgv.AlternatingRowsDefaultCellStyle.BackColor          = Color.FromArgb(58, 60, 74);
            dgv.AlternatingRowsDefaultCellStyle.ForeColor          = TextPrimary;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 80, 160);

            // 欄位標題列
            dgv.ColumnHeadersDefaultCellStyle.BackColor = BgDark;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            dgv.ColumnHeadersDefaultCellStyle.Font      = new Font(_ff, 9.5f, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.Padding   = new Padding(8, 0, 8, 0);
            dgv.ColumnHeadersHeight                     = 36;
            dgv.RowTemplate.Height                      = 36;
        }

        public static string FontFamily => _ff;

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
        /// <summary>建立「📋 範本 ▾」按鈕，點擊彈出選單套用/儲存範本</summary>
        public static Button MakeTemplateButton(TextBox titleBox, TextBox contentBox)
        {
            var btn = new Button
            {
                Text     = "📋 範本 ▾",
                Width    = 100, Height = 26,
                BackColor = Color.FromArgb(50, 80, 130),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = FontSmall,
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 120, 180);

            btn.Click += (s, e) =>
            {
                var menu = new ContextMenuStrip { BackColor = BgCard, ForeColor = TextPrimary, Font = FontBody };

                // ── 標題 ──
                var hdr = new ToolStripLabel("  📋  郵件範本")
                {
                    ForeColor = AccentBlue,
                    Font      = new Font(FontFamily, 9f, FontStyle.Bold)
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
                        TemplateManager.Instance.Add(new MailTemplate
                        {
                            Name  = name,
                            Buff1 = titleBox.Text.Trim(),
                            Buff2 = contentBox.Text.Trim(),
                            CreatedAt = DateTime.Now
                        });
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

                menu.Show(btn, new Point(0, btn.Height));
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
                MultiSelect           = false,
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
            var btnDel = MakeButton("🗑  刪除選取", AccentRed, Color.White, 110, 30);
            btnDel.Location = new Point(12, 8);
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
            bottom.Controls.Add(btnDel);
            bottom.Controls.Add(new Label
            {
                Text = "提示：範本儲存在應用程式目錄的 templates.json",
                ForeColor = TextMuted, Font = FontSmall,
                AutoSize = true, Location = new Point(130, 14)
            });

            dlg.Controls.Add(dgv);
            dlg.Controls.Add(bottom);
            dlg.ShowDialog(parent);
        }
    }
}
