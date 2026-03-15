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

    [HttpGet("shared-ip")]
    public async Task<IActionResult> SharedIp([FromQuery] string account)
    {
        if (string.IsNullOrWhiteSpace(account)) return BadRequest(new { message = "請輸入帳號" });
        return Ok(await _db.GetSharedIpAsync(account.Trim()));
    }

    [HttpGet("ip-groups")]
    public async Task<IActionResult> IpGroups([FromQuery] int minGroup = 2, [FromQuery] int limit = 300)
        => Ok(await _db.GetIpGroupsAsync(Math.Max(2, minGroup), Math.Min(limit, 1000)));

    [HttpGet("ip-owner")]
    public async Task<IActionResult> IpOwner([FromQuery] string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return BadRequest(new { message = "請輸入 IP" });
        return Ok(await _db.GetIpOwnerAsync(ip.Trim()));
    }
}
