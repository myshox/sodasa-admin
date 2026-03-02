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
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
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
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  💰  批量金幣修改  —  搜尋玩家，勾選目標後發放或扣除金幣",
                ForeColor = Theme.AccentOrange, Font = Theme.FontBody,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            });

            // ── 狀態列 ──────────────────────────────────────────
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _statusLbl = new Label
            {
                Text = "請選擇目標範圍，載入玩家後勾選要操作的對象",
                ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
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
                    try { split.SplitterDistance = Math.Max(300, Math.Min(split.Width - 460, 340)); } catch { }
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

            _rbAll = MakeRadio(scroll, "🌐 全服所有玩家", x, ref y, true);
            _rbOnline = MakeRadio(scroll, "🟢 僅在線玩家", x, ref y);
            _rbSearch = MakeRadio(scroll, "🔍 依關鍵字搜尋", x, ref y);

            // 搜尋框
            var searchRow = new Panel
            {
                Location = new Point(x + 20, y), Width = 280, Height = 30,
                BackColor = Color.Transparent
            };
            _txtSearch = new TextBox
            {
                PlaceholderText = "角色名稱或帳號",
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

            scroll.Controls.Add(new Label { Text = "操作：", Location = new Point(x, y + 4), ForeColor = Theme.TextSecondary, AutoSize = true });
            _rbAdd = new RadioButton { Text = "➕ 發放（增加）", Location = new Point(x + 56, y), ForeColor = Theme.AccentGreen, AutoSize = true, Checked = true, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent };
            _rbSub = new RadioButton { Text = "➖ 扣除（減少）", Location = new Point(x + 210, y), ForeColor = Theme.AccentRed, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent };
            scroll.Controls.AddRange(new Control[] { _rbAdd, _rbSub });
            y += 36;

            scroll.Controls.Add(new Label { Text = "金幣數量：", Location = new Point(x, y + 4), ForeColor = Theme.TextSecondary, AutoSize = true });
            _nudAmount = new NumericUpDown
            {
                Location = new Point(x + 80, y), Width = 180, Height = 28,
                Minimum = 1, Maximum = 10_000_000, Value = 1000, Increment = 1000,
                BackColor = Theme.BgLight, ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody, ThousandsSeparator = true
            };
            scroll.Controls.Add(_nudAmount);
            y += 50;

            // ── STEP 3：確認執行 ──────────────────────────────
            AddSection(scroll, "STEP 3 — 確認執行", ref y, x);

            var warnPanel = new Panel { Location = new Point(x, y), Size = new Size(300, 44), BackColor = Color.FromArgb(60, 30, 10) };
            warnPanel.Controls.Add(new Label
            {
                Text = "⚠  此操作無法撤銷，請確認勾選名單和金額！",
                ForeColor = Theme.AccentOrange, Font = Theme.FontSmall,
                Location = new Point(8, 6), AutoSize = true
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
            var toolbarWrap = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(20, 24, 36) };

            // 第一列：標題 + 全選操作
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(20, 24, 36), Padding = new Padding(8, 5, 8, 0) };
            var titleLbl = new Label
            {
                Text = "📋  玩家清單  — 勾選要操作的對象",
                ForeColor = Theme.AccentBlue, Font = new Font(Theme.FontFamily, 9f, FontStyle.Bold),
                Dock = DockStyle.Left, Width = 220, TextAlign = ContentAlignment.MiddleLeft
            };
            _btnSelAll    = Theme.MakeSecondaryButton("全選",    52, 24);
            _btnSelNone   = Theme.MakeSecondaryButton("取消全選", 68, 24);
            _btnSelInvert = Theme.MakeSecondaryButton("反選",    52, 24);
            _btnSelAll.Dock    = DockStyle.Left; _btnSelAll.Margin    = new Padding(0, 0, 4, 0);
            _btnSelNone.Dock   = DockStyle.Left; _btnSelNone.Margin   = new Padding(0, 0, 4, 0);
            _btnSelInvert.Dock = DockStyle.Left; _btnSelInvert.Margin = new Padding(0, 0, 4, 0);
            _lblSelected = new Label
            {
                Text = "請先載入玩家", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.Right, Width = 130, TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 4, 0)
            };
            _btnSelAll.Click    += (s, e) => SetAllChecked(true);
            _btnSelNone.Click   += (s, e) => SetAllChecked(false);
            _btnSelInvert.Click += (s, e) => InvertChecked();
            toolbar.Controls.Add(titleLbl);
            toolbar.Controls.Add(_btnSelAll);
            toolbar.Controls.Add(_btnSelNone);
            toolbar.Controls.Add(_btnSelInvert);
            toolbar.Controls.Add(_lblSelected);

            // 第二列：群組操作 + 匯出
            var toolbar2 = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(16, 20, 32), Padding = new Padding(8, 4, 8, 4) };
            var btnSaveGrp = Theme.MakeSecondaryButton("💾 儲存群組", 88, 24);
            var btnLoadGrp = Theme.MakeSecondaryButton("📂 載入群組", 88, 24);
            var btnExport  = Theme.MakeSecondaryButton("📥 匯出 Excel", 100, 24);
            btnSaveGrp.Dock = DockStyle.Left; btnSaveGrp.Margin = new Padding(0, 0, 6, 0);
            btnLoadGrp.Dock = DockStyle.Left; btnLoadGrp.Margin = new Padding(0, 0, 6, 0);
            btnExport.Dock  = DockStyle.Left; btnExport.Margin  = new Padding(0, 0, 6, 0);

            btnSaveGrp.Click += (s, e) => SaveGroup();
            btnLoadGrp.Click += (s, e) => LoadGroup();
            btnExport.Click  += (s, e) => ExportResultsExcel();

            toolbar2.Controls.Add(btnSaveGrp);
            toolbar2.Controls.Add(btnLoadGrp);
            toolbar2.Controls.Add(btnExport);

            toolbarWrap.Controls.Add(toolbar2);
            toolbarWrap.Controls.Add(toolbar);

            // ── 玩家清單 DGV（帶勾選框）────────────────────────
            _listDgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_listDgv);
            _listDgv.ReadOnly            = false;
            _listDgv.AllowUserToAddRows  = false;
            _listDgv.RowTemplate.Height  = 28;
            _listDgv.ColumnHeadersHeight = 28;
            _listDgv.MultiSelect         = true;
            _listDgv.Tag                 = "picker_no_copy";

            var colChk = new DataGridViewCheckBoxColumn { Name = "cChk", HeaderText = "✓", Width = 42 };
            colChk.DefaultCellStyle.Alignment  = DataGridViewContentAlignment.MiddleCenter;
            colChk.HeaderCell.Style.Alignment  = DataGridViewContentAlignment.MiddleCenter;
            var colSt  = new DataGridViewTextBoxColumn { Name = "cSt",  HeaderText = "狀", Width = 42, ReadOnly = true };
            colSt.DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            var colName = new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "角色名稱", Width = 140, ReadOnly = true };
            var colAcc  = new DataGridViewTextBoxColumn { Name = "cAcc",     HeaderText = "帳號",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120, ReadOnly = true };
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
                Dock = DockStyle.Bottom, Height = 140,
                BackColor = Color.FromArgb(14, 14, 20), ForeColor = Theme.TextPrimary,
                Font = new Font("Consolas", 9f), ReadOnly = true, BorderStyle = BorderStyle.None
            };
            var logHdr = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = Theme.BgDark };
            logHdr.Controls.Add(new Label
            {
                Text = "  執行日誌", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
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
                    players = await DatabaseManager.Instance.SearchPlayersAsync("");
                else if (_rbOnline.Checked)
                    players = await DatabaseManager.Instance.GetOnlinePlayersAsync();
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
                    int ri = _listDgv.Rows.Add(true, pl.IsOnline ? "🟢" : "⚫", pl.OnlineName, pl.Account);
                    // 預設全選，高亮標示
                    _listDgv.Rows[ri].DefaultCellStyle.BackColor = Color.FromArgb(22, 103, 194, 58);
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
            row.DefaultCellStyle.BackColor = selected
                ? Color.FromArgb(22, 103, 194, 58)
                : Color.Empty;
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

            string opText = _rbSub.Checked ? $"扣除 {_nudAmount.Value:N0} 金幣" : $"發放 {_nudAmount.Value:N0} 金幣";
            var confirm = MessageBox.Show(
                $"確定對已勾選的 {targets.Count} 位玩家\n{opText}？\n\n此操作無法撤銷！",
                "確認批量操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
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
                Text = text, Location = new Point(x, y),
                ForeColor = Theme.TextPrimary, AutoSize = true, Checked = check,
                FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent
            };
            parent.Controls.Add(rb);
            y += 28;
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
            dgv.RowTemplate.Height = 26; dgv.ColumnHeadersHeight = 26;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cN",  HeaderText = "群組名稱", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cC",  HeaderText = "人數",     Width = 60,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cT",  HeaderText = "建立時間", Width = 130 });
            foreach (var g in groups)
                dgv.Rows.Add(g.Name, g.Accounts.Count, g.UpdatedAt.ToString("yyyy/MM/dd HH:mm"));

            var btnLoad = Theme.MakePrimaryButton("載入並加入清單", 140, 30);
            var btnDel  = Theme.MakeSecondaryButton("刪除", 60, 30);
            var btnFoot = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Theme.BgDark, Padding = new Padding(8, 7, 8, 7) };
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
                        int ri = _listDgv.Rows.Add(true, "", cname, acc);
                        _listDgv.Rows[ri].DefaultCellStyle.BackColor = Color.FromArgb(22, 103, 194, 58);
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
