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

    [HttpPut("users/{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetAdminStatusRequest req)
    {
        var ok = await _db.SetAdminStatusAsync(id, req.Enabled);
        return ok ? Ok(new { message = req.Enabled ? "已啟用" : "已停用" })
                  : BadRequest(new { message = "操作失敗或不可修改 admin" });
    }

    [HttpPut("users/{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetAdminPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "密碼不可為空" });
        var ok = await _db.ResetAdminPasswordAsync(id, req.NewPassword);
        return ok ? Ok(new { message = "密碼已重設" }) : BadRequest(new { message = "重設失敗" });
    }
}
