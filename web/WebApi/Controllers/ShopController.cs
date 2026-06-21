using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/shop"), Authorize]
public class ShopController : ControllerBase
{
    private readonly DbService _db;
    public ShopController(DbService db) => _db = db;

    private static readonly string[] AllowedTables = { "vipshop", "fameshop", "csshopnum", "csxsshopnum" };

    [HttpGet("{table}")]
    public async Task<IActionResult> GetTop(string table, [FromQuery] int top = 20, [FromQuery] string? from = null, [FromQuery] string? to = null)
    {
        if (!AllowedTables.Contains(table.ToLowerInvariant()))
            return BadRequest(new { message = "不支援的商城表" });
        DateTime? fromD = null, toD = null;
        if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var fd)) fromD = fd.Date;
        if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var td)) toD = td.Date;
        if (fromD.HasValue ^ toD.HasValue)
        {
            if (fromD.HasValue) toD = fromD;
            else fromD = toD;
        }
        var (items, spenders) = await _db.GetShopTopItemsAsync(table, Math.Min(top, 50), fromD, toD);
        return Ok(new { items, spenders });
    }
}
