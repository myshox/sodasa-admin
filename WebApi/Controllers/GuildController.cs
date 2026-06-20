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

    private string Gm => string.IsNullOrWhiteSpace(User?.Identity?.Name) ? "GM" : User!.Identity!.Name!;
    private Task LogOp(string action, string target, string detail, bool success)
        => _db.WriteGmLogAsync(Gm, action, target, detail, success, "web");

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
        await LogOp("解散家族", $"家族 {guildId}", msg, ok);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }

    [HttpDelete("{guildId:int}/members/{cdkey}")]
    public async Task<IActionResult> KickMember(int guildId, string cdkey)
    {
        var (ok, msg) = await _db.KickGuildMemberAsync(guildId, cdkey);
        await LogOp("踢出家族成員", cdkey, $"家族 {guildId}", ok);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }

    [HttpPost("members/transfer")]
    public async Task<IActionResult> TransferMember([FromBody] TransferMemberRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cdkey) || req.TargetGuildId <= 0)
            return BadRequest(new { message = "參數不完整" });
        var (ok, msg) = await _db.TransferGuildMemberAsync(req.Cdkey, req.TargetGuildId, req.TargetGuildName);
        await LogOp("轉移家族成員", req.Cdkey, $"→ 家族「{req.TargetGuildName}」({req.TargetGuildId})", ok);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }
}
