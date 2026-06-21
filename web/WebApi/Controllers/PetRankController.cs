using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/petrank")]
public class PetRankController : ControllerBase
{
    private readonly DbService _db;
    public PetRankController(DbService db) => _db = db;

    /// <summary>取得寵物總排行榜（petbilling 動態表）</summary>
    [HttpGet("billing")]
    public async Task<IActionResult> GetBilling([FromQuery] int limit = 2000) =>
        Ok(await _db.GetPetBillingRankAsync(limit));

    /// <summary>取得所有練寵種類（id, name, 參賽數, 最高分）</summary>
    [HttpGet("pets")]
    public async Task<IActionResult> GetPets() =>
        Ok(await _db.GetPetRankTypesAsync());

    /// <summary>取得指定寵物 id 的排行榜。mode=best 每人最高分一筆；mode=raw 全部提交列依戰力（與技術直接查表排序一致）</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int petId, [FromQuery] int limit = 50, [FromQuery] string mode = "best") =>
        Ok(await _db.GetPetLeaderboardAsync(petId, limit, mode));

    /// <summary>查詢某玩家（帳號/角色名）的所有練寵記錄</summary>
    [HttpGet("player/{account}")]
    public async Task<IActionResult> GetPlayerEntries(string account) =>
        Ok(await _db.GetPlayerPetEntriesAsync(account));

    /// <summary>切換某筆記錄的審核狀態</summary>
    [HttpPut("{unicode}/check")]
    public async Task<IActionResult> SetCheck(string unicode, [FromBody] bool check)
    {
        var ok = await _db.SetPetCheckAsync(unicode, check);
        return ok ? Ok() : NotFound();
    }

    /// <summary>刪除某筆練寵記錄（作弊/無效刷榜）</summary>
    [HttpDelete("{unicode}")]
    public async Task<IActionResult> Delete(string unicode)
    {
        var ok = await _db.DeletePetEntryAsync(unicode);
        return ok ? Ok() : NotFound();
    }
}
