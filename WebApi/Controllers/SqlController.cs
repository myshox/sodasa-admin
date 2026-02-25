using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/sql"), Authorize]
public class SqlController : ControllerBase
{
    private readonly DbService _db;
    public SqlController(DbService db) => _db = db;

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] SqlQueryRequest req)
    {
        var (ok, rows, error) = await _db.ExecuteReadOnlyQueryAsync(req?.Sql ?? "");
        if (!ok) return BadRequest(new { error, rows = (object?)null });
        return Ok(new { rows, error = (string?)null });
    }
}
