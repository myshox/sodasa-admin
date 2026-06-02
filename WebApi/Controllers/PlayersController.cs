using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Services;

namespace WebApi.Controllers;

[ApiController, Route("api/players"), Authorize]
public class PlayersController : ControllerBase
{
    private readonly DbService _db;
    public PlayersController(DbService db) => _db = db;

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q = "", [FromQuery] int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<PlayerRow>());
        return Ok(await _db.SearchPlayersAsync(q, limit));
    }

    /// <summary>在線玩家。recent=true 時納入 30 分鐘內有 LoginTime 的角色（與批量 target=online_recent 一致）。</summary>
    [HttpGet("online")]
    public async Task<IActionResult> Online([FromQuery] bool recent = false)
        => Ok(await _db.GetOnlineAsync(recent));

    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] int limit = 500)
        => Ok(await _db.GetPlayerListAsync(Math.Min(limit, 1000)));

    [HttpGet("banned")]
    public async Task<IActionResult> Banned([FromQuery] string q = "") => Ok(await _db.GetBannedListAsync(q));

    [HttpGet("master/{name}")]
    public async Task<IActionResult> Master(string name)
    {
        var r = await _db.GetMasterAsync(name);
        return r == null ? NotFound() : Ok(r);
    }

    [HttpGet("recharge")]
    public async Task<IActionResult> Recharge([FromQuery] string q = "")
        => Ok(await _db.GetRechargeAsync(q));

    [HttpGet("goldlog")]
    public async Task<IActionResult> GoldLog([FromQuery] string q = "")
        => Ok(await _db.GetGoldLogAsync(q));

    [HttpGet("mail")]
    public async Task<IActionResult> Mail([FromQuery] string q = "")
        => Ok(await _db.GetMailAsync(q));

    [HttpGet("tradelog")]
    public async Task<IActionResult> TradeLog([FromQuery] string q = "", [FromQuery] int limit = 300)
        => Ok(await _db.GetTradeLogAsync(q, limit));

    [HttpGet("vip")]
    public async Task<IActionResult> Vip() => Ok(await _db.GetVipListAsync());

    [HttpPost("batch-gold")]
    public async Task<IActionResult> BatchGold([FromBody] BatchGoldRequest req)
    {
        var target = req.Target ?? "online";
        if (!DbService.IsValidBatchTarget(target))
            return BadRequest(new { message = $"不支援的批量目標：{target}" });
        var (done, fail) = await _db.BatchGoldAsync(
            target,
            req.CustomList ?? "",
            req.AccountIds ?? "",
            req.Amount);
        return Ok(new { done, fail });
    }

    [HttpPost("batch-mail")]
    public async Task<IActionResult> BatchMail([FromBody] BatchMailRequest req)
    {
        if (!DbService.IsValidBatchTarget(req.Target))
            return BadRequest(new { message = $"不支援的批量目標：{req.Target}" });
        var count = await _db.BatchMailAsync(req.Target, req.CustomList, req.Title, req.Content);
        return Ok(new { count });
    }

    [HttpPost("batch-send-cart")]
    public async Task<IActionResult> BatchSendCart([FromBody] BatchSendCartRequest req)
    {
        if (!DbService.IsValidBatchTarget(req.Target))
            return BadRequest(new { message = $"不支援的批量目標：{req.Target}" });
        if (req.Cart == null || req.Cart.Count == 0)
            return BadRequest(new { message = "購物車為空" });
        var (count, fail, sentAccounts, lastError) = await _db.BatchSendCartAsync(
            req.Target, req.CustomList, req.Cart, req.Title, req.Content, req.ExcludeList);
        if (sentAccounts.Count == 0)
        {
            string errHint = string.IsNullOrEmpty(lastError) ? "" : $"\n錯誤：{lastError}";
            return Ok(new { count = 0, fail, accounts = sentAccounts,
                message = $"⚠ 無玩家收到道具（在線名單可能為空，或全部寫入失敗）{errHint}" });
        }
        string warnPart = fail > 0 ? $"，失敗 {fail} 筆" : "";
        if (!string.IsNullOrEmpty(lastError)) warnPart += $"（錯誤：{lastError}）";
        return Ok(new { count, fail, accounts = sentAccounts,
            message = $"✓ 已發送至 {sentAccounts.Count} 人（共 {count} 筆郵件{warnPart}）" });
    }

    [HttpPost("send-item")]
    public async Task<IActionResult> SendItem([FromBody] SendItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Account)) return BadRequest(new { message = "請指定玩家帳號" });
        var (success, fail) = await _db.SendItemMailAsync(
            req.Account.Trim(), req.ItemId, req.Quantity <= 0 ? 1 : req.Quantity,
            req.Title ?? "", req.Content ?? "");
        return Ok(new { success, fail, message = $"已發送 {success} 封道具郵件" + (fail > 0 ? $"，失敗 {fail}" : "") });
    }

    [HttpGet("{account}")]
    public async Task<IActionResult> Detail(string account)
    {
        var d = await _db.GetDetailAsync(account);
        return d == null ? NotFound() : Ok(d);
    }

    /// <summary>診斷寵物查詢：回傳查詢條件與資料庫中 capturepet 的 cdkey/author 樣本，供「讀不到寵物」時比對</summary>
    [HttpGet("{account}/pets/diagnose")]
    public async Task<IActionResult> GetPetsDiagnose(string account, [FromQuery] string? charName = null)
    {
        var result = await _db.GetPetDiagnoseAsync(account, charName);
        return Ok(result);
    }

    /// <summary>取得該玩家角色底下的寵物清單（capturepet）</summary>
    [HttpGet("{account}/pets")]
    public async Task<IActionResult> GetPets(string account, [FromQuery] string? charName = null)
    {
        var list = await _db.GetPlayerPetsAsync(account, charName);
        return Ok(list);
    }

    /// <summary>移除指定寵物（依 unicode 刪除 capturepet 一筆，不可復原）</summary>
    [HttpPost("{account}/pets/remove")]
    public async Task<IActionResult> RemovePet(string account, [FromBody] RemovePetRequest req)
    {
        if (string.IsNullOrWhiteSpace(req?.Unicode))
            return BadRequest(new { message = "請提供寵物 unicode" });
        var ok = await _db.DeletePetAsync(req.Unicode.Trim());
        return ok ? Ok(new { message = "✓ 已移除寵物" }) : BadRequest(new { message = "移除失敗或該筆不存在" });
    }

    [HttpPut("{account}/gold")]
    public async Task<IActionResult> SetGold(string account, [FromBody] SetCurrencyRequest req)
    {
        var ok = await _db.SetGoldAsync(account, req.Value);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u66F4\u65B0" }) : BadRequest();
    }

    [HttpPut("{account}/crystal")]
    public async Task<IActionResult> SetCrystal(string account, [FromBody] SetCurrencyRequest req)
    {
        var ok = await _db.SetCrystalAsync(account, req.Value);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u66F4\u65B0" }) : BadRequest();
    }

    /// <summary>給予儲值：更新 paydata 累積進度，可選同時發放金幣（與 EXE 一致）</summary>
    [HttpPost("{account}/recharge")]
    public async Task<IActionResult> Recharge(string account, [FromBody] RechargeRequest req)
    {
        if (req.TwdAmount <= 0)
            return BadRequest(new { message = "台幣金額須大於 0" });
        if (req.GiveGold && req.GoldAmount < 0)
            return BadRequest(new { message = "金幣數量不可為負" });
        var ok = await _db.AdjustPayDataPointAsync(account, req.TwdAmount, req.GoldAmount, req.GiveGold, req.UpdatePaydata);
        if (ok)
        {
            string orderNo  = $"GM-{DateTime.UtcNow:yyyyMMddHHmmss}-{(account.Length > 8 ? account[..8] : account)}";
            string prodName = req.GiveGold
                ? $"GM補單（+NT${req.TwdAmount:N0} / +{req.GoldAmount:N0}金幣）"
                : $"GM補單（僅累儲 +NT${req.TwdAmount:N0}）";
            long yuanbao = req.GiveGold ? req.GoldAmount : req.TwdAmount * 100;
            await _db.WriteRechargeOrderAsync(account, orderNo, prodName, yuanbao);
        }
        return ok ? Ok(new { message = "✓ 已給予儲值" }) : BadRequest(new { message = "修改失敗" });
    }

    /// <summary>只加累儲顯示（玩家無法領獎）：動 PayTotal/lifetime/point、鎖 check、不發金幣（與 EXE AdjustPayDisplayOnlyAsync 一致）</summary>
    [HttpPost("{account}/paydata/display-only")]
    public async Task<IActionResult> DisplayOnly(string account, [FromBody] DisplayOnlyRequest req)
    {
        if (req.TwdAmount <= 0)
            return BadRequest(new { message = "台幣金額須大於 0" });
        var (ok, newPoint) = await _db.AdjustPayDisplayOnlyAsync(account, req.TwdAmount);
        if (ok)
        {
            string orderNo  = $"GM-DISP-{DateTime.UtcNow:yyyyMMddHHmmss}-{(account.Length > 8 ? account[..8] : account)}";
            string prodName = $"GM補單（只加顯示 +NT${req.TwdAmount:N0}，不動輪次、不可領獎）";
            await _db.WriteRechargeOrderAsync(account, orderNo, prodName, req.TwdAmount * 100);
        }
        return ok
            ? Ok(new { message = $"🔒 已只加累儲顯示 +NT${req.TwdAmount:N0}（已鎖領獎，玩家重登後才看到新進度）", newPoint })
            : BadRequest(new { message = "修改失敗" });
    }

    /// <summary>主帳號分配儲值：為旗下多個 CDKEY 各別執行儲值</summary>
    [HttpPost("master-split-recharge")]
    public async Task<IActionResult> MasterSplitRecharge([FromBody] List<SplitRechargeItem> items)
    {
        if (items == null || items.Count == 0)
            return BadRequest(new { message = "清單為空" });
        int done = 0;
        var results = new List<object>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Account) || item.TwdAmount <= 0)
            {
                results.Add(new { account = item.Account, ok = false, msg = "台幣金額須 > 0" });
                continue;
            }
            var ok = await _db.AdjustPayDataPointAsync(
                item.Account.Trim(), item.TwdAmount, item.GoldAmount, item.GiveGold);
            if (ok)
            {
                done++;
                string ord  = $"GM-{DateTime.UtcNow:yyyyMMddHHmmss}-{(item.Account.Length > 8 ? item.Account[..8] : item.Account)}";
                string prod = item.GiveGold
                    ? $"GM分配補單（+NT${item.TwdAmount:N0} / +{item.GoldAmount:N0}金幣）"
                    : $"GM分配補單（僅累儲 +NT${item.TwdAmount:N0}）";
                await _db.WriteRechargeOrderAsync(item.Account.Trim(), ord, prod, item.GiveGold ? item.GoldAmount : item.TwdAmount * 100);
            }
            results.Add(new { account = item.Account, ok, msg = ok ? "✓ 成功" : "✗ 失敗" });
        }
        return Ok(new { done, total = items.Count, results });
    }

    [HttpPost("{account}/ban")]
    public async Task<IActionResult> Ban(string account, [FromBody] BanRequest req)
    {
        var ok = await _db.SetBanAsync(account, req.Ban, req.Days, req.Hours);
        return ok ? Ok(new { message = req.Ban ? "\u2713 \u5DF2\u5C01\u865F" : "\u2713 \u5DF2\u89E3\u5C01" }) : BadRequest();
    }

    [HttpPost("{account}/rename")]
    public async Task<IActionResult> Rename(string account, [FromBody] RenameRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewName)) return BadRequest(new { message = "請輸入新名稱" });
        var ok = await _db.RenamePlayerAsync(account, req.NewName.Trim());
        return ok ? Ok(new { message = "\u2713 \u6539\u540D\u6210\u529F" }) : BadRequest(new { message = "\u6539\u540D\u5931\u6557" });
    }

    [HttpPost("{account}/force-offline")]
    public async Task<IActionResult> ForceOffline(string account)
    {
        var ok = await _db.ForceOfflineAsync(account);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u5F37\u5236\u4E0B\u7DDA" }) : BadRequest();
    }

    [HttpPost("{account}/mute")]
    public async Task<IActionResult> Mute(string account, [FromBody] MuteRequest req)
    {
        var ok = await _db.SetMuteAsync(account, req.Mute);
        return ok ? Ok(new { message = req.Mute ? "\u2713 \u5DF2\u7981\u8A00" : "\u2713 \u5DF2\u89E3\u9664\u7981\u8A00" }) : BadRequest();
    }

    [HttpPost("{account}/paydata/reset")]
    public async Task<IActionResult> ResetPaydata(string account)
    {
        var ok = await _db.ResetPaydataAsync(account);
        return ok ? Ok(new { message = "\u2713 \u5DF2\u91CD\u7F6E\u5132\u5024\u9032\u5EA6" }) : BadRequest();
    }

    [HttpPost("{account}/paydata/fix")]
    public async Task<IActionResult> FixPaydata(string account)
    {
        var ok = await _db.FixPaydataCheckAsync(account);
        return ok ? Ok(new { message = "✓ 已修復循環顯示" }) : BadRequest(new { message = "修復失敗（可能無 paydata 記錄）" });
    }

    /// <summary>發放累積獎勵（設 check=1，防呆：check 必須為 0 才允許）</summary>
    [HttpPost("{account}/paydata/claim")]
    public async Task<IActionResult> ClaimPaydataReward(string account)
    {
        var result = await _db.ClaimPaydataRewardAsync(account);
        return result switch
        {
            "already_claimed"=> BadRequest(new { message = "⚠ 此輪獎勵已發放，無法重複領取" }),
            "no_cycle"       => BadRequest(new { message = "⚠ 尚未完成任何循環，無獎勵可發放" }),
            "not_found"      => BadRequest(new { message = "找不到玩家 paydata 記錄" }),
            "error"          => StatusCode(500, new { message = "資料庫錯誤，請稍後再試" }),
            _                => Ok(new { message = $"✓ 獎勵已發放（輪次 #{result}）" })
        };
    }

    [HttpGet("{account}/paydata")]
    public async Task<IActionResult> GetPaydata(string account)
        => Ok(await _db.GetPaydataSummaryAsync(account));

    // ── 消費達成獎勵（costdata）────────────────────────────────────

    /// <summary>全服（或線上）玩家 costdata 列表，用於批量操作</summary>
    [HttpGet("costdata/list")]
    public async Task<IActionResult> GetAllCostData([FromQuery] bool online = false)
        => Ok(await _db.GetAllCostDataAsync(online));

    /// <summary>批量重置多個玩家的 costdata</summary>
    [HttpPost("costdata/batch-reset")]
    public async Task<IActionResult> BatchResetCostdata([FromBody] BatchCostResetRequest req)
    {
        if (req.Accounts == null || req.Accounts.Count == 0)
            return BadRequest(new { message = "請提供帳號列表" });
        var (success, fail) = await _db.BatchResetCostDataAsync(req.Accounts, req.FullReset);
        string kind = req.FullReset ? "完全重置" : "重置已領狀態";
        return Ok(new { message = $"✓ 批量{kind}完成：成功 {success} 筆，失敗 {fail} 筆", success, fail });
    }

    [HttpGet("{account}/costdata")]
    public async Task<IActionResult> GetCostdata(string account)
        => Ok(await _db.GetCostdataSummaryAsync(account));

    [HttpGet("{master}/costdata/all-chars")]
    public async Task<IActionResult> GetAllCharsCostdata(string master)
        => Ok(await _db.GetAllCharsCostdataAsync(master));

    [HttpPost("{account}/costdata/adjust")]
    public async Task<IActionResult> AdjustCostdata(string account, [FromBody] AdjustCostRequest req)
    {
        if (req.AddPoint <= 0) return BadRequest(new { message = "增加量必須大於 0" });
        var ok = await _db.AdjustCostdataPointAsync(account, req.CharName ?? "", req.AddPoint);
        return ok ? Ok(new { message = $"✓ 已增加 {req.AddPoint:N0} 消費點數" }) : BadRequest(new { message = "調整失敗" });
    }

    [HttpPost("{account}/costdata/reset")]
    public async Task<IActionResult> ResetCostdata(string account)
    {
        var ok = await _db.ResetCostdataAsync(account);
        return ok ? Ok(new { message = "✓ 已清除已領狀態（check=0），消費點數保留，玩家可立即重領" }) : BadRequest(new { message = "重置失敗（玩家可能無 costdata 記錄）" });
    }

    /// <summary>完全重置消費達成進度：point=0 且 check=0，玩家必須重新消費才能領取</summary>
    [HttpPost("{account}/costdata/full-reset")]
    public async Task<IActionResult> FullResetCostdata(string account)
    {
        var ok = await _db.FullResetCostdataAsync(account);
        return ok ? Ok(new { message = "✓ 完全重置完成（point=0, check=0），玩家須重新消費才能再領取" }) : BadRequest(new { message = "完全重置失敗（玩家可能無 costdata 記錄）" });
    }

    /// <summary>同步遊戲模式：退 check=milestoneIdx，讓遊戲伺服器自動發道具到背包</summary>
    [HttpPost("{account}/costdata/claim/{milestoneIdx:int}")]
    public async Task<IActionResult> ClaimCostMilestone(string account, int milestoneIdx)
    {
        var ok = await _db.ClaimCostMilestoneAsync(account, milestoneIdx);
        return ok
            ? Ok(new { message = $"✓ 已退回 check={milestoneIdx}，遊戲伺服器下次偵測時將自動發放道具到背包" })
            : BadRequest(new { message = "操作失敗（可能無記錄或索引超出範圍）" });
    }

    /// <summary>郵件模式：直接寄出道具，同時標 check=milestoneIdx+1</summary>
    [HttpPost("{account}/costdata/claim-mail/{milestoneIdx:int}")]
    public async Task<IActionResult> ClaimCostMilestoneMail(
        string account, int milestoneIdx, [FromBody] AdjustCostRequest req)
    {
        var ok = await _db.ClaimCostMilestoneByMailAsync(
            account, req.CharName ?? "", milestoneIdx,
            (int)(req.AddPoint),          // 郵件模式：AddPoint 當 itemId 傳入
            req.CharName ?? "消費達成獎勵道具",
            req.Quantity);
        return ok
            ? Ok(new { message = $"✓ 道具已寄出，check 標記為已領取" })
            : BadRequest(new { message = "操作失敗" });
    }

    [HttpPost("send-cart")]
    public async Task<IActionResult> SendCart([FromBody] SendCartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Account)) return BadRequest(new { message = "請指定玩家帳號" });
        if (req.Cart == null || req.Cart.Count == 0) return BadRequest(new { message = "購物車為空" });
        var (success, fail) = await _db.SendCartMailAsync(req.Account.Trim(), req.Cart, req.Title, req.Content);
        return Ok(new { success, fail, message = $"已發送 {success} 筆" + (fail > 0 ? $"，失敗 {fail}" : "") });
    }

    [HttpGet("{account}/mail-history")]
    public async Task<IActionResult> MailHistory(string account)
        => Ok(await _db.GetPlayerMailHistoryAsync(account));

    /// <summary>獎池紀錄 (poolitem)，是否為寶箱/骰子開出結果請對照遊戲確認</summary>
    [HttpGet("{account}/poolitem")]
    public async Task<IActionResult> PoolItem(string account, [FromQuery] int limit = 200)
        => Ok(await _db.GetPlayerPoolItemAsync(account ?? "", Math.Clamp(limit, 1, 500)));

    /// <summary>顯示 maildata 原始欄位（type/data/buff3）用於診斷無法領取問題</summary>
    [HttpGet("{account}/mail-raw")]
    public async Task<IActionResult> MailRaw(string account, [FromQuery] int limit = 50)
        => Ok(await _db.GetMailRawAsync(account, limit));

    /// <summary>取得 maildata 完整欄位（SELECT *）用於深度診斷</summary>
    [HttpGet("{account}/mail-full")]
    public async Task<IActionResult> MailFull(string account, [FromQuery] int limit = 20)
        => Ok(await _db.GetMailFullAsync(account, limit));

    /// <summary>取得 maildata 表的欄位定義</summary>
    [HttpGet("maildata-schema")]
    public async Task<IActionResult> MaildataSchema()
        => Ok(await _db.GetMaildataSchemaAsync());

    /// <summary>修正舊版網頁發送的郵件（buff1/buff2 使用 GM 固定文字的記錄），使其可正常領取</summary>
    [HttpPost("fix-old-mails")]
    public async Task<IActionResult> FixOldMails([FromBody] FixOldMailsRequest req)
    {
        var descs = req.ItemDescriptions?
            .Where(d => d.ItemId > 0 && !string.IsNullOrWhiteSpace(d.Desc))
            .Select(d => (d.ItemId, d.Desc!))
            .ToList();
        var (fixedCount, total, buff3Fixed) = await _db.FixOldWebMailsAsync(req.Account, descs);
        string scope = string.IsNullOrWhiteSpace(req.Account) ? "全部玩家" : req.Account;
        string descNote = descs?.Count > 0 ? $"（使用 {descs.Count} 種道具描述）" : "（無道具描述，僅從資料庫比對）";
        string msg = $"✓ 修正完成（{scope}）{descNote}\n• 標題修正：{fixedCount} 筆\n• buff3 回填：{buff3Fixed} 筆\n• 掃描 buff3 空筆數：{total} 筆";
        return Ok(new { fixedCount, buff3Fixed, total, message = msg });
    }

    /// <summary>清除指定玩家的遊戲內郵件（軟刪除 deleamill=1）</summary>
    [HttpPost("{account}/clear-mail")]
    public async Task<IActionResult> ClearPlayerMail(string account, [FromBody] ClearMailRequest req)
    {
        var count = await _db.ClearPlayerMailAsync(account, req.UnclaimedOnly);
        string scope = req.UnclaimedOnly ? "未領取郵件" : "全部郵件";
        return Ok(new { count, message = $"✓ 已清除「{account}」{scope} {count} 封" });
    }

    /// <summary>清除全服所有玩家的遊戲內郵件（軟刪除 deleamill=1）</summary>
    [HttpPost("clear-all-mail")]
    public async Task<IActionResult> ClearAllMail([FromBody] ClearMailRequest req)
    {
        var count = await _db.ClearPlayerMailAsync("", req.UnclaimedOnly);
        string scope = req.UnclaimedOnly ? "未領取郵件" : "全部郵件";
        return Ok(new { count, message = $"✓ 已清除全服 {scope} {count} 封" });
    }

    /// <summary>取得加速外掛玩家列表（依總次數排序）</summary>
    [HttpGet("speed-hackers")]
    public async Task<IActionResult> SpeedHackers([FromQuery] int min = 1, [FromQuery] int limit = 200)
        => Ok(await _db.GetSpeedHackPlayersAsync(min, limit));

    /// <summary>查詢與指定帳號共用相同 IP 的其他帳號</summary>
    [HttpGet("{account}/shared-ip")]
    public async Task<IActionResult> SharedIp(string account)
        => Ok(await _db.GetSharedIpAccountsAsync(account));

    /// <summary>查詢指定帳號的封禁歷史記錄</summary>
    [HttpGet("{account}/ban-log")]
    public async Task<IActionResult> BanLog(string account)
        => Ok(await _db.GetBanLogAsync(account));

    /// <summary>查詢指定帳號的家族資訊</summary>
    [HttpGet("{account}/family")]
    public async Task<IActionResult> Family(string account)
        => Ok(await _db.GetPlayerFamilyAsync(account));

    /// <summary>批量封禁玩家</summary>
    [HttpPost("batch-ban")]
    public async Task<IActionResult> BatchBan([FromBody] BatchBanRequest req)
    {
        if (req.Accounts == null || req.Accounts.Count == 0)
            return BadRequest(new { message = "帳號清單不可空" });
        int success = 0, fail = 0;
        foreach (var acc in req.Accounts)
        {
            try { if (await _db.SetBanAsync(acc, true, req.Days, req.Hours)) success++; else fail++; }
            catch { fail++; }
        }
        string dur = req.Days > 0 ? $"{req.Days} 天" : req.Hours > 0 ? $"{req.Hours} 小時" : "永久";
        return Ok(new { success, fail, message = $"✓ 批量封禁完成（{dur}）：成功 {success}，失敗 {fail}" });
    }
}

