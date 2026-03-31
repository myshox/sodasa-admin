using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace SQ_Email_Tools
{
    public class DatabaseManager
    {
        private static DatabaseManager _instance;
        public static DatabaseManager Instance => _instance ??= new DatabaseManager();

        private string _connectionString;
        private readonly string _cfgPath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppDomain.CurrentDomain.BaseDirectory,
            "connection.cfg");

        public bool IsConnected { get; private set; }

        // ── csalogin 欄位偵測快取（null=尚未偵測，true/false=偵測結果）──
        private bool? _csaloginHasId     = null;   // 是否有 id 欄位（自動遞增主鍵）
        private bool? _csaloginHasBelong = null;  // 是否有 Belong 欄位（輩份）
        private readonly SemaphoreSlim _schemaSem = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 偵測 csalogin 是否有 id 欄位，使用傳入的既有連線（避免額外開新連線）。
        /// 只偵測一次，之後傳回快取值。
        /// </summary>
        private async Task<bool> CsaloginHasIdAsync(MySqlConnection existingConn = null)
        {
            if (_csaloginHasId.HasValue) return _csaloginHasId.Value;
            await _schemaSem.WaitAsync();
            try
            {
                if (_csaloginHasId.HasValue) return _csaloginHasId.Value;
                bool ownsConn = existingConn == null;
                var conn = existingConn ?? GetConnection();
                try
                {
                    if (ownsConn) await conn.OpenAsync();
                    using var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                        "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='csalogin' AND COLUMN_NAME='id'", conn);
                    var r = await cmd.ExecuteScalarAsync();
                    _csaloginHasId = r != null && Convert.ToInt32(r) > 0;
                }
                finally { if (ownsConn) conn.Dispose(); }
            }
            catch { _csaloginHasId = false; }
            finally { _schemaSem.Release(); }
            return _csaloginHasId.Value;
        }

        /// <summary>偵測 csalogin 是否有 Belong 欄位（輩份），欄位不存在時查詢不選取，避免 Unknown column 錯誤。</summary>
        private async Task<bool> CsaloginHasBelongAsync(MySqlConnection existingConn = null)
        {
            if (_csaloginHasBelong.HasValue) return _csaloginHasBelong.Value;
            await _schemaSem.WaitAsync();
            try
            {
                if (_csaloginHasBelong.HasValue) return _csaloginHasBelong.Value;
                bool ownsConn = existingConn == null;
                var conn = existingConn ?? GetConnection();
                try
                {
                    if (ownsConn) await conn.OpenAsync();
                    using var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                        "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='csalogin' AND COLUMN_NAME='Belong'", conn);
                    var r = await cmd.ExecuteScalarAsync();
                    _csaloginHasBelong = r != null && Convert.ToInt32(r) > 0;
                }
                finally { if (ownsConn) conn.Dispose(); }
            }
            catch { _csaloginHasBelong = false; }
            finally { _schemaSem.Release(); }
            return _csaloginHasBelong.Value;
        }

        public string LoadSavedConnectionString()
        {
            if (File.Exists(_cfgPath))
                return File.ReadAllText(_cfgPath).Trim();
            return "Server=;Database=;User ID=;Password=;Connection Timeout=8;";
        }

        public void SaveConnectionString(string cs) => File.WriteAllText(_cfgPath, cs);

        // ══════════════════════════════════════════════════════════
        // 資料庫結構確保（首次連線後自動新增必要欄位）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 確保 paydata 表存在 lifetime_total 欄位（歷史總累積儲值，永不歸零）。
        /// MySQL 5.7 不支援 ADD COLUMN IF NOT EXISTS，故以 INFORMATION_SCHEMA 判斷後再 ALTER。
        /// </summary>
        public async Task EnsurePaydataLifetimeColumnAsync()
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                // 取得目前連線的資料庫名稱
                string dbName;
                using (var c = new MySqlCommand("SELECT DATABASE()", conn))
                    dbName = (await c.ExecuteScalarAsync())?.ToString() ?? "";

                // 檢查欄位是否存在
                using var chk = new MySqlCommand(@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'paydata' AND COLUMN_NAME = 'lifetime_total'", conn);
                chk.Parameters.AddWithValue("@db", dbName);
                long exists = Convert.ToInt64(await chk.ExecuteScalarAsync());

                if (exists == 0)
                {
                    using var alter = new MySqlCommand(
                        "ALTER TABLE paydata ADD COLUMN lifetime_total BIGINT NOT NULL DEFAULT 0", conn);
                    await alter.ExecuteNonQueryAsync();
                    await GmLogger.Instance.LogAsync("系統初始化", "paydata",
                        "新增 lifetime_total 欄位（歷史總累積儲值）", true);
                }
            }
            catch (Exception ex)
            {
                await GmLogger.Instance.LogAsync("系統初始化", "paydata",
                    "新增 lifetime_total 欄位失敗：" + ex.Message, false);
            }
        }

        public async Task<(bool ok, string error)> ConnectAsync(string connectionString)
        {
            if (!connectionString.Contains("Connection Timeout") && !connectionString.Contains("ConnectionTimeout"))
                connectionString = connectionString.TrimEnd(';') + ";Connection Timeout=8;";
            if (!connectionString.Contains("charset") && !connectionString.Contains("CharSet"))
                connectionString = connectionString.TrimEnd(';') + ";charset=utf8mb4;";

            _connectionString = connectionString;
            // 每次重新連線時清除欄位快取，確保重新偵測
            _csaloginHasId = null;
            _csaloginHasBelong = null;
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                IsConnected = conn.State == System.Data.ConnectionState.Open;
                if (IsConnected)
                {
                    SaveConnectionString(connectionString);
                    // 連線成功後立刻偵測欄位，快取結果，後續查詢不再額外開連線
                    await CsaloginHasIdAsync(conn);
                }
                return (IsConnected, null);
            }
            catch (Exception ex) { IsConnected = false; return (false, ex.Message); }
        }

        public MySqlConnection GetConnection() => new MySqlConnection(_connectionString);

        // ══════════════════════════════════════════════════════════
        // 玩家查詢
        // ══════════════════════════════════════════════════════════
        public async Task<List<PlayerInfo>> SearchPlayersAsync(string query, int limit = 300)
        {
            var list = new List<PlayerInfo>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            // 動態決定是否加入 id 欄位（傳入既有連線，避免多開連線）
            bool hasId = await CsaloginHasIdAsync(conn);
            string idCol = hasId ? ", c.`id` AS CharDbId" : ", 0 AS CharDbId";
            // limit <= 0 代表不限筆數
            string limitClause = limit > 0 ? $"LIMIT {limit}" : "";

            // 強制以 UTF-8 解讀：若資料表為 latin1/big5 但實際存的是 UTF-8 位元組，可避免顯示成亂碼或錯誤中文
            string nameUtf8  = "CONVERT(CONVERT(c.`Name` USING binary) USING utf8mb4)";
            string onameUtf8 = "CONVERT(CONVERT(c.OnlineName USING binary) USING utf8mb4)";
            string mnameUtf8 = "IFNULL(CONVERT(CONVERT(m.`Name` USING binary) USING utf8mb4),'')";
            string sql = string.IsNullOrWhiteSpace(query)
                ? $@"SELECT c.MasterId, {nameUtf8} AS `Name`, {onameUtf8} AS OnlineName, c.Online, c.LoginTime, c.ServerId,
                           IFNULL(p.point, 0)   AS PayTotal,
                           IFNULL(pet.cnt, 0)   AS PetCount,
                           {mnameUtf8}  AS MasterName
                           {idCol}
                    FROM csalogin c
                    LEFT JOIN paydata p          ON p.cdkey = c.`Name`
                    LEFT JOIN csaloginmaster m   ON m.Id    = c.MasterId
                    LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet
                           ON pet.cdkey = c.`Name`
                    ORDER BY c.Online DESC, c.LoginTime DESC {limitClause}"
                : $@"SELECT c.MasterId, {nameUtf8} AS `Name`, {onameUtf8} AS OnlineName, c.Online, c.LoginTime, c.ServerId,
                           IFNULL(p.point, 0)   AS PayTotal,
                           IFNULL(pet.cnt, 0)   AS PetCount,
                           {mnameUtf8}  AS MasterName
                           {idCol},
                           CASE WHEN m.`Name` = @exact OR c.OnlineName = @exact OR c.`Name` = @exact
                                THEN 0 ELSE 1 END AS _rank
                    FROM csalogin c
                    LEFT JOIN paydata p          ON p.cdkey = c.`Name`
                    LEFT JOIN csaloginmaster m   ON m.Id    = c.MasterId
                    LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet
                           ON pet.cdkey = c.`Name`
                    WHERE c.OnlineName LIKE @q OR c.`Name` LIKE @q OR m.`Name` LIKE @q
                    ORDER BY _rank ASC, c.Online DESC, c.LoginTime DESC {limitClause}";

            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(query))
            {
                cmd.Parameters.AddWithValue("@q", $"%{query}%");
                cmd.Parameters.AddWithValue("@exact", query);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new PlayerInfo
                {
                    MasterId   = reader["MasterId"] == DBNull.Value ? 0 : reader.GetInt32("MasterId"),
                    Account    = reader["Name"]?.ToString() ?? "",
                    OnlineName = reader["OnlineName"]?.ToString() ?? "",
                    IsOnline   = reader["Online"] != DBNull.Value && reader.GetInt32("Online") == 1,
                    LoginTime  = reader["LoginTime"]?.ToString() ?? "",
                    ServerId   = reader["ServerId"]?.ToString() ?? "",
                    PayTotal   = reader["PayTotal"] == DBNull.Value ? 0 : Convert.ToInt64(reader["PayTotal"]),
                    PetCount   = reader["PetCount"] == DBNull.Value ? 0 : Convert.ToInt32(reader["PetCount"]),
                    MasterName = reader["MasterName"]?.ToString() ?? "",
                    CharDbId   = reader["CharDbId"] == DBNull.Value ? 0 : Convert.ToInt32(reader["CharDbId"])
                });
            }
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 封禁管理（lock 表：Name=帳號, time=到期 unix 秒，0=永久）
        // ══════════════════════════════════════════════════════════
        public async Task<bool> BanPlayerAsync(string account, int endUnix, string reason)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "INSERT INTO `lock` (`Name`, `time`) VALUES (@name, @time) " +
                "ON DUPLICATE KEY UPDATE `time`=@time", conn);
            cmd.Parameters.AddWithValue("@name", account);
            cmd.Parameters.AddWithValue("@time", endUnix);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("封禁帳號", account, reason, true);
            return ok;
        }

        public async Task<bool> UnbanPlayerAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM `lock` WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("解封帳號", account, "", true);
            return ok;
        }

        public async Task<(bool isBanned, string endTime)> GetBanStatusAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("SELECT `time` FROM `lock` WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@name", account);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return (false, "");
            int t = r.GetInt32("time");
            string ts = t == 0 ? "永久" : DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
            return (true, ts);
        }

        // ══════════════════════════════════════════════════════════
        // 郵件：單一 / 批量 / 歷史 / 刪除
        // ══════════════════════════════════════════════════════════
        public async Task<List<MailRecord>> GetMailHistoryAsync(string cdkey)
        {
            var list = new List<MailRecord>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT * FROM maildata WHERE cdkey=@cdkey ORDER BY id DESC LIMIT 200", conn);
            cmd.Parameters.AddWithValue("@cdkey", cdkey);
            return await ReadMailRecords(cmd, cdkey);
        }

        public async Task<List<MailRecord>> GetAllMailHistoryAsync(
            string filterCdkey = "", int limit = 300)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            string sql = string.IsNullOrWhiteSpace(filterCdkey)
                ? $"SELECT * FROM maildata ORDER BY id DESC LIMIT {limit}"
                : $"SELECT * FROM maildata WHERE cdkey LIKE @q ORDER BY id DESC LIMIT {limit}";
            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(filterCdkey))
                cmd.Parameters.AddWithValue("@q", $"%{filterCdkey}%");
            return await ReadMailRecords(cmd, "");
        }

        private static async Task<List<MailRecord>> ReadMailRecords(MySqlCommand cmd, string defaultCdkey)
        {
            var list = new List<MailRecord>();
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                // data 欄位可能是 VARCHAR（遊戲儲存複雜格式如 "1234,5"），以字串讀取避免 FormatException
                string rawData = r["data"]?.ToString() ?? "";
                int.TryParse(rawData, out int dataInt);
                list.Add(new MailRecord
                {
                    Id        = r.GetInt32("id"),
                    Type      = r.GetInt32("type"),
                    Buff1     = r["buff1"]?.ToString() ?? "",
                    Buff2     = r["buff2"]?.ToString() ?? "",
                    Data      = dataInt,
                    RawData   = rawData,
                    SendTime  = r["sendtime"]  == DBNull.Value ? 0 : Convert.ToInt32(r["sendtime"]),
                    EndTime   = r["endtime"]   == DBNull.Value ? 0 : Convert.ToInt32(r["endtime"]),
                    CheckFlag = r["check"]     == DBNull.Value ? 0 : Convert.ToInt32(r["check"]),
                    Deleamill = r["deleamill"] == DBNull.Value ? 0 : Convert.ToInt32(r["deleamill"]),
                    Buff3     = r["buff3"]?.ToString() ?? "",
                    Cdkey     = defaultCdkey.Length > 0 ? defaultCdkey : (r["cdkey"]?.ToString() ?? "")
                });
            }
            return list;
        }

        public async Task<bool> SendMailAsync(SendMailRequest req)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            int qty = Math.Max(1, req.Quantity);
            const string sql = @"INSERT INTO maildata
                (type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3)
                VALUES (@type,@cdkey,@buff1,@buff2,@data,@sendtime,@endtime,0,0,@buff3)";
            int success = 0;
            for (int i = 0; i < qty; i++)
            {
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@type",     req.Type);
                cmd.Parameters.AddWithValue("@cdkey",    req.Cdkey);
                cmd.Parameters.AddWithValue("@buff1",    req.Buff1);
                cmd.Parameters.AddWithValue("@buff2",    req.Buff2);
                cmd.Parameters.AddWithValue("@data",     req.Data);
                cmd.Parameters.AddWithValue("@sendtime", req.StartTime);
                cmd.Parameters.AddWithValue("@endtime",  req.EndTime);
                cmd.Parameters.AddWithValue("@buff3",    req.Buff3);
                if (await cmd.ExecuteNonQueryAsync() > 0) success++;
            }
            bool ok = success == qty;
            if (success > 0) await GmLogger.Instance.LogAsync("發送郵件", req.Cdkey,
                $"道具:{req.Data} 類型:{req.Type} 數量:{qty} 標題:{req.Buff1}", true);
            return ok;
        }

        /// <summary>批量「僅在線」：Online=1 或 LoginTime 6 小時內，再加同主帳號（MasterId）所有角色（不限 IP）。</summary>
        private static async Task<List<string>> LoadOnlineBatchAccountNamesAsync(MySqlConnection conn)
        {
            const string sql = @"
SELECT DISTINCT c.`Name`
FROM csalogin c
WHERE (c.Online = 1 OR c.LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR))
   OR (
        c.MasterId IS NOT NULL
        AND EXISTS (
          SELECT 1 FROM csalogin o
          WHERE (o.Online = 1 OR o.LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR))
            AND o.MasterId = c.MasterId
        )
      )";
            var list = new List<string>();
            try
            {
                using var cmd = new MySqlCommand(sql, conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(r.GetString(0));
            }
            catch
            {
                using var cmd = new MySqlCommand("SELECT `Name` FROM csalogin WHERE Online=1 OR LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR) ORDER BY `Name`", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(r.GetString(0));
            }
            return list;
        }

        public async Task<(int success, int fail)> BatchSendMailAsync(
            SendMailRequest template,
            IProgress<(int done, int total, string account, bool ok)> progress,
            CancellationToken ct,
            int batchSize = 100,
            HashSet<string>? excludeSet = null,
            bool onlineOnly = false)
        {
            // 取帳號清單（onlineOnly：Online=1 ＋ 同 MasterId＋同 IP 之所有角色，與 WebApi 一致）
            var allAccounts = new List<string>();
            using (var connA = GetConnection())
            {
                await connA.OpenAsync();
                if (onlineOnly)
                {
                    allAccounts = await LoadOnlineBatchAccountNamesAsync(connA);
                }
                else
                {
                    using var cmdA = new MySqlCommand("SELECT `Name` FROM csalogin ORDER BY `Name`", connA);
                    using var rA   = await cmdA.ExecuteReaderAsync();
                    while (await rA.ReadAsync()) allAccounts.Add(rA.GetString(0));
                }
            }

            // 套用排除名單
            if (excludeSet != null && excludeSet.Count > 0)
                allAccounts = allAccounts.Where(a => !excludeSet.Contains(a)).ToList();

            int qty   = Math.Max(1, template.Quantity);
            int total = allAccounts.Count, success = 0, fail = 0;
            batchSize = Math.Max(1, Math.Min(batchSize, 500));

            using var conn = GetConnection();
            await conn.OpenAsync();

            for (int i = 0; i < allAccounts.Count; i += batchSize)
            {
                if (ct.IsCancellationRequested) break;

                var batch     = allAccounts.Skip(i).Take(batchSize).ToList();
                bool batchOk  = false;
                try
                {
                    // 批量 INSERT：只有 cdkey 逐列不同，其餘參數共用
                    var valueParts = new System.Collections.Generic.List<string>();
                    using var cmd = new MySqlCommand();
                    cmd.Connection = conn;
                    cmd.Parameters.AddWithValue("@type",     template.Type);
                    cmd.Parameters.AddWithValue("@buff1",    template.Buff1 ?? "");
                    cmd.Parameters.AddWithValue("@buff2",    template.Buff2 ?? "");
                    cmd.Parameters.AddWithValue("@data",     template.Data);
                    cmd.Parameters.AddWithValue("@sendtime", template.StartTime);
                    cmd.Parameters.AddWithValue("@endtime",  template.EndTime);
                    cmd.Parameters.AddWithValue("@buff3",    template.Buff3 ?? "");

                    for (int j = 0; j < batch.Count; j++)
                    {
                        string pName = $"@ck{j}";
                        cmd.Parameters.AddWithValue(pName, batch[j]);
                        for (int q = 0; q < qty; q++)
                            valueParts.Add($"(@type,{pName},@buff1,@buff2,@data,@sendtime,@endtime,0,0,@buff3)");
                    }
                    cmd.CommandText =
                        "INSERT INTO maildata (type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3) VALUES "
                        + string.Join(",", valueParts);

                    int rows   = await cmd.ExecuteNonQueryAsync();
                    int expect = batch.Count * qty;
                    batchOk    = rows >= expect;
                    if (batchOk) success += batch.Count;
                    else         { success += rows / Math.Max(1, qty); fail += batch.Count - rows / Math.Max(1, qty); }
                }
                catch (Exception batchEx)
                {
                    fail += batch.Count;
                    // 通知呼叫方這批次的錯誤訊息
                    progress?.Report((Math.Min(i + batchSize, total), total,
                        $"[DB錯誤] {batchEx.Message}", false));
                }

                int done = Math.Min(i + batchSize, total);
                progress?.Report((done, total, batch[^1], batchOk));
            }

            string targetDesc = (excludeSet != null && excludeSet.Count > 0)
                ? $"全服（排除 {excludeSet.Count} 人）共 {total} 人"
                : $"全服 {total} 人";
            await GmLogger.Instance.LogAsync("批量發送",
                targetDesc,
                $"道具:{template.Data} 數量:{qty}份 標題:{template.Buff1} 成功:{success} 失敗:{fail} 批次:{batchSize}",
                success > 0);
            return (success, fail);
        }

        public async Task<bool> DeleteMailAsync(int mailId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM maildata WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("@id", mailId);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("刪除郵件", $"ID:{mailId}", "", true);
            return ok;
        }

        // ── 清除玩家郵件（軟刪除 deleamill=1）────────────────────────
        // account 空字串 = 全部玩家，onlineOnly=true 只清在線玩家
        public async Task<int> ClearPlayerMailAsync(string account, bool unclaimedOnly, bool onlineOnly = false)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            string accountFilter;
            if (!string.IsNullOrWhiteSpace(account))
                accountFilter = "cdkey=@acc AND ";
            else if (onlineOnly)
                accountFilter = "cdkey IN (SELECT `Name` FROM csalogin WHERE Online=1) AND ";
            else
                accountFilter = "";

            string checkFilter = unclaimedOnly ? " AND `check`=0" : "";
            string sql = $"UPDATE maildata SET deleamill=1 WHERE {accountFilter}deleamill=0{checkFilter}";

            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(account))
                cmd.Parameters.AddWithValue("@acc", account.Trim());

            int count = await cmd.ExecuteNonQueryAsync();
            string scope = !string.IsNullOrWhiteSpace(account) ? account : (onlineOnly ? "在線玩家" : "全部玩家");
            string type  = unclaimedOnly ? "未領取郵件" : "全部郵件";
            await GmLogger.Instance.LogAsync("清除郵件", scope, $"清除{type} {count} 封", true);
            return count;
        }

        /// <summary>取得玩家帳號清單（用於批量操作選擇）</summary>
        public async Task<List<(string Account, string Name, bool Online)>> GetPlayerListAsync(bool onlineOnly)
        {
            var list = new List<(string, string, bool)>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            string where = onlineOnly ? " WHERE Online=1" : "";
            string sql = $"SELECT `Name`, `NickName`, Online FROM csalogin{where} ORDER BY Online DESC, `Name` ASC LIMIT 3000";
            using var cmd = new MySqlCommand(sql, conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                string account = r.IsDBNull(0) ? "" : r.GetString(0);
                string name    = r.IsDBNull(1) ? "" : r.GetString(1);
                bool   online  = !r.IsDBNull(2) && r.GetInt32(2) == 1;
                if (!string.IsNullOrEmpty(account))
                    list.Add((account, name, online));
            }
            return list;
        }

        /// <summary>批量清除指定多個帳號的郵件</summary>
        public async Task<int> ClearPlayerMailBatchAsync(IEnumerable<string> accounts, bool unclaimedOnly)
        {
            var accs = accounts.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
            if (accs.Count == 0) return 0;

            using var conn = GetConnection();
            await conn.OpenAsync();

            string checkFilter = unclaimedOnly ? " AND `check`=0" : "";
            // 用 IN 一次清除多個帳號
            var pNames = accs.Select((_, i) => $"@a{i}").ToList();
            string sql = $"UPDATE maildata SET deleamill=1 WHERE cdkey IN ({string.Join(",", pNames)}) AND deleamill=0{checkFilter}";

            using var cmd = new MySqlCommand(sql, conn);
            for (int i = 0; i < accs.Count; i++)
                cmd.Parameters.AddWithValue($"@a{i}", accs[i]);

            int count = await cmd.ExecuteNonQueryAsync();
            string type = unclaimedOnly ? "未領取郵件" : "全部郵件";
            await GmLogger.Instance.LogAsync("批量清除郵件", $"{accs.Count} 個玩家", $"清除{type} {count} 封", true);
            return count;
        }

        /// <summary>
        /// 修正 endtime=0 的未領取郵件，延長到 30 天後到期
        /// （有些遊戲會把 endtime=0 視為已過期，導致無法領取）
        /// </summary>
        public async Task<int> FixMailEndtimeAsync(string account = "")
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            int futureTs = (int)DateTimeOffset.Now.AddDays(30).ToUnixTimeSeconds();
            string accWhere = string.IsNullOrWhiteSpace(account) ? "" : "AND cdkey=@acc";
            string sql = $"UPDATE maildata SET endtime=@end WHERE (endtime IS NULL OR endtime=0) AND `check`=0 AND deleamill=0 {accWhere}";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@end", futureTs);
            if (!string.IsNullOrWhiteSpace(account)) cmd.Parameters.AddWithValue("@acc", account);
            int count = await cmd.ExecuteNonQueryAsync();
            await GmLogger.Instance.LogAsync("修正endtime", string.IsNullOrWhiteSpace(account) ? "全服" : account, $"設定endtime={futureTs} 共{count}封", true);
            return count;
        }

        /// <summary>
        /// 取得「已領取」vs「未領取」的郵件樣本，用於診斷可領取格式
        /// </summary>
        public async Task<(List<MailRecord> claimed, List<MailRecord> unclaimed)> GetMailDiagnoseAsync(string cdkey)
        {
            var claimed   = new List<MailRecord>();
            var unclaimed = new List<MailRecord>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd1 = new MySqlCommand("SELECT * FROM maildata WHERE cdkey=@ck AND `check`=1 AND deleamill=0 ORDER BY id DESC LIMIT 10", conn);
            cmd1.Parameters.AddWithValue("@ck", cdkey);
            claimed = await ReadMailRecords(cmd1, cdkey);

            using var cmd2 = new MySqlCommand("SELECT * FROM maildata WHERE cdkey=@ck AND `check`=0 AND deleamill=0 ORDER BY id DESC LIMIT 10", conn);
            cmd2.Parameters.AddWithValue("@ck", cdkey);
            unclaimed = await ReadMailRecords(cmd2, cdkey);
            return (claimed, unclaimed);
        }

        /// <summary>
        /// 修正舊版發送的郵件：
        ///   1. 把標題為「[GM] ...」格式的 buff1/buff2 改為「道具#ID」格式
        ///   2. 用 items.xlsx 已載入的道具描述回填 buff3（讓遊戲能顯示道具說明）
        ///   3. 從資料庫已有非空 buff3 記錄補救其餘空 buff3
        /// </summary>
        public async Task<(int titleFixed, int buff3Fixed, int totalEmpty)> FixOldMailsAsync(
            string account = "",
            IEnumerable<(int ItemId, string Desc)>? itemDescs = null)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            string accFilter = string.IsNullOrWhiteSpace(account) ? "" : "AND cdkey=@acc";

            // 1. 統計 buff3 為空且未領取的郵件數
            using var cnt = new MySqlCommand(
                $"SELECT COUNT(*) FROM maildata WHERE `check`=0 AND deleamill=0 AND (buff3 IS NULL OR buff3='') {accFilter}", conn);
            if (!string.IsNullOrWhiteSpace(account)) cnt.Parameters.AddWithValue("@acc", account);
            int totalEmpty = Convert.ToInt32(await cnt.ExecuteScalarAsync());

            // 2. 修正 buff1/buff2（舊式通用標題 → 道具#ID）
            string updTitle = string.IsNullOrWhiteSpace(account)
                ? @"UPDATE maildata SET buff1=CONCAT('道具#',data), buff2=CONCAT('道具#',data)
                    WHERE `check`=0 AND deleamill=0
                    AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%'
                         OR buff1='[GM] 批量發送' OR buff1 LIKE '[GM] %')"
                : @"UPDATE maildata SET buff1=CONCAT('道具#',data), buff2=CONCAT('道具#',data)
                    WHERE `check`=0 AND deleamill=0 AND cdkey=@acc2
                    AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%'
                         OR buff1='[GM] 批量發送' OR buff1 LIKE '[GM] %')";
            using var fixTitle = new MySqlCommand(updTitle, conn);
            if (!string.IsNullOrWhiteSpace(account)) fixTitle.Parameters.AddWithValue("@acc2", account);
            int titleFixed = await fixTitle.ExecuteNonQueryAsync();

            int buff3Fixed = 0;

            // 3a. 用 items.xlsx 道具名稱逐一回填 buff3
            // ★ 不再只修空的，改為「只要 data 對得上就強制覆蓋」—— 確保所有道具郵件都有正確 buff3
            if (itemDescs != null)
            {
                string accWhere = string.IsNullOrWhiteSpace(account) ? "" : "AND cdkey=@acc3";
                foreach (var (itemId, itemName) in itemDescs)
                {
                    if (string.IsNullOrWhiteSpace(itemName) || itemId == 0) continue;
                    // 強制更新：只要 data=itemId 且未領取，不管 buff3 是否為空，都改成正確名稱
                    using var upd = new MySqlCommand(
                        $"UPDATE maildata SET buff3=@name WHERE data=@id AND `check`=0 AND deleamill=0 {accWhere}", conn);
                    upd.Parameters.AddWithValue("@name", itemName);
                    upd.Parameters.AddWithValue("@id",   itemId);
                    if (!string.IsNullOrWhiteSpace(account)) upd.Parameters.AddWithValue("@acc3", account);
                    try { buff3Fixed += await upd.ExecuteNonQueryAsync(); } catch { }
                }
            }

            // 3b. 從資料庫現有非空 buff3 記錄補救剩餘的
            string updBuff3 = string.IsNullOrWhiteSpace(account)
                ? @"UPDATE maildata m
                    JOIN (
                        SELECT data, buff3 FROM maildata
                        WHERE buff3 IS NOT NULL AND buff3 != ''
                        GROUP BY data, buff3 ORDER BY COUNT(*) DESC
                    ) ref ON m.data = ref.data
                    SET m.buff3 = ref.buff3
                    WHERE m.`check`=0 AND m.deleamill=0 AND (m.buff3 IS NULL OR m.buff3='')"
                : @"UPDATE maildata m
                    JOIN (
                        SELECT data, buff3 FROM maildata
                        WHERE buff3 IS NOT NULL AND buff3 != ''
                        GROUP BY data, buff3 ORDER BY COUNT(*) DESC
                    ) ref ON m.data = ref.data
                    SET m.buff3 = ref.buff3
                    WHERE m.`check`=0 AND m.deleamill=0 AND (m.buff3 IS NULL OR m.buff3='')
                    AND m.cdkey=@acc4";
            using var fixBuff3 = new MySqlCommand(updBuff3, conn);
            if (!string.IsNullOrWhiteSpace(account)) fixBuff3.Parameters.AddWithValue("@acc4", account);
            try { buff3Fixed += await fixBuff3.ExecuteNonQueryAsync(); } catch { }

            await GmLogger.Instance.LogAsync("修正郵件", string.IsNullOrWhiteSpace(account) ? "全服" : account,
                $"標題修正:{titleFixed} buff3回填:{buff3Fixed} 空郵件:{totalEmpty}", true);
            return (titleFixed, buff3Fixed, totalEmpty);
        }

        // ══════════════════════════════════════════════════════════
        // 道具直接給予（itempetgetdata 表）
        // 對應 [gm additem 道具ID 數量 帳號] 指令
        // 伺服器在玩家重新登入時處理此表，直接放入背包
        // ══════════════════════════════════════════════════════════
        public async Task<bool> GiveItemDirectAsync(
            string account, string playerName,
            int itemId, string itemName, int quantity)
        {
            // 透過 maildata 給予道具（與個人發送相同機制，玩家從信箱領取即可）
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var req = new SendMailRequest
            {
                Cdkey     = account,
                Type      = 1,
                Buff1     = $"[GM] {itemName}",
                Buff2     = "GM 直接發放道具",
                Data      = itemId,
                StartTime = (int)now,
                EndTime   = (int)(now + 30L * 24 * 3600),
                Buff3     = itemName,   // buff3 = 道具名稱（遊戲判斷用）
                Quantity  = quantity
            };
            bool ok = await SendMailAsync(req);
            if (ok) await GmLogger.Instance.LogAsync("直接給予道具(郵件)", account,
                $"道具:{itemName}(ID:{itemId}) 數量:{quantity} 玩家:{playerName}", true);
            return ok;
        }

        // ══════════════════════════════════════════════════════════
        // 寵物直接給予（capturepet 表）
        // 對應 [gm petmake 寵物ID 數量 帳號] 指令
        // unicode 格式：{unix秒}i{隨機4位}，和遊戲伺服器格式一致
        // ══════════════════════════════════════════════════════════
        public async Task<bool> GivePetDirectAsync(
            string account, string gmName,
            int petId, string petName, int petType,
            int level, int hp, int attack, int def, int quick)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // 計算綜合戰力（sum）— 根據觀察到的 capturepet 數據估算
            double sum = hp * 0.5 + (attack + def + quick) * 0.5;

            // 產生 unicode：格式與遊戲伺服器一致
            long ts  = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int  rnd = new Random().Next(1000, 9999);
            string unicode = $"{ts}i{rnd}";

            const string sql = @"INSERT INTO capturepet
                (unicode,id,name,type,lv,hp,attack,def,quick,sum,author,cdkey,`check`)
                VALUES (@uni,@id,@name,@type,@lv,@hp,@att,@def,@qck,@sum,@author,@cdkey,0)";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uni",    unicode);
            cmd.Parameters.AddWithValue("@id",     petId);
            cmd.Parameters.AddWithValue("@name",   petName);
            cmd.Parameters.AddWithValue("@type",   petType);
            cmd.Parameters.AddWithValue("@lv",     level);
            cmd.Parameters.AddWithValue("@hp",     hp);
            cmd.Parameters.AddWithValue("@att",    attack);
            cmd.Parameters.AddWithValue("@def",    def);
            cmd.Parameters.AddWithValue("@qck",    quick);
            cmd.Parameters.AddWithValue("@sum",    sum);
            cmd.Parameters.AddWithValue("@author", gmName);
            cmd.Parameters.AddWithValue("@cdkey",  account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("直接給予寵物", account,
                $"寵物:{petName}(ID:{petId}) 等級:{level} 數值:HP{hp}/攻{attack}/防{def}/速{quick}", true);
            return ok;
        }

        // ══════════════════════════════════════════════════════════
        // 貨幣操作（csalogin 表）
        //   VipPoint = 金幣（遊戲內主要貨幣）
        //   PetPoint = 水晶（次要貨幣）
        //   PayPoint = 充值點（充值獲得）
        //   RmbPoint = R幣
        //
        // 石幣 / 聲望 / 戰點 存於伺服器二進位角色檔案，無法從 DB 讀取
        // ══════════════════════════════════════════════════════════

        /// <summary>讀取玩家的所有可讀貨幣</summary>
        public async Task<PlayerCurrencies> GetCurrenciesAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT VipPoint, PetPoint, PayPoint, RmbPoint FROM csalogin WHERE Name=@cdkey", conn);
            cmd.Parameters.AddWithValue("@cdkey", account);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return new PlayerCurrencies();
            return new PlayerCurrencies
            {
                Gold    = Convert.ToInt64(r["VipPoint"]),
                Crystal = Convert.ToInt64(r["PetPoint"]),
                PayPoint= Convert.ToInt64(r["PayPoint"]),
                RmbPoint= Convert.ToInt64(r["RmbPoint"]),
            };
        }

        /// <summary>設定金幣（VipPoint）— 覆蓋式</summary>
        public async Task<(bool ok, long newBalance)> SetGoldAsync(string account, long newValue)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csalogin SET VipPoint=@v WHERE Name=@cdkey", conn);
            cmd.Parameters.AddWithValue("@v",     newValue);
            cmd.Parameters.AddWithValue("@cdkey", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("修改金幣", account, $"金幣設為 {newValue:N0}", true);
            return (ok, newValue);
        }

        /// <summary>增減金幣（VipPoint）— 相對增減</summary>
        public async Task<(bool ok, long newBalance)> GiveGoldAsync(string account, string _, long amount)
        {
            var cur = await GetCurrenciesAsync(account);
            long next = Math.Max(0, cur.Gold + amount);
            return await SetGoldAsync(account, next);
        }

        // 舊版 int 介面保持相容
        public async Task<(bool ok, int newBalance)> GiveGoldAsync(string account, string name, int amount)
        {
            var (ok, bal) = await GiveGoldAsync(account, name, (long)amount);
            return (ok, (int)Math.Min(int.MaxValue, bal));
        }

        /// <summary>設定水晶（PetPoint）</summary>
        public async Task<(bool ok, long newBalance)> SetCrystalAsync(string account, long newValue)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csalogin SET PetPoint=@v WHERE Name=@cdkey", conn);
            cmd.Parameters.AddWithValue("@v",     newValue);
            cmd.Parameters.AddWithValue("@cdkey", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("修改水晶", account, $"水晶設為 {newValue:N0}", true);
            return (ok, newValue);
        }

        /// <summary>增減水晶（PetPoint）— 相對增減</summary>
        public async Task<(bool ok, long newBalance)> GiveCrystalAsync(string account, long amount)
        {
            var cur = await GetCurrenciesAsync(account);
            long next = Math.Max(0, cur.Crystal + amount);
            return await SetCrystalAsync(account, next);
        }

        public async Task<int> GetGoldAsync(string account)
        {
            var c = await GetCurrenciesAsync(account);
            return (int)Math.Min(int.MaxValue, c.Gold);
        }

        /// <summary>
        /// 給予儲值（完整）：
        ///   twdAmount  → paydata.point  += twdAmount   （台幣，遊戲累積充值獎勵系統讀取此值）
        ///   goldAmount → csalogin.VipPoint += goldAmount（含套餐加成的實際金幣，若 giveGold=true）
        ///
        /// paydata.point 單位為【台幣（NT$）】，與遊戲面板顯示一致（1循環 = NT$25,000）。
        /// </summary>
        public async Task<bool> SetPayTotalAsync(string account, long twdAmount, long goldAmount)
        {
            return await AdjustPayDataPointAsync(account, twdAmount, goldAmount, giveGold: true);
        }

        /// <summary>
        /// 調整累積充值記錄（可選是否同時給予金幣）：
        // ── 累積充值獎勵系統常數 ──────────────────────────────────────────
        // 每輪最高門檻（NT$25,000），達到後循環歸零進入下一輪
        private const long CYCLE_MAX = 25_000L;

        // 每輪 11 個獎勵門檻（bit 0 ~ bit 10），均為「當前輪次累積台幣」
        // 玩家領完第 11 個（25000/25000），伺服器自動歸零進下一輪
        private static readonly long[] RewardTiers = {
              100,   300,   500,  1_000,  3_000,   // tiers 1-5  (bit 0-4)
            5_000, 7_000, 10_000, 15_000, 20_000,  // tiers 6-10 (bit 5-9)
           25_000,                                  // tier 11    (bit 10) ← 循環終點
        };

        // 全部 11 個 bit 都設 1 = 0b11111111111 = 2047
        private const long ALL_TIERS_BITS = (1L << 11) - 1;

        /// <summary>
        /// 根據「當前輪次進度（0 ~ 25000）」計算應設定的 check bitmask。
        /// 門檻 ≤ cyclePoint 的 bit 全部設為 1。
        /// </summary>
        private static long CalcCheckBits(long cyclePoint)
        {
            long bits = 0;
            for (int i = 0; i < RewardTiers.Length; i++)
                if (cyclePoint >= RewardTiers[i])
                    bits |= (1L << i);
            return bits;
        }

        /// <summary>
        /// 調整累積充值記錄（含自動循環進位）：
        ///   twdAmount  ─ 本次增加的台幣金額
        ///   goldAmount ─ 同步發放的金幣（含套餐加成）
        ///   giveGold   ─ true = 同時發放金幣
        ///
        /// 循環規則：
        ///   每 25,000 NT$ 為一輪。累積「嚴格超過」25,000 才算完成一輪。
        ///   剛好等於 25,000 仍屬當前輪（玩家可領取第 11 個獎勵）。
        ///   lifetime_total 永不歸零（歷史累計）。
        /// </summary>
        public async Task<bool> AdjustPayDataPointAsync(string account, long twdAmount, long goldAmount, bool giveGold)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            bool ok = true;

            if (giveGold)
            {
                using var cmdLogin = new MySqlCommand(
                    "UPDATE csalogin SET VipPoint = VipPoint + @gold, PayTotal = PayTotal + @twd WHERE `Name` = @cdkey", conn);
                cmdLogin.Parameters.AddWithValue("@gold",  goldAmount);
                cmdLogin.Parameters.AddWithValue("@twd",   twdAmount);
                cmdLogin.Parameters.AddWithValue("@cdkey", account);
                ok = await cmdLogin.ExecuteNonQueryAsync() > 0;
            }

            // 查現有 point（當前輪次進度）與 totalcheck（已完成輪數）
            long currentPoint = 0, currentTotalCheck = 0;
            using (var cmdGet = new MySqlCommand(
                "SELECT IFNULL(point,0) AS pt, IFNULL(totalcheck,0) AS tc FROM paydata WHERE cdkey=@cdkey", conn))
            {
                cmdGet.Parameters.AddWithValue("@cdkey", account);
                using var r = await cmdGet.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    currentPoint      = Convert.ToInt64(r["pt"]);
                    currentTotalCheck = Convert.ToInt64(r["tc"]);
                }
            }

            long rawTotal = currentPoint + twdAmount;
            // ★ 剛好等於 25000 仍屬當前輪（玩家應可領取第 11 個獎勵）
            //   只有「嚴格超過 25000」才算完成一輪並進入下一輪。
            //   用 (rawTotal - 1) / CYCLE_MAX 實現：
            //     25000 → 24999/25000 = 0（留在當前輪，progress=25000）
            //     25001 → 25000/25000 = 1（進入下一輪，progress=1）
            long completedCycles = rawTotal > 0 ? (rawTotal - 1) / CYCLE_MAX : 0;
            long newCyclePoint   = rawTotal - completedCycles * CYCLE_MAX;
            long newTotalCheck   = currentTotalCheck + completedCycles;

            // ⚠ check 欄位【永遠不由 GM 工具設定】
            //   遊戲伺服器才會在玩家點「領取」時寫入 check bits。
            //   GM 只負責設定正確的 point（進度）與 totalcheck（完成輪數）。
            //   這樣新循環開始後，玩家就能看到可點的「領取」按鈕。

            if (completedCycles > 0)
            {
                // ✅ 跨越循環：point 設為餘數，totalcheck 累加
                // ⚠ check 欄位不動，由遊戲伺服器自行管理（設 0 反而觸發自動領取 bug）
                using var cmdPay = new MySqlCommand(@"
                    INSERT INTO paydata (cdkey, point, time, `check`, totalcheck, lifetime_total)
                    VALUES (@cdkey, @newpt, NOW(), 0, @tc, @lt)
                    ON DUPLICATE KEY UPDATE
                        point          = @newpt,
                        totalcheck     = @tc,
                        lifetime_total = lifetime_total + @twd", conn);
                cmdPay.Parameters.AddWithValue("@cdkey", account);
                cmdPay.Parameters.AddWithValue("@newpt", newCyclePoint);
                cmdPay.Parameters.AddWithValue("@tc",    newTotalCheck);
                cmdPay.Parameters.AddWithValue("@twd",   twdAmount);
                cmdPay.Parameters.AddWithValue("@lt",    twdAmount);
                await cmdPay.ExecuteNonQueryAsync();
            }
            else
            {
                // ✅ 同一輪：直接加 point，check 不動（由遊戲伺服器管理）
                using var cmdPay = new MySqlCommand(@"
                    INSERT INTO paydata (cdkey, point, time, `check`, totalcheck, lifetime_total)
                    VALUES (@cdkey, @twd, NOW(), 0, 0, @twd)
                    ON DUPLICATE KEY UPDATE
                        point          = point + @twd,
                        lifetime_total = lifetime_total + @twd", conn);
                cmdPay.Parameters.AddWithValue("@cdkey", account);
                cmdPay.Parameters.AddWithValue("@twd",   twdAmount);
                await cmdPay.ExecuteNonQueryAsync();
            }

            string cycleInfo = completedCycles > 0
                ? $"完成{completedCycles}輪循環，新輪次進度 NT${newCyclePoint:N0}/25,000，check 歸零"
                : $"輪次進度 NT${rawTotal:N0}/25,000";
            string detail = $"台幣 +NT${twdAmount:N0}，{cycleInfo}"
                          + (giveGold ? $"，金幣 +{goldAmount:N0}" : "，不發金幣");

            // ★ 寫入充值記錄（讓充值記錄查詢可見 GM 補單）
            try
            {
                string productName = giveGold
                    ? $"GM補單（+NT${twdAmount:N0} / +{goldAmount:N0}金幣）"
                    : $"GM補單（僅累儲 +NT${twdAmount:N0}）";
                string orderNo = $"GM-{DateTime.Now:yyyyMMddHHmmss}-{account[..Math.Min(account.Length,8)]}";
                // amount 欄位存元寶；user_id 查 game_users，找不到用 0
                long yuanbaoAmt = giveGold ? goldAmount : twdAmount * 100;
                using var cmdOrder = new MySqlCommand(@"
                    INSERT INTO recharge_orders
                        (order_no, user_id, role_name, product_name, amount, status, created_at)
                    VALUES (@ord,
                            IFNULL((SELECT id FROM game_users WHERE username=@role LIMIT 1), 0),
                            @role, @prod, @amt, 'completed', NOW())", conn);
                cmdOrder.Parameters.AddWithValue("@ord",  orderNo);
                cmdOrder.Parameters.AddWithValue("@role", account);
                cmdOrder.Parameters.AddWithValue("@prod", productName);
                cmdOrder.Parameters.AddWithValue("@amt",  yuanbaoAmt);
                await cmdOrder.ExecuteNonQueryAsync();
            }
            catch { /* recharge_orders 表結構不符時靜默忽略 */ }

            await GmLogger.Instance.LogAsync("給予儲值", account, detail, ok);
            return ok;
        }

        /// <summary>
        /// 修復循環顯示（針對舊資料 point > 25000 的情況）：
        ///   - 若 point > 25000（嚴格超過），才自動進位
        ///   - 剛好等於 25000 不進位，玩家仍可領取最後一個獎勵
        /// </summary>
        public async Task<bool> FixPaydataCheckAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            long currentPoint = 0, currentTotalCheck = 0;
            using (var cmdGet = new MySqlCommand(
                "SELECT IFNULL(point,0) AS pt, IFNULL(totalcheck,0) AS tc FROM paydata WHERE cdkey=@cdkey", conn))
            {
                cmdGet.Parameters.AddWithValue("@cdkey", account);
                using var r = await cmdGet.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return false;
                currentPoint      = Convert.ToInt64(r["pt"]);
                currentTotalCheck = Convert.ToInt64(r["tc"]);
            }

            // 與 AdjustPayDataPointAsync 相同：25000 留在當前輪，25001+ 才進位
            long completedCycles = currentPoint > 0 ? (currentPoint - 1) / CYCLE_MAX : 0;
            long newCyclePoint   = currentPoint - completedCycles * CYCLE_MAX;
            long newTotalCheck   = currentTotalCheck + completedCycles;

            // check 歸零：讓玩家在新輪次可以重新點「領取」
            using var cmdFix = new MySqlCommand(@"
                UPDATE paydata
                SET point      = @newpt,
                    `check`    = 0,
                    totalcheck = @tc
                WHERE cdkey = @cdkey", conn);
            cmdFix.Parameters.AddWithValue("@newpt", newCyclePoint);
            cmdFix.Parameters.AddWithValue("@tc",    newTotalCheck);
            cmdFix.Parameters.AddWithValue("@cdkey", account);
            int rows = await cmdFix.ExecuteNonQueryAsync();

            await GmLogger.Instance.LogAsync("修復循環check", account,
                $"舊point={currentPoint:N0}→新輪次point={newCyclePoint:N0}，check 歸零，完成{completedCycles}輪", rows > 0);
            return rows > 0;
        }

        /// <summary>
        /// 重置累積充值進度（僅 GM 修正用）：
        ///   - paydata.point    → 0（當前循環進度歸零）
        ///   - paydata.check    → 0（本循環已領取獎勵標記清除）
        ///   - paydata.totalcheck → 0
        ///   - paydata.lifetime_total 保留不動（永不歸零的歷史記錄）
        ///   - csalogin.PayTotal 同步歸零
        /// </summary>
        public async Task<bool> ResetPaydataProgressAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand(@"
                UPDATE paydata
                SET point = 0, `check` = 0, totalcheck = 0
                WHERE cdkey = @cdkey", conn);
            cmd.Parameters.AddWithValue("@cdkey", account);
            int rows = await cmd.ExecuteNonQueryAsync();

            using var cmdLogin = new MySqlCommand(
                "UPDATE csalogin SET PayTotal = 0 WHERE `Name` = @cdkey", conn);
            cmdLogin.Parameters.AddWithValue("@cdkey", account);
            await cmdLogin.ExecuteNonQueryAsync();

            await GmLogger.Instance.LogAsync("重置累儲進度", account,
                "paydata.point / check / totalcheck 歸零；lifetime_total 保留不動", rows > 0);
            return rows > 0;
        }

        // ══════════════════════════════════════════════════════════
        // 發放累積獎勵（check: 0=待領, 1=已領）含防呆
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 標記第 N 輪累積獎勵為已發放（check 0→1）。
        /// 防呆：check 必須為 0，且 totalcheck > 0，否則拒絕。
        /// 回傳：("ok", 輪次) 或 ("already_claimed"/"no_cycle"/"not_found", 0)
        /// </summary>
        public async Task<(string status, long cycle)> ClaimPaydataRewardAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();
            try
            {
                int  ck = 1; long tc = 0;
                using (var cmdGet = new MySqlCommand(
                    "SELECT IFNULL(`check`,1) ck, IFNULL(totalcheck,0) tc FROM paydata WHERE cdkey=@a FOR UPDATE",
                    conn, (MySqlConnector.MySqlTransaction)tx))
                {
                    cmdGet.Parameters.AddWithValue("@a", account);
                    using var r = await cmdGet.ExecuteReaderAsync();
                    if (!await r.ReadAsync()) { await tx.RollbackAsync(); return ("not_found", 0); }
                    ck = Convert.ToInt32(r["ck"]);
                    tc = Convert.ToInt64(r["tc"]);
                }
                if (tc == 0) { await tx.RollbackAsync(); return ("no_cycle", 0); }
                if (ck != 0) { await tx.RollbackAsync(); return ("already_claimed", tc); }

                using var cmdUp = new MySqlCommand(
                    "UPDATE paydata SET `check`=1 WHERE cdkey=@a",
                    conn, (MySqlConnector.MySqlTransaction)tx);
                cmdUp.Parameters.AddWithValue("@a", account);
                await cmdUp.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                await GmLogger.Instance.LogAsync("發放循環獎勵", account, $"第 {tc} 輪 check 0→1", true);
                return ("ok", tc);
            }
            catch { await tx.RollbackAsync(); return ("error", 0); }
        }

        // ══════════════════════════════════════════════════════════
        // 禁言操作（csalogin.Offline 欄位：0=正常, 1=禁言）
        // 對應 GM 指令 [shutup] / [禁言] / [unlock]
        // ══════════════════════════════════════════════════════════
        public async Task<bool> MutePlayerAsync(string account, bool mute)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csalogin SET Offline=@v WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@v",    mute ? 1 : 0);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync(mute ? "禁言" : "解除禁言", account, "", true);
            return ok;
        }

        public async Task<bool> GetMuteStatusAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("SELECT Offline FROM csalogin WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@name", account);
            var r = await cmd.ExecuteScalarAsync();
            return r != null && r != DBNull.Value && Convert.ToInt32(r) == 1;
        }

        // ══════════════════════════════════════════════════════════
        // 玩家名稱 / 刪除
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 查詢「若用 WHERE Name=@acc 會影響幾筆」，用來判斷 Name 是否唯一。
        /// 若 > 1 代表 Name 被多角色共用，需改用 id。
        /// </summary>
        public async Task<int> CountByAccountAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM csalogin WHERE `Name`=@acc", conn);
            cmd.Parameters.AddWithValue("@acc", account);
            var r = await cmd.ExecuteScalarAsync();
            return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
        }

        /// <summary>
        /// 嘗試從 csalogin 讀取 id 欄位（自動遞增主鍵）。
        /// 若欄位不存在回傳 0。
        /// </summary>
        public async Task<int> GetCharDbIdAsync(string account, int masterId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            if (!await CsaloginHasIdAsync(conn)) return 0;
            try
            {
                using var cmd = new MySqlCommand(
                    "SELECT `id` FROM csalogin WHERE `Name`=@acc AND `MasterId`=@mid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@acc", account);
                cmd.Parameters.AddWithValue("@mid", masterId);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value) return Convert.ToInt32(r);
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return 0;
        }

        /// <summary>
        /// 僅修改指定角色的名稱，不影響同主帳號下其他角色。
        /// 優先使用 charDbId（csalogin.id）精確定位；
        /// 若 id 不存在則用 Name + MasterId 雙條件，仍用 LIMIT 1 保護。
        /// </summary>
        public async Task<bool> UpdatePlayerNameAsync(string account, string oldName, string newName,
                                                       int charDbId = 0, int masterId = 0)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            MySqlCommand cmd;
            bool hasId = await CsaloginHasIdAsync(conn);
            if (charDbId > 0 && hasId)
            {
                // 最精確：用 auto-increment 主鍵
                cmd = new MySqlCommand(
                    "UPDATE csalogin SET OnlineName=@name WHERE `id`=@cid", conn);
                cmd.Parameters.AddWithValue("@name", newName);
                cmd.Parameters.AddWithValue("@cid",  charDbId);
            }
            else if (masterId > 0)
            {
                // 次精確：Name + MasterId，且 LIMIT 1 避免重複更新
                cmd = new MySqlCommand(
                    "UPDATE csalogin SET OnlineName=@name WHERE `Name`=@acc AND `MasterId`=@mid LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@name", newName);
                cmd.Parameters.AddWithValue("@acc",  account);
                cmd.Parameters.AddWithValue("@mid",  masterId);
            }
            else
            {
                // 最後退路：單純 Name，加 LIMIT 1
                cmd = new MySqlCommand(
                    "UPDATE csalogin SET OnlineName=@name WHERE `Name`=@acc LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@name", newName);
                cmd.Parameters.AddWithValue("@acc",  account);
            }

            bool ok;
            using (cmd) { ok = await cmd.ExecuteNonQueryAsync() > 0; }

            string keyInfo = charDbId > 0 ? $"id={charDbId}" : $"Name={account},MasterId={masterId}";
            if (ok) await GmLogger.Instance.LogAsync("修改名稱", account,
                $"舊名稱:{oldName} → 新名稱:{newName} [{keyInfo}]", true);
            return ok;
        }

        // ── 名稱還原輔助 ───────────────────────────────────────────

        /// <summary>執行 SELECT 查詢並傳回欄位名稱與資料列。</summary>
        public async Task<(List<string> cols, List<List<string>> rows, long ms)> RunSelectQueryAsync(string sql)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cols = new List<string>();
            var rows = new List<List<string>>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(sql, conn) { CommandTimeout = 15 };
            using var r = await cmd.ExecuteReaderAsync();
            for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
            while (await r.ReadAsync())
            {
                var row = new List<string>();
                for (int i = 0; i < r.FieldCount; i++)
                    row.Add(r.IsDBNull(i) ? "(null)" : r.GetValue(i).ToString());
                rows.Add(row);
            }
            sw.Stop();
            return (cols, rows, sw.ElapsedMilliseconds);
        }

        /// <summary>取得指定帳號的目前 OnlineName。</summary>
        public async Task<string> GetCurrentOnlineNameAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT OnlineName FROM csalogin WHERE `Name`=@acc LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@acc", account);
            var r = await cmd.ExecuteScalarAsync();
            return r?.ToString() ?? account;
        }

        /// <summary>
        /// 從歷史資料猜測指定帳號的原始角色名稱。
        /// 優先查 capturepet.author（寵物捕捉人），其次查 tradelog。
        /// 回傳 null 代表查無歷史記錄。
        /// </summary>
        public async Task<string> GuessOriginalNameAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // ① capturepet.author — 最可靠：捕捉寵物時記錄的角色名
            try
            {
                using var cmd = new MySqlCommand(
                    @"SELECT author FROM capturepet
                      WHERE cdkey=@acc AND author IS NOT NULL AND author<>''
                      ORDER BY id DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@acc", account);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value)
                {
                    string n = r.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }

            // ② tradelog — 交易記錄中的角色名（from_name / to_name）
            try
            {
                using var cmd = new MySqlCommand(
                    @"SELECT from_name FROM tradelog
                      WHERE from_cdkey=@acc AND from_name IS NOT NULL AND from_name<>''
                      ORDER BY id DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@acc", account);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value)
                {
                    string n = r.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }

            // ③ tradelog（作為交易對象）
            try
            {
                using var cmd = new MySqlCommand(
                    @"SELECT to_name FROM tradelog
                      WHERE to_cdkey=@acc AND to_name IS NOT NULL AND to_name<>''
                      ORDER BY id DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@acc", account);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value)
                {
                    string n = r.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }

            return null;  // 查無歷史，無法自動還原
        }

        /// <summary>
        /// 找出所有 OnlineName 重複的角色（排除只有一筆的正常情況）。
        /// 回傳 (帳號cdkey, 角色名, MasterId, 猜測原始名) 清單。
        /// </summary>
        public async Task<List<(string Account, string CurrentName, int MasterId)>> GetDuplicateNamesAsync()
        {
            var list = new List<(string, string, int)>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                @"SELECT c.`Name`, c.OnlineName, c.MasterId
                  FROM csalogin c
                  WHERE c.OnlineName IN (
                      SELECT OnlineName FROM csalogin
                      GROUP BY OnlineName HAVING COUNT(*) > 1
                  )
                  ORDER BY c.OnlineName, c.MasterId", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add((r["Name"].ToString(), r["OnlineName"].ToString(),
                          Convert.ToInt32(r["MasterId"])));
            return list;
        }

        // ── 輩份（Belong）──────────────────────────────────────────

        /// <summary>讀取角色輩份（csalogin.Belong），欄位不存在時回傳 -1。</summary>
        public async Task<int> GetPlayerBelongAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(
                    "SELECT Belong FROM csalogin WHERE `Name`=@acc", conn);
                cmd.Parameters.AddWithValue("@acc", account);
                var r = await cmd.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }
            catch { return -1; }  // 欄位不存在
        }

        /// <summary>設定角色輩份（csalogin.Belong）。</summary>
        public async Task<bool> UpdatePlayerBelongAsync(string account, int belong)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(
                    "UPDATE csalogin SET Belong=@v WHERE `Name`=@acc", conn);
                cmd.Parameters.AddWithValue("@v",   belong);
                cmd.Parameters.AddWithValue("@acc", account);
                bool ok = await cmd.ExecuteNonQueryAsync() > 0;
                if (ok) await GmLogger.Instance.LogAsync("修改輩份", account, $"Belong → {belong}", true);
                return ok;
            }
            catch { return false; }
        }

        /// <summary>
        /// 刪除指定帳號（cdkey/Name）的角色。
        /// 注意：使用 Name（cdkey）精確刪除，避免誤刪同主帳號下的其他角色。
        /// </summary>
        public async Task<bool> DeletePlayerAsync(string account, string onlineName)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();

            // ── 刪除前先備份到 csalogin_recycle（回收桶），讓還原成為可能 ──
            try
            {
                // 確保回收桶資料表存在
                using var createCmd = new MySqlCommand(@"
                    CREATE TABLE IF NOT EXISTS csalogin_recycle (
                        recycle_id   INT AUTO_INCREMENT PRIMARY KEY,
                        deleted_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        deleted_by   VARCHAR(64) DEFAULT 'GM',
                        original_data LONGTEXT NOT NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn);
                await createCmd.ExecuteNonQueryAsync();

                // 備份完整資料列（以 JSON 形式保存）
                using var backupSel = new MySqlCommand(
                    "SELECT * FROM csalogin WHERE `Name`=@name LIMIT 1", conn);
                backupSel.Parameters.AddWithValue("@name", account);
                using var reader = await backupSel.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var sb = new System.Text.StringBuilder("{");
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        string fieldName = reader.GetName(i);
                        string val;
                        if (reader.IsDBNull(i))
                            val = "null";
                        else
                        {
                            object raw = reader.GetValue(i);
                            // DateTime 必須用 MySQL 格式存，否則還原時無法 INSERT
                            string str = raw is DateTime dt
                                ? dt.ToString("yyyy-MM-dd HH:mm:ss")
                                : raw.ToString();
                            val = System.Text.Json.JsonSerializer.Serialize(str);
                        }
                        sb.Append($"\"{fieldName}\":{val}");
                    }
                    sb.Append('}');
                    await reader.CloseAsync();

                    using var ins = new MySqlCommand(
                        "INSERT INTO csalogin_recycle (deleted_by, original_data) VALUES (@by, @data)", conn);
                    ins.Parameters.AddWithValue("@by", GmLogger.Instance.OperatorName);
                    ins.Parameters.AddWithValue("@data", sb.ToString());
                    await ins.ExecuteNonQueryAsync();
                }
                else { await reader.CloseAsync(); }
            }
            catch (Exception backupEx)
            {
                await GmLogger.Instance.LogAsync("刪除角色(備份失敗)", account,
                    $"回收桶備份失敗：{backupEx.Message}", false);
            }

            // 使用 Name（cdkey）刪除，不使用 MasterId（否則會刪除同主帳號所有角色）
            using var cmd = new MySqlCommand(
                "DELETE FROM csalogin WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("刪除角色", account, $"角色名:{onlineName}（已備份至回收桶）", true);
            return ok;
        }

        /// <summary>取得回收桶中所有已刪除的角色清單。</summary>
        public async Task<List<RecycleEntry>> GetRecycleBinAsync()
        {
            var list = new List<RecycleEntry>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(
                    "SELECT recycle_id, deleted_at, deleted_by, original_data FROM csalogin_recycle ORDER BY deleted_at DESC LIMIT 200", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var e = new RecycleEntry
                    {
                        RecycleId   = r.GetInt32("recycle_id"),
                        DeletedAt   = r.GetDateTime("deleted_at"),
                        DeletedBy   = r["deleted_by"]?.ToString() ?? "",
                        OriginalData = r["original_data"]?.ToString() ?? "{}"
                    };
                    // 從 JSON 取出 Name 和 OnlineName 方便顯示
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(e.OriginalData);
                        e.Account    = doc.RootElement.TryGetProperty("Name",       out var n) ? n.GetString() ?? "" : "";
                        e.OnlineName = doc.RootElement.TryGetProperty("OnlineName", out var o) ? o.GetString() ?? "" : "";
                        e.MasterName = doc.RootElement.TryGetProperty("MasterId",   out var m) ? m.GetString() ?? "" : "";
                    }
                    catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
                    list.Add(e);
                }
            }
            catch { /* 資料表不存在則傳回空清單 */ }
            return list;
        }

        /// <summary>從回收桶還原指定角色（重新 INSERT 回 csalogin）。</summary>
        public async Task<(bool ok, string msg)> RestoreFromRecycleAsync(int recycleId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            try
            {
                // 讀取備份資料
                using var sel = new MySqlCommand(
                    "SELECT original_data, deleted_at FROM csalogin_recycle WHERE recycle_id=@id", conn);
                sel.Parameters.AddWithValue("@id", recycleId);
                string json = null;
                using (var r = await sel.ExecuteReaderAsync())
                    if (await r.ReadAsync()) json = r["original_data"]?.ToString();

                if (string.IsNullOrEmpty(json)) return (false, "找不到備份資料。");

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 檢查是否已存在同名帳號
                string accName = root.TryGetProperty("Name", out var nProp) ? nProp.GetString() : null;
                if (!string.IsNullOrEmpty(accName))
                {
                    using var chk = new MySqlCommand(
                        "SELECT COUNT(*) FROM csalogin WHERE `Name`=@n", conn);
                    chk.Parameters.AddWithValue("@n", accName);
                    long cnt = Convert.ToInt64(await chk.ExecuteScalarAsync());
                    if (cnt > 0) return (false, $"帳號 {accName} 已存在，無法還原（可能已重新建立）。");
                }

                // 動態組合 INSERT 語句（排除 id 欄位，讓 AUTO_INCREMENT 自動產生）
                var cols  = new List<string>();
                var vals  = new List<string>();
                var parms = new List<(string name, string val)>();
                int idx   = 0;
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) continue;
                    string pname = $"@p{idx++}";
                    cols.Add($"`{prop.Name}`");
                    vals.Add(pname);

                    string rawStr = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                        ? null : prop.Value.GetString();

                    // 修正舊備份中可能存有中文日期格式（如「2026/2/23 下午 05:58:02」）
                    if (rawStr != null && (rawStr.Contains("上午") || rawStr.Contains("下午")))
                    {
                        if (DateTime.TryParse(rawStr,
                            System.Globalization.CultureInfo.GetCultureInfo("zh-TW"),
                            System.Globalization.DateTimeStyles.None, out DateTime parsedDt))
                            rawStr = parsedDt.ToString("yyyy-MM-dd HH:mm:ss");
                    }

                    parms.Add((pname, rawStr));
                }
                string insertSql = $"INSERT INTO csalogin ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})";
                using var ins = new MySqlCommand(insertSql, conn);
                foreach (var (pname, val) in parms)
                    ins.Parameters.AddWithValue(pname, (object)val ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync();

                // 從回收桶移除
                using var del = new MySqlCommand(
                    "DELETE FROM csalogin_recycle WHERE recycle_id=@id", conn);
                del.Parameters.AddWithValue("@id", recycleId);
                await del.ExecuteNonQueryAsync();

                string displayName = root.TryGetProperty("OnlineName", out var op) ? op.GetString() : accName;
                await GmLogger.Instance.LogAsync("還原角色", accName, $"角色名:{displayName}（從回收桶還原）", true);
                return (true, $"✅ 角色「{displayName}」已成功還原！");
            }
            catch (Exception ex) { return (false, "還原失敗：" + ex.Message); }
        }

        // ══════════════════════════════════════════════════════════
        // 統計資料
        // ══════════════════════════════════════════════════════════
        public async Task<ServerStats> GetStatsAsync()
        {
            var stats = new ServerStats();
            using var conn = GetConnection();
            await conn.OpenAsync();

            async Task<int> Scalar(string sql)
            {
                using var c = new MySqlCommand(sql, conn);
                var r = await c.ExecuteScalarAsync();
                return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
            }

            stats.OnlineCount     = await Scalar("SELECT COUNT(*) FROM csalogin WHERE Online=1 OR LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR)");
            stats.TotalPlayers    = await Scalar("SELECT COUNT(*) FROM csalogin");
            stats.TodayNewPlayers = await Scalar("SELECT COUNT(*) FROM csalogin WHERE DATE(created_at)=CURDATE()");
            stats.TodayActive     = await Scalar("SELECT COUNT(*) FROM csalogin WHERE DATE(LoginTime)=CURDATE()");
            stats.TotalMails      = await Scalar("SELECT COUNT(*) FROM maildata");
            stats.UnreadMails     = await Scalar("SELECT COUNT(*) FROM maildata WHERE `check`=0 AND deleamill=0");

            // 充值統計（recharge_orders）
            try
            {
                using var c1 = new MySqlCommand(
                    "SELECT IFNULL(SUM(amount),0), IFNULL(COUNT(*),0) FROM recharge_orders WHERE status='completed' AND DATE(created_at)=CURDATE()", conn);
                using var r1 = await c1.ExecuteReaderAsync();
                if (await r1.ReadAsync())
                {
                    stats.TodayRevenue = r1[0] == DBNull.Value ? 0 : Convert.ToDecimal(r1[0]);
                    stats.TodayOrders  = r1[1] == DBNull.Value ? 0 : Convert.ToInt32(r1[1]);
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            try
            {
                using var c2 = new MySqlCommand(
                    "SELECT IFNULL(SUM(amount),0) FROM recharge_orders WHERE status='completed'", conn);
                var rv = await c2.ExecuteScalarAsync();
                stats.TotalRevenue = rv == null || rv == DBNull.Value ? 0 : Convert.ToDecimal(rv);
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            try
            {
                using var c3 = new MySqlCommand(
                    @"SELECT r.role_name, IFNULL(c.OnlineName,'') AS CharName,
                             SUM(r.amount) AS total, COUNT(*) AS cnt
                      FROM recharge_orders r
                      LEFT JOIN csalogin c ON c.`Name`=r.role_name
                      WHERE r.status='completed'
                      GROUP BY r.role_name ORDER BY total DESC LIMIT 10", conn);
                using var r3 = await c3.ExecuteReaderAsync();
                while (await r3.ReadAsync())
                    stats.TopRechargersAllTime.Add(new RechargeRankItem
                    {
                        RoleName = r3["role_name"]?.ToString() ?? "",
                        CharName = r3["CharName"]?.ToString() ?? "",
                        Total    = Convert.ToDecimal(r3["total"]),
                        Count    = Convert.ToInt32(r3["cnt"])
                    });
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }

            return stats;
        }

        // ══════════════════════════════════════════════════════════
        // 玩家詳細資料
        // ══════════════════════════════════════════════════════════
        public async Task<PlayerDetail> GetPlayerDetailAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            var detail = new PlayerDetail();
            bool hasId = await CsaloginHasIdAsync(conn);
            bool hasBelong = await CsaloginHasBelongAsync(conn);
            string idSel = hasId ? ", c.id" : "";
            string belongSel = hasBelong ? ", c.Belong" : "";

            // csalogin 主資料 + 主帳號名；強制以 UTF-8 解讀帳號/角色名/主帳號；Belong 欄位若不存在則不選取
            using (var cmd = new MySqlCommand(
                $@"SELECT CONVERT(CONVERT(c.`Name` USING binary) USING utf8mb4) AS `Name`,
                         CONVERT(CONVERT(c.OnlineName USING binary) USING utf8mb4) AS OnlineName,
                         c.MasterId, c.IP, c.RegIP, c.RegTime, c.LoginTime, c.Online, c.Offline,
                         c.GroupId, c.GroupName, c.NeiCe, c.ServerId, c.ServerName,
                         c.VipPoint, c.PetPoint, c.PayPoint, c.RmbPoint, IFNULL(c.PayTotal,0) AS PayTotal,
                         c.QQ, c.uid, c.MAC1, c.PassWord, c.SafePasswd{belongSel}{idSel},
                         IFNULL(CONVERT(CONVERT(m.`Name` USING binary) USING utf8mb4),'') AS MasterName
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id = c.MasterId
                  WHERE c.`Name`=@acc", conn))
            {
                cmd.Parameters.AddWithValue("@acc", account);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    detail.OnlineName = r["OnlineName"]?.ToString() ?? "";
                    detail.Account    = r["Name"]?.ToString() ?? "";
                    detail.IP         = r["IP"]?.ToString() ?? "";
                    detail.RegIP      = r["RegIP"]?.ToString() ?? "";
                    detail.RegTime    = r["RegTime"]?.ToString() ?? "";
                    detail.LoginTime  = r["LoginTime"]?.ToString() ?? "";
                    detail.IsOnline   = Convert.ToInt32(r["Online"]) == 1;
                    detail.IsMuted    = Convert.ToInt32(r["Offline"]) == 1;
                    detail.GroupId    = Convert.ToInt32(r["GroupId"]);
                    detail.GroupName  = r["GroupName"]?.ToString() ?? "";
                    detail.NeiCe      = Convert.ToInt32(r["NeiCe"]);
                    detail.ServerId   = Convert.ToInt32(r["ServerId"]);
                    detail.ServerName = r["ServerName"]?.ToString() ?? "";
                    detail.Gold       = Convert.ToInt64(r["VipPoint"]);
                    detail.Crystal    = Convert.ToInt64(r["PetPoint"]);
                    detail.PayPoint   = Convert.ToInt64(r["PayPoint"]);
                    detail.RmbPoint   = Convert.ToInt64(r["RmbPoint"]);
                    detail.PayTotal   = Convert.ToInt64(r["PayTotal"]);
                    detail.QQ           = r["QQ"]?.ToString() ?? "";
                    detail.Uid          = r["uid"]?.ToString() ?? "";
                    detail.MAC          = r["MAC1"]?.ToString() ?? "";
                    detail.Password     = r["PassWord"]?.ToString() ?? "";
                    detail.SafePassword = r["SafePasswd"]?.ToString() ?? "";
                    detail.MasterName = r["MasterName"]?.ToString() ?? "";
                    // 輩份欄位（Belong），僅在資料表有此欄位時讀取
                    if (hasBelong)
                        try { detail.Belong = r["Belong"] == DBNull.Value ? 0 : Convert.ToInt32(r["Belong"]); }
                        catch { detail.Belong = -1; }
                    else
                        detail.Belong = -1;
                    // csalogin 自動遞增主鍵 id
                    if (_csaloginHasId == true)
                        try { detail.CharDbId = r["id"] == DBNull.Value ? 0 : Convert.ToInt32(r["id"]); }
                        catch { detail.CharDbId = 0; }
                }
            }

            // 寵物數量 + 最強寵物四圍素質（hp/attack/def/quick/sum）
            // cdkey 或 author 可能存登入帳號/角色名/uid，皆比對
            string petCharName = detail.OnlineName;
            string petUid      = detail.Uid;
            using (var cmd2 = new MySqlCommand(
                "SELECT COUNT(*) FROM capturepet WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid) OR author=@cname OR author=@acc", conn))
            {
                cmd2.Parameters.AddWithValue("@acc",   account);
                cmd2.Parameters.AddWithValue("@cname", petCharName);
                cmd2.Parameters.AddWithValue("@uid",   petUid);
                var cnt = await cmd2.ExecuteScalarAsync();
                detail.PetCount = cnt == null || cnt == DBNull.Value ? 0 : Convert.ToInt32(cnt);
            }
            detail.TopPet = new PetSummary { Count = detail.PetCount };
            if (detail.PetCount > 0)
            {
                using var petCmd = new MySqlCommand(
                    @"SELECT id, name, lv, hp, attack, def, quick, sum, author
                      FROM capturepet WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid) OR author=@cname OR author=@acc
                      ORDER BY sum DESC LIMIT 1", conn);
                petCmd.Parameters.AddWithValue("@acc",   account);
                petCmd.Parameters.AddWithValue("@cname", petCharName);
                petCmd.Parameters.AddWithValue("@uid",   petUid);
                using var pr = await petCmd.ExecuteReaderAsync();
                if (await pr.ReadAsync())
                {
                    detail.TopPet.BestId     = pr["id"] == DBNull.Value ? 0 : Convert.ToInt32(pr["id"]);
                    detail.TopPet.BestName   = pr["name"]?.ToString() ?? "";
                    detail.TopPet.BestLv     = pr["lv"] == DBNull.Value ? 0 : Convert.ToInt32(pr["lv"]);
                    detail.TopPet.BestHp     = pr["hp"] == DBNull.Value ? 0 : Convert.ToInt32(pr["hp"]);
                    detail.TopPet.BestAttack = pr["attack"] == DBNull.Value ? 0 : Convert.ToInt32(pr["attack"]);
                    detail.TopPet.BestDef    = pr["def"] == DBNull.Value ? 0 : Convert.ToInt32(pr["def"]);
                    detail.TopPet.BestQuick  = pr["quick"] == DBNull.Value ? 0 : Convert.ToInt32(pr["quick"]);
                    detail.TopPet.BestSum    = pr["sum"] == DBNull.Value ? 0 : Convert.ToDouble(pr["sum"]);
                    detail.TopPet.BestAuthor = pr["author"]?.ToString() ?? "";
                }
            }

            // 封禁狀態
            using (var cmd3 = new MySqlCommand(
                "SELECT `time` FROM `lock` WHERE `Name`=@acc", conn))
            {
                cmd3.Parameters.AddWithValue("@acc", account);
                using var r3 = await cmd3.ExecuteReaderAsync();
                if (await r3.ReadAsync())
                {
                    detail.IsBanned  = true;
                    int t = r3.GetInt32("time");
                    detail.BanEndTime = t == 0 ? "永久封禁" :
                        DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
                }
            }

            // 郵件統計
            using (var cmd4 = new MySqlCommand(
                "SELECT COUNT(*) as tot, SUM(IF(`check`=0,1,0)) as unread FROM maildata WHERE cdkey=@acc", conn))
            {
                cmd4.Parameters.AddWithValue("@acc", account);
                using var r4 = await cmd4.ExecuteReaderAsync();
                if (await r4.ReadAsync())
                {
                    detail.TotalMails  = Convert.ToInt32(r4["tot"]);
                    detail.UnreadMails = Convert.ToInt32(r4["unread"] == DBNull.Value ? 0 : r4["unread"]);
                }
            }

            // 累積充值台幣：讀取 paydata.point（遊戲「累積充值獎勵」介面讀取的欄位）
            // 同時讀取 lifetime_total、totalcheck、check（用於判斷領獎狀態）
            using (var cmd5 = new MySqlCommand(
                "SELECT point, IFNULL(lifetime_total, point) AS lifetime_total, IFNULL(totalcheck,0) AS tc, IFNULL(`check`,1) AS ck FROM paydata WHERE cdkey=@acc", conn))
            {
                cmd5.Parameters.AddWithValue("@acc", account);
                using var r5 = await cmd5.ExecuteReaderAsync();
                if (await r5.ReadAsync())
                {
                    detail.PayTotal         = r5["point"]          == DBNull.Value ? 0 : Convert.ToInt64(r5["point"]);
                    detail.LifetimePayTotal = r5["lifetime_total"] == DBNull.Value ? 0 : Convert.ToInt64(r5["lifetime_total"]);
                    detail.TotalCheck       = r5["tc"]             == DBNull.Value ? 0 : Convert.ToInt64(r5["tc"]);
                    detail.PaydataCheck     = r5["ck"]             == DBNull.Value ? 1 : Convert.ToInt32(r5["ck"]);
                }
                // 若 paydata 無記錄，PayTotal / LifetimePayTotal 維持 csalogin.PayTotal 的值
            }

            // 累計消費達成獎勵：costdata（point = 累計消費金幣, check = 已領取里程碑數）
            try
            {
                using var cmdC = new MySqlCommand(
                    "SELECT point, IFNULL(`check`, 0) AS ck FROM costdata WHERE cdkey=@acc ORDER BY time DESC LIMIT 1", conn);
                cmdC.Parameters.AddWithValue("@acc", account);
                using var rC = await cmdC.ExecuteReaderAsync();
                if (await rC.ReadAsync())
                {
                    detail.CostPoint = rC["point"] == DBNull.Value ? 0 : Convert.ToInt64(rC["point"]);
                    detail.CostCheck = rC["ck"]    == DBNull.Value ? 0 : Convert.ToInt32(rC["ck"]);
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB/costdata] " + dbEx.Message); }

            return detail;
        }

        // ══════════════════════════════════════════════════════════
        // 主帳號查詢（csaloginmaster → csalogin 子帳號）
        // ══════════════════════════════════════════════════════════
        public async Task<List<MasterAccount>> GetMasterAccountsAsync(string filter = "")
        {
            var list = new List<MasterAccount>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            // 主帳號列表 + 子帳號數量
            // 有 filter 時：先用 LIKE 模糊撈取，再於 C# 端排序（精確符合排最前）
            string sql = string.IsNullOrWhiteSpace(filter)
                ? @"SELECT m.Id, m.`Name`, m.created_at,
                           COUNT(c.Id) AS SubCount
                    FROM csaloginmaster m
                    LEFT JOIN csalogin c ON c.MasterId = m.Id
                    GROUP BY m.Id, m.`Name`, m.created_at
                    ORDER BY SubCount DESC, m.created_at DESC
                    LIMIT 300"
                : @"SELECT m.Id, m.`Name`, m.created_at,
                           COUNT(c.Id) AS SubCount
                    FROM csaloginmaster m
                    LEFT JOIN csalogin c ON c.MasterId = m.Id
                    WHERE m.`Name` LIKE @q
                    GROUP BY m.Id, m.`Name`, m.created_at
                    ORDER BY SubCount DESC, m.created_at DESC
                    LIMIT 300";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(filter))
                    cmd.Parameters.AddWithValue("@q", $"%{filter}%");
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new MasterAccount
                    {
                        Id        = r.GetInt32("Id"),
                        Name      = r["Name"]?.ToString() ?? "",
                        SubCount  = r["SubCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["SubCount"]),
                        CreatedAt = r["created_at"]?.ToString() ?? ""
                    });
            }

            // C# 端排序：精確符合優先，其次依子帳號數量降序
            if (!string.IsNullOrWhiteSpace(filter))
                list = list
                    .OrderBy(m => m.Name.Equals(filter, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenByDescending(m => m.SubCount)
                    .ThenBy(m => m.CreatedAt)
                    .ToList();

            return list;
        }

        /// <summary>取得指定主帳號（ID）旗下所有子帳號（含角色名、寵物數、充值金幣）</summary>
        public async Task<List<PlayerInfo>> GetSubAccountsAsync(int masterId)
        {
            var list = new List<PlayerInfo>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            bool hasId2 = await CsaloginHasIdAsync(conn);
            string idCol2 = hasId2 ? ", c.`id` AS CharDbId" : ", 0 AS CharDbId";
            // 強制以 UTF-8 解讀帳號/角色名/主帳號（與 SearchPlayersAsync 一致）
            string n2 = "CONVERT(CONVERT(c.`Name` USING binary) USING utf8mb4)";
            string o2 = "CONVERT(CONVERT(c.OnlineName USING binary) USING utf8mb4)";
            string m2 = "IFNULL(CONVERT(CONVERT(m.`Name` USING binary) USING utf8mb4),'')";
            string sql = $@"
                SELECT c.MasterId, {n2} AS `Name`, {o2} AS OnlineName, c.Online, c.LoginTime, c.ServerId,
                       IFNULL(c.PayTotal, 0) AS PayTotal,
                       IFNULL(pet.cnt, 0)   AS PetCount,
                       {m2}  AS MasterName
                       {idCol2}
                FROM csalogin c
                LEFT JOIN csaloginmaster m  ON m.Id    = c.MasterId
                LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet
                       ON pet.cdkey = c.`Name`
                WHERE c.MasterId = @mid
                ORDER BY c.Online DESC, c.LoginTime DESC";

            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@mid", masterId);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                try
                {
                    list.Add(new PlayerInfo
                    {
                        MasterId   = r["MasterId"] == DBNull.Value ? 0 : r.GetInt32("MasterId"),
                        Account    = r["Name"]?.ToString() ?? "",
                        OnlineName = r["OnlineName"]?.ToString() ?? "",
                        IsOnline   = r["Online"] != DBNull.Value && Convert.ToInt32(r["Online"]) == 1,
                        LoginTime  = r["LoginTime"]?.ToString() ?? "",
                        ServerId   = r["ServerId"]?.ToString() ?? "",
                        PayTotal   = r["PayTotal"] == DBNull.Value ? 0 : Convert.ToInt64(r["PayTotal"]),
                        PetCount   = r["PetCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["PetCount"]),
                        MasterName = r["MasterName"]?.ToString() ?? "",
                        CharDbId   = r["CharDbId"] == DBNull.Value ? 0 : Convert.ToInt32(r["CharDbId"])
                    });
                }
                catch { /* 跳過有問題的資料列，不影響其他結果 */ }
            }
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // GM 帳號管理（admin_users 表）
        // ══════════════════════════════════════════════════════════
        public async Task<List<AdminUser>> GetAdminUsersAsync()
        {
            var list = new List<AdminUser>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT id, username, nickname, status, created_at FROM admin_users ORDER BY id", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new AdminUser
                {
                    Id        = r.GetInt32("id"),
                    Username  = r["username"]?.ToString() ?? "",
                    Nickname  = r["nickname"]?.ToString() ?? "",
                    IsEnabled = Convert.ToBoolean(r["status"]),
                    CreatedAt = r["created_at"]?.ToString() ?? "",
                });
            return list;
        }

        public async Task<bool> AddAdminUserAsync(string username, string password, string nickname)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            // 以 MD5 存儲密碼（和原版相容）
            string hash = Convert.ToHexString(
                System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(password))).ToLower();
            using var cmd = new MySqlCommand(
                "INSERT INTO admin_users (username, password, nickname, status) VALUES (@u,@p,@n,1)", conn);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", hash);
            cmd.Parameters.AddWithValue("@n", nickname);
            try { return await cmd.ExecuteNonQueryAsync() > 0; }
            catch { return false; }
        }

        public async Task<bool> DeleteAdminUserAsync(int id)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("DELETE FROM admin_users WHERE id=@id AND username<>'admin'", conn);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> ToggleAdminStatusAsync(int id, bool enable)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE admin_users SET status=@s WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("@s", enable ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> ResetAdminPasswordAsync(int id, string newPassword)
        {
            string hash = Convert.ToHexString(
                System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(newPassword))).ToLower();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE admin_users SET password=@p WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("@p", hash);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // ══════════════════════════════════════════════════════════
        // 遊戲內 GM 權限（csalogin.GroupId / NeiCe）
        // GroupId：伺服器群組 ID；具體哪個值代表 GM 因伺服器設定而異
        // NeiCe  ：內測標記（0=一般，1=內測/GM）
        // ══════════════════════════════════════════════════════════

        /// <summary>取得玩家的 GroupId 和 NeiCe 值</summary>
        public async Task<(int groupId, int neiCe)> GetGmFlagsAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT GroupId, NeiCe FROM csalogin WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@name", account);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return (0, 0);
            return (Convert.ToInt32(r["GroupId"]), Convert.ToInt32(r["NeiCe"]));
        }

        /// <summary>設定 GroupId（遊戲群組/GM 標識）</summary>
        public async Task<bool> SetGroupIdAsync(string account, int groupId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csalogin SET GroupId=@g WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@g",    groupId);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("修改GroupId", account, $"GroupId → {groupId}", true);
            return ok;
        }

        /// <summary>設定 NeiCe（內測/GM 標記：0=一般, 1=GM）</summary>
        public async Task<bool> SetNeiCeAsync(string account, int neiCe)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csalogin SET NeiCe=@v WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@v",    neiCe);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync(neiCe == 1 ? "授予GM權限" : "撤銷GM權限",
                account, $"NeiCe={neiCe}", true);
            return ok;
        }

        // ══════════════════════════════════════════════════════════
        // 玩家密碼重設
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 重設角色密碼（csalogin）。
        /// PassWord 欄位以 MD5 儲存；SafePasswd 欄位儲存明文。
        /// </summary>
        public async Task<bool> ResetPlayerPasswordAsync(string account, string newPassword, string field = "PassWord")
        {
            string storedValue;
            if (field == "PassWord")
            {
                // PassWord 用 MD5 (小寫 32 位)
                using var md5 = System.Security.Cryptography.MD5.Create();
                storedValue = BitConverter.ToString(
                    md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(newPassword)))
                    .Replace("-", "").ToLower();
            }
            else
            {
                // SafePasswd 存明文
                storedValue = newPassword;
            }

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                $"UPDATE csalogin SET `{field}`=@pwd WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@pwd",  storedValue);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            string fmt = field == "PassWord" ? "（MD5 加密）" : "（明文）";
            if (ok) await GmLogger.Instance.LogAsync("重設角色密碼",
                account, $"欄位：{field}{fmt}", true);
            return ok;
        }

        /// <summary>
        /// 重設主帳號登入密碼（csaloginmaster）。
        /// 使用 bcrypt 雜湊，與網頁登入相容。
        /// masterName 為 csaloginmaster.Name（主帳號名稱，非角色 UID）。
        /// </summary>
        public async Task<bool> ResetMasterPasswordAsync(string masterName, string newPassword)
        {
            string bcryptHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 10);

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csaloginmaster SET PassWord=@pwd WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@pwd",  bcryptHash);
            cmd.Parameters.AddWithValue("@name", masterName);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("重設主帳號密碼",
                masterName, "bcrypt 加密", true);
            return ok;
        }

        // ══════════════════════════════════════════════════════════
        // 充值記錄（recharge_orders）
        // ══════════════════════════════════════════════════════════
        public async Task<List<RechargeRecord>> GetRechargeOrdersAsync(string filter = "", int limit = 500)
        {
            var list = new List<RechargeRecord>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            // ── Part 1：recharge_orders（官方訂單）──────────────────────
            DateTime latestOrderTime = DateTime.MinValue;
            string sql = string.IsNullOrWhiteSpace(filter)
                ? $@"SELECT o.*, IFNULL(c.OnlineName,'') AS charName
                     FROM recharge_orders o
                     LEFT JOIN csalogin c ON c.`Name` = o.role_name
                     ORDER BY o.created_at DESC LIMIT {limit}"
                : $@"SELECT o.*, IFNULL(c.OnlineName,'') AS charName
                     FROM recharge_orders o
                     LEFT JOIN csalogin c ON c.`Name` = o.role_name
                     WHERE o.role_name LIKE @q OR o.product_name LIKE @q OR c.OnlineName LIKE @q
                     ORDER BY o.created_at DESC LIMIT {limit}";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(filter))
                    cmd.Parameters.AddWithValue("@q", $"%{filter}%");
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    if (r["created_at"] != DBNull.Value)
                    {
                        var t = Convert.ToDateTime(r["created_at"]);
                        if (t > latestOrderTime) latestOrderTime = t;
                    }
                    list.Add(new RechargeRecord
                    {
                        Id          = r.GetInt32("id"),
                        OrderNo     = r["order_no"]?.ToString() ?? "",
                        RoleName    = r["role_name"]?.ToString() ?? "",
                        CharName    = r["charName"]?.ToString() ?? "",
                        ProductName = r["product_name"]?.ToString() ?? "",
                        Amount      = r["amount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["amount"]),
                        Status      = r["status"]?.ToString() ?? "",
                        CreatedAt   = r["created_at"]?.ToString() ?? "",
                        Source      = "orders"
                    });
                }
            }

            // ── Part 2：paydata 補充（付費系統直接寫 DB 的充值）────────────
            try
            {
                bool hasFilter = !string.IsNullOrWhiteSpace(filter);
                string timeWhere = (!hasFilter && latestOrderTime != DateTime.MinValue)
                    ? "AND p.time > @lat"
                    : "";
                string paySql = $@"
                    SELECT p.cdkey, IFNULL(c.OnlineName,'') AS charName,
                           IFNULL(p.lifetime_total, p.point) AS lifetimeTotal,
                           p.time
                    FROM paydata p
                    LEFT JOIN csalogin c ON c.`Name` = p.cdkey
                    WHERE p.time IS NOT NULL AND IFNULL(p.lifetime_total, p.point) > 0
                    AND (@q='' OR p.cdkey LIKE @q OR c.OnlineName LIKE @q)
                    {timeWhere}
                    ORDER BY p.time DESC LIMIT 200";
                using var cmdP = new MySqlCommand(paySql, conn);
                cmdP.Parameters.AddWithValue("@q", string.IsNullOrWhiteSpace(filter) ? "" : $"%{filter}%");
                if (!hasFilter && latestOrderTime != DateTime.MinValue)
                    cmdP.Parameters.AddWithValue("@lat", latestOrderTime);
                using var rP = await cmdP.ExecuteReaderAsync();
                while (await rP.ReadAsync())
                {
                    decimal lt = rP["lifetimeTotal"] == DBNull.Value ? 0 : Convert.ToDecimal(rP["lifetimeTotal"]);
                    list.Add(new RechargeRecord
                    {
                        Id          = 0,
                        OrderNo     = "",
                        RoleName    = rP["cdkey"]?.ToString() ?? "",
                        CharName    = rP["charName"]?.ToString() ?? "",
                        ProductName = "充值（付費系統記錄）",
                        Amount      = lt,   // lifetime_total 為台幣，直接存（顯示時用 TwdAmount）
                        Status      = "paydata",
                        CreatedAt   = rP["time"]?.ToString() ?? "",
                        Source      = "paydata"
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/Paydata supplement] " + ex.Message);
            }

            return list;
        }

        /// <summary>取得指定玩家的充值總額及次數</summary>
        public async Task<(decimal total, int count)> GetPlayerRechargeSummaryAsync(string account)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT SUM(amount), COUNT(*) FROM recharge_orders WHERE role_name=@acc AND status='completed'", conn);
            cmd.Parameters.AddWithValue("@acc", account);
            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync() || r[0] == DBNull.Value) return (0, 0);
            return (Convert.ToDecimal(r[0]), Convert.ToInt32(r[1]));
        }

        // ══════════════════════════════════════════════════════════
        // 交易記錄（tradelog）
        // ══════════════════════════════════════════════════════════
        public async Task<List<TradeRecord>> GetTradeLogAsync(string filter = "", int limit = 500)
        {
            var list = new List<TradeRecord>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            string sql = string.IsNullOrWhiteSpace(filter)
                ? $"SELECT * FROM tradelog ORDER BY time DESC LIMIT {limit}"
                : $"SELECT * FROM tradelog WHERE mecdkey LIKE @q OR mename LIKE @q OR tocdkey LIKE @q OR toname LIKE @q ORDER BY time DESC LIMIT {limit}";
            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(filter))
                cmd.Parameters.AddWithValue("@q", $"%{filter}%");
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new TradeRecord
                {
                    FromCdkey = r["mecdkey"]?.ToString() ?? "",
                    FromName  = r["mename"]?.ToString() ?? "",
                    ToCdkey   = r["tocdkey"]?.ToString() ?? "",
                    ToName    = r["toname"]?.ToString() ?? "",
                    Time      = r["time"]?.ToString() ?? "",
                    Item      = r["item"]?.ToString() ?? "",
                    Pet       = r["pet"]?.ToString() ?? "",
                    Gold      = r["gold"] == DBNull.Value ? 0 : Convert.ToInt64(r["gold"])
                });
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 商城熱賣統計
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 取得商城熱賣道具排行。
        /// table: "vipshop"=金幣商店, "fameshop"=聲望商店,
        ///        "csshopnum"=石壁商店, "csxsshopnum"=戰點商店（結構不同，自動判斷）
        /// fromDate/toDate 皆為 null 時為全時段；否則依日期（含起訖日）篩選。
        /// </summary>
        public async Task<(List<ShopSaleRecord> items, List<ShopSpenderRecord> spenders)>
            GetShopTopItemsAsync(string table, int topN = 20, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var items    = new List<ShopSaleRecord>();
            var spenders = new List<ShopSpenderRecord>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            // ── 確認表格存在且有資料 ───────────────────────────────
            long rowCount = 0;
            try {
                using var cc = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", conn);
                rowCount = Convert.ToInt64(await cc.ExecuteScalarAsync());
            } catch { return (items, spenders); }
            if (rowCount == 0) return (items, spenders);

            bool useDate = fromDate.HasValue && toDate.HasValue;
            DateTime d0 = useDate ? fromDate!.Value.Date : default;
            DateTime d1 = useDate ? toDate!.Value.Date : default;
            if (useDate && d0 > d1) { var x = d0; d0 = d1; d1 = x; }

            string whereVipFame = useDate ? " WHERE DATE(`time`) BETWEEN @dfrom AND @dto " : "";
            string whereCs      = useDate ? " WHERE DATE(`date`) BETWEEN @dfrom AND @dto " : "";

            void AddDateParams(MySqlCommand cmd)
            {
                if (!useDate) return;
                cmd.Parameters.AddWithValue("@dfrom", d0);
                cmd.Parameters.AddWithValue("@dto", d1);
            }

            // ── 按表格類型分別查詢 ────────────────────────────────
            if (table == "vipshop" || table == "fameshop")
            {
                // 這兩張表結構相同: cdkey, name, itemid, itemname, itemnum, time, oldpoint, newpoint
                string sql1 = $@"
                    SELECT itemid, itemname,
                           SUM(itemnum) AS total_qty,
                           COUNT(*) AS order_count,
                           SUM(IFNULL(oldpoint,0) - IFNULL(newpoint,0)) AS total_cost,
                           MAX(`time`) AS last_time
                    FROM `{table}` {whereVipFame}
                    GROUP BY itemid, itemname
                    ORDER BY total_qty DESC
                    LIMIT {topN}";
                using (var cmd = new MySqlCommand(sql1, conn))
                {
                    AddDateParams(cmd);
                    using var r = await cmd.ExecuteReaderAsync();
                    int rank = 1;
                    while (await r.ReadAsync())
                        items.Add(new ShopSaleRecord
                        {
                            Rank        = rank++,
                            ItemId      = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]),
                            ItemName    = r["itemname"]?.ToString() ?? "",
                            TotalQty    = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]),
                            OrderCount  = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"]),
                            TotalCost   = r["total_cost"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_cost"]),
                            LastBuyTime = r["last_time"]?.ToString() ?? ""
                        });
                }

                string sql2 = $@"
                    SELECT cdkey, name,
                           SUM(itemnum) AS total_qty,
                           SUM(IFNULL(oldpoint,0) - IFNULL(newpoint,0)) AS total_cost
                    FROM `{table}` {whereVipFame}
                    GROUP BY cdkey, name
                    ORDER BY total_cost DESC
                    LIMIT {topN}";
                using (var cmd2 = new MySqlCommand(sql2, conn))
                {
                    AddDateParams(cmd2);
                    using var r2 = await cmd2.ExecuteReaderAsync();
                    int rank = 1;
                    while (await r2.ReadAsync())
                        spenders.Add(new ShopSpenderRecord
                        {
                            Rank      = rank++,
                            Cdkey     = r2["cdkey"]?.ToString() ?? "",
                            Name      = r2["name"]?.ToString() ?? "",
                            TotalQty  = r2["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r2["total_qty"]),
                            TotalCost = r2["total_cost"] == DBNull.Value ? 0 : Convert.ToInt64(r2["total_cost"])
                        });
                }
            }
            else if (table == "csshopnum" || table == "csxsshopnum")
            {
                // 這兩張表: id, type, itemid, buynum, price, date
                string sql1 = $@"
                    SELECT itemid,
                           SUM(buynum) AS total_qty,
                           COUNT(*) AS order_count,
                           MAX(`date`) AS last_time
                    FROM `{table}` {whereCs}
                    GROUP BY itemid
                    ORDER BY total_qty DESC
                    LIMIT {topN}";
                using var cmd = new MySqlCommand(sql1, conn);
                AddDateParams(cmd);
                using var r = await cmd.ExecuteReaderAsync();
                int rank = 1;
                while (await r.ReadAsync())
                    items.Add(new ShopSaleRecord
                    {
                        Rank        = rank++,
                        ItemId      = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]),
                        ItemName    = $"道具 #{r["itemid"]}",
                        TotalQty    = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]),
                        OrderCount  = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"]),
                        LastBuyTime = r["last_time"]?.ToString() ?? ""
                    });
            }

            return (items, spenders);
        }


        // ══════════════════════════════════════════════════════════
        // 金幣異動日誌（vippointlog / VipPointLog）
        // ══════════════════════════════════════════════════════════
        public async Task<List<GoldLogRecord>> GetGoldLogAsync(string filter = "", int limit = 500)
        {
            var list = new List<GoldLogRecord>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            const string table = "vippointlog";
            // LEFT JOIN csalogin 取角色名稱
            string sql = string.IsNullOrWhiteSpace(filter)
                ? $@"SELECT v.*, IFNULL(c.OnlineName,'') AS charName
                     FROM `{table}` v
                     LEFT JOIN csalogin c ON c.`Name` = v.cdkey
                     ORDER BY v.time DESC LIMIT {limit}"
                : $@"SELECT v.*, IFNULL(c.OnlineName,'') AS charName
                     FROM `{table}` v
                     LEFT JOIN csalogin c ON c.`Name` = v.cdkey
                     WHERE v.cdkey LIKE @q OR v.buff LIKE @q OR c.OnlineName LIKE @q
                     ORDER BY v.time DESC LIMIT {limit}";
            try
            {
                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(filter))
                    cmd.Parameters.AddWithValue("@q", $"%{filter}%");
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new GoldLogRecord
                    {
                        Cdkey    = r["cdkey"]?.ToString() ?? "",
                        CharName = r["charName"]?.ToString() ?? "",
                        Point    = r["point"]    == DBNull.Value ? 0 : Convert.ToInt64(r["point"]),
                        OldPoint = r["oldpoint"] == DBNull.Value ? 0 : Convert.ToInt64(r["oldpoint"]),
                        NewPoint = r["newpoint"] == DBNull.Value ? 0 : Convert.ToInt64(r["newpoint"]),
                        Buff     = r["buff"]?.ToString() ?? "",
                        Time     = r["time"]?.ToString() ?? ""
                    });
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 資料庫備份 / 還原
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 將 csalogin 和 lock 備份為 SQL 檔案（INSERT IGNORE 格式）。
        /// 返回 (寫入筆數, 完整檔案路徑)。
        /// </summary>
        public async Task<(int rows, string filePath)> BackupAsync(
            string folderPath, IProgress<string> progress = null)
        {
            Directory.CreateDirectory(folderPath);
            string fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
            string filePath = Path.Combine(folderPath, fileName);
            int totalRows = 0;

            using var conn = GetConnection();
            await conn.OpenAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("-- 番茄GM管理系統 資料庫備份");
            sb.AppendLine($"-- 備份時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("-- 使用 INSERT IGNORE：還原時不覆蓋現有資料，只補回遺失記錄");
            sb.AppendLine("-- =====================================================");
            sb.AppendLine();

            // 備份 csalogin
            progress?.Report("備份玩家帳號（csalogin）…");
            sb.AppendLine("-- ==== csalogin (玩家帳號) ====");
            using (var cmd = new MySqlCommand("SELECT * FROM `csalogin`", conn))
            using (var r = await cmd.ExecuteReaderAsync())
            {
                var cols = new List<string>();
                for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
                string colList = string.Join(", ", System.Linq.Enumerable.Select(cols, c => $"`{c}`"));
                while (await r.ReadAsync())
                {
                    var vals = new List<string>();
                    for (int i = 0; i < r.FieldCount; i++) vals.Add(SqlLiteral(r.GetValue(i)));
                    sb.AppendLine($"INSERT IGNORE INTO `csalogin` ({colList}) VALUES ({string.Join(", ", vals)});");
                    totalRows++;
                }
            }
            sb.AppendLine();

            // 備份 lock（封禁記錄）
            progress?.Report("備份封禁記錄（lock）…");
            sb.AppendLine("-- ==== lock (封禁記錄) ====");
            try
            {
                using var cmd2 = new MySqlCommand("SELECT * FROM `lock`", conn);
                using var r2 = await cmd2.ExecuteReaderAsync();
                var cols2 = new List<string>();
                for (int i = 0; i < r2.FieldCount; i++) cols2.Add(r2.GetName(i));
                string colList2 = string.Join(", ", System.Linq.Enumerable.Select(cols2, c => $"`{c}`"));
                while (await r2.ReadAsync())
                {
                    var vals2 = new List<string>();
                    for (int i = 0; i < r2.FieldCount; i++) vals2.Add(SqlLiteral(r2.GetValue(i)));
                    sb.AppendLine($"INSERT IGNORE INTO `lock` ({colList2}) VALUES ({string.Join(", ", vals2)});");
                    totalRows++;
                }
            }
            catch { /* lock 表若不存在則略過 */ }

            await File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            progress?.Report($"✓ 備份完成！共 {totalRows} 筆記錄");
            await GmLogger.Instance.LogAsync("資料庫備份", "全服",
                $"備份至：{filePath}，共 {totalRows} 筆", true);
            return (totalRows, filePath);
        }

        /// <summary>
        /// 從備份 SQL 檔案還原資料（INSERT IGNORE，不覆蓋現有記錄）。
        /// 返回 (成功筆數, 失敗筆數, 錯誤訊息列表)。
        /// </summary>
        public async Task<(int success, int fail, List<string> errors)> RestoreFromBackupAsync(
            string filePath, IProgress<string> progress = null)
        {
            if (!File.Exists(filePath))
                return (0, 0, new List<string> { "找不到備份檔案：" + filePath });

            string[] lines = await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8);
            int success = 0, fail = 0, processed = 0;
            var errors = new List<string>();

            using var conn = GetConnection();
            await conn.OpenAsync();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("--")) continue;
                if (!trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    using var cmd = new MySqlCommand(trimmed, conn);
                    await cmd.ExecuteNonQueryAsync();
                    success++;
                }
                catch (Exception ex)
                {
                    fail++;
                    if (errors.Count < 20)
                        errors.Add($"第 {processed + 1} 筆：{ex.Message.Replace("\n", " ")}");
                }

                processed++;
                if (processed % 20 == 0)
                    progress?.Report($"已處理 {processed} 筆，成功 {success}，失敗 {fail}…");
            }

            progress?.Report($"✓ 還原完成！成功 {success} 筆，失敗 {fail} 筆");
            await GmLogger.Instance.LogAsync("資料庫還原", "全服",
                $"從 {Path.GetFileName(filePath)} 還原，成功:{success} 失敗:{fail}", success > 0);
            return (success, fail, errors);
        }

        private static string SqlLiteral(object val)
        {
            if (val == null || val == DBNull.Value) return "NULL";
            return val switch
            {
                int    v => v.ToString(),
                long   v => v.ToString(),
                uint   v => v.ToString(),
                ulong  v => v.ToString(),
                decimal v => v.ToString(),
                double  v => v.ToString(),
                float   v => v.ToString(),
                bool    v => v ? "1" : "0",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                _       => "'" + val.ToString()
                                    .Replace("\\", "\\\\")
                                    .Replace("'",  "\\'")
                                    .Replace("\n", "\\n")
                                    .Replace("\r", "\\r")
                                    .Replace("\0", "\\0") + "'"
            };
        }

        // ══════════════════════════════════════════════════════════
        // 玩家寵物清單（capturepet）
        // cdkey 可能存登入帳號、角色名或 uid，全部嘗試
        // ══════════════════════════════════════════════════════════
        public async Task<List<PetInfo>> GetPlayerPetsAsync(string account, string charName = "")
        {
            var list = new List<PetInfo>();
            using var conn = GetConnection();
            await conn.OpenAsync();

            // 從 csalogin 取 OnlineName + uid，用於擴大比對範圍
            string uid = "";
            try
            {
                using var nc = new MySqlCommand(
                    "SELECT OnlineName, uid FROM csalogin WHERE `Name`=@n LIMIT 1", conn);
                nc.Parameters.AddWithValue("@n", account);
                using var nr = await nc.ExecuteReaderAsync();
                if (await nr.ReadAsync())
                {
                    if (string.IsNullOrEmpty(charName))
                        charName = nr["OnlineName"]?.ToString() ?? "";
                    uid = nr["uid"]?.ToString() ?? "";
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }

            // 同時比對：登入帳號 / 角色名 / uid；遊戲可能把擁有者存於 author（角色名），一併比對
            using var cmd = new MySqlCommand(
                @"SELECT unicode,id,name,type,lv,hp,attack,def,quick,sum,author,cdkey,`check`
                  FROM capturepet
                  WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid)
                     OR author=@cname OR author=@acc
                  ORDER BY sum DESC", conn);
            cmd.Parameters.AddWithValue("@acc",   account);
            cmd.Parameters.AddWithValue("@cname", charName);
            cmd.Parameters.AddWithValue("@uid",   uid);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PetInfo
                {
                    Unicode = r["unicode"]?.ToString() ?? "",
                    Id      = Convert.ToInt32(r["id"]),
                    Name    = r["name"]?.ToString() ?? "",
                    Type    = r["type"]?.ToString() ?? "",
                    Lv      = Convert.ToInt32(r["lv"]),
                    Hp      = Convert.ToInt32(r["hp"]),
                    Attack  = Convert.ToInt32(r["attack"]),
                    Def     = Convert.ToInt32(r["def"]),
                    Quick   = Convert.ToInt32(r["quick"]),
                    Sum     = Convert.ToDouble(r["sum"]),
                    Author  = r["author"]?.ToString() ?? "",
                    Cdkey   = r["cdkey"]?.ToString() ?? "",
                    Check   = Convert.ToInt32(r["check"])
                });
            return list;
        }

        /// <summary>取得 capturepet 中所有不重複的寵物種類（id + name + type）供下拉選單使用。</summary>
        public async Task<List<(int id, string name, string type)>> GetPetTypesAsync()
        {
            var list = new List<(int, string, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT id, name, type FROM capturepet
                      GROUP BY id, name, type ORDER BY id ASC", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["id"]   == DBNull.Value ? 0  : Convert.ToInt32(r["id"]),
                        r["name"]?.ToString() ?? "",
                        r["type"]?.ToString() ?? ""));
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        /// <summary>診斷用：找出 capturepet 中所有可能屬於此玩家的記錄及 cdkey 格式。</summary>
        public async Task<(string name, string onlineName, string uid,
                           List<(string cdkey, string author, string petName, int petId)> byId,
                           List<(string cdkey, string author, string petName, int petId)> sample)>
            DiagnosePetCdkeyAsync(string account, string charName)
        {
            string dbName = "", dbOnline = "", dbUid = "";
            var byId   = new List<(string, string, string, int)>();
            var sample = new List<(string, string, string, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();

                // 1. csalogin 資料
                using (var c1 = new MySqlCommand(
                    "SELECT `Name`, OnlineName, uid FROM csalogin WHERE `Name`=@n LIMIT 1", conn))
                {
                    c1.Parameters.AddWithValue("@n", account);
                    using var r1 = await c1.ExecuteReaderAsync();
                    if (await r1.ReadAsync())
                    {
                        dbName   = r1["Name"]?.ToString()       ?? "";
                        dbOnline = r1["OnlineName"]?.ToString() ?? "";
                        dbUid    = r1["uid"]?.ToString()        ?? "";
                    }
                }
                if (string.IsNullOrEmpty(charName)) charName = dbOnline;

                // 2. 用所有已知識別碼搜 cdkey
                string uid2 = dbUid;
                using (var c2 = new MySqlCommand(
                    @"SELECT cdkey, author, name, id FROM capturepet
                      WHERE cdkey=@acc OR cdkey=@cname OR cdkey=@uid
                      LIMIT 20", conn))
                {
                    c2.Parameters.AddWithValue("@acc",   dbName);
                    c2.Parameters.AddWithValue("@cname", charName);
                    c2.Parameters.AddWithValue("@uid",   uid2);
                    using var r2 = await c2.ExecuteReaderAsync();
                    while (await r2.ReadAsync())
                        byId.Add((r2["cdkey"]?.ToString()  ?? "",
                                  r2["author"]?.ToString() ?? "",
                                  r2["name"]?.ToString()   ?? "",
                                  r2["id"] == DBNull.Value ? 0 : Convert.ToInt32(r2["id"])));
                }

                // 3. 無論如何，取 capturepet 前 10 筆當樣本（看 cdkey 格式）
                using (var c3 = new MySqlCommand(
                    "SELECT cdkey, author, name, id FROM capturepet ORDER BY id DESC LIMIT 10", conn))
                {
                    using var r3 = await c3.ExecuteReaderAsync();
                    while (await r3.ReadAsync())
                        sample.Add((r3["cdkey"]?.ToString()  ?? "",
                                    r3["author"]?.ToString() ?? "",
                                    r3["name"]?.ToString()   ?? "",
                                    r3["id"] == DBNull.Value ? 0 : Convert.ToInt32(r3["id"])));
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB-diag] " + dbEx.Message); }
            return (dbName, dbOnline, dbUid, byId, sample);
        }

        // ══════════════════════════════════════════════════════════
        // 同 IP / 同 MAC 帳號查詢
        // ══════════════════════════════════════════════════════════
        public async Task<List<PlayerInfo>> GetSameIpAccountsAsync(string ip)
        {
            var list = new List<PlayerInfo>();
            if (string.IsNullOrWhiteSpace(ip)) return list;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                @"SELECT `Name`, OnlineName, Online, LoginTime, MasterId
                  FROM csalogin WHERE `IP`=@ip OR `RegIP`=@ip ORDER BY LoginTime DESC LIMIT 200", conn);
            cmd.Parameters.AddWithValue("@ip", ip);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PlayerInfo
                {
                    Account    = r["Name"]?.ToString() ?? "",
                    OnlineName = r["OnlineName"]?.ToString() ?? "",
                    IsOnline   = Convert.ToInt32(r["Online"]) == 1,
                    LoginTime  = r["LoginTime"]?.ToString() ?? "",
                    MasterId   = Convert.ToInt32(r["MasterId"])
                });
            return list;
        }

        public async Task<List<PlayerInfo>> GetSameMacAccountsAsync(string mac)
        {
            var list = new List<PlayerInfo>();
            if (string.IsNullOrWhiteSpace(mac)) return list;
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                @"SELECT `Name`, OnlineName, Online, LoginTime, MasterId
                  FROM csalogin WHERE `MAC1`=@mac ORDER BY LoginTime DESC LIMIT 200", conn);
            cmd.Parameters.AddWithValue("@mac", mac);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PlayerInfo
                {
                    Account    = r["Name"]?.ToString() ?? "",
                    OnlineName = r["OnlineName"]?.ToString() ?? "",
                    IsOnline   = Convert.ToInt32(r["Online"]) == 1,
                    LoginTime  = r["LoginTime"]?.ToString() ?? "",
                    MasterId   = Convert.ToInt32(r["MasterId"])
                });
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 待給予道具佇列（itempetgetdata）
        // ══════════════════════════════════════════════════════════
        public async Task<List<ItemQueueEntry>> GetItemQueueAsync(string filter = "")
        {
            // 查 maildata 中尚未被領取的道具郵件（check=0 且 deleamill=0）
            var list = new List<ItemQueueEntry>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            string filterWhere = string.IsNullOrWhiteSpace(filter)
                ? ""
                : "AND (m.cdkey LIKE @f OR c.OnlineName LIKE @f OR m.buff1 LIKE @f)";
            using var cmd = new MySqlCommand(
                $@"SELECT m.id AS mail_id, m.cdkey, m.buff1, m.data, m.endtime,
                          IFNULL(c.OnlineName,'') AS CharName
                   FROM maildata m
                   LEFT JOIN csalogin c ON c.`Name` = m.cdkey
                   WHERE m.`check`=0 AND m.deleamill=0
                   {filterWhere}
                   ORDER BY m.id DESC LIMIT 500", conn);
            if (!string.IsNullOrWhiteSpace(filter))
                cmd.Parameters.AddWithValue("@f", $"%{filter}%");
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int endTs = Convert.ToInt32(r["endtime"]);
                var endDt = DateTimeOffset.FromUnixTimeSeconds(endTs).LocalDateTime;
                list.Add(new ItemQueueEntry
                {
                    MailId   = Convert.ToInt32(r["mail_id"]),
                    Cdkey    = r["cdkey"]?.ToString() ?? "",
                    CharName = r["CharName"]?.ToString() ?? "",
                    ItemId   = Convert.ToInt32(r["data"]),
                    ItemName = r["buff1"]?.ToString() ?? "",
                    EndDate  = endDt.ToString("MM/dd HH:mm")
                });
            }
            return list;
        }

        public async Task<int> DeleteItemQueueEntriesAsync(int mailId)
        {
            // 刪除 maildata 中指定郵件（取消給予）
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "DELETE FROM maildata WHERE id=@id LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@id", mailId);
            return await cmd.ExecuteNonQueryAsync();
        }

        // ══════════════════════════════════════════════════════════
        // 封號清單（lock 表）
        // ══════════════════════════════════════════════════════════
        public async Task<List<(string account, string charName, long endUnix)>> GetAllBannedPlayersAsync()
        {
            var list = new List<(string, string, long)>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                @"SELECT l.`Name`, IFNULL(c.OnlineName,'') AS CharName, l.`time`
                  FROM `lock` l LEFT JOIN csalogin c ON c.`Name`=l.`Name`
                  ORDER BY l.`time` ASC", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add((r["Name"]?.ToString() ?? "", r["CharName"]?.ToString() ?? "", Convert.ToInt64(r["time"])));
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 線上玩家
        // ══════════════════════════════════════════════════════════
        public async Task<List<PlayerInfo>> GetOnlinePlayersAsync()
        {
            var list = new List<PlayerInfo>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                @"SELECT c.`Name`, c.OnlineName, c.Online, c.LoginTime, c.ServerId,
                         IFNULL(m.`Name`,'') AS MasterName,
                         IFNULL(p.point,0)   AS PayTotal
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  LEFT JOIN paydata p        ON p.cdkey=c.`Name`
                  WHERE c.Online=1
                  ORDER BY c.LoginTime DESC", conn);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PlayerInfo
                {
                    Account    = r["Name"]?.ToString()      ?? "",
                    OnlineName = r["OnlineName"]?.ToString() ?? "",
                    IsOnline   = true,
                    LoginTime  = r["LoginTime"]?.ToString()  ?? "",
                    ServerId   = r["ServerId"]?.ToString()   ?? "",
                    MasterName = r["MasterName"]?.ToString() ?? "",
                    PayTotal   = Convert.ToInt64(r["PayTotal"])
                });
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // VIP 玩家查詢
        // ══════════════════════════════════════════════════════════
        /// <summary>取得所有 VIP 玩家（累計儲值 ≥ 黃金門檻）</summary>
        public async Task<List<PlayerInfo>> GetVipPlayersAsync()
        {
            var list = new List<PlayerInfo>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                @"SELECT c.`Name`, c.OnlineName, c.Online, c.LoginTime,
                         IFNULL(m.`Name`,'') AS MasterName,
                         IFNULL(p.point,0)   AS PayTotal
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id    = c.MasterId
                  LEFT JOIN paydata p        ON p.cdkey = c.`Name`
                  WHERE IFNULL(p.point,0) >= @threshold
                  ORDER BY p.point DESC", conn);
            cmd.Parameters.AddWithValue("@threshold", VipHelper.GoldThreshold);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new PlayerInfo
                {
                    Account    = r["Name"]?.ToString()       ?? "",
                    OnlineName = r["OnlineName"]?.ToString() ?? "",
                    IsOnline   = r["Online"] != DBNull.Value && Convert.ToInt32(r["Online"]) == 1,
                    LoginTime  = r["LoginTime"]?.ToString()  ?? "",
                    MasterName = r["MasterName"]?.ToString() ?? "",
                    PayTotal   = r["PayTotal"] == DBNull.Value ? 0 : Convert.ToInt64(r["PayTotal"])
                });
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 批量金幣修改
        // ══════════════════════════════════════════════════════════
        public async Task<(int success, int fail)> BatchGiveGoldAsync(
            List<string> accounts, long amount,
            IProgress<(int done, int total, string acc, bool ok)> progress,
            System.Threading.CancellationToken ct)
        {
            int success = 0, fail = 0, total = accounts.Count;
            if (total == 0) return (0, 0);

            // 批量 UPDATE 使用 IN 子句，每批最多 500 筆（效能大幅提升）
            const int batchSize = 500;
            using var conn = GetConnection();
            await conn.OpenAsync();

            for (int i = 0; i < total; i += batchSize)
            {
                if (ct.IsCancellationRequested) break;
                var batch = accounts.Skip(i).Take(batchSize).ToList();
                try
                {
                    var paramNames = batch.Select((_, idx) => $"@n{idx}").ToList();
                    string inClause = string.Join(",", paramNames);
                    string sql = amount >= 0
                        ? $"UPDATE csalogin SET VipPoint=VipPoint+@v WHERE `Name` IN ({inClause})"
                        : $"UPDATE csalogin SET VipPoint=GREATEST(0,VipPoint+@v) WHERE `Name` IN ({inClause})";
                    using var cmd = new MySqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@v", amount);
                    for (int j = 0; j < batch.Count; j++)
                        cmd.Parameters.AddWithValue(paramNames[j], batch[j]);
                    int affected = await cmd.ExecuteNonQueryAsync();
                    success += affected;
                    fail    += batch.Count - affected;
                }
                catch (Exception ex)
                {
                    fail += batch.Count;
                    await GmLogger.Instance.LogAsync("批量金幣[錯誤]", $"批次 {i/batchSize+1}", ex.Message, false);
                }
                int done = Math.Min(i + batchSize, total);
                progress?.Report((done, total, $"第 {i / batchSize + 1} 批（{batch.Count} 人）", fail == 0));
            }

            string op = amount >= 0 ? $"發放 {amount:N0} 金幣" : $"扣除 {Math.Abs(amount):N0} 金幣";
            await GmLogger.Instance.LogAsync("批量金幣", $"{total} 位玩家",
                $"{op}，成功:{success} 失敗:{fail}", success > 0);
            return (success, fail);
        }

        /// <summary>強制將玩家設為離線（Online = 0）</summary>
        public async Task<bool> ForceOfflineAsync(string account)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand("UPDATE csalogin SET Online=0 WHERE `Name`=@n", conn);
                cmd.Parameters.AddWithValue("@n", account);
                bool ok = await cmd.ExecuteNonQueryAsync() > 0;
                if (ok) await GmLogger.Instance.LogAsync("強制下線", account, "Online 已設為 0", true);
                return ok;
            }
            catch (Exception ex)
            {
                await GmLogger.Instance.LogAsync("強制下線[錯誤]", account, ex.Message, false);
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // GM 標記 / 群組修改
        // ══════════════════════════════════════════════════════════
        public async Task<bool> SetPlayerPermAsync(string account, int neiCe, int groupId)
        {
            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE csalogin SET NeiCe=@nc, GroupId=@gid WHERE `Name`=@n", conn);
            cmd.Parameters.AddWithValue("@nc",  neiCe);
            cmd.Parameters.AddWithValue("@gid", groupId);
            cmd.Parameters.AddWithValue("@n",   account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("修改權限", account,
                $"NeiCe={neiCe} GroupId={groupId}", true);
            return ok;
        }

        /// <summary>查詢全部玩家的 GM 標記（用於 GM 管理清單）</summary>
        public async Task<List<GameGmInfo>> GetAllPlayersGmInfoAsync(string searchName = "")
        {
            var list = new List<GameGmInfo>();
            using var conn = GetConnection();
            await conn.OpenAsync();
            string sql = string.IsNullOrWhiteSpace(searchName)
                ? @"SELECT `Name`, OnlineName, GroupId, NeiCe, Online
                    FROM csalogin ORDER BY NeiCe DESC, GroupId DESC, LoginTime DESC LIMIT 1000"
                : @"SELECT `Name`, OnlineName, GroupId, NeiCe, Online
                    FROM csalogin WHERE OnlineName LIKE @q
                    ORDER BY NeiCe DESC, GroupId DESC LIMIT 500";
            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(searchName))
                cmd.Parameters.AddWithValue("@q", $"%{searchName}%");
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new GameGmInfo
                {
                    Account    = r["Name"]?.ToString() ?? "",
                    OnlineName = r["OnlineName"]?.ToString() ?? "",
                    GroupId    = Convert.ToInt32(r["GroupId"]),
                    NeiCe      = Convert.ToInt32(r["NeiCe"]),
                    IsOnline   = Convert.ToInt32(r["Online"]) == 1
                });
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 分析模組 — 玩家活躍度
        // ══════════════════════════════════════════════════════════

        /// <summary>24小時登入分佈（index = 小時 0-23）</summary>
        public async Task<int[]> GetLoginHourDistributionAsync()
        {
            var result = new int[24];
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand(
                    "SELECT HOUR(LoginTime) AS h, COUNT(*) AS cnt FROM csalogin WHERE LoginTime IS NOT NULL AND LoginTime > '2000-01-01' GROUP BY h", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int h = Convert.ToInt32(r["h"]);
                    if (h >= 0 && h < 24) result[h] = Convert.ToInt32(r["cnt"]);
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return result;
        }

        /// <summary>星期幾登入分佈（index 0=周日…6=周六）</summary>
        public async Task<int[]> GetLoginWeekdayDistributionAsync()
        {
            var result = new int[7];
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand(
                    "SELECT DAYOFWEEK(LoginTime)-1 AS d, COUNT(*) AS cnt FROM csalogin WHERE LoginTime IS NOT NULL AND LoginTime > '2000-01-01' GROUP BY d", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int d = Convert.ToInt32(r["d"]);
                    if (d >= 0 && d < 7) result[d] = Convert.ToInt32(r["cnt"]);
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return result;
        }

        /// <summary>最近 N 天每日新增帳號數</summary>
        public async Task<(DateTime[] dates, int[] counts)> GetDailyNewAccountsAsync(int days = 30)
        {
            var dateList  = new List<DateTime>();
            var countList = new List<int>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand(
                    $"SELECT DATE(created_at) AS d, COUNT(*) AS cnt FROM csalogin WHERE created_at >= DATE_SUB(CURDATE(), INTERVAL {days} DAY) GROUP BY d ORDER BY d", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    dateList.Add(Convert.ToDateTime(r["d"]));
                    countList.Add(Convert.ToInt32(r["cnt"]));
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return (dateList.ToArray(), countList.ToArray());
        }

        /// <summary>玩家留存率（7/14/30/90天）</summary>
        public async Task<Dictionary<string, (int cohort, int retained, double rate)>> GetRetentionAsync()
        {
            var result = new Dictionary<string, (int, int, double)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                foreach (var (label, days) in new[] { ("7天", 7), ("14天", 14), ("30天", 30), ("90天", 90) })
                {
                    using var cmd = new MySqlCommand(@$"
                        SELECT COUNT(*) AS cohort,
                               SUM(CASE WHEN LoginTime >= DATE_SUB(NOW(), INTERVAL {days} DAY) THEN 1 ELSE 0 END) AS retained
                        FROM csalogin
                        WHERE created_at >= DATE_SUB(CURDATE(), INTERVAL {days} DAY)
                          AND created_at IS NOT NULL", conn);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        int   c  = r["cohort"]   == DBNull.Value ? 0 : Convert.ToInt32(r["cohort"]);
                        int   re = r["retained"] == DBNull.Value ? 0 : Convert.ToInt32(r["retained"]);
                        double rt = c > 0 ? (double)re / c * 100 : 0;
                        result[label] = (c, re, rt);
                    }
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return result;
        }

        /// <summary>沉睡玩家清單（超過 N 天未登入）</summary>
        public async Task<List<(string name, string account, string lastLogin, int daysSince)>> GetInactivePlayersAsync(int days = 30, int limit = 200)
        {
            var list = new List<(string, string, string, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand($@"
                    SELECT OnlineName, `Name`, LoginTime,
                           DATEDIFF(NOW(), LoginTime) AS days_since
                    FROM csalogin
                    WHERE LoginTime < DATE_SUB(NOW(), INTERVAL {days} DAY)
                      AND LoginTime IS NOT NULL AND LoginTime > '2000-01-01'
                    ORDER BY LoginTime ASC LIMIT {limit}", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((r["OnlineName"]?.ToString() ?? "",
                              r["Name"]?.ToString() ?? "",
                              r["LoginTime"]?.ToString() ?? "",
                              r["days_since"] == DBNull.Value ? 0 : Convert.ToInt32(r["days_since"])));
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        // ══════════════════════════════════════════════════════════
        // 分析模組 — 充值趨勢
        // ══════════════════════════════════════════════════════════

        /// <summary>最近 N 天每日充值金額與筆數</summary>
        public async Task<(DateTime[] dates, decimal[] amounts, int[] counts)> GetDailyRechargeAsync(int days = 30)
        {
            var dl = new List<DateTime>(); var al = new List<decimal>(); var cl = new List<int>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand($@"
                    SELECT DATE(created_at) AS d, SUM(amount) AS total, COUNT(*) AS cnt
                    FROM recharge_orders WHERE status='completed'
                      AND created_at >= DATE_SUB(CURDATE(), INTERVAL {days} DAY)
                    GROUP BY d ORDER BY d", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    dl.Add(Convert.ToDateTime(r["d"]));
                    al.Add(r["total"] == DBNull.Value ? 0 : Convert.ToDecimal(r["total"]));
                    cl.Add(r["cnt"]   == DBNull.Value ? 0 : Convert.ToInt32(r["cnt"]));
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return (dl.ToArray(), al.ToArray(), cl.ToArray());
        }

        /// <summary>最近 12 個月月度充值</summary>
        public async Task<(string[] months, decimal[] amounts, int[] counts)> GetMonthlyRechargeAsync()
        {
            var ml = new List<string>(); var al = new List<decimal>(); var cl = new List<int>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand(@"
                    SELECT DATE_FORMAT(created_at,'%Y-%m') AS m, SUM(amount) AS total, COUNT(*) AS cnt
                    FROM recharge_orders WHERE status='completed'
                      AND created_at >= DATE_SUB(CURDATE(), INTERVAL 12 MONTH)
                    GROUP BY m ORDER BY m", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    ml.Add(r["m"]?.ToString() ?? "");
                    al.Add(r["total"] == DBNull.Value ? 0 : Convert.ToDecimal(r["total"]));
                    cl.Add(r["cnt"]   == DBNull.Value ? 0 : Convert.ToInt32(r["cnt"]));
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return (ml.ToArray(), al.ToArray(), cl.ToArray());
        }

        /// <summary>付費分層（0/1-99/100-499/500-999/1000-4999/5000+）</summary>
        public async Task<Dictionary<string, int>> GetPaymentTierAsync()
        {
            var result = new Dictionary<string, int>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                int total = Convert.ToInt32(await new MySqlCommand("SELECT COUNT(*) FROM csalogin", conn).ExecuteScalarAsync());
                using var cmd = new MySqlCommand(@"
                    SELECT
                        SUM(CASE WHEN PayTotal = 0                     THEN 1 ELSE 0 END) AS t0,
                        SUM(CASE WHEN PayTotal BETWEEN 1   AND 99      THEN 1 ELSE 0 END) AS t1,
                        SUM(CASE WHEN PayTotal BETWEEN 100 AND 499     THEN 1 ELSE 0 END) AS t2,
                        SUM(CASE WHEN PayTotal BETWEEN 500 AND 999     THEN 1 ELSE 0 END) AS t3,
                        SUM(CASE WHEN PayTotal BETWEEN 1000 AND 4999   THEN 1 ELSE 0 END) AS t4,
                        SUM(CASE WHEN PayTotal >= 5000                  THEN 1 ELSE 0 END) AS t5
                    FROM csalogin", conn);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    result["免費玩家"]   = r["t0"] == DBNull.Value ? 0 : Convert.ToInt32(r["t0"]);
                    result["$1-99"]     = r["t1"] == DBNull.Value ? 0 : Convert.ToInt32(r["t1"]);
                    result["$100-499"]  = r["t2"] == DBNull.Value ? 0 : Convert.ToInt32(r["t2"]);
                    result["$500-999"]  = r["t3"] == DBNull.Value ? 0 : Convert.ToInt32(r["t3"]);
                    result["$1000-4999"]= r["t4"] == DBNull.Value ? 0 : Convert.ToInt32(r["t4"]);
                    result["$5000+"]    = r["t5"] == DBNull.Value ? 0 : Convert.ToInt32(r["t5"]);
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return result;
        }

        /// <summary>首次付費距離註冊天數分佈（0/1-3/4-7/8-30/30+）</summary>
        public async Task<Dictionary<string, int>> GetTimeToFirstPaymentAsync()
        {
            var result = new Dictionary<string, int>
            {
                ["當天"] = 0, ["1-3天"] = 0, ["4-7天"] = 0, ["8-30天"] = 0, ["30天以上"] = 0
            };
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand(@"
                    SELECT DATEDIFF(MIN(o.created_at), c.created_at) AS days_to_first
                    FROM recharge_orders o
                    JOIN csalogin c ON c.`Name` = o.role_name
                    WHERE o.status = 'completed' AND c.created_at IS NOT NULL
                    GROUP BY o.role_name", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int d = r["days_to_first"] == DBNull.Value ? 0 : Convert.ToInt32(r["days_to_first"]);
                    if      (d <= 0)  result["當天"]++;
                    else if (d <= 3)  result["1-3天"]++;
                    else if (d <= 7)  result["4-7天"]++;
                    else if (d <= 30) result["8-30天"]++;
                    else              result["30天以上"]++;
                }
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return result;
        }

        // ══════════════════════════════════════════════════════════
        // 分析模組 — 交易稽核
        // ══════════════════════════════════════════════════════════

        /// <summary>高頻交易配對（同兩帳號短時間多次交易）</summary>
        public async Task<List<(string from, string fromName, string to, string toName, int cnt, string lastTime)>> GetFrequentTradePairsAsync(int minCount = 10)
        {
            var list = new List<(string, string, string, string, int, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand($@"
                    SELECT mecdkey, mename, tocdkey, toname,
                           COUNT(*) AS cnt, MAX(FROM_UNIXTIME(time)) AS last_time
                    FROM tradelog
                    GROUP BY mecdkey, tocdkey
                    HAVING cnt >= {minCount}
                    ORDER BY cnt DESC LIMIT 100", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((r["mecdkey"]?.ToString() ?? "",
                              r["mename"]?.ToString()  ?? "",
                              r["tocdkey"]?.ToString() ?? "",
                              r["toname"]?.ToString()  ?? "",
                              Convert.ToInt32(r["cnt"]),
                              r["last_time"]?.ToString() ?? ""));
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        /// <summary>同 IP 帳號之間的交易（可能互刷）</summary>
        public async Task<List<(string from, string to, int cnt, string sharedIp)>> GetSameIpTradesAsync(int minCount = 5)
        {
            var list = new List<(string, string, int, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                // 找出同IP帳號對，再比對 tradelog
                using var cmd  = new MySqlCommand($@"
                    SELECT t.mecdkey, t.tocdkey, COUNT(*) AS cnt, a.IP AS shared_ip
                    FROM tradelog t
                    JOIN csalogin a ON a.`Name` = t.mecdkey
                    JOIN csalogin b ON b.`Name` = t.tocdkey
                    WHERE a.IP = b.IP AND a.IP IS NOT NULL AND a.IP != ''
                    GROUP BY t.mecdkey, t.tocdkey, a.IP
                    HAVING cnt >= {minCount}
                    ORDER BY cnt DESC LIMIT 100", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((r["mecdkey"]?.ToString()  ?? "",
                              r["tocdkey"]?.ToString()  ?? "",
                              Convert.ToInt32(r["cnt"]),
                              r["shared_ip"]?.ToString() ?? ""));
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        /// <summary>金幣異動大額排行（goldlog）</summary>
        public async Task<List<(string account, string name, long totalGain, long totalLoss, int entries)>> GetGoldAnomalyAsync(int limit = 50)
        {
            var list = new List<(string, string, long, long, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand($@"
                    SELECT g.cdkey,
                           IFNULL(c.OnlineName,'') AS cname,
                           SUM(CASE WHEN g.point > 0 THEN g.point ELSE 0 END)  AS gain,
                           SUM(CASE WHEN g.point < 0 THEN ABS(g.point) ELSE 0 END) AS loss,
                           COUNT(*) AS entries
                    FROM goldlog g
                    LEFT JOIN csalogin c ON c.`Name` = g.cdkey
                    GROUP BY g.cdkey
                    ORDER BY gain DESC LIMIT {limit}", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((r["cdkey"]?.ToString()  ?? "",
                              r["cname"]?.ToString()  ?? "",
                              r["gain"]    == DBNull.Value ? 0 : Convert.ToInt64(r["gain"]),
                              r["loss"]    == DBNull.Value ? 0 : Convert.ToInt64(r["loss"]),
                              r["entries"] == DBNull.Value ? 0 : Convert.ToInt32(r["entries"])));
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        /// <summary>交易量排行（tradelog，發出方）</summary>
        public async Task<List<(string account, string name, int tradeCnt, string lastTime)>> GetTopTradersAsync(int limit = 50)
        {
            var list = new List<(string, string, int, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd  = new MySqlCommand($@"
                    SELECT mecdkey, mename,
                           COUNT(*) AS cnt,
                           MAX(FROM_UNIXTIME(time)) AS last_time
                    FROM tradelog
                    GROUP BY mecdkey
                    ORDER BY cnt DESC LIMIT {limit}", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((r["mecdkey"]?.ToString() ?? "",
                              r["mename"]?.ToString()  ?? "",
                              Convert.ToInt32(r["cnt"]),
                              r["last_time"]?.ToString() ?? ""));
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return list;
        }

        /// <summary>交易量摘要統計</summary>
        // ══════════════════════════════════════════════════════════
        // 資料庫表探索
        // ══════════════════════════════════════════════════════════
        /// <summary>列出所有資料表名稱與估計筆數，供探索未知表使用</summary>
        public async Task<List<(string table, long rows, string columns)>> GetAllTablesInfoAsync()
        {
            var list = new List<(string, long, string)>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                // 先取得所有表名
                var tables = new List<string>();
                using (var cmd = new MySqlCommand("SHOW TABLES", conn))
                using (var r   = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync()) tables.Add(r.GetString(0));

                // 查每個表的精確筆數與欄位
                foreach (var tbl in tables)
                {
                    long rowCnt = 0;
                    string colStr = "";
                    try
                    {
                        using var c1 = new MySqlCommand($"SELECT COUNT(*) FROM `{tbl}`", conn);
                        var rr = await c1.ExecuteScalarAsync();
                        rowCnt = rr == DBNull.Value || rr == null ? 0 : Convert.ToInt64(rr);
                    }
                    catch { }
                    try
                    {
                        var cols = new List<string>();
                        using var c2 = new MySqlCommand($"SHOW COLUMNS FROM `{tbl}`", conn);
                        using var r2 = await c2.ExecuteReaderAsync();
                        while (await r2.ReadAsync()) cols.Add(r2.GetString(0));
                        colStr = string.Join(", ", cols.Take(8)) + (cols.Count > 8 ? "…" : "");
                    }
                    catch { }
                    list.Add((tbl, rowCnt, colStr));
                }
            }
            catch { }
            return list;
        }

        /// <summary>讀取任意表的前 N 筆資料（動態欄位），供探索使用</summary>
        public async Task<(List<string> cols, List<Dictionary<string, string>> rows)> PreviewTableAsync(string tableName, int limit = 50)
        {
            var cols = new List<string>();
            var rows = new List<Dictionary<string, string>>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand($"SELECT * FROM `{tableName}` LIMIT @n", conn);
                cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 200));
                using var r = await cmd.ExecuteReaderAsync();
                for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
                while (await r.ReadAsync())
                {
                    var row = new Dictionary<string, string>();
                    for (int i = 0; i < r.FieldCount; i++)
                        row[cols[i]] = r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
                    rows.Add(row);
                }
            }
            catch { }
            return (cols, rows);
        }

        // capturepet 欄位探索
        // ══════════════════════════════════════════════════════════
        /// <summary>回傳任意表所有欄位名稱（小寫），找不到回傳空集合</summary>
        public async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand($"SHOW COLUMNS FROM `{tableName}`", conn);
                using var r   = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    cols.Add(r.GetString(0).ToLower());
            }
            catch { }
            return cols;
        }

        /// <summary>回傳 capturepet 所有欄位名稱（小寫）</summary>
        public Task<HashSet<string>> GetCapturePetColumnsAsync() => GetTableColumnsAsync("capturepet");

        /// <summary>自動偵測寵物主表名稱（capturepet / PETNO / petno 等）</summary>
        public async Task<string> DetectPetTableAsync()
        {
            var candidates = new[] { "petbilling", "capturepet", "PETNO", "petno", "petdata",
                                     "petinfo", "pet_info", "csapet", "playerpet" };
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                var exist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new MySqlCommand("SHOW TABLES", conn))
                using (var r   = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync()) exist.Add(r.GetString(0));

                foreach (var c in candidates)
                    if (exist.Contains(c))
                    {
                        // 確認有資料
                        using var cnt = new MySqlCommand($"SELECT COUNT(*) FROM `{c}`", conn);
                        long n = Convert.ToInt64(await cnt.ExecuteScalarAsync() ?? 0L);
                        if (n > 0) return c;
                    }
                // 最後 fallback：找第一個有 hp 或 attack 欄位且有資料的表
                foreach (var tbl in exist)
                {
                    try
                    {
                        var cols = new List<string>();
                        using var cc = new MySqlCommand($"SHOW COLUMNS FROM `{tbl}`", conn);
                        using var rc = await cc.ExecuteReaderAsync();
                        while (await rc.ReadAsync()) cols.Add(rc.GetString(0).ToLower());
                        if ((cols.Contains("hp") || cols.Contains("attack")) && cols.Contains("cdkey"))
                        {
                            using var cnt2 = new MySqlCommand($"SELECT COUNT(*) FROM `{tbl}`", conn);
                            long n2 = Convert.ToInt64(await cnt2.ExecuteScalarAsync() ?? 0L);
                            if (n2 > 1) return tbl;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        /// <summary>取得指定寵物種類(petId)的各項排行前 N 名，帶全欄位</summary>
        public async Task<List<Dictionary<string, object>>> GetPetSpeciesRankAsync(int petId, int topN = 10)
        {
            var list = new List<Dictionary<string, object>>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                string where = petId > 0 ? "WHERE p.id=@pid" : "";
                string sql = $@"
                    SELECT p.*, IFNULL(c.OnlineName, p.cdkey) AS _playerName, IFNULL(c.Online,0) AS _online
                    FROM capturepet p
                    LEFT JOIN csalogin c ON c.`Name` = p.cdkey
                    {where}
                    ORDER BY p.sum DESC
                    LIMIT @n";
                using var cmd = new MySqlCommand(sql, conn);
                if (petId > 0) cmd.Parameters.AddWithValue("@pid", petId);
                cmd.Parameters.AddWithValue("@n", Math.Clamp(topN, 1, 500));
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < r.FieldCount; i++)
                        row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                    list.Add(row);
                }
            }
            catch { }
            return list;
        }

        // 寵物排行榜（capturepet）
        // ══════════════════════════════════════════════════════════
        /// <summary>依指定欄位取得寵物排行（sum/hp/attack/def/quick），可依 petId 篩選特定寵物種類</summary>
        public async Task<(List<PetRankRow> rows, string error)> GetPetRankingAsync(string orderCol, int topN = 100, int petId = 0, string tableName = "capturepet")
        {
            var safe = new HashSet<string> { "sum", "hp", "attack", "def", "quick" };
            if (!safe.Contains(orderCol)) orderCol = "sum";

            var list = new List<PetRankRow>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                // 先確認表是否存在且有資料
                long count = 0;
                using (var cntCmd = new MySqlCommand($"SELECT COUNT(*) FROM `{tableName}`", conn))
                    count = Convert.ToInt64(await cntCmd.ExecuteScalarAsync() ?? 0L);

                if (count == 0) return (list, $"{tableName} 表存在但筆數為 0");

                // 動態偵測欄位（不同表欄位名可能不同）
                var tblCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cc = new MySqlCommand($"SHOW COLUMNS FROM `{tableName}`", conn))
                using (var rr = await cc.ExecuteReaderAsync())
                    while (await rr.ReadAsync()) tblCols.Add(rr.GetString(0));

                // 找欄位：找不到時用 NULL（避免 Unknown column 錯誤）
                string Pick(string alias, params string[] names)
                {
                    var found = names.FirstOrDefault(n => tblCols.Contains(n));
                    return found != null ? $"p.`{found}` AS `{alias}`" : $"NULL AS `{alias}`";
                }
                string PickOrder(params string[] names) => names.FirstOrDefault(n => tblCols.Contains(n));

                string colId     = (new[]{"id","petid","pet_id","PetId"}).FirstOrDefault(n => tblCols.Contains(n)) ?? "id";
                string colCdkey  = (new[]{"cdkey","account","uid"}).FirstOrDefault(n => tblCols.Contains(n)) ?? "cdkey";

                // 若 orderCol 欄位不存在，改用第一個存在的數值欄
                if (!tblCols.Contains(orderCol))
                {
                    orderCol = (new[]{"sum","power","combat","total","hp","attack","def","quick"})
                               .FirstOrDefault(n => tblCols.Contains(n)) ?? tblCols.First();
                }

                string where = petId > 0 ? $"WHERE p.`{colId}`=@pid " : "";
                string sql = $@"
                    SELECT {Pick("cdkey",   "cdkey","account","uid")},
                           {Pick("author",  "author","capturer","cdkey")},
                           {Pick("petName", "name","petname","pet_name","pname")},
                           {Pick("type",    "type","pettype","pet_type")},
                           p.`{colId}` AS petId,
                           {Pick("lv",      "lv","level","petlv")},
                           {Pick("hp",      "hp","HP")},
                           {Pick("attack",  "attack","atk","ATK")},
                           {Pick("def",     "def","DEF","defense")},
                           {Pick("quick",   "quick","spd","speed","agility")},
                           {Pick("sum",     "sum","power","combat","total","billing","score")},
                           IFNULL(c.OnlineName, p.`{colCdkey}`) AS playerName, c.Online
                    FROM `{tableName}` p
                    LEFT JOIN csalogin c ON c.`Name` = p.cdkey
                    {where}
                    ORDER BY p.`{orderCol}` DESC
                    LIMIT @n";
                using var cmd = new MySqlCommand(sql, conn);
                if (petId > 0) cmd.Parameters.AddWithValue("@pid", petId);
                cmd.Parameters.AddWithValue("@n", Math.Clamp(topN, 1, 500));
                using var r = await cmd.ExecuteReaderAsync();
                int rank = 1;
                while (await r.ReadAsync())
                {
                    list.Add(new PetRankRow
                    {
                        Rank       = rank++,
                        Cdkey      = r["cdkey"]?.ToString()     ?? "",
                        Author     = r["author"]?.ToString()    ?? "",
                        PetName    = r["petName"]?.ToString()   ?? "",
                        PetType    = r["type"]?.ToString()      ?? "",
                        PetId      = Convert.ToInt32(r["petId"]),
                        Lv         = r["lv"]   == DBNull.Value ? 0 : Convert.ToInt32(r["lv"]),
                        Hp         = r["hp"]   == DBNull.Value ? 0 : Convert.ToInt32(r["hp"]),
                        Attack     = r["attack"]== DBNull.Value ? 0 : Convert.ToInt32(r["attack"]),
                        Def        = r["def"]  == DBNull.Value ? 0 : Convert.ToInt32(r["def"]),
                        Quick      = r["quick"]== DBNull.Value ? 0 : Convert.ToInt32(r["quick"]),
                        Sum        = r["sum"]  == DBNull.Value ? 0.0 : Convert.ToDouble(r["sum"]),
                        PlayerName = r["playerName"]?.ToString() ?? "",
                        Online     = r["Online"] != DBNull.Value && Convert.ToInt32(r["Online"]) == 1,
                    });
                }
                return (list, null);
            }
            catch (Exception ex)
            {
                return (list, ex.Message);
            }
        }

        /// <summary>
        /// 嘗試讀取遊戲原生寵物排行榜表（SELECT *，原始欄位）。
        /// 已知可能的表名：rankpet / petrank / pet_rank / petranking
        /// 若找到則回傳 (tableName, cols, rows)；找不到回傳 (null, empty, empty)
        /// </summary>
        public async Task<(string tableName, List<string> cols, List<Dictionary<string,string>> rows)> GetGamePetRankRawAsync(int limit = 500)
        {
            var candidates = new[] { "petbilling", "petrank", "rankpet", "pet_rank", "petranking",
                                     "PETNO", "petno", "PetNo", "pet_no" };
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                var existTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var chk = new MySqlCommand("SHOW TABLES", conn))
                using (var rr  = await chk.ExecuteReaderAsync())
                    while (await rr.ReadAsync()) existTables.Add(rr.GetString(0));

                string found = candidates.FirstOrDefault(t => existTables.Contains(t));
                if (found == null) return (null, new(), new());

                // 找出 petbilling 欄位，偵測帳號欄（cdkey / account / userid）
                var petCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cc = new MySqlCommand($"SHOW COLUMNS FROM `{found}`", conn))
                using (var cr = await cc.ExecuteReaderAsync())
                    while (await cr.ReadAsync()) petCols.Add(cr.GetString(0));

                string cdkeyCol = petCols.FirstOrDefault(c =>
                    c.Equals("cdkey", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("account", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("userid", StringComparison.OrdinalIgnoreCase)) ?? "";

                // 如果有帳號欄且 csalogin 存在，就 JOIN 取得玩家暱稱
                bool hasLogin = !string.IsNullOrEmpty(cdkeyCol) && existTables.Contains("csalogin");

                string sql;
                if (hasLogin)
                {
                    // 偵測 csalogin 實際有哪些「暱稱」欄位可用
                    var loginCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var lc = new MySqlCommand("SHOW COLUMNS FROM `csalogin`", conn))
                    using (var lr = await lc.ExecuteReaderAsync())
                        while (await lr.ReadAsync()) loginCols.Add(lr.GetString(0));

                    // 依優先順序選名稱欄：OnlineName > NickName > Name
                    string nameExpr = loginCols.Contains("OnlineName") ? "l.OnlineName"
                                    : loginCols.Contains("NickName")   ? "l.NickName"
                                    : "l.Name";
                    string onlineExpr = loginCols.Contains("Online")
                        ? "IF(l.Online IS NOT NULL AND l.Online != 0, '🟢', '⚫')"
                        : "'⚫'";

                    sql = $@"SELECT p.*,
                                    COALESCE({nameExpr}, l.Name, p.`{cdkeyCol}`) AS _playerName,
                                    {onlineExpr} AS _online
                             FROM `{found}` p
                             LEFT JOIN `csalogin` l ON l.Name = p.`{cdkeyCol}`
                             LIMIT @n";
                }
                else
                {
                    sql = $"SELECT * FROM `{found}` LIMIT @n";
                }

                var cols = new List<string>();
                var rows = new List<Dictionary<string,string>>();
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 2000));
                using var r = await cmd.ExecuteReaderAsync();
                for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
                while (await r.ReadAsync())
                {
                    var row = new Dictionary<string,string>();
                    for (int i = 0; i < r.FieldCount; i++)
                        row[cols[i]] = r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
                    rows.Add(row);
                }
                return (found, cols, rows);
            }
            catch (Exception ex) { return ("ERROR: " + ex.Message, new(), new()); }
        }

        /// <summary>
        /// 重置 petbilling 排行記錄。
        /// nameFilter / typeFilter 為空時刪除全部；否則只刪除符合條件的列。
        /// cols 為已知欄位清單（用來猜測 name / type 欄位名稱）。
        /// 回傳影響筆數。
        /// </summary>
        /// <param name="cols">表所有欄位（用來驗證 filterCol 是否合法）</param>
        /// <param name="filterCol">要篩選的欄位名稱（空=刪全部）</param>
        /// <param name="filterVal">要篩選的值</param>
        public async Task<int> ResetPetBillingAsync(List<string> cols, string filterCol, string filterVal)
        {
            bool hasFilter = !string.IsNullOrEmpty(filterCol) && !string.IsNullOrEmpty(filterVal)
                             && cols.Any(c => c.Equals(filterCol, StringComparison.OrdinalIgnoreCase));
            string realCol = hasFilter ? cols.First(c => c.Equals(filterCol, StringComparison.OrdinalIgnoreCase)) : "";

            string sql = hasFilter
                ? $"DELETE FROM `petbilling` WHERE `{realCol}` = @val"
                : "DELETE FROM `petbilling`";

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(sql, conn);
            if (hasFilter) cmd.Parameters.AddWithValue("@val", filterVal);
            return await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 嘗試讀取遊戲原生寵物排行榜表（自動偵測表名）。
        /// 已知可能的表名：rankpet / petrank / pet_rank / petranking
        /// 若找到則回傳 (tableName, rows)；找不到回傳 (null, empty)
        /// </summary>
        public async Task<(string tableName, List<PetRankRow> rows)> GetGamePetRankAsync(int limit = 200)
        {
            var candidates = new[] { "rankpet", "petrank", "pet_rank", "petranking", "rank_pet", "pet_ranking" };
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                // 查出當前 DB 中存在的表名
                var existTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var chk = new MySqlCommand("SHOW TABLES", conn))
                using (var rr  = await chk.ExecuteReaderAsync())
                    while (await rr.ReadAsync())
                        existTables.Add(rr.GetString(0));

                string found = candidates.FirstOrDefault(t => existTables.Contains(t));
                if (found == null) return (null, new());

                // 讀出欄位名稱
                var cols = new List<string>();
                using (var cc = new MySqlCommand($"SHOW COLUMNS FROM `{found}`", conn))
                using (var rc = await cc.ExecuteReaderAsync())
                    while (await rc.ReadAsync())
                        cols.Add(rc.GetString(0).ToLower());

                // 動態建立 SELECT，盡量對應已知欄位
                string Pick(params string[] names) => names.FirstOrDefault(n => cols.Contains(n)) ?? "";

                string colCdkey  = Pick("cdkey", "account", "name");
                string colName   = Pick("petname", "pet_name", "name", "pname");
                string colType   = Pick("type", "ptype", "pettype");
                string colLv     = Pick("lv", "level", "petlv");
                string colHp     = Pick("hp");
                string colAtk    = Pick("attack", "atk");
                string colDef    = Pick("def", "defense");
                string colQck    = Pick("quick", "speed", "spd", "agility");
                string colSum    = Pick("sum", "power", "combat");
                string colRank   = Pick("rank", "rankno", "rank_no");
                string colAuthor = Pick("author", "capturer");

                var selParts = new List<string>();
                if (colCdkey  != "") selParts.Add($"`{colCdkey}` AS cdkey");
                if (colName   != "") selParts.Add($"`{colName}`  AS petName");
                if (colType   != "") selParts.Add($"`{colType}`  AS petType");
                if (colLv     != "") selParts.Add($"`{colLv}`    AS lv");
                if (colHp     != "") selParts.Add($"`{colHp}`    AS hp");
                if (colAtk    != "") selParts.Add($"`{colAtk}`   AS attack");
                if (colDef    != "") selParts.Add($"`{colDef}`   AS def");
                if (colQck    != "") selParts.Add($"`{colQck}`   AS quick");
                if (colSum    != "") selParts.Add($"`{colSum}`   AS sum");
                if (colRank   != "") selParts.Add($"`{colRank}`  AS rankno");
                if (colAuthor != "") selParts.Add($"`{colAuthor}` AS author");

                if (selParts.Count == 0) return (found, new());

                string orderBy = colSum != "" ? $"`{colSum}` DESC" : colRank != "" ? $"`{colRank}` ASC" : "1";
                string sql = $"SELECT {string.Join(", ", selParts)} FROM `{found}` ORDER BY {orderBy} LIMIT @n";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 2000));
                using var r = await cmd.ExecuteReaderAsync();

                var list = new List<PetRankRow>();
                int rank = 1;
                while (await r.ReadAsync())
                {
                    list.Add(new PetRankRow
                    {
                        Rank       = rank++,
                        Cdkey      = r.HasColumn("cdkey")   ? r["cdkey"]?.ToString()   ?? "" : "",
                        Author     = r.HasColumn("author")  ? r["author"]?.ToString()  ?? "" : "",
                        PetName    = r.HasColumn("petName") ? r["petName"]?.ToString() ?? "" : "",
                        PetType    = r.HasColumn("petType") ? r["petType"]?.ToString() ?? "" : "",
                        Lv         = r.HasColumn("lv")     && r["lv"]     != DBNull.Value ? Convert.ToInt32(r["lv"])     : 0,
                        Hp         = r.HasColumn("hp")     && r["hp"]     != DBNull.Value ? Convert.ToInt32(r["hp"])     : 0,
                        Attack     = r.HasColumn("attack") && r["attack"] != DBNull.Value ? Convert.ToInt32(r["attack"]) : 0,
                        Def        = r.HasColumn("def")    && r["def"]    != DBNull.Value ? Convert.ToInt32(r["def"])    : 0,
                        Quick      = r.HasColumn("quick")  && r["quick"]  != DBNull.Value ? Convert.ToInt32(r["quick"])  : 0,
                        Sum        = r.HasColumn("sum")    && r["sum"]    != DBNull.Value ? Convert.ToDouble(r["sum"])   : 0,
                        PlayerName = r.HasColumn("cdkey")  ? r["cdkey"]?.ToString()   ?? "" : "",
                    });
                }
                return (found, list);
            }
            catch { return (null, new()); }
        }

        /// <summary>取得 capturepet 中所有不重複的寵物種類 (id, name, type)，供篩選下拉使用</summary>
        public async Task<List<(int id, string name, string type)>> GetPetKindsAsync()
        {
            var list = new List<(int, string, string)>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT id, name, type, COUNT(*) AS cnt
                      FROM capturepet
                      GROUP BY id, name, type
                      ORDER BY cnt DESC", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["id"]  == DBNull.Value ? 0  : Convert.ToInt32(r["id"]),
                        r["name"]?.ToString() ?? "",
                        r["type"]?.ToString() ?? ""));
            }
            catch { }
            return list;
        }

        // 寵物 CRUD（capturepet）
        // ══════════════════════════════════════════════════════════
        public async Task<bool> UpdatePetAsync(PetInfo pet)
        {
            try
            {
                double newSum = pet.Hp * 0.5 + (pet.Attack + pet.Def + pet.Quick) * 0.5;
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"UPDATE capturepet
                      SET name=@name, lv=@lv, hp=@hp, attack=@atk, def=@def, quick=@spd, sum=@sum, `check`=@chk
                      WHERE unicode=@uid", conn);
                cmd.Parameters.AddWithValue("@name", pet.Name);
                cmd.Parameters.AddWithValue("@lv",   pet.Lv);
                cmd.Parameters.AddWithValue("@hp",   pet.Hp);
                cmd.Parameters.AddWithValue("@atk",  pet.Attack);
                cmd.Parameters.AddWithValue("@def",  pet.Def);
                cmd.Parameters.AddWithValue("@spd",  pet.Quick);
                cmd.Parameters.AddWithValue("@sum",  newSum);
                cmd.Parameters.AddWithValue("@chk",  pet.Check);
                cmd.Parameters.AddWithValue("@uid",  pet.Unicode);
                bool ok = await cmd.ExecuteNonQueryAsync() > 0;
                if (ok)
                    await GmLogger.Instance.LogAsync("寵物編輯", $"#{pet.Id} {pet.Name}",
                        $"Lv{pet.Lv} HP{pet.Hp} ATK{pet.Attack} DEF{pet.Def} SPD{pet.Quick}", true);
                return ok;
            }
            catch (Exception ex)
            {
                await GmLogger.Instance.LogAsync("寵物編輯[錯誤]", pet.Unicode, ex.Message, false);
                return false;
            }
        }

        public async Task<bool> DeletePetAsync(string unicode, string petName)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand("DELETE FROM capturepet WHERE unicode=@uid", conn);
                cmd.Parameters.AddWithValue("@uid", unicode);
                bool ok = await cmd.ExecuteNonQueryAsync() > 0;
                if (ok) await GmLogger.Instance.LogAsync("寵物刪除", petName, unicode, true);
                return ok;
            }
            catch (Exception ex)
            {
                await GmLogger.Instance.LogAsync("寵物刪除[錯誤]", petName, ex.Message, false);
                return false;
            }
        }

        public async Task<bool> TransferPetAsync(string unicode, string newAccount, string petName)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "UPDATE capturepet SET cdkey=@acc WHERE unicode=@uid", conn);
                cmd.Parameters.AddWithValue("@acc", newAccount);
                cmd.Parameters.AddWithValue("@uid", unicode);
                bool ok = await cmd.ExecuteNonQueryAsync() > 0;
                if (ok) await GmLogger.Instance.LogAsync("寵物轉移", petName, $"→ {newAccount}", true);
                return ok;
            }
            catch (Exception ex)
            {
                await GmLogger.Instance.LogAsync("寵物轉移[錯誤]", petName, ex.Message, false);
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 練寵活動排行榜（capturepet，含審核管理）
        // ══════════════════════════════════════════════════════════

        /// <summary>取得所有練寵活動種類（id, name, 參賽數, 最高分, 最後提交）</summary>
        public async Task<List<(int id, string name, int entryCount, double topScore, string lastEntry)>> GetCapturePetRankTypesAsync()
        {
            var list = new List<(int, string, int, double, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT id, name, COUNT(*) AS entryCount,
                           MAX(sum) AS topScore,
                           DATE_FORMAT(MAX(inserttime),'%Y-%m-%d %H:%i') AS lastEntry
                    FROM capturepet
                    GROUP BY id, name
                    ORDER BY MAX(inserttime) DESC", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        Convert.ToInt32(r["id"]),
                        r["name"]?.ToString() ?? "",
                        Convert.ToInt32(r["entryCount"]),
                        r["topScore"] == DBNull.Value ? 0.0 : Convert.ToDouble(r["topScore"]),
                        r["lastEntry"]?.ToString() ?? ""));
            }
            catch { }
            return list;
        }

        /// <summary>取得指定寵物排行榜（每人只取最高分那筆，相容 MySQL 5.7）</summary>
        public async Task<List<CaptureRankEntry>> GetCapturePetLeaderboardAsync(int petId, int limit = 100)
        {
            var list = new List<CaptureRankEntry>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT cp.unicode, cp.author, cp.cdkey, cp.name AS petName, cp.id AS petId,
                           cp.lv, cp.hp, cp.attack, cp.def, cp.quick, cp.sum,
                           cp.`check`, DATE_FORMAT(cp.inserttime,'%Y-%m-%d %H:%i') AS inserttime,
                           ec.entryCount,
                           IFNULL(c.Online,0) AS isOnline
                    FROM capturepet cp
                    INNER JOIN (
                        SELECT cdkey, MAX(sum) AS maxsum
                        FROM capturepet WHERE id=@pid
                        GROUP BY cdkey
                    ) m ON cp.cdkey=m.cdkey AND cp.sum=m.maxsum AND cp.id=@pid
                    INNER JOIN (
                        SELECT cdkey, COUNT(*) AS entryCount
                        FROM capturepet WHERE id=@pid
                        GROUP BY cdkey
                    ) ec ON cp.cdkey=ec.cdkey
                    LEFT JOIN csalogin c ON c.`Name`=cp.cdkey
                    ORDER BY cp.sum DESC
                    LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@pid", petId);
                cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 500));
                using var r = await cmd.ExecuteReaderAsync();
                int rank = 1;
                while (await r.ReadAsync())
                    list.Add(new CaptureRankEntry
                    {
                        Rank       = rank++,
                        Unicode    = r["unicode"]?.ToString()    ?? "",
                        Author     = r["author"]?.ToString()     ?? "",
                        Cdkey      = r["cdkey"]?.ToString()      ?? "",
                        PetName    = r["petName"]?.ToString()    ?? "",
                        PetId      = Convert.ToInt32(r["petId"]),
                        Lv         = r["lv"]     == DBNull.Value ? 0 : Convert.ToInt32(r["lv"]),
                        Hp         = r["hp"]     == DBNull.Value ? 0 : Convert.ToInt32(r["hp"]),
                        Attack     = r["attack"] == DBNull.Value ? 0 : Convert.ToInt32(r["attack"]),
                        Def        = r["def"]    == DBNull.Value ? 0 : Convert.ToInt32(r["def"]),
                        Quick      = r["quick"]  == DBNull.Value ? 0 : Convert.ToInt32(r["quick"]),
                        Sum        = r["sum"]    == DBNull.Value ? 0.0 : Convert.ToDouble(r["sum"]),
                        Check      = r["check"]  != DBNull.Value && Convert.ToBoolean(r["check"]),
                        InsertTime = r["inserttime"]?.ToString() ?? "",
                        EntryCount = Convert.ToInt32(r["entryCount"]),
                        IsOnline   = r["isOnline"] != DBNull.Value && Convert.ToInt32(r["isOnline"]) == 1,
                    });
            }
            catch { }
            return list;
        }

        /// <summary>查詢某玩家的所有練寵記錄（多號偵測用）</summary>
        public async Task<List<CaptureRankEntry>> GetCapturePetPlayerEntriesAsync(string account)
        {
            var list = new List<CaptureRankEntry>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT unicode, id AS petId, name AS petName, lv, hp, attack, def, quick, sum,
                           author, cdkey, `check`, DATE_FORMAT(inserttime,'%Y-%m-%d %H:%i') AS inserttime
                    FROM capturepet
                    WHERE cdkey=@a OR author=@a
                    ORDER BY sum DESC", conn);
                cmd.Parameters.AddWithValue("@a", account);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new CaptureRankEntry
                    {
                        Unicode    = r["unicode"]?.ToString()    ?? "",
                        PetId      = Convert.ToInt32(r["petId"]),
                        PetName    = r["petName"]?.ToString()    ?? "",
                        Lv         = r["lv"]     == DBNull.Value ? 0 : Convert.ToInt32(r["lv"]),
                        Hp         = r["hp"]     == DBNull.Value ? 0 : Convert.ToInt32(r["hp"]),
                        Attack     = r["attack"] == DBNull.Value ? 0 : Convert.ToInt32(r["attack"]),
                        Def        = r["def"]    == DBNull.Value ? 0 : Convert.ToInt32(r["def"]),
                        Quick      = r["quick"]  == DBNull.Value ? 0 : Convert.ToInt32(r["quick"]),
                        Sum        = r["sum"]    == DBNull.Value ? 0.0 : Convert.ToDouble(r["sum"]),
                        Author     = r["author"]?.ToString()     ?? "",
                        Cdkey      = r["cdkey"]?.ToString()      ?? "",
                        Check      = r["check"]  != DBNull.Value && Convert.ToBoolean(r["check"]),
                        InsertTime = r["inserttime"]?.ToString() ?? "",
                    });
            }
            catch { }
            return list;
        }

        /// <summary>切換練寵記錄的審核狀態</summary>
        public async Task<bool> SetCapturePetCheckAsync(string unicode, bool check)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "UPDATE capturepet SET `check`=@c WHERE unicode=@u", conn);
                cmd.Parameters.AddWithValue("@c", check ? 1 : 0);
                cmd.Parameters.AddWithValue("@u", unicode);
                bool ok = await cmd.ExecuteNonQueryAsync() > 0;
                if (ok) await GmLogger.Instance.LogAsync("練寵審核", unicode, check ? "通過" : "取消", true);
                return ok;
            }
            catch { return false; }
        }

        public async Task<(bool exists, string ownerAccount)> CheckAccountExistsAsync(string account)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "SELECT `Name` FROM csalogin WHERE `Name`=@n LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@n", account);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value)
                    return (true, r.ToString() ?? account);
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return (false, "");
        }

        // ══════════════════════════════════════════════════════════
        // 玩家活動歷程（PlayerHistoryForm 使用）
        // ══════════════════════════════════════════════════════════

        public async Task<List<(string time, string dir, string otherAcc, string otherName, string items, string pets, long gold)>>
            GetPlayerHistoryTradesAsync(string account, int limit = 150)
        {
            var list = new List<(string, string, string, string, string, string, long)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT mecdkey,mename,tocdkey,toname,
                             DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time,
                             item, pet, gold
                      FROM tradelog
                      WHERE mecdkey=@a OR tocdkey=@a
                      ORDER BY time DESC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@a",   account);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string from = r.GetString("mecdkey");
                    bool   sent = from == account;
                    string dir  = sent ? "→ 送出" : "← 收到";
                    string otherAcc  = sent ? r.GetString("tocdkey") : from;
                    string otherName = sent ? (r["toname"]?.ToString() ?? "") : (r["mename"]?.ToString() ?? "");
                    list.Add((
                        r["time"]?.ToString() ?? "",
                        dir, otherAcc, otherName,
                        r["item"]?.ToString() ?? "",
                        r["pet"]?.ToString()  ?? "",
                        r["gold"] == DBNull.Value ? 0 : Convert.ToInt64(r["gold"])
                    ));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/History/Trade] " + ex.Message); }
            return list;
        }

        public async Task<List<(string time, string role, string itemName, int num, int price, string otherAcc, string otherName)>>
            GetPlayerHistoryStreetAsync(string account, int limit = 150)
        {
            var list = new List<(string, string, string, int, int, string, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT sellcdkey, name, num, point, buycdkey, buyname,
                             FROM_UNIXTIME(time,'%Y-%m-%d %H:%i:%S') time
                      FROM streetlog
                      WHERE sellcdkey=@a OR buycdkey=@a
                      ORDER BY time DESC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@a",   account);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string seller = r.GetString("sellcdkey");
                    bool   isSell = seller == account;
                    string role   = isSell ? "賣出" : "買入";
                    string other  = isSell ? (r["buycdkey"]?.ToString() ?? "") : seller;
                    string otherN = isSell ? (r["buyname"]?.ToString()  ?? "") : "";
                    list.Add((
                        r["time"]?.ToString() ?? "",
                        role,
                        r["name"]?.ToString() ?? "",
                        r["num"]   == DBNull.Value ? 0 : Convert.ToInt32(r["num"]),
                        r["point"] == DBNull.Value ? 0 : Convert.ToInt32(r["point"]),
                        other, otherN
                    ));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/History/Street] " + ex.Message); }
            return list;
        }

        public async Task<List<(string time, string shopType, string itemName, int num, int cost)>>
            GetPlayerHistoryShopAsync(string account, int limit = 150)
        {
            var list = new List<(string, string, string, int, int)>();
            foreach (var tbl in new[] { ("fameshop", "聲望商城"), ("vipshop", "金幣商城") })
            {
                try
                {
                    using var conn = GetConnection(); await conn.OpenAsync();
                    using var cmd = new MySqlCommand(
                        $@"SELECT DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time,
                                  itemname, itemnum, oldpoint, newpoint
                           FROM `{tbl.Item1}` WHERE cdkey=@a
                           ORDER BY time DESC LIMIT @lim", conn);
                    cmd.Parameters.AddWithValue("@a",   account);
                    cmd.Parameters.AddWithValue("@lim", limit);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        int old_ = r["oldpoint"] == DBNull.Value ? 0 : Convert.ToInt32(r["oldpoint"]);
                        int new_ = r["newpoint"] == DBNull.Value ? 0 : Convert.ToInt32(r["newpoint"]);
                        list.Add((
                            r["time"]?.ToString()     ?? "",
                            tbl.Item2,
                            r["itemname"]?.ToString() ?? "",
                            r["itemnum"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemnum"]),
                            old_ - new_
                        ));
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB/History/Shop/{tbl.Item1}] " + ex.Message); }
            }
            list.Sort((a, b) => string.Compare(b.Item1, a.Item1, StringComparison.Ordinal));
            return list;
        }

        public async Task<List<(string time, int speedTime, int speedCnt)>>
            GetPlayerHistorySpeedAsync(string account, int limit = 100)
        {
            var list = new List<(string, int, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT speedtime, speedcnt,
                             DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
                      FROM speedlog WHERE cdkey=@a
                      ORDER BY time DESC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@a",   account);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["time"]?.ToString() ?? "",
                        r["speedtime"] == DBNull.Value ? 0 : Convert.ToInt32(r["speedtime"]),
                        r["speedcnt"]  == DBNull.Value ? 0 : Convert.ToInt32(r["speedcnt"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/History/Speed] " + ex.Message); }
            return list;
        }

        // ── 加速外掛統計（按帳號彙整）────────────────────────────────
        public async Task<List<SpeedHackEntry>> GetSpeedHackPlayersAsync(long minCnt = 1, int limit = 500)
        {
            var list = new List<SpeedHackEntry>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT s.cdkey,
                             IFNULL(c.OnlineName,'') charName,
                             IFNULL(c.Online,0) isOnline,
                             SUM(s.speedcnt)           totalCnt,
                             COUNT(*)                  records,
                             MAX(s.time)               lastTime,
                             ROUND(AVG(s.speedtime),1) avgSpeedTime,
                             MAX(s.speedtime)          maxSpeedTime,
                             (SELECT COUNT(*) FROM `lock` l WHERE l.`Name`=s.cdkey) isBanned
                      FROM speedlog s
                      LEFT JOIN csalogin c ON c.`Name`=s.cdkey
                      GROUP BY s.cdkey
                      HAVING totalCnt >= @min
                      ORDER BY totalCnt DESC
                      LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@min", minCnt);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new SpeedHackEntry
                    {
                        Account      = r["cdkey"]?.ToString() ?? "",
                        CharName     = r["charName"]?.ToString() ?? "",
                        IsOnline     = Convert.ToInt32(r["isOnline"]) == 1,
                        TotalCnt     = Convert.ToInt64(r["totalCnt"]),
                        Records      = Convert.ToInt32(r["records"]),
                        LastTime     = r["lastTime"] == DBNull.Value ? "" : ((DateTime)r["lastTime"]).ToString("yyyy/MM/dd HH:mm"),
                        AvgSpeedTime = r["avgSpeedTime"] == DBNull.Value ? 0 : Convert.ToDouble(r["avgSpeedTime"]),
                        MaxSpeedTime = r["maxSpeedTime"] == DBNull.Value ? 0 : Convert.ToInt32(r["maxSpeedTime"]),
                        IsBanned     = Convert.ToInt32(r["isBanned"]) > 0,
                    });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/SpeedHack] " + ex.Message); }
            return list;
        }

        public async Task<List<(string account, bool ok)>> BatchBanAsync(
            IEnumerable<string> accounts, int days = 0, double hours = 0)
        {
            var results = new List<(string, bool)>();
            foreach (var acc in accounts)
            {
                int endUnix = 0; // 0 = 永久
                if (hours > 0)   endUnix = (int)DateTimeOffset.Now.AddHours(hours).ToUnixTimeSeconds();
                else if (days > 0) endUnix = (int)DateTimeOffset.Now.AddDays(days).ToUnixTimeSeconds();
                bool ok = await BanPlayerAsync(acc, endUnix, "加速外掛批量封禁");
                results.Add((acc, ok));
            }
            return results;
        }

        public async Task<List<(string time, string name, long point)>>
            GetPlayerHistoryCostAsync(string account, int limit = 150)
        {
            var list = new List<(string, string, long)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT cdkey, name, point,
                             DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
                      FROM costdata WHERE cdkey=@a
                      ORDER BY time DESC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@a",   account);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["time"]?.ToString()  ?? "",
                        r["name"]?.ToString()  ?? "",
                        r["point"] == DBNull.Value ? 0 : Convert.ToInt64(r["point"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/History/Cost] " + ex.Message); }
            return list;
        }

        /// <summary>獎池紀錄 (poolitem)，是否為寶箱/骰子開出結果需對照遊戲確認</summary>
        public class PoolItemDto
        {
            public string Cdkey      { get; set; } = "";
            public string Uid        { get; set; } = "";
            public int    ItemId     { get; set; }
            public string ItemName   { get; set; } = "";
            public string TypeCode   { get; set; } = "";
            public int    Pile       { get; set; }
            public int    Atk        { get; set; }
            public int    Def        { get; set; }
            public int    Hp         { get; set; }
            public int    Luck       { get; set; }
            public bool   Locked     { get; set; }
            public string GetTime    { get; set; } = "";  // 取得時間（ITEM_UNIQUECODE 前半部）
            public string ExpireTime { get; set; } = "";  // 到期時間（ITEM_USETIME，0 = 永久）
        }

        private static PoolItemDto ReadPoolItemDto(MySqlDataReader r)
        {
            string uniqueCode = r["ITEM_UNIQUECODE"]?.ToString() ?? "";
            string getTime = "";
            if (!string.IsNullOrEmpty(uniqueCode))
            {
                var parts = uniqueCode.Split('i');
                if (parts.Length > 0 && long.TryParse(parts[0], out long ts) && ts > 0)
                    getTime = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            int useTime = r["ITEM_USETIME"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_USETIME"]);
            string expireTime = useTime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(useTime).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "";
            return new PoolItemDto
            {
                ItemId     = r["ITEM_ID"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_ID"]),
                ItemName   = r["ITEM_NAME"]?.ToString() ?? "",
                TypeCode   = r["ITEM_TYPECODE"]?.ToString() ?? "",
                Pile       = r["ITEM_USEPILENUMS"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_USEPILENUMS"]),
                Atk        = r["ITEM_MODIFYATTACK"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_MODIFYATTACK"]),
                Def        = r["ITEM_MODIFYDEFENCE"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_MODIFYDEFENCE"]),
                Hp         = r["ITEM_MODIFYHP"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_MODIFYHP"]),
                Luck       = r["ITEM_MODIFYLUCK"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_MODIFYLUCK"]),
                Locked     = r["ITEM_LOCKED"] != DBNull.Value && Convert.ToInt32(r["ITEM_LOCKED"]) == 1,
                GetTime    = getTime,
                ExpireTime = expireTime,
            };
        }

        public async Task<List<PoolItemDto>> GetPlayerStorageAsync(string account, int limit = 1000)
        {
            var list = new List<PoolItemDto>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT IFNULL(ITEM_ID,0) ITEM_ID,
                             IFNULL(ITEM_NAME,'') ITEM_NAME, IFNULL(ITEM_TYPECODE,'') ITEM_TYPECODE,
                             ITEM_USEPILENUMS, ITEM_MODIFYATTACK, ITEM_MODIFYDEFENCE,
                             ITEM_MODIFYHP, ITEM_MODIFYLUCK, IFNULL(ITEM_LOCKED,0) ITEM_LOCKED,
                             IFNULL(ITEM_UNIQUECODE,'') ITEM_UNIQUECODE, IFNULL(ITEM_USETIME,0) ITEM_USETIME
                      FROM poolitem WHERE cdkey=@a LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@a", account);
                cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 2000));
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(ReadPoolItemDto(r));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/Storage] " + ex.Message); }
            return list;
        }
        // ══════════════════════════════════════════════════════════
        // 累計消費達成獎勵（costdata）
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 里程碑定義（金幣）：3000 / 5000 / 10000 / 50000 / 100000
        /// check 欄位 = 已領取的里程碑數（0~5），與遊戲一致。
        /// </summary>
        public static readonly long[] CostMilestones = { 3_000, 5_000, 10_000, 50_000, 100_000 };

        /// <summary>
        /// 將任意輸入（主帳號名/角色名/csalogin.Name UID）解析為 csalogin.Name（12位UID）。
        /// costdata/paydata 的 cdkey 即為此值。
        /// </summary>
        private async Task<(string uid, string onlineName)> ResolveCsaloginUidAsync(MySqlConnection conn, string input)
        {
            try
            {
                using var cmd = new MySqlCommand(
                    @"SELECT c.`Name`, IFNULL(c.OnlineName,'') n
                      FROM csalogin c
                      LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                      WHERE c.`Name`=@inp OR c.OnlineName=@inp OR m.`Name`=@inp
                      ORDER BY c.Online DESC, c.LoginTime DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@inp", input);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return (r.GetString(0), r.GetString(1));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/ResolveCsalogin] " + ex.Message); }
            return (input, "");
        }

        private async Task<(string uid, string onlineName, string masterAccount)> ResolveCsaloginWithMasterAsync(MySqlConnection conn, string input)
        {
            try
            {
                using var cmd = new MySqlCommand(
                    @"SELECT c.`Name`, IFNULL(c.OnlineName,'') n, IFNULL(m.`Name`,'') master
                      FROM csalogin c
                      LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                      WHERE c.`Name`=@inp OR c.OnlineName=@inp OR m.`Name`=@inp
                      ORDER BY c.Online DESC, c.LoginTime DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@inp", input);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return (r.GetString(0), r.GetString(1), r.GetString(2));
            }
            catch { }
            return (input, "", "");
        }

        /// <summary>取得全服（或線上）所有有 costdata 記錄的玩家列表，用於批量操作</summary>
        public async Task<List<(string uid, string onlineName, string masterAccount, bool isOnline, long point, int check)>> GetAllCostDataListAsync(bool onlineOnly)
        {
            var list = new List<(string, string, string, bool, long, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                string where = onlineOnly ? "AND c.Online=1" : "";
                using var cmd = new MySqlCommand($@"
                    SELECT c.`Name` cdkey, IFNULL(c.OnlineName,'') charName,
                           IFNULL(m.`Name`,'') masterName,
                           (c.Online=1) isOnline,
                           IFNULL(d.point,0) point, IFNULL(d.`check`,0) ck
                    FROM csalogin c
                    INNER JOIN costdata d ON d.cdkey=c.`Name`
                    LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                    WHERE 1=1 {where}
                    ORDER BY d.point DESC LIMIT 2000", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["cdkey"]?.ToString()      ?? "",
                        r["charName"]?.ToString()   ?? "",
                        r["masterName"]?.ToString() ?? "",
                        Convert.ToInt32(r["isOnline"]) == 1,
                        r["point"] == DBNull.Value ? 0 : Convert.ToInt64(r["point"]),
                        r["ck"]    == DBNull.Value ? 0 : Convert.ToInt32(r["ck"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/GetAllCostDataList] " + ex.Message); }
            return list;
        }

        /// <summary>
        /// 取主帳號下所有角色的 costdata（用於 CostMilestoneForm 列出多角色）。
        /// 回傳空列表代表輸入不是主帳號名。
        /// </summary>
        public async Task<List<(string uid, string onlineName, bool isOnline, long point, int check)>> GetAllCharsCostDataAsync(string masterName)
        {
            var result = new List<(string, string, bool, long, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                int masterId = 0;
                using (var cmdM = new MySqlCommand(
                    "SELECT Id FROM csaloginmaster WHERE `Name`=@n LIMIT 1", conn))
                {
                    cmdM.Parameters.AddWithValue("@n", masterName);
                    var val = await cmdM.ExecuteScalarAsync();
                    if (val == null || val == DBNull.Value) return result;
                    masterId = Convert.ToInt32(val);
                }
                using var cmdC = new MySqlCommand(
                    @"SELECT c.`Name`, IFNULL(c.OnlineName,''), (c.Online=1)
                      FROM csalogin c WHERE c.MasterId=@mid
                      ORDER BY c.Online DESC, c.LoginTime DESC", conn);
                cmdC.Parameters.AddWithValue("@mid", masterId);
                var chars = new List<(string uid, string name, bool online)>();
                using (var rC = await cmdC.ExecuteReaderAsync())
                    while (await rC.ReadAsync())
                        chars.Add((rC.GetString(0), rC.GetString(1), rC.GetBoolean(2)));

                foreach (var (uid, name, online) in chars)
                {
                    long pt = 0; int ck = 0;
                    using var cmdCd = new MySqlCommand(
                        "SELECT point, IFNULL(`check`,0) FROM costdata WHERE cdkey=@acc LIMIT 1", conn);
                    cmdCd.Parameters.AddWithValue("@acc", uid);
                    using var rCd = await cmdCd.ExecuteReaderAsync();
                    if (await rCd.ReadAsync())
                    {
                        pt = rCd[0] == DBNull.Value ? 0 : Convert.ToInt64(rCd[0]);
                        ck = rCd[1] == DBNull.Value ? 0 : Convert.ToInt32(rCd[1]);
                    }
                    result.Add((uid, name, online, pt, ck));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/GetAllCharsCostData] " + ex.Message); }
            return result;
        }

        /// <summary>讀取玩家的消費達成進度（costdata），支援主帳號名/角色名/UID</summary>
        public async Task<(long point, int check, string uid, string onlineName, string masterAccount)> GetCostDataAsync(string account)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var (uid, onlineName, masterAccount) = await ResolveCsaloginWithMasterAsync(conn, account);
                using var cmd = new MySqlCommand(
                    "SELECT point, IFNULL(`check`,0) AS ck FROM costdata WHERE cdkey=@acc ORDER BY time DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@acc", uid);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return (r["point"] == DBNull.Value ? 0 : Convert.ToInt64(r["point"]),
                            r["ck"]    == DBNull.Value ? 0 : Convert.ToInt32(r["ck"]),
                            uid, onlineName, masterAccount);
                return (0, 0, uid, onlineName, masterAccount);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/GetCostData] " + ex.Message); }
            return (0, 0, account, "", "");
        }

        /// <summary>
        /// 調整消費達成進度（INSERT…ON DUPLICATE KEY UPDATE），類似 AdjustPayDataPointAsync。
        /// addPoint：增加的消費金幣數量。
        /// </summary>
        public async Task<bool> AdjustCostDataPointAsync(string account, string charName, long addPoint)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var (uid, resolvedName) = await ResolveCsaloginUidAsync(conn, account);
                using var cmd = new MySqlCommand(
                    @"INSERT INTO costdata (cdkey, name, point, `check`, time)
                      VALUES (@cdkey, @name, @pt, 0, NOW())
                      ON DUPLICATE KEY UPDATE
                          point = point + @pt,
                          time  = NOW()", conn);
                cmd.Parameters.AddWithValue("@cdkey", uid);
                cmd.Parameters.AddWithValue("@name",  string.IsNullOrEmpty(charName) ? resolvedName : charName);
                cmd.Parameters.AddWithValue("@pt",    addPoint);
                await cmd.ExecuteNonQueryAsync();
                await GmLogger.Instance.LogAsync("調整消費進度", uid, $"+{addPoint:N0} 金幣消費點數", true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/AdjustCostData] " + ex.Message);
                return false;
            }
        }

        /// <summary>重置消費達成進度（只清 check，point 保留）</summary>
        /// <summary>僅清除已領取狀態（check=0），消費進度 point 不變 → 玩家可立即重領</summary>
        public async Task<bool> ResetCostDataAsync(string account)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var (uid, _) = await ResolveCsaloginUidAsync(conn, account);
                using var cmd = new MySqlCommand(
                    "UPDATE costdata SET `check`=0, time=NOW() WHERE cdkey=@acc", conn);
                cmd.Parameters.AddWithValue("@acc", uid);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                    await GmLogger.Instance.LogAsync("重置領取狀態", uid, "costdata.check 歸零（已領狀態清除，point 保留，玩家可立即重領）", true);
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/ResetCostData] " + ex.Message);
                return false;
            }
        }

        /// <summary>完全重置（point=0 且 check=0）→ 玩家必須重新消費才能領取</summary>
        public async Task<bool> FullResetCostDataAsync(string account)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var (uid, _) = await ResolveCsaloginUidAsync(conn, account);
                using var cmd = new MySqlCommand(
                    "UPDATE costdata SET point=0, `check`=0, time=NOW() WHERE cdkey=@acc", conn);
                cmd.Parameters.AddWithValue("@acc", uid);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                    await GmLogger.Instance.LogAsync("完全重置消費", uid, "costdata.point=0, check=0（玩家須重新消費才能領取）", true);
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/FullResetCostData] " + ex.Message);
                return false;
            }
        }

        // ── Bitmask 說明 ──────────────────────────────────────────────
        // costdata.check 是位元遮罩：bit i = 第 i+1 個里程碑已領取
        //   bit 0 (1)  = 3,000  金幣里程碑已領
        //   bit 1 (2)  = 5,000  金幣里程碑已領
        //   bit 2 (4)  = 10,000 金幣里程碑已領
        //   bit 3 (8)  = 50,000 金幣里程碑已領
        //   bit 4 (16) = 100,000金幣里程碑已領
        //   check=31 (11111₂) = 全部五個里程碑都已領取
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 補發消費達成獎勵（同步遊戲模式）：
        /// 清除 check 中對應 bit（讓遊戲偵測到「達成但未領」並自動發放道具到背包）。
        /// </summary>
        public async Task<bool> ClaimCostMilestoneAsync(string account, int milestoneIdx)
        {
            if (milestoneIdx < 0 || milestoneIdx >= CostMilestones.Length) return false;
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var (uid, _) = await ResolveCsaloginUidAsync(conn, account);
                int bit = 1 << milestoneIdx;
                using var cmd = new MySqlCommand(
                    "UPDATE costdata SET `check`=(`check` & ~@bit), time=NOW() WHERE cdkey=@acc", conn);
                cmd.Parameters.AddWithValue("@bit", bit);
                cmd.Parameters.AddWithValue("@acc", uid);
                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                    await GmLogger.Instance.LogAsync("補發消費獎勵(同步遊戲)", uid,
                        $"里程碑 {CostMilestones[milestoneIdx]:N0} 金幣（清除 bit{milestoneIdx}，等待遊戲自動發放）", true);
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/ClaimCostMilestone] " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 補發消費達成獎勵（郵件模式）：直接寄出道具，並設定對應 bit 為已領。
        /// </summary>
        public async Task<bool> ClaimCostMilestoneByMailAsync(
            string account, string playerName, int milestoneIdx, int itemId, string itemName, int quantity)
        {
            if (milestoneIdx < 0 || milestoneIdx >= CostMilestones.Length) return false;
            try
            {
                using var connR = GetConnection(); await connR.OpenAsync();
                var (uid, resolvedName) = await ResolveCsaloginUidAsync(connR, account);
                string name = string.IsNullOrEmpty(playerName) ? resolvedName : playerName;

                bool mailOk = await GiveItemDirectAsync(uid, name, itemId, itemName, quantity);

                int bit = 1 << milestoneIdx;
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "UPDATE costdata SET `check`=(`check` | @bit), time=NOW() WHERE cdkey=@acc", conn);
                cmd.Parameters.AddWithValue("@bit", bit);
                cmd.Parameters.AddWithValue("@acc", uid);
                await cmd.ExecuteNonQueryAsync();

                if (mailOk)
                    await GmLogger.Instance.LogAsync("補發消費獎勵(郵件)", uid,
                        $"里程碑 {CostMilestones[milestoneIdx]:N0} 金幣 → 道具 ID:{itemId} x{quantity:N0}（設 bit{milestoneIdx}）", true);
                return mailOk;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/ClaimCostMilestoneMail] " + ex.Message);
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // 攤位 & 市場查詢（StreetShopForm 使用）
        // ══════════════════════════════════════════════════════════

        public async Task<List<(string cdkey, string charName, int itemCount)>> GetAllVendorsAsync()
        {
            var list = new List<(string, string, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT si.cdkey, IFNULL(c.OnlineName,'') charName, COUNT(*) AS cnt
                      FROM streetitem si
                      LEFT JOIN csalogin c ON c.Name = si.cdkey
                      GROUP BY si.cdkey, c.OnlineName
                      ORDER BY cnt DESC", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r.GetString("cdkey"),
                        r["charName"]?.ToString() ?? "",
                        r["cnt"] == DBNull.Value ? 0 : Convert.ToInt32(r["cnt"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/Vendors] " + ex.Message); }
            return list;
        }

        public async Task<List<(int itemId, string itemName, int num, int price)>>
            GetVendorItemsAsync(string cdkey)
        {
            var list = new List<(int, string, int, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT ITEM_ID, ITEM_NAME, ITEM_USEPILENUMS, price
                      FROM streetitem WHERE cdkey=@a ORDER BY price ASC", conn);
                cmd.Parameters.AddWithValue("@a", cdkey);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["ITEM_ID"]           == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_ID"]),
                        r["ITEM_NAME"]?.ToString() ?? "",
                        r["ITEM_USEPILENUMS"]  == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_USEPILENUMS"]),
                        r["price"]             == DBNull.Value ? 0 : Convert.ToInt32(r["price"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/VendorItems] " + ex.Message); }
            return list;
        }

        public async Task<List<(string time, string itemName, int num, int price, string buyCdkey, string buyName)>>
            GetVendorSalesAsync(string cdkey, int limit = 200)
        {
            var list = new List<(string, string, int, int, string, string)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT name, num, point, buycdkey, buyname,
                             FROM_UNIXTIME(time,'%Y-%m-%d %H:%i:%S') time
                      FROM streetlog WHERE sellcdkey=@a
                      ORDER BY time DESC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@a",   cdkey);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["time"]?.ToString()     ?? "",
                        r["name"]?.ToString()     ?? "",
                        r["num"]     == DBNull.Value ? 0 : Convert.ToInt32(r["num"]),
                        r["point"]   == DBNull.Value ? 0 : Convert.ToInt32(r["point"]),
                        r["buycdkey"]?.ToString() ?? "",
                        r["buyname"]?.ToString()  ?? ""
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/VendorSales] " + ex.Message); }
            return list;
        }

        public async Task<List<(string cdkey, string charName, string itemName, int num, int price)>>
            GetListingsByItemAsync(string keyword, int limit = 200)
        {
            var list = new List<(string, string, string, int, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT si.cdkey, IFNULL(c.OnlineName,'') charName,
                             si.ITEM_NAME, si.ITEM_USEPILENUMS, si.price
                      FROM streetitem si
                      LEFT JOIN csalogin c ON c.Name = si.cdkey
                      WHERE si.ITEM_NAME LIKE @kw
                      ORDER BY si.price ASC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@kw",  $"%{keyword}%");
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["cdkey"]?.ToString()    ?? "",
                        r["charName"]?.ToString() ?? "",
                        r["ITEM_NAME"]?.ToString() ?? "",
                        r["ITEM_USEPILENUMS"] == DBNull.Value ? 0 : Convert.ToInt32(r["ITEM_USEPILENUMS"]),
                        r["price"]            == DBNull.Value ? 0 : Convert.ToInt32(r["price"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/ListingsByItem] " + ex.Message); }
            return list;
        }

        public async Task<List<(string time, string sellCdkey, string sellerName, string buyCdkey, string buyName, string itemName, int num, int price)>>
            GetStreetBuyersByItemAsync(string keyword, int limit = 300)
        {
            var list = new List<(string, string, string, string, string, string, int, int)>();
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT sl.sellcdkey, IFNULL(cs.OnlineName,'') sellerName,
                             sl.buycdkey, sl.buyname, sl.name, sl.num, sl.point,
                             FROM_UNIXTIME(sl.time,'%Y-%m-%d %H:%i:%S') time
                      FROM streetlog sl
                      LEFT JOIN csalogin cs ON cs.Name = sl.sellcdkey
                      WHERE sl.name LIKE @kw
                      ORDER BY sl.time DESC LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@kw",  $"%{keyword}%");
                cmd.Parameters.AddWithValue("@lim", limit);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add((
                        r["time"]?.ToString()       ?? "",
                        r["sellcdkey"]?.ToString()  ?? "",
                        r["sellerName"]?.ToString() ?? "",
                        r["buycdkey"]?.ToString()   ?? "",
                        r["buyname"]?.ToString()    ?? "",
                        r["name"]?.ToString()       ?? "",
                        r["num"]   == DBNull.Value  ? 0 : Convert.ToInt32(r["num"]),
                        r["point"] == DBNull.Value  ? 0 : Convert.ToInt32(r["point"])
                    ));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/StreetBuyersByItem] " + ex.Message); }
            return list;
        }

        public async Task<List<(string time, string shopType, string cdkey, string charName, string itemName, int num, int cost)>>
            GetShopBuyersByItemAsync(string keyword, int limit = 200)
        {
            var list = new List<(string, string, string, string, string, int, int)>();
            foreach (var tbl in new[] { ("fameshop", "聲望商城"), ("vipshop", "金幣商城") })
            {
                try
                {
                    using var conn = GetConnection(); await conn.OpenAsync();
                    using var cmd = new MySqlCommand(
                        $@"SELECT DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time,
                                  cdkey, name, itemname, itemnum, oldpoint, newpoint
                           FROM `{tbl.Item1}` WHERE itemname LIKE @kw
                           ORDER BY time DESC LIMIT @lim", conn);
                    cmd.Parameters.AddWithValue("@kw",  $"%{keyword}%");
                    cmd.Parameters.AddWithValue("@lim", limit);
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        int old_ = r["oldpoint"] == DBNull.Value ? 0 : Convert.ToInt32(r["oldpoint"]);
                        int new_ = r["newpoint"] == DBNull.Value ? 0 : Convert.ToInt32(r["newpoint"]);
                        list.Add((
                            r["time"]?.ToString()     ?? "",
                            tbl.Item2,
                            r["cdkey"]?.ToString()    ?? "",
                            r["name"]?.ToString()     ?? "",
                            r["itemname"]?.ToString() ?? "",
                            r["itemnum"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemnum"]),
                            old_ - new_
                        ));
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[DB/ShopBuyersByItem/{tbl.Item1}] " + ex.Message); }
            }
            list.Sort((a, b) => string.Compare(b.Item1, a.Item1, StringComparison.Ordinal));
            return list;
        }

        /// <summary>根據帳號或角色名稱找出 cdkey；找不到則原樣返回輸入值。</summary>
        public async Task<string> ResolveAccountAsync(string nameOrOnlineName)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "SELECT `Name` FROM csalogin WHERE `Name`=@q OR OnlineName=@q LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@q", nameOrOnlineName);
                var r = await cmd.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value) return r.ToString() ?? nameOrOnlineName;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/ResolveAccount] " + ex.Message); }
            return nameOrOnlineName;
        }

        public async Task<(int totalTrades, int uniquePairs, int suspiciousPairs, int sameIpPairs)> GetTradeAuditSummaryAsync()
        {
            int total = 0, pairs = 0, suspicious = 0, sameIp = 0;
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var r1 = await new MySqlCommand("SELECT COUNT(*) FROM tradelog", conn).ExecuteScalarAsync();
                total = r1 == null || r1 == DBNull.Value ? 0 : Convert.ToInt32(r1);
                var r2 = await new MySqlCommand("SELECT COUNT(DISTINCT CONCAT(mecdkey,'-',tocdkey)) FROM tradelog", conn).ExecuteScalarAsync();
                pairs = r2 == null || r2 == DBNull.Value ? 0 : Convert.ToInt32(r2);
                var r3 = await new MySqlCommand("SELECT COUNT(*) FROM (SELECT mecdkey,tocdkey,COUNT(*) c FROM tradelog GROUP BY mecdkey,tocdkey HAVING c>=10) x", conn).ExecuteScalarAsync();
                suspicious = r3 == null || r3 == DBNull.Value ? 0 : Convert.ToInt32(r3);
            }
            catch (Exception dbEx) { System.Diagnostics.Debug.WriteLine("[DB] " + dbEx.Message); }
            return (total, pairs, suspicious, sameIp);
        }

        // ══════════════════════════════════════════════════════════
        // 伺服器狀態：最新註冊 / 分流在線 / 主帳號統計
        // ══════════════════════════════════════════════════════════

        /// <summary>最新註冊帳號（依 created_at 或 id 排序）</summary>
        public async Task<List<RecentRegAccount>> GetRecentRegistrationsAsync(int limit = 30)
        {
            var list = new List<RecentRegAccount>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                // 嘗試用 created_at 排序；若欄位不存在則 fallback 到 RegTime
                bool hasCreatedAt = false;
                try
                {
                    using var chk = new MySqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
                        "WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='csalogin' AND COLUMN_NAME='created_at'", conn);
                    var r = await chk.ExecuteScalarAsync();
                    hasCreatedAt = r != null && Convert.ToInt32(r) > 0;
                }
                catch { }

                string orderBy = hasCreatedAt ? "c.created_at DESC" : "c.RegTime DESC";
                using var cmd = new MySqlCommand(
                    $@"SELECT c.Name, IFNULL(c.OnlineName,'') AS CharName,
                              IFNULL(m.Name,'') AS MasterName,
                              IFNULL(c.RegTime,'') AS RegTime,
                              IFNULL(c.RegIP,'')   AS RegIP,
                              IFNULL(c.ServerName,'') AS ServerName,
                              c.Online
                       FROM csalogin c
                       LEFT JOIN csaloginmaster m ON m.Id = c.MasterId
                       ORDER BY {orderBy}
                       LIMIT @lim", conn);
                cmd.Parameters.AddWithValue("@lim", limit);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(new RecentRegAccount
                    {
                        Account    = reader["Name"]?.ToString()       ?? "",
                        CharName   = reader["CharName"]?.ToString()   ?? "",
                        MasterName = reader["MasterName"]?.ToString() ?? "",
                        RegTime    = reader["RegTime"]?.ToString()    ?? "",
                        RegIP      = reader["RegIP"]?.ToString()      ?? "",
                        ServerName = reader["ServerName"]?.ToString() ?? "",
                        IsOnline   = reader["Online"] != DBNull.Value && Convert.ToInt32(reader["Online"]) == 1
                    });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/RecentReg] " + ex.Message); }
            return list;
        }

        /// <summary>各分流在線人數（+ 各分流總人數）</summary>
        public async Task<List<ChannelOnlineEntry>> GetChannelOnlineCountAsync()
        {
            var list = new List<ChannelOnlineEntry>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT ServerId,
                             IFNULL(ServerName,'') AS ServerName,
                             SUM(CASE WHEN Online=1 OR LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR) THEN 1 ELSE 0 END) AS OnlineCount,
                             COUNT(*) AS TotalCount
                      FROM csalogin
                      GROUP BY ServerId, ServerName
                      ORDER BY ServerId", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new ChannelOnlineEntry
                    {
                        ServerId    = r["ServerId"]    == DBNull.Value ? 0 : Convert.ToInt32(r["ServerId"]),
                        ServerName  = r["ServerName"]?.ToString() ?? "",
                        OnlineCount = r["OnlineCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["OnlineCount"]),
                        TotalCount  = r["TotalCount"]  == DBNull.Value ? 0 : Convert.ToInt32(r["TotalCount"])
                    });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/ChannelOnline] " + ex.Message); }
            return list;
        }

        /// <summary>依登入 IP 彙總在線人數（csalogin.IP，僅非空 IP）</summary>
        public async Task<List<OnlineIpEntry>> GetOnlineByLoginIpAsync(int topN = 40)
        {
            var list = new List<OnlineIpEntry>();
            topN = Math.Clamp(topN, 1, 200);
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    $@"SELECT IFNULL(IP,'') AS ip,
                              SUM(IF(Online=1,1,0)) AS onlineCount,
                              COUNT(*) AS totalCount
                       FROM csalogin
                       WHERE IP IS NOT NULL AND TRIM(IP) <> ''
                       GROUP BY IP
                       ORDER BY onlineCount DESC, totalCount DESC
                       LIMIT {topN}", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    list.Add(new OnlineIpEntry
                    {
                        Ip          = r["ip"]?.ToString() ?? "",
                        OnlineCount = r["onlineCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["onlineCount"]),
                        TotalCount  = r["totalCount"] == DBNull.Value ? 0 : Convert.ToInt32(r["totalCount"])
                    });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/OnlineByIp] " + ex.Message); }
            return list;
        }

        /// <summary>全服在線總人數與登入 IP 維度彙總（供 IP 區塊標題列）</summary>
        public async Task<OnlineLoginIpSummary> GetOnlineLoginIpSummaryAsync()
        {
            var s = new OnlineLoginIpSummary();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT
                        (SELECT COUNT(*) FROM csalogin WHERE Online=1 OR LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR)) AS totalOnline,
                        (SELECT COUNT(DISTINCT IP) FROM csalogin WHERE (Online=1 OR LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR)) AND IP IS NOT NULL AND TRIM(IP) <> '') AS ipWithOnline,
                        (SELECT COUNT(DISTINCT IP) FROM csalogin WHERE IP IS NOT NULL AND TRIM(IP) <> '') AS ipAll,
                        (SELECT COUNT(*) FROM csalogin WHERE (Online=1 OR LoginTime > DATE_SUB(NOW(), INTERVAL 6 HOUR)) AND (IP IS NULL OR TRIM(IFNULL(IP,'')) = '')) AS onlineNoIp", conn);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    s.TotalOnline           = r["totalOnline"] == DBNull.Value ? 0 : Convert.ToInt32(r["totalOnline"]);
                    s.DistinctIpWithOnline  = r["ipWithOnline"] == DBNull.Value ? 0 : Convert.ToInt32(r["ipWithOnline"]);
                    s.DistinctIpAll         = r["ipAll"] == DBNull.Value ? 0 : Convert.ToInt32(r["ipAll"]);
                    s.OnlineWithoutLoginIp  = r["onlineNoIp"] == DBNull.Value ? 0 : Convert.ToInt32(r["onlineNoIp"]);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/OnlineIpSummary] " + ex.Message); }
            return s;
        }

        /// <summary>主帳號在線 / 離線統計（csaloginmaster）</summary>
        public async Task<MasterAccountStats> GetMasterAccountStatsAsync()
        {
            var stats = new MasterAccountStats();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT COUNT(DISTINCT m.Id) AS TotalMasters,
                             COUNT(DISTINCT CASE WHEN c.Online=1 THEN m.Id END) AS OnlineMasters
                      FROM csaloginmaster m
                      LEFT JOIN csalogin c ON c.MasterId = m.Id", conn);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    stats.TotalMasters  = r["TotalMasters"]  == DBNull.Value ? 0 : Convert.ToInt32(r["TotalMasters"]);
                    stats.OnlineMasters = r["OnlineMasters"] == DBNull.Value ? 0 : Convert.ToInt32(r["OnlineMasters"]);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/MasterStats] " + ex.Message); }
            return stats;
        }

        // ══════════════════════════════════════════════════════════
        // 家族查詢與管理
        // ══════════════════════════════════════════════════════════

        /// <summary>取得家族列表（含成員數、家族商店總貢獻）</summary>
        public async Task<List<FamilyInfo>> GetFamilyListAsync()
        {
            var list = new List<FamilyInfo>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT g.jiazuid, g.jiazu,
                           COUNT(*) AS memberCount,
                           MAX(g.addtime) AS lastActive,
                           IFNULL(sc.shopContrib, 0) AS shopContrib
                    FROM gm_family_members g
                    LEFT JOIN (
                        SELECT mem.jiazuid,
                               SUM(fs.oldpoint - fs.newpoint) AS shopContrib
                        FROM fameshop fs
                        INNER JOIN (SELECT cdkey, jiazuid FROM gm_family_members) mem
                            ON fs.cdkey = mem.cdkey
                        GROUP BY mem.jiazuid
                    ) sc ON sc.jiazuid = g.jiazuid
                    GROUP BY g.jiazuid, g.jiazu
                    ORDER BY memberCount DESC, shopContrib DESC", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(new FamilyInfo
                    {
                        FamilyId     = Convert.ToInt32(r["jiazuid"]),
                        FamilyName   = r["jiazu"]?.ToString() ?? "",
                        MemberCount  = Convert.ToInt32(r["memberCount"]),
                        LastActive   = r["lastActive"] == DBNull.Value ? "" : Convert.ToDateTime(r["lastActive"]).ToString("yyyy-MM-dd HH:mm"),
                        ShopContrib  = Convert.ToInt64(r["shopContrib"])
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/FamilyList] " + ex.Message); throw; }
            return list;
        }

        /// <summary>取得指定家族的成員列表</summary>
        public async Task<List<FamilyMember>> GetFamilyMembersAsync(int familyId)
        {
            var list = new List<FamilyMember>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT g.cdkey, g.uname, g.addtime, g.role,
                           IFNULL(c.OnlineName,'') onlineName,
                           IFNULL(c.PayTotal,0) payTotal,
                           IFNULL(c.VipPoint,0) gold,
                           (c.Online = 1) isOnline,
                           IFNULL((SELECT SUM(fs.oldpoint - fs.newpoint) FROM fameshop fs WHERE fs.cdkey = g.cdkey), 0) shopContrib
                    FROM gm_family_members g
                    LEFT JOIN csalogin c ON c.Name = g.cdkey
                    WHERE g.jiazuid = @fid
                    ORDER BY g.role DESC, shopContrib DESC, g.uname", conn);
                cmd.Parameters.AddWithValue("@fid", familyId);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    int role = r.HasColumn("role") ? Convert.ToInt32(r["role"]) : 0;
                    list.Add(new FamilyMember
                    {
                        Cdkey       = r["cdkey"]?.ToString() ?? "",
                        CharName    = r["uname"]?.ToString() ?? "",
                        OnlineName  = r["onlineName"]?.ToString() ?? "",
                        JoinTime    = r["addtime"] == DBNull.Value ? "" : Convert.ToDateTime(r["addtime"]).ToString("yyyy-MM-dd HH:mm"),
                        PayTotal    = Convert.ToInt32(r["payTotal"]),
                        Gold        = Convert.ToInt64(r["gold"]),
                        IsOnline    = Convert.ToBoolean(r["isOnline"]),
                        ShopContrib = Convert.ToInt64(r["shopContrib"]),
                        IsLeader    = role == 1,
                        Role        = role
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/FamilyMembers] " + ex.Message); throw; }
            return list;
        }

        /// <summary>設定族長：將 familyId 家族的 cdkey 設為族長（role=1），其他人全設 role=0）</summary>
        public async Task<(bool ok, string msg)> SetFamilyLeaderAsync(int familyId, string cdkey)
            => await SetFamilyRoleAsync(familyId, cdkey, 1);

        /// <summary>設定成員職位：role 0=成員, 1=族長, 2=長老。族長唯一（設新族長會取消舊族長）</summary>
        public async Task<(bool ok, string msg)> SetFamilyRoleAsync(int familyId, string cdkey, int role)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();
                try
                {
                    // 若設為族長，先清除所有人的族長身份
                    if (role == 1)
                    {
                        using var c0 = new MySqlCommand(
                            "UPDATE gm_family_members SET role=0 WHERE jiazuid=@fid AND role=1", conn, tx);
                        c0.Parameters.AddWithValue("@fid", familyId);
                        await c0.ExecuteNonQueryAsync();
                    }
                    using var c1 = new MySqlCommand(
                        "UPDATE gm_family_members SET role=@role WHERE jiazuid=@fid AND cdkey=@ck", conn, tx);
                    c1.Parameters.AddWithValue("@role", role);
                    c1.Parameters.AddWithValue("@fid", familyId);
                    c1.Parameters.AddWithValue("@ck", cdkey);
                    int n = await c1.ExecuteNonQueryAsync();
                    if (n == 0) { await tx.RollbackAsync(); return (false, "未找到成員或該成員不屬於此家族"); }

                    await tx.CommitAsync();
                    string roleLabel = role == 1 ? "族長" : role == 2 ? "長老" : "一般成員";
                    return (true, $"已將 {cdkey} 設為{roleLabel}");
                }
                catch { await tx.RollbackAsync(); throw; }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>踢出成員（從 gm_family_members 中刪除，並重置 csalogin.GroupId）</summary>
        public async Task<(bool ok, string msg)> KickFamilyMemberAsync(int familyId, string cdkey)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();
                try
                {
                    int n = 0;
                    using (var c1 = new MySqlCommand(
                        "DELETE FROM gm_family_members WHERE jiazuid = @fid AND cdkey = @ck", conn, tx))
                    {
                        c1.Parameters.AddWithValue("@fid", familyId);
                        c1.Parameters.AddWithValue("@ck", cdkey);
                        n = await c1.ExecuteNonQueryAsync();
                    }
                    // 清除 csalogin GroupId
                    using (var c2 = new MySqlCommand(
                        "UPDATE csalogin SET GroupId=0, GroupName='' WHERE Name=@ck", conn, tx))
                    {
                        c2.Parameters.AddWithValue("@ck", cdkey);
                        await c2.ExecuteNonQueryAsync();
                    }
                    await tx.CommitAsync();
                    return n > 0 ? (true, $"已將 {cdkey} 從家族移除") : (false, "未找到該成員");
                }
                catch { await tx.RollbackAsync(); throw; }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>解散家族（刪除 gm_family_members 所有記錄，清除 csalogin.GroupId）</summary>
        public async Task<(bool ok, string msg)> DissolveFamilyAsync(int familyId)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();
                try
                {
                    // 取得所有成員 cdkey
                    var cdkeys = new List<string>();
                    using (var cq = new MySqlCommand("SELECT cdkey FROM gm_family_members WHERE jiazuid=@fid", conn, tx))
                    {
                        cq.Parameters.AddWithValue("@fid", familyId);
                        using var rq = await cq.ExecuteReaderAsync();
                        while (await rq.ReadAsync()) cdkeys.Add(rq.GetString(0));
                    }
                    int n = 0;
                    using (var c2 = new MySqlCommand("DELETE FROM gm_family_members WHERE jiazuid=@fid", conn, tx))
                    {
                        c2.Parameters.AddWithValue("@fid", familyId);
                        n = await c2.ExecuteNonQueryAsync();
                    }
                    // 清除所有成員的 csalogin GroupId
                    foreach (var ck in cdkeys)
                    {
                        using var c3 = new MySqlCommand("UPDATE csalogin SET GroupId=0, GroupName='' WHERE Name=@ck", conn, tx);
                        c3.Parameters.AddWithValue("@ck", ck);
                        await c3.ExecuteNonQueryAsync();
                    }
                    await tx.CommitAsync();
                    return (true, $"家族已解散，共刪除 {n} 位成員");
                }
                catch { await tx.RollbackAsync(); throw; }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>將成員轉移到另一個家族</summary>
        public async Task<(bool ok, string msg)> TransferMemberAsync(string cdkey, int targetFamilyId, string targetFamilyName)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();
                try
                {
                    // 更新 GM 管理表
                    using (var c1 = new MySqlCommand(
                        "UPDATE gm_family_members SET jiazuid=@tid, jiazu=@tname WHERE cdkey=@ck", conn, tx))
                    {
                        c1.Parameters.AddWithValue("@tid", targetFamilyId);
                        c1.Parameters.AddWithValue("@tname", targetFamilyName);
                        c1.Parameters.AddWithValue("@ck", cdkey);
                        int n = await c1.ExecuteNonQueryAsync();
                        if (n == 0)
                        {
                            await tx.RollbackAsync();
                            return (false, $"在 GM 名冊中找不到帳號「{cdkey}」，請先確認該成員已在家族名冊中");
                        }
                    }
                    // 同步更新 csalogin.GroupId（遊戲讀取的家族欄位）
                    using (var c2 = new MySqlCommand(
                        "UPDATE csalogin SET GroupId=@tid, GroupName=@tname WHERE Name=@ck", conn, tx))
                    {
                        c2.Parameters.AddWithValue("@tid", targetFamilyId);
                        c2.Parameters.AddWithValue("@tname", targetFamilyName);
                        c2.Parameters.AddWithValue("@ck", cdkey);
                        await c2.ExecuteNonQueryAsync();
                    }
                    // 同步更新 zuzhanlog 最新記錄
                    using (var c3 = new MySqlCommand(@"
                        UPDATE zuzhanlog SET jiazuid=@tid, jiazu=@tname
                        WHERE cdkey=@ck
                          AND id=(SELECT mid FROM (SELECT MAX(id) mid FROM zuzhanlog WHERE cdkey=@ck) t)", conn, tx))
                    {
                        c3.Parameters.AddWithValue("@tid", targetFamilyId);
                        c3.Parameters.AddWithValue("@tname", targetFamilyName);
                        c3.Parameters.AddWithValue("@ck", cdkey);
                        await c3.ExecuteNonQueryAsync();
                    }
                    await tx.CommitAsync();
                    return (true, $"已將 {cdkey} 轉移至家族「{targetFamilyName}」");
                }
                catch { await tx.RollbackAsync(); throw; }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>手動新增成員到家族（gm_family_members）</summary>
        public async Task<(bool ok, string msg)> AddFamilyMemberAsync(int familyId, string familyName, string cdkey, string charName, int role = 0)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                // 從 csalogin 取角色名稱（若未提供）
                using var chk = new MySqlCommand("SELECT IFNULL(OnlineName,'') FROM csalogin WHERE Name=@ck LIMIT 1", conn);
                chk.Parameters.AddWithValue("@ck", cdkey);
                var onlineName = await chk.ExecuteScalarAsync();
                if (onlineName == null) return (false, $"帳號「{cdkey}」不存在");
                if (string.IsNullOrWhiteSpace(charName))
                    charName = onlineName.ToString() ?? cdkey;

                using var ins = new MySqlCommand(@"
                    INSERT INTO gm_family_members (jiazuid, jiazu, cdkey, uname, role)
                    VALUES (@fid, @fname, @ck, @uname, @role)
                    ON DUPLICATE KEY UPDATE jiazu=@fname, uname=@uname, role=@role, addtime=NOW()", conn);
                ins.Parameters.AddWithValue("@fid",   familyId);
                ins.Parameters.AddWithValue("@fname", familyName);
                ins.Parameters.AddWithValue("@ck",    cdkey);
                ins.Parameters.AddWithValue("@uname", charName);
                ins.Parameters.AddWithValue("@role",  role);
                await ins.ExecuteNonQueryAsync();
                // 同步更新 csalogin GroupId
                using var upd = new MySqlCommand("UPDATE csalogin SET GroupId=@fid, GroupName=@fname WHERE Name=@ck", conn);
                upd.Parameters.AddWithValue("@fid",   familyId);
                upd.Parameters.AddWithValue("@fname", familyName);
                upd.Parameters.AddWithValue("@ck",    cdkey);
                await upd.ExecuteNonQueryAsync();
                return (true, $"已將 {cdkey}（{charName}）加入家族「{familyName}」");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>全服掃描共用 IP 的帳號群組</summary>
        public async Task<List<IpGroupEntry>> GetIpGroupsAsync(int minGroup = 2)
        {
            var result = new List<IpGroupEntry>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                // 找出每個 IP 有 >= minGroup 個帳號的群組（登入IP + 註冊IP 合併）
                // 先設 GROUP_CONCAT 長度上限，避免帳號清單被截斷（MySQL 預設 1024）
                using (var setCmd = new MySqlCommand("SET SESSION group_concat_max_len = 1000000", conn))
                    await setCmd.ExecuteNonQueryAsync();

                var sql = $@"
                    SELECT ip, GROUP_CONCAT(account ORDER BY isOnline DESC, account SEPARATOR '|||') accs,
                           SUM(isOnline) onlineCnt, COUNT(DISTINCT account) total
                    FROM (
                        SELECT `Name` account, IFNULL(IP,'') ip, IF(Online=1,1,0) isOnline
                        FROM csalogin WHERE IP IS NOT NULL AND IP != ''
                        UNION ALL
                        SELECT `Name` account, IFNULL(RegIP,'') ip, IF(Online=1,1,0) isOnline
                        FROM csalogin WHERE RegIP IS NOT NULL AND RegIP != '' AND (IP IS NULL OR IP != RegIP)
                    ) t
                    GROUP BY ip
                    HAVING COUNT(DISTINCT account) >= {minGroup}
                    ORDER BY SUM(isOnline) DESC, COUNT(DISTINCT account) DESC
                    LIMIT 500";

                var ipGroups = new List<(string ip, string[] accs, int online, int total)>();
                using (var cmd = new MySqlCommand(sql, conn))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        ipGroups.Add((
                            r.GetString("ip"),
                            r.GetString("accs").Split("|||"),
                            Convert.ToInt32(r["onlineCnt"]),
                            Convert.ToInt32(r["total"])
                        ));

                if (ipGroups.Count == 0) return result;

                // 批次查帳號詳細資訊
                var allAccs = ipGroups.SelectMany(g => g.accs).Distinct().ToList();
                var details = new Dictionary<string, IpGroupMember>();

                for (int i = 0; i < allAccs.Count; i += 200)
                {
                    var batch  = allAccs.Skip(i).Take(200).ToList();
                    var inList = string.Join(",", batch.Select(a => $"'{a.Replace("'", "\\'")}'"));
                    var dSql   = $@"SELECT c.`Name` acc, IFNULL(c.OnlineName,'') charName,
                                           IFNULL(m.Name,'') masterName,
                                           IFNULL(c.IP,'') ip, IFNULL(c.RegIP,'') regIp,
                                           IF(c.Online=1,1,0) isOnline
                                    FROM csalogin c
                                    LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                                    WHERE c.`Name` IN ({inList})";
                    using var dc = new MySqlCommand(dSql, conn);
                    using var dr = await dc.ExecuteReaderAsync();
                    while (await dr.ReadAsync())
                        details[dr.GetString("acc")] = new IpGroupMember
                        {
                            Account    = dr.GetString("acc"),
                            CharName   = dr.GetString("charName"),
                            MasterName = dr.GetString("masterName"),
                            LoginIp    = dr.GetString("ip"),
                            RegIp      = dr.GetString("regIp"),
                            IsOnline   = Convert.ToInt32(dr["isOnline"]) == 1
                        };
                }

                foreach (var (ip, accs, online, total) in ipGroups)
                {
                    var entry = new IpGroupEntry { Ip = ip, OnlineCount = online, TotalCount = total };
                    foreach (var acc in accs)
                        if (details.TryGetValue(acc, out var m))
                            entry.Members.Add(m);
                    result.Add(entry);
                }
            }
            catch { }
            return result;
        }

        /// <summary>查詢單一帳號的 IP 以及共用該 IP 的帳號</summary>
        public async Task<SingleIpQueryResult?> GetSharedIpForAccountAsync(string account)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                string loginIp = "", regIp = "", charName = "", masterName = "";
                bool isOnline = false;

                using (var cmd = new MySqlCommand(
                    @"SELECT IFNULL(c.IP,'') ip, IFNULL(c.RegIP,'') regIp,
                             IFNULL(c.OnlineName,'') charName,
                             IFNULL(m.Name,'') masterName,
                             IF(c.Online=1,1,0) isOnline
                      FROM csalogin c
                      LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                      WHERE c.`Name`=@acc LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@acc", account);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (!await r.ReadAsync()) return null;
                    loginIp    = r.GetString("ip");
                    regIp      = r.GetString("regIp");
                    charName   = r.GetString("charName");
                    masterName = r.GetString("masterName");
                    isOnline   = Convert.ToInt32(r["isOnline"]) == 1;
                }

                var ips = new HashSet<string>();
                if (!string.IsNullOrWhiteSpace(loginIp)) ips.Add(loginIp);
                if (!string.IsNullOrWhiteSpace(regIp))   ips.Add(regIp);

                var shared = new List<IpGroupMember>();
                if (ips.Count > 0)
                {
                    var ipList = string.Join(",", ips.Select(i => $"'{i.Replace("'", "\\'")}'"));
                    var sql    = $@"SELECT c.`Name` acc, IFNULL(c.OnlineName,'') charName,
                                          IFNULL(m.Name,'') masterName,
                                          IFNULL(c.IP,'') ip, IFNULL(c.RegIP,'') regIp,
                                          IF(c.Online=1,1,0) isOnline
                                   FROM csalogin c
                                   LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                                   WHERE c.`Name` != @acc
                                     AND (c.IP IN ({ipList}) OR c.RegIP IN ({ipList}))
                                   ORDER BY c.Online DESC, c.LoginTime DESC LIMIT 200";
                    using var sc = new MySqlCommand(sql, conn);
                    sc.Parameters.AddWithValue("@acc", account);
                    using var sr = await sc.ExecuteReaderAsync();
                    while (await sr.ReadAsync())
                        shared.Add(new IpGroupMember
                        {
                            Account    = sr.GetString("acc"),
                            CharName   = sr.GetString("charName"),
                            MasterName = sr.GetString("masterName"),
                            LoginIp    = sr.GetString("ip"),
                            RegIp      = sr.GetString("regIp"),
                            IsOnline   = Convert.ToInt32(sr["isOnline"]) == 1
                        });
                }

                return new SingleIpQueryResult
                {
                    Account       = account,
                    CharName      = charName,
                    MasterName    = masterName,
                    LoginIp       = loginIp,
                    RegIp         = regIp,
                    IsOnline      = isOnline,
                    SharedMembers = shared
                };
            }
            catch { return null; }
        }

        /// <summary>查詢 IP 最早使用的帳號（原始主人）</summary>
        // ══════════════════════════════════════════════════════════
        // IP 黑白名單（ip_labels 表，GMTool 自管）
        // ══════════════════════════════════════════════════════════

        /// <summary>確保 ip_labels 表存在（首次使用時自動建立）</summary>
        public async Task EnsureIpLabelsTableAsync()
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    CREATE TABLE IF NOT EXISTS `ip_labels` (
                        `ip`         VARCHAR(64)  NOT NULL PRIMARY KEY,
                        `label`      TINYINT      NOT NULL DEFAULT 1 COMMENT '1=工作室 2=白名單',
                        `note`       VARCHAR(255) NOT NULL DEFAULT '',
                        `created_at` DATETIME     NOT NULL DEFAULT NOW()
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/IpLabels] EnsureTable: " + ex.Message); }
        }

        /// <summary>取得所有 IP 標記，回傳 ip → IpLabel 字典</summary>
        public async Task<Dictionary<string, IpLabel>> GetIpLabelsAsync()
        {
            var dict = new Dictionary<string, IpLabel>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand("SELECT ip, label, note, DATE_FORMAT(created_at,'%Y-%m-%d %H:%i') created_at FROM ip_labels", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    var ip = r.GetString("ip");
                    dict[ip] = new IpLabel
                    {
                        Ip        = ip,
                        Label     = (IpLabelType)r.GetInt32("label"),
                        Note      = r.GetString("note"),
                        CreatedAt = r.GetString("created_at"),
                    };
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/IpLabels] Get: " + ex.Message); }
            return dict;
        }

        /// <summary>設定或更新 IP 標記（工作室 or 白名單）</summary>
        public async Task SetIpLabelAsync(string ip, IpLabelType label, string note = "")
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "INSERT INTO ip_labels (ip, label, note) VALUES (@ip, @lb, @note) ON DUPLICATE KEY UPDATE label=@lb, note=@note", conn);
                cmd.Parameters.AddWithValue("@ip",   ip);
                cmd.Parameters.AddWithValue("@lb",   (int)label);
                cmd.Parameters.AddWithValue("@note", note);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/IpLabels] Set: " + ex.Message); throw; }
        }

        /// <summary>移除 IP 標記</summary>
        public async Task RemoveIpLabelAsync(string ip)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                using var cmd = new MySqlCommand("DELETE FROM ip_labels WHERE ip=@ip", conn);
                cmd.Parameters.AddWithValue("@ip", ip);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/IpLabels] Remove: " + ex.Message); throw; }
        }

        /// <summary>查詢指定帳號的封禁歷史記錄（lock 表）</summary>
        public async Task<List<PlayerBanRecord>> GetPlayerBanLogAsync(string account)
        {
            var list = new List<PlayerBanRecord>();
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    "SELECT `time` banEndTime, IFNULL(reason,'') reason FROM `lock` WHERE `Name`=@a ORDER BY `time` ASC LIMIT 50", conn);
                cmd.Parameters.AddWithValue("@a", account);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    long t = r.GetInt64("banEndTime");
                    list.Add(new PlayerBanRecord
                    {
                        IsPermanent = t == 0,
                        BanEndTime  = t == 0 ? "永久" : DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime.ToString("yyyy/MM/dd HH:mm"),
                        Reason      = r.GetString("reason"),
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/BanLog] " + ex.Message); }
            return list;
        }

        /// <summary>查詢指定帳號所屬家族資訊</summary>
        public async Task<PlayerFamilyInfo?> GetPlayerFamilyAsync(string account)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT z.jiazuid guildId, z.jiazu guildName,
                             (SELECT COUNT(*) FROM zuzhanlog zz WHERE zz.jiazuid=z.jiazuid AND zz.id IN (SELECT MAX(id) FROM zuzhanlog GROUP BY cdkey)) memberCount
                      FROM zuzhanlog z
                      WHERE z.cdkey=@a AND z.jiazuid>0
                      ORDER BY z.id DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@a", account);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return new PlayerFamilyInfo
                    {
                        FamilyId    = Convert.ToInt32(r["guildId"]),
                        FamilyName  = r["guildName"]?.ToString() ?? "",
                        MemberCount = Convert.ToInt32(r["memberCount"]),
                    };
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/PlayerFamily] " + ex.Message); }
            return null;
        }

        public async Task<IpOwnerResult?> GetIpOwnerAsync(string ip)
        {
            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(
                    @"SELECT c.`Name` account,
                             IFNULL(c.OnlineName,'') charName,
                             IFNULL(m.Name,'') masterName,
                             IFNULL(c.IP,'') loginIp,
                             IFNULL(c.RegIP,'') regIp,
                             IF(c.Online=1,1,0) isOnline,
                             IFNULL(DATE_FORMAT(IFNULL(c.created_at,c.LoginTime),'%Y-%m-%d %H:%i'),'') regTime,
                             CASE WHEN c.RegIP=@ip THEN '註冊IP命中' ELSE '登入IP命中' END matchType
                      FROM csalogin c
                      LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                      WHERE c.RegIP=@ip OR c.IP=@ip
                      ORDER BY IFNULL(c.created_at,c.LoginTime) ASC
                      LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@ip", ip);
                using var r = await cmd.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return null;
                return new IpOwnerResult
                {
                    Ip         = ip,
                    Account    = r.GetString("account"),
                    CharName   = r.GetString("charName"),
                    MasterName = r.GetString("masterName"),
                    LoginIp    = r.GetString("loginIp"),
                    RegIp      = r.GetString("regIp"),
                    IsOnline   = Convert.ToInt32(r["isOnline"]) == 1,
                    RegTime    = r.GetString("regTime"),
                    MatchType  = r.GetString("matchType")
                };
            }
            catch { return null; }
        }
    }

    // ── 擴充：DataReader 是否含指定欄位 ──────────────────────────
    internal static class DataReaderExtensions
    {
        public static bool HasColumn(this MySqlDataReader r, string name)
        {
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    // ── IP 查詢資料模型 ──────────────────────────────────────────
    public class IpGroupEntry
    {
        public string Ip          { get; set; } = "";
        public int    OnlineCount { get; set; }
        public int    TotalCount  { get; set; }
        public List<IpGroupMember> Members { get; set; } = new();
    }

    public class IpGroupMember
    {
        public string Account    { get; set; } = "";
        public string CharName   { get; set; } = "";
        public string MasterName { get; set; } = "";
        public string LoginIp    { get; set; } = "";
        public string RegIp      { get; set; } = "";
        public bool   IsOnline   { get; set; }
    }

    public class SingleIpQueryResult
    {
        public string Account       { get; set; } = "";
        public string CharName      { get; set; } = "";
        public string MasterName    { get; set; } = "";
        public string LoginIp       { get; set; } = "";
        public string RegIp         { get; set; } = "";
        public bool   IsOnline      { get; set; }
        public List<IpGroupMember> SharedMembers { get; set; } = new();
    }

    public class IpOwnerResult
    {
        public string Ip         { get; set; } = "";
        public string Account    { get; set; } = "";
        public string CharName   { get; set; } = "";
        public string MasterName { get; set; } = "";
        public string LoginIp    { get; set; } = "";
        public string RegIp      { get; set; } = "";
        public bool   IsOnline   { get; set; }
        public string RegTime    { get; set; } = "";
        public string MatchType  { get; set; } = "";
    }

    public class PlayerBanRecord
    {
        public string BanEndTime  { get; set; } = "";
        public bool   IsPermanent { get; set; }
        public string Reason      { get; set; } = "";
    }

    public class PlayerFamilyInfo
    {
        public int    FamilyId    { get; set; }
        public string FamilyName  { get; set; } = "";
        public int    MemberCount { get; set; }
    }

    public enum IpLabelType { None = 0, Studio = 1, Whitelist = 2 }

    public class IpLabel
    {
        public string       Ip        { get; set; } = "";
        public IpLabelType  Label     { get; set; }
        public string       Note      { get; set; } = "";
        public string       CreatedAt { get; set; } = "";
    }
}
