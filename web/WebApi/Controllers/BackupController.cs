using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/backup"), Authorize]
public class BackupController : ControllerBase
{
    private readonly DbService _db;
    public BackupController(DbService db) => _db = db;

    /// <summary>下載備份（csalogin + lock，INSERT IGNORE 格式，與 EXE 一致）</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var (sql, rows) = await _db.GetBackupSqlAsync();
        var bytes = Encoding.UTF8.GetBytes(sql);
        var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
        return File(bytes, "application/sql", fileName);
    }
}
