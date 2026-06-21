using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Route("api/players/{account}/history")]
[Authorize]
public class PlayerHistoryController : ControllerBase
{
    private readonly DbService _db;
    public PlayerHistoryController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(string account, [FromQuery] int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(account)) return BadRequest("account required");
        var result = await _db.GetPlayerHistoryAsync(account.Trim(), Math.Clamp(limit, 10, 500));
        return Ok(result);
    }
}
