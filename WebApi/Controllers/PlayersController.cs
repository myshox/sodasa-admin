using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/players"), Authorize]
public class PlayersController : ControllerBase
{
    private readonly DbService _db;
    public PlayersController(DbService db) => _db = db;

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<PlayerRow>());
        return Ok(await _db.SearchPlayersAsync(q, limit));
    }

    [HttpGet("online")]
    public async Task<IActionResult> Online() => Ok(await _db.GetOnlineAsync());

    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] int limit = 500)
        => Ok(await _db.GetPlayerListAsync(Math.Min(limit, 1000)));

    [HttpGet("banned")]
    public async Task<IActionResult> Banned([FromQuery] string q = "") => Ok(await _db.GetBannedListAsync(q));

    [HttpGet("master/{name}")]
    public async Task<IActionResult> Master(string name)
    {
        var r = await _db.GetMasterAsync(name);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpGet("recharge")]
    public async Task<IActionResult> Recharge([FromQuery] string q = "")
        => Ok(await _db.GetRechargeAsync(q));

    [HttpGet("goldlog")]
    public async Task<IActionResult> GoldLog([FromQuery] string q = "")
        => Ok(await _db.GetGoldLogAsync(q));

    [HttpGet("mail")]
    public async Task<IActionResult> Mail([FromQuery] string q = "")
        => Ok(await _db.GetMailAsync(q));

    [HttpGet("tradelog")]
    public async Task<IActionResult> TradeLog([FromQuery] string q = "", [FromQuery] int limit = 300)
        => Ok(await _db.GetTradeLogAsync(q, limit));

    [HttpGet("vip")]
    public async Task<IActionResult> Vip() => Ok(await _db.GetVipListAsync());

    [HttpPost("batch-gold")]
    public async Task<IActionResult> BatchGold([FromBody] BatchGoldRequest req)
    {
        var (done, fail) = await _db.BatchGoldAsync(
            req.Target ?? "online",
            req.CustomList ?? "",
            req.AccountIds ?? "",
            req.Amount);
        return Ok(new { done, fail });
    }

    [HttpPost("batch-mail")]
    public async Task<IActionResult> BatchMail([FromBody] BatchMailRequest req)
    {
        var count = await _db.BatchMailAsync(req.Target, req.CustomList, req.Title, req.Content);
        return Ok(new { count });
    }

    [HttpPost("batch-send-cart")]
    public async Task<IActionResult> BatchSendCart([FromBody] BatchSendCartRequest req)
    {
        if (req.Cart == null || req.Cart.Count == 0)
            return BadRequest(new { message = "購物車為空" });
        var count = await _db.BatchSendCartAsync(req.Target, req.CustomList, req.Cart, req.Title, req.Content);
        return Ok(new { count, message = $"已發送 {count} 筆道具郵件" });
    }

    [HttpPost("send-item")]
    public async Task<IActionResult> SendItem([FromBody] SendItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Account)) return BadRequest(new { message = "請指定玩家帳號" });
        var (success, fail) = await _db.SendItemMailAsync(
            req.Account.Trim(), req.ItemId, req.Quantity <= 0 ? 1 : req.Quantity,
            req.Title ?? "", req.Content ?? "");
        return Ok(new { success, fail, message = $"已發送 {success} 封道具郵件" + (fail > 0 ? $"，失敗 {fail}" : "") });
    }

    [HttpGet("{account}")]
    public async Task<IActionResult> Detail(string account)
    {
        var d = await _db.GetDetailAsync(account);
        return d == null ? NotFound() : Ok(d);
    }

    [HttpPut("{account}/gold")]
    public async Task<IActionResult> SetGold(string account, [FromBody] SetCurrencyRequest req)
    {
        var ok = await _db.SetGoldAsync(account, req.Value);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u66F4\u65B0" }) : BadRequest();
    }

    [HttpPut("{account}/crystal")]
    public async Task<IActionResult> SetCrystal(string account, [FromBody] SetCurrencyRequest req)
    {
        var ok = await _db.SetCrystalAsync(account, req.Value);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u66F4\u65B0" }) : BadRequest();
    }

    /// <summary>給予儲值：更新 paydata 累積進度，可選同時發放金幣（與 EXE 一致）</summary>
    [HttpPost("{account}/recharge")]
    public async Task<IActionResult> Recharge(string account, [FromBody] RechargeRequest req)
    {
        if (req.TwdAmount <= 0)
            return BadRequest(new { message = "台幣金額須大於 0" });
        if (req.GiveGold && req.GoldAmount < 0)
            return BadRequest(new { message = "金幣數量不可為負" });
        var ok = await _db.AdjustPayDataPointAsync(account, req.TwdAmount, req.GoldAmount, req.GiveGold, req.UpdatePaydata);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u7D66\u4E88\u5132\u503C" }) : BadRequest(new { message = "\u4FEE\u6539\u5931\u6557" });
    }

    /// <summary>主帳號分配儲值：為旗下多個 CDKEY 各別執行儲值</summary>
    [HttpPost("master-split-recharge")]
    public async Task<IActionResult> MasterSplitRecharge([FromBody] List<SplitRechargeItem> items)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { message = "清單為空" });
        int done = 0;
        var results = new List<object>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Account) || item.TwdAmount <= 0)
            {
                results.Add(new { account = item.Account, ok = false, msg = "台幣金額須 > 0" });
                continue;
            }
            var ok = await _db.AdjustPayDataPointAsync(
                item.Account.Trim(), item.TwdAmount, item.GoldAmount, item.GiveGold);
            if (ok) done++;
            results.Add(new { account = item.Account, ok, msg = ok ? "✓ 成功" : "✗ 失敗" });
        }
        return Ok(new { done, total = items.Count, results });
    }

    [HttpPost("{account}/ban")]
    public async Task<IActionResult> Ban(string account, [FromBody] BanRequest req)
    {
        var ok = await _db.SetBanAsync(account, req.Ban, req.Days, req.Hours);
        return ok ? Ok(new { message = req.Ban ? "\u2713 \u5DF2\u5C01\u865F" : "\u2713 \u5DF2\u89E3\u5C01" }) : BadRequest();
    }

    [HttpPost("{account}/rename")]
    public async Task<IActionResult> Rename(string account, [FromBody] RenameRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewName)) return BadRequest(new { message = "請輸入新名稱" });
        var ok = await _db.RenamePlayerAsync(account, req.NewName.Trim());
        return ok ? Ok(new { message = "\u2713 \u6539\u540D\u6210\u529F" }) : BadRequest(new { message = "\u6539\u540D\u5931\u6557" });
    }

    [HttpPost("{account}/force-offline")]
    public async Task<IActionResult> ForceOffline(string account)
    {
        var ok = await _db.ForceOfflineAsync(account);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u5F37\u5236\u4E0B\u7DDA" }) : BadRequest();
    }

    [HttpPost("{account}/mute")]
    public async Task<IActionResult> Mute(string account, [FromBody] MuteRequest req)
    {
        var ok = await _db.SetMuteAsync(account, req.Mute);
        return ok ? Ok(new { message = req.Mute ? "\u2713 \u5DF2\u7981\u8A00" : "\u2713 \u5DF2\u89E3\u9664\u7981\u8A00" }) : BadRequest();
    }

    [HttpPost("{account}/paydata/reset")]
    public async Task<IActionResult> ResetPaydata(string account)
    {
        var ok = await _db.ResetPaydataAsync(account);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u91CD\u7F6E\u5132\u5024\u9032\u5EA6" }) : BadRequest();
    }

    [HttpPost("{account}/paydata/fix")]
    public async Task<IActionResult> FixPaydata(string account)
    {
        var ok = await _db.FixPaydataCheckAsync(account);
        return ok ? Ok(new { message = "✓ 已修復循環顯示" }) : BadRequest(new { message = "修復失敗（可能無 paydata 記錄）" });
    }

    /// <summary>發放累積獎勵（設 check=1，防呆：check 必須為 0 才允許）</summary>
    [HttpPost("{account}/paydata/claim")]
    public async Task<IActionResult> ClaimPaydataReward(string account)
    {
        var result = await _db.ClaimPaydataRewardAsync(account);
        return result switch
        {
            "ok"             => Ok(new { message = "✓ 已標記獎勵已發放（第 " + result + " 輪）" }),
            "already_claimed"=> BadRequest(new { message = "⚠ 此輪獎勵已發放，無法重複領取" }),
            "no_cycle"       => BadRequest(new { message = "⚠ 尚未完成任何循環，無獎勵可發放" }),
            "not_found"      => BadRequest(new { message = "找不到玩家 paydata 記錄" }),
            _                => Ok(new { message = $"✓ 獎勵已發放（輪次 #{result}）" })
        };
    }

    [HttpGet("{account}/paydata")]
    public async Task<IActionResult> GetPaydata(string account)
        => Ok(await _db.GetPaydataSummaryAsync(account));

    [HttpPost("send-cart")]
    public async Task<IActionResult> SendCart([FromBody] SendCartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Account)) return BadRequest(new { message = "請指定玩家帳號" });
        if (req.Cart == null || req.Cart.Count == 0) return BadRequest(new { message = "購物車為空" });
        var (success, fail) = await _db.SendCartMailAsync(req.Account.Trim(), req.Cart, req.Title, req.Content);
        return Ok(new { success, fail, message = $"已發送 {success} 筆" + (fail > 0 ? $"，失敗 {fail}" : "") });
    }

    [HttpGet("{account}/mail-history")]
    public async Task<IActionResult> MailHistory(string account)
        => Ok(await _db.GetPlayerMailHistoryAsync(account));
}

