using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

/// <summary>
/// 外部 API — 供官網後台呼叫，使用 API Key 驗證（不需要 JWT）。
/// 
/// 驗證方式：Request Header 帶入
///   X-Api-Key: {appsettings ExternalApi.ApiKey 的值}
/// </summary>
[ApiController, Route("api/external")]
public class ExternalController : ControllerBase
{
    private readonly DbService    _db;
    private readonly IConfiguration _cfg;

    public ExternalController(DbService db, IConfiguration cfg)
    {
        _db  = db;
        _cfg = cfg;
    }

    // ── 驗證 API Key ──────────────────────────────────────
    private bool IsAuthorized()
    {
        var configKey = _cfg["ExternalApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(configKey)) return false;   // 未設定 = 停用

        Request.Headers.TryGetValue("X-Api-Key", out var headerKey);
        return headerKey.ToString() == configKey;
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 官網後台「確認儲值」呼叫此端點。
    /// POST /api/external/recharge
    /// Header: X-Api-Key: {key}
    /// Body JSON: { account, twdAmount, goldAmount, giveGold, updatePaydata, orderNo, remark }
    /// </summary>
    [HttpPost("recharge")]
    public async Task<IActionResult> Recharge([FromBody] ExternalRechargeRequest req)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "API Key 錯誤或未設定" });

        if (string.IsNullOrWhiteSpace(req.Account))
            return BadRequest(new { message = "account 不可為空" });

        if (req.TwdAmount <= 0)
            return BadRequest(new { message = "twdAmount 須大於 0" });

        if (req.GiveGold && req.GoldAmount < 0)
            return BadRequest(new { message = "goldAmount 不可為負" });

        var account = req.Account.Trim();

        // 冪等：同一 orderNo 已處理過則直接回成功，避免付款回調重試造成重複入帳
        if (!string.IsNullOrWhiteSpace(req.OrderNo) && await _db.RechargeOrderExistsAsync(req.OrderNo.Trim()))
            return Ok(new
            {
                success    = true,
                duplicated = true,
                message    = $"訂單 {req.OrderNo.Trim()} 已處理過，未重複入帳",
                account,
                orderNo    = req.OrderNo.Trim(),
            });

        var ok = await _db.AdjustPayDataPointAsync(
            account, req.TwdAmount, req.GoldAmount, req.GiveGold, req.UpdatePaydata);

        if (!ok)
            return NotFound(new
            {
                message = $"找不到玩家「{account}」，請確認帳號是否正確"
            });

        // ★ 寫入充值訂單記錄（讓充值記錄查詢可見）
        string orderNo = string.IsNullOrWhiteSpace(req.OrderNo)
            ? $"EXT-{DateTime.UtcNow:yyyyMMddHHmmss}-{(account.Length > 8 ? account[..8] : account)}"
            : req.OrderNo;
        string remark = string.IsNullOrWhiteSpace(req.Remark) ? "" : $"（{req.Remark}）";
        string prodName = req.GiveGold
            ? $"官網充值 NT${req.TwdAmount:N0} / {req.GoldAmount:N0}元寶{remark}"
            : $"官網累儲 NT${req.TwdAmount:N0}{remark}";
        long yuanbao = req.GiveGold ? req.GoldAmount : req.TwdAmount * 100;
        await _db.WriteRechargeOrderAsync(account, orderNo, prodName, yuanbao);

