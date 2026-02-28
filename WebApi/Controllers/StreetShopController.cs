using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
public class StreetShopController : ControllerBase
{
    private readonly DbService _db;
    public StreetShopController(DbService db) => _db = db;

    /// <summary>取得所有攤主清單</summary>
    [HttpGet("api/street/vendors")]
    public async Task<IActionResult> GetAllVendors()
    {
        var result = await _db.GetAllVendorsAsync();
        return Ok(result);
    }

    /// <summary>查詢攤主目前商品 + 歷史成交（支援 cdkey 或角色名）</summary>
    [HttpGet("api/street/vendor/{query}")]
    public async Task<IActionResult> GetVendor(string query, [FromQuery] int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(query)) return BadRequest();
        var result = await _db.GetStreetVendorAsync(query.Trim(), Math.Clamp(limit, 10, 500));
        return Ok(result);
    }

    /// <summary>商城反查：輸入物品名稱，查誰買過</summary>
    [HttpGet("api/shop/buyers")]
    public async Task<IActionResult> GetShopBuyers([FromQuery] string item, [FromQuery] int limit = 200)
    {
        if (string.IsNullOrWhiteSpace(item)) return BadRequest();
        var result = await _db.GetShopBuyersAsync(item.Trim(), Math.Clamp(limit, 10, 500));
        return Ok(result);
    }
}
