using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WebApi.Models;

namespace WebApi.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public AuthController(IConfiguration cfg) => _cfg = cfg;

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var accounts = _cfg.GetSection("GmAccounts")
            .Get<List<GmAccount>>() ?? new();
        var found = accounts.FirstOrDefault(a =>
            a.Username == req.Username && a.Password == req.Password);
        if (found == null)
            return Unauthorized(new { message = "帳號或密碼錯誤" });

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var hours = int.TryParse(_cfg["Jwt:ExpiryHours"], out var h) ? h : 12;
        var token = new JwtSecurityToken(
            claims: new[] {
                new Claim(ClaimTypes.Name, found.Username),
                new Claim(ClaimTypes.Role, found.Role)
            },
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds);

        return Ok(new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            found.Username, found.Role));
    }
}

public class GmAccount
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role     { get; set; } = "gm";
}
