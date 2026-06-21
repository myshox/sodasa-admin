using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/recycle"), Authorize]
public class RecycleController : ControllerBase
{
    private readonly DbService _db;
    public RecycleController(DbService db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.GetRecycleBinAsync());

    [HttpPost("restore/{id:int}")]
    public async Task<IActionResult> Restore(int id)
    {
        var (ok, msg) = await _db.RestoreFromRecycleAsync(id);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }
}
