using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/tradeaudit"), Authorize]
public class TradeAuditController : ControllerBase
{
    private readonly DbService _db;
    public TradeAuditController(DbService db) => _db = db;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var (total, pairs, suspicious, sameIp) = await _db.GetTradeAuditSummaryAsync();
        return Ok(new { totalTrades = total, uniquePairs = pairs, suspiciousPairs = suspicious, sameIpPairs = sameIp });
    }

    [HttpGet("frequent")]
    public async Task<IActionResult> Frequent([FromQuery] int minCount = 10)
        => Ok(await _db.GetFrequentTradePairsAsync(minCount));

    [HttpGet("sameip")]
    public async Task<IActionResult> SameIp([FromQuery] int minCount = 5)
        => Ok(await _db.GetSameIpTradesAsync(minCount));

    [HttpGet("gold")]
    public async Task<IActionResult> Gold([FromQuery] int limit = 50)
        => Ok(await _db.GetGoldAnomalyAsync(limit));

    [HttpGet("traders")]
    public async Task<IActionResult> Traders([FromQuery] int limit = 50)
        => Ok(await _db.GetTopTradersAsync(limit));
}
