using MySqlConnector;
string cs = "Server=141.140.14.43;Database=sqsd;User ID=sqsd;Password=sarFGSEKJdJrnaFc;Connection Timeout=8;";
await using var conn = new MySqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new MySqlCommand("SHOW COLUMNS FROM csalogin", conn);
await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync())
    Console.WriteLine($"{r["Field"],-25} {r["Type"],-25} {r["Null"]}");
