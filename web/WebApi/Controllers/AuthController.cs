using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WebApi.Models;
using WebApi.Services;
using Microsoft.Extensions.Hosting;

namespace WebApi.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;
    private readonly DbService _db;
    private readonly IHostEnvironment _env;

    public AuthController(IConfiguration cfg, DbService db, IHostEnvironment env)
    {
        _cfg = cfg;
        _db  = db;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest? req)
    {
        try
        {
            if (req == null)
                return BadRequest(new { message = "請求內容為空" });

            string user = (req.Username ?? "").Trim();
            string pass = req.Password ?? "";
            if (user.Length == 0 || pass.Length == 0)
                return BadRequest(new { message = "請輸入帳號與密碼" });

            string claimUser;
            string role;

            // 優先資料庫 admin_users（與 EXE／GM 管理一致）；失敗再用 appsettings 緊急帳號
            var (dbOk, dbUser, dbRole) = await _db.TryValidateAdminUsersLoginAsync(user, pass);
            if (dbOk)
            {
                claimUser = dbUser;
                role      = dbRole;
            }
            else
            {
                List<GmAccount> accounts;
                try
                {
                    accounts = _cfg.GetSection("GmAccounts").Get<List<GmAccount>>() ?? new();
                }
                catch
                {
                    accounts = new List<GmAccount>();
                }

                var foundCfg = accounts.FirstOrDefault(a =>
                    string.Equals((a.Username ?? "").Trim(), user, StringComparison.OrdinalIgnoreCase)
                    && a.Password == pass);
                if (foundCfg == null)
                    return Unauthorized(new { message = "帳號或密碼錯誤" });
                claimUser = (foundCfg.Username ?? "").Trim();
                role      = string.IsNullOrWhiteSpace(foundCfg.Role) ? "gm" : foundCfg.Role;
            }

            var secret = _cfg["Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                return StatusCode(503, new { message = "伺服器未設定 Jwt:Secret，無法簽發登入權杖，請檢查 appsettings 或環境變數。" });
            if (Encoding.UTF8.GetByteCount(secret) < 32)
                return StatusCode(503, new { message = "Jwt:Secret 位元組長度不足（.NET 8 HMAC-SHA256 要求金鑰至少 256 bits，即 32 個 ASCII 字元或更多）。請在環境變數或 appsettings 更新後重啟。" });

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var hours = int.TryParse(_cfg["Jwt:ExpiryHours"], out var h) ? h : 12;
            var token = new JwtSecurityToken(
                claims: new[] {
                    new Claim(ClaimTypes.Name, claimUser),
                    new Claim(ClaimTypes.Role, role)
                },
                expires: DateTime.UtcNow.AddHours(hours),
                signingCredentials: creds);

            return Ok(new LoginResponse(
                new JwtSecurityTokenHandler().WriteToken(token),
                claimUser, role));
        }
        catch (Exception ex)
        {
            var detail = _env.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new
            {
                message = "登入處理發生錯誤（常見：資料庫連線失敗、admin_users 不存在、或 Jwt 設定異常）。請查看伺服器日誌並確認 MySQL 連線與資料表。",
                detail
            });
        }
    }
}

public class GmAccount
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role     { get; set; } = "gm";
}
