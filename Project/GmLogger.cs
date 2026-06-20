using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SQ_Email_Tools
{
    public class GmLogger
    {
        private static GmLogger _instance;
        public static GmLogger Instance => _instance ??= new GmLogger();

        private readonly string _logDir;
        private readonly object _lock = new object();

        // 目前 GM 操作員名稱（可在設定中更改）
        public string OperatorName { get; set; } = "GM";

        // 記憶體中保留最近 500 筆，供 UI 顯示
        private readonly List<GmLogEntry> _recentLogs = new List<GmLogEntry>(500);
        public IReadOnlyList<GmLogEntry> RecentLogs => _recentLogs.AsReadOnly();

        public event Action LogUpdated;

        public GmLogger()
        {
            _logDir = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath)
                ?? AppDomain.CurrentDomain.BaseDirectory,
                "logs");
            Directory.CreateDirectory(_logDir);
            LoadTodayLogs();
        }

        public Task LogAsync(string action, string target, string detail, bool success)
        {
            var entry = new GmLogEntry
            {
                Time     = DateTime.Now,
                Operator = OperatorName,
                Action   = action,
                Target   = target,
                Detail   = detail,
                Success  = success
            };

            lock (_lock)
            {
                // 加到記憶體清單（最多 500，超過移除最舊的）
                if (_recentLogs.Count >= 500) _recentLogs.RemoveAt(0);
                _recentLogs.Add(entry);

                // 寫到當天的 log 檔（本機備援，即使資料庫不可用仍保留紀錄）
                string logFile = Path.Combine(_logDir, $"{DateTime.Today:yyyy-MM-dd}.log");
                string line = $"[{entry.Time:HH:mm:ss}] [{entry.Operator}] {(success ? "✓" : "✗")} {action} | {target} | {detail}";
                File.AppendAllText(logFile, line + "\n", Encoding.UTF8);
            }

            // 同步寫入共用資料庫（EXE 與網頁共用 gm_operation_log）；失敗不影響主流程
            _ = Task.Run(() => DatabaseManager.Instance.WriteGmLogAsync(
                entry.Operator, action, target, detail, success, "exe"));

            LogUpdated?.Invoke();
            return Task.CompletedTask;
        }

        private void LoadTodayLogs()
        {
            try
            {
                string logFile = Path.Combine(_logDir, $"{DateTime.Today:yyyy-MM-dd}.log");
                if (!File.Exists(logFile)) return;
                var lines = File.ReadAllLines(logFile, Encoding.UTF8);
                foreach (var line in lines)
                {
                    _recentLogs.Add(new GmLogEntry
                    {
                        Time   = DateTime.Today,
                        Action = line,
                        Target = "",
                        Detail = ""
                    });
                }
            }
            catch { }
        }

        // 取得所有日誌檔列表
        public List<string> GetLogFiles()
        {
            var files = new List<string>();
            if (!Directory.Exists(_logDir)) return files;
            foreach (var f in Directory.GetFiles(_logDir, "*.log"))
                files.Add(Path.GetFileName(f));
            files.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));
            return files;
        }

        // 讀取指定日期的日誌
        public string ReadLogFile(string filename)
        {
            string path = Path.Combine(_logDir, filename);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
        }
    }
}
