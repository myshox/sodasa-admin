using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// JWT
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer           = false,
            ValidateAudience         = false
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSingleton<DbService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// 在遊戲用 MySQL 上自動建立 admin_users（若尚不存在）；勿在 PostgreSQL 執行手動腳本。
try
{
    var dbSvc = app.Services.GetRequiredService<DbService>();
    await dbSvc.EnsureAdminUsersTableAsync();
    await dbSvc.SeedDefaultAdminWhenTableEmptyAsync();
    await dbSvc.ApplyBootstrapAdminFromEnvAsync();
}
catch (Exception ex)
{
    Console.WriteLine("[Startup] EnsureAdminUsersTable: " + ex.Message);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// 伺服前端靜態檔案
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// SPA fallback：所有非 /api 路徑都回傳 index.html
app.MapFallbackToFile("index.html");

app.Run();
