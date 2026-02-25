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
    public async Task<IActionResult> GetTop(string table, [FromQuery] int top = 20)
    {
        if (!AllowedTables.Contains(table.ToLowerInvariant()))
            return BadRequest(new { message = "不支援的商城表" });
        var (items, spenders) = await _db.GetShopTopItemsAsync(table, Math.Min(top, 50));
        return Ok(new { items, spenders });
    }
}
