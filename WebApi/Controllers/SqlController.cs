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

    // 列出所有資料表
    [HttpGet("tables")]
    public async Task<IActionResult> GetTables()
    {
        var (ok, rows, error) = await _db.ExecuteReadOnlyQueryAsync("SHOW TABLES");
        if (!ok) return BadRequest(new { error });
        var tables = rows.Select(r => r.Values.FirstOrDefault()?.ToString() ?? "").Where(s => s != "").ToList();
        return Ok(tables);
    }

    // 查詢指定表的資料（帶翻頁 & 搜尋）
    [HttpGet("browse")]
    public async Task<IActionResult> Browse(
        [FromQuery] string table,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (string.IsNullOrWhiteSpace(table) || table.Contains("`") || table.Contains(";"))
            return BadRequest(new { error = "無效的表名" });
        if (pageSize > 500) pageSize = 500;
        if (page < 1) page = 1;

        // 取欄位
        var (colOk, colRows, colErr) = await _db.ExecuteReadOnlyQueryAsync($"DESCRIBE `{table}`");
        if (!colOk) return BadRequest(new { error = colErr });
        var columns = colRows.Select(r => r.ContainsKey("Field") ? r["Field"]?.ToString() ?? "" : r.Values.FirstOrDefault()?.ToString() ?? "").Where(s => s != "").ToList();

        // WHERE 條件（參數化）
        string where = "";
        var searchParams = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var parts = columns.Select((c, i) => $"CAST(`{c}` AS CHAR) LIKE @_s{i}");
            where = "WHERE " + string.Join(" OR ", parts);
            for (int i = 0; i < columns.Count; i++)
                searchParams[$"@_s{i}"] = $"%{search}%";
        }

        // 總筆數
        var (cntOk, cntRows, cntErr) = await _db.ExecuteReadOnlyQueryAsync($"SELECT COUNT(*) AS cnt FROM `{table}` {where}", searchParams);
        if (!cntOk) return BadRequest(new { error = cntErr });
        int total = cntRows.Count > 0 && cntRows[0].ContainsKey("cnt") ? Convert.ToInt32(cntRows[0]["cnt"]) : 0;

        // 資料
        int offset = (page - 1) * pageSize;
        var (dataOk, dataRows, dataErr) = await _db.ExecuteReadOnlyQueryAsync(
            $"SELECT * FROM `{table}` {where} LIMIT {pageSize} OFFSET {offset}", searchParams);
        if (!dataOk) return BadRequest(new { error = dataErr });

        return Ok(new { columns, rows = dataRows, total, page, pageSize });
    }
}