        return Ok(new
        {
            success = true,
            message = $"✓ 已為玩家「{account}」入帳 NT${req.TwdAmount:0}" +
                      (req.GiveGold ? $"，發放 {req.GoldAmount:N0} 元寶" : "（僅更新累積進度）"),
            account    = account,
            twdAmount  = req.TwdAmount,
            goldAmount = req.GiveGold ? req.GoldAmount : 0,
            orderNo    = orderNo,
        });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 查詢玩家基本資料（用於官網確認前顯示帳號正確性）。
    /// GET /api/external/player/{account}
    /// Header: X-Api-Key: {key}
    /// </summary>
    [HttpGet("player/{account}")]
    public async Task<IActionResult> GetPlayer(string account)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "API Key 錯誤或未設定" });

        if (string.IsNullOrWhiteSpace(account))
            return BadRequest(new { message = "account 不可為空" });

        dynamic? info = await _db.GetPaydataSummaryAsync(account.Trim());
        if (info == null)
            return NotFound(new { message = $"找不到玩家「{account}」" });

        return Ok(new
        {
            account      = (string)info.account,
            charName     = (string)info.onlineName,
            masterName   = (string)info.masterName,
            isOnline     = (bool)info.isOnline,
            vipLevel     = (int)info.vipLevel,
            payTotal     = (long)info.payTotal,
            paydataPoint = (long)info.paydataPoint,
            gold         = (long)info.gold,
        });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 查詢主帳號底下的所有遊戲角色（供官網會員中心綁定帳號用）。
    /// GET /api/external/master/{masterName}
    /// Header: X-Api-Key: {key}
    /// </summary>
    [HttpGet("master/{masterName}")]
    public async Task<IActionResult> GetMaster(string masterName)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "API Key 錯誤或未設定" });

        if (string.IsNullOrWhiteSpace(masterName))
            return BadRequest(new { message = "masterName 不可為空" });

        var result = await _db.GetMasterAsync(masterName.Trim());
        if (result == null)
            return NotFound(new { message = $"找不到主帳號「{masterName}」，請確認輸入是否正確" });

        return Ok(result);
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 主帳號分配儲值（官網後台用）。
    /// 管理員選擇儲值主帳號後，可將一筆儲值分配給旗下多個 CDKEY，各別輸入台幣金額與金幣，累積儲值會依台幣正確計算。
    /// POST /api/external/master-split-recharge
    /// Header: X-Api-Key: {key}
    /// Body JSON: [ { "account": "cdkey1", "twdAmount": 200, "goldAmount": 23000, "giveGold": true }, ... ]
    /// </summary>
    [HttpPost("master-split-recharge")]
    public async Task<IActionResult> MasterSplitRecharge([FromBody] List<SplitRechargeItem> items)
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "API Key 錯誤或未設定" });

        if (items == null || items.Count == 0)
            return BadRequest(new { message = "清單為空" });

        int done = 0;
        var results = new List<object>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Account) || item.TwdAmount <= 0)
            {
                results.Add(new { account = item.Account ?? "", ok = false, msg = "台幣金額須 > 0" });
                continue;
            }
            var acc2 = item.Account.Trim();
            var ok = await _db.AdjustPayDataPointAsync(acc2, item.TwdAmount, item.GoldAmount, item.GiveGold);
            if (ok)
            {
                done++;
                string ord2  = $"EXT-{DateTime.UtcNow:yyyyMMddHHmmss}-{(acc2.Length > 8 ? acc2[..8] : acc2)}";
                string prod2 = item.GiveGold
                    ? $"官網分配 NT${item.TwdAmount:N0} / {item.GoldAmount:N0}元寶"
                    : $"官網分配累儲 NT${item.TwdAmount:N0}";
                await _db.WriteRechargeOrderAsync(acc2, ord2, prod2, item.GiveGold ? item.GoldAmount : item.TwdAmount * 100);
            }
            results.Add(new { account = item.Account, ok, msg = ok ? "✓ 成功" : "✗ 失敗" });
        }
        return Ok(new { done, total = items.Count, results });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 驗證 API Key 是否正確（測試用）。
    /// GET /api/external/ping
    /// Header: X-Api-Key: {key}
    /// </summary>
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        if (!IsAuthorized())
            return Unauthorized(new { message = "API Key 錯誤或未設定" });

        return Ok(new { message = "✓ 蘇打石器 GM 外部 API 連線成功", time = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 取得伺服器統計數據（供官網報表統計頁面使用）。
    /// GET /api/external/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsAuthorized()) return Unauthorized(new { message = "API Key 錯誤或未設定" });
        var s = await _db.GetStatsAsync();
        return Ok(new {
            totalPlayers  = s.TotalPlayers,
            onlinePlayers = s.OnlinePlayers,
            bannedPlayers = s.BannedPlayers,
            newToday      = s.NewToday,
            totalGold     = s.TotalGold,
            totalCrystal  = s.TotalCrystal,
        });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 取得 VIP 玩家排行榜（前 20 名，依累積儲值排序）。
    /// GET /api/external/vip
    /// </summary>
    [HttpGet("vip")]
    public async Task<IActionResult> GetVip()
    {
        if (!IsAuthorized()) return Unauthorized(new { message = "API Key 錯誤或未設定" });
        var list = await _db.GetVipListAsync();
        var top  = list.Take(20).Select(v => new {
            account    = v.Account,
            charName   = v.OnlineName,
            masterName = v.MasterName,
            payTotal   = v.PayTotal,
            gold       = v.Gold,
            isOnline   = v.IsOnline,
            vipLevel   = v.VipLevel,
        });
        return Ok(top);
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 搜尋遊戲玩家（官網管理後台「遊戲玩家」tab 使用）。
    /// GET /api/external/players?q={query}
    /// </summary>
    [HttpGet("players")]
    public async Task<IActionResult> SearchPlayers([FromQuery] string q = "")
    {
        if (!IsAuthorized()) return Unauthorized(new { message = "API Key 錯誤或未設定" });
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "q 不可為空" });
        var rows = await _db.SearchPlayersAsync(q.Trim(), 30);
        var result = rows.Select(r => new {
            account    = r.Account,
            charName   = r.OnlineName,
            masterName = r.MasterName,
            gold       = r.Gold,
            payTotal   = r.PayTotal,
            vipLevel   = r.VipLevel,
            isOnline   = r.IsOnline,
            isBanned   = r.IsBanned,
            regTime    = r.RegTime,
            loginTime  = r.LoginTime,
        });
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 設定玩家金幣（官網管理後台直接操作）。
    /// PUT /api/external/player/{account}/gold
    /// Body: { "gold": 1000 }
    /// </summary>
    [HttpPut("player/{account}/gold")]
    public async Task<IActionResult> SetGold(string account, [FromBody] ExternalSetGoldRequest req)
    {
        if (!IsAuthorized()) return Unauthorized(new { message = "API Key 錯誤或未設定" });
        if (string.IsNullOrWhiteSpace(account)) return BadRequest(new { message = "account 不可為空" });
        if (req.Gold < 0) return BadRequest(new { message = "gold 不可為負" });
        var ok = await _db.SetGoldAsync(account.Trim(), req.Gold);
        if (!ok) return NotFound(new { message = $"找不到玩家「{account}」" });
        return Ok(new { success = true, message = $"✓ 玩家「{account}」金幣已設定為 {req.Gold:N0}", account, gold = req.Gold });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 封號或解封玩家（官網管理後台直接操作）。
    /// POST /api/external/player/{account}/ban
    /// Body: { "ban": true, "days": 0 }  // days=0 永久封號
    /// </summary>
    [HttpPost("player/{account}/ban")]
    public async Task<IActionResult> BanPlayer(string account, [FromBody] ExternalBanRequest req)
    {
        if (!IsAuthorized()) return Unauthorized(new { message = "API Key 錯誤或未設定" });
        if (string.IsNullOrWhiteSpace(account)) return BadRequest(new { message = "account 不可為空" });
        var ok = await _db.SetBanAsync(account.Trim(), req.Ban, req.Days);
        if (!ok) return NotFound(new { message = $"找不到玩家「{account}」" });
        string msg = req.Ban
            ? $"✓ 玩家「{account}」已{(req.Days > 0 ? $"封號 {req.Days} 天" : "永久封號")}"
            : $"✓ 玩家「{account}」已解封";
        return Ok(new { success = true, message = msg, account, ban = req.Ban, days = req.Days });
    }

    // ─────────────────────────────────────────────────────
    /// <summary>
    /// 發送站內文字信件給指定玩家（官網管理後台直接操作）。
    /// POST /api/external/player/{account}/mail
    /// Body: { "title": "標題", "content": "內容" }
    /// </summary>
    [HttpPost("player/{account}/mail")]
    public async Task<IActionResult> SendMail(string account, [FromBody] ExternalSendMailRequest req)
    {
        if (!IsAuthorized()) return Unauthorized(new { message = "API Key 錯誤或未設定" });
        if (string.IsNullOrWhiteSpace(account)) return BadRequest(new { message = "account 不可為空" });
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest(new { message = "title 不可為空" });
        if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest(new { message = "content 不可為空" });
        var ok = await _db.SendTextMailAsync(account.Trim(), req.Title, req.Content);
        if (!ok) return NotFound(new { message = $"找不到玩家「{account}」或發送失敗" });
        return Ok(new { success = true, message = $"✓ 已發送信件給玩家「{account}」", account });
    }
}
