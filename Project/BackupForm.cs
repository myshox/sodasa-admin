using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 資料庫備份 / 還原視窗
    /// 備份內容：csalogin（玩家帳號）+ lock（封禁記錄）
    /// 使用 INSERT IGNORE：還原時只補回遺失資料，不覆蓋現有記錄
    /// </summary>
    public class BackupForm : Form
    {
        private Label    _statusLbl;
        private ListBox  _backupList;
        private Button   _btnBackup, _btnRestoreSelected, _btnRestoreFile;
        private ProgressBar _progress;

        private static string BackupFolder => Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppDomain.CurrentDomain.BaseDirectory,
            "GMTool", "backups");

        public BackupForm()
        {
            Text          = "💾 資料庫備份 / 還原";
            Size          = new Size(720, 620);
            MinimumSize   = new Size(640, 540);
            BackColor = Theme.BgPage;
            ForeColor     = Theme.TextPrimary;
            Font          = Theme.FontBody;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            BuildUI();
            RefreshList();
        }

        private void BuildUI()
        {
            // ── 底部狀態列 ────────────────────────────────────────
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Theme.BgCard };
            _statusLbl = new Label
            {
                Text      = "就緒",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(10, 0, 0, 0)
            };
            bottom.Controls.Add(_statusLbl);
            Controls.Add(bottom);

            // ── 進度列 ───────────────────────────────────────────
            _progress = new ProgressBar
            {
                Dock    = DockStyle.Bottom,
                Height  = 6,
                Style   = ProgressBarStyle.Marquee,
                Visible = false
            };
            Controls.Add(_progress);

            // ── 主體（TableLayoutPanel 兩行：備份區 + 還原區）───
            var main = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                Margin      = Padding.Empty,
                Padding     = new Padding(0)
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));   // 備份說明 + 按鈕
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // 備份清單
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));   // 還原區（加高避免被截）

            // ── 第一區：備份說明 ──────────────────────────────
            var backupPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.BgCard,
                Padding   = new Padding(18, 14, 18, 12)
            };

            var title1 = new Label
            {
                Text      = "📦  資料備份",
                ForeColor = Theme.AccentBlue,
                Font      = Theme.FontHeader,
                AutoSize  = true,
                Location  = new Point(18, 14)
            };

            var desc = new Label
            {
                Text = "備份內容：csalogin（玩家帳號資料）+ lock（封禁記錄）\n" +
                       "備份格式：SQL 文字檔案（可手動查閱），預設儲存至 GMTool\\backups\\ 資料夾",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                AutoSize  = false,
                Size      = new Size(560, 36),
                Location  = new Point(18, 40)
            };

            _btnBackup = new Button
            {
                Text      = "💾  立即備份",
                BackColor = Theme.AccentGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontHeader,
                Size      = new Size(150, 40),
                Location  = new Point(18, 88),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            _btnBackup.FlatAppearance.BorderColor = Theme.AccentGreen;
            _btnBackup.Click += BtnBackup_Click;

            var btnOpenFolder = new Button
            {
                Text      = "📁 開啟備份資料夾",
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontSmall,
                Size      = new Size(140, 40),
                Location  = new Point(178, 88),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            btnOpenFolder.FlatAppearance.BorderColor = Theme.Border;
            btnOpenFolder.Click += (s, e) =>
            {
                Directory.CreateDirectory(BackupFolder);
                System.Diagnostics.Process.Start("explorer.exe", BackupFolder);
            };

            backupPanel.Controls.AddRange(new Control[] { title1, desc, _btnBackup, btnOpenFolder });
            main.Controls.Add(backupPanel, 0, 0);

            // ── 第二區：備份清單 ──────────────────────────────
            var listPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.BgPage, Padding = new Padding(12, 0, 12, 0) };

            var listTitle = new Label
            {
                Text      = "📋  備份記錄（雙擊或選取後點「還原選取」可還原）",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                Dock      = DockStyle.Top,
                Height    = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(2, 0, 0, 0)
            };

            _backupList = new ListBox
            {
                Dock        = DockStyle.Fill,
                BackColor = Theme.BgInput,
                ForeColor   = Theme.TextPrimary,
                Font        = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                ItemHeight  = 22
            };
            _backupList.DoubleClick += (s, e) => DoRestoreSelected();

            listPanel.Controls.Add(_backupList);
            listPanel.Controls.Add(listTitle);
            main.Controls.Add(listPanel, 0, 1);

            // ── 第三區：還原操作 ──────────────────────────────
            var restorePanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Theme.BgCard,
                Padding   = new Padding(18, 14, 18, 14)
            };

            restorePanel.Controls.Add(new Label
            {
                Text      = "⚠️  還原區",
                ForeColor = Theme.AccentOrange,
                Font      = Theme.FontHeader,
                AutoSize  = true,
                Location  = new Point(18, 12)
            });
            restorePanel.Controls.Add(new Label
            {
                Text = "還原採用「INSERT IGNORE」模式：不覆蓋現有資料，只補回已遺失的記錄，安全可靠。",
                ForeColor = Theme.TextSecondary,
                Font      = Theme.FontSmall,
                AutoSize  = false,
                Size      = new Size(580, 18),
                Location  = new Point(18, 40)
            });

            _btnRestoreSelected = new Button
            {
                Text      = "📥  還原選取的備份",
                BackColor = Theme.AccentOrange,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontBody,
                Size      = new Size(168, 36),
                Location  = new Point(18, 68),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            _btnRestoreSelected.FlatAppearance.BorderColor = Theme.AccentOrange;
            _btnRestoreSelected.Click += (s, e) => DoRestoreSelected();

            _btnRestoreFile = new Button
            {
                Text      = "📂  選擇其他備份檔案",
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font      = Theme.FontBody,
                Size      = new Size(168, 36),
                Location  = new Point(196, 68),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            _btnRestoreFile.FlatAppearance.BorderColor = Theme.Border;
            _btnRestoreFile.Click += BtnRestoreFile_Click;

            restorePanel.Controls.AddRange(new Control[] { _btnRestoreSelected, _btnRestoreFile });
            main.Controls.Add(restorePanel, 0, 2);

            Controls.Add(main);
        }

        private void RefreshList()
        {
            _backupList.Items.Clear();
            try
            {
                if (!Directory.Exists(BackupFolder))
                {
                    _backupList.Items.Add("（備份資料夾尚未建立，請先執行備份）");
                    return;
                }

                var files = new DirectoryInfo(BackupFolder)
                    .GetFiles("*.sql", SearchOption.TopDirectoryOnly);
                Array.Sort(files, (a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

                if (files.Length == 0)
                {
                    _backupList.Items.Add("（尚無備份檔案，請先執行備份）");
                    return;
                }

                foreach (var f in files)
                {
                    double kb = f.Length / 1024.0;
                    string size = kb >= 1024 ? $"{kb / 1024:F1} MB" : $"{kb:F0} KB";
                    _backupList.Items.Add(
                        $"{f.LastWriteTime:yyyy-MM-dd HH:mm:ss}  │  {size,-8}  │  {f.Name}");
                    _backupList.Tag = _backupList.Tag ?? new Dictionary<int, string>();
                    ((Dictionary<int, string>)_backupList.Tag)[_backupList.Items.Count - 1] = f.FullName;
                }

                _backupList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _backupList.Items.Add("載入備份清單失敗：" + ex.Message);
            }
        }

        private string GetSelectedFilePath()
        {
            int idx = _backupList.SelectedIndex;
            if (idx < 0) return null;
            if (_backupList.Tag is Dictionary<int, string> dict && dict.TryGetValue(idx, out string path))
                return path;
            return null;
        }

        private async void BtnBackup_Click(object sender, EventArgs e)
        {
            if (!DatabaseManager.Instance.IsConnected)
            {
                MessageBox.Show("請先連接資料庫！", "未連接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true, "備份中，請稍候…");
            try
            {
                var prog = new Progress<string>(msg => SetStatus(msg));
                var (rows, filePath) = await DatabaseManager.Instance.BackupAsync(BackupFolder, prog);
                SetStatus($"✓ 備份完成！共 {rows} 筆記錄 → {Path.GetFileName(filePath)}");
                RefreshList();
                MessageBox.Show(
                    $"備份成功！\n\n共備份 {rows} 筆記錄\n儲存於：{filePath}",
                    "備份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("✗ 備份失敗：" + ex.Message);
                MessageBox.Show("備份失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false); }
        }

        private async void DoRestoreSelected()
        {
            string path = GetSelectedFilePath();
            if (path == null)
            {
                MessageBox.Show("請先從清單中選取一個備份檔案。", "未選取", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            await DoRestore(path);
        }

        private async void BtnRestoreFile_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title  = "選擇備份 SQL 檔案",
                Filter = "SQL 備份檔案|*.sql|所有檔案|*.*",
                InitialDirectory = Directory.Exists(BackupFolder) ? BackupFolder : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            await DoRestore(ofd.FileName);
        }

        private async System.Threading.Tasks.Task DoRestore(string filePath)
        {
            if (!DatabaseManager.Instance.IsConnected)
            {
                MessageBox.Show("請先連接資料庫！", "未連接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string fileName = Path.GetFileName(filePath);
            var confirm = MessageBox.Show(
                $"確定從以下備份還原資料？\n\n" +
                $"  備份檔案：{fileName}\n\n" +
                $"⚠ 還原採用 INSERT IGNORE 模式：\n" +
                $"  · 遺失的記錄 → 補回資料庫\n" +
                $"  · 現有的記錄 → 不受影響，保持不變",
                "確認還原", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "還原中，請稍候…");
            try
            {
                var prog = new Progress<string>(msg => SetStatus(msg));
                var (success, fail, errors) = await DatabaseManager.Instance.RestoreFromBackupAsync(filePath, prog);

                string detail = fail > 0
                    ? $"\n\n失敗記錄（最多顯示20筆）：\n" + string.Join("\n", errors)
                    : "";

                SetStatus($"✓ 還原完成！成功 {success} 筆，失敗 {fail} 筆");
                MessageBox.Show(
                    $"還原完成！\n\n  成功：{success} 筆\n  失敗：{fail} 筆" + detail,
                    "還原結果",
                    MessageBoxButtons.OK,
                    fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("✗ 還原失敗：" + ex.Message);
                MessageBox.Show("還原失敗：\n" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { SetBusy(false); }
        }

        private void SetBusy(bool busy, string msg = "就緒")
        {
            if (InvokeRequired) { Invoke(new Action(() => SetBusy(busy, msg))); return; }
            _btnBackup.Enabled          = !busy;
            _btnRestoreSelected.Enabled = !busy;
            _btnRestoreFile.Enabled     = !busy;
            _progress.Visible           = busy;
            if (!busy) SetStatus(msg);
        }

        private void SetStatus(string msg)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(msg))); return; }
            _statusLbl.Text = msg;
        }
    }
}
