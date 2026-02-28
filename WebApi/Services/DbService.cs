using System.Linq;
using MySqlConnector;
using WebApi.Models;

namespace WebApi.Services;

public class DbService
{
    private readonly string _conn;
    public DbService(IConfiguration cfg) =>
        _conn = cfg.GetConnectionString("Default")!;

    private MySqlConnection Open() => new(_conn);

    // ── 玩家搜尋 ─────────────────────────────────────────────
    public async Task<List<PlayerRow>> SearchPlayersAsync(string kw, int limit = 50)
    {
        await using var db = Open();
        await db.OpenAsync();
        // 嘗試含主帳號的完整查詢；若 csaloginmaster 不存在則降級
        string[] sqls = {
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline, IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   IFNULL(pet.cnt,0) petCount, IFNULL(c.PayTotal,0) payTotal,
                   IFNULL(m.`Name`,'') masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet ON pet.cdkey=c.`Name`
            LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
            WHERE c.`Name` LIKE @kw OR c.OnlineName LIKE @kw
            ORDER BY c.Online DESC, c.LoginTime DESC LIMIT @lim",
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline, IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   IFNULL(pet.cnt,0) petCount, IFNULL(c.PayTotal,0) payTotal, '' masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet ON pet.cdkey=c.`Name`
            WHERE c.`Name` LIKE @kw OR c.OnlineName LIKE @kw
            ORDER BY c.Online DESC, c.LoginTime DESC LIMIT @lim"
        };
        var list = new List<PlayerRow>();
        foreach (var sql in sqls)
        {
            try
            {
                await using var cmd = new MySqlCommand(sql, db);
                cmd.Parameters.AddWithValue("@kw", $"%{kw}%");
                cmd.Parameters.AddWithValue("@lim", limit);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(MapRow(r));
                return list;
            }
            catch { list.Clear(); }
        }
        return list;
    }

