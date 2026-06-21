using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace SQ_Email_Tools
{
    // ══════════════════════════════════════════════════════════
    // 伺服器設定（角色存檔資料夾路徑等）
    // 儲存於 exe 同目錄的 server_settings.json
    // ══════════════════════════════════════════════════════════
    public class ServerSettings
    {
        private static ServerSettings _instance;
        public static ServerSettings Instance => _instance ??= Load();

        private static readonly string _cfgPath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppDomain.CurrentDomain.BaseDirectory,
            "server_settings.json");

        /// <summary>
        /// 伺服器角色存檔資料夾根目錄。
        /// 石器私服角色資料通常存於 [此路徑]/[角色名稱]/ 子資料夾中。
        /// 範例（本機）：C:\GameServer\role\
        /// 範例（網路）：\\192.168.1.100\GameShare\role\
        /// 若留空，改名時只更新資料庫，需手動重命名伺服器檔案。
        /// </summary>
        public string RoleDataPath { get; set; } = "";

        public void Save()
        {
            try { File.WriteAllText(_cfgPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }

        private static ServerSettings Load()
        {
            try
            {
                if (File.Exists(_cfgPath))
                    return JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(_cfgPath)) ?? new ServerSettings();
            }
            catch { }
            return new ServerSettings();
        }

        /// <summary>
        /// 嘗試在磁碟上重命名角色資料夾（舊名→新名）。
        /// 回傳 (成功, 訊息)。
        /// </summary>
        public (bool ok, string msg) RenameRoleFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(RoleDataPath))
                return (false, "未設定伺服器角色資料夾路徑，請到「⚙ 伺服器設定」中設定。");

            string oldPath = Path.Combine(RoleDataPath, oldName);
            string newPath = Path.Combine(RoleDataPath, newName);

            if (!Directory.Exists(oldPath) && !File.Exists(oldPath))
                return (false, $"找不到角色資料夾：{oldPath}\n請確認路徑是否正確。");

            if (Directory.Exists(newPath) || File.Exists(newPath))
                return (false, $"目標名稱已存在：{newPath}\n請先確認是否有同名角色。");

            try
            {
                if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);
                else
                    File.Move(oldPath, newPath);
                return (true, $"已將角色資料夾從\n「{oldPath}」\n重命名為\n「{newPath}」");
            }
            catch (Exception ex)
            {
                return (false, $"重命名角色資料夾失敗：{ex.Message}");
            }
        }
    }
    public class ItemInfo
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public string Description { get; set; } = "";
        public bool IsPet { get; set; }

        public override string ToString() => $"{Name}  #{Id}";
    }

    // ══════════════════════════════════════════════════════════
    // VIP 等級輔助工具
    // ══════════════════════════════════════════════════════════
    public static class VipHelper
    {
        public const  long   GoldThreshold    = 5_000L;
        public const  long   DiamondThreshold = 10_000L;
        public const  double GoldBonus        = 0.10;
        public const  double DiamondBonus     = 0.15;

        /// <returns>(level 0/1/2, emoji, label, bonusRate)</returns>
        public static (int level, string emoji, string label, double bonusRate) GetTier(long payTotal)
        {
            if (payTotal >= DiamondThreshold) return (2, "🔹", "鑽石 VIP", DiamondBonus);
            if (payTotal >= GoldThreshold)    return (1, "🔸", "黃金 VIP", GoldBonus);
            return (0, "", "一般玩家", 0.0);
        }

        /// <summary>計算含 VIP 加成後的金幣數</summary>
        public static long ApplyBonus(long gold, double bonusRate)
            => gold + (long)Math.Round(gold * bonusRate);

        /// <summary>距離下一個 VIP 等級還差多少台幣（已最高回傳 -1）</summary>
        public static long GapToNext(long payTotal)
        {
            if (payTotal < GoldThreshold)    return GoldThreshold    - payTotal;
            if (payTotal < DiamondThreshold) return DiamondThreshold - payTotal;
            return -1;
        }

        /// <summary>VIP 加成百分比（整數，如 10 = +10%）</summary>
        public static int BonusPercent(long payTotal)
            => (int)(GetTier(payTotal).bonusRate * 100);
    }

    public class PlayerInfo
    {
        public int    MasterId        { get; set; }
        public string Account         { get; set; }
        public string OnlineName      { get; set; }
        public bool   IsOnline        { get; set; }
        public string LoginTime       { get; set; }
        public string ServerId        { get; set; }
        public bool   IsBanned        { get; set; }
        public string BanEndTime      { get; set; }
        /// <summary>累積充值（金幣）= paydata.point</summary>
        public long   PayTotal        { get; set; }
        /// <summary>持有寵物數量（capturepet）</summary>
        public int    PetCount        { get; set; }
        /// <summary>主帳號名稱（csaloginmaster.Name）</summary>
        public string MasterName      { get; set; } = "";
        /// <summary>csalogin 的自動遞增主鍵 id（0 = 表示此欄位不存在）</summary>
        public int    CharDbId        { get; set; } = 0;

        public string OnlineText => IsOnline ? "🟢 在線" : "⚫ 離線";
    }

    // ── 主帳號 ────────────────────────────────────────
    public class MasterAccount
    {
        public int    Id         { get; set; }
        public string Name       { get; set; } = "";
        public int    SubCount   { get; set; }
        public string CreatedAt  { get; set; } = "";
        /// <summary>旗下所有子帳號列表</summary>
        public List<PlayerInfo> SubAccounts { get; set; } = new();
    }

    public class MailRecord
    {
        public int    Id        { get; set; }
        public int    Type      { get; set; }
        public string Buff1     { get; set; }
        public string Buff2     { get; set; }
        public int    Data      { get; set; }
        public string RawData   { get; set; } = "";
        public int    SendTime  { get; set; }
        public int    EndTime   { get; set; }
        public int    CheckFlag { get; set; }
        public int    Deleamill { get; set; }
        public string Buff3     { get; set; }
        public string Cdkey     { get; set; }
        public string Operator  { get; set; } = "";

        public string TypeStr     => Type == 2 ? "🐾 寵物" : "📦 道具";
        public string SendTimeStr => DateTimeOffset.FromUnixTimeSeconds(SendTime).LocalDateTime.ToString("yyyy/MM/dd HH:mm");
        public string EndTimeStr  => DateTimeOffset.FromUnixTimeSeconds(EndTime).LocalDateTime.ToString("yyyy/MM/dd");
        public string StatusStr   => CheckFlag == 1 ? "✓ 已領取" : "○ 未領取";
    }

    public class SendMailRequest
    {
        public string Cdkey     { get; set; }
        public int    Type      { get; set; } = 1;
        public string Buff1     { get; set; }
        public string Buff2     { get; set; }
        public int    Data      { get; set; }
        public int    StartTime { get; set; }
        public int    EndTime   { get; set; }
        public string Buff3     { get; set; }
        public string Operator  { get; set; } = "";
        // maildata 無 num 欄，數量 > 1 時插入多筆
        // 對應 [gm additem/newsend 編號 數量 帳號]
        public int    Quantity  { get; set; } = 1;
    }

    // ── 郵件範本（標題/內容/購物車，與網頁版範例紀錄一致）──────────────────
    public class MailTemplateCartItem
    {
        public int    ItemId { get; set; }
        public int    Qty    { get; set; } = 1;
        public int    Type   { get; set; } = 1;  // 1=道具 2=寵物 等
        public string Name   { get; set; } = "";  // 選填，顯示用
        /// <summary>網頁版購物車欄位（寵物等）</summary>
        public string Buff3  { get; set; } = "";
    }

    public class MailTemplate
    {
        /// <summary>網頁版範例 id（與伺服器 mail_templates 同步）</summary>
        public string   WebId     { get; set; } = "";
        public string   Name      { get; set; }
        public int      Type      { get; set; }
        public int      Data      { get; set; }
        public string   Buff1     { get; set; }  // 標題
        public string   Buff2     { get; set; }  // 內容
        public string   Buff3     { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        /// <summary>儲存的購物車（載入範本時一併還原）</summary>
        public List<MailTemplateCartItem> Cart { get; set; } = new List<MailTemplateCartItem>();
    }

    // ── GM 操作日誌 ───────────────────────────────────
    public class GmLogEntry
    {
        public DateTime Time     { get; set; } = DateTime.Now;
        public string   Operator { get; set; }
        public string   Action   { get; set; }
        public string   Target   { get; set; }
        public string   Detail   { get; set; }
        public bool     Success  { get; set; }
        /// <summary>來源：exe（工具）或 web（網頁）</summary>
        public string   Source   { get; set; } = "exe";
    }

    // ── 玩家貨幣（csalogin 可讀寫欄位）────────────────
    public class PlayerCurrencies
    {
        /// <summary>金幣 = csalogin.VipPoint</summary>
        public long Gold     { get; set; }
        /// <summary>水晶 = csalogin.PetPoint</summary>
        public long Crystal  { get; set; }
        /// <summary>充值點 = csalogin.PayPoint</summary>
        public long PayPoint { get; set; }
        /// <summary>R幣 = csalogin.RmbPoint</summary>
        public long RmbPoint { get; set; }
        // 石幣 / 聲望 / 戰點 存於伺服器二進位角色檔案，不在 MySQL
    }

    // ── 寵物四圍素質摘要 ──────────────────────────────
    public class PetSummary
    {
        public int    Count      { get; set; }   // 持有數量
        public string BestName   { get; set; } = "";
        public int    BestId     { get; set; }
        public int    BestLv     { get; set; }
        public int    BestHp     { get; set; }
        public int    BestAttack { get; set; }
        public int    BestDef    { get; set; }
        public int    BestQuick  { get; set; }
        public double BestSum    { get; set; }   // 評分 / 戰力
        public string BestAuthor { get; set; } = ""; // 捕捉者角色名

        public bool HasPet => Count > 0;
    }

    // ── 玩家完整資料 ──────────────────────────────────
    public class PlayerDetail
    {
        public string OnlineName { get; set; }
        public string Account    { get; set; }
        public string IP         { get; set; }
        public string RegIP      { get; set; }
        public string RegTime    { get; set; }
        public string LoginTime  { get; set; }
        public bool   IsOnline   { get; set; }
        public bool   IsMuted    { get; set; }
        public bool   IsBanned   { get; set; }
        public string BanEndTime { get; set; }
        public int    GroupId    { get; set; }
        public string GroupName  { get; set; }
        public int    NeiCe      { get; set; }   // 0=一般, 1=GM/內測
        public int    ServerId   { get; set; }
        public string ServerName { get; set; }
        public long   Gold       { get; set; }
        public long   Crystal    { get; set; }
        public long   PayPoint   { get; set; }
        public long   RmbPoint   { get; set; }
        /// <summary>遊戲面板當前循環進度 = paydata.point（遊戲「累積充值獎勵」介面直接讀此欄）</summary>
        public long   PayTotal         { get; set; }
        /// <summary>csalogin.PayTotal（玩家資料卡顯示 / VIP 分層用，可能與 paydata.point 不同步）</summary>
        public long   CsaPayTotal      { get; set; }
        /// <summary>歷史總累積儲值（永不歸零）= paydata.lifetime_total</summary>
        public long   LifetimePayTotal { get; set; }
        /// <summary>已完成循環數 = paydata.totalcheck</summary>
        public long   TotalCheck       { get; set; }
        /// <summary>領獎狀態 = paydata.check（0=可領, 1=已領）</summary>
        public int    PaydataCheck     { get; set; } = 1;
        /// <summary>是否有未領的循環獎勵（check==0 且 totalCheck>0）</summary>
        public bool   ClaimReady       => PaydataCheck == 0 && TotalCheck > 0;

        // ── 累計消費達成獎勵（costdata，與累積儲值 paydata 平行）────────
        /// <summary>消費達成累計點數 = costdata.point（花費的金幣總計）</summary>
        public long CostPoint  { get; set; }
        /// <summary>已領取的里程碑數 = costdata.check（對應遊戲「消費達成獎勵」）</summary>
        public int  CostCheck  { get; set; } = -1; // -1 表示無記錄
        public string QQ         { get; set; }
        public string Uid        { get; set; }
        public string MAC        { get; set; }
        public int    PetCount    { get; set; }
        public int    TotalMails  { get; set; }
        public int    UnreadMails { get; set; }
        /// <summary>主帳號名稱（csaloginmaster.Name）</summary>
        public string MasterName  { get; set; } = "";
        /// <summary>最強寵物四圍素質摘要</summary>
        public PetSummary TopPet { get; set; } = new PetSummary();
        /// <summary>輩份（csalogin.Belong），-1 表示欄位不存在</summary>
        public int Belong { get; set; } = -1;
        /// <summary>csalogin 的自動遞增主鍵 id（0 = 表示此欄位不存在）</summary>
        public int CharDbId { get; set; } = 0;
        /// <summary>遊戲登入密碼（csalogin.PassWord）</summary>
        public string Password     { get; set; } = "";
        /// <summary>安全密碼（csalogin.SafePasswd，通常為 MD5）</summary>
        public string SafePassword { get; set; } = "";
    }

    // ── 遊戲內 GM 資訊 ────────────────────────────────
    public class GameGmInfo
    {
        public string Account    { get; set; }
        public string OnlineName { get; set; }
        public int    GroupId    { get; set; }
        public int    NeiCe      { get; set; }
        public bool   IsOnline   { get; set; }

        /// <summary>是否符合 GM 條件（NeiCe=1 或 GroupId 符合設定值）</summary>
        public bool IsGm(int gmGroupId) => NeiCe == 1 || GroupId == gmGroupId;
    }

    // ── GM 帳號 ───────────────────────────────────────
    public class AdminUser
    {
        public int    Id        { get; set; }
        public string Username  { get; set; }
        public string Nickname  { get; set; }
        public bool   IsEnabled { get; set; }
        public string CreatedAt { get; set; }
    }

    // ── 統計資訊 ──────────────────────────────────────
    public class ServerStats
    {
        public int     OnlineCount     { get; set; }
        public int     TotalPlayers    { get; set; }
        public int     TodayNewPlayers { get; set; }
        public int     TodayActive     { get; set; }
        public int     TotalMails      { get; set; }
        public int     UnreadMails     { get; set; }
        // 充值統計
        public decimal TodayRevenue    { get; set; }  // 今日充值總額（元寶）
        public decimal TotalRevenue    { get; set; }  // 歷史充值總額（元寶）
        public int     TodayOrders     { get; set; }  // 今日訂單數
        public List<RechargeRankItem> TopRechargersAllTime { get; set; } = new();
    }

    // ── 充值記錄（recharge_orders）────────────────────
    // Amount 欄位 = 元寶數量（遊戲幣），1 台幣 = 100 元寶
    public class RechargeRecord
    {
        public int     Id          { get; set; }
        public string  OrderNo     { get; set; }
        /// <summary>帳號（cdkey），即 role_name 欄位</summary>
        public string  RoleName    { get; set; }
        /// <summary>角色名稱（從 csalogin.OnlineName JOIN 取得）</summary>
        public string  CharName    { get; set; } = "";
        public string  ProductName { get; set; }
        /// <summary>充值獲得的元寶數量（遊戲幣，非台幣）</summary>
        public decimal Amount      { get; set; }
        public string  Status      { get; set; }
        public string  CreatedAt   { get; set; }
        /// <summary>資料來源：orders = recharge_orders, paydata = paydata 補充</summary>
        public string  Source      { get; set; } = "orders";

        public string StatusText  => Source == "paydata" ? "付費記錄" : (Status == "completed" ? "✓ 完成" : Status == "failed" ? "✗ 失敗" : "⏳ 待處理");
        /// <summary>顯示格式：角色名稱 (帳號)</summary>
        public string DisplayName => string.IsNullOrEmpty(CharName) ? RoleName : $"{CharName}\n({RoleName})";
        /// <summary>元寶顯示（遊戲幣）</summary>
        public string YuanbaoText => Source == "paydata" ? $"累計 {Amount:N0} 元寶" : $"{Amount:N0} 元寶";
        /// <summary>台幣換算（元寶 ÷ 100）</summary>
        public decimal TwdAmount  => Source == "paydata" ? Amount : Amount / 100m;
        public string  TwdText    => $"NT$ {TwdAmount:N0}";
    }

    // ── 交易記錄（tradelog）──────────────────────────
    public class TradeRecord
    {
        public string FromCdkey  { get; set; }
        public string FromName   { get; set; }
        public string ToCdkey    { get; set; }
        public string ToName     { get; set; }
        public string Time       { get; set; }
        public string Item       { get; set; }
        public string Pet        { get; set; }
        public long   Gold       { get; set; }

        public string TypeText =>
            !string.IsNullOrEmpty(Item) && !string.IsNullOrEmpty(Pet) ? "道具+寵物" :
            !string.IsNullOrEmpty(Item) ? "📦 道具" :
            !string.IsNullOrEmpty(Pet)  ? "🐾 寵物" :
            Gold > 0                    ? "💰 金幣" : "—";
        public string GoldText => Gold > 0 ? $"{Gold:N0}" : "—";
        public string ContentSummary =>
            !string.IsNullOrEmpty(Item) ? Item.Length > 40 ? Item[..40] + "…" : Item :
            !string.IsNullOrEmpty(Pet)  ? Pet.Length  > 40 ? Pet[..40]  + "…" : Pet  : "—";
    }

    // ── 商城熱賣統計 ─────────────────────────────────
    public class ShopSaleRecord
    {
        public int    ItemId      { get; set; }
        public string ItemName    { get; set; } = "";
        public long   TotalQty    { get; set; }  // 總購買數量
        public long   OrderCount  { get; set; }  // 購買次數（筆數）
        public long   TotalCost   { get; set; }  // 總消耗點數
        public string LastBuyTime { get; set; } = "";
        public int    Rank        { get; set; }
    }

    // ── 商城玩家消費排行 ──────────────────────────────
    public class ShopSpenderRecord
    {
        public string Cdkey    { get; set; } = "";
        public string Name     { get; set; } = "";
        public long   TotalQty { get; set; }
        public long   TotalCost{ get; set; }
        public int    Rank     { get; set; }
    }

    // ── 角色回收桶（刪除備份）────────────────────────
    public class RecycleEntry
    {
        public int      RecycleId    { get; set; }
        public DateTime DeletedAt    { get; set; }
        public string   DeletedBy    { get; set; } = "";
        public string   Account      { get; set; } = "";
        public string   OnlineName   { get; set; } = "";
        public string   MasterName   { get; set; } = "";
        public string   OriginalData { get; set; } = "{}";
    }

    // ── 寵物詳細資料（capturepet）────────────────────
    public class PetInfo
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
        public int    Check   { get; set; }

        public string StatusText => Check == 0 ? "揹包中" : Check == 1 ? "出戰中" : $"狀態{Check}";
    }

    // ── 待給予道具佇列（itempetgetdata）──────────────
    public class ItemQueueEntry
    {
        public int    MailId   { get; set; }   // maildata.id（真正的 PK，用於刪除）
        public string Cdkey    { get; set; } = "";
        public string CharName { get; set; } = "";
        public int    ItemId   { get; set; }
        public string ItemName { get; set; } = "";
        public string EndDate  { get; set; } = "";
    }

    // ── 充值排行記錄 ──────────────────────────────────
    public class RechargeRankItem
    {
        public string RoleName  { get; set; } = "";
        public string CharName  { get; set; } = "";
        public decimal Total    { get; set; }
        public int    Count     { get; set; }
        public string DisplayName => string.IsNullOrEmpty(CharName) ? RoleName : $"{CharName}（{RoleName}）";
        public string TwdText  => $"NT$ {Total / 100m:N0}";
        public string YuanText => $"{Total:N0} 元寶";
    }

    // ── 金幣異動日誌（VipPointLog）───────────────────
    public class GoldLogRecord
    {
        public string Cdkey    { get; set; }
        /// <summary>角色名稱（從 csalogin.OnlineName JOIN 取得）</summary>
        public string CharName { get; set; } = "";
        public long   Point    { get; set; }
        public long   OldPoint { get; set; }
        public long   NewPoint { get; set; }
        public string Buff     { get; set; }
        public string Time     { get; set; }

        /// <summary>顯示格式：角色名稱 (帳號)</summary>
        public string DisplayName => string.IsNullOrEmpty(CharName) ? Cdkey : $"{CharName}\n({Cdkey})";
        public string PointText   => Point >= 0 ? $"+{Point:N0}" : $"{Point:N0}";
        public Color  PointColor  => Point >= 0 ? Color.FromArgb(80, 210, 100) : Color.FromArgb(230, 80, 80);
    }

    // ── 加速外掛偵測 ──────────────────────────────────────────────
    public class SpeedHackEntry
    {
        public string Account      { get; set; } = "";
        public string CharName     { get; set; } = "";
        public bool   IsOnline     { get; set; }
        public long   TotalCnt     { get; set; }
        public int    Records      { get; set; }
        public string LastTime     { get; set; } = "";
        public double AvgSpeedTime { get; set; }
        public int    MaxSpeedTime { get; set; }
        public bool   IsBanned     { get; set; }
    }

    // ── 伺服器狀態（最新註冊 / 分流在線 / 主帳號統計）──────────────

    public class RecentRegAccount
    {
        public string Account    { get; set; } = "";
        public string CharName   { get; set; } = "";
        public string MasterName { get; set; } = "";
        public string RegTime    { get; set; } = "";
        public string RegIP      { get; set; } = "";
        public string ServerName { get; set; } = "";
        public bool   IsOnline   { get; set; }
    }

    public class ChannelOnlineEntry
    {
        public int    ServerId    { get; set; }
        public string ServerName  { get; set; } = "";
        public int    OnlineCount { get; set; }
        public int    TotalCount  { get; set; }
    }

    /// <summary>依目前登入 IP（csalogin.IP）彙總：在線人數與該 IP 下帳號總數</summary>
    public class OnlineIpEntry
    {
        public string Ip          { get; set; } = "";
        public int    OnlineCount { get; set; }
        public int    TotalCount  { get; set; }
    }

    /// <summary>登入 IP 區塊頂部：全服在線總人數與 IP 維度統計</summary>
    public class OnlineLoginIpSummary
    {
        /// <summary>全服在線角色數（csalogin Online=1）</summary>
        public int TotalOnline { get; set; }
        /// <summary>至少有一人在線的相異登入 IP 數</summary>
        public int DistinctIpWithOnline { get; set; }
        /// <summary>有填登入 IP 的相異 IP 數（含全離線）</summary>
        public int DistinctIpAll { get; set; }
        /// <summary>在線但登入 IP 為空（無法列入 IP 表）</summary>
        public int OnlineWithoutLoginIp { get; set; }
    }

    public class MasterAccountStats
    {
        public int TotalMasters   { get; set; }
        public int OnlineMasters  { get; set; }
        public int OfflineMasters => TotalMasters - OnlineMasters;
    }

    // ══════════════════════════════════════════════════════════
    // 寵物排行榜資料列
    // ══════════════════════════════════════════════════════════
    public class PetRankRow
    {
        public int    Rank       { get; set; }
        public string Cdkey      { get; set; } = "";
        public string Author     { get; set; } = "";
        public string PetName    { get; set; } = "";
        public string PetType    { get; set; } = "";
        public int    PetId      { get; set; }
        public int    Lv         { get; set; }
        public int    Hp         { get; set; }
        public int    Attack     { get; set; }
        public int    Def        { get; set; }
        public int    Quick      { get; set; }
        public double Sum        { get; set; }
        public string PlayerName { get; set; } = "";
        public bool   Online     { get; set; }
    }

    /// <summary>練寵活動排行單筆（capturepet，每人最高戰力一筆；同分取較晚提交）</summary>
    public class CaptureRankEntry
    {
        public int    Rank       { get; set; }
        public string Unicode    { get; set; } = "";
        public string Author     { get; set; } = "";
        public string Cdkey      { get; set; } = "";
        public string PetName    { get; set; } = "";
        public int    PetId      { get; set; }
        public int    Lv         { get; set; }
        public int    Hp         { get; set; }
        public int    Attack     { get; set; }
        public int    Def        { get; set; }
        public int    Quick      { get; set; }
        public double Sum        { get; set; }
        public bool   Check      { get; set; }
        public string InsertTime { get; set; } = "";
        public int    EntryCount { get; set; }
        public bool   IsOnline   { get; set; }
    }

    // ══════════════════════════════════════════════════════════
    // 家族相關模型
    // ══════════════════════════════════════════════════════════
    public class FamilyInfo
    {
        public int    FamilyId    { get; set; }
        public string FamilyName  { get; set; } = "";
        public int    MemberCount { get; set; }
        public string LastActive  { get; set; } = "";
        public long   ShopContrib { get; set; }
    }

    public class FamilyMember
    {
        public string Cdkey       { get; set; } = "";
        public string CharName    { get; set; } = "";
        public string OnlineName  { get; set; } = "";
        public string JoinTime    { get; set; } = "";
        public int    PayTotal    { get; set; }
        public long   Gold        { get; set; }
        public bool   IsOnline    { get; set; }
        public long   ShopContrib { get; set; }
        public bool   IsLeader    { get; set; }
        public int    Role        { get; set; }  // 0=成員, 1=族長, 2=長老
        public string RoleLabel => Role == 1 ? "\u2654 \u65cf\u9577" : Role == 2 ? "\u2605 \u9577\u8001" : "\u6210\u54e1";
    }

}
