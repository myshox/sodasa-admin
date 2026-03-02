using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/server-status"), Authorize]
public class ServerStatusController : ControllerBase
{
    private readonly DbService _db;
    public ServerStatusController(DbService db) => _db = db;

    [HttpGet("recent-registrations")]
    public async Task<IActionResult> RecentRegistrations([FromQuery] int limit = 30)
        => Ok(await _db.GetRecentRegistrationsAsync(Math.Min(limit, 200)));

    [HttpGet("channel-online")]
    public async Task<IActionResult> ChannelOnline()
        => Ok(await _db.GetChannelOnlineCountAsync());

    [HttpGet("master-stats")]
    public async Task<IActionResult> MasterStats()
        => Ok(await _db.GetMasterAccountStatsAsync());
}
