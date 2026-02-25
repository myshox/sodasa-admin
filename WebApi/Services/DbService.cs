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
        var sql = @"
            SELECT c.`Name` account,
                   IFNULL(c.OnlineName,'') onlineName,
                   (c.Online=1) isOnline,
                   IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip,
                   (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold,
                   IFNULL(c.PetPoint,0) crystal,
                   IFNULL(pet.cnt,0) petCount
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            LEFT JOIN (SELECT cdkey, COUNT(*) AS cnt FROM capturepet GROUP BY cdkey) pet
                   ON pet.cdkey=c.`Name`
            WHERE c.`Name` LIKE @kw OR c.OnlineName LIKE @kw
            ORDER BY c.Online DESC, c.LoginTime DESC
            LIMIT @lim";
        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@kw", $"%{kw}%");
        cmd.Parameters.AddWithValue("@lim", limit);
        var list = new List<PlayerRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapRow(r));
        return list;
    }

    // ── 玩家詳情 ─────────────────────────────────────────────
    public async Task<PlayerDetail?> GetDetailAsync(string account)
    {
        await using var db = Open();
        await db.OpenAsync();
        var sql = @"
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
        await using var cmd = new MySqlCommand(sql, db);
        cmd.Parameters.AddWithValue("@acc", account);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        var d = new PlayerDetail
        {
            Account    = r.GetString("account"),
            OnlineName = r.GetString("onlineName"),
            IsOnline   = r.GetBoolean("isOnline"),
            ServerId   = r.GetInt32("serverId"),
            RegTime    = r.GetString("regTime"),
            LoginTime  = r.GetString("loginTime"),
            IP         = r.GetString("ip"),
            RegIP      = r.GetString("regIP"),
            IsBanned   = r.GetBoolean("isBanned"),
            Gold       = r.GetInt64("gold"),
            Crystal    = r.GetInt64("crystal"),
            Uid        = r.GetString("uid"),
            MAC        = r.GetString("mac"),
            IsMuted    = r.GetBoolean("isMuted"),
            PayTotal   = r.GetInt64("payTotal"),
            TotalMails = r.GetInt32("totalMails"),
            UnreadMails= r.GetInt32("unreadMails"),
            PetCount   = r.GetInt32("petCount"),
        };
        // 解析封號時間
        if (d.IsBanned)
        {
            long banTime = r.GetInt64("banTime");
            d.BanEndTime = banTime == 0 ? "\u6C38\u4E45" :
                DateTimeOffset.FromUnixTimeSeconds(banTime).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
        }
        return d;
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
    public async Task<bool> SetBanAsync(string account, bool ban, int days = 0)
    {
        await using var db = Open(); await db.OpenAsync();
        if (ban)
        {
            long endUnix = days > 0
                ? DateTimeOffset.Now.AddDays(days).ToUnixTimeSeconds()
                : 0;  // 0 = 永久
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

    // ── 線上玩家 ─────────────────────────────────────────────
    public async Task<List<PlayerRow>> GetOnlineAsync()
    {
        await using var db = Open(); await db.OpenAsync();
        var sql = @"
            SELECT c.`Name` account,
                   IFNULL(c.OnlineName,'') onlineName,
                   1 isOnline,
                   IFNULL(c.ServerId,0) serverId,
                   IFNULL(DATE_FORMAT(c.created_at,'%Y-%m-%d %H:%i'),'') regTime,
                   IFNULL(DATE_FORMAT(c.LoginTime,'%Y-%m-%d %H:%i'),'') loginTime,
                   IFNULL(c.IP,'') ip,
                   (lk.Name IS NOT NULL) isBanned,
                   IFNULL(c.VipPoint,0) gold,
                   IFNULL(c.PetPoint,0) crystal,
                   0 petCount
            FROM csalogin c
            LEFT JOIN `lock` lk ON lk.`Name`=c.`Name`
            WHERE c.Online=1
            ORDER BY c.ServerId";
        await using var cmd = new MySqlCommand(sql, db);
        var list = new List<PlayerRow>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapRow(r));
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

    private static PlayerRow MapRow(MySqlDataReader r) => new()
    {
        Account    = r.GetString("account"),
        OnlineName = r.GetString("onlineName"),
        IsOnline   = r.GetBoolean("isOnline"),
        ServerId   = r.GetInt32("serverId"),
        RegTime    = r.GetString("regTime"),
        LoginTime  = r.GetString("loginTime"),
        IP         = r.GetString("ip"),
        IsBanned   = r.GetBoolean("isBanned"),
        Gold       = r.GetInt64("gold"),
        Crystal    = r.GetInt64("crystal"),
        PetCount   = r.GetInt32("petCount"),
    };
}
