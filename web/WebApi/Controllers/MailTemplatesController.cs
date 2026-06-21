using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WebApi.Controllers;

/// <summary>
/// 郵件範例（標題/內容/購物車）— 存於伺服器 Data/mail_templates.json，
/// 讓網頁版（桌機／手機瀏覽器）共用同一份資料。與 EXE 本機 templates.json 分開，需另匯入同步。
/// </summary>
[ApiController, Authorize]
public class MailTemplatesController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    public MailTemplatesController(IWebHostEnvironment env) => _env = env;

    private string DataDir => Path.Combine(_env.ContentRootPath, "Data");
    private string FilePath => Path.Combine(DataDir, "mail_templates.json");

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [HttpGet("api/mail-templates")]
    public IActionResult Get()
    {
        if (!System.IO.File.Exists(FilePath))
            return Ok(Array.Empty<MailTemplateWire>());
        try
        {
            var json = System.IO.File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<MailTemplateWire>>(json, ReadOpts);
            return Ok(list ?? new List<MailTemplateWire>());
        }
        catch
        {
            return Ok(Array.Empty<MailTemplateWire>());
        }
    }

    [HttpPut("api/mail-templates")]
    public IActionResult Put([FromBody] List<MailTemplateWire>? body)
    {
        Directory.CreateDirectory(DataDir);
        var list = body ?? new List<MailTemplateWire>();
        System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(list, WriteOpts));
        return Ok(new { message = "已儲存", count = list.Count });
    }
}

/// <summary>與 WebApp BatchOpsPage 的 MailTemplate / CartItem 欄位一致（camelCase JSON）</summary>
public class MailTemplateWire
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public List<MailTemplateCartWire>? Cart { get; set; }
}

public class MailTemplateCartWire
{
    public int ItemId { get; set; }
    public int Qty { get; set; }
    public int Type { get; set; }
    public string? Name { get; set; }
    public string? Buff3 { get; set; }
}
