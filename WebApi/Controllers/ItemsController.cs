using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace WebApi.Controllers;

[ApiController, Authorize]
public class ItemsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    public ItemsController(IWebHostEnvironment env) => _env = env;

    // publish/Data/
    private string DataDir => Path.Combine(_env.ContentRootPath, "Data");

    // 往上兩層找 git repo 根目錄（publish/ → WebApi/ → repo root）
    private string? RepoRoot
    {
        get
        {
            var dir = _env.ContentRootPath; // /opt/gmtool/publish
            for (int i = 0; i < 3; i++)
            {
                var parent = Directory.GetParent(dir)?.FullName;
                if (parent == null) break;
                dir = parent;
                if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
            }
            return null;
        }
    }

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
        var opts = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        if (req.Items != null)
            System.IO.File.WriteAllText(Path.Combine(DataDir, "items.json"), JsonSerializer.Serialize(req.Items, opts));
        if (req.Pets != null)
            System.IO.File.WriteAllText(Path.Combine(DataDir, "pets.json"), JsonSerializer.Serialize(req.Pets, opts));
        return Ok(new { message = "已儲存至伺服器", itemCount = req.Items?.Count ?? 0, petCount = req.Pets?.Count ?? 0 });
    }

    /// <summary>將 publish/Data/*.json 同步到 git repo 並 commit + push</summary>
    [HttpPost("api/items/git-sync")]
    public IActionResult GitSync()
    {
        var repo = RepoRoot;
        if (repo == null) return StatusCode(500, new { message = "找不到 git repo 根目錄" });

        // 把 publish/Data/*.json 複製回 repo 的 WebApi/Data/
        var repoDataDir = Path.Combine(repo, "WebApi", "Data");
        Directory.CreateDirectory(repoDataDir);
        foreach (var f in new[] { "items.json", "pets.json" })
        {
            var src = Path.Combine(DataDir, f);
            var dst = Path.Combine(repoDataDir, f);
            if (System.IO.File.Exists(src)) System.IO.File.Copy(src, dst, overwrite: true);
        }

        // git add → commit → push
        string Run(string args)
        {
            var p = Process.Start(new ProcessStartInfo("git", args)
            {
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })!;
            p.WaitForExit(15_000);
            return (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
        }

        Run("add WebApi/Data/items.json WebApi/Data/pets.json");
        var commitOut = Run("commit -m \"update: sync items/pets data files\"");
        var pushOut   = Run("push origin master");

        var nothingToCommit = commitOut.Contains("nothing to commit");
        return Ok(new
        {
            message = nothingToCommit ? "無變更，不需要 commit" : "已同步到 Git ✓",
            commit  = commitOut,
            push    = pushOut,
        });
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