public class ClearMailRequest { public bool UnclaimedOnly { get; set; } = false; }
public class ItemDescEntry { public int ItemId { get; set; } public string? Desc { get; set; } }
public class FixOldMailsRequest { public string Account { get; set; } = ""; public List<ItemDescEntry>? ItemDescriptions { get; set; } }

[ApiController, Route("api/stats"), Authorize]
public class StatsController : ControllerBase
{
    private readonly DbService _db;
    public StatsController(DbService db) => _db = db;
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.GetStatsAsync());
}

[ApiController, Route("api/gmlog"), Authorize]
public class GmLogController : ControllerBase
{
    [HttpGet("dates")]
    public IActionResult Dates()
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return Ok(new List<string>());
        var dates = Directory.GetFiles(logDir, "*.log")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderByDescending(x => x).ToList();
        return Ok(dates);
    }

    [HttpGet]
    public IActionResult Get([FromQuery] int offset = 0, [FromQuery] int limit = 100,
        [FromQuery] string q = "", [FromQuery] string date = "")
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return Ok(new List<object>());
        var entries = new List<object>();
        IEnumerable<string> files = Directory.GetFiles(logDir, "*.log").OrderByDescending(x => x);
        if (!string.IsNullOrWhiteSpace(date))
            files = files.Where(f => Path.GetFileNameWithoutExtension(f) == date);
        foreach (var f in files)
        {
            foreach (var line in System.IO.File.ReadAllLines(f).Reverse())
            {
                try {
                    var doc = System.Text.Json.JsonDocument.Parse(line);
                    var r   = doc.RootElement;
                    bool success = !r.TryGetProperty("Success", out var sc) || sc.GetBoolean();
                    string action = r.TryGetProperty("Action", out var ac) ? ac.GetString() ?? "" : "";
                    string target = r.TryGetProperty("Target", out var tg) ? tg.GetString() ?? "" : "";
                    string detail = r.TryGetProperty("Detail", out var dt) ? dt.GetString() ?? "" : "";
                    string gmUser = r.TryGetProperty("Operator", out var op) ? op.GetString() ?? "GM" : "GM";
                    string time   = r.TryGetProperty("Time",     out var tm) ? tm.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(q))
                    {
                        string combined = $"{action} {target} {detail} {gmUser}";
                        if (!combined.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    entries.Add(new { id = entries.Count + 1, gmUser, action, target, detail, time, success });
                } catch { }
            }
        }
        int total = entries.Count;
        return Ok(new { total, items = entries.Skip(offset).Take(limit).ToList() });
    }

    [HttpGet("export")]
    public IActionResult Export([FromQuery] string date = "")
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return NotFound();
        IEnumerable<string> files = Directory.GetFiles(logDir, "*.log").OrderByDescending(x => x);
        if (!string.IsNullOrWhiteSpace(date))
            files = files.Where(f => Path.GetFileNameWithoutExtension(f) == date);
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("時間\tGM\t結果\t操作\t對象\t詳情");
        foreach (var f in files)
        {
            foreach (var line in System.IO.File.ReadAllLines(f))
            {
                try {
                    var doc = System.Text.Json.JsonDocument.Parse(line);
                    var r   = doc.RootElement;
                    bool ok   = !r.TryGetProperty("Success", out var sc) || sc.GetBoolean();
                    string tm = r.TryGetProperty("Time",     out var t)  ? t.GetString()  ?? "" : "";
                    string op = r.TryGetProperty("Operator", out var o)  ? o.GetString()  ?? "" : "";
                    string ac = r.TryGetProperty("Action",   out var a)  ? a.GetString()  ?? "" : "";
                    string tg = r.TryGetProperty("Target",   out var tgt)? tgt.GetString()?? "" : "";
                    string dt = r.TryGetProperty("Detail",   out var d)  ? d.GetString()  ?? "" : "";
                    lines.AppendLine($"{tm}\t{op}\t{(ok?"✓":"✗")}\t{ac}\t{tg}\t{dt}");
                } catch { }
            }
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(lines.ToString());
        string fname = string.IsNullOrWhiteSpace(date) ? "gmlog_all.txt" : $"gmlog_{date}.txt";
        return File(bytes, "text/plain; charset=utf-8", fname);
    }
}

public class BatchMailRequest
{
    public string Target     { get; set; } = "online";
    public string CustomList { get; set; } = "";
    public string Title      { get; set; } = "";
    public string Content    { get; set; } = "";
}
