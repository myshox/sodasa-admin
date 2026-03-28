using System;
using System.Collections.Generic;
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
                   (SELECT COUNT(*) FROM capturepet p WHERE p.cdkey=c.`Name` OR p.cdkey=c.OnlineName OR (IFNULL(c.uid,'')<>'' AND p.cdkey=c.uid) OR p.author=c.OnlineName OR p.author=c.`Name`) petCount,
                   IFNULL(c.PayTotal,0) payTotal, IFNULL(m.`Name`,'') masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
            WHERE c.`Name` LIKE @kw OR c.OnlineName LIKE @kw OR m.`Name` LIKE @kw
            ORDER BY c.Online DESC, c.LoginTime DESC LIMIT @lim",
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline, IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   (SELECT COUNT(*) FROM capturepet p WHERE p.cdkey=c.`Name` OR p.cdkey=c.OnlineName OR (IFNULL(c.uid,'')<>'' AND p.cdkey=c.uid) OR p.author=c.OnlineName OR p.author=c.`Name`) petCount,
                   IFNULL(c.PayTotal,0) payTotal, '' masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
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
                       (SELECT COUNT(*) FROM capturepet p
                        WHERE p.cdkey=c.`Name` OR p.cdkey=c.OnlineName OR (c.uid<>'' AND p.cdkey=c.uid)
                           OR p.author=c.OnlineName OR p.author=c.`Name`) AS petCount
                FROM csalogin c
                LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
                LEFT JOIN (
                    SELECT receiverid,
                           COUNT(*) AS total,
                           SUM(CASE WHEN isread=0 THEN 1 ELSE 0 END) AS unread
                    FROM maildata GROUP BY receiverid
                ) mail ON mail.receiverid=c.`Name`
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

    // ── 玩家寵物清單（capturepet），cdkey 比對帳號/角色名/uid ───
    public async Task<List<PetInfoDto>> GetPlayerPetsAsync(string account, string? charName = null)
    {
        var list = new List<PetInfoDto>();
        await using var db = Open();
        await db.OpenAsync();

        string uid = "";
        string cname = charName ?? "";
        try
        {
            await using var nc = new MySqlCommand("SELECT OnlineName, uid FROM csalogin WHERE `Name`=@n LIMIT 1", db);
            nc.Parameters.AddWithValue("@n", account);
            await using var nr = await nc.ExecuteReaderAsync();
            if (await nr.ReadAsync())
            {
                if (string.IsNullOrEmpty(cname)) cname = nr["OnlineName"]?.ToString() ?? "";
                uid = nr["uid"]?.ToString() ?? "";
            }
        }
        catch { /* 忽略 */ }

        // 遊戲可能把擁有者存於 cdkey（帳號/角色名/uid）或 author（角色名），皆比對
        const string sql = @"
            SELECT unicode, id, name, type, lv, hp, attack, def, quick, sum, author, cdkey, `check`
            FROM capturepet
            WHERE cdkey=@acc OR cdkey=@cname OR (@uid<>'' AND cdkey=@uid)
               OR author=@cname OR author=@acc
            ORDER BY sum DESC";
        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@acc", account);
        cmd.Parameters.AddWithValue("@cname", cname);
        cmd.Parameters.AddWithValue("@uid", uid);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new PetInfoDto
            {
                Unicode = r["unicode"]?.ToString() ?? "",
                Id      = r["id"] == DBNull.Value ? 0 : Convert.ToInt32(r["id"]),
                Name    = r["name"]?.ToString() ?? "",
                Type    = r["type"]?.ToString() ?? "",
                Lv      = r["lv"] == DBNull.Value ? 0 : Convert.ToInt32(r["lv"]),
                Hp      = r["hp"] == DBNull.Value ? 0 : Convert.ToInt32(r["hp"]),
                Attack  = r["attack"] == DBNull.Value ? 0 : Convert.ToInt32(r["attack"]),
                Def     = r["def"] == DBNull.Value ? 0 : Convert.ToInt32(r["def"]),
                Quick   = r["quick"] == DBNull.Value ? 0 : Convert.ToInt32(r["quick"]),
                Sum     = r["sum"] == DBNull.Value ? 0 : Convert.ToDouble(r["sum"]),
                Author  = r["author"]?.ToString() ?? "",
                Cdkey   = r["cdkey"]?.ToString() ?? "",
                Check   = r["check"] == DBNull.Value ? 0 : Convert.ToInt32(r["check"])
            });
        return list;
    }

    /// <summary>依 unicode 刪除 capturepet 一筆記錄（不可復原）</summary>
    public async Task<bool> DeletePetAsync(string unicode)
    {
        if (string.IsNullOrWhiteSpace(unicode)) return false;
        await using var db = Open();
        await db.OpenAsync();
        await using var cmd = new MySqlCommand("DELETE FROM capturepet WHERE unicode=@uid", db);
        cmd.Parameters.AddWithValue("@uid", unicode);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>診斷：回傳查詢用到的 account/onlineName/uid、匹配到的寵物數、以及 capturepet 最近幾筆的 cdkey/author 供比對</summary>
    public async Task<object> GetPetDiagnoseAsync(string account, string? charName = null)
    {
        string uid = "", cname = charName ?? "";
        await using var db = Open();
        await db.OpenAsync();
        try
        {
            await using var nc = new MySqlCommand("SELECT OnlineName, uid FROM csalogin WHERE `Name`=@n LIMIT 1", db);
            nc.Parameters.AddWithValue("@n", account);
            await using var nr = await nc.ExecuteReaderAsync();
            if (await nr.ReadAsync())
            {
                if (string.IsNullOrEmpty(cname)) cname = nr["OnlineName"]?.ToString() ?? "";
                uid = nr["uid"]?.ToString() ?? "";
            }
        }
        catch { /* ignore */ }

        var pets = await GetPlayerPetsAsync(account, charName);
        var sample = new List<object>();
        var fuzzyByAuthor = new List<object>();
        try
        {
            await using var cmd = new MySqlCommand("SELECT cdkey, author, name, id FROM capturepet ORDER BY id DESC LIMIT 10", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                sample.Add(new
                {
                    cdkey = r["cdkey"]?.ToString() ?? "",
                    author = r["author"]?.ToString() ?? "",
                    name = r["name"]?.ToString() ?? "",
                    id = r["id"] == DBNull.Value ? 0 : Convert.ToInt32(r["id"])
                });
        }
        catch { /* ignore */ }

        // 若精確比對為 0，用 author LIKE '%角色名%' 模糊查，確認是否有編碼/空格差異
        if (pets.Count == 0 && !string.IsNullOrWhiteSpace(cname))
        {
            try
            {
                await using var fc = new MySqlCommand("SELECT cdkey, author, name, id FROM capturepet WHERE author LIKE @pat LIMIT 20", db);
                fc.Parameters.AddWithValue("@pat", "%" + cname + "%");
                await using var fr = await fc.ExecuteReaderAsync();
                while (await fr.ReadAsync())
                    fuzzyByAuthor.Add(new
                    {
                        cdkey = fr["cdkey"]?.ToString() ?? "",
                        author = fr["author"]?.ToString() ?? "",
                        name = fr["name"]?.ToString() ?? "",
                        id = fr["id"] == DBNull.Value ? 0 : Convert.ToInt32(fr["id"])
                    });
            }
            catch { /* ignore */ }
        }

        return new
        {
            account,
            onlineName = cname,
            uid,
            matchedPetCount = pets.Count,
            fuzzyMatchByAuthor = fuzzyByAuthor.Count,
            fuzzyPets = fuzzyByAuthor,
            hint = "查詢條件：cdkey 或 author 符合 account / 角色名(onlineName) / uid 任一個即會列出。若 matchedPetCount 為 0，見 sample 看 cdkey/author 格式；若 fuzzyMatchByAuthor>0 表示有 author 含角色名的筆數（可改為模糊匹配）。",
            sample
        };
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
                   (SELECT COUNT(*) FROM capturepet p WHERE p.cdkey=c.`Name` OR p.cdkey=c.OnlineName OR (IFNULL(c.uid,'')<>'' AND p.cdkey=c.uid) OR p.author=c.OnlineName OR p.author=c.`Name`) petCount,
                   IFNULL(c.PayTotal,0) payTotal, IFNULL(m.`Name`,'') masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
            ORDER BY c.Online DESC, c.LoginTime DESC LIMIT @lim",
            @"SELECT c.`Name` account, IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline, IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip, (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold, IFNULL(c.PetPoint,0) crystal,
                   (SELECT COUNT(*) FROM capturepet p WHERE p.cdkey=c.`Name` OR p.cdkey=c.OnlineName OR (IFNULL(c.uid,'')<>'' AND p.cdkey=c.uid) OR p.author=c.OnlineName OR p.author=c.`Name`) petCount,
                   IFNULL(c.PayTotal,0) payTotal, '' masterName
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
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

        // ── Part 1：recharge_orders（官方訂單）────────────────────────
        DateTime latestOrderTime = DateTime.MinValue;
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT o.order_no,
                         IFNULL(c.OnlineName,'') charName,
                         IFNULL(o.role_name,'') account,
                         IFNULL(o.product_name,'') productName,
                         IFNULL(o.amount,0) yuanbao,
                         ROUND(o.amount/100) twd,
                         IFNULL(o.status,'') status,
                         o.created_at,
                         IFNULL(DATE_FORMAT(o.created_at,'%Y-%m-%d %H:%i'),'') time
                  FROM recharge_orders o
                  LEFT JOIN csalogin c ON c.`Name`=o.role_name
                  WHERE (@q='' OR o.role_name LIKE @q OR IFNULL(c.OnlineName,'') LIKE @q OR IFNULL(o.product_name,'') LIKE @q)
                  ORDER BY o.created_at DESC LIMIT 500", db);
            cmd.Parameters.AddWithValue("@q", string.IsNullOrEmpty(kw) ? "" : $"%{kw}%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(r.GetOrdinal("created_at")))
                {
                    var t = r.GetDateTime("created_at");
                    if (t > latestOrderTime) latestOrderTime = t;
                }
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
            }
        }
        catch { list.Clear(); latestOrderTime = DateTime.MinValue; }

        // ── Part 2：paydata 補充（付費系統直接寫 DB、未進 recharge_orders 的充值）──
        // 顯示比 recharge_orders 最新記錄更近的 paydata 更新
        // 或查詢特定玩家時一律顯示
        try
        {
            bool hasFilter = !string.IsNullOrEmpty(kw);
            // 無過濾條件時：只補充比最新訂單更新的 paydata；
            // 有過濾條件時：直接顯示該玩家所有 paydata（順便比對）
            string timeWhere = (!hasFilter && latestOrderTime != DateTime.MinValue)
                ? "AND p.time > @lat"
                : "";
            await using var cmd = new MySqlCommand(
                $@"SELECT p.cdkey account, IFNULL(c.OnlineName,'') charName,
                          IFNULL(p.lifetime_total, p.point) lifetimeTotal,
                          IFNULL(DATE_FORMAT(p.time,'%Y-%m-%d %H:%i'),'') time
                   FROM paydata p
                   LEFT JOIN csalogin c ON c.`Name`=p.cdkey
                   WHERE p.time IS NOT NULL AND p.lifetime_total > 0
                   AND (@q='' OR p.cdkey LIKE @q OR IFNULL(c.OnlineName,'') LIKE @q)
                   {timeWhere}
                   ORDER BY p.time DESC LIMIT 200", db);
            cmd.Parameters.AddWithValue("@q", string.IsNullOrEmpty(kw) ? "" : $"%{kw}%");
            if (!hasFilter && latestOrderTime != DateTime.MinValue)
                cmd.Parameters.AddWithValue("@lat", latestOrderTime);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                long lt = TryGetInt64(r, "lifetimeTotal");
                list.Add(new {
                    orderNo     = "",
                    account     = r.GetString("account"),
                    charName    = r.GetString("charName"),
                    productName = "充值（付費系統記錄）",
                    yuanbao     = lt * 100,   // lifetime_total 為台幣，×100 估算元寶
                    twd         = lt,
                    status      = "paydata",
                    time        = r.GetString("time"),
                    source      = "paydata"
                });
            }
        }
        catch { }

        // 按時間排序（orders 在前，paydata 補充緊接在後）
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
        // 消費達成獎勵：costdata
        long costPoint = 0;
        int costCheck = -1;
        try
        {
            await using var cmdC = new MySqlCommand(
                "SELECT point, IFNULL(`check`,0) ck FROM costdata WHERE cdkey=@acc ORDER BY time DESC LIMIT 1", db);
            cmdC.Parameters.AddWithValue("@acc", account);
            await using var rC = await cmdC.ExecuteReaderAsync();
            if (await rC.ReadAsync())
            {
                costPoint = rC.IsDBNull(0) ? 0 : rC.GetInt64(0);
                costCheck = rC.IsDBNull(1) ? 0 : rC.GetInt32(1);
            }
        }
        catch { }
        return new { account, onlineName, masterName, isOnline, gold, crystal, payTotal,
                     paydataPoint = point, totalCheck = tc, lifetimeTotal = lt,
                     paydataCheck = checkVal, claimReady,
                     vipLevel = payTotal >= 15000 ? 2 : payTotal >= 5000 ? 1 : 0,
                     costPoint, costCheck };
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

    /// <summary>
    /// 寫入充值記錄到 recharge_orders（供 GM 補單 / 外部付款回調使用）。
    /// amount 欄位存元寶（yuanbao）；twd 台幣只在 productName 中備註。
    /// </summary>
    public async Task WriteRechargeOrderAsync(string account, string orderNo, string productName, long yuanbaoAmt)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            // 限制 order_no 在 32 字元內（VARCHAR(32) UNIQUE）
            if (orderNo.Length > 32) orderNo = orderNo[..32];
            await using var cmd = new MySqlCommand(@"
                INSERT IGNORE INTO recharge_orders
                    (order_no, user_id, role_name, product_name, amount, status, created_at)
                VALUES (@ord,
                        IFNULL((SELECT id FROM game_users WHERE username=@role LIMIT 1), 0),
                        @role, @prod, @amt, 'completed', NOW())", db);
            cmd.Parameters.AddWithValue("@ord",  orderNo);
            cmd.Parameters.AddWithValue("@role", account);
            cmd.Parameters.AddWithValue("@prod", productName);
            cmd.Parameters.AddWithValue("@amt",  yuanbaoAmt);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* recharge_orders 表結構不符時靜默忽略 */ }
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
        string target, string customList, List<CartItem> cart, string title, string content,
        List<string>? excludeList = null)
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
        // 套用排除名單
        if (excludeList != null && excludeList.Count > 0)
        {
            var excludeSet = new HashSet<string>(excludeList.Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
            accounts = accounts.Where(a => !excludeSet.Contains(a)).ToList();
        }
        if (accounts.Count == 0) return (0, 0, new List<string>(), "找不到符合條件的玩家帳號（排除後為空）");

        int  nowInt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int  endInt = nowInt + 30 * 24 * 3600;
        string tf   = title.Trim();
        string cf   = content.Trim();
        int totalSent = 0, totalFail = 0;
        string lastError = "";
        var sentAccounts = new List<string>();

        // 預先整理 cart 資料（buff3 使用道具描述，與 EXE 一致）
        var rows = cart.Where(c => c.ItemId > 0).Select(c =>
        {
            string nm   = string.IsNullOrWhiteSpace(c.Name) ? $"道具#{c.ItemId}" : c.Name;
            string b1   = string.IsNullOrWhiteSpace(tf) ? nm : tf;
            string b2   = string.IsNullOrWhiteSpace(cf) ? nm : cf;
            string b3   = !string.IsNullOrWhiteSpace(c.Name) ? c.Name.Trim() : (c.Buff3 ?? "").Trim();
            return new {
                c.ItemId,
                MailType = c.Type > 0 ? c.Type : 1,
                Qty      = Math.Max(1, c.Qty),
                Buff1    = b1.Length > 200 ? b1[..200] : b1,
                Buff2    = b2.Length > 200 ? b2[..200] : b2,
                Buff3    = b3,
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
                    string b3Name  = $"@b3_{pIdx}";
                    cmd.Parameters.AddWithValue(tpName,  row.MailType);
                    cmd.Parameters.AddWithValue(b1Name,  row.Buff1);
                    cmd.Parameters.AddWithValue(b2Name,  row.Buff2);
                    cmd.Parameters.AddWithValue(datName, row.ItemId);
                    cmd.Parameters.AddWithValue(b3Name,  row.Buff3);
                    for (int q = 0; q < row.Qty; q++)
                        valueParts.Add($"({tpName},{cpName},{b1Name},{b2Name},{datName},@sendtime,@endtime,0,0,{b3Name})");
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
    public async Task<(List<ShopItemDto> items, List<ShopSpenderDto> spenders)> GetShopTopItemsAsync(string table, int topN = 20, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var items = new List<ShopItemDto>(); var spenders = new List<ShopSpenderDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            long rowCount = 0;
            try { await using var cc = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", db); rowCount = Convert.ToInt64(await cc.ExecuteScalarAsync()); } catch { return (items, spenders); }
            if (rowCount == 0) return (items, spenders);

            bool useDate = fromDate.HasValue && toDate.HasValue;
            DateTime d0 = useDate ? fromDate!.Value.Date : default;
            DateTime d1 = useDate ? toDate!.Value.Date : default;
            if (useDate && d0 > d1) { var x = d0; d0 = d1; d1 = x; }

            string whereVipFame = useDate ? " WHERE DATE(`time`) BETWEEN @dfrom AND @dto " : "";
            string whereCs = useDate ? " WHERE DATE(`date`) BETWEEN @dfrom AND @dto " : "";

            void AddDateParams(MySqlCommand cmd)
            {
                if (!useDate) return;
                cmd.Parameters.AddWithValue("@dfrom", d0);
                cmd.Parameters.AddWithValue("@dto", d1);
            }

            if (table == "vipshop" || table == "fameshop")
            {
                // `time` 為 MySQL 保留字，未加反引號時在部分版本會導致查詢失敗（畫面空白、後端 catch 吞錯）
                var sql1 = $@"SELECT itemid, itemname, SUM(itemnum) AS total_qty, COUNT(*) AS order_count,
                    SUM(IFNULL(oldpoint,0) - IFNULL(newpoint,0)) AS total_cost, MAX(`time`) AS last_time
                    FROM `{table}` {whereVipFame} GROUP BY itemid, itemname ORDER BY total_qty DESC LIMIT {topN}";
                await using (var cmd = new MySqlCommand(sql1, db))
                {
                    AddDateParams(cmd);
                    await using var r = await cmd.ExecuteReaderAsync();
                    int rank = 1;
                    while (await r.ReadAsync())
                        items.Add(new ShopItemDto { Rank = rank++, ItemId = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]), ItemName = r["itemname"]?.ToString() ?? "", TotalQty = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]), OrderCount = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"]), TotalCost = r["total_cost"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_cost"]), LastTime = r["last_time"]?.ToString() ?? "" });
                }
                var sql2 = $@"SELECT cdkey, name, SUM(itemnum) AS total_qty,
                    SUM(IFNULL(oldpoint,0) - IFNULL(newpoint,0)) AS total_cost FROM `{table}` {whereVipFame} GROUP BY cdkey, name ORDER BY total_cost DESC LIMIT {topN}";
                await using (var cmd2 = new MySqlCommand(sql2, db))
                {
                    AddDateParams(cmd2);
                    await using var r2 = await cmd2.ExecuteReaderAsync();
                    int rank = 1;
                    while (await r2.ReadAsync())
                        spenders.Add(new ShopSpenderDto { Rank = rank++, Cdkey = r2["cdkey"]?.ToString() ?? "", Name = r2["name"]?.ToString() ?? "", TotalQty = r2["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r2["total_qty"]), TotalCost = r2["total_cost"] == DBNull.Value ? 0 : Convert.ToInt64(r2["total_cost"]) });
                }
            }
            else if (table == "csshopnum" || table == "csxsshopnum")
            {
                var sql1 = $@"SELECT itemid, SUM(buynum) AS total_qty, COUNT(*) AS order_count, MAX(`date`) AS last_time FROM `{table}` {whereCs} GROUP BY itemid ORDER BY total_qty DESC LIMIT {topN}";
                await using var cmd = new MySqlCommand(sql1, db);
                AddDateParams(cmd);
                await using var r = await cmd.ExecuteReaderAsync();
                int rank = 1;
                while (await r.ReadAsync())
                    items.Add(new ShopItemDto { Rank = rank++, ItemId = r["itemid"] == DBNull.Value ? 0 : Convert.ToInt32(r["itemid"]), ItemName = $"道具 #{r["itemid"]}", TotalQty = r["total_qty"] == DBNull.Value ? 0 : Convert.ToInt64(r["total_qty"]), OrderCount = r["order_count"] == DBNull.Value ? 0 : Convert.ToInt64(r["order_count"]), LastTime = r["last_time"]?.ToString() ?? "" });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GetShopTopItemsAsync/{table}] {ex.Message}");
        }
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
    public async Task<(int fixed_, int total, int buff3Fixed)> FixOldWebMailsAsync(
        string account, List<(int ItemId, string Desc)>? itemDescriptions = null)
    {
        await using var db = Open(); await db.OpenAsync();

        // ── 1. 統計 buff3 為空且未領取的郵件總數 ──
        string accFilter = string.IsNullOrWhiteSpace(account) ? "" : "AND cdkey=@acc";
        await using var cnt = new MySqlCommand(
            $"SELECT COUNT(*) FROM maildata WHERE `check`=0 AND deleamill=0 AND (buff3 IS NULL OR buff3='') {accFilter}", db);
        if (!string.IsNullOrWhiteSpace(account)) cnt.Parameters.AddWithValue("@acc", account);
        int total = Convert.ToInt32(await cnt.ExecuteScalarAsync());

        // ── 2. 修正 buff1/buff2：把舊的通用標題改成「道具#ID」格式 ──
        string updTitle = string.IsNullOrWhiteSpace(account)
            ? @"UPDATE maildata SET buff1=CONCAT('道具#',data), buff2=CONCAT('道具#',data)
                WHERE `check`=0 AND deleamill=0
                AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%' OR buff1='[GM] 批量發送'
                     OR buff1 LIKE '[GM] %')"
            : @"UPDATE maildata SET buff1=CONCAT('道具#',data), buff2=CONCAT('道具#',data)
                WHERE `check`=0 AND deleamill=0 AND cdkey=@acc2
                AND (buff1='[GM] 道具發送' OR buff1 LIKE '[GM] 道具 #%' OR buff1='[GM] 批量發送'
                     OR buff1 LIKE '[GM] %')";
        await using var fixTitle = new MySqlCommand(updTitle, db);
        if (!string.IsNullOrWhiteSpace(account)) fixTitle.Parameters.AddWithValue("@acc2", account);
        int fixed_ = await fixTitle.ExecuteNonQueryAsync();

        int buff3Fixed = 0;

        // ── 3a. 優先用前端傳來的 items.xlsx 描述清單逐一 UPDATE ──
        if (itemDescriptions != null && itemDescriptions.Count > 0)
        {
            string accWhere = string.IsNullOrWhiteSpace(account) ? "" : "AND cdkey=@acc3";
            foreach (var (itemId, desc) in itemDescriptions)
            {
                if (string.IsNullOrWhiteSpace(desc)) continue;
                await using var upd = new MySqlCommand(
                    $@"UPDATE maildata SET buff3=@desc
                       WHERE data=@itemId AND `check`=0 AND (buff3 IS NULL OR buff3='') {accWhere}", db);
                upd.Parameters.AddWithValue("@desc",   desc);
                upd.Parameters.AddWithValue("@itemId", itemId);
                if (!string.IsNullOrWhiteSpace(account)) upd.Parameters.AddWithValue("@acc3", account);
                try { buff3Fixed += await upd.ExecuteNonQueryAsync(); } catch { }
            }
        }

        // ── 3b. 補救：從資料庫內既有的非空 buff3 記錄回填剩餘的 ──
        string updBuff3 = string.IsNullOrWhiteSpace(account)
            ? @"UPDATE maildata m
                JOIN (
                    SELECT data, buff3
                    FROM maildata
                    WHERE buff3 IS NOT NULL AND buff3 != ''
                    GROUP BY data, buff3
                    ORDER BY COUNT(*) DESC
                ) ref ON m.data = ref.data
                SET m.buff3 = ref.buff3
                WHERE m.`check`=0 AND m.deleamill=0 AND (m.buff3 IS NULL OR m.buff3='')"
            : @"UPDATE maildata m
                JOIN (
                    SELECT data, buff3
                    FROM maildata
                    WHERE buff3 IS NOT NULL AND buff3 != ''
                    GROUP BY data, buff3
                    ORDER BY COUNT(*) DESC
                ) ref ON m.data = ref.data
                SET m.buff3 = ref.buff3
                WHERE m.`check`=0 AND m.deleamill=0 AND (m.buff3 IS NULL OR m.buff3='')
                AND m.cdkey=@acc4";
        await using var fixBuff3 = new MySqlCommand(updBuff3, db);
        if (!string.IsNullOrWhiteSpace(account)) fixBuff3.Parameters.AddWithValue("@acc4", account);
        try { buff3Fixed += await fixBuff3.ExecuteNonQueryAsync(); } catch { }

        return (fixed_, total, buff3Fixed);
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

    // ── 加速外掛偵測 ──────────────────────────────────────────────
    public async Task<List<SpeedHackDto>> GetSpeedHackPlayersAsync(int minCnt = 1, int limit = 200)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            @"SELECT s.cdkey,
                     IFNULL(c.OnlineName,'') charName,
                     IFNULL(c.Online,0) isOnline,
                     SUM(s.speedcnt)    totalCnt,
                     COUNT(*)           records,
                     MAX(s.time)        lastTime,
                     ROUND(AVG(s.speedtime),1) avgSpeedTime,
                     MAX(s.speedtime)          maxSpeedTime,
                     (SELECT COUNT(*) FROM `lock` l WHERE l.`Name`=s.cdkey) isBanned
              FROM speedlog s
              LEFT JOIN csalogin c ON c.`Name`=s.cdkey
              GROUP BY s.cdkey
              HAVING totalCnt >= @min
              ORDER BY totalCnt DESC
              LIMIT @lim", db);
        cmd.Parameters.AddWithValue("@min", minCnt);
        cmd.Parameters.AddWithValue("@lim", limit);
        var list = new List<SpeedHackDto>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SpeedHackDto
            {
                Account      = r.GetString("cdkey"),
                CharName     = r.GetString("charName"),
                IsOnline     = r.GetInt32("isOnline") == 1,
                TotalCnt     = r.GetInt64("totalCnt"),
                Records      = r.GetInt32("records"),
                LastTime     = r.IsDBNull(r.GetOrdinal("lastTime")) ? "" :
                               ((DateTime)r["lastTime"]).ToString("yyyy/MM/dd HH:mm"),
                AvgSpeedTime = r.IsDBNull(r.GetOrdinal("avgSpeedTime")) ? 0 : r.GetDouble("avgSpeedTime"),
                MaxSpeedTime = r.IsDBNull(r.GetOrdinal("maxSpeedTime")) ? 0 : r.GetInt32("maxSpeedTime"),
                IsBanned     = r.GetInt32("isBanned") > 0,
            });
        return list;
    }

    // ── 清除玩家郵件（軟刪除，deleamill=1）────────────────────────
    public async Task<int> ClearPlayerMailAsync(string account, bool unclaimedOnly)
    {
        await using var db = Open(); await db.OpenAsync();
        string where = string.IsNullOrWhiteSpace(account)
            ? (unclaimedOnly ? "WHERE deleamill=0 AND `check`=0" : "WHERE deleamill=0")
            : (unclaimedOnly
                ? "WHERE cdkey=@acc AND deleamill=0 AND `check`=0"
                : "WHERE cdkey=@acc AND deleamill=0");
        await using var cmd = new MySqlCommand(
            $"UPDATE maildata SET deleamill=1 {where}", db);
        if (!string.IsNullOrWhiteSpace(account))
            cmd.Parameters.AddWithValue("@acc", account.Trim());
        return await cmd.ExecuteNonQueryAsync();
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
            string b3Name  = $"@b3_{pIdx}";
            cmd.Parameters.AddWithValue(tpName,  mailType);
            cmd.Parameters.AddWithValue(b1Name,  buff1);
            cmd.Parameters.AddWithValue(b2Name,  buff2);
            cmd.Parameters.AddWithValue(datName, item.ItemId);
            // buff3：與 EXE SendForm 一致（Buff3=道具名稱）；有 Name 時不用前端傳來的描述當 buff3
            string b3Game = !string.IsNullOrWhiteSpace(item.Name) ? item.Name.Trim() : (item.Buff3 ?? "").Trim();
            cmd.Parameters.AddWithValue(b3Name,  b3Game);
            for (int q = 0; q < Math.Max(1, item.Qty); q++)
                valueParts.Add($"({tpName},@cdkey,{b1Name},{b2Name},{datName},@sendtime,@endtime,0,0,{b3Name})");
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

    // ── 獎池/開獎紀錄 (poolitem)：依玩家 cdkey 查詢，是否為寶箱/骰子開出結果需對照遊戲確認
    public async Task<List<PoolItemRecordDto>> GetPlayerPoolItemAsync(string account, int limit = 200)
    {
        var list = new List<PoolItemRecordDto>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"SELECT cdkey, IFNULL(uid,'') uid, IFNULL(ITEM_ID,0) ITEM_ID, IFNULL(ITEM_NAME,'') ITEM_NAME
                  FROM poolitem WHERE cdkey=@acc LIMIT @lim", db);
            cmd.Parameters.AddWithValue("@acc", account);
            cmd.Parameters.AddWithValue("@lim", Math.Clamp(limit, 1, 500));
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new PoolItemRecordDto
                {
                    Cdkey    = r.GetString(0),
                    Uid      = r.IsDBNull(1) ? "" : r.GetString(1),
                    ItemId   = r.GetInt32(2),
                    ItemName = r.IsDBNull(3) ? "" : r.GetString(3),
                });
            }
        }
        catch { /* 表可能不存在或欄位不同 */ }
        return list;
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

    // ── 伺服器狀態查詢 ────────────────────────────────────────────────────────────────

    public async Task<List<object>> GetRecentRegistrationsAsync(int limit = 30)
    {
        var list = new List<object>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"SELECT c.`Name` account,
                         IFNULL(c.OnlineName,'') charName,
                         IFNULL(m.Name,'') masterName,
                         IFNULL(DATE_FORMAT(IFNULL(c.created_at,c.LoginTime),'%Y-%m-%d %H:%i'),'') regTime,
                         IFNULL(c.IP,'') regIP,
                         IFNULL(c.ServerId,0) serverId,
                         IF(c.Online=1,1,0) isOnlineBit
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id = c.MasterId
                  ORDER BY IFNULL(c.created_at,c.LoginTime) DESC LIMIT @lim", db);
            cmd.Parameters.AddWithValue("@lim", limit);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new {
                    account    = r.GetString("account"),
                    charName   = r.GetString("charName"),
                    masterName = r.GetString("masterName"),
                    regTime    = r.GetString("regTime"),
                    regIP      = r.GetString("regIP"),
                    serverName = $"分流 {r.GetInt32("serverId")}",
                    isOnline   = r.GetInt32("isOnlineBit") == 1
                });
        }
        catch { }
        return list;
    }

    /// <summary>查詢帳號的 IP，並找出共用同一 IP 的所有帳號</summary>
    public async Task<object> GetSharedIpAsync(string account)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();

            // 1. 查帳號的所有 IP（歷史登入 IP 和註冊 IP）
            await using var ipCmd = new MySqlCommand(
                @"SELECT IFNULL(c.IP,'') ip, IFNULL(c.RegIP,'') regIp,
                         IFNULL(c.OnlineName,'') charName,
                         IF(c.Online=1,1,0) isOnlineBit
                  FROM csalogin c WHERE c.`Name`=@acc LIMIT 1", db);
            ipCmd.Parameters.AddWithValue("@acc", account);
            await using var ipR = await ipCmd.ExecuteReaderAsync();
            if (!await ipR.ReadAsync())
                return new { found = false, message = "找不到帳號" };

            string ip    = ipR.GetString("ip");
            string regIp = ipR.GetString("regIp");
            string charName = ipR.GetString("charName");
            bool isOnline = ipR.GetInt32("isOnlineBit") == 1;
            await ipR.CloseAsync();

            // 收集不重複的 IP 清單
            var ips = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(ip))    ips.Add(ip);
            if (!string.IsNullOrWhiteSpace(regIp)) ips.Add(regIp);

            if (ips.Count == 0)
                return new { found = true, account, charName, isOnline, loginIp = "", regIp, sharedAccounts = new List<object>() };

            // 2. 找出用過相同 IP 的帳號（排除自己）
            var ipList = string.Join(",", ips.Select(i => $"'{i.Replace("'", "\\'")}'"));
            var sql = $@"SELECT c.`Name` account,
                                IFNULL(c.OnlineName,'') charName,
                                IFNULL(m.Name,'') masterName,
                                IFNULL(c.IP,'') ip,
                                IFNULL(c.RegIP,'') regIp,
                                IF(c.Online=1,1,0) isOnlineBit
                         FROM csalogin c
                         LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                         WHERE c.`Name` != @acc
                           AND (c.IP IN ({ipList}) OR c.RegIP IN ({ipList}))
                         ORDER BY c.Online DESC, c.LoginTime DESC
                         LIMIT 200";
            await using var shaCmd = new MySqlCommand(sql, db);
            shaCmd.Parameters.AddWithValue("@acc", account);
            await using var shaR = await shaCmd.ExecuteReaderAsync();

            var shared = new List<object>();
            while (await shaR.ReadAsync())
            {
                string aIp    = shaR.GetString("ip");
                string aRegIp = shaR.GetString("regIp");
                var matchIps  = ips.Where(x => x == aIp || x == aRegIp).ToList();
                shared.Add(new {
                    account    = shaR.GetString("account"),
                    charName   = shaR.GetString("charName"),
                    masterName = shaR.GetString("masterName"),
                    ip         = aIp,
                    regIp      = aRegIp,
                    isOnline   = shaR.GetInt32("isOnlineBit") == 1,
                    matchIps   = matchIps  // 命中哪個 IP
                });
            }

            return new {
                found    = true,
                account,
                charName,
                isOnline,
                loginIp  = ip,
                regIp,
                ips      = ips.ToList(),
                sharedAccounts = shared
            };
        }
        catch (Exception ex)
        {
            return new { found = false, message = ex.Message };
        }
    }

    /// <summary>查詢一個 IP 最早使用（原始主人）的帳號</summary>
    public async Task<object> GetIpOwnerAsync(string ip)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            // 找 RegIP 或 IP 最早使用此 IP 的帳號（依 created_at 或 LoginTime 排序）
            await using var cmd = new MySqlCommand(
                @"SELECT c.`Name` account,
                         IFNULL(c.OnlineName,'') charName,
                         IFNULL(m.Name,'') masterName,
                         IFNULL(c.IP,'') loginIp,
                         IFNULL(c.RegIP,'') regIp,
                         IF(c.Online=1,1,0) isOnline,
                         IFNULL(DATE_FORMAT(IFNULL(c.created_at,c.LoginTime),'%Y-%m-%d %H:%i'),'') regTime,
                         CASE WHEN c.RegIP=@ip THEN 'reg' ELSE 'login' END matchType
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  WHERE c.RegIP=@ip OR c.IP=@ip
                  ORDER BY IFNULL(c.created_at,c.LoginTime) ASC
                  LIMIT 1", db);
            cmd.Parameters.AddWithValue("@ip", ip);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return new { found = false, message = $"找不到使用過 {ip} 的帳號" };

            return new {
                found      = true,
                ip,
                account    = r.GetString("account"),
                charName   = r.GetString("charName"),
                masterName = r.GetString("masterName"),
                loginIp    = r.GetString("loginIp"),
                regIp      = r.GetString("regIp"),
                isOnline   = r.GetInt32("isOnline") == 1,
                regTime    = r.GetString("regTime"),
                matchType  = r.GetString("matchType")   // "reg"=由RegIP命中 / "login"=由IP命中
            };
        }
        catch (Exception ex)
        {
            return new { found = false, message = ex.Message };
        }
    }

    /// <summary>全服掃描：找出所有共用同一 IP 的帳號群組</summary>
    public async Task<object> GetIpGroupsAsync(int minGroup = 2, int limit = 300)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();

            // 先設 GROUP_CONCAT 長度上限，避免帳號清單被截斷（MySQL 預設 1024）
            await using (var setCmd = new MySqlCommand("SET SESSION group_concat_max_len = 1000000", db))
                await setCmd.ExecuteNonQueryAsync();

            // 找出 IP 底下有 >= minGroup 個帳號的 IP 群組
            // 同時比對登入IP(IP)和註冊IP(RegIP)
            var sql = $@"
                SELECT ip, GROUP_CONCAT(account ORDER BY isOnline DESC, account SEPARATOR '|||') accounts,
                       SUM(isOnline) onlineCount, COUNT(*) total
                FROM (
                    SELECT `Name` account, IFNULL(IP,'') ip, IF(Online=1,1,0) isOnline FROM csalogin WHERE IP IS NOT NULL AND IP != ''
                    UNION ALL
                    SELECT `Name` account, IFNULL(RegIP,'') ip, IF(Online=1,1,0) isOnline FROM csalogin WHERE RegIP IS NOT NULL AND RegIP != '' AND (IP IS NULL OR RegIP != IP)
                ) t
                GROUP BY ip
                HAVING COUNT(DISTINCT account) >= {minGroup}
                ORDER BY onlineCount DESC, total DESC
                LIMIT {limit}";

            await using var cmd = new MySqlCommand(sql, db);
            await using var r = await cmd.ExecuteReaderAsync();

            var ipGroups = new List<(string ip, string[] accs, int online, int total)>();
            while (await r.ReadAsync())
            {
                var accs = r.GetString("accounts").Split("|||");
                ipGroups.Add((
                    r.GetString("ip"),
                    accs,
                    Convert.ToInt32(r["onlineCount"]),
                    Convert.ToInt32(r["total"])
                ));
            }
            await r.CloseAsync();

            if (ipGroups.Count == 0)
                return new { groups = new List<object>(), totalGroups = 0, totalAccounts = 0 };

            // 批次查帳號詳細資訊
            var allAccs = ipGroups.SelectMany(g => g.accs).Distinct().ToList();
            var accDetails = new Dictionary<string, (string charName, string masterName, bool isOnline)>();

            // 分批查，避免 SQL 過長
            for (int i = 0; i < allAccs.Count; i += 200)
            {
                var batch = allAccs.Skip(i).Take(200).ToList();
                var inList = string.Join(",", batch.Select(a => $"'{a.Replace("'", "\\'")}'"));
                var detailSql = $@"SELECT c.`Name` acc, IFNULL(c.OnlineName,'') charName,
                                          IFNULL(m.Name,'') masterName, IF(c.Online=1,1,0) isOnline
                                   FROM csalogin c
                                   LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                                   WHERE c.`Name` IN ({inList})";
                await using var dCmd = new MySqlCommand(detailSql, db);
                await using var dR = await dCmd.ExecuteReaderAsync();
                while (await dR.ReadAsync())
                    accDetails[dR.GetString("acc")] = (dR.GetString("charName"), dR.GetString("masterName"), dR.GetInt32("isOnline") == 1);
                await dR.CloseAsync();
            }

            // 組成結果
            var groups = ipGroups.Select(g => new {
                ip = g.ip,
                onlineCount = g.online,
                totalCount = g.total,
                accounts = g.accs.Select(a => {
                    var d = accDetails.TryGetValue(a, out var v) ? v : (charName: "", masterName: "", isOnline: false);
                    return new { account = a, charName = d.charName, masterName = d.masterName, isOnline = d.isOnline };
                }).ToList()
            }).ToList();

            return new {
                groups,
                totalGroups   = groups.Count,
                totalAccounts = groups.Sum(g => g.accounts.Count)
            };
        }
        catch (Exception ex)
        {
            return new { groups = new List<object>(), totalGroups = 0, totalAccounts = 0, error = ex.Message };
        }
    }

    public async Task<List<object>> GetChannelOnlineCountAsync()
    {
        var list = new List<object>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"SELECT IFNULL(ServerId,0) serverId,
                         SUM(IF(Online=1,1,0)) onlineCount,
                         COUNT(*) totalCount
                  FROM csalogin
                  GROUP BY ServerId ORDER BY ServerId", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                int sid = r.GetInt32("serverId");
                list.Add(new {
                    serverId    = sid,
                    serverName  = $"分流 {sid}",
                    onlineCount = Convert.ToInt32(r["onlineCount"]),
                    totalCount  = Convert.ToInt32(r["totalCount"])
                });
            }
        }
        catch { }
        return list;
    }

    public async Task<object> GetMasterAccountStatsAsync()
    {
        int total = 0, online = 0;
        try
        {
            await using var db = Open();
            await db.OpenAsync();
            await using var cmd = new MySqlCommand(
                @"SELECT COUNT(DISTINCT m.Id) TotalMasters,
                         COUNT(DISTINCT CASE WHEN c.Online=1 THEN m.Id END) OnlineMasters
                  FROM csaloginmaster m
                  LEFT JOIN csalogin c ON c.MasterId = m.Id", db);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                total  = r["TotalMasters"]  == DBNull.Value ? 0 : Convert.ToInt32(r["TotalMasters"]);
                online = r["OnlineMasters"] == DBNull.Value ? 0 : Convert.ToInt32(r["OnlineMasters"]);
            }
        }
        catch { }
        return new { totalMasters = total, onlineMasters = online, offlineMasters = total - online };
    }

    // ── 累計消費達成獎勵（costdata，與累積儲值 paydata 對稱）─────────────────────────
    private static readonly long[] CostMilestones = { 3_000, 5_000, 10_000, 50_000, 100_000 };

    /// <summary>
    /// 將任意輸入（主帳號名/角色名/csalogin.Name UID）解析為 csalogin.Name（12位UID）。
    /// costdata/paydata 的 cdkey 即為此值。
    /// </summary>
    private async Task<(string uid, string onlineName)> ResolveCsaloginAsync(MySqlConnection db, string input)
    {
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT c.`Name`, IFNULL(c.OnlineName,'') n
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  WHERE c.`Name`=@inp OR c.OnlineName=@inp OR m.`Name`=@inp
                  ORDER BY c.Online DESC, c.LoginTime DESC LIMIT 1", db);
            cmd.Parameters.AddWithValue("@inp", input);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                return (r.GetString(0), r.GetString(1));
        }
        catch { }
        return (input, "");
    }

    /// <summary>同 ResolveCsaloginAsync，但額外回傳主帳號名稱 masterAccount</summary>
    private async Task<(string uid, string onlineName, string masterAccount)> ResolveCsaloginWithMasterAsync(MySqlConnection db, string input)
    {
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT c.`Name`, IFNULL(c.OnlineName,'') n, IFNULL(m.`Name`,'') master
                  FROM csalogin c
                  LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                  WHERE c.`Name`=@inp OR c.OnlineName=@inp OR m.`Name`=@inp
                  ORDER BY c.Online DESC, c.LoginTime DESC LIMIT 1", db);
            cmd.Parameters.AddWithValue("@inp", input);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                return (r.GetString(0), r.GetString(1), r.GetString(2));
        }
        catch { }
        return (input, "", "");
    }

    /// <summary>
    /// 查詢主帳號下所有角色的 costdata（用於主帳號搜尋顯示角色列表）。
    /// 若輸入不是主帳號名，則退化為單角色查詢。
    /// </summary>
    public async Task<List<object>> GetAllCharsCostdataAsync(string masterName)
    {
        var result = new List<object>();
        await using var db = Open(); await db.OpenAsync();
        try
        {
            // 找主帳號 ID
            int masterId = 0;
            await using var cmdM = new MySqlCommand(
                "SELECT Id FROM csaloginmaster WHERE `Name`=@n LIMIT 1", db);
            cmdM.Parameters.AddWithValue("@n", masterName);
            await using var rM = await cmdM.ExecuteReaderAsync();
            if (await rM.ReadAsync()) masterId = rM.GetInt32(0);
            await rM.CloseAsync();

            if (masterId == 0) return result; // 不是主帳號

            // 取主帳號下所有角色（含主帳號名）
            string masterAccountName = masterName;
            try
            {
                await using var cmdMn = new MySqlCommand("SELECT `Name` FROM csaloginmaster WHERE Id=@mid LIMIT 1", db);
                cmdMn.Parameters.AddWithValue("@mid", masterId);
                await using var rMn = await cmdMn.ExecuteReaderAsync();
                if (await rMn.ReadAsync()) masterAccountName = rMn.GetString(0);
            }
            catch { }

            await using var cmdC = new MySqlCommand(
                @"SELECT c.`Name`, IFNULL(c.OnlineName,'') onlineName, (c.Online=1) isOnline
                  FROM csalogin c WHERE c.MasterId=@mid ORDER BY c.Online DESC, c.LoginTime DESC", db);
            cmdC.Parameters.AddWithValue("@mid", masterId);
            var chars = new List<(string uid, string onlineName, bool isOnline)>();
            await using (var rC = await cmdC.ExecuteReaderAsync())
            {
                while (await rC.ReadAsync())
                    chars.Add((rC.GetString(0), rC.GetString(1), rC.GetBoolean(2)));
            }

            // 逐一查 costdata
            foreach (var (uid, onlineName, isOnline) in chars)
            {
                long costPoint = 0; int costCheck = -1;
                try
                {
                    await using var cmdCd = new MySqlCommand(
                        "SELECT point, IFNULL(`check`,0) ck FROM costdata WHERE cdkey=@acc LIMIT 1", db);
                    cmdCd.Parameters.AddWithValue("@acc", uid);
                    await using var rCd = await cmdCd.ExecuteReaderAsync();
                    if (await rCd.ReadAsync())
                    {
                        costPoint = rCd.IsDBNull(0) ? 0 : rCd.GetInt64(0);
                        costCheck = rCd.IsDBNull(1) ? 0 : rCd.GetInt32(1);
                    }
                }
                catch { }

                var milestones = CostMilestones.Select((m, i) => new
                {
                    index = i, required = m,
                    reached = costPoint >= m,
                    claimed = costCheck >= 0 && (costCheck & (1 << i)) != 0
                }).ToArray();
                int claimedCount = costCheck < 0 ? 0 : System.Numerics.BitOperations.PopCount((uint)costCheck);
                result.Add(new { account = uid, onlineName, isOnline, masterAccount = masterAccountName, costPoint, costCheck, claimedCount, milestones });
            }
        }
        catch { }
        return result;
    }

    public async Task<object> GetCostdataSummaryAsync(string account)
    {
        long costPoint = 0; int costCheck = -1;
        await using var db = Open(); await db.OpenAsync();

        var (uid, onlineName, masterAccount) = await ResolveCsaloginWithMasterAsync(db, account);

        try
        {
            await using var cmd = new MySqlCommand(
                "SELECT point, IFNULL(`check`,0) ck FROM costdata WHERE cdkey=@acc ORDER BY time DESC LIMIT 1", db);
            cmd.Parameters.AddWithValue("@acc", uid);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                costPoint = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                costCheck = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            }
        }
        catch { }
        // costdata.check 是 Bitmask：bit i = 第 i+1 個里程碑已領取（check=31=11111₂=全部5個）
        var milestones = CostMilestones.Select((m, i) => new
        {
            index    = i,
            required = m,
            reached  = costPoint >= m,
            claimed  = costCheck >= 0 && (costCheck & (1 << i)) != 0
        }).ToArray();
        int claimedCount = costCheck < 0 ? 0 : System.Numerics.BitOperations.PopCount((uint)costCheck);
        return new { account = uid, onlineName, masterAccount, costPoint, costCheck, claimedCount, milestones };
    }

    /// <summary>取得全服（或線上）玩家的 costdata 列表，用於批量操作頁面</summary>
    public async Task<List<object>> GetAllCostDataAsync(bool onlineOnly)
    {
        var list = new List<object>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            string where = onlineOnly ? "AND c.Online=1" : "";
            await using var cmd = new MySqlCommand($@"
                SELECT c.`Name` cdkey, IFNULL(c.OnlineName,'') charName,
                       IFNULL(m.`Name`,'') masterName,
                       (c.Online=1) isOnline,
                       IFNULL(d.point,0) point, IFNULL(d.`check`,0) ck,
                       IFNULL(DATE_FORMAT(d.time,'%Y-%m-%d %H:%i'),'') lastTime
                FROM csalogin c
                INNER JOIN costdata d ON d.cdkey=c.`Name`
                LEFT JOIN csaloginmaster m ON m.Id=c.MasterId
                WHERE 1=1 {where}
                ORDER BY d.point DESC LIMIT 2000", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                long point = r.GetInt64("point");
                int  check = r.GetInt32("ck");
                var milestones = CostMilestones.Select((req, idx) => new
                {
                    index = idx, required = req,
                    reached = point >= req,
                    claimed = (check & (1 << idx)) != 0
                }).ToList();
                int claimedCount = System.Numerics.BitOperations.PopCount((uint)(check >= 0 ? check : 0));
                list.Add(new {
                    account       = r.GetString("cdkey"),
                    onlineName    = r.GetString("charName"),
                    masterAccount = r.GetString("masterName"),
                    isOnline      = r.GetBoolean("isOnline"),
                    costPoint     = point,
                    costCheck     = check,
                    claimedCount,
                    milestones,
                    lastTime      = r.GetString("lastTime")
                });
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[GetAllCostData] " + ex.Message); }
        return list;
    }

    /// <summary>批量重置多個帳號的 costdata（check-only 或 full-reset）</summary>
    public async Task<(int success, int fail)> BatchResetCostDataAsync(List<string> accounts, bool fullReset)
    {
        int success = 0, fail = 0;
        foreach (var acc in accounts)
        {
            bool ok = fullReset
                ? await FullResetCostdataAsync(acc)
                : await ResetCostdataAsync(acc);
            if (ok) success++; else fail++;
        }
        return (success, fail);
    }

    public async Task<bool> AdjustCostdataPointAsync(string account, string charName, long addPoint)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            var (uid, _) = await ResolveCsaloginAsync(db, account);
            await using var cmd = new MySqlCommand(
                @"INSERT INTO costdata (cdkey, name, point, `check`, time)
                  VALUES (@cdkey, @name, @pt, 0, NOW())
                  ON DUPLICATE KEY UPDATE point = point + @pt, time = NOW()", db);
            cmd.Parameters.AddWithValue("@cdkey", uid);
            cmd.Parameters.AddWithValue("@name",  charName);
            cmd.Parameters.AddWithValue("@pt",    addPoint);
            return await cmd.ExecuteNonQueryAsync() >= 0;
        }
        catch { return false; }
    }

    /// <summary>僅清除已領取狀態（check=0），消費點數 point 保留 → 玩家可立即重領</summary>
    public async Task<bool> ResetCostdataAsync(string account)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            var (uid, _) = await ResolveCsaloginAsync(db, account);
            await using var cmd = new MySqlCommand(
                "UPDATE costdata SET `check`=0, time=NOW() WHERE cdkey=@acc", db);
            cmd.Parameters.AddWithValue("@acc", uid);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch { return false; }
    }

    /// <summary>完全重置（point=0 且 check=0）→ 玩家必須重新消費才能領取</summary>
    public async Task<bool> FullResetCostdataAsync(string account)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            var (uid, _) = await ResolveCsaloginAsync(db, account);
            await using var cmd = new MySqlCommand(
                "UPDATE costdata SET point=0, `check`=0, time=NOW() WHERE cdkey=@acc", db);
            cmd.Parameters.AddWithValue("@acc", uid);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch { return false; }
    }

    // costdata.check Bitmask 說明：
    // bit 0(1)=3000已領, bit 1(2)=5000已領, bit 2(4)=10000已領,
    // bit 3(8)=50000已領, bit 4(16)=100000已領, 全部=31(11111₂)

    /// <summary>
    /// 補發消費達成獎勵（同步遊戲模式）：
    /// 清除 check 中對應的 bit，讓遊戲伺服器偵測到「達成但未領」並自動發道具到背包。
    /// </summary>
    public async Task<bool> ClaimCostMilestoneAsync(string account, int milestoneIdx)
    {
        if (milestoneIdx < 0 || milestoneIdx >= CostMilestones.Length) return false;
        try
        {
            int bit = 1 << milestoneIdx;
            await using var db = Open(); await db.OpenAsync();
            var (uid, _) = await ResolveCsaloginAsync(db, account);
            await using var cmd = new MySqlCommand(
                "UPDATE costdata SET `check`=(`check` & ~@bit), time=NOW() WHERE cdkey=@acc", db);
            cmd.Parameters.AddWithValue("@bit", bit);
            cmd.Parameters.AddWithValue("@acc", uid);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// 補發消費達成獎勵（郵件模式）：直接寄出道具，並設定對應 bit 為已領。
    /// </summary>
    public async Task<bool> ClaimCostMilestoneByMailAsync(
        string account, string charName, int milestoneIdx, int itemId, string itemName, int quantity)
    {
        if (milestoneIdx < 0 || milestoneIdx >= CostMilestones.Length) return false;
        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await using var dbMail = Open(); await dbMail.OpenAsync();
            var (uid, _) = await ResolveCsaloginAsync(dbMail, account);
            await using var cmdMail = new MySqlCommand(
                @"INSERT INTO maildata (cdkey,type,buff1,buff2,data,starttime,endtime,buff3,`check`,deleamill,quantity)
                  VALUES (@k,@t,@b1,@b2,@d,@s,@e,'',0,0,@q)", dbMail);
            cmdMail.Parameters.AddWithValue("@k",  uid);
            cmdMail.Parameters.AddWithValue("@t",  1);
            cmdMail.Parameters.AddWithValue("@b1", $"[GM] {itemName}");
            cmdMail.Parameters.AddWithValue("@b2", $"消費達成里程碑 {CostMilestones[milestoneIdx]:N0} 金幣獎勵補發");
            cmdMail.Parameters.AddWithValue("@d",  itemId);
            cmdMail.Parameters.AddWithValue("@s",  (int)now);
            cmdMail.Parameters.AddWithValue("@e",  (int)(now + 30L * 24 * 3600));
            cmdMail.Parameters.AddWithValue("@q",  quantity);
            await cmdMail.ExecuteNonQueryAsync();

            // 設定 bit（標記此里程碑已發送）
            int bit = 1 << milestoneIdx;
            await using var dbCk = Open(); await dbCk.OpenAsync();
            await using var cmdCk = new MySqlCommand(
                "UPDATE costdata SET `check`=(`check` | @bit), time=NOW() WHERE cdkey=@acc", dbCk);
            cmdCk.Parameters.AddWithValue("@bit", bit);
            cmdCk.Parameters.AddWithValue("@acc", uid);
            await cmdCk.ExecuteNonQueryAsync();
            return true;
        }
        catch { return false; }
    }

    // ══════════════════════════════════════════════════════════
    // 家族查詢與管理
    // ══════════════════════════════════════════════════════════

    public async Task<List<GuildInfo>> GetGuildListAsync()
    {
        var list = new List<GuildInfo>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(@"
                SELECT z.jiazuid, z.jiazu,
                       COUNT(DISTINCT z.cdkey) AS memberCount,
                       MAX(z.addtime) AS lastActive,
                       IFNULL(sc.shopContrib, 0) AS shopContrib
                FROM zuzhanlog z
                INNER JOIN (SELECT cdkey, MAX(id) mid FROM zuzhanlog WHERE jiazuid > 0 GROUP BY cdkey) latest
                    ON z.cdkey = latest.cdkey AND z.id = latest.mid
                LEFT JOIN (
                    SELECT mem.jiazuid,
                           SUM(fs.oldpoint - fs.newpoint) AS shopContrib
                    FROM fameshop fs
                    INNER JOIN (SELECT cdkey, jiazuid FROM zuzhanlog
                                WHERE id IN (SELECT MAX(id) FROM zuzhanlog WHERE jiazuid > 0 GROUP BY cdkey)) mem
                        ON fs.cdkey = mem.cdkey
                    GROUP BY mem.jiazuid
                ) sc ON sc.jiazuid = z.jiazuid
                WHERE z.jiazuid > 0
                GROUP BY z.jiazuid, z.jiazu
                ORDER BY memberCount DESC, shopContrib DESC", db);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new GuildInfo
                {
                    GuildId     = Convert.ToInt32(r["jiazuid"]),
                    GuildName   = r["jiazu"]?.ToString() ?? "",
                    MemberCount = Convert.ToInt32(r["memberCount"]),
                    LastActive  = r["lastActive"] == DBNull.Value ? "" : Convert.ToDateTime(r["lastActive"]).ToString("yyyy-MM-dd HH:mm"),
                    ShopContrib = Convert.ToInt64(r["shopContrib"])
                });
        }
        catch { }
        return list;
    }

    public async Task<List<GuildMember>> GetGuildMembersAsync(int guildId)
    {
        var list = new List<GuildMember>();
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var cmd = new MySqlCommand(@"
                SELECT z.cdkey, z.uname, z.addtime,
                       IFNULL(c.OnlineName,'') onlineName,
                       IFNULL(c.PayTotal,0) payTotal,
                       IFNULL(c.VipPoint,0) gold,
                       (c.Online = 1) isOnline,
                       IFNULL((SELECT SUM(fs.oldpoint - fs.newpoint) FROM fameshop fs WHERE fs.cdkey = z.cdkey), 0) shopContrib
                FROM zuzhanlog z
                INNER JOIN (SELECT cdkey, MAX(id) mid FROM zuzhanlog WHERE jiazuid = @gid GROUP BY cdkey) latest
                    ON z.cdkey = latest.cdkey AND z.id = latest.mid
                LEFT JOIN csalogin c ON c.Name = z.cdkey
                WHERE z.jiazuid = @gid
                ORDER BY shopContrib DESC, z.uname", db);
            cmd.Parameters.AddWithValue("@gid", guildId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                list.Add(new GuildMember
                {
                    Cdkey       = r["cdkey"]?.ToString() ?? "",
                    CharName    = r["uname"]?.ToString() ?? "",
                    OnlineName  = r["onlineName"]?.ToString() ?? "",
                    JoinTime    = r["addtime"] == DBNull.Value ? "" : Convert.ToDateTime(r["addtime"]).ToString("yyyy-MM-dd HH:mm"),
                    PayTotal    = Convert.ToInt32(r["payTotal"]),
                    Gold        = Convert.ToInt64(r["gold"]),
                    IsOnline    = Convert.ToBoolean(r["isOnline"]),
                    ShopContrib = Convert.ToInt64(r["shopContrib"])
                });
        }
        catch { }
        return list;
    }

    public async Task<(bool ok, string msg)> DissolveGuildAsync(int guildId)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var tx = await db.BeginTransactionAsync();
            try
            {
                await using (var c1 = new MySqlCommand(
                    "UPDATE playerdata SET fmindex=0, fmname='' WHERE fmindex=@gid", db, tx))
                { c1.Parameters.AddWithValue("@gid", guildId); await c1.ExecuteNonQueryAsync(); }

                int n = 0;
                await using (var c2 = new MySqlCommand(
                    "DELETE FROM zuzhanlog WHERE jiazuid=@gid", db, tx))
                { c2.Parameters.AddWithValue("@gid", guildId); n = await c2.ExecuteNonQueryAsync(); }

                await tx.CommitAsync();
                return (true, $"家族已解散，共刪除 {n} 筆記錄");
            }
            catch { await tx.RollbackAsync(); throw; }
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> KickGuildMemberAsync(int guildId, string cdkey)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var tx = await db.BeginTransactionAsync();
            try
            {
                int n = 0;
                await using (var c1 = new MySqlCommand(
                    "DELETE FROM zuzhanlog WHERE jiazuid=@gid AND cdkey=@ck", db, tx))
                { c1.Parameters.AddWithValue("@gid", guildId); c1.Parameters.AddWithValue("@ck", cdkey); n = await c1.ExecuteNonQueryAsync(); }

                await using (var c2 = new MySqlCommand(
                    "UPDATE playerdata SET fmindex=0, fmname='' WHERE cdkey=@ck AND fmindex=@gid", db, tx))
                { c2.Parameters.AddWithValue("@ck", cdkey); c2.Parameters.AddWithValue("@gid", guildId); await c2.ExecuteNonQueryAsync(); }

                await tx.CommitAsync();
                return n > 0 ? (true, $"已將 {cdkey} 移除") : (false, "未找到該成員");
            }
            catch { await tx.RollbackAsync(); throw; }
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── 玩家詳情強化：同IP帳號、封禁記錄、家族資訊 ─────────────────

    /// <summary>查詢與指定帳號共用相同 IP 的其他帳號（登入IP或註冊IP）</summary>
    public async Task<List<object>> GetSharedIpAccountsAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        // 先取得該帳號的 IP 和 RegIP
        string loginIp = "", regIp = "";
        await using (var cmd0 = new MySqlCommand("SELECT IFNULL(IP,'') ip, IFNULL(RegIP,'') regip FROM csalogin WHERE `Name`=@a LIMIT 1", db))
        {
            cmd0.Parameters.AddWithValue("@a", account);
            await using var r0 = await cmd0.ExecuteReaderAsync();
            if (await r0.ReadAsync()) { loginIp = r0.GetString("ip"); regIp = r0.GetString("regip"); }
        }
        if (string.IsNullOrWhiteSpace(loginIp) && string.IsNullOrWhiteSpace(regIp))
            return new List<object>();

        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(loginIp))  conditions.Add("(IP=@lip OR RegIP=@lip)");
        if (!string.IsNullOrWhiteSpace(regIp) && regIp != loginIp) conditions.Add("(IP=@rip OR RegIP=@rip)");

        var sql = $@"SELECT `Name` account, IFNULL(OnlineName,'') charName,
                            IFNULL(IP,'') ip, IFNULL(RegIP,'') regIp,
                            Online isOnline,
                            IFNULL(PayTotal,0) payTotal,
                            DATE_FORMAT(LoginTime,'%Y-%m-%d %H:%i') loginTime,
                            DATE_FORMAT(created_at,'%Y-%m-%d') regTime
                     FROM csalogin
                     WHERE `Name`!=@self AND ({string.Join(" OR ", conditions)})
                     ORDER BY Online DESC, LoginTime DESC LIMIT 50";
        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@self", account);
        if (!string.IsNullOrWhiteSpace(loginIp))  cmd.Parameters.AddWithValue("@lip", loginIp);
        if (!string.IsNullOrWhiteSpace(regIp) && regIp != loginIp) cmd.Parameters.AddWithValue("@rip", regIp);

        var list = new List<object>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new {
                account   = r.GetString("account"),
                charName  = r.GetString("charName"),
                ip        = r.GetString("ip"),
                regIp     = r.GetString("regIp"),
                isOnline  = r.GetInt32("isOnline") == 1,
                payTotal  = Convert.ToInt64(r["payTotal"]),
                loginTime = r.GetString("loginTime"),
                regTime   = r.GetString("regTime"),
            });
        }
        return list;
    }

    /// <summary>查詢指定帳號的封禁歷史記錄（lock 表）</summary>
    public async Task<List<object>> GetBanLogAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<object>();
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT `time` banEndTime, IFNULL(reason,'') reason
                  FROM `lock` WHERE `Name`=@a ORDER BY `time` ASC LIMIT 50", db);
            cmd.Parameters.AddWithValue("@a", account);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                long t = r.GetInt64("banEndTime");
                list.Add(new {
                    banEndTime  = t == 0 ? "永久" : DateTimeOffset.FromUnixTimeSeconds(t).LocalDateTime.ToString("yyyy/MM/dd HH:mm"),
                    isPermanent = t == 0,
                    reason      = r.GetString("reason"),
                });
            }
        }
        catch { }
        return list;
    }

    /// <summary>查詢指定帳號的家族資訊</summary>
    public async Task<object?> GetPlayerFamilyAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        try
        {
            await using var cmd = new MySqlCommand(
                @"SELECT z.jiazuid guildId, z.jiazu guildName,
                         (SELECT COUNT(*) FROM zuzhanlog zz WHERE zz.jiazuid=z.jiazuid AND zz.id IN (SELECT MAX(id) FROM zuzhanlog GROUP BY cdkey)) memberCount
                  FROM zuzhanlog z
                  WHERE z.cdkey=@a AND z.jiazuid>0
                  ORDER BY z.id DESC LIMIT 1", db);
            cmd.Parameters.AddWithValue("@a", account);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new {
                    guildId     = Convert.ToInt32(r["guildId"]),
                    guildName   = r["guildName"]?.ToString() ?? "",
                    memberCount = Convert.ToInt32(r["memberCount"]),
                };
            }
        }
        catch { }
        return null;
    }

    // ── 練寵排行榜 ─────────────────────────────────────────────────

    /// <summary>取得所有曾出現的練寵寵物種類（id + name + 參賽數 + 最高分）</summary>
    public async Task<List<object>> GetPetRankTypesAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<object>();
        await using var cmd = new MySqlCommand(@"
            SELECT id, name,
                   COUNT(*) AS entryCount,
                   MAX(sum) AS topScore,
                   MIN(inserttime) AS firstEntry,
                   MAX(inserttime) AS lastEntry
            FROM capturepet
            GROUP BY id, name
            ORDER BY lastEntry DESC", db);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new {
                id         = Convert.ToInt32(r["id"]),
                name       = r["name"]?.ToString() ?? "",
                entryCount = Convert.ToInt32(r["entryCount"]),
                topScore   = Convert.ToDouble(r["topScore"]),
                firstEntry = r["firstEntry"]?.ToString() ?? "",
                lastEntry  = r["lastEntry"]?.ToString() ?? "",
            });
        return list;
    }

    /// <summary>取得指定寵物排行榜（每人只取最高分那筆，相容 MySQL 5.7）</summary>
    public async Task<List<object>> GetPetLeaderboardAsync(int petId, int limit = 50)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<object>();
        await using var cmd = new MySqlCommand(@"
            SELECT c.unicode, c.author, c.cdkey, c.name AS petName,
                   c.lv, c.hp, c.attack, c.def, c.quick, c.sum,
                   c.`check`, DATE_FORMAT(c.inserttime,'%Y-%m-%d %H:%i') AS inserttime,
                   ec.entryCount
            FROM capturepet c
            INNER JOIN (
                SELECT cdkey, MAX(sum) AS maxsum
                FROM capturepet WHERE id = @pid
                GROUP BY cdkey
            ) m ON c.cdkey = m.cdkey AND c.sum = m.maxsum AND c.id = @pid
            INNER JOIN (
                SELECT cdkey, COUNT(*) AS entryCount
                FROM capturepet WHERE id = @pid
                GROUP BY cdkey
            ) ec ON c.cdkey = ec.cdkey
            ORDER BY c.sum DESC
            LIMIT @lim", db);
        cmd.Parameters.AddWithValue("@pid", petId);
        cmd.Parameters.AddWithValue("@lim", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        int rank = 1;
        while (await r.ReadAsync())
            list.Add(new {
                rank       = rank++,
                unicode    = r["unicode"]?.ToString() ?? "",
                author     = r["author"]?.ToString() ?? "",
                cdkey      = r["cdkey"]?.ToString() ?? "",
                petName    = r["petName"]?.ToString() ?? "",
                lv         = Convert.ToInt32(r["lv"]),
                hp         = Convert.ToInt32(r["hp"]),
                attack     = Convert.ToInt32(r["attack"]),
                def        = Convert.ToInt32(r["def"]),
                quick      = Convert.ToInt32(r["quick"]),
                sum        = Convert.ToDouble(r["sum"]),
                check      = Convert.ToBoolean(r["check"]),
                inserttime = r["inserttime"]?.ToString() ?? "",
                entryCount = Convert.ToInt32(r["entryCount"]),
            });
        return list;
    }

    /// <summary>查詢某玩家的所有練寵記錄（含各期）</summary>
    public async Task<List<object>> GetPlayerPetEntriesAsync(string account)
    {
        await using var db = Open(); await db.OpenAsync();
        var list = new List<object>();
        await using var cmd = new MySqlCommand(@"
            SELECT unicode, id, name AS petName, lv, hp, attack, def, quick, sum,
                   author, cdkey, `check`, DATE_FORMAT(inserttime,'%Y-%m-%d %H:%i') AS inserttime
            FROM capturepet
            WHERE cdkey = @a OR author = @a
            ORDER BY sum DESC", db);
        cmd.Parameters.AddWithValue("@a", account);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new {
                unicode    = r["unicode"]?.ToString() ?? "",
                id         = Convert.ToInt32(r["id"]),
                petName    = r["petName"]?.ToString() ?? "",
                lv         = Convert.ToInt32(r["lv"]),
                hp         = Convert.ToInt32(r["hp"]),
                attack     = Convert.ToInt32(r["attack"]),
                def        = Convert.ToInt32(r["def"]),
                quick      = Convert.ToInt32(r["quick"]),
                sum        = Convert.ToDouble(r["sum"]),
                author     = r["author"]?.ToString() ?? "",
                cdkey      = r["cdkey"]?.ToString() ?? "",
                check      = Convert.ToBoolean(r["check"]),
                inserttime = r["inserttime"]?.ToString() ?? "",
            });
        return list;
    }

    /// <summary>切換 capturepet check 審核狀態</summary>
    public async Task<bool> SetPetCheckAsync(string unicode, bool check)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "UPDATE capturepet SET `check`=@c WHERE unicode=@u", db);
        cmd.Parameters.AddWithValue("@c", check ? 1 : 0);
        cmd.Parameters.AddWithValue("@u", unicode);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>刪除特定練寵記錄</summary>
    public async Task<bool> DeletePetEntryAsync(string unicode)
    {
        await using var db = Open(); await db.OpenAsync();
        await using var cmd = new MySqlCommand(
            "DELETE FROM capturepet WHERE unicode=@u", db);
        cmd.Parameters.AddWithValue("@u", unicode);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<(bool ok, string msg)> TransferGuildMemberAsync(string cdkey, int targetGuildId, string targetGuildName)
    {
        try
        {
            await using var db = Open(); await db.OpenAsync();
            await using var tx = await db.BeginTransactionAsync();
            try
            {
                await using (var c1 = new MySqlCommand(@"
                    UPDATE zuzhanlog SET jiazuid=@tid, jiazu=@tname
                    WHERE cdkey=@ck AND id=(SELECT mid FROM (SELECT MAX(id) mid FROM zuzhanlog WHERE cdkey=@ck) t)", db, tx))
                { c1.Parameters.AddWithValue("@tid", targetGuildId); c1.Parameters.AddWithValue("@tname", targetGuildName); c1.Parameters.AddWithValue("@ck", cdkey); await c1.ExecuteNonQueryAsync(); }

                await using (var c2 = new MySqlCommand(
                    "UPDATE playerdata SET fmindex=@tid, fmname=@tname WHERE cdkey=@ck", db, tx))
                { c2.Parameters.AddWithValue("@tid", targetGuildId); c2.Parameters.AddWithValue("@tname", targetGuildName); c2.Parameters.AddWithValue("@ck", cdkey); await c2.ExecuteNonQueryAsync(); }

                await tx.CommitAsync();
                return (true, $"已將 {cdkey} 轉移至家族「{targetGuildName}」");
            }
            catch { await tx.RollbackAsync(); throw; }
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

}
