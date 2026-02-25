namespace WebApi.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string Role);

public class PlayerRow
{
    public string Account    { get; set; } = "";
    public string OnlineName { get; set; } = "";
    public bool   IsOnline   { get; set; }
    public int    ServerId   { get; set; }
    public string RegTime    { get; set; } = "";
    public string LoginTime  { get; set; } = "";
    public string IP         { get; set; } = "";
    public bool   IsBanned   { get; set; }
    public long   Gold       { get; set; }
    public long   Crystal    { get; set; }
    public int    PetCount   { get; set; }
}

public class PlayerDetail : PlayerRow
{
    public string RegIP       { get; set; } = "";
    public string Uid         { get; set; } = "";
    public string MAC         { get; set; } = "";
    public int    TotalMails  { get; set; }
    public int    UnreadMails { get; set; }
    public bool   IsMuted     { get; set; }
    public string BanEndTime  { get; set; } = "";
    public long   PayTotal    { get; set; }
}

public class SetCurrencyRequest
{
    public long Value { get; set; }
}

public class BanRequest
{
    public bool   Ban     { get; set; }
    public string Reason  { get; set; } = "";
    public int    Days    { get; set; }  // 0 = 永久
}

public class DashboardStats
{
    public int  TotalPlayers   { get; set; }
    public int  OnlinePlayers  { get; set; }
    public int  BannedPlayers  { get; set; }
    public int  NewToday       { get; set; }
    public long TotalGold      { get; set; }
    public long TotalCrystal   { get; set; }
}
