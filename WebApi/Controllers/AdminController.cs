using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/admin"), Authorize]
public class AdminController : ControllerBase
{
    private readonly DbService _db;
    public AdminController(DbService db) => _db = db;

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() => Ok(await _db.GetAdminUsersAsync());

    [HttpPost("users")]
    public async Task<IActionResult> AddUser([FromBody] AddAdminUserRequest req)
    {
        var (ok, msg) = await _db.AddAdminUserAsync(req.Username ?? "", req.Password ?? "", req.Nickname ?? "");
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var ok = await _db.DeleteAdminUserAsync(id);
        return ok ? Ok(new { message = "已刪除" }) : BadRequest(new { message = "刪除失敗或不可刪除 admin" });
    }
}
