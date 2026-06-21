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

    private string Gm => string.IsNullOrWhiteSpace(User?.Identity?.Name) ? "GM" : User!.Identity!.Name!;
    private Task LogOp(string action, string target, string detail, bool success)
        => _db.WriteGmLogAsync(Gm, action, target, detail, success, "web");

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers() => Ok(await _db.GetAdminUsersAsync());

    [HttpPost("users")]
    public async Task<IActionResult> AddUser([FromBody] AddAdminUserRequest req)
    {
        var (ok, msg) = await _db.AddAdminUserAsync(req.Username ?? "", req.Password ?? "", req.Nickname ?? "");
        await LogOp("新增工具帳號", req.Username ?? "", req.Nickname ?? "", ok);
        return ok ? Ok(new { message = msg }) : BadRequest(new { message = msg });
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var ok = await _db.DeleteAdminUserAsync(id);
        await LogOp("刪除工具帳號", $"id {id}", "", ok);
        return ok ? Ok(new { message = "已刪除" }) : BadRequest(new { message = "刪除失敗或不可刪除 admin" });
    }

    [HttpPut("users/{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetAdminStatusRequest req)
    {
        var ok = await _db.SetAdminStatusAsync(id, req.Enabled);
        await LogOp(req.Enabled ? "啟用工具帳號" : "停用工具帳號", $"id {id}", "", ok);
        return ok ? Ok(new { message = req.Enabled ? "已啟用" : "已停用" })
                  : BadRequest(new { message = "操作失敗或不可修改 admin" });
    }

    [HttpPut("users/{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetAdminPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "密碼不可為空" });
        var ok = await _db.ResetAdminPasswordAsync(id, req.NewPassword);
        await LogOp("重設工具帳號密碼", $"id {id}", "", ok);
        return ok ? Ok(new { message = "密碼已重設" }) : BadRequest(new { message = "重設失敗" });
    }
}
