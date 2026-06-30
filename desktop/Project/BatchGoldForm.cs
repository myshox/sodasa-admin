using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace SQ_Email_Tools
{
    public class BatchGoldForm : Form
    {
        // ── 左側控件 ──
        private RadioButton   _rbAll, _rbOnline, _rbSearch;
        private TextBox       _txtSearch;
        private Button        _btnLoad;
        private NumericUpDown _nudAmount;
        private RadioButton   _rbAdd, _rbSub;
        private Button        _btnSend, _btnCancel;
        private ProgressBar   _progress;
        private Label         _progressLbl, _statusLbl;

        // ── 右側控件 ──
        private DataGridView  _listDgv;          // 勾選清單（可複選）
        private Label         _lblSelected;      // 已選 N / 共 M 人
        private Button        _btnSelAll, _btnSelNone, _btnSelInvert;
        private RichTextBox   _logBox;

        // ── 執行結果（供匯出用）──
        private readonly List<(string acc, string name, bool ok, string msg)> _execResults = new();

        // ── 狀態 ──
        private CancellationTokenSource _cts;
        private bool _isSending;

        public BatchGoldForm()
        {
            Text          = "💰 批量金幣修改";
            Size          = new Size(1080, 720);
            MinimumSize   = new Size(820, 560);
            Theme.ApplyHubForm(this);

            StartPosition = FormStartPosition.CenterParent;
            FormClosing  += (s, e) => { if (_isSending) e.Cancel = true; };
            BuildUI();
        }

        // ═══════════════════════════════════════════════════════
        // 整體佈局
        // ═══════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── 頂部標題列 ──────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = Theme.ToolbarHeight, BackColor = Theme.BgDialogHeader };
            header.Controls.Add(new Label
            {
                Text      = "  💰  批量金幣修改  ·  載入名單 → 勾選對象 → 選擇加／減金額 → 執行",
                ForeColor = Theme.AccentOrange, Font = Theme.FontHeader,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            });

            // ── 狀態列 ──────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _statusLbl = new Label
            {
                Text = "① 左側選範圍後按「載入玩家清單」② 右側勾選要操作的帳號 ③ 設定金額後執行",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };
            statusBar.Controls.Add(_statusLbl);

            // ── 主體：左設定 | 右勾選清單 ────────────────────────
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
                BackColor = Theme.Border, SplitterWidth = 3
            };
            split.Panel1.BackColor = Theme.BgMid;
            split.Panel2.BackColor = Theme.BgMid;
            split.HandleCreated += (_, __) =>
            {
                if (split.Width >= 760)
                    try { split.SplitterDistance = Math.Max(312, Math.Min(split.Width - 480, 380)); } catch { }
            };

            BuildLeftPanel(split.Panel1);
            BuildRightPanel(split.Panel2);

            Controls.Add(split);
            Controls.Add(statusBar);
            Controls.Add(header);
        }

        // ═══════════════════════════════════════════════════════
        // 左側：設定面板
        // ═══════════════════════════════════════════════════════
        private void BuildLeftPanel(Panel p)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            p.Controls.Add(scroll);

            int y = 16, x = 16;

            // ── STEP 1：選擇載入範圍 ──────────────────────────
            AddSection(scroll, "STEP 1 — 載入玩家範圍", ref y, x);

            _rbAll = MakeRadio(scroll, "🌐 全服（與批量發送相同）", x, ref y, true);
            _rbOnline = MakeRadio(scroll, "🟢 同批量發送「僅線上」條件", x, ref y);
            _rbSearch = MakeRadio(scroll, "🔍 依關鍵字搜尋", x, ref y);

            // 搜尋框
            var searchRow = new Panel
            {
                Location = new Point(x + 20, y), Width = 280, Height = 30,
                BackColor = Color.Transparent
            };
            _txtSearch = new TextBox
            {
                PlaceholderText = "主帳號 / 角色名 / UID",
                Location = new Point(0, 0), Width = 200, Height = 28,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Enabled = false
            };
            var btnSearchInline = Theme.MakePrimaryButton("搜尋", 62, 28);
            btnSearchInline.Location = new Point(206, 0);
            btnSearchInline.Enabled  = false;
            searchRow.Controls.Add(_txtSearch);
            searchRow.Controls.Add(btnSearchInline);
            scroll.Controls.Add(searchRow);
            y += 38;

            _rbSearch.CheckedChanged += (s, e) =>
            {
                _txtSearch.Enabled   = _rbSearch.Checked;
                btnSearchInline.Enabled = _rbSearch.Checked;
            };
            _txtSearch.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) await LoadListAsync();
            };
            btnSearchInline.Click += async (s, e) => await LoadListAsync();

            // 主載入按鈕
            _btnLoad = Theme.MakePrimaryButton("📥 載入玩家清單", 150, 34);
            _btnLoad.Location = new Point(x, y);
            _btnLoad.Click   += async (s, e) => await LoadListAsync();
            scroll.Controls.Add(_btnLoad);
            y += 50;

            // 提示
            var hint = new Label
            {
                Text = "💡 載入後可在右側清單勾選要操作的玩家",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Location = new Point(x, y), AutoSize = true
            };
            scroll.Controls.Add(hint);
            y += 30;

            // ── STEP 2：金額設定 ──────────────────────────────
            AddSection(scroll, "STEP 2 — 設定金幣金額", ref y, x);

            scroll.Controls.Add(new Label { Text = "操作：", Location = new Point(x, y), ForeColor = Theme.TextSecondary, AutoSize = true });
            y += 22;
            _rbAdd = new RadioButton { Text = "發放（增加金幣）", Location = new Point(x, y), Checked = true };
            _rbSub = new RadioButton { Text = "扣除（減少金幣）", Location = new Point(x, y + 40) };
            Theme.StyleRadioButtonSegment(_rbAdd, 290, Theme.AccentGreen);
            Theme.StyleRadioButtonSegment(_rbSub, 290, Theme.AccentRed);
            scroll.Controls.AddRange(new Control[] { _rbAdd, _rbSub });
            y += 88;

            scroll.Controls.Add(new Label { Text = "金幣數量：", Location = new Point(x, y + 4), ForeColor = Theme.TextSecondary, AutoSize = true });
            _nudAmount = new NumericUpDown
            {
                Location = new Point(x + 80, y), Width = 200, Height = 28,
                Minimum = 1, Maximum = 10_000_000, Value = 1000, Increment = 1000,
                ThousandsSeparator = true
            };
            Theme.StyleNumericUpDown(_nudAmount);
            scroll.Controls.Add(_nudAmount);
            y += 50;

            // ── STEP 3：確認執行 ──────────────────────────────
            AddSection(scroll, "STEP 3 — 確認執行", ref y, x);

            var warnPanel = new Panel { Location = new Point(x, y), Size = new Size(300, 52), BackColor = Color.FromArgb(52, 28, 12), BorderStyle = BorderStyle.FixedSingle };
            warnPanel.Controls.Add(new Label
            {
                Text = "⚠  此操作無法撤銷。\n請再次確認：右側已勾選的帳號、以及金幣加／減與數量。",
                ForeColor = Color.FromArgb(255, 210, 160), Font = Theme.FontSmall,
                Location = new Point(10, 8), Size = new Size(280, 40), AutoSize = false
            });
            scroll.Controls.Add(warnPanel);
            y += 54;

            _btnSend = Theme.MakePrimaryButton("✅ 對已勾選玩家執行", 160, 36);
            _btnSend.BackColor = Theme.AccentGreen;
            _btnSend.Location  = new Point(x, y);
            _btnSend.Click    += BtnSend_Click;
            scroll.Controls.Add(_btnSend);

            _btnCancel = Theme.MakeSecondaryButton("⛔ 停止", 80, 36);
            _btnCancel.Location = new Point(x + 170, y);
            _btnCancel.Enabled  = false;
            _btnCancel.Click   += (s, e) => _cts?.Cancel();
            scroll.Controls.Add(_btnCancel);
            y += 52;

            _progress = new ProgressBar { Location = new Point(x, y), Width = 280, Height = 16, Style = ProgressBarStyle.Continuous };
            scroll.Controls.Add(_progress);
            y += 22;

            _progressLbl = new Label { Text = "", Location = new Point(x, y), AutoSize = true, ForeColor = Theme.TextSecondary, Font = Theme.FontSmall };
            scroll.Controls.Add(_progressLbl);
            y += 30;
            scroll.AutoScrollMinSize = new Size(0, y);
        }

        // ═══════════════════════════════════════════════════════
        // 右側：可勾選玩家清單
        // ═══════════════════════════════════════════════════════
        private void BuildRightPanel(Panel p)
        {
            // ── 頂部工具列（兩列）──────────────────────────────────
            var toolbarWrap = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(20, 24, 36) };

            // 第一列：標題 + 全選操作
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(20, 24, 36), Padding = new Padding(10, 8, 10, 0) };
            var titleLbl = new Label
            {
                Text = "📋  玩家清單",
                ForeColor = Theme.AccentBlue, Font = Theme.FontHeader,
                Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft
            };
            _btnSelAll    = Theme.MakeSecondaryButton("全選",    56, 26);
            _btnSelNone   = Theme.MakeSecondaryButton("取消全選", 76, 26);
            _btnSelInvert = Theme.MakeSecondaryButton("反選",    56, 26);
            _btnSelAll.Dock    = DockStyle.Left; _btnSelAll.Margin    = new Padding(0, 0, 4, 0);
            _btnSelNone.Dock   = DockStyle.Left; _btnSelNone.Margin   = new Padding(0, 0, 4, 0);
            _btnSelInvert.Dock = DockStyle.Left; _btnSelInvert.Margin = new Padding(0, 0, 4, 0);
            _lblSelected = new Label
            {
                Text = "請先載入玩家", ForeColor = Theme.TextMuted, Font = Theme.FontBody,
                Dock = DockStyle.Right, Width = 200, TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };
            _btnSelAll.Click    += (s, e) => SetAllChecked(true);
            _btnSelNone.Click   += (s, e) => SetAllChecked(false);
            _btnSelInvert.Click += (s, e) => InvertChecked();
            toolbar.Controls.Add(titleLbl);
            toolbar.Controls.Add(_btnSelAll);
            toolbar.Controls.Add(_btnSelNone);
            toolbar.Controls.Add(_btnSelInvert);
            toolbar.Controls.Add(_lblSelected);

            // 第二列：說明 + 群組／匯出
            var toolbar2 = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(16, 20, 32), Padding = new Padding(10, 4, 10, 6) };
            var hintRight = new Label
            {
                Text = "點「角色名稱／帳號」可切換勾選　·　表頭可排序",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                AutoSize = true, TextAlign = ContentAlignment.MiddleLeft
            };
            var btnUpload  = Theme.MakeButton("📤 上傳清單", Theme.AccentBlue, Color.White, 100, 26);
            var btnSaveGrp = Theme.MakeSecondaryButton("💾 儲存群組", 92, 26);
            var btnLoadGrp = Theme.MakeSecondaryButton("📂 載入群組", 92, 26);
            var btnExport  = Theme.MakeSecondaryButton("📥 匯出 Excel", 108, 26);

            btnUpload.Click  += (s, e) => UploadAndCheckList();
            btnSaveGrp.Click += (s, e) => SaveGroup();
            btnLoadGrp.Click += (s, e) => LoadGroup();
            btnExport.Click  += (s, e) => ExportResultsExcel();

            void LayoutToolbar2()
            {
                int midY = (toolbar2.ClientSize.Height - btnSaveGrp.Height) / 2;
                int rx = toolbar2.ClientSize.Width - 10;
                foreach (var b in new[] { btnExport, btnLoadGrp, btnSaveGrp, btnUpload })
                {
                    rx -= b.Width;
                    b.Location = new Point(rx, midY);
                    rx -= 8;
                }
                hintRight.Location = new Point(10, (toolbar2.ClientSize.Height - hintRight.PreferredHeight) / 2);
                hintRight.MaximumSize = new Size(Math.Max(180, rx - 24), 0);
            }

            toolbar2.Controls.Add(hintRight);
            toolbar2.Controls.Add(btnSaveGrp);
            toolbar2.Controls.Add(btnLoadGrp);
            toolbar2.Controls.Add(btnExport);
            toolbar2.Controls.Add(btnUpload);
            toolbar2.Resize += (_, __) => LayoutToolbar2();
            LayoutToolbar2();

            toolbarWrap.Controls.Add(toolbar2);
            toolbarWrap.Controls.Add(toolbar);

            // ── 玩家清單 DGV（帶勾選框）────────────────────────
            _listDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_listDgv);
            _listDgv.ReadOnly            = false;
            _listDgv.AllowUserToAddRows  = false;
            _listDgv.MultiSelect         = true;
            _listDgv.Tag                 = "picker_no_copy";
            _listDgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _listDgv.DefaultCellStyle.Font = Theme.FontBody;

            var colChk = new DataGridViewCheckBoxColumn
            {
                Name = "cChk", HeaderText = "選取", Width = 52,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ToolTipText = "打勾的帳號才會被批次加／減金幣"
            };
            colChk.DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
            colChk.HeaderCell.Style.Alignment  = DataGridViewContentAlignment.MiddleCenter;
            var colSt = new DataGridViewTextBoxColumn
            {
                Name = "cSt", HeaderText = "在線", Width = 56, ReadOnly = true,
                ToolTipText = "載入當下是否在線（參考用）"
            };
            colSt.DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            var colName = new DataGridViewTextBoxColumn
            {
                Name = "cName", HeaderText = "角色名稱", Width = 168, ReadOnly = true,
                ToolTipText = "遊戲內顯示名稱"
            };
            var colAcc = new DataGridViewTextBoxColumn
            {
                Name = "cAcc", HeaderText = "主帳號（cdkey）", ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 160,
                ToolTipText = "資料庫帳號欄位，實際加減金幣的對象"
            };
            _listDgv.Columns.AddRange(new DataGridViewColumn[] { colChk, colSt, colName, colAcc });

            // 單擊勾選框 → 立即提交（CellContentClick 是 WinForms 勾選框的正確事件）
            _listDgv.CellContentClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (_listDgv.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
                {
                    _listDgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    UpdateSelectedCount();
                }
            };

            // 點名稱或帳號欄也切換勾選
            _listDgv.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (!(_listDgv.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn))
                    ToggleRow(e.RowIndex);
            };

            // Space 鍵 → 切換所有反白列
            _listDgv.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Space) return;
                e.Handled = true;
                var rows   = _listDgv.SelectedRows.Cast<DataGridViewRow>().ToList();
                int cnt    = rows.Count(r => r.Cells["cChk"].Value is bool b && b);
                bool setTo = cnt < rows.Count / 2.0 + 1;
                foreach (var row in rows)
                {
                    row.Cells["cChk"].Value = setTo;
                    ApplyRowStyle(row, setTo);
                }
                UpdateSelectedCount();
            };

            // 點標題「✓」→ 全選/全取消
            _listDgv.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex != 0) return;
                int total = _listDgv.Rows.Count;
                int chk   = _listDgv.Rows.Cast<DataGridViewRow>().Count(r => r.Cells["cChk"].Value is bool b && b);
                SetAllChecked(chk < total);
            };

            // ── 執行日誌 ──────────────────────────────────────
            _logBox = new RichTextBox
            {
                Dock = DockStyle.Bottom, Height = 132,
                BackColor = Color.FromArgb(22, 26, 36), ForeColor = Theme.TextSecondary,
                Font = new Font(Theme.FontFamily, 9.5f), ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "（尚無執行紀錄）\n執行後會逐筆顯示 ✓ 成功 或 ✗ 失敗；可捲動檢視。\n"
            };
            var logHdr = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = Theme.BgDark };
            logHdr.Controls.Add(new Label
            {
                Text = "  執行日誌（即時）", ForeColor = Theme.TextSecondary, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            });

            p.Controls.AddRange(new Control[] { _listDgv, logHdr, _logBox, toolbarWrap });
        }

        // ═══════════════════════════════════════════════════════
        // 載入玩家到清單
        // ═══════════════════════════════════════════════════════
        private async Task LoadListAsync()
        {
            _btnLoad.Enabled = false;
            _listDgv.Rows.Clear();
            _statusLbl.Text = "載入中…";
            try
            {
                List<PlayerInfo> players;
                if (_rbAll.Checked)
                    players = await DatabaseManager.Instance.SearchPlayersAsync("", limit: 0);
                else if (_rbOnline.Checked)
                    players = await DatabaseManager.Instance.GetOnlineTargetsMatchingBatchMailAsync();
                else
                {
                    string q = _txtSearch.Text.Trim();
                    if (string.IsNullOrWhiteSpace(q))
                    {
                        _statusLbl.Text = "請輸入搜尋關鍵字";
                        return;
                    }
                    players = await DatabaseManager.Instance.SearchPlayersAsync(q);
                }

                _listDgv.SuspendLayout();
                foreach (var pl in players)
                {
                    int ri = _listDgv.Rows.Add(true, pl.IsOnline ? "在線" : "離線", pl.OnlineName ?? "", pl.Account);
                    ApplyRowStyle(_listDgv.Rows[ri], true);
                }
                _listDgv.ResumeLayout();

                _statusLbl.Text = $"已載入 {players.Count} 位玩家（預設全選，可在右側取消勾選）";
                UpdateSelectedCount();
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
            finally { _btnLoad.Enabled = true; }
        }

        // ═══════════════════════════════════════════════════════
        // 勾選操作
        // ═══════════════════════════════════════════════════════
        private void ToggleRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _listDgv.Rows.Count) return;
            var row  = _listDgv.Rows[rowIndex];
            bool cur = row.Cells["cChk"].Value is bool b && b;
            row.Cells["cChk"].Value = !cur;
            _listDgv.InvalidateRow(rowIndex);
            ApplyRowStyle(row, !cur);
            UpdateSelectedCount();
        }

        private void SetAllChecked(bool value)
        {
            _listDgv.SuspendLayout();
            foreach (DataGridViewRow row in _listDgv.Rows)
            {
                row.Cells["cChk"].Value = value;
                ApplyRowStyle(row, value);
            }
            _listDgv.ResumeLayout();
            UpdateSelectedCount();
        }

        private void InvertChecked()
        {
            _listDgv.SuspendLayout();
            foreach (DataGridViewRow row in _listDgv.Rows)
            {
                bool cur = row.Cells["cChk"].Value is bool b && b;
                row.Cells["cChk"].Value = !cur;
                ApplyRowStyle(row, !cur);
            }
            _listDgv.ResumeLayout();
            UpdateSelectedCount();
        }

        private static void ApplyRowStyle(DataGridViewRow row, bool selected)
        {
            if (selected)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(36, 52, 44);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(230, 255, 238);
            }
            else
            {
                bool alt = row.Index >= 0 && (row.Index % 2 == 1);
                row.DefaultCellStyle.BackColor = alt ? Theme.BgMid : Theme.BgCard;
                row.DefaultCellStyle.ForeColor = Theme.TextPrimary;
            }
        }

        private void UpdateSelectedCount()
        {
            if (_listDgv.InvokeRequired) { _listDgv.Invoke(UpdateSelectedCount); return; }
            int chk   = _listDgv.Rows.Cast<DataGridViewRow>().Count(r => r.Cells["cChk"].Value is bool b && b);
            int total = _listDgv.Rows.Count;
            _lblSelected.Text      = $"已選 {chk} / 共 {total} 人";
            _lblSelected.ForeColor = chk > 0 ? Color.FromArgb(103, 194, 58) : Theme.TextMuted;
        }

        // ═══════════════════════════════════════════════════════
        // 執行
        // ═══════════════════════════════════════════════════════
        private async void BtnSend_Click(object sender, EventArgs e)
        {
            if (_isSending) return;   // 防重入：避免快速雙擊造成重複加金
            // 收集勾選帳號
            var targets = _listDgv.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["cChk"].Value is bool b && b)
                .Select(r => r.Cells["cAcc"].Value?.ToString() ?? "")
                .Where(a => !string.IsNullOrEmpty(a))
                .ToList();

            if (targets.Count == 0)
            {
                MessageBox.Show(
                    _listDgv.Rows.Count == 0
                        ? "請先點「📥 載入玩家清單」"
                        : "目前沒有勾選任何玩家，請在右側清單勾選目標",
                    "尚未選擇目標", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            long   amount = (long)_nudAmount.Value;
            if (_rbSub.Checked) amount = -amount;

            long amount0 = (long)_nudAmount.Value;
            bool isSub   = _rbSub.Checked;
            string opText = isSub ? $"扣除 {amount0:N0} 金幣" : $"發放 {amount0:N0} 金幣";

            // 列出前 10 個帳號；超大金額（≥ 500 萬）額外警告
            var nameList  = targets.Take(10).ToList();
            string names  = string.Join("\n", nameList.Select(a => $"  • {a}"));
            if (targets.Count > 10) names += $"\n  …（共 {targets.Count} 位）";

            bool bigAmount = amount0 >= 5_000_000;
            string bigWarn = bigAmount ? "\n\n⚠ 金幣數量超過 500 萬，請確認無誤！" : "";

            var confirm = MessageBox.Show(
                $"確定對以下 {targets.Count} 位玩家【{opText}】？\n\n{names}{bigWarn}\n\n此操作無法撤銷！",
                bigAmount ? "⚠ 大額操作確認" : "確認批量操作",
                MessageBoxButtons.YesNo,
                bigAmount ? MessageBoxIcon.Stop : MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            _isSending     = true;
            _btnSend.Enabled   = false;
            _btnCancel.Enabled = true;
            _progress.Maximum  = targets.Count;
            _progress.Value    = 0;
            _logBox.Clear();
            _execResults.Clear();

            _cts = new CancellationTokenSource();
            var prog = new Progress<(int done, int total, string acc, bool ok)>(r =>
            {
                _progress.Value   = r.done;
                _progressLbl.Text = $"{r.done}/{r.total}  {r.acc}";
                AppendLog($"{(r.ok ? "✓" : "✗")} {r.acc}\n",
                    r.ok ? Color.FromArgb(103, 194, 58) : Theme.AccentRed);
                _execResults.Add((r.acc, "", r.ok, r.acc));
            });

            try
            {
                var (success, fail) = await DatabaseManager.Instance.BatchGiveGoldAsync(
                    targets, amount, prog, _cts.Token);
                _statusLbl.Text = $"✓ 完成！成功 {success} 筆 / 失敗 {fail} 筆";
                AppendLog($"\n── 完成  成功:{success}  失敗:{fail} ──\n",
                    fail == 0 ? Color.FromArgb(103, 194, 58) : Theme.AccentOrange);
            }
            catch (OperationCanceledException)
            {
                _statusLbl.Text = "⛔ 已停止";
                AppendLog("⛔ 使用者中止\n", Theme.AccentOrange);
            }
            catch (Exception ex)
            {
                _statusLbl.Text = "✗ " + ex.Message;
                AppendLog("✗ " + ex.Message + "\n", Theme.AccentRed);
            }
            finally
            {
                _isSending     = false;
                _btnSend.Enabled   = true;
                _btnCancel.Enabled = false;
                _cts?.Dispose();
            }
        }

        // ═══════════════════════════════════════════════════════
        // 輔助
        // ═══════════════════════════════════════════════════════
        private void AppendLog(string text, Color color)
        {
            if (_logBox.InvokeRequired) { _logBox.Invoke(() => AppendLog(text, color)); return; }
            int start = _logBox.TextLength;
            _logBox.AppendText(text);
            _logBox.Select(start, text.Length);
            _logBox.SelectionColor = color;
            _logBox.SelectionLength = 0;
            _logBox.ScrollToCaret();
        }

        private RadioButton MakeRadio(Panel parent, string text, int x, ref int y, bool check = false)
        {
            var rb = new RadioButton
            {
                Text = text,
                Location = new Point(x, y),
                Checked = check
            };
            Theme.StyleRadioButtonSegment(rb, 290);
            parent.Controls.Add(rb);
            y += 40;
            return rb;
        }

        private void AddSection(Panel p, string title, ref int y, int x)
        {
            p.Controls.Add(new Label
            {
                Text = title, Location = new Point(x, y),
                ForeColor = Theme.AccentBlue,
                Font = new Font(Theme.FontFamily, 9.5f, FontStyle.Bold),
                AutoSize = true
            });
            y += 22;
            p.Controls.Add(new Panel
            {
                Location = new Point(x, y), Size = new Size(290, 1),
                BackColor = Theme.Border
            });
            y += 10;
        }

        // ═══════════════════════════════════════════════════════
        // 群組儲存 / 載入
        // ═══════════════════════════════════════════════════════
        private void SaveGroup()
        {
            var targets = _listDgv.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells["cChk"].Value is bool b && b).ToList();
            if (targets.Count == 0)
            {
                MessageBox.Show("請先勾選要儲存的玩家", "無勾選", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string name = Theme.ShowInputDialog("儲存玩家群組", "請輸入群組名稱：", "", this);
            if (string.IsNullOrWhiteSpace(name)) return;

            var group = new PlayerGroup
            {
                Name      = name,
                Accounts  = targets.Select(r => r.Cells["cAcc"].Value?.ToString() ?? "").Where(a => a != "").ToList(),
                CharNames = targets.Select(r => r.Cells["cName"].Value?.ToString() ?? "").ToList(),
                Note      = $"{targets.Count} 人"
            };
            PlayerGroupManager.Instance.AddOrUpdate(group);
            _statusLbl.Text = $"✓ 群組「{name}」已儲存（{group.Accounts.Count} 人）";
        }

        private void LoadGroup()
        {
            var groups = PlayerGroupManager.Instance.Groups;
            if (groups.Count == 0)
            {
                MessageBox.Show("尚未儲存任何群組\n請先勾選玩家後點「💾 儲存群組」", "無群組", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new Form
            {
                Text = "載入玩家群組", Size = new Size(460, 360),
                BackColor = Theme.BgMid, ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody, StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false
            };
            var dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(dgv);
            dgv.ReadOnly = true; dgv.AllowUserToAddRows = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cN",  HeaderText = "群組名稱", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cC",  HeaderText = "人數",     Width = 60,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cT",  HeaderText = "建立時間", Width = 130 });
            foreach (var g in groups)
                dgv.Rows.Add(g.Name, g.Accounts.Count, g.UpdatedAt.ToString("yyyy/MM/dd HH:mm"));

            var btnLoad = Theme.MakePrimaryButton("載入並加入清單", 140, 30);
            var btnDel  = Theme.MakeSecondaryButton("刪除", 60, 30);
            var btnFoot = new Panel { Dock = DockStyle.Bottom, Height = Theme.ToolbarHeight, BackColor = Theme.BgDialogHeader, Padding = new Padding(8, 7, 8, 7) };
            btnLoad.Dock = DockStyle.Left; btnLoad.Margin = new Padding(0, 0, 8, 0);
            btnDel.Dock  = DockStyle.Left;
            btnFoot.Controls.Add(btnLoad);
            btnFoot.Controls.Add(btnDel);
            dlg.Controls.Add(dgv);
            dlg.Controls.Add(btnFoot);

            btnLoad.Click += (s, e) =>
            {
                if (dgv.CurrentRow == null) return;
                string gname = dgv.CurrentRow.Cells["cN"].Value?.ToString() ?? "";
                var grp = PlayerGroupManager.Instance.Get(gname);
                if (grp == null) return;

                // 加入清單（補上尚未存在的帳號）
                var existing = _listDgv.Rows.Cast<DataGridViewRow>()
                    .Select(r => r.Cells["cAcc"].Value?.ToString() ?? "").ToHashSet();
                int added = 0;
                for (int i = 0; i < grp.Accounts.Count; i++)
                {
                    string acc  = grp.Accounts[i];
                    string cname = i < grp.CharNames.Count ? grp.CharNames[i] : "";
                    if (!existing.Contains(acc))
                    {
                        int ri = _listDgv.Rows.Add(true, "—", cname, acc);
                        ApplyRowStyle(_listDgv.Rows[ri], true);
                        added++;
                    }
                }
                _statusLbl.Text = $"✓ 載入群組「{gname}」，加入 {added} 人";
                UpdateSelectedCount();
                dlg.Close();
            };
            btnDel.Click += (s, e) =>
            {
                if (dgv.CurrentRow == null) return;
                string gname = dgv.CurrentRow.Cells["cN"].Value?.ToString() ?? "";
                if (MessageBox.Show($"確定刪除群組「{gname}」？", "確認", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
                PlayerGroupManager.Instance.Remove(gname);
                dgv.Rows.RemoveAt(dgv.CurrentRow.Index);
            };
            dlg.ShowDialog(this);
        }

        // ═══════════════════════════════════════════════════════
        // 上傳檔案 → 自動勾選對應玩家
        //   檔案內容（CSV / TXT / XLSX）支援：
        //     1) 標題列含 Name/cdkey/識別編號 與 OnlineName/角色名稱
        //     2) 沒標題時：第 1 欄 = 識別編號，第 2 欄 = 角色名稱
        //   行為：
        //     - DGV 中已有的玩家 → 直接勾選
        //     - DGV 中沒有的玩家 → 加為新列（在線狀態 = 「？」）
        //     - 不會自動跑 SQL 對帳；執行批量金幣時若 cdkey 不存在 DB，
        //       BatchGiveGoldAsync 內部會 fail 該筆，使用者會在執行日誌看到 ✗
        // ═══════════════════════════════════════════════════════
        private void UploadAndCheckList()
        {
            using var ofd = new OpenFileDialog
            {
                Title  = "上傳玩家清單（自動勾選）",
                Filter = PlayerListImporter.DialogFilter
            };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            PlayerListImporter.ParseResult parsed;
            try { parsed = PlayerListImporter.ParseFile(ofd.FileName); }
            catch (Exception ex)
            {
                MessageBox.Show("解析失敗：" + ex.Message, "上傳失敗",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (parsed.Rows.Count == 0)
            {
                MessageBox.Show($"檔案內沒有有效資料。\n\n{parsed.DetectedSource}",
                    "無資料", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 預覽
            var preview = string.Join("\n", parsed.Rows.Take(5).Select(r =>
                string.IsNullOrEmpty(r.OnlineName) ? $"  • {r.Cdkey}" : $"  • {r.Cdkey}（{r.OnlineName}）"));
            if (parsed.Rows.Count > 5) preview += $"\n  …（共 {parsed.Rows.Count} 筆）";

            string msg =
                $"已從檔案讀到 {parsed.Rows.Count} 筆。\n" +
                $"來源：{parsed.DetectedSource}\n" +
                (parsed.Skipped > 0 ? $"略過空白列：{parsed.Skipped}\n" : "") +
                $"\n預覽：\n{preview}\n\n" +
                $"目前清單已有 {_listDgv.Rows.Count} 位玩家、勾選 " +
                $"{_listDgv.Rows.Cast<DataGridViewRow>().Count(r => r.Cells["cChk"].Value is bool b && b)} 位。\n\n" +
                "要如何處理？\n" +
                "[是] = 覆蓋（清空清單，只保留檔案內的玩家並全部勾選）\n" +
                "[否] = 追加（保留現有清單，將檔案內的玩家加入並勾選；已存在者只勾選不重複新增）\n" +
                "[取消] = 不動作";
            var rsp = MessageBox.Show(msg, "上傳玩家清單",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (rsp == DialogResult.Cancel) return;

            _listDgv.SuspendLayout();
            if (rsp == DialogResult.Yes) _listDgv.Rows.Clear();

            // 建立 cdkey → row 的快速對照表
            var byCdkey = _listDgv.Rows.Cast<DataGridViewRow>()
                .ToDictionary(
                    r => (r.Cells["cAcc"].Value?.ToString() ?? "").Trim().ToLowerInvariant(),
                    r => r,
                    StringComparer.OrdinalIgnoreCase);

            // 先取消所有原本的勾選（覆蓋模式上面已清空，這裡只影響追加模式）
            // ※ 追加模式下：不主動取消既有勾選，保留既有勾選 + 新增的全部勾選
            int newRows = 0, alreadyExisted = 0, skipped = 0;
            foreach (var r in parsed.Rows)
            {
                string cdkey = (r.Cdkey ?? "").Trim();
                if (string.IsNullOrEmpty(cdkey)) { skipped++; continue; }

                string key = cdkey.ToLowerInvariant();
                if (byCdkey.TryGetValue(key, out var existing))
                {
                    existing.Cells["cChk"].Value = true;
                    ApplyRowStyle(existing, true);
                    alreadyExisted++;
                }
                else
                {
                    int ri = _listDgv.Rows.Add(true, "？", r.OnlineName ?? "", cdkey);
                    ApplyRowStyle(_listDgv.Rows[ri], true);
                    byCdkey[key] = _listDgv.Rows[ri];
                    newRows++;
                }
            }
            _listDgv.ResumeLayout();
            UpdateSelectedCount();

            string verb = rsp == DialogResult.Yes ? "覆蓋" : "追加";
            _statusLbl.Text = $"✓ 已{verb}：新增 {newRows} 人、原已存在自動勾選 {alreadyExisted} 人" +
                              (skipped > 0 ? $"、空白略過 {skipped}" : "");
            AppendLog($"📤 上傳清單：{verb}模式，新增 {newRows}、勾選原有 {alreadyExisted}\n",
                Color.FromArgb(100, 180, 255));
        }

        // ═══════════════════════════════════════════════════════
        // 匯出 Excel
        // ═══════════════════════════════════════════════════════
        private void ExportResultsExcel()
        {
            // 收集清單資料（不論是否執行過）
            var rows = _listDgv.Rows.Cast<DataGridViewRow>().ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("清單是空的，無可匯出的資料", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title            = "匯出玩家清單",
                Filter           = "Excel 檔案|*.xlsx",
                FileName         = $"批量金幣_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var pkg = new ExcelPackage();
                var ws = pkg.Workbook.Worksheets.Add("玩家清單");

                // 標題行
                ws.Cells[1, 1].Value = "勾選";
                ws.Cells[1, 2].Value = "角色名稱";
                ws.Cells[1, 3].Value = "帳號";
                ws.Cells[1, 4].Value = "執行結果";
                using (var hdr = ws.Cells[1, 1, 1, 4])
                {
                    hdr.Style.Font.Bold  = true;
                    hdr.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    hdr.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 57, 110));
                    hdr.Style.Font.Color.SetColor(Color.White);
                }

                // 資料行
                var resultMap = _execResults.ToDictionary(r => r.acc, r => r.ok ? "✓ 成功" : "✗ 失敗");
                int row = 2;
                foreach (var dgvRow in rows)
                {
                    bool   chk  = dgvRow.Cells["cChk"].Value  is bool b && b;
                    string name = dgvRow.Cells["cName"].Value?.ToString() ?? "";
                    string acc  = dgvRow.Cells["cAcc"].Value?.ToString()  ?? "";
                    string res  = resultMap.TryGetValue(acc, out string rv) ? rv : "";
                    ws.Cells[row, 1].Value = chk ? "✓" : "";
                    ws.Cells[row, 2].Value = name;
                    ws.Cells[row, 3].Value = acc;
                    ws.Cells[row, 4].Value = res;
                    if (res == "✗ 失敗") ws.Cells[row, 4].Style.Font.Color.SetColor(Color.Red);
                    else if (res == "✓ 成功") ws.Cells[row, 4].Style.Font.Color.SetColor(Color.FromArgb(0, 128, 0));
                    row++;
                }
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                // 附加：操作摘要
                int sumRow = row + 2;
                ws.Cells[sumRow,   1].Value = "操作時間";
                ws.Cells[sumRow,   2].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                ws.Cells[sumRow+1, 1].Value = "金幣操作";
                ws.Cells[sumRow+1, 2].Value = _rbSub.Checked
                    ? $"扣除 {_nudAmount.Value:N0} 金幣"
                    : $"發放 {_nudAmount.Value:N0} 金幣";
                ws.Cells[sumRow+2, 1].Value = "成功筆數";
                ws.Cells[sumRow+2, 2].Value = _execResults.Count(r => r.ok);
                ws.Cells[sumRow+3, 1].Value = "失敗筆數";
                ws.Cells[sumRow+3, 2].Value = _execResults.Count(r => !r.ok);

                pkg.SaveAs(new FileInfo(sfd.FileName));
                _statusLbl.Text = $"✓ 已匯出：{Path.GetFileName(sfd.FileName)}";

                if (MessageBox.Show("匯出成功，是否開啟檔案？", "完成",
                    MessageBoxButtons.YesNo) == DialogResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("匯出失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
