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

        var ok = await _db.AdjustPayDataPointAsync(
            req.Account.Trim(),
            req.TwdAmount,
            req.GoldAmount,
            req.GiveGold,
            req.UpdatePaydata);

        if (!ok)
            return NotFound(new
            {
                message = $"找不到玩家「{req.Account}」，請確認帳號是否正確"
            });

        return Ok(new
        {
            success = true,
            message = $"✓ 已為玩家「{req.Account}」入帳 NT${req.TwdAmount:0}" +
                      (req.GiveGold ? $"，發放 {req.GoldAmount:N0} 元寶" : "（僅更新累積進度）"),
            account    = req.Account,
            twdAmount  = req.TwdAmount,
            goldAmount = req.GiveGold ? req.GoldAmount : 0,
            orderNo    = req.OrderNo,
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
}