    // ── 玩家詳情 ─────────────────────────────────────────────
    public async Task<PlayerDetail?> GetDetailAsync(string account)
    {
        await using var db = Open();
        await db.OpenAsync();

        PlayerDetail? d = null;

        // 嘗試完整查詢（含 lock/maildata/capturepet）
        try
        {
            var sqlFull = @"
                SELECT c.`Name` account,
                       IFNULL(c.OnlineName,'') onlineName,
                       (c.Online=1) isOnline,
                       IFNULL(c.ServerId,0) serverId,
                       IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                       IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                       IFNULL(c.IP,'') ip,
                       IFNULL(c.RegIP,'') regIP,
                       (lk.Name IS NOT NULL) isBanned,
                       IFNULL(lk.time,0) banTime,
                       IFNULL(c.VipPoint,0) gold,
                       IFNULL(c.PetPoint,0) crystal,
                       IFNULL(c.uid,'') uid,
                       IFNULL(c.MAC1,'') mac,
                       (c.Offline=1) isMuted,
                       IFNULL(c.PayTotal,0) payTotal,
                       IFNULL(mail.total,0) totalMails,
                       IFNULL(mail.unread,0) unreadMails,
                       IFNULL(pet.cnt,0) petCount
                FROM csalogin c
                LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
                LEFT JOIN (
                    SELECT receiverid,
                           COUNT(*) AS total,
                           SUM(CASE WHEN isread=0 THEN 1 ELSE 0 END) AS unread
                    FROM maildata GROUP BY receiverid
                ) mail ON mail.receiverid=c.`Name`
                LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet
                       ON pet.cdkey=c.`Name`
                WHERE c.`Name`=@acc LIMIT 1";
            await using var cmd = new MySqlCommand(sqlFull, db);
            cmd.Parameters.AddWithValue("@acc", account);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            d = new PlayerDetail
            {
                Account     = r.GetString("account"),
                OnlineName  = r.GetString("onlineName"),
                IsOnline    = r.GetBoolean("isOnline"),
                ServerId    = r.GetInt32("serverId"),
                RegTime     = r.GetString("regTime"),
                LoginTime   = r.GetString("loginTime"),
                IP          = r.GetString("ip"),
                RegIP       = r.GetString("regIP"),
                IsBanned    = r.GetBoolean("isBanned"),
                Gold        = r.GetInt64("gold"),
                Crystal     = r.GetInt64("crystal"),
                Uid         = r.GetString("uid"),
                MAC         = r.GetString("mac"),
                IsMuted     = r.GetBoolean("isMuted"),
                PayTotal    = r.GetInt64("payTotal"),
                TotalMails  = r.GetInt32("totalMails"),
                UnreadMails = r.GetInt32("unreadMails"),
                PetCount    = r.GetInt32("petCount"),
            };
            if (d.IsBanned)
            {
                long banTime = r.GetInt64("banTime");
                d.BanEndTime = banTime == 0 ? "永久" :
                    DateTimeOffset.FromUnixTimeSeconds(banTime).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
            }
        }
        catch
        {
            // fallback：最小查詢，不依賴可能缺失的表格
            try
            {
                var sqlMin = @"SELECT `Name` account,
                           IFNULL(OnlineName,'') onlineName,
                           (Online=1) isOnline,
                           IFNULL(ServerId,0) serverId,
                           IFNULL(DATE_FORMAT(created_at,'%Y-%m-%d %H:%i'),'') regTime,
                           IFNULL(DATE_FORMAT(LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                           IFNULL(IP,'') ip,
                           IFNULL(RegIP,'') regIP,
                           0 isBanned, 0 banTime,
                           IFNULL(VipPoint,0) gold,
                           IFNULL(PetPoint,0) crystal,
                           IFNULL(uid,'') uid,
                           IFNULL(MAC1,'') mac,
                           (Offline=1) isMuted,
                           IFNULL(PayTotal,0) payTotal,
                           0 totalMails, 0 unreadMails, 0 petCount
                    FROM csalogin WHERE `Name`=@acc LIMIT 1";
                await using var cmd2 = new MySqlCommand(sqlMin, db);
                cmd2.Parameters.AddWithValue("@acc", account);
                await using var r2 = await cmd2.ExecuteReaderAsync();
                if (!await r2.ReadAsync()) return null;
                d = new PlayerDetail
                {
                    Account    = r2.GetString("account"),
                    OnlineName = r2.GetString("onlineName"),
                    IsOnline   = r2.GetBoolean("isOnline"),
                    ServerId   = r2.GetInt32("serverId"),
                    RegTime    = r2.GetString("regTime"),
                    LoginTime  = r2.GetString("loginTime"),
                    IP         = r2.GetString("ip"),
                    RegIP      = r2.GetString("regIP"),
                    IsBanned   = false,
                    Gold       = r2.GetInt64("gold"),
                    Crystal    = r2.GetInt64("crystal"),
                    Uid        = r2.GetString("uid"),
                    MAC        = r2.GetString("mac"),
                    IsMuted    = r2.GetBoolean("isMuted"),
                    PayTotal   = r2.GetInt64("payTotal"),
                };
            }
            catch { return null; }
        }

        if (d == null) return null;
        d.VipLevel = d.PayTotal >= 15000 ? 2 : d.PayTotal >= 5000 ? 1 : 0;

        // 可選欄位：PayPoint、RmbPoint（充值點/R幣）
        try
        {
            await using var cmdX = new MySqlCommand(
                "SELECT IFNULL(PayPoint,0) pp, IFNULL(RmbPoint,0) rp FROM csalogin WHERE `Name`=@acc LIMIT 1", db);
            cmdX.Parameters.AddWithValue("@acc", account);
            await using var rX = await cmdX.ExecuteReaderAsync();
            if (await rX.ReadAsync()) { d.PayPoint = rX.GetInt64("pp"); d.RmbPoint = rX.GetInt64("rp"); }
        }
        catch { }

        // 可選欄位：GroupId、NeiCe（GM 權限）
        try
        {
            await using var cmdG = new MySqlCommand(
                "SELECT IFNULL(GroupId,0) gid, IFNULL(NeiCe,0) nc FROM csalogin WHERE `Name`=@acc LIMIT 1", db);
            cmdG.Parameters.AddWithValue("@acc", account);
            await using var rG = await cmdG.ExecuteReaderAsync();
            if (await rG.ReadAsync()) { d.GroupId = rG.GetInt32("gid"); d.NeiCe = rG.GetInt32("nc"); }
        }
        catch { }

        // 可選：主帳號名稱（csaloginmaster 可能不存在）
        try
        {
            await using var cmdM = new MySqlCommand(
                @"SELECT IFNULL(m.`Name`,'') mname FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  WHERE c.`Name`=@acc LIMIT 1", db);
            cmdM.Parameters.AddWithValue("@acc", account);
            await using var rM = await cmdM.ExecuteReaderAsync();
            if (await rM.ReadAsync()) d.MasterName = rM.GetString("mname");
        }
        catch { }

        // paydata 循環進度（可能不存在或欄位不同）
        try
        {
            await using var cmdPd = new MySqlCommand(
                "SELECT IFNULL(point,0) pt, IFNULL(totalcheck,0) tc, IFNULL(lifetime_total,point) lt FROM paydata WHERE cdkey=@acc LIMIT 1", db);
            cmdPd.Parameters.AddWithValue("@acc", account);
            await using var rPd = await cmdPd.ExecuteReaderAsync();
            if (await rPd.ReadAsync())
            {
                d.PaydataPoint = rPd.GetInt64("pt");
                d.TotalCheck   = rPd.GetInt64("tc");
                d.PaydataTotal = rPd.GetInt64("lt");
            }
        }
        catch { }

        return d;
    }

    // ── 改名 ─────────────────────────────────────────────────
    // ── 強制下線 ──────────────────────────────────────────────
    public async Task<bool> ForceOfflineAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE csalogin SET Online=0 WHERE `Name`=@n", db);
        cmd.Parameters.AddWithValue("@n", account);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── 禁言 ─────────────────────────────────────────────────
    public async Task<bool> SetMuteAsync(string account, bool mute)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE csalogin SET Offline=@v WHERE `Name`=@n", db);
        cmd.Parameters.AddWithValue("@v", mute ? 1 : 0);
        cmd.Parameters.AddWithValue("@n", account);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── 重置 paydata 循環進度 ─────────────────────────────────
    public async Task<bool> ResetPaydataAsync(string account)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "UPDATE paydata SET point=0 WHERE cdkey=@a", db);
            cmd.Parameters.AddWithValue("@a", account);
            return await cmd.ExecuteNonQueryAsync() >= 0;
        }
        catch { return false; }
    }

    // ── 設定金幣 ─────────────────────────────────────────────
    public async Task<bool> SetGoldAsync(string account, long val)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE csalogin SET VipPoint=@v WHERE `Name`=@a", db);
        cmd.Parameters.AddWithValue("@v", val);
        cmd.Parameters.AddWithValue("@a", account);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── 設定水晶 ─────────────────────────────────────────────
    public async Task<bool> SetCrystalAsync(string account, long val)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE csalogin SET PetPoint=@v WHERE `Name`=@a", db);
        cmd.Parameters.AddWithValue("@v", val);
        cmd.Parameters.AddWithValue("@a", account);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── 封號 ─────────────────────────────────────────────────
    public async Task<bool> SetBanAsync(string account, bool ban, int days = 0, double hours = 0)
    {
        await using var db = Open(); await db.OpenAsync();
        if (ban)
        {
            long endUnix = 0;
            if (hours > 0) endUnix = (long)DateTimeOffset.Now.AddHours(hours).ToUnixTimeSeconds();
            else if (days > 0) endUnix = DateTimeOffset.Now.AddDays(days).ToUnixTimeSeconds();
            // 0 = 永久
            await using var cmd = new MySqlCommand(
                "INSERT INTO `lock`(`Name`,`time`) VALUES(@n,@t) ON DUPLICATE KEY UPDATE `time`=@t", db);
            cmd.Parameters.AddWithValue("@n", account);
            cmd.Parameters.AddWithValue("@t", endUnix);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        else
        {
            await using var cmd = new MySqlCommand(
                "DELETE FROM `lock` WHERE `Name`=@n", db);
            cmd.Parameters.AddWithValue("@n", account);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }

    // ── 改名 ─────────────────────────────────────────────────
    public async Task<bool> RenamePlayerAsync(string account, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE csalogin SET OnlineName=@n WHERE `Name`=@a", db);
        cmd.Parameters.AddWithValue("@n", newName.Trim());
        cmd.Parameters.AddWithValue("@a", account);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── 玩家列表（全服，供批量操作用）────────────────────────
    public async Task<List<PlayerRow>> GetPlayerListAsync(int limit = 500)
        => await RunWithFallbackAsync(
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline, IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   IFNULL(pet.cnt,0) petCount, IFNULL(c.PayTotal,0) payTotal, IFNULL(m.`Name`,'') masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet ON pet.cdkey=c.`Name`
            LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
            ORDER BY c.Online DESC, c.LoginTime DESC LIMIT @lim",
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline, IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   IFNULL(pet.cnt,0) petCount, IFNULL(c.PayTotal,0) payTotal, '' masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet ON pet.cdkey=c.`Name`
            ORDER BY c.Online DESC, c.LoginTime DESC LIMIT @lim",
            p => { p.AddWithValue("@lim", limit); });

    // ── 線上玩家 ─────────────────────────────────────────────
    public async Task<List<PlayerRow>> GetOnlineAsync()
        => await RunWithFallbackAsync(
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName, 1 isOnline,
                   IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   0 petCount, IFNULL(c.PayTotal,0) payTotal, IFNULL(m.`Name`,'') masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
            WHERE c.Online=1 ORDER BY c.ServerId",
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName, 1 isOnline,
                   IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   0 petCount, IFNULL(c.PayTotal,0) payTotal, '' masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            WHERE c.Online=1 ORDER BY c.ServerId",
            _ => { });

    private async Task<List<PlayerRow>> RunWithFallbackAsync(string sql1, string sql2, Action<MySqlParameterCollection> paramFn)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<PlayerRow>();

        // sql3：最小化 fallback，只查 csalogin，不依賴任何可能缺失的資料表
        const string sql3 = @"SELECT `Name` account, IFNULL(OnlineName,'') onlineName,
               (Online=1) isOnline, IFNULL(ServerId,0) serverId,
               IFNULL(DATE_FORMAT(created_at,'%Y-%m-%d %H:%i'),'') regTime,
               IFNULL(DATE_FORMAT(LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
               IFNULL(IP,'') ip, 0 isBanned,
               IFNULL(VipPoint,0) gold, IFNULL(PetPoint,0) crystal,
               0 petCount, IFNULL(PayTotal,0) payTotal, '' masterName
        FROM csalogin ORDER BY Online DESC, LoginTime DESC LIMIT @lim";

        foreach (var sql in new[] { sql1, sql2, sql3 })
        {
            try
            {
                await using var cmd = new MySqlCommand(sql, db);
                paramFn(cmd.Parameters);
                // sql3 也接受 @lim 參數，其他不需要的參數會被忽略
                if (!cmd.Parameters.Contains("@lim"))
                    cmd.Parameters.AddWithValue("@lim", 1000);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) list.Add(MapRow(r));
                return list;
            }
            catch { list.Clear(); }
        }
        return list;
    }

    // ── Dashboard 統計 ───────────────────────────────────────
    public async Task<DashboardStats> GetStatsAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        // 分開查詢，避免子查詢語法問題
        var stats = new DashboardStats();
        async Task<long> Scalar(string sql)
        {
            await using var c = new MySqlCommand(sql, db);
            var v = await c.ExecuteScalarAsync();
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }
        stats.TotalPlayers  = (int)await Scalar("SELECT COUNT(*) FROM csalogin");
        stats.OnlinePlayers = (int)await Scalar("SELECT COUNT(*) FROM csalogin WHERE Online=1");
        stats.BannedPlayers = (int)await Scalar("SELECT COUNT(*) FROM `lock`");
        stats.NewToday      = (int)await Scalar("SELECT COUNT(*) FROM csalogin WHERE DATE(created_at)=CURDATE()");
        stats.TotalGold     = await Scalar("SELECT IFNULL(SUM(VipPoint),0) FROM csalogin");
        stats.TotalCrystal  = await Scalar("SELECT IFNULL(SUM(PetPoint),0) FROM csalogin");
        return stats;
    }


    // ── 主帳號查詢 ───────────────────────────────────────────
    public async Task<object?> GetMasterAsync(string masterName)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmdM = new MySqlCommand(
            "SELECT Id FROM csaloginmaster WHERE `Name`=@n LIMIT 1", db);
        cmdM.Parameters.AddWithValue("@n", masterName);
        var mid = await cmdM.ExecuteScalarAsync();
        if (mid == null) return null;

        await using var cmd = new MySqlCommand(
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') charName,
                     (c.Online=1) isOnline,
                     IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                     IFNULL(c.PayTotal,0) payTotal,
                     IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                     (lk.`Name` IS NOT NULL) isBanned,
                     IFNULL(pet.cnt,0) petCount
              FROM csalogin c
              LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
              LEFT JOIN (SELECT cdkey, COUNT(*) cnt FROM capturepet GROUP BY cdkey) pet ON pet.cdkey=c.`Name`
              WHERE c.MasterId=@mid ORDER BY c.Online DESC", db);
        cmd.Parameters.AddWithValue("@mid", mid);
        var chars = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            chars.Add(new {
                account   = r.GetString("account"),
                charName  = r.GetString("charName"),
                isOnline  = r.GetBoolean("isOnline"),
                gold      = r.GetInt64("gold"),
                crystal   = r.GetInt64("crystal"),
                payTotal  = r.GetInt64("payTotal"),
                loginTime = r.GetString("loginTime"),
                isBanned  = r.GetBoolean("isBanned"),
                petCount  = r.GetInt32("petCount"),
            });
        return new { masterName, chars };
    }

    // ── 充值記錄（優先查 recharge_orders，否則降級查 paydata）────────
    public async Task<List<object>> GetRechargeAsync(string kw)
    {
        var list = new List<object>();
        await using var db = Open(); await db.OpenAsync();
        // 先嘗試 recharge_orders 表（與 EXE 一致）
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT o.order_no,
                         IFNULL(c.OnlineName,'') charName,
                         IFNULL(o.role_name,'') account,
                         IFNULL(o.product_name,'') productName,
                         IFNULL(o.amount,0) yuanbao,
                         IFNULL(o.twd_amount, ROUND(o.amount/100)) twd,
                         IFNULL(o.status,'') status,
                         IFNULL(DATE_FORMAT(o.created_at,'%Y-%m-%d %H:%i'),'') time
                  FROM recharge_orders o
                  LEFT JOIN csalogin c ON c.`Name`=o.role_name
                  WHERE (@q='' OR o.role_name LIKE @q OR IFNULL(c.OnlineName,'') LIKE @q OR IFNULL(o.product_name,'') LIKE @q)
                  ORDER BY o.created_at DESC LIMIT 500", db);
            cmd.Parameters.AddWithValue("@q", string.IsNullOrEmpty(kw) ? "" : $"%{kw}%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    orderNo     = TryGetString(r, "order_no"),
                    account     = r.GetString("account"),
                    charName    = r.GetString("charName"),
                    productName = r.GetString("productName"),
                    yuanbao     = TryGetInt64(r, "yuanbao"),
                    twd         = TryGetInt64(r, "twd"),
                    status      = r.GetString("status"),
                    time        = r.GetString("time"),
                    source      = "orders"
                });
            return list;
        }
        catch { list.Clear(); }
        // 降級：paydata 表（顯示累積進度，非訂單）
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT p.cdkey account, IFNULL(c.OnlineName,'') charName,
                         IFNULL(p.point,0) twd, IFNULL(p.lifetime_total, p.point) lifetimeTotal,
                         IFNULL(p.totalcheck,0) totalCheck,
                         IFNULL(DATE_FORMAT(p.time,'%Y-%m-%d %H:%i'),'') time
                  FROM paydata p
                  LEFT JOIN csalogin c ON c.`Name`=p.cdkey
                  WHERE (@q='' OR p.cdkey LIKE @q OR IFNULL(c.OnlineName,'') LIKE @q)
                  ORDER BY p.point DESC LIMIT 200", db);
            cmd.Parameters.AddWithValue("@q", string.IsNullOrEmpty(kw) ? "" : $"%{kw}%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    orderNo     = "",
                    account     = r.GetString("account"),
                    charName    = r.GetString("charName"),
                    productName = "累積充值進度",
                    yuanbao     = TryGetInt64(r, "twd") * 100,
                    twd         = TryGetInt64(r, "twd"),
                    lifetimeTotal = TryGetInt64(r, "lifetimeTotal"),
                    totalCheck  = TryGetInt64(r, "totalCheck"),
                    status      = "paydata",
                    time        = r.GetString("time"),
                    source      = "paydata"
                });
        }
        catch { }
        return list;
    }

    // ── 發放累積獎勵（check: 0=待領, 1=已領）防呆版 ────────────────
    public async Task<string> ClaimPaydataRewardAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        try
        {
            long checkVal = -1, totalCheck = 0;
            await using (var cmdGet = new MySqlCommand(
                "SELECT IFNULL(`check`,1) ck, IFNULL(totalcheck,0) tc FROM paydata WHERE cdkey=@a FOR UPDATE", db, (MySqlTransaction)tx))
            {
                cmdGet.Parameters.AddWithValue("@a", account);
                await using var r = await cmdGet.ExecuteReaderAsync();
                if (!await r.ReadAsync()) { await tx.RollbackAsync(); return "not_found"; }
                checkVal   = Convert.ToInt64(r["ck"]);
                totalCheck = Convert.ToInt64(r["tc"]);
            }
            // 防呆 1：還沒完成任何循環
            if (totalCheck == 0) { await tx.RollbackAsync(); return "no_cycle"; }
            // 防呆 2：已領過（check=1）
            if (checkVal != 0) { await tx.RollbackAsync(); return "already_claimed"; }

            // 標記已領
            await using var cmdUp = new MySqlCommand(
                "UPDATE paydata SET `check`=1 WHERE cdkey=@a", db, (MySqlTransaction)tx);
            cmdUp.Parameters.AddWithValue("@a", account);
            await cmdUp.ExecuteNonQueryAsync();
            await tx.CommitAsync();
            return totalCheck.ToString(); // 回傳當前輪次
        }
        catch { await tx.RollbackAsync(); return "error"; }
    }

    // ── 修復 paydata 循環（與 EXE FixPaydataCheckAsync 一致）────────
    public async Task<bool> FixPaydataCheckAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        try
        {
            long currentPoint = 0, currentTotalCheck = 0;
            await using (var cmdGet = new MySqlCommand(
                "SELECT IFNULL(point,0) AS pt, IFNULL(totalcheck,0) AS tc FROM paydata WHERE cdkey=@cdkey", db))
            {
                cmdGet.Parameters.AddWithValue("@cdkey", account);
                await using var r = await cmdGet.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return false;
                currentPoint      = Convert.ToInt64(r["pt"]);
                currentTotalCheck = Convert.ToInt64(r["tc"]);
            }
            long completedCycles = currentPoint > 0 ? (currentPoint - 1) / CYCLE_MAX : 0;
            long newCyclePoint   = currentPoint - completedCycles * CYCLE_MAX;
            long newTotalCheck   = currentTotalCheck + completedCycles;
            await using var cmdFix = new MySqlCommand(
                "UPDATE paydata SET point=@newpt, `check`=0, totalcheck=@tc WHERE cdkey=@cdkey", db);
            cmdFix.Parameters.AddWithValue("@newpt", newCyclePoint);
            cmdFix.Parameters.AddWithValue("@tc",    newTotalCheck);
            cmdFix.Parameters.AddWithValue("@cdkey", account);
            return await cmdFix.ExecuteNonQueryAsync() > 0;
        }
        catch { return false; }
    }

    // ── 玩家 paydata 摘要（供儲值頁顯示）────────────────────────
    public async Task<object> GetPaydataSummaryAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        long point = 0, tc = 0, lt = 0, payTotal = 0, gold = 0, crystal = 0;
        string onlineName = "", masterName = "";
        bool isOnline = false;
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                         IFNULL(c.PayTotal,0) payTotal, (c.Online=1) isOnline,
                         IFNULL(c.OnlineName,'') onlineName
                  FROM csalogin c WHERE c.`Name`=@acc LIMIT 1", db);
            cmd.Parameters.AddWithValue("@acc", account);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) {
                gold = r.GetInt64("gold"); crystal = r.GetInt64("crystal");
                payTotal = r.GetInt64("payTotal"); isOnline = r.GetBoolean("isOnline");
                onlineName = r.GetString("onlineName");
            }
        }
        catch { }
        // 嘗試取主帳號名稱
        try
        {
            await using var cmdM = new MySqlCommand(
                @"SELECT IFNULL(m.`Name`,'') mname FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  WHERE c.`Name`=@acc LIMIT 1", db);
            cmdM.Parameters.AddWithValue("@acc", account);
            await using var rM = await cmdM.ExecuteReaderAsync();
            if (await rM.ReadAsync()) masterName = rM.GetString("mname");
        }
        catch { }
        long checkVal = 1; // 預設已領（不顯示按鈕）
        try
        {
            await using var cmd2 = new MySqlCommand(
                "SELECT IFNULL(point,0) pt, IFNULL(totalcheck,0) tc2, IFNULL(lifetime_total,point) lt2, IFNULL(`check`,1) ck FROM paydata WHERE cdkey=@acc LIMIT 1", db);
            cmd2.Parameters.AddWithValue("@acc", account);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            if (await r2.ReadAsync())
            {
                point    = r2.GetInt64("pt");
                tc       = r2.GetInt64("tc2");
                lt       = r2.GetInt64("lt2");
                checkVal = r2.GetInt64("ck");
            }
        }
        catch { }
        // claimReady = check==0 且已完成至少一輪（totalcheck > 0）
        // 防呆：point 在本輪尚未達標但 check=0 代表尚有未領的跨輪獎勵
        bool claimReady = checkVal == 0 && tc > 0;
        return new { account, onlineName, masterName, isOnline, gold, crystal, payTotal,
                     paydataPoint = point, totalCheck = tc, lifetimeTotal = lt,
                     paydataCheck = checkVal, claimReady,
                     vipLevel = payTotal >= 15000 ? 2 : payTotal >= 5000 ? 1 : 0 };
    }

    // ── 給予儲值（與 EXE 一致：paydata 循環 25,000、csalogin.PayTotal/VipPoint）────────
    private const long CYCLE_MAX = 25_000L;

    public async Task<bool> AdjustPayDataPointAsync(string account, long twdAmount, long goldAmount, bool giveGold, bool updatePaydata = true)
    {
        await using var db = Open();
        await db.OpenAsync();
        bool ok = true;

        if (giveGold)
        {
            await using var cmdLogin = new MySqlCommand(
                "UPDATE csalogin SET VipPoint = VipPoint + @gold, PayTotal = PayTotal + @twd WHERE `Name` = @cdkey", db);
            cmdLogin.Parameters.AddWithValue("@gold",  goldAmount);
            cmdLogin.Parameters.AddWithValue("@twd",   twdAmount);
            cmdLogin.Parameters.AddWithValue("@cdkey", account);
            ok = await cmdLogin.ExecuteNonQueryAsync() > 0;
        }

        // 若不同步累積儲值，直接回傳
        if (!updatePaydata) return ok;

        long currentPoint = 0, currentTotalCheck = 0;
        try
        {
            await using (var cmdGet = new MySqlCommand(
                "SELECT IFNULL(point,0) AS pt, IFNULL(totalcheck,0) AS tc FROM paydata WHERE cdkey=@cdkey", db))
            {
                cmdGet.Parameters.AddWithValue("@cdkey", account);
                await using var r = await cmdGet.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    currentPoint      = Convert.ToInt64(r["pt"]);
                    currentTotalCheck = Convert.ToInt64(r["tc"]);
                }
            }

            long rawTotal = currentPoint + twdAmount;
            long completedCycles = rawTotal > 0 ? (rawTotal - 1) / CYCLE_MAX : 0;
            long newCyclePoint   = rawTotal - completedCycles * CYCLE_MAX;
            long newTotalCheck   = currentTotalCheck + completedCycles;

            if (completedCycles > 0)
            {
                // ⚠ check 欄位不動，由遊戲伺服器自行管理（強制設 0 反而觸發自動領取 bug）
                await using var cmdPay = new MySqlCommand(@"
                    INSERT INTO paydata (cdkey, point, time, `check`, totalcheck, lifetime_total)
                    VALUES (@cdkey, @newpt, NOW(), 0, @tc, @lt)
                    ON DUPLICATE KEY UPDATE
                        point          = @newpt,
                        totalcheck     = @tc,
                        lifetime_total = lifetime_total + @twd", db);
                cmdPay.Parameters.AddWithValue("@cdkey", account);
                cmdPay.Parameters.AddWithValue("@newpt", newCyclePoint);
                cmdPay.Parameters.AddWithValue("@tc",    newTotalCheck);
                cmdPay.Parameters.AddWithValue("@twd",   twdAmount);
                cmdPay.Parameters.AddWithValue("@lt",    twdAmount);
                await cmdPay.ExecuteNonQueryAsync();
            }
            else
            {
                await using var cmdPay = new MySqlCommand(@"
                    INSERT INTO paydata (cdkey, point, time, `check`, totalcheck, lifetime_total)
                    VALUES (@cdkey, @twd, NOW(), 0, 0, @twd)
                    ON DUPLICATE KEY UPDATE
                        point          = point + @twd,
                        lifetime_total = lifetime_total + @twd", db);
                cmdPay.Parameters.AddWithValue("@cdkey", account);
                cmdPay.Parameters.AddWithValue("@twd",   twdAmount);
                await cmdPay.ExecuteNonQueryAsync();
            }
        }
        catch { /* paydata 表不存在時靜默忽略 */ }

        return ok;
    }

    // ── 郵件記錄 ─────────────────────────────────────────────
    public async Task<List<object>> GetMailAsync(string kw)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            @"SELECT id, IFNULL(cdkey,'') receiver,
                     IFNULL(buff1,'') sender,
                     IFNULL(buff2,'') title,
                     IFNULL(data,'') content,
                     IFNULL(check,0) isRead,
                     DATE_FORMAT(sendtime,'%Y-%m-%d %H:%i') time
              FROM maildata
              WHERE cdkey LIKE @q
              ORDER BY id DESC LIMIT 100", db);
        cmd.Parameters.AddWithValue("@q", $"%{kw}%");
        var list = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new {
                id      = r.GetInt64("id"),
                sender  = r.GetString("sender"),
                title   = r.GetString("title"),
                content = r.GetString("content"),
                isRead  = !r.IsDBNull(r.GetOrdinal("isRead")) && r.GetInt32("isRead") == 1,
                time    = r.IsDBNull(r.GetOrdinal("time")) ? "" : r.GetString("time"),
            });
        return list;
    }

    // ── 金幣日誌 ─────────────────────────────────────────────
    public async Task<List<object>> GetGoldLogAsync(string kw)
    {
        // 嘗試從 gold_log / vip_log 表讀取，若不存在回傳空
        await using var db = Open(); await db.OpenAsync();
        var list = new List<object>();
        try {
            await using var cmd = new MySqlCommand(
                @"SELECT cdkey account, before_val, after_val, (after_val-before_val) diff,
                         IFNULL(op_type,'') op,
                         DATE_FORMAT(create_time,'%Y-%m-%d %H:%i') time
                  FROM gold_log
                  WHERE cdkey LIKE @q
                  ORDER BY id DESC LIMIT 200", db);
            cmd.Parameters.AddWithValue("@q", $"%{kw}%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    account = r.GetString("account"),
                    before  = r.GetInt64("before_val"),
                    after   = r.GetInt64("after_val"),
                    diff    = r.GetInt64("diff"),
                    op      = r.GetString("op"),
                    time    = r.IsDBNull(r.GetOrdinal("time")) ? "" : r.GetString("time"),
                });
        } catch { /* 表不存在時回傳空 */ }
        return list;
    }

    // ── 發送道具至玩家信箱（maildata，type=1）──────────────────
    public async Task<(int success, int fail)> SendItemMailAsync(string account, int itemId, int quantity, string title = "", string content = "", string buff3 = "")
    {
        if (quantity < 1) quantity = 1;
        // 與 EXE 一致：無標題時使用道具ID作為名稱
        string itemLabel = $"道具#{itemId}";
        string buff1 = string.IsNullOrWhiteSpace(title)   ? itemLabel : title.Trim();
        string buff2 = string.IsNullOrWhiteSpace(content) ? itemLabel : content.Trim();
        int nowInt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int endInt = nowInt + 30 * 24 * 3600;
        int success = 0, fail = 0;
        await using var db = Open(); await db.OpenAsync();
        for (int i = 0; i < quantity; i++)
        {
            try
            {
                await using var cmd = new MySqlCommand(
                    @"INSERT INTO maildata(type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3)
                      VALUES(1,@cdkey,@buff1,@buff2,@data,@sendtime,@endtime,0,0,'')", db);
                cmd.Parameters.AddWithValue("@cdkey",    account);
                cmd.Parameters.AddWithValue("@buff1",    buff1);
                cmd.Parameters.AddWithValue("@buff2",    buff2);
                cmd.Parameters.AddWithValue("@data",     itemId);
                cmd.Parameters.AddWithValue("@sendtime", nowInt);
                cmd.Parameters.AddWithValue("@endtime",  endInt);
                if (await cmd.ExecuteNonQueryAsync() > 0) success++; else fail++;
            }
            catch { fail++; }
        }
        return (success, fail);
    }

    // ── 發送文字郵件給單一玩家（外部 API 用）──────────────────
    public async Task<bool> SendTextMailAsync(string account, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(account)) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long end = now + 30 * 24 * 3600;
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"INSERT INTO maildata(type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3)
                  VALUES(3,@cdkey,'GM',@title,@content,@now,@end,0,0,'')", db);
            cmd.Parameters.AddWithValue("@cdkey",   account.Trim());
            cmd.Parameters.AddWithValue("@title",   title.Trim());
            cmd.Parameters.AddWithValue("@content", content.Trim());
            cmd.Parameters.AddWithValue("@now",     now);
            cmd.Parameters.AddWithValue("@end",     end);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch { return false; }
    }

    // ── 批量發送郵件 ─────────────────────────────────────────
    public async Task<int> BatchMailAsync(string target, string customList, string title, string content)
    {
        await using var db = Open(); await db.OpenAsync();
        List<string> accounts = new();
        if (target == "custom")
        {
            accounts = customList.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        else
        {
            string whereClause = target == "online" ? "WHERE Online=1" : "";
            await using var cmd2 = new MySqlCommand($"SELECT `Name` FROM csalogin {whereClause}", db);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync()) accounts.Add(r2.GetString(0));
        }
        if (accounts.Count == 0) return 0;

        int sent = 0;
        var now  = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var acc in accounts)
        {
            await using var cmd = new MySqlCommand(
                @"INSERT INTO maildata(type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3)
                  VALUES(3,@cdkey,'GM',@title,@content,@now,@end,0,0,'')", db);
            cmd.Parameters.AddWithValue("@cdkey",   acc);
            cmd.Parameters.AddWithValue("@title",   title);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@now",     now);
            cmd.Parameters.AddWithValue("@end",     now + 86400 * 30);
            try { await cmd.ExecuteNonQueryAsync(); sent++; } catch { }
        }
        return sent;
    }

    // ── 批量購物車發送 ──────────────────────────────────────────
    public async Task<(int count, int fail, List<string> sentAccounts, string lastError)> BatchSendCartAsync(
        string target, string customList, List<CartItem> cart, string title, string content)
    {
        if (cart == null || cart.Count == 0) return (0, 0, new List<string>(), "購物車為空");
        await using var db = Open(); await db.OpenAsync();
        List<string> accounts = new();
        if (target == "custom")
        {
            accounts = customList.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        else
        {
            string wh = target == "online" ? "WHERE Online=1" : "";
            await using var cmd2 = new MySqlCommand($"SELECT `Name` FROM csalogin {wh}", db);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync()) accounts.Add(r2.GetString(0));
        }
        if (accounts.Count == 0) return (0, 0, new List<string>(), "找不到符合條件的玩家帳號");

        int  nowInt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int  endInt = nowInt + 30 * 24 * 3600;
        string tf   = title.Trim();
        string cf   = content.Trim();
        int totalSent = 0, totalFail = 0;
        string lastError = "";
        var sentAccounts = new List<string>();

        // 預先整理 cart 資料（與 EXE 完全一致的欄位格式）
        var rows = cart.Where(c => c.ItemId > 0).Select(c =>
        {
            string nm   = string.IsNullOrWhiteSpace(c.Name) ? $"道具#{c.ItemId}" : c.Name;
            string b1   = string.IsNullOrWhiteSpace(tf) ? nm : tf;
            string b2   = string.IsNullOrWhiteSpace(cf) ? nm : cf;
            return new {
                c.ItemId,
                MailType = c.Type > 0 ? c.Type : 1,
                Qty      = Math.Max(1, c.Qty),
                Buff1    = b1.Length > 200 ? b1[..200] : b1,
                Buff2    = b2.Length > 200 ? b2[..200] : b2,
            };
        }).ToList();

        // 每批帳號用一條大 INSERT（與 EXE BatchSendMailAsync 相同策略）
        const int batchSize = 200;
        const string sqlTpl =
            "INSERT INTO maildata(type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3) VALUES ";

        for (int bi = 0; bi < accounts.Count; bi += batchSize)
        {
            var batch = accounts.Skip(bi).Take(batchSize).ToList();
            var valueParts = new List<string>();
            await using var cmd = new MySqlCommand();
            cmd.Connection = db;
            cmd.Parameters.AddWithValue("@sendtime", nowInt);
            cmd.Parameters.AddWithValue("@endtime",  endInt);

            int pIdx = 0;
            foreach (var acc in batch)
            {
                string cpName = $"@ck{pIdx}";
                cmd.Parameters.AddWithValue(cpName, acc);
                foreach (var row in rows)
                {
                    string tpName  = $"@t{pIdx}";
                    string b1Name  = $"@b1_{pIdx}";
                    string b2Name  = $"@b2_{pIdx}";
                    string datName = $"@d{pIdx}";
                    cmd.Parameters.AddWithValue(tpName,  row.MailType);
                    cmd.Parameters.AddWithValue(b1Name,  row.Buff1);
                    cmd.Parameters.AddWithValue(b2Name,  row.Buff2);
                    cmd.Parameters.AddWithValue(datName, row.ItemId);
                    for (int q = 0; q < row.Qty; q++)
                        valueParts.Add($"({tpName},{cpName},{b1Name},{b2Name},{datName},@sendtime,@endtime,0,0,'')");
                    pIdx++;
                }
            }
            cmd.CommandText = sqlTpl + string.Join(",", valueParts);
            try
            {
                int inserted = await cmd.ExecuteNonQueryAsync();
                foreach (var acc in batch) sentAccounts.Add(acc);
                totalSent += inserted;
            }
            catch (Exception ex) { totalFail += batch.Count; lastError = ex.Message; }
        }
        return (totalSent, totalFail, sentAccounts, lastError);
    }

    // ── 交易記錄（tradelog）────────────────────────────────────
    public async Task<List<TradeRecordDto>> GetTradeLogAsync(string q = "", int limit = 500)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<TradeRecordDto>();
        string sql = string.IsNullOrWhiteSpace(q)
            ? $"SELECT * FROM tradelog ORDER BY time DESC LIMIT {Math.Min(limit, 500)}"
            : @"SELECT * FROM tradelog WHERE mecdkey LIKE @kw OR mename LIKE @kw OR tocdkey LIKE @kw OR toname LIKE @kw ORDER BY time DESC LIMIT @lim";
        await using var cmd = new MySqlCommand(sql, db);
        if (!string.IsNullOrWhiteSpace(q)) { cmd.Parameters.AddWithValue("@kw", $"%{q}%"); cmd.Parameters.AddWithValue("@lim", Math.Min(limit, 500)); }
        try
        {
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new TradeRecordDto
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
        }
        catch { /* tradelog 表不存在時回傳空 */ }
        return list;
    }

    // ── VIP 玩家列表（依 PayTotal 排序）────────────────────────
    public async Task<List<VipRowDto>> GetVipListAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<VipRowDto>();
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                         IFNULL(c.PayTotal,0) payTotal, IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                         (c.Online=1) isOnline,
                         IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                         IFNULL(m.`Name`,'') masterName
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  WHERE IFNULL(c.PayTotal,0) > 0 ORDER BY c.PayTotal DESC LIMIT 500", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                long pt = r.GetInt64("payTotal");
                list.Add(new VipRowDto
                {
                    Account    = r.GetString("account"),
                    OnlineName = r.GetString("onlineName"),
                    MasterName = r.GetString("masterName"),
                    PayTotal   = pt,
                    Gold       = r.GetInt64("gold"),
                    Crystal    = r.GetInt64("crystal"),
                    IsOnline   = r.GetBoolean("isOnline"),
                    LoginTime  = r.GetString("loginTime"),
                    VipLevel   = pt >= 15000 ? 2 : pt >= 5000 ? 1 : 0,
                });
            }
        }
        catch { }
        return list;
    }

    // ── 回收桶 ─────────────────────────────────────────────────
    public async Task<List<RecycleEntryDto>> GetRecycleBinAsync()
    {
        var list = new List<RecycleEntryDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "SELECT recycle_id, deleted_at, deleted_by, original_data FROM csalogin_recycle ORDER BY deleted_at DESC LIMIT 200", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var json = r["original_data"]?.ToString() ?? "{}";
                string acc = "", name = "", master = "";
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Name", out var n)) acc = n.GetString() ?? "";
                    if (root.TryGetProperty("OnlineName", out var o)) name = o.GetString() ?? "";
                    if (root.TryGetProperty("MasterId", out var m)) master = m.GetString() ?? "";
                }
                catch { }
                list.Add(new RecycleEntryDto
                {
                    RecycleId  = r.GetInt32("recycle_id"),
                    DeletedAt  = r["deleted_at"] is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm") : "",
                    DeletedBy  = r["deleted_by"]?.ToString() ?? "",
                    Account    = acc,
                    OnlineName = name,
                    MasterName = master,
                });
            }
        }
        catch { }
        return list;
    }

    public async Task<(bool ok, string msg)> RestoreFromRecycleAsync(int recycleId)
    {
        await using var db = Open(); await db.OpenAsync();
        try
        {
            string json = null;
            await using (var sel = new MySqlCommand("SELECT original_data FROM csalogin_recycle WHERE recycle_id=@id", db))
            {
                sel.Parameters.AddWithValue("@id", recycleId);
                var o = await sel.ExecuteScalarAsync();
                if (o != null && o != DBNull.Value) json = o.ToString();
            }
            if (string.IsNullOrEmpty(json)) return (false, "找不到備份資料。");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            string accName = root.TryGetProperty("Name", out var np) ? np.GetString() : null;
            if (!string.IsNullOrEmpty(accName))
            {
                await using var chk = new MySqlCommand("SELECT COUNT(*) FROM csalogin WHERE `Name`=@n", db);
                chk.Parameters.AddWithValue("@n", accName);
                var cnt = Convert.ToInt64(await chk.ExecuteScalarAsync());
                if (cnt > 0) return (false, $"帳號 {accName} 已存在，無法還原。");
            }
            var cols = new List<string>();
            var vals = new List<string>();
            var parms = new List<MySqlParameter>();
            int idx = 0;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name.Equals("id", StringComparison.OrdinalIgnoreCase)) continue;
                string pname = $"@p{idx++}";
                cols.Add($"`{prop.Name}`");
                vals.Add(pname);
                string rawStr = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? null : prop.Value.GetString();
                parms.Add(new MySqlParameter(pname, rawStr ?? (object)DBNull.Value));
            }
            var insertSql = $"INSERT INTO csalogin ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})";
            await using var ins = new MySqlCommand(insertSql, db);
            foreach (var p in parms) ins.Parameters.Add(p);
            await ins.ExecuteNonQueryAsync();
            await using var del = new MySqlCommand("DELETE FROM csalogin_recycle WHERE recycle_id=@id", db);
            del.Parameters.AddWithValue("@id", recycleId);
            await del.ExecuteNonQueryAsync();
            return (true, "已還原。");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── 批量金幣 ───────────────────────────────────────────────
    public async Task<(int done, int fail)> BatchGoldAsync(string target, string customList, string accountIds, long amount)
    {
        await using var db = Open(); await db.OpenAsync();
        List<string> accounts = new();
        if (!string.IsNullOrWhiteSpace(accountIds))
            accounts = accountIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (accounts.Count == 0)
        {
            if (target == "custom")
                accounts = customList.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            else
            {
                string where = target == "online" ? "WHERE Online=1" : "";
                await using var cmd = new MySqlCommand($"SELECT `Name` FROM csalogin {where}", db);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) accounts.Add(r.GetString(0));
            }
        }
        if (accounts.Count == 0) return (0, 0);
        int done = 0, fail = 0;
        foreach (var acc in accounts)
        {
            try
            {
                await using var sel = new MySqlCommand("SELECT IFNULL(VipPoint,0) FROM csalogin WHERE `Name`=@a", db);
                sel.Parameters.AddWithValue("@a", acc);
                var cur = await sel.ExecuteScalarAsync();
                long curVal = cur == null || cur == DBNull.Value ? 0 : Convert.ToInt64(cur);
                long newVal = Math.Max(0, curVal + amount);
                await using var upd = new MySqlCommand("UPDATE csalogin SET VipPoint=@v WHERE `Name`=@a", db);
                upd.Parameters.AddWithValue("@v", newVal);
                upd.Parameters.AddWithValue("@a", acc);
                if (await upd.ExecuteNonQueryAsync() > 0) done++; else fail++;
            }
            catch { fail++; }
        }
        return (done, fail);
    }

    // ── GM 工具帳號（admin_users）────────────────────────────────
    public async Task<List<AdminUserDto>> GetAdminUsersAsync()
    {
        var list = new List<AdminUserDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                "SELECT id, username, nickname, status, created_at FROM admin_users ORDER BY id", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new AdminUserDto
                {
                    Id        = r.GetInt32("id"),
                    Username  = r["username"]?.ToString() ?? "",
                    Nickname  = r["nickname"]?.ToString() ?? "",
                    IsEnabled = r["status"] != DBNull.Value && Convert.ToInt32(r["status"]) != 0,
                    CreatedAt = r["created_at"]?.ToString() ?? "",
                });
        }
        catch { }
        return list;
    }

    public async Task<(bool ok, string msg)> AddAdminUserAsync(string username, string password, string nickname)
    {
        if (string.IsNullOrWhiteSpace(username)) return (false, "帳號不可為空");
        await using var db = Open(); await db.OpenAsync();
        string hash = Convert.ToHexString(
            System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(password ?? ""))).ToLower();
        await using var cmd = new MySqlCommand(
            "INSERT INTO admin_users (username, password, nickname, status) VALUES (@u,@p,@n,1)", db);
        cmd.Parameters.AddWithValue("@u", username.Trim());
        cmd.Parameters.AddWithValue("@p", hash);
        cmd.Parameters.AddWithValue("@n", (nickname ?? "").Trim());
        try { await cmd.ExecuteNonQueryAsync(); return (true, "已新增"); }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<bool> DeleteAdminUserAsync(int id)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand("DELETE FROM admin_users WHERE id=@id AND username<>'admin'", db);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> SetAdminStatusAsync(int id, bool enabled)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand("UPDATE admin_users SET status=@s WHERE id=@id AND username<>'admin'", db);
        cmd.Parameters.AddWithValue("@s", enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> ResetAdminPasswordAsync(int id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) return false;
        await using var db = Open(); await db.OpenAsync();
        string hash = Convert.ToHexString(
            System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(newPassword))).ToLower();
        await using var cmd = new MySqlCommand("UPDATE admin_users SET password=@p WHERE id=@id", db);
        cmd.Parameters.AddWithValue("@p", hash);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // ── 唯讀 SQL（僅允許 SELECT / SHOW / DESCRIBE）─────────────
    public async Task<(bool ok, List<Dictionary<string, object>> rows, string error)> ExecuteReadOnlyQueryAsync(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return (false, new List<Dictionary<string, object>>(), "查詢不可為空");
        var upper = sql.TrimStart().ToUpperInvariant();
        if (!upper.StartsWith("SELECT") && !upper.StartsWith("SHOW") && !upper.StartsWith("DESCRIBE"))
            return (false, new List<Dictionary<string, object>>(), "只允許 SELECT / SHOW / DESCRIBE 查詢");
        var rows = new List<Dictionary<string, object>>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(sql, db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                rows.Add(row);
            }
        }
        catch (Exception ex) { return (false, rows, ex.Message); }
        return (true, rows, "");
    }

    // ── 商城熱賣（與 EXE 一致：vipshop/fameshop/csshopnum/csxsshopnum）────────
    public async Task<(List<ShopItemDto> items, List<ShopSpenderDto> spenders)> GetShopTopItemsAsync(string table, int topN = 20)
    {
        var items = new List<ShopItemDto>(); var spenders = new List<ShopSpenderDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            long rowCount = 0;
            try { await using var cc = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", db); rowCount = Convert.ToInt64(await cc.ExecuteScalarAsync()); } catch { return (items, spenders); }
            if (rowCount == 0) return (items, spenders);

            if (table == "vipshop" || table == "fameshop")
            {
                var sql1 = $@"SELECT itemid, itemname, SUM(itemnum) AS total_qty, COUNT(*) AS order_count, SUM(oldpoint - newpoint) AS total_cost, MAX(time) AS last_time FROM `{table}` GROUP BY itemid, itemname ORDER BY total_qty DESC LIMIT {topN}";
                await using (var cmd = new MySqlCommand(sql1, db))
                await using (var r = await cmd.ExecuteReaderAsync())
                {
                    int rank = 1;
                    while (await r.ReadAsync())
                        items.Add(new ShopItemDto { Rank = rank++, ItemId = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]), ItemName = r["itemname"]?.ToString() ?? "", TotalQty = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]), OrderCount = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"]), TotalCost = r["total_cost"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_cost"]), LastTime = r["last_time"]?.ToString() ?? "" });
                }
                var sql2 = $@"SELECT cdkey, name, SUM(itemnum) AS total_qty, SUM(oldpoint - newpoint) AS total_cost FROM `{table}` GROUP BY cdkey, name ORDER BY total_cost DESC LIMIT {topN}";
                await using (var cmd2 = new MySqlCommand(sql2, db))
                await using (var r2 = await cmd2.ExecuteReaderAsync())
                {
                    int rank = 1;
                    while (await r2.ReadAsync())
                        spenders.Add(new ShopSpenderDto { Rank = rank++, Cdkey = r2["cdkey"]?.ToString() ?? "", Name = r2["name"]?.ToString() ?? "", TotalQty = r2["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r2["total_qty"]), TotalCost = r2["total_cost"] == DBNull.Value ? 0 : Convert.ToInt64(r2["total_cost"]) });
                }
            }
            else if (table == "csshopnum" || table == "csxsshopnum")
            {
                var sql1 = $@"SELECT itemid, SUM(buynum) AS total_qty, COUNT(*) AS order_count FROM `{table}` GROUP BY itemid ORDER BY total_qty DESC LIMIT {topN}";
                await using var cmd = new MySqlCommand(sql1, db);
                await using var r = await cmd.ExecuteReaderAsync();
                int rank = 1;
                while (await r.ReadAsync())
                    items.Add(new ShopItemDto { Rank = rank++, ItemId = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]), ItemName = $"道具 #{r["itemid"]}", TotalQty = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]), OrderCount = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"]) });
            }
        }
        catch { }
        return (items, spenders);
    }

    // ── 儲值趨勢分析（recharge_orders；若無表則回傳空/0）────────
    public async Task<(DateTime[] dates, decimal[] amounts, int[] counts)> GetDailyRechargeAsync(int days = 30)
    {
        var dl = new List<DateTime>(); var al = new List<decimal>(); var cl = new List<int>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($@"SELECT DATE(created_at) AS d, SUM(amount) AS total, COUNT(*) AS cnt FROM recharge_orders WHERE status='completed' AND created_at >= DATE_SUB(CURDATE(), INTERVAL {days} DAY) GROUP BY d ORDER BY d", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { dl.Add(Convert.ToDateTime(r["d"])); al.Add(r["total"] == DBNull.Value ? 0 : Convert.ToDecimal(r["total"])); cl.Add(r["cnt"] == DBNull.Value ? 0 : Convert.ToInt32(r["cnt"])); }
        }
        catch { }
        return (dl.ToArray(), al.ToArray(), cl.ToArray());
    }

    public async Task<(string[] months, decimal[] amounts, int[] counts)> GetMonthlyRechargeAsync()
    {
        var ml = new List<string>(); var al = new List<decimal>(); var cl = new List<int>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(@"SELECT DATE_FORMAT(created_at,'%Y-%m') AS m, SUM(amount) AS total, COUNT(*) AS cnt FROM recharge_orders WHERE status='completed' AND created_at >= DATE_SUB(CURDATE(), INTERVAL 12 MONTH) GROUP BY m ORDER BY m", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { ml.Add(r["m"]?.ToString() ?? ""); al.Add(r["total"] == DBNull.Value ? 0 : Convert.ToDecimal(r["total"])); cl.Add(r["cnt"] == DBNull.Value ? 0 : Convert.ToInt32(r["cnt"])); }
        }
        catch { }
        return (ml.ToArray(), al.ToArray(), cl.ToArray());
    }

    public async Task<Dictionary<string, int>> GetPaymentTierAsync()
    {
        var result = new Dictionary<string, int>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(@"SELECT SUM(CASE WHEN IFNULL(PayTotal,0)=0 THEN 1 ELSE 0 END) AS t0, SUM(CASE WHEN IFNULL(PayTotal,0) BETWEEN 1 AND 99 THEN 1 ELSE 0 END) AS t1, SUM(CASE WHEN IFNULL(PayTotal,0) BETWEEN 100 AND 499 THEN 1 ELSE 0 END) AS t2, SUM(CASE WHEN IFNULL(PayTotal,0) BETWEEN 500 AND 999 THEN 1 ELSE 0 END) AS t3, SUM(CASE WHEN IFNULL(PayTotal,0) BETWEEN 1000 AND 4999 THEN 1 ELSE 0 END) AS t4, SUM(CASE WHEN IFNULL(PayTotal,0)>=5000 THEN 1 ELSE 0 END) AS t5 FROM csalogin", db);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) { result["免費玩家"] = r["t0"] == DBNull.Value ? 0 : Convert.ToInt32(r["t0"]); result["$1-99"] = r["t1"] == DBNull.Value ? 0 : Convert.ToInt32(r["t1"]); result["$100-499"] = r["t2"] == DBNull.Value ? 0 : Convert.ToInt32(r["t2"]); result["$500-999"] = r["t3"] == DBNull.Value ? 0 : Convert.ToInt32(r["t3"]); result["$1000-4999"] = r["t4"] == DBNull.Value ? 0 : Convert.ToInt32(r["t4"]); result["$5000+"] = r["t5"] == DBNull.Value ? 0 : Convert.ToInt32(r["t5"]); }
        }
        catch { }
        return result;
    }

    public async Task<Dictionary<string, int>> GetTimeToFirstPaymentAsync()
    {
        var result = new Dictionary<string, int> { ["當天"] = 0, ["1-3天"] = 0, ["4-7天"] = 0, ["8-30天"] = 0, ["30天以上"] = 0 };
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(@"SELECT DATEDIFF(MIN(o.created_at), c.created_at) AS days_to_first FROM recharge_orders o JOIN csalogin c ON c.`Name`=o.role_name WHERE o.status='completed' AND c.created_at IS NOT NULL GROUP BY o.role_name", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { int d = r["days_to_first"] == DBNull.Value ? 0 : Convert.ToInt32(r["days_to_first"]); if (d <= 0) result["當天"]++; else if (d <= 3) result["1-3天"]++; else if (d <= 7) result["4-7天"]++; else if (d <= 30) result["8-30天"]++; else result["30天以上"]++; }
        }
        catch { }
        return result;
    }

    public async Task<(decimal todayRevenue, decimal monthRevenue, decimal totalRevenue, int payingPlayers)> GetRechargeKpiAsync()
    {
        decimal today = 0, month = 0, total = 0; int paying = 0;
        try
        {
            await using var db = Open(); await db.OpenAsync();
            try { await using var c1 = new MySqlCommand("SELECT IFNULL(SUM(amount),0), IFNULL(COUNT(*),0) FROM recharge_orders WHERE status='completed' AND DATE(created_at)=CURDATE()", db); await using var r1 = await c1.ExecuteReaderAsync(); if (await r1.ReadAsync()) { today = Convert.ToDecimal(r1.GetValue(0)); } } catch { }
            try { await using var c2 = new MySqlCommand("SELECT IFNULL(SUM(amount),0) FROM recharge_orders WHERE status='completed' AND created_at >= DATE_FORMAT(CURDATE(),'%Y-%m-01')", db); var rv = await c2.ExecuteScalarAsync(); month = rv == null || rv == DBNull.Value ? 0 : Convert.ToDecimal(rv); } catch { }
            try { await using var c3 = new MySqlCommand("SELECT IFNULL(SUM(amount),0) FROM recharge_orders WHERE status='completed'", db); var rv = await c3.ExecuteScalarAsync(); total = rv == null || rv == DBNull.Value ? 0 : Convert.ToDecimal(rv); } catch { }
            var tiers = await GetPaymentTierAsync();
            paying = tiers.Values.Sum() - (tiers.TryGetValue("免費玩家", out var f) ? f : 0);
        }
        catch { }
        return (today, month, total, paying);
    }

    // ── 玩家活躍分析 ───────────────────────────────────────────
    public async Task<int[]> GetLoginHourDistributionAsync()
    {
        var result = new int[24];
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT HOUR(LoginTime) AS h, COUNT(*) AS cnt FROM csalogin WHERE LoginTime IS NOT NULL AND LoginTime > '2000-01-01' GROUP BY h", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { int h = Convert.ToInt32(r["h"]); if (h >= 0 && h < 24) result[h] = Convert.ToInt32(r["cnt"]); }
        }
        catch { }
        return result;
    }

    public async Task<int[]> GetLoginWeekdayDistributionAsync()
    {
        var result = new int[7];
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT DAYOFWEEK(LoginTime)-1 AS d, COUNT(*) AS cnt FROM csalogin WHERE LoginTime IS NOT NULL AND LoginTime > '2000-01-01' GROUP BY d", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { int d = Convert.ToInt32(r["d"]); if (d >= 0 && d < 7) result[d] = Convert.ToInt32(r["cnt"]); }
        }
        catch { }
        return result;
    }

    public async Task<(DateTime[] dates, int[] counts)> GetDailyNewAccountsAsync(int days = 30)
    {
        var dateList = new List<DateTime>(); var countList = new List<int>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($"SELECT DATE(created_at) AS d, COUNT(*) AS cnt FROM csalogin WHERE created_at >= DATE_SUB(CURDATE(), INTERVAL {days} DAY) GROUP BY d ORDER BY d", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) { dateList.Add(Convert.ToDateTime(r["d"])); countList.Add(Convert.ToInt32(r["cnt"])); }
        }
        catch { }
        return (dateList.ToArray(), countList.ToArray());
    }

    public async Task<Dictionary<string, (int cohort, int retained, double rate)>> GetRetentionAsync()
    {
        var result = new Dictionary<string, (int, int, double)>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            foreach (var (label, days) in new[] { ("7天", 7), ("14天", 14), ("30天", 30), ("90天", 90) })
            {
                await using var cmd = new MySqlCommand($@"SELECT COUNT(*) AS cohort, SUM(CASE WHEN LoginTime >= DATE_SUB(NOW(), INTERVAL {days} DAY) THEN 1 ELSE 0 END) AS retained FROM csalogin WHERE created_at >= DATE_SUB(CURDATE(), INTERVAL {days} DAY) AND created_at IS NOT NULL", db);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) { int c = r["cohort"] == DBNull.Value ? 0 : Convert.ToInt32(r["cohort"]); int re = r["retained"] == DBNull.Value ? 0 : Convert.ToInt32(r["retained"]); double rt = c > 0 ? (double)re / c * 100 : 0; result[label] = (c, re, rt); }
            }
        }
        catch { }
        return result;
    }

    public async Task<List<InactivePlayerDto>> GetInactivePlayersAsync(int days = 30, int limit = 200)
    {
        var list = new List<InactivePlayerDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($@"SELECT OnlineName, `Name`, LoginTime, DATEDIFF(NOW(), LoginTime) AS days_since FROM csalogin WHERE LoginTime < DATE_SUB(NOW(), INTERVAL {days} DAY) AND LoginTime IS NOT NULL AND LoginTime > '2000-01-01' ORDER BY LoginTime ASC LIMIT {limit}", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new InactivePlayerDto { OnlineName = r["OnlineName"]?.ToString() ?? "", Account = r["Name"]?.ToString() ?? "", LastLogin = r["LoginTime"]?.ToString() ?? "", DaysSince = r["days_since"] == DBNull.Value ? 0 : Convert.ToInt32(r["days_since"]) });
        }
        catch { }
        return list;
    }

    public async Task<int> GetTodayActiveCountAsync() // 今日有登入的玩家數
    {
        try { await using var db = Open(); await db.OpenAsync(); await using var cmd = new MySqlCommand("SELECT COUNT(*) FROM csalogin WHERE DATE(LoginTime)=CURDATE()", db); var v = await cmd.ExecuteScalarAsync(); return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v); } catch { return 0; }
    }

    // ── 交易稽核 ───────────────────────────────────────────────
    public async Task<(int totalTrades, int uniquePairs, int suspiciousPairs, int sameIpPairs)> GetTradeAuditSummaryAsync()
    {
        int total = 0, pairs = 0, suspicious = 0, sameIp = 0;
        try
        {
            await using var db = Open(); await db.OpenAsync();
            var r1 = await new MySqlCommand("SELECT COUNT(*) FROM tradelog", db).ExecuteScalarAsync();
            total = r1 == null || r1 == DBNull.Value ? 0 : Convert.ToInt32(r1);
            var r2 = await new MySqlCommand("SELECT COUNT(DISTINCT CONCAT(mecdkey,'-',tocdkey)) FROM tradelog", db).ExecuteScalarAsync();
            pairs = r2 == null || r2 == DBNull.Value ? 0 : Convert.ToInt32(r2);
            var r3 = await new MySqlCommand("SELECT COUNT(*) FROM (SELECT mecdkey,tocdkey,COUNT(*) c FROM tradelog GROUP BY mecdkey,tocdkey HAVING c>=10) x", db).ExecuteScalarAsync();
            suspicious = r3 == null || r3 == DBNull.Value ? 0 : Convert.ToInt32(r3);
            try { var r4 = await new MySqlCommand(@"SELECT COUNT(*) FROM (SELECT t.mecdkey,t.tocdkey FROM tradelog t JOIN csalogin a ON a.`Name`=t.mecdkey JOIN csalogin b ON b.`Name`=t.tocdkey WHERE a.IP=b.IP AND a.IP IS NOT NULL AND a.IP!='' GROUP BY t.mecdkey,t.tocdkey) x", db).ExecuteScalarAsync(); sameIp = r4 == null || r4 == DBNull.Value ? 0 : Convert.ToInt32(r4); } catch { }
        }
        catch { }
        return (total, pairs, suspicious, sameIp);
    }

    public async Task<List<FrequentPairDto>> GetFrequentTradePairsAsync(int minCount = 10)
    {
        var list = new List<FrequentPairDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($@"SELECT mecdkey, mename, tocdkey, toname, COUNT(*) AS cnt, MAX(FROM_UNIXTIME(time)) AS last_time FROM tradelog GROUP BY mecdkey, tocdkey HAVING cnt >= {minCount} ORDER BY cnt DESC LIMIT 100", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new FrequentPairDto { FromAccount = r["mecdkey"]?.ToString() ?? "", FromName = r["mename"]?.ToString() ?? "", ToAccount = r["tocdkey"]?.ToString() ?? "", ToName = r["toname"]?.ToString() ?? "", Count = Convert.ToInt32(r["cnt"]), LastTime = r["last_time"]?.ToString() ?? "" });
        }
        catch { }
        return list;
    }

    public async Task<List<SameIpTradeDto>> GetSameIpTradesAsync(int minCount = 5)
    {
        var list = new List<SameIpTradeDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($@"SELECT t.mecdkey, t.tocdkey, COUNT(*) AS cnt, a.IP AS shared_ip FROM tradelog t JOIN csalogin a ON a.`Name`=t.mecdkey JOIN csalogin b ON b.`Name`=t.tocdkey WHERE a.IP=b.IP AND a.IP IS NOT NULL AND a.IP!='' GROUP BY t.mecdkey, t.tocdkey, a.IP HAVING cnt >= {minCount} ORDER BY cnt DESC LIMIT 100", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new SameIpTradeDto { FromAccount = r["mecdkey"]?.ToString() ?? "", ToAccount = r["tocdkey"]?.ToString() ?? "", Count = Convert.ToInt32(r["cnt"]), SharedIp = r["shared_ip"]?.ToString() ?? "" });
        }
        catch { }
        return list;
    }

    public async Task<List<GoldAnomalyDto>> GetGoldAnomalyAsync(int limit = 50)
    {
        var list = new List<GoldAnomalyDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($@"SELECT g.cdkey, IFNULL(c.OnlineName,'') AS cname, SUM(CASE WHEN g.point>0 THEN g.point ELSE 0 END) AS gain, SUM(CASE WHEN g.point<0 THEN ABS(g.point) ELSE 0 END) AS loss, COUNT(*) AS entries FROM goldlog g LEFT JOIN csalogin c ON c.`Name`=g.cdkey GROUP BY g.cdkey ORDER BY gain DESC LIMIT {limit}", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new GoldAnomalyDto { Account = r["cdkey"]?.ToString() ?? "", Name = r["cname"]?.ToString() ?? "", TotalGain = r["gain"] == DBNull.Value ? 0 : Convert.ToInt64(r["gain"]), TotalLoss = r["loss"] == DBNull.Value ? 0 : Convert.ToInt64(r["loss"]), Entries = r["entries"] == DBNull.Value ? 0 : Convert.ToInt32(r["entries"]) });
        }
        catch { }
        return list;
    }

    public async Task<List<TopTraderDto>> GetTopTradersAsync(int limit = 50)
    {
        var list = new List<TopTraderDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand($@"SELECT mecdkey, mename, COUNT(*) AS cnt, MAX(FROM_UNIXTIME(time)) AS last_time FROM tradelog GROUP BY mecdkey ORDER BY cnt DESC LIMIT {limit}", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new TopTraderDto { Account = r["mecdkey"]?.ToString() ?? "", Name = r["mename"]?.ToString() ?? "", TradeCount = Convert.ToInt32(r["cnt"]), LastTime = r["last_time"]?.ToString() ?? "" });
        }
        catch { }
        return list;
    }

    // ── GM 權限（NeiCe / GroupId）──────────────────────────────
    public async Task<List<GmPermDto>> GetGmPermListAsync(string search = "")
    {
        var list = new List<GmPermDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            string sql = string.IsNullOrWhiteSpace(search) ? @"SELECT `Name`, OnlineName, GroupId, NeiCe, Online FROM csalogin ORDER BY NeiCe DESC, GroupId DESC, LoginTime DESC LIMIT 1000" : @"SELECT `Name`, OnlineName, GroupId, NeiCe, Online FROM csalogin WHERE OnlineName LIKE @q OR `Name` LIKE @q ORDER BY NeiCe DESC, GroupId DESC LIMIT 500";
            await using var cmd = new MySqlCommand(sql, db);
            if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@q", $"%{search}%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(new GmPermDto { Account = r["Name"]?.ToString() ?? "", OnlineName = r["OnlineName"]?.ToString() ?? "", GroupId = r["GroupId"] == DBNull.Value ? 0 : Convert.ToInt32(r["GroupId"]), NeiCe = r["NeiCe"] == DBNull.Value ? 0 : Convert.ToInt32(r["NeiCe"]), IsOnline = r["Online"] != DBNull.Value && Convert.ToInt32(r["Online"]) == 1 });
        }
        catch { }
        return list;
    }

    public async Task<bool> SetPlayerPermAsync(string account, int neiCe, int groupId)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand("UPDATE csalogin SET NeiCe=@nc, GroupId=@gid WHERE `Name`=@n", db);
            cmd.Parameters.AddWithValue("@nc", neiCe); cmd.Parameters.AddWithValue("@gid", groupId); cmd.Parameters.AddWithValue("@n", account);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch { return false; }
    }

    // ── 備份（與 EXE 一致：csalogin + lock，INSERT IGNORE）────────────
    private static string SqlLiteral(object v)
    {
        if (v == null || v == DBNull.Value) return "NULL";
        if (v is string s) return "'" + s.Replace("\\", "\\\\").Replace("'", "''") + "'";
        if (v is DateTime dt) return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "'";
        if (v is bool b) return b ? "1" : "0";
        return v.ToString() ?? "NULL";
    }

    public async Task<(string sql, int rows)> GetBackupSqlAsync()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-- 軒打石器 GM 資料庫備份");
        sb.AppendLine($"-- 備份時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("-- 使用 INSERT IGNORE：還原時不覆蓋現有資料");
        sb.AppendLine();
        int totalRows = 0;
        await using var db = Open(); await db.OpenAsync();

        await using (var cmd = new MySqlCommand("SELECT * FROM csalogin", db))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            var cols = new List<string>();
            for (int i = 0; i < r.FieldCount; i++) cols.Add(r.GetName(i));
            string colList = string.Join(", ", cols.Select(c => $"`{c}`"));
            while (await r.ReadAsync())
            {
                var vals = new List<string>();
                for (int i = 0; i < r.FieldCount; i++) vals.Add(SqlLiteral(r.GetValue(i)));
                sb.AppendLine($"INSERT IGNORE INTO `csalogin` ({colList}) VALUES ({string.Join(", ", vals)});");
                totalRows++;
            }
        }
        sb.AppendLine();
        try
        {
            await using var cmd2 = new MySqlCommand("SELECT * FROM `lock`", db);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            var cols2 = new List<string>();
            for (int i = 0; i < r2.FieldCount; i++) cols2.Add(r2.GetName(i));
            string colList2 = string.Join(", ", cols2.Select(c => $"`{c}`"));
            while (await r2.ReadAsync())
            {
                var vals2 = new List<string>();
                for (int i = 0; i < r2.FieldCount; i++) vals2.Add(SqlLiteral(r2.GetValue(i)));
                sb.AppendLine($"INSERT IGNORE INTO `lock` ({colList2}) VALUES ({string.Join(", ", vals2)});");
                totalRows++;
            }
        }
        catch { }
        return (sb.ToString(), totalRows);
    }

    // ── 修正舊版網頁發送的郵件（buff1/buff2 使用固定 GM 文字）──
    public async Task<(int fixed_, int total)> FixOldWebMailsAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        // 先統計
        string where = string.IsNullOrWhiteSpace(account)
            ? "WHERE `check`=0 AND deleamill=0 AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%' OR buff1='[GM] 批量發送')"
            : "WHERE `check`=0 AND deleamill=0 AND cdkey=@acc AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%' OR buff1='[GM] 批量發送')";
        await using var cnt = new MySqlCommand($"SELECT COUNT(*) FROM maildata {where}", db);
        if (!string.IsNullOrWhiteSpace(account)) cnt.Parameters.AddWithValue("@acc", account);
        int total = Convert.ToInt32(await cnt.ExecuteScalarAsync());
        if (total == 0) return (0, 0);
        // 修正：把 buff1/buff2 改成 "道具#data"（以 data 欄位值作為道具名稱）
        string upd = string.IsNullOrWhiteSpace(account)
            ? "UPDATE maildata SET buff1=CONCAT('道具#',data), buff2=CONCAT('道具#',data) WHERE `check`=0 AND deleamill=0 AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%' OR buff1='[GM] 批量發送')"
            : "UPDATE maildata SET buff1=CONCAT('道具#',data), buff2=CONCAT('道具#',data) WHERE `check`=0 AND deleamill=0 AND cdkey=@acc2 AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%' OR buff1='[GM] 批量發送')";
        await using var fix = new MySqlCommand(upd, db);
        if (!string.IsNullOrWhiteSpace(account)) fix.Parameters.AddWithValue("@acc2", account);
        int fixed_ = await fix.ExecuteNonQueryAsync();
        return (fixed_, total);
    }

    // ── maildata 完整欄位診斷（SELECT *）──────────────────────
    public async Task<List<Dictionary<string, string>>> GetMailFullAsync(string account, int limit = 20)
    {
        var result = new List<Dictionary<string, string>>();
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "SELECT * FROM maildata WHERE cdkey=@acc ORDER BY id DESC LIMIT @lim", db);
        cmd.Parameters.AddWithValue("@acc", account);
        cmd.Parameters.AddWithValue("@lim", Math.Min(limit, 50));
        try
        {
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? "(null)" : r.GetValue(i)?.ToString() ?? "";
                result.Add(row);
            }
        }
        catch (Exception ex) { result.Add(new Dictionary<string, string> { ["error"] = ex.Message }); }
        return result;
    }

    // ── maildata 表欄位定義 ────────────────────────────────────
    public async Task<List<Dictionary<string, string>>> GetMaildataSchemaAsync()
    {
        var result = new List<Dictionary<string, string>>();
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand("DESCRIBE maildata", db);
        try
        {
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? "" : r.GetValue(i)?.ToString() ?? "";
                result.Add(row);
            }
        }
        catch (Exception ex) { result.Add(new Dictionary<string, string> { ["error"] = ex.Message }); }
        return result;
    }

    private static long TryGetInt64(MySqlDataReader r, string col) { try { int o = r.GetOrdinal(col); return r.IsDBNull(o) ? 0 : r.GetInt64(o); } catch { return 0; } }
    private static string TryGetString(MySqlDataReader r, string col) { try { int o = r.GetOrdinal(col); return r.IsDBNull(o) ? "" : r.GetString(o); } catch { return ""; } }
    private static int TryGetInt32(MySqlDataReader r, string col) { try { int o = r.GetOrdinal(col); return r.IsDBNull(o) ? 0 : r.GetInt32(o); } catch { return 0; } }

    private static PlayerRow MapRow(MySqlDataReader r)
    {
        long payTotal = TryGetInt64(r, "payTotal");
        return new()
        {
            Account    = r.GetString("account"),
            OnlineName = r.GetString("onlineName"),
            IsOnline   = r.GetBoolean("isOnline"),
            ServerId   = TryGetInt32(r, "serverId"),
            RegTime    = TryGetString(r, "regTime"),
            LoginTime  = TryGetString(r, "loginTime"),
            IP         = TryGetString(r, "ip"),
            IsBanned   = r.GetBoolean("isBanned"),
            Gold       = TryGetInt64(r, "gold"),
            Crystal    = TryGetInt64(r, "crystal"),
            PetCount   = TryGetInt32(r, "petCount"),
            PayTotal   = payTotal,
            MasterName = TryGetString(r, "masterName"),
            VipLevel   = payTotal >= 15000 ? 2 : payTotal >= 5000 ? 1 : 0,
        };
    }

    // ── 信件原始診斷（用於排查無法領取的道具）────────────────
    public async Task<List<MailRawDto>> GetMailRawAsync(string account, int limit = 50)
    {
        var list = new List<MailRawDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"SELECT id, type, cdkey,
                         IFNULL(buff1,'') buff1, IFNULL(buff2,'') buff2,
                         IFNULL(data,'')  rawData,
                         IFNULL(buff3,'') buff3,
                         sendtime, endtime,
                         IFNULL(`check`,0) isRead,
                         IFNULL(deleamill,0) deleted
                  FROM maildata
                  WHERE cdkey=@acc
                  ORDER BY id DESC LIMIT @lim", db);
            cmd.Parameters.AddWithValue("@acc", account);
            cmd.Parameters.AddWithValue("@lim", Math.Min(limit, 200));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new MailRawDto
                {
                    Id       = r.GetInt32("id"),
                    Type     = r.GetInt32("type"),
                    Buff1    = r.GetString("buff1"),
                    Buff2    = r.GetString("buff2"),
                    RawData  = r.GetString("rawData"),
                    Buff3    = r.GetString("buff3"),
                    SendTime = DateTimeOffset.FromUnixTimeSeconds(
                                   r["sendtime"] == DBNull.Value ? 0 : Convert.ToInt64(r["sendtime"]))
                               .LocalDateTime.ToString("MM-dd HH:mm"),
                    IsRead   = r.GetInt32("isRead") == 1,
                    Deleted  = r.GetInt32("deleted") == 1,
                });
        }
        catch { }
        return list;
    }

    // ── 玩家郵件歷史（已收道具）──────────────────────────────
    public async Task<List<MailHistoryDto>> GetPlayerMailHistoryAsync(string account)
    {
        var list = new List<MailHistoryDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"SELECT id, buff1 title, buff2 body, data itemData,
                         FROM_UNIXTIME(sendtime,'%Y-%m-%d %H:%i') sendTime,
                         IFNULL(`check`,0) isRead
                  FROM maildata WHERE cdkey=@acc AND type=1
                  ORDER BY id DESC LIMIT 100", db);
            cmd.Parameters.AddWithValue("@acc", account);
            await using var rr = await cmd.ExecuteReaderAsync();
            while (await rr.ReadAsync())
            {
                int.TryParse(rr["itemData"]?.ToString(), out int itemId);
                list.Add(new MailHistoryDto
                {
                    MailId   = rr.GetInt32("id"),
                    ItemId   = itemId,
                    ItemName = rr.GetString("title"),
                    Quantity = 1,
                    SendTime = rr.IsDBNull(rr.GetOrdinal("sendTime")) ? "" : rr.GetString("sendTime"),
                    IsRead   = rr.GetInt32("isRead") == 1,
                });
            }
        }
        catch { }
        return list;
    }

    // ── 多道具購物車發送（單人）─────────────────────────────────────
    public async Task<(int success, int fail)> SendCartMailAsync(string account, List<CartItem> cart, string title, string content)
    {
        if (cart == null || cart.Count == 0) return (0, 0);
        // 與 EXE 完全一致：用 (int) 型別的 unix timestamp
        int nowInt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int endInt = nowInt + 30 * 24 * 3600;
        string tf  = title.Trim();
        string cf  = content.Trim();
        int success = 0, fail = 0;
        await using var db = Open(); await db.OpenAsync();

        // 用單一大 INSERT 提升速度（與 EXE BatchSendMailAsync 相同策略）
        var valueParts = new List<string>();
        await using var cmd = new MySqlCommand();
        cmd.Connection = db;
        cmd.Parameters.AddWithValue("@cdkey",    account);
        cmd.Parameters.AddWithValue("@sendtime", nowInt);
        cmd.Parameters.AddWithValue("@endtime",  endInt);

        int pIdx = 0;
        foreach (var item in cart.Where(c => c.ItemId > 0))
        {
            string nm    = string.IsNullOrWhiteSpace(item.Name) ? $"道具#{item.ItemId}" : item.Name;
            string buff1 = string.IsNullOrWhiteSpace(tf) ? nm : tf;
            string buff2 = string.IsNullOrWhiteSpace(cf) ? nm : cf;
            if (buff1.Length > 200) buff1 = buff1[..200];
            if (buff2.Length > 200) buff2 = buff2[..200];
            int mailType = item.Type > 0 ? item.Type : 1;
            string tpName  = $"@t{pIdx}";
            string b1Name  = $"@b1_{pIdx}";
            string b2Name  = $"@b2_{pIdx}";
            string datName = $"@d{pIdx}";
            cmd.Parameters.AddWithValue(tpName,  mailType);
            cmd.Parameters.AddWithValue(b1Name,  buff1);
            cmd.Parameters.AddWithValue(b2Name,  buff2);
            cmd.Parameters.AddWithValue(datName, item.ItemId);
            for (int q = 0; q < Math.Max(1, item.Qty); q++)
                valueParts.Add($"({tpName},@cdkey,{b1Name},{b2Name},{datName},@sendtime,@endtime,0,0,'')");
            pIdx++;
        }
        if (valueParts.Count == 0) return (0, 0);
        cmd.CommandText =
            "INSERT INTO maildata(type,cdkey,buff1,buff2,data,sendtime,endtime,`check`,deleamill,buff3) VALUES "
            + string.Join(",", valueParts);
        try { success = await cmd.ExecuteNonQueryAsync(); }
        catch { fail = cart.Sum(c => Math.Max(1, c.Qty)); }
        return (success, fail);
    }

    // ── 封號清單搜尋 ─────────────────────────────────────────
    public async Task<List<object>> GetBannedListAsync(string kw = "")
    {
        await using var db = Open(); await db.OpenAsync();
        string where = string.IsNullOrWhiteSpace(kw) ? "" :
            " AND (l.`Name` LIKE @q OR IFNULL(c.OnlineName,'') LIKE @q)";
        await using var cmd = new MySqlCommand(
            $@"SELECT l.`Name` account, IFNULL(c.OnlineName,'') charName, l.`time` banTime
               FROM `lock` l
               LEFT JOIN csalogin c ON c.`Name`=l.`Name`
               WHERE 1=1 {where}
               ORDER BY l.`time` ASC", db);
        if (!string.IsNullOrWhiteSpace(kw))
            cmd.Parameters.AddWithValue("@q", $"%{kw}%");
        var list = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            long t = r.GetInt64("banTime");
            list.Add(new {
                account     = r.GetString("account"),
                charName    = r.GetString("charName"),
                isPermanent = t == 0,
                endTime     = t == 0 ? "\u6C38\u4E45" :
                    DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            });
        }
        return list;
    }

    // ── 玩家活動歷程 ──────────────────────────────────────────────
    public async Task<PlayerHistoryResult> GetPlayerHistoryAsync(string account, int limit = 100)
    {
        await using var db = Open(); await db.OpenAsync();
        var result = new PlayerHistoryResult();

        // ── 交易紀錄（送出 + 收到）────────────────────────────────
        await using var cmd1 = new MySqlCommand(
            @"SELECT mecdkey,mename,tocdkey,toname,
                     DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time,
                     item, pet, gold
              FROM tradelog
              WHERE mecdkey=@a OR tocdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd1.Parameters.AddWithValue("@a", account);
        cmd1.Parameters.AddWithValue("@lim", limit);
        await using var r1 = await cmd1.ExecuteReaderAsync();
        while (await r1.ReadAsync())
        {
            var from = r1.GetString("mecdkey");
            result.Trades.Add(new TradeLogDto
            {
                Time      = r1.GetString("time"),
                FromCdkey = from,
                FromName  = r1.GetString("mename"),
                ToCdkey   = r1.GetString("tocdkey"),
                ToName    = r1.GetString("toname"),
                Items     = r1.IsDBNull(r1.GetOrdinal("item")) ? "" : r1.GetString("item"),
                Pets      = r1.IsDBNull(r1.GetOrdinal("pet"))  ? "" : r1.GetString("pet"),
                Gold      = r1.GetInt64("gold"),
                Direction = from == account ? "sent" : "received",
            });
        }
        await r1.CloseAsync();
        result.TradeSent     = result.Trades.Count(t => t.Direction == "sent");
        result.TradeReceived = result.Trades.Count(t => t.Direction == "received");

        // ── 街頭商店買賣 ──────────────────────────────────────────
        await using var cmd2 = new MySqlCommand(
            @"SELECT sellcdkey, type, name, num, point, buycdkey, buyname,
                     FROM_UNIXTIME(time,'%Y-%m-%d %H:%i:%S') time
              FROM streetlog
              WHERE sellcdkey=@a OR buycdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd2.Parameters.AddWithValue("@a", account);
        cmd2.Parameters.AddWithValue("@lim", limit);
        await using var r2 = await cmd2.ExecuteReaderAsync();
        while (await r2.ReadAsync())
        {
            result.Street.Add(new StreetLogDto
            {
                Time      = r2.GetString("time"),
                SellCdkey = r2.GetString("sellcdkey"),
                BuyCdkey  = r2.IsDBNull(r2.GetOrdinal("buycdkey")) ? "" : r2.GetString("buycdkey"),
                BuyName   = r2.IsDBNull(r2.GetOrdinal("buyname"))  ? "" : r2.GetString("buyname"),
                ItemName  = r2.GetString("name"),
                Num       = r2.GetInt32("num"),
                Price     = r2.GetInt32("point"),
                Type      = r2.GetInt32("type"),
                Role      = r2.GetString("sellcdkey") == account ? "seller" : "buyer",
            });
        }
        await r2.CloseAsync();

        // ── 速度異常偵測 ──────────────────────────────────────────
        await using var cmd3 = new MySqlCommand(
            @"SELECT speedtime, speedcnt,
                     DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
              FROM speedlog WHERE cdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd3.Parameters.AddWithValue("@a", account);
        cmd3.Parameters.AddWithValue("@lim", 50);
        await using var r3 = await cmd3.ExecuteReaderAsync();
        while (await r3.ReadAsync())
        {
            result.Speed.Add(new SpeedLogDto
            {
                Time      = r3.GetString("time"),
                SpeedTime = r3.GetInt32("speedtime"),
                SpeedCnt  = r3.GetInt32("speedcnt"),
            });
        }
        await r3.CloseAsync();

        // ── 消費紀錄 ──────────────────────────────────────────────
        await using var cmd4 = new MySqlCommand(
            @"SELECT cdkey, name, point, `check`,
                     DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
              FROM costdata WHERE cdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd4.Parameters.AddWithValue("@a", account);
        cmd4.Parameters.AddWithValue("@lim", limit);
        await using var r4 = await cmd4.ExecuteReaderAsync();
        while (await r4.ReadAsync())
        {
            result.Cost.Add(new CostLogDto
            {
                Time  = r4.GetString("time"),
                Name  = r4.IsDBNull(r4.GetOrdinal("name")) ? "" : r4.GetString("name"),
                Point = r4.GetInt64("point"),
                Check = r4.GetInt32("check"),
            });
        }
        await r4.CloseAsync();

        // ── 聲望商城購買紀錄 ──────────────────────────────────────
        await using var cmd5 = new MySqlCommand(
            @"SELECT cdkey, name, itemid, itemname, itemnum, oldpoint, newpoint,
                     DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
              FROM fameshop WHERE cdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd5.Parameters.AddWithValue("@a", account);
        cmd5.Parameters.AddWithValue("@lim", limit);
        await using var r5 = await cmd5.ExecuteReaderAsync();
        while (await r5.ReadAsync())
        {
            result.ShopLogs.Add(new ShopLogDto
            {
                Time      = r5.GetString("time"),
                CharName  = r5.IsDBNull(r5.GetOrdinal("name")) ? "" : r5.GetString("name"),
                ItemId    = r5.GetInt32("itemid"),
                ItemName  = r5.IsDBNull(r5.GetOrdinal("itemname")) ? "" : r5.GetString("itemname"),
                ItemNum   = r5.GetInt32("itemnum"),
                OldPoint  = r5.GetInt32("oldpoint"),
                NewPoint  = r5.GetInt32("newpoint"),
                ShopType  = "fame",
            });
        }
        await r5.CloseAsync();

        // ── VIP 商城購買紀錄 ──────────────────────────────────────
        await using var cmd6 = new MySqlCommand(
            @"SELECT cdkey, name, itemid, itemname, itemnum, oldpoint, newpoint,
                     DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
              FROM vipshop WHERE cdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd6.Parameters.AddWithValue("@a", account);
        cmd6.Parameters.AddWithValue("@lim", limit);
        await using var r6 = await cmd6.ExecuteReaderAsync();
        while (await r6.ReadAsync())
        {
            result.ShopLogs.Add(new ShopLogDto
            {
                Time      = r6.GetString("time"),
                CharName  = r6.IsDBNull(r6.GetOrdinal("name")) ? "" : r6.GetString("name"),
                ItemId    = r6.GetInt32("itemid"),
                ItemName  = r6.IsDBNull(r6.GetOrdinal("itemname")) ? "" : r6.GetString("itemname"),
                ItemNum   = r6.GetInt32("itemnum"),
                OldPoint  = r6.GetInt32("oldpoint"),
                NewPoint  = r6.GetInt32("newpoint"),
                ShopType  = "vip",
            });
        }
        await r6.CloseAsync();
        // 依時間排序
        result.ShopLogs.Sort((a, b) => string.Compare(b.Time, a.Time, StringComparison.Ordinal));

        // ── VIP 點數增減紀錄 ──────────────────────────────────────
        await using var cmd7 = new MySqlCommand(
            @"SELECT point, oldpoint, newpoint, buff,
                     DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
              FROM vippointlog WHERE cdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd7.Parameters.AddWithValue("@a", account);
        cmd7.Parameters.AddWithValue("@lim", limit);
        await using var r7 = await cmd7.ExecuteReaderAsync();
        while (await r7.ReadAsync())
        {
            result.VipPointLog.Add(new VipPointLogDto
            {
                Time     = r7.GetString("time"),
                Point    = r7.GetInt32("point"),
                OldPoint = r7.GetInt32("oldpoint"),
                NewPoint = r7.GetInt32("newpoint"),
                Buff     = r7.IsDBNull(r7.GetOrdinal("buff")) ? "" : r7.GetString("buff"),
            });
        }

        return result;
    }

    // ── 取得所有目前有攤位的攤主清單 ──────────────────────────────
    public async Task<List<VendorSummaryDto>> GetAllVendorsAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<VendorSummaryDto>();
        await using var cmd = new MySqlCommand(
            @"SELECT si.cdkey, c.OnlineName, COUNT(*) AS item_count
              FROM streetitem si
              LEFT JOIN csalogin c ON si.cdkey = c.Name
              GROUP BY si.cdkey, c.OnlineName
              ORDER BY item_count DESC", db);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new VendorSummaryDto
            {
                CdKey     = r.GetString("cdkey"),
                CharName  = r.IsDBNull(r.GetOrdinal("OnlineName")) ? "" : r.GetString("OnlineName"),
                ItemCount = (int)r.GetInt64("item_count"),
            });
        }
        return list;
    }

    // ── 攤位查詢（依攤主 cdkey 或角色名）──────────────────────────
    public async Task<StreetVendorResult> GetStreetVendorAsync(string query, int limit = 100)
    {
        await using var db = Open(); await db.OpenAsync();
        var result = new StreetVendorResult();

        // 先解析 query → 取得 cdkey 和角色名
        string cdkey = query, charName = "";
        await using var findCmd = new MySqlCommand(
            @"SELECT Name, OnlineName FROM csalogin
              WHERE Name=@q OR OnlineName=@q LIMIT 1", db);
        findCmd.Parameters.AddWithValue("@q", query);
        await using var findR = await findCmd.ExecuteReaderAsync();
        if (await findR.ReadAsync())
        {
            cdkey    = findR.GetString("Name");
            charName = findR.IsDBNull(findR.GetOrdinal("OnlineName")) ? "" : findR.GetString("OnlineName");
        }
        await findR.CloseAsync();
        result.CdKey    = cdkey;
        result.CharName = charName;

        // 目前上架商品
        await using var cmd1 = new MySqlCommand(
            @"SELECT cdkey, ITEM_ID, ITEM_NAME, ITEM_USEPILENUMS, price
              FROM streetitem WHERE cdkey=@a ORDER BY price", db);
        cmd1.Parameters.AddWithValue("@a", cdkey);
        await using var r1 = await cmd1.ExecuteReaderAsync();
        while (await r1.ReadAsync())
        {
            result.CurrentItems.Add(new StreetItemDto
            {
                CdKey    = r1.GetString("cdkey"),
                ItemId   = r1.GetInt32("ITEM_ID"),
                ItemName = r1.IsDBNull(r1.GetOrdinal("ITEM_NAME")) ? "" : r1.GetString("ITEM_NAME"),
                Num      = r1.GetInt32("ITEM_USEPILENUMS"),
                Price    = r1.GetInt32("price"),
            });
        }
        await r1.CloseAsync();

        // 歷史成交紀錄
        await using var cmd2 = new MySqlCommand(
            @"SELECT sellcdkey, name, num, point, buycdkey, buyname,
                     FROM_UNIXTIME(time,'%Y-%m-%d %H:%i:%S') time
              FROM streetlog WHERE sellcdkey=@a
              ORDER BY time DESC LIMIT @lim", db);
        cmd2.Parameters.AddWithValue("@a", cdkey);
        cmd2.Parameters.AddWithValue("@lim", limit);
        await using var r2 = await cmd2.ExecuteReaderAsync();
        while (await r2.ReadAsync())
        {
            result.SaleHistory.Add(new StreetSaleDto
            {
                Time      = r2.GetString("time"),
                SellCdkey = r2.GetString("sellcdkey"),
                ItemName  = r2.GetString("name"),
                Num       = r2.GetInt32("num"),
                Point     = r2.GetInt32("point"),
                BuyCdkey  = r2.IsDBNull(r2.GetOrdinal("buycdkey")) ? "" : r2.GetString("buycdkey"),
                BuyName   = r2.IsDBNull(r2.GetOrdinal("buyname"))  ? "" : r2.GetString("buyname"),
            });
        }
        return result;
    }

    // ── 目前上架查詢（依物品名稱搜 streetitem）───────────────────
    public async Task<List<StreetListingDto>> GetStreetListingsByItemAsync(string itemName, int limit = 200)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<StreetListingDto>();
        var kw = $"%{itemName}%";
        await using var cmd = new MySqlCommand(
            @"SELECT si.cdkey, c.OnlineName AS charName,
                     si.ITEM_NAME, si.ITEM_USEPILENUMS, si.price
              FROM streetitem si
              LEFT JOIN csalogin c ON c.Name = si.cdkey
              WHERE si.ITEM_NAME LIKE @kw
              ORDER BY si.price ASC LIMIT @lim", db);
        cmd.Parameters.AddWithValue("@kw", kw);
        cmd.Parameters.AddWithValue("@lim", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new StreetListingDto
            {
                CdKey    = r.GetString("cdkey"),
                CharName = r.IsDBNull(r.GetOrdinal("charName")) ? "" : r.GetString("charName"),
                ItemName = r.IsDBNull(r.GetOrdinal("ITEM_NAME")) ? "" : r.GetString("ITEM_NAME"),
                Num      = r.GetInt32("ITEM_USEPILENUMS"),
                Price    = r.GetInt32("price"),
            });
        }
        return list;
    }

    // ── 攤位商品反查（依物品名稱查 streetlog）────────────────────
    public async Task<List<StreetBuyerDto>> GetStreetBuyersAsync(string itemName, int limit = 300)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<StreetBuyerDto>();
        var kw = $"%{itemName}%";
        await using var cmd = new MySqlCommand(
            @"SELECT sl.sellcdkey, cs.OnlineName AS sellerName,
                     sl.buycdkey, sl.buyname,
                     sl.name AS itemName, sl.num, sl.point,
                     FROM_UNIXTIME(sl.time,'%Y-%m-%d %H:%i:%S') AS tradeTime
              FROM streetlog sl
              LEFT JOIN csalogin cs ON cs.Name = sl.sellcdkey
              WHERE sl.name LIKE @kw
              ORDER BY sl.time DESC LIMIT @lim", db);
        cmd.Parameters.AddWithValue("@kw", kw);
        cmd.Parameters.AddWithValue("@lim", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new StreetBuyerDto
            {
                Time       = r.GetString("tradeTime"),
                SellCdkey  = r.GetString("sellcdkey"),
                SellerName = r.IsDBNull(r.GetOrdinal("sellerName")) ? "" : r.GetString("sellerName"),
                BuyCdkey   = r.IsDBNull(r.GetOrdinal("buycdkey"))   ? "" : r.GetString("buycdkey"),
                BuyName    = r.IsDBNull(r.GetOrdinal("buyname"))     ? "" : r.GetString("buyname"),
                ItemName   = r.GetString("itemName"),
                Num        = r.GetInt32("num"),
                Point      = r.GetInt32("point"),
            });
        }
        return list;
    }

    // ── 商城反查（依物品名稱查誰買過）────────────────────────────
    public async Task<List<ShopBuyerDto>> GetShopBuyersAsync(string itemName, int limit = 200)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<ShopBuyerDto>();
        var kw = $"%{itemName}%";

        foreach (var (tbl, shopType) in new[] { ("fameshop", "fame"), ("vipshop", "vip") })
        {
            await using var cmd = new MySqlCommand(
                $@"SELECT cdkey, name, itemid, itemname, itemnum, oldpoint, newpoint,
                          DATE_FORMAT(time,'%Y-%m-%d %H:%i:%S') time
                   FROM `{tbl}` WHERE itemname LIKE @kw
                   ORDER BY time DESC LIMIT @lim", db);
            cmd.Parameters.AddWithValue("@kw", kw);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new ShopBuyerDto
                {
                    Time     = r.GetString("time"),
                    CdKey    = r.GetString("cdkey"),
                    CharName = r.IsDBNull(r.GetOrdinal("name")) ? "" : r.GetString("name"),
                    ItemName = r.IsDBNull(r.GetOrdinal("itemname")) ? "" : r.GetString("itemname"),
                    ItemNum  = r.GetInt32("itemnum"),
                    OldPoint = r.GetInt32("oldpoint"),
                    NewPoint = r.GetInt32("newpoint"),
                    ShopType = shopType,
                });
            }
            await r.CloseAsync();
        }
        list.Sort((a, b) => string.Compare(b.Time, a.Time, StringComparison.Ordinal));
        return list;
    }

}
