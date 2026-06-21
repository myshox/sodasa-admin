using System.Text.Json.Serialization;

namespace WebApi.Models;

/// <summary>明確綁定 camelCase，避免部分環境 JSON 選項與 record 位置參數不相容導致帳密為空。</summary>
public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}
public record LoginResponse(string Token, string Username, string Role);

public class PlayerRow
{
    public string Account     { get; set; } = "";
    public string OnlineName  { get; set; } = "";
    public bool   IsOnline    { get; set; }
    public int    ServerId    { get; set; }
    public string RegTime     { get; set; } = "";
    public string LoginTime   { get; set; } = "";
    public string IP          { get; set; } = "";
    public bool   IsBanned    { get; set; }
    public long   Gold        { get; set; }
    public long   Crystal     { get; set; }
    public int    PetCount    { get; set; }
    // 列表額外欄位
    public string MasterName  { get; set; } = "";
    public long   PayTotal    { get; set; }
    public int    VipLevel    { get; set; }   // 0=一般 1=黃金 2=鑽石
}

public class PlayerDetail : PlayerRow
{
    public string RegIP        { get; set; } = "";
    public string Uid          { get; set; } = "";
    public string MAC          { get; set; } = "";
    public int    TotalMails   { get; set; }
    public int    UnreadMails  { get; set; }
    public bool   IsMuted      { get; set; }
    public string BanEndTime   { get; set; } = "";
    // 新增欄位
    public long   PayPoint     { get; set; }   // 充值點
    public long   RmbPoint     { get; set; }   // R幣
    public int    GroupId      { get; set; }
    public int    NeiCe        { get; set; }
    // paydata 累積充值
    public long   PaydataPoint { get; set; }   // 當前循環進度
    public long   PaydataTotal { get; set; }   // lifetime_total
    public long   TotalCheck   { get; set; }   // 完成循環次數
}

public class RenameRequest      { public string NewName { get; set; } = ""; }
public class MuteRequest        { public bool Mute { get; set; } }

public class SetCurrencyRequest
{
    public long Value { get; set; }
}

public class BanRequest
{
    public bool   Ban     { get; set; }
    public string Reason  { get; set; } = "";
    public int    Days    { get; set; }    // 0 = 永久
    public double Hours   { get; set; }   // > 0 時以小時為單位
}

public class SetAdminStatusRequest   { public bool   Enabled     { get; set; } }
public class ResetAdminPasswordRequest { public string NewPassword { get; set; } = ""; }

/// <summary>依 csalogin.ServerId 統計在線人數（加總應等於 OnlinePlayers）。</summary>
public class OnlineByServerRow
{
    public int ServerId { get; set; }
    public int Count    { get; set; }
}

public class DashboardStats
{
    public int  TotalPlayers   { get; set; }
    public int  OnlinePlayers  { get; set; }
    public int  BannedPlayers  { get; set; }
    public int  NewToday       { get; set; }
    public long TotalGold      { get; set; }
    public long TotalCrystal   { get; set; }
    /// <summary>各分流在線（Online=1）筆數，未重複扣減。</summary>
    public List<OnlineByServerRow> OnlineByServer { get; set; } = new();
}

// 交易記錄（tradelog）
public class TradeRecordDto
{
    public string FromCdkey { get; set; } = "";
    public string FromName  { get; set; } = "";
    public string ToCdkey   { get; set; } = "";
    public string ToName    { get; set; } = "";
    public string Time      { get; set; } = "";
    public string Item      { get; set; } = "";
    public string Pet       { get; set; } = "";
    public long   Gold      { get; set; }
}

// VIP 玩家列（依累儲排序）
public class VipRowDto
{
    public string Account     { get; set; } = "";
    public string OnlineName  { get; set; } = "";
    public string MasterName  { get; set; } = "";
    public long   PayTotal    { get; set; }
    public long   Gold        { get; set; }
    public long   Crystal     { get; set; }
    public bool   IsOnline    { get; set; }
    public string LoginTime   { get; set; } = "";
    public int    VipLevel    { get; set; }
}

// 回收桶
public class RecycleEntryDto
{
    public int      RecycleId   { get; set; }
    public string   DeletedAt   { get; set; } = "";
    public string   DeletedBy   { get; set; } = "";
    public string   Account     { get; set; } = "";
    public string   OnlineName  { get; set; } = "";
    public string   MasterName  { get; set; } = "";
}