[ApiController, Route("api/stats"), Authorize]
public class StatsController : ControllerBase
{
    private readonly DbService _db;
    public StatsController(DbService db) => _db = db;
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.GetStatsAsync());
}

[ApiController, Route("api/gmlog"), Authorize]
public class GmLogController : ControllerBase
{
    [HttpGet("dates")]
    public IActionResult Dates()
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return Ok(new List<string>());
        var dates = Directory.GetFiles(logDir, "*.log")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderByDescending(x => x).ToList();
        return Ok(dates);
    }

    [HttpGet]
    public IActionResult Get([FromQuery] int offset = 0, [FromQuery] int limit = 100,
        [FromQuery] string q = "", [FromQuery] string date = "")
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return Ok(new List<object>());
        var entries = new List<object>();
        IEnumerable<string> files = Directory.GetFiles(logDir, "*.log").OrderByDescending(x => x);
        if (!string.IsNullOrWhiteSpace(date))
            files = files.Where(f => Path.GetFileNameWithoutExtension(f) == date);
        foreach (var f in files)
        {
            foreach (var line in System.IO.File.ReadAllLines(f).Reverse())
            {
                try {
                    var doc = System.Text.Json.JsonDocument.Parse(line);
                    var r   = doc.RootElement;
                    bool success = !r.TryGetProperty("Success", out var sc) || sc.GetBoolean();
                    string action = r.TryGetProperty("Action", out var ac) ? ac.GetString() ?? "" : "";
                    string target = r.TryGetProperty("Target", out var tg) ? tg.GetString() ?? "" : "";
                    string detail = r.TryGetProperty("Detail", out var dt) ? dt.GetString() ?? "" : "";
                    string gmUser = r.TryGetProperty("Operator", out var op) ? op.GetString() ?? "GM" : "GM";
                    string time   = r.TryGetProperty("Time",     out var tm) ? tm.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        string combined = $"{action} {target} {detail} {gmUser}";
                        if (!combined.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    entries.Add(new { id = entries.Count + 1, gmUser, action, target, detail, time, success });
                } catch { }
            }
        }
        int total = entries.Count;
        return Ok(new { total, items = entries.Skip(offset).Take(limit).ToList() });
    }

    [HttpGet("export")]
    public IActionResult Export([FromQuery] string date = "")
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return NotFound();
        IEnumerable<string> files = Directory.GetFiles(logDir, "*.log").OrderByDescending(x => x);
        if (!string.IsNullOrWhiteSpace(date))
            files = files.Where(f => Path.GetFileNameWithoutExtension(f) == date);
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("時間\tGM\t結果\t操作\t對象\t詳情");
        foreach (var f in files)
        {
            foreach (var line in System.IO.File.ReadAllLines(f))
            {
                try {
                    var doc = System.Text.Json.JsonDocument.Parse(line);
                    var r   = doc.RootElement;
                    bool ok   = !r.TryGetProperty("Success", out var sc) || sc.GetBoolean();
                    string tm = r.TryGetProperty("Time",     out var t)  ? t.GetString()  ?? "" : "";
                    string op = r.TryGetProperty("Operator", out var o)  ? o.GetString()  ?? "" : "";
                    string ac = r.TryGetProperty("Action",   out var a)  ? a.GetString()  ?? "" : "";
                    string tg = r.TryGetProperty("Target",   out var tgt)? tgt.GetString()?? "" : "";
                    string dt = r.TryGetProperty("Detail",   out var d)  ? d.GetString()  ?? "" : "";
                    lines.AppendLine($"{tm}\t{op}\t{(ok?"✓":"✗")}\t{ac}\t{tg}\t{dt}");
                } catch { }
            }
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(lines.ToString());
        string fname = string.IsNullOrWhiteSpace(date) ? "gmlog_all.txt" : $"gmlog_{date}.txt";
        return File(bytes, "text/plain; charset=utf-8", fname);
    }
}

public class BatchMailRequest
{
    public string Target     { get; set; } = "online";
    public string CustomList { get; set; } = "";
    public string Title      { get; set; } = "";
    public string Content    { get; set; } = "";
}
