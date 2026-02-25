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
    public async Task<IActionResult> Online() =>
        Ok(await _db.GetOnlineAsync());

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
        return ok ? Ok(new { message = "✓ 已更新" }) : BadRequest(new { message = "更新失敗" });
    }

    [HttpPut("{account}/crystal")]
    public async Task<IActionResult> SetCrystal(string account, [FromBody] SetCurrencyRequest req)
    {
        var ok = await _db.SetCrystalAsync(account, req.Value);
        return ok ? Ok(new { message = "✓ 已更新" }) : BadRequest(new { message = "更新失敗" });
    }

    [HttpPost("{account}/ban")]
    public async Task<IActionResult> Ban(string account, [FromBody] BanRequest req)
    {
        var ok = await _db.SetBanAsync(account, req.Ban, req.Days);
        return ok ? Ok(new { message = req.Ban ? "✓ 已封號" : "✓ 已解封" }) : BadRequest();
    }
}

[ApiController, Route("api/stats"), Authorize]
public class StatsController : ControllerBase
{
    private readonly DbService _db;
    public StatsController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.GetStatsAsync());
}
