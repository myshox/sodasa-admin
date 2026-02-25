using System;
using MySqlConnector;
string cs = ""Server=141.140.14.43;Database=sqsd;User ID=sqsd;Password=sarFGSEKJdJrnaFc;Connection Timeout=8;charset=utf8mb4;"";
await using var conn = new MySqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new MySqlCommand(""SHOW TABLES"", conn);
await using var r = await cmd.ExecuteReaderAsync();
while(await r.ReadAsync()) Console.WriteLine(r[0]);
