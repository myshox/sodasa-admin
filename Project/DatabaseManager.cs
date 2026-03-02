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
                // 優先使用傳入的既有連線，避免多開連線
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

        private MySqlConnection GetConnection() => new MySqlConnection(_connectionString);

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

            string sql = string.IsNullOrWhiteSpace(query)
                ? $@"SELECT c.MasterId, c.`Name`, c.OnlineName, c.Online, c.LoginTime, c.ServerId,
                           IFNULL(p.point, 0)   AS PayTotal,
                           IFNULL(pet.cnt, 0)   AS PetCount,
                           IFNULL(m.`Name`,'')  AS MasterName
                           {idCol}
                    FROM csalogin c
                    LEFT JOIN paydata p          ON p.cdkey = c.`Name`
                    LEFT JOIN csaloginmaster m   ON m.Id    = c.MasterId
                    LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet
                           ON pet.cdkey = c.`Name`
                    ORDER BY c.Online DESC, c.LoginTime DESC {limitClause}"
                : $@"SELECT c.MasterId, c.`Name`, c.OnlineName, c.Online, c.LoginTime, c.ServerId,
                           IFNULL(p.point, 0)   AS PayTotal,
                           IFNULL(pet.cnt, 0)   AS PetCount,
                           IFNULL(m.`Name`,'')  AS MasterName
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
            // maildata 沒有 num/數量欄位：數量 > 1 時插入多筆記錄
            // 對應 [gm newsend/additem 編號 數量 帳號] 指令
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

        public async Task<(int success, int fail)> BatchSendMailAsync(
            SendMailRequest template,
            IProgress<(int done, int total, string account, bool ok)> progress,
            CancellationToken ct,
            int batchSize = 100,
            HashSet<string>? excludeSet = null)
        {
            // 取全部帳號（不用 SearchPlayersAsync，避免 LIMIT 300 限制）
            var allAccounts = new List<string>();
            using (var connA = GetConnection())
            {
                await connA.OpenAsync();
                using var cmdA = new MySqlCommand("SELECT `Name` FROM csalogin ORDER BY `Name`", connA);
                using var rA   = await cmdA.ExecuteReaderAsync();
                while (await rA.ReadAsync()) allAccounts.Add(rA.GetString(0));
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
                EndTime   = (int)(now + 30L * 24 * 3600), // 30 天後到期
                Buff3     = "",
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

            stats.OnlineCount     = await Scalar("SELECT COUNT(*) FROM csalogin WHERE Online=1");
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

            // csalogin 主資料 + LEFT JOIN csaloginmaster 取主帳號名
            using (var cmd = new MySqlCommand(
                @"SELECT c.*, IFNULL(m.`Name`,'') AS MasterName
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
                    // 輩份欄位（Belong），若欄位不存在則保留 -1
                    try { detail.Belong = r["Belong"] == DBNull.Value ? 0 : Convert.ToInt32(r["Belong"]); }
                    catch { detail.Belong = -1; }
                    // csalogin 自動遞增主鍵 id（直接用快取值，避免在 DataReader 開啟時再執行 SQL）
                    if (_csaloginHasId == true)
                        try { detail.CharDbId = r["id"] == DBNull.Value ? 0 : Convert.ToInt32(r["id"]); }
                        catch { detail.CharDbId = 0; }
                }
            }

            // 寵物數量 + 最強寵物四圍素質（hp/attack/def/quick/sum）
            // cdkey 可能存登入帳號 / 角色名 / uid，三者都嘗試
            string petCharName = detail.OnlineName;
            string petUid      = detail.Uid;
            using (var cmd2 = new MySqlCommand(
                "SELECT COUNT(*) FROM capturepet WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid)", conn))
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
                      FROM capturepet WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid)
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
            // 累積儲值使用 csalogin.PayTotal（與網頁版、AdjustPayDataPointAsync 一致），不用 paydata.point
            string sql = $@"
                SELECT c.MasterId, c.`Name`, c.OnlineName, c.Online, c.LoginTime, c.ServerId,
                       IFNULL(c.PayTotal, 0) AS PayTotal,
                       IFNULL(pet.cnt, 0)   AS PetCount,
                       IFNULL(m.`Name`,'')  AS MasterName
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
        /// 重設玩家登入密碼。newPassword 為明文，自動以 MD5 轉換後存入 PassWord 欄位。
        /// </summary>
        public async Task<bool> ResetPlayerPasswordAsync(string account, string newPassword, string field = "PassWord")
        {
            // MD5 轉換（小寫 32 位十六進位）
            string md5Hash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(newPassword);
                md5Hash = BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-", "").ToLower();
            }

            using var conn = GetConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                $"UPDATE csalogin SET `{field}`=@pwd WHERE `Name`=@name", conn);
            cmd.Parameters.AddWithValue("@pwd",  md5Hash);
            cmd.Parameters.AddWithValue("@name", account);
            bool ok = await cmd.ExecuteNonQueryAsync() > 0;
            if (ok) await GmLogger.Instance.LogAsync("重設玩家密碼",
                account, $"欄位：{field}（已 MD5 加密）", true);
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
            // LEFT JOIN csalogin 取角色名稱；role_name 欄位存的是帳號(cdkey)
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
            using var cmd = new MySqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(filter))
                cmd.Parameters.AddWithValue("@q", $"%{filter}%");
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new RechargeRecord
                {
                    Id          = r.GetInt32("id"),
                    OrderNo     = r["order_no"]?.ToString() ?? "",
                    RoleName    = r["role_name"]?.ToString() ?? "",
                    CharName    = r["charName"]?.ToString() ?? "",
                    ProductName = r["product_name"]?.ToString() ?? "",
                    Amount      = r["amount"] == DBNull.Value ? 0 : Convert.ToDecimal(r["amount"]),
                    Status      = r["status"]?.ToString() ?? "",
                    CreatedAt   = r["created_at"]?.ToString() ?? ""
                });
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
        /// </summary>
        public async Task<(List<ShopSaleRecord> items, List<ShopSpenderRecord> spenders)>
            GetShopTopItemsAsync(string table, int topN = 20)
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

            // ── 按表格類型分別查詢 ────────────────────────────────
            if (table == "vipshop" || table == "fameshop")
            {
                // 這兩張表結構相同: cdkey, name, itemid, itemname, itemnum, time, oldpoint, newpoint
                // 熱賣商品
                string sql1 = $@"
                    SELECT itemid, itemname,
                           SUM(itemnum) AS total_qty,
                           COUNT(*) AS order_count,
                           SUM(oldpoint - newpoint) AS total_cost,
                           MAX(time) AS last_time
                    FROM `{table}`
                    GROUP BY itemid, itemname
                    ORDER BY total_qty DESC
                    LIMIT {topN}";
                using (var cmd = new MySqlCommand(sql1, conn))
                using (var r = await cmd.ExecuteReaderAsync())
                {
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

                // 消費排行（玩家）
                string sql2 = $@"
                    SELECT cdkey, name,
                           SUM(itemnum) AS total_qty,
                           SUM(oldpoint - newpoint) AS total_cost
                    FROM `{table}`
                    GROUP BY cdkey, name
                    ORDER BY total_cost DESC
                    LIMIT {topN}";
                using (var cmd2 = new MySqlCommand(sql2, conn))
                using (var r2 = await cmd2.ExecuteReaderAsync())
                {
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
                           COUNT(*) AS order_count
                    FROM `{table}`
                    GROUP BY itemid
                    ORDER BY total_qty DESC
                    LIMIT {topN}";
                using var cmd = new MySqlCommand(sql1, conn);
                using var r = await cmd.ExecuteReaderAsync();
                int rank = 1;
                while (await r.ReadAsync())
                    items.Add(new ShopSaleRecord
                    {
                        Rank       = rank++,
                        ItemId     = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]),
                        ItemName   = $"道具 #{r["itemid"]}",
                        TotalQty   = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]),
                        OrderCount = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"])
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

            // 同時比對：登入帳號 / 角色名 / uid
            using var cmd = new MySqlCommand(
                @"SELECT unicode,id,name,type,lv,hp,attack,def,quick,sum,author,cdkey,`check`
                  FROM capturepet
                  WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid)
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

        /// <summary>讀取玩家的消費達成進度（costdata），支援主帳號名/角色名/UID</summary>
        public async Task<(long point, int check, string uid, string onlineName)> GetCostDataAsync(string account)
        {
            try
            {
                using var conn = GetConnection(); await conn.OpenAsync();
                var (uid, onlineName) = await ResolveCsaloginUidAsync(conn, account);
                using var cmd = new MySqlCommand(
                    "SELECT point, IFNULL(`check`,0) AS ck FROM costdata WHERE cdkey=@acc ORDER BY time DESC LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@acc", uid);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                    return (r["point"] == DBNull.Value ? 0 : Convert.ToInt64(r["point"]),
                            r["ck"]    == DBNull.Value ? 0 : Convert.ToInt32(r["ck"]),
                            uid, onlineName);
                return (0, 0, uid, onlineName);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[DB/GetCostData] " + ex.Message); }
            return (0, 0, account, "");
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
                    await GmLogger.Instance.LogAsync("重置消費進度", uid, "costdata.check 歸零（已領狀態清除，point 保留）", true);
                return rows > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DB/ResetCostData] " + ex.Message);
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
                             SUM(CASE WHEN Online=1 THEN 1 ELSE 0 END) AS OnlineCount,
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
    }
}
