using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

var cs = "Server=141.140.14.43;Database=sqsd;User ID=sqsd;Password=sarFGSEKJdJrnaFc;Connection Timeout=8;charset=utf8mb4;";
using var conn = new MySqlConnection(cs);
await conn.OpenAsync();

// 1. 找所有含玩家記錄且含整數欄位的表，顯示所有整數值
Console.WriteLine("=== 含玩家 acfc4a79c3f5 且有整數欄位的所有表（掃描值 2733）===");
using var tabCmd = new MySqlCommand("SHOW TABLES", conn);
using var tabR = await tabCmd.ExecuteReaderAsync();
var tables = new List<string>();
while (await tabR.ReadAsync()) tables.Add(tabR[0].ToString()!);
tabR.Close();

foreach (var tbl in tables)
{
    try
    {
        using var desc = new MySqlCommand($"DESCRIBE `{tbl}`", conn);
        using var dr = await desc.ExecuteReaderAsync();
        var allCols = new List<string>();
        var intCols = new List<string>();
        while (await dr.ReadAsync())
        {
            allCols.Add(dr[0].ToString()!);
            string typ = dr[1].ToString()!;
            if (typ.Contains("int") || typ.Contains("decimal") || typ.Contains("float") || typ.Contains("double"))
                intCols.Add(dr[0].ToString()!);
        }
        dr.Close();
        bool hasCdkey = allCols.Contains("cdkey");
        bool hasName  = allCols.Contains("Name") || allCols.Contains("name");
        if (!hasCdkey && !hasName) continue;
        string whereClause = hasCdkey ? "cdkey='acfc4a79c3f5'" : "(`Name`='acfc4a79c3f5' OR `name`='acfc4a79c3f5')";
        if (intCols.Count == 0) continue;
        using var c = new MySqlCommand($"SELECT * FROM `{tbl}` WHERE {whereClause} LIMIT 10", conn);
        using var r = await c.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                var val = r[i];
                if (val is int iv && iv == 2733)
                    Console.WriteLine($"  ★ [{tbl}].{r.GetName(i)} = 2733 !!!!");
                else if (val is long lv && lv == 2733)
                    Console.WriteLine($"  ★ [{tbl}].{r.GetName(i)} = 2733 !!!!");
                else if (val is short sv && sv == 2733)
                    Console.WriteLine($"  ★ [{tbl}].{r.GetName(i)} = 2733 !!!!");
            }
        }
    }
    catch { }
}

// 2. 查 gold 表結構（它是啥用途？）
Console.WriteLine("\n=== gold 表全部欄位與說明 ===");
using (var d = new MySqlCommand("DESCRIBE gold; SELECT * FROM gold WHERE cdkey='acfc4a79c3f5'", conn))
using (var dr = await d.ExecuteReaderAsync())
{
    Console.Write("欄位: ");
    while (await dr.ReadAsync()) Console.Write($"{dr[0]}({dr[1]})  ");
    Console.WriteLine();
    while (dr.NextResult())
        while (await dr.ReadAsync())
        {
            for (int i = 0; i < dr.FieldCount; i++) Console.Write($"{dr.GetName(i)}={dr[i]}  ");
            Console.WriteLine();
        }
}

// 3. 查 mmexp / huoyue / chengjiu 表（可能含戰點）
Console.WriteLine("\n=== mmexp / huoyue / chengjiu / damages 玩家資料 ===");
foreach (var tbl in new[]{"mmexp","huoyue","chengjiu","damages","autopkdata","autopk","dwpkdata"})
{
    try
    {
        using var d2 = new MySqlCommand($"DESCRIBE `{tbl}`", conn);
        using var dr2 = await d2.ExecuteReaderAsync();
        var cols2 = new List<string>();
        while (await dr2.ReadAsync()) cols2.Add($"{dr2[0]}({dr2[1]})");
        dr2.Close();
        Console.WriteLine($"  [{tbl}] 欄位: {string.Join(", ", cols2)}");
        string where2 = cols2.Any(c => c.StartsWith("cdkey")) ? "cdkey='acfc4a79c3f5'" : "Name='acfc4a79c3f5'";
        using var c2 = new MySqlCommand($"SELECT * FROM `{tbl}` WHERE {where2} LIMIT 3", conn);
        using var r2 = await c2.ExecuteReaderAsync();
        while (await r2.ReadAsync())
        {
            Console.Write("    → ");
            for (int i = 0; i < r2.FieldCount; i++) Console.Write($"{r2.GetName(i)}={r2[i]}  ");
            Console.WriteLine();
        }
    }
    catch (Exception ex) { Console.WriteLine($"  [{tbl}] 錯誤: {ex.Message}"); }
}
