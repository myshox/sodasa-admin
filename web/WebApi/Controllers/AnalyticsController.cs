using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/analytics"), Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly DbService _db;
    public AnalyticsController(DbService db) => _db = db;

    [HttpGet("recharge/kpi")]
    public async Task<IActionResult> RechargeKpi()
    {
        var (today, month, total, paying) = await _db.GetRechargeKpiAsync();
        return Ok(new { todayRevenue = today, monthRevenue = month, totalRevenue = total, payingPlayers = paying });
    }

    [HttpGet("recharge/daily")]
    public async Task<IActionResult> RechargeDaily([FromQuery] int days = 30)
    {
        var (dates, amounts, counts) = await _db.GetDailyRechargeAsync(days);
        return Ok(new { dates = dates.Select(d => d.ToString("yyyy-MM-dd")).ToArray(), amounts, counts });
    }

    [HttpGet("recharge/monthly")]
    public async Task<IActionResult> RechargeMonthly()
    {
        var (months, amounts, counts) = await _db.GetMonthlyRechargeAsync();
        return Ok(new { months, amounts, counts });
    }

    [HttpGet("recharge/tier")]
    public async Task<IActionResult> RechargeTier() => Ok(await _db.GetPaymentTierAsync());

    [HttpGet("recharge/firstpay")]
    public async Task<IActionResult> RechargeFirstPay() => Ok(await _db.GetTimeToFirstPaymentAsync());

    [HttpGet("player/stats")]
    public async Task<IActionResult> PlayerStats()
    {
        var stats = await _db.GetStatsAsync();
        var todayActive = await _db.GetTodayActiveCountAsync();
        return Ok(new { stats.TotalPlayers, stats.OnlinePlayers, stats.NewToday, todayActive });
    }

    [HttpGet("player/hour")]
    public async Task<IActionResult> PlayerHour() => Ok(await _db.GetLoginHourDistributionAsync());

    [HttpGet("player/weekday")]
    public async Task<IActionResult> PlayerWeekday() => Ok(await _db.GetLoginWeekdayDistributionAsync());

    [HttpGet("player/growth")]
    public async Task<IActionResult> PlayerGrowth([FromQuery] int days = 30)
    {
        var (dates, counts) = await _db.GetDailyNewAccountsAsync(days);
        return Ok(new { dates = dates.Select(d => d.ToString("yyyy-MM-dd")).ToArray(), counts });
    }

    [HttpGet("player/retention")]
    public async Task<IActionResult> PlayerRetention() => Ok(await _db.GetRetentionAsync());

    [HttpGet("player/inactive")]
    public async Task<IActionResult> PlayerInactive([FromQuery] int days = 30, [FromQuery] int limit = 200)
        => Ok(await _db.GetInactivePlayersAsync(days, limit));
}
