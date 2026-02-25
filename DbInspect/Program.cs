using System;
using System.Threading.Tasks;
using MySqlConnector;

string cs = "Server=141.140.14.43;Database=sqsd;User ID=sqsd;Password=sarFGSEKJdJrnaFc;Connection Timeout=8;charset=utf8mb4;";

// 從購買紀錄反推商城現有商品列表
Console.WriteLine("=== 金幣商城 (vipshop) 曾出現的商品 ===");
await using var c1 = new MySqlConnection(cs); await c1.OpenAsync();
await using var s1 = new MySqlCommand(
    "SELECT itemid, itemname, COUNT(*) as buytimes, SUM(itemnum) as totalqty, " +
    "MAX(oldpoint-newpoint) as maxprice, MIN(oldpoint-newpoint) as minprice " +
    "FROM vipshop GROUP BY itemid, itemname ORDER BY buytimes DESC", c1);
await using var r1 = await s1.ExecuteReaderAsync();
Console.WriteLine($"{"道具ID",8} {"道具名稱",20} {"購買次數",8} {"總數量",8} {"最高價",8} {"最低價",8}");
Console.WriteLine(new string('-', 72));
while (await r1.ReadAsync())
    Console.WriteLine($"{r1["itemid"],8} {r1["itemname"],20} {r1["buytimes"],8} {r1["totalqty"],8} {r1["maxprice"],8} {r1["minprice"],8}");

Console.WriteLine("\n=== 聲望商城 (fameshop) 曾出現的商品 ===");
await using var c2 = new MySqlConnection(cs); await c2.OpenAsync();
await using var s2 = new MySqlCommand(
    "SELECT itemid, itemname, COUNT(*) as buytimes, SUM(itemnum) as totalqty, " +
    "MAX(oldpoint-newpoint) as maxprice, MIN(oldpoint-newpoint) as minprice " +
    "FROM fameshop GROUP BY itemid, itemname ORDER BY buytimes DESC", c2);
await using var r2 = await s2.ExecuteReaderAsync();
Console.WriteLine($"{"道具ID",8} {"道具名稱",20} {"購買次數",8} {"總數量",8} {"最高價",8} {"最低價",8}");
Console.WriteLine(new string('-', 72));
while (await r2.ReadAsync())
    Console.WriteLine($"{r2["itemid"],8} {r2["itemname"],20} {r2["buytimes"],8} {r2["totalqty"],8} {r2["maxprice"],8} {r2["minprice"],8}");
