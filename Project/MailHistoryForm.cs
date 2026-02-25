using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using OfficeOpenXml;

namespace SQ_Email_Tools
{
    public class MailHistoryForm : Form
    {
        private TextBox      _searchBox;
        private Button       _btnSearch, _btnExport;
        private DataGridView _dgv;
        private Label        _statusLbl, _countLbl;
        private List<MailRecord> _records = new List<MailRecord>();

        public MailHistoryForm()
        {
            InitUI();
            _ = LoadAsync("");
        }

        private void InitUI()
        {
            Text          = "📋 郵件發送記錄";
            Size          = new Size(1150, 680);
            MinimumSize   = new Size(850, 500);
            BackColor     = Theme.BgMid;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ──
            var header = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.BgDark };
            header.Controls.Add(new Label
            {
                Text      = "  📋  郵件發送記錄  —  查詢所有道具郵件的發送狀態",
                ForeColor = Color.FromArgb(140, 190, 255),
                Font      = Theme.FontBody,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            });

            // ── 搜尋列 ──
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.BgCard };
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Border });
            searchPanel.Controls.Add(new Label
            {
                Text = "輸入帳號或角色名稱搜尋郵件記錄",
                ForeColor = Theme.TextMuted, Font = new Font(Theme.FontFamily, 8.5f),
                AutoSize = true, Location = new Point(42, 4)
            });
            var searchIcon = new Label { Text = "🔍", Font = new Font("Segoe UI Emoji", 14f), AutoSize = true, Location = new Point(12, 22) };
            _searchBox = new TextBox
            {
                BackColor = Theme.BgPage, ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font(Theme.FontFamily, 11f),
                PlaceholderText = "帳號或角色名稱（留空 = 全部）",
                Location = new Point(42, 22), Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _ = LoadAsync(_searchBox.Text.Trim()); };
            _btnSearch = Theme.MakePrimaryButton("查詢", 80, 28);
            _btnSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSearch.Click += (s, e) => _ = LoadAsync(_searchBox.Text.Trim());
            _btnExport = Theme.MakeButton("📥 匯出", Theme.AccentGreen, Color.White, 86, 28);
            _btnExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnExport.Click += BtnExport_Click;
            searchPanel.Controls.AddRange(new Control[] { searchIcon, _searchBox, _btnSearch, _btnExport });
            searchPanel.Resize += (s, e) =>
            {
                int pw = searchPanel.ClientSize.Width;
                _btnSearch.Left  = pw - 12 - _btnSearch.Width;
                _btnSearch.Top   = 32;
                _btnExport.Left  = _btnSearch.Left - _btnExport.Width - 8;
                _btnExport.Top   = 32;
                _searchBox.Width = Math.Max(100, _btnExport.Left - _searchBox.Left - 8);
            };

            // ── 狀態列 ──
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = Theme.BgDark };
            _countLbl  = new Label { Text = "共 0 筆", ForeColor = Theme.AccentGreen, Font = Theme.FontSmall,
                Dock = DockStyle.None, AutoSize = true, Location = new Point(12, 7) };
            _statusLbl = new Label { Text = "", ForeColor = Theme.TextMuted, Font = Theme.FontSmall,
                Dock = DockStyle.None, AutoSize = true, Location = new Point(75, 7) };
            statusBar.Controls.AddRange(new Control[] { _countLbl, _statusLbl });

            // ── DataGridView ──
            _dgv = new DataGridView { Dock = DockStyle.Fill };
            Theme.StyleDataGridView(_dgv);
            _dgv.ReadOnly           = true;
            _dgv.RowTemplate.Height = 32;
            AddCol("cType",  "類型",    68);
            AddCol("cCdkey", "帳號",   130);
            AddCol("cData",  "道具號",  78);
            AddCol("cBuff1", "標題",   180);
            AddCol("cBuff3", "說明",   200, fill: true);
            AddCol("cSend",  "發送時間",150);
            AddCol("cEnd",   "到期",    95);
            AddCol("cStat",  "狀態",    72);

            // 加入順序：Bottom → Fill → Top
            Controls.Add(statusBar);
            Controls.Add(_dgv);
            Controls.Add(searchPanel);
            Controls.Add(header);
        }

        private void AddCol(string name, string header, int w, bool fill = false)
        {
            var col = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = w, ReadOnly = true };
            if (fill) col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _dgv.Columns.Add(col);
        }

        private async Task LoadAsync(string filter)
        {
            if (!DatabaseManager.Instance.IsConnected)
            { _statusLbl.Text = "⚠ 未連接資料庫"; return; }

            _btnSearch.Enabled = false;
            _statusLbl.Text = "查詢中…";
            try
            {
                _records = await DatabaseManager.Instance.GetAllMailHistoryAsync(filter, 500);
                if (InvokeRequired) Invoke(new Action(FillGrid));
                else FillGrid();
            }
            catch (Exception ex) { _statusLbl.Text = "✗ " + ex.Message; }
            finally { if (InvokeRequired) Invoke(new Action(() => _btnSearch.Enabled = true)); else _btnSearch.Enabled = true; }
        }

        private void FillGrid()
        {
            _dgv.Rows.Clear();
            foreach (var r in _records)
            {
                int i = _dgv.Rows.Add(
                    r.Id, r.TypeStr, r.Cdkey, r.Data,
                    r.Buff1, r.Buff3, r.SendTimeStr, r.EndTimeStr, r.StatusStr);
                _dgv.Rows[i].Tag = r;
                if (r.CheckFlag == 1)
                    _dgv.Rows[i].DefaultCellStyle.ForeColor = Theme.TextMuted;
            }
            _countLbl.Text  = $"共 {_records.Count} 筆";
            _statusLbl.Text = _records.Count > 0 ? "✓ 查詢完成" : "查無資料";
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_records.Count == 0) { MessageBox.Show("沒有資料可匯出"); return; }

            using var sfd = new SaveFileDialog
            {
                Filter   = "Excel|*.xlsx",
                FileName = $"郵件記錄_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var pkg = new ExcelPackage();
                var ws = pkg.Workbook.Worksheets.Add("郵件記錄");

                // 標題
                string[] headers = { "ID", "類型", "帳號", "道具號", "標題", "內容", "說明", "發送時間", "到期時間", "狀態" };
                for (int c = 0; c < headers.Length; c++)
                {
                    ws.Cells[1, c + 1].Value = headers[c];
                    ws.Cells[1, c + 1].Style.Font.Bold = true;
                    ws.Cells[1, c + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 50, 100));
                    ws.Cells[1, c + 1].Style.Font.Color.SetColor(Color.White);
                }

                // 資料
                for (int r = 0; r < _records.Count; r++)
                {
                    var rec = _records[r];
                    ws.Cells[r + 2, 1].Value  = rec.Id;
                    ws.Cells[r + 2, 2].Value  = rec.TypeStr;
                    ws.Cells[r + 2, 3].Value  = rec.Cdkey;
                    ws.Cells[r + 2, 4].Value  = rec.Data;
                    ws.Cells[r + 2, 5].Value  = rec.Buff1;
                    ws.Cells[r + 2, 6].Value  = rec.Buff2;
                    ws.Cells[r + 2, 7].Value  = rec.Buff3;
                    ws.Cells[r + 2, 8].Value  = rec.SendTimeStr;
                    ws.Cells[r + 2, 9].Value  = rec.EndTimeStr;
                    ws.Cells[r + 2, 10].Value = rec.StatusStr;
                }

                ws.Cells.AutoFitColumns();
                pkg.SaveAs(new FileInfo(sfd.FileName));
                MessageBox.Show($"✓ 已匯出 {_records.Count} 筆至：\n{sfd.FileName}", "匯出成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show("匯出失敗：" + ex.Message, "錯誤");
            }
        }
    }
}
