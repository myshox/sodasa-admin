using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WebApi.Controllers;

[ApiController, Authorize]
public class ItemsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    public ItemsController(IWebHostEnvironment env) => _env = env;

    private string DataDir => Path.Combine(_env.ContentRootPath, "Data");

    /// <summary>讀取 items.json / pets.json，回傳給前端</summary>
    [HttpGet("api/items")]
    public IActionResult GetItems()
    {
        var itemsPath = Path.Combine(DataDir, "items.json");
        var petsPath  = Path.Combine(DataDir, "pets.json");
        var items = System.IO.File.Exists(itemsPath) ? System.IO.File.ReadAllText(itemsPath) : "[]";
        var pets  = System.IO.File.Exists(petsPath)  ? System.IO.File.ReadAllText(petsPath)  : "[]";
        return Content($"{{\"items\":{items},\"pets\":{pets}}}", "application/json");
    }

    /// <summary>前端解析 xlsx → 以 JSON 陣列上傳儲存，並寫入 Data/*.json</summary>
    [HttpPost("api/items/save")]
    public IActionResult SaveItems([FromBody] SaveItemsRequest req)
    {
        Directory.CreateDirectory(DataDir);
        var opts = new JsonSerializerOptions { WriteIndented = false };
        if (req.Items != null)
            System.IO.File.WriteAllText(Path.Combine(DataDir, "items.json"), JsonSerializer.Serialize(req.Items, opts));
        if (req.Pets != null)
            System.IO.File.WriteAllText(Path.Combine(DataDir, "pets.json"), JsonSerializer.Serialize(req.Pets, opts));
        return Ok(new { message = "已儲存", itemCount = req.Items?.Count ?? 0, petCount = req.Pets?.Count ?? 0 });
    }
}

public class ItemEntry
{
    public int    Id     { get; set; }
    public string Name   { get; set; } = "";
    public string Desc   { get; set; } = "";
    public bool   IsPet  { get; set; }
}

public class SaveItemsRequest
{
    public List<ItemEntry>? Items { get; set; }
    public List<ItemEntry>? Pets  { get; set; }
}