// GM 工具帳號
public class AdminUserDto
{
    public int    Id        { get; set; }
    public string Username  { get; set; } = "";
    public string Nickname  { get; set; } = "";
    public bool   IsEnabled { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class BatchGoldRequest
{
    public string Target     { get; set; } = "online"; // all | online | online_recent | custom
    public string CustomList { get; set; } = "";
    public string AccountIds { get; set; } = "";       // 勾選的帳號，逗號分隔（batch 時用）
    public long   Amount     { get; set; }             // 正數=加，負數=扣
}

public class SqlQueryRequest
{
    public string Sql { get; set; } = "";
}

public class AddAdminUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Nickname { get; set; } = "";
}

public class SendItemRequest
{
    public string Account  { get; set; } = "";
    public int    ItemId   { get; set; }
    public int    Quantity { get; set; } = 1;
    public int    Type     { get; set; } = 0;
    public string Title    { get; set; } = "";
    public string Content  { get; set; } = "";
}

public class CartItem     { public int ItemId { get; set; } public int Qty { get; set; } public int Type { get; set; } public string Buff3 { get; set; } = ""; public string Name { get; set; } = ""; }
public class SendCartRequest
{
    public string     Account  { get; set; } = "";
    public List<CartItem> Cart { get; set; } = new();
    public string     Title    { get; set; } = "";
    public string     Content  { get; set; } = "";
}

public class BatchSendCartRequest
{
    public string     Target      { get; set; } = "online"; // all | online | online_recent | custom
    public string     CustomList  { get; set; } = "";
    public List<CartItem> Cart    { get; set; } = new();
    public string     Title       { get; set; } = "";
    public string     Content     { get; set; } = "";
    public string     ExpireDate  { get; set; } = "";
    public List<string> ExcludeList { get; set; } = new(); // 排除帳號
}

public class MailHistoryDto
{
    public int    MailId    { get; set; }
    public string ItemName  { get; set; } = "";
    public int    ItemId    { get; set; }
    public int    Quantity  { get; set; }
    public string SendTime  { get; set; } = "";
    public bool   IsRead    { get; set; }
}

/// <summary>給予儲值：台幣累積進度 + 可選同時發放金幣 + 可選同步累積儲值（與 EXE 一致）</summary>
public class RechargeRequest
{
    public long TwdAmount      { get; set; }
    public long GoldAmount     { get; set; }
    public bool GiveGold       { get; set; } = true;
    /// <summary>是否同步更新 paydata 累積儲值循環進度（預設 true，與 EXE 一致）</summary>
    public bool UpdatePaydata  { get; set; } = true;
    /// <summary>加成百分比 0/5/10/15/20（用於記錄，實際金幣由前端計算後傳入）</summary>
    public int  BonusPercent   { get; set; } = 0;
}

/// <summary>只加累儲顯示（玩家無法領獎）：僅帶台幣金額/差額</summary>
public class DisplayOnlyRequest
{
    public long TwdAmount { get; set; }
}

/// <summary>主帳號分配儲值：單一 CDKEY 的儲值項目</summary>
public class SplitRechargeItem
{
    public string Account    { get; set; } = "";
    public long   TwdAmount  { get; set; }
    public long   GoldAmount { get; set; }
    public bool   GiveGold   { get; set; } = true;
    public int    BonusPct   { get; set; } = 0;
}

// 商城分析
public class ShopItemDto { public int Rank { get; set; } public int ItemId { get; set; } public string ItemName { get; set; } = ""; public long TotalQty { get; set; } public long OrderCount { get; set; } public long TotalCost { get; set; } public string LastTime { get; set; } = ""; }
public class ShopSpenderDto { public int Rank { get; set; } public string Cdkey { get; set; } = ""; public string Name { get; set; } = ""; public long TotalQty { get; set; } public long TotalCost { get; set; } }

// 玩家活躍
public class InactivePlayerDto { public string OnlineName { get; set; } = ""; public string Account { get; set; } = ""; public string LastLogin { get; set; } = ""; public int DaysSince { get; set; } }

// 交易稽核
public class FrequentPairDto { public string FromAccount { get; set; } = ""; public string FromName { get; set; } = ""; public string ToAccount { get; set; } = ""; public string ToName { get; set; } = ""; public int Count { get; set; } public string LastTime { get; set; } = ""; }
public class SameIpTradeDto { public string FromAccount { get; set; } = ""; public string ToAccount { get; set; } = ""; public int Count { get; set; } public string SharedIp { get; set; } = ""; }
public class GoldAnomalyDto { public string Account { get; set; } = ""; public string Name { get; set; } = ""; public long TotalGain { get; set; } public long TotalLoss { get; set; } public int Entries { get; set; } }
public class TopTraderDto { public string Account { get; set; } = ""; public string Name { get; set; } = ""; public int TradeCount { get; set; } public string LastTime { get; set; } = ""; }

// GM 權限
public class GmPermDto { public string Account { get; set; } = ""; public string OnlineName { get; set; } = ""; public int GroupId { get; set; } public int NeiCe { get; set; } public bool IsOnline { get; set; } }
public class SetGmPermRequest { public int NeiCe { get; set; } public int GroupId { get; set; } }

// ── 玩家活動歷程 ────────────────────────────────────────────
public class TradeLogDto
{
    public string Time       { get; set; } = "";
    public string FromCdkey  { get; set; } = "";
    public string FromName   { get; set; } = "";
    public string ToCdkey    { get; set; } = "";
    public string ToName     { get; set; } = "";
    public string Items      { get; set; } = "";
    public string Pets       { get; set; } = "";
    public long   Gold       { get; set; }
    public string Direction  { get; set; } = ""; // "sent" | "received"
}
public class StreetLogDto
{
    public string Time      { get; set; } = "";
    public string SellCdkey { get; set; } = "";
    public string BuyCdkey  { get; set; } = "";
    public string BuyName   { get; set; } = "";
    public string ItemName  { get; set; } = "";
    public int    Num       { get; set; }
    public int    Price     { get; set; }
    public int    Type      { get; set; } // 0=bought/sold
    public string Role      { get; set; } = ""; // "seller"|"buyer"
}
public class SpeedLogDto
{
    public string Time      { get; set; } = "";
    public int    SpeedTime { get; set; }
    public int    SpeedCnt  { get; set; }
}
public class CostLogDto
{
    public string Time  { get; set; } = "";
    public string Name  { get; set; } = "";
    public long   Point { get; set; }
    public int    Check { get; set; }
}
public class ShopLogDto
{
    public string Time      { get; set; } = "";
    public string CharName  { get; set; } = "";
    public int    ItemId    { get; set; }
    public string ItemName  { get; set; } = "";
    public int    ItemNum   { get; set; }
    public int    OldPoint  { get; set; }
    public int    NewPoint  { get; set; }
    public int    Cost      => OldPoint - NewPoint;
    public string ShopType  { get; set; } = ""; // "fame" | "vip"
}
public class VipPointLogDto
{
    public string Time     { get; set; } = "";
    public int    Point    { get; set; }
    public int    OldPoint { get; set; }
    public int    NewPoint { get; set; }
    public string Buff     { get; set; } = "";
}
public class PlayerHistoryResult
{
    public List<TradeLogDto>    Trades      { get; set; } = new();
    public List<StreetLogDto>   Street      { get; set; } = new();
    public List<SpeedLogDto>    Speed       { get; set; } = new();
    public List<CostLogDto>     Cost        { get; set; } = new();
    public List<ShopLogDto>     ShopLogs    { get; set; } = new();
    public List<VipPointLogDto> VipPointLog { get; set; } = new();
    public int TradeSent     { get; set; }
    public int TradeReceived { get; set; }
}

/// <summary>獎池/開獎紀錄 (poolitem)，是否為寶箱骰子開出結果需對照遊戲確認</summary>
public class PoolItemRecordDto
{
    public string Cdkey    { get; set; } = "";
    public string Uid      { get; set; } = "";
    public int    ItemId   { get; set; }
    public string ItemName { get; set; } = "";
}

// ── 攤位 & 商城查詢 DTO ──────────────────────────────────────
public class StreetListingDto
{
    public string CdKey    { get; set; } = "";
    public string CharName { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int    Num      { get; set; }
    public int    Price    { get; set; }
}
public class StreetBuyerDto
{
    public string Time       { get; set; } = "";
    public string SellCdkey  { get; set; } = "";
    public string SellerName { get; set; } = "";
    public string BuyCdkey   { get; set; } = "";
    public string BuyName    { get; set; } = "";
    public string ItemName   { get; set; } = "";
    public int    Num        { get; set; }
    public int    Point      { get; set; }
}
public class StreetItemDto
{
    public string CdKey    { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int    Price    { get; set; }
    public int    Num      { get; set; }
    public int    ItemId   { get; set; }
}
public class StreetSaleDto
{
    public string Time       { get; set; } = "";
    public string SellCdkey  { get; set; } = "";
    public string ItemName   { get; set; } = "";
    public int    Num        { get; set; }
    public int    Point      { get; set; }
    public string BuyCdkey   { get; set; } = "";
    public string BuyName    { get; set; } = "";
}
public class ShopBuyerDto
{
    public string Time     { get; set; } = "";
    public string CdKey    { get; set; } = "";
    public string CharName { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int    ItemNum  { get; set; }
    public int    OldPoint { get; set; }
    public int    NewPoint { get; set; }
    public string ShopType { get; set; } = "";
}
public class VendorSummaryDto
{
    public string CdKey      { get; set; } = "";
    public string CharName   { get; set; } = "";
    public int    ItemCount  { get; set; }
}
public class StreetVendorResult
{
    public string             CdKey        { get; set; } = "";
    public string             CharName     { get; set; } = "";
    public List<StreetItemDto> CurrentItems { get; set; } = new();
    public List<StreetSaleDto> SaleHistory  { get; set; } = new();
}

// ── 信件原始診斷 DTO ─────────────────────────────────────────
public class MailRawDto
{
    public int    Id       { get; set; }
    public int    Type     { get; set; }
    public string Buff1    { get; set; } = "";
    public string Buff2    { get; set; } = "";
    public string RawData  { get; set; } = "";
    public string Buff3    { get; set; } = "";
    public string SendTime { get; set; } = "";
    public bool   IsRead   { get; set; }
    public bool   Deleted  { get; set; }
}

// ── 外部 API 追加請求（供官網後台 GM 操作呼叫）─────────────
public class ExternalSetGoldRequest  { public long Gold    { get; set; } }
public class ExternalBanRequest      { public bool Ban     { get; set; } public int Days { get; set; } = 0; }
public class ExternalSendMailRequest { public string Title { get; set; } = ""; public string Content { get; set; } = ""; }

// ── 外部 API 充值請求（供官網後台呼叫）─────────────────────
/// <summary>
/// 官網後台確認訂單時呼叫此請求體。
/// Header 需帶 X-Api-Key: {ExternalApiKey}
/// </summary>
public class ExternalRechargeRequest
{
    /// <summary>玩家登入帳號</summary>
    public string Account      { get; set; } = "";
    /// <summary>台幣金額（用於累積儲值計算）</summary>
    public long   TwdAmount    { get; set; }
    /// <summary>給予金幣數量</summary>
    public long   GoldAmount   { get; set; }
    /// <summary>是否同時發放金幣（預設 true）</summary>
    public bool   GiveGold     { get; set; } = true;
    /// <summary>是否同步累積儲值循環進度（預設 true）</summary>
    public bool   UpdatePaydata { get; set; } = true;
    /// <summary>訂單編號（選填，記錄用）</summary>
    public string OrderNo      { get; set; } = "";
    /// <summary>備註（選填，記錄用）</summary>
    public string Remark       { get; set; } = "";
}

// ── 加速外掛偵測 ──────────────────────────────────────────────
public class SpeedHackDto
{
    public string Account      { get; set; } = "";
    public string CharName     { get; set; } = "";
    public bool   IsOnline     { get; set; }
    public long   TotalCnt     { get; set; }
    public int    Records      { get; set; }
    public string LastTime     { get; set; } = "";
    public double AvgSpeedTime { get; set; }   // 平均每筆 speedtime（異常持續量）
    public int    MaxSpeedTime { get; set; }   // 最大單筆 speedtime
    public bool   IsBanned     { get; set; }
}

public class BatchBanRequest
{
    public List<string> Accounts { get; set; } = new();
    public int  Days    { get; set; } = 0;
    public double Hours { get; set; } = 0;
}

public class AdjustCostRequest
{
    public long   AddPoint { get; set; }
    public string? CharName { get; set; }
    public int    Quantity  { get; set; } = 1;
}

public class BatchCostResetRequest
{
    public List<string> Accounts  { get; set; } = new();
    public bool         FullReset { get; set; } = false;
}

// ── 玩家寵物（capturepet）────────────────────────────────────
public class PetInfoDto
{
    public string Unicode { get; set; } = "";
    public int    Id      { get; set; }
    public string Name    { get; set; } = "";
    public string Type    { get; set; } = "";
    public int    Lv      { get; set; }
    public int    Hp      { get; set; }
    public int    Attack  { get; set; }
    public int    Def     { get; set; }
    public int    Quick   { get; set; }
    public double Sum     { get; set; }
    public string Author  { get; set; } = "";
    public string Cdkey   { get; set; } = "";
    public int    Check   { get; set; }  // 0=揹包 1=出戰
}

public class RemovePetRequest
{
    public string Unicode { get; set; } = "";
}

public class GuildInfo
{
    public int    GuildId     { get; set; }
    public string GuildName   { get; set; } = "";
    public int    MemberCount { get; set; }
    public string LastActive  { get; set; } = "";
    public long   ShopContrib { get; set; }
}

public class GuildMember
{
    public string Cdkey       { get; set; } = "";
    public string CharName    { get; set; } = "";
    public string OnlineName  { get; set; } = "";
    public string JoinTime    { get; set; } = "";
    public int    PayTotal    { get; set; }
    public long   Gold        { get; set; }
    public bool   IsOnline    { get; set; }
    public long   ShopContrib { get; set; }
}

public class TransferMemberRequest
{
    public string Cdkey           { get; set; } = "";
    public int    TargetGuildId   { get; set; }
    public string TargetGuildName { get; set; } = "";
}
