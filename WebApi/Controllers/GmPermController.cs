using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/gmperm"), Authorize]
public class GmPermController : ControllerBase
{
    private readonly DbService _db;
    public GmPermController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string q = "")
        => Ok(await _db.GetGmPermListAsync(q));

    [HttpPut("{account}")]
    public async Task<IActionResult> Set(string account, [FromBody] SetGmPermRequest req)
    {
        var ok = await _db.SetPlayerPermAsync(account, req.NeiCe, req.GroupId);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u66F4\u65B0\u6B0A\u9650" }) : BadRequest(new { message = "\u4FEE\u6539\u5931\u6557" });
    }
}
