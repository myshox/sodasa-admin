using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/guild"), Authorize]
public class GuildController : ControllerBase
{
    private readonly DbService _db;
    public GuildController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetList() =>
        Ok(await _db.GetGuildListAsync());

    [HttpGet("{guildId:int}/members")]
    public async Task<IActionResult> GetMembers(int guildId) =>
        Ok(await _db.GetGuildMembersAsync(guildId));

    [HttpDelete("{guildId:int}")]
    public async Task<IActionResult> Dissolve(int guildId)
    {
        var (ok, msg) = await _db.DissolveGuildAsync(guildId);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }

    [HttpDelete("{guildId:int}/members/{cdkey}")]
    public async Task<IActionResult> KickMember(int guildId, string cdkey)
    {
        var (ok, msg) = await _db.KickGuildMemberAsync(guildId, cdkey);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }

    [HttpPost("members/transfer")]
    public async Task<IActionResult> TransferMember([FromBody] TransferMemberRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cdkey) || req.TargetGuildId <= 0)
            return BadRequest(new { message = "參數不完整" });
        var (ok, msg) = await _db.TransferGuildMemberAsync(req.Cdkey, req.TargetGuildId, req.TargetGuildName);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }
}
