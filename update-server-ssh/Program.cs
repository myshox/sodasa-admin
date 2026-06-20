using System;
using System.Collections.Generic;
using System.Linq;
using Renci.SshNet;

class Program
{
    static void Main(string[] cliArgs)
    {
        if (cliArgs.Length > 0 && cliArgs[0] == "dbcheck")
        {
            RunDbCheck();
            return;
        }

        var host = "172.234.95.180";
        var user = "root";
        var pass = "~QbW(8c8tXAKM*f3v";

        using var client = new SshClient(host, user, pass);
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
        client.Connect();
        Console.WriteLine("=== 連線成功，開始更新... ===\n");

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1] == "sh")
        {
            var cmd = Environment.GetEnvironmentVariable("REMOTE_CMD") ?? "";
            Console.WriteLine("$ " + cmd + "\n");
            var r = client.RunCommand(cmd);
            Console.WriteLine(r.Result.TrimEnd());
            if (!string.IsNullOrWhiteSpace(r.Error)) Console.WriteLine("[stderr] " + r.Error.TrimEnd());
            Console.WriteLine($"[exit {r.ExitStatus}]");
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "fulldeploy")
        {
            const string NEWCS = "Server=141.140.14.61;Port=3306;Database=sqsd;User ID=sqsd;Password=sarFGSEKJdJrnaFc;Connection Timeout=8;charset=utf8mb4;";
            var unit = @"[Unit]
Description=SodaGM Web Tool
After=network.target

[Service]
WorkingDirectory=/opt/gmtool/publish
ExecStart=/usr/bin/dotnet --roll-forward LatestMajor /opt/gmtool/publish/WebApi.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5050
Environment=GmAccounts__0__Username=admin
Environment=GmAccounts__0__Password=@Aa31178375
Environment=GmAccounts__0__Role=superadmin
Environment=Jwt__Secret=kQ7nRwXz3YpM8fH2sL4vBt6cE1gJuA9dNhT5bVaKoXeFmP0rCyZqDsIlUwGhJxNv
Environment=Jwt__ExpiryHours=12

[Install]
WantedBy=multi-user.target
";
            var fixCs = "python3 - <<'PY'\n" +
                "import json,os\n" +
                "cs='" + NEWCS + "'\n" +
                "for p in ['/opt/gmtool/publish/appsettings.json','/opt/gmtool/WebApi/appsettings.json']:\n" +
                "    if os.path.exists(p):\n" +
                "        d=json.load(open(p))\n" +
                "        d.setdefault('ConnectionStrings',{})['Default']=cs\n" +
                "        json.dump(d,open(p,'w'),indent=2,ensure_ascii=False)\n" +
                "        print('updated',p)\n" +
                "    else:\n" +
                "        print('missing',p)\n" +
                "PY";
            var steps = new List<(string desc, string cmd)>
            {
                ("write service unit (no ConnStr override)", $"cat > /etc/systemd/system/gmtool.service <<'EOF'\n{unit}EOF\necho unit-written"),
                ("git pull", "cd /opt/gmtool && git stash push -u -m auto-stash-before-pull 2>/dev/null; git pull origin master 2>&1 | tail -8"),
                ("publish", "cd /opt/gmtool/WebApi && dotnet publish --configuration Release --output /opt/gmtool/publish 2>&1 | tail -4"),
                ("fix connection string (after publish)", fixCs),
                ("check unit for ConnStr override", "grep -n 'ConnectionStrings__Default' /etc/systemd/system/gmtool.service || echo NO-OVERRIDE-IN-UNIT"),
                ("daemon-reload + restart", "systemctl daemon-reload; systemctl restart gmtool; sleep 4; systemctl is-active gmtool"),
                ("effective connection string", "python3 -c \"import json;print(json.load(open('/opt/gmtool/publish/appsettings.json'))['ConnectionStrings']['Default'])\""),
                ("systemctl status", "systemctl status gmtool --no-pager | head -12"),
                ("DB connectivity test", "mysql -h141.140.14.61 -P3306 -usqsd -p'sarFGSEKJdJrnaFc' sqsd --protocol=tcp -e \"SELECT 1 AS ok, DATABASE() AS db;\" 2>&1 | grep -v 'password on the command line'"),
                ("port 5050", "curl -s -o /dev/null -w 'HTTP=%{http_code}\\n' http://localhost:5050/"),
                ("login admin (new pw)", "curl -s -w '\\nHTTP:%{http_code}' -X POST http://localhost:5050/api/auth/login -H 'Content-Type: application/json' -d '{\"username\":\"admin\",\"password\":\"@Aa31178375\"}'"),
            };
            foreach (var (desc, cmd) in steps)
            {
                Console.WriteLine($"\n===== [{desc}] =====");
                var r = client.RunCommand(cmd);
                Console.WriteLine(r.Result.Trim());
                if (!string.IsNullOrWhiteSpace(r.Error)) Console.WriteLine("[stderr] " + r.Error.Trim());
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "binlog")
        {
            var getCs = client.RunCommand(
                "cat /opt/gmtool/publish/appsettings.json 2>/dev/null | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d[\"ConnectionStrings\"][\"Default\"])' 2>/dev/null || " +
                "grep -Po 'Environment=ConnectionStrings__Default=\\K.*' /etc/systemd/system/gmtool.service"
            ).Result.Trim();
            string H="",U="",P="",D="",Port="3306";
            foreach (var part in getCs.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                var kv = part.Split('=', 2); if (kv.Length<2) continue;
                var k=kv[0].Trim().ToLowerInvariant(); var v=kv[1].Trim();
                if (k=="server"||k=="host") H=v;
                else if (k=="port") Port=v;
                else if (k=="user"||k=="uid"||k=="user id") U=v;
                else if (k=="password"||k=="pwd") P=v;
                else if (k=="database"||k=="db") D=v;
            }
            string Q(string sql) =>
                client.RunCommand($"mysql -h{H} -P{Port} -u{U} -p'{P}' {D} --protocol=tcp -e \"{sql.Replace("\"","\\\"")}\" 2>&1 | grep -v 'password on the command line'").Result.Trim();

            Console.WriteLine("== 1) log_bin 狀態 ==");
            Console.WriteLine(Q("SHOW VARIABLES LIKE 'log_bin';"));
            Console.WriteLine("\n== 2) binlog_format & expire_logs_days ==");
            Console.WriteLine(Q("SHOW VARIABLES WHERE Variable_name IN ('binlog_format','expire_logs_days','binlog_expire_logs_seconds','log_bin_basename');"));
            Console.WriteLine("\n== 3) 所有 binary log 檔案 (需要 REPLICATION CLIENT 權限) ==");
            Console.WriteLine(Q("SHOW BINARY LOGS;"));
            Console.WriteLine("\n== 4) 當前使用者權限 ==");
            Console.WriteLine(Q("SHOW GRANTS FOR CURRENT_USER;"));
            Console.WriteLine("\n== 5) 伺服器時區與現在時間 ==");
            Console.WriteLine(Q("SELECT @@global.time_zone, @@session.time_zone, NOW(), UTC_TIMESTAMP();"));
            Console.WriteLine("\n== 6) cat1987 主帳號下最近的角色更新時間 (看還活著的殘骸) ==");
            Console.WriteLine(Q("SELECT Id, Name, OnlineName, RegTime, LoginTime, updated_at FROM csalogin WHERE MasterId=1507 ORDER BY Id;"));
            Console.WriteLine("\n== 7) 附近 Id 範圍確認 (4115~4125) ==");
            Console.WriteLine(Q("SELECT Id, MasterId, Name, OnlineName FROM csalogin WHERE Id BETWEEN 4115 AND 4125 ORDER BY Id;"));
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "master")
        {
            var ids = args.Length > 2 ? args[2] : "1507,1987,1988";
            var list = ids.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var getCs = client.RunCommand(
                "cat /opt/gmtool/publish/appsettings.json 2>/dev/null | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d[\"ConnectionStrings\"][\"Default\"])' 2>/dev/null || " +
                "grep -Po 'Environment=ConnectionStrings__Default=\\K.*' /etc/systemd/system/gmtool.service"
            ).Result.Trim();
            string H="",U="",P="",D="",Port="3306";
            foreach (var part in getCs.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                var kv = part.Split('=', 2); if (kv.Length<2) continue;
                var k=kv[0].Trim().ToLowerInvariant(); var v=kv[1].Trim();
                if (k=="server"||k=="host") H=v;
                else if (k=="port") Port=v;
                else if (k=="user"||k=="uid"||k=="user id") U=v;
                else if (k=="password"||k=="pwd") P=v;
                else if (k=="database"||k=="db") D=v;
            }
            string Q(string sql) =>
                client.RunCommand($"mysql -h{H} -P{Port} -u{U} -p'{P}' {D} --protocol=tcp -e \"{sql.Replace("\"","\\\"")}\" 2>&1 | grep -v 'password on the command line'").Result.Trim();

            Console.WriteLine("== csaloginmaster 欄位 ==");
            Console.WriteLine(Q("DESC csaloginmaster;"));
            foreach (var id in list)
            {
                Console.WriteLine($"\n============ MasterId = {id} ============");
                Console.WriteLine("-- csaloginmaster (主帳號) --");
                Console.WriteLine(Q($"SELECT * FROM csaloginmaster WHERE Id={id} OR id={id};"));
                Console.WriteLine("-- csalogin (該主帳號下現存角色) --");
                Console.WriteLine(Q($"SELECT Id, Name, OnlineName, LoginTime FROM csalogin WHERE MasterId={id} ORDER BY Id;"));
                Console.WriteLine("-- csalogin_recycle (該主帳號下曾被 GM Tool 刪除的角色) --");
                Console.WriteLine(Q($"SELECT recycle_id, deleted_at, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name')) AS Name, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.OnlineName')) AS OnlineName FROM csalogin_recycle WHERE JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.MasterId'))='{id}' ORDER BY deleted_at DESC;"));
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "backups")
        {
            var cmds2 = new[]
            {
                "find / -name '*.sql' -size +1M 2>/dev/null | head -30",
                "find / -name '*.sql.gz' 2>/dev/null | head -30",
                "find / -name '*.dump' 2>/dev/null | head -20",
                "find /root /home /var /opt -name 'backup*' -type d 2>/dev/null | head -20",
                "ls -la /var/lib/mysql 2>/dev/null | head -5; ls -la /var/log/mysql 2>/dev/null | head -5",
                "systemctl list-units | grep -i backup",
                "crontab -l 2>/dev/null",
            };
            foreach (var c in cmds2)
            {
                Console.WriteLine("\n>>> " + c);
                var r = client.RunCommand(c);
                Console.WriteLine(r.Result.Trim());
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "hunt")
        {
            // 全資料庫所有文字欄位搜尋關鍵字
            var key = args.Length > 2 ? args[2] : "CAT1987";
            var getCs = client.RunCommand(
                "cat /opt/gmtool/publish/appsettings.json 2>/dev/null | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d[\"ConnectionStrings\"][\"Default\"])' 2>/dev/null || " +
                "grep -Po 'Environment=ConnectionStrings__Default=\\K.*' /etc/systemd/system/gmtool.service"
            ).Result.Trim();
            string H="",U="",P="",D="",Port="3306";
            foreach (var part in getCs.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                var kv = part.Split('=', 2); if (kv.Length<2) continue;
                var k=kv[0].Trim().ToLowerInvariant(); var v=kv[1].Trim();
                if (k=="server"||k=="host") H=v;
                else if (k=="port") Port=v;
                else if (k=="user"||k=="uid"||k=="user id") U=v;
                else if (k=="password"||k=="pwd") P=v;
                else if (k=="database"||k=="db") D=v;
            }
            string Q(string sql) =>
                client.RunCommand($"mysql -h{H} -P{Port} -u{U} -p'{P}' {D} --protocol=tcp -N -e \"{sql.Replace("\"","\\\"")}\" 2>&1 | grep -v 'password on the command line'").Result.Trim();

            Console.WriteLine("== 所有 table ==");
            Console.WriteLine(Q("SHOW TABLES;"));

            Console.WriteLine("\n== 取得所有文字欄位（全庫） ==");
            var all = Q("SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND DATA_TYPE IN ('varchar','char','text','longtext','mediumtext','tinytext');");
            var entries = new List<(string tbl, string col)>();
            foreach (var line in all.Split('\n'))
            {
                var t = line.Split('\t');
                if (t.Length >= 2 && !string.IsNullOrWhiteSpace(t[0])) entries.Add((t[0].Trim(), t[1].Trim()));
            }
            Console.WriteLine($"共 {entries.Count} 個文字欄位要掃");

            // 每一欄位 UNION 組在一起（可能 SQL 太長，分批）
            Console.WriteLine($"\n== 全庫搜尋含 '{key}' 的紀錄 ==");
            int batch = 40, total = 0;
            for (int i=0; i < entries.Count; i += batch)
            {
                var segs = entries.Skip(i).Take(batch)
                    .Select(e => $"SELECT '{e.tbl}' AS tbl, '{e.col}' AS col, COUNT(*) AS cnt FROM `{e.tbl}` WHERE `{e.col}` LIKE '%{key}%'");
                var sql = string.Join(" UNION ALL ", segs);
                var rs = Q(sql + ";");
                foreach (var ln in rs.Split('\n'))
                {
                    var tt = ln.Split('\t');
                    if (tt.Length >= 3 && int.TryParse(tt[2], out var n) && n > 0)
                    {
                        Console.WriteLine($"HIT ► {tt[0]}.{tt[1]} = {n} 筆");
                        total += n;
                    }
                }
            }
            if (total == 0) Console.WriteLine($"全庫掃過，完全沒有含 '{key}' 的紀錄");
            else Console.WriteLine($"\n共 {total} 筆符合。");
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "schema")
        {
            var getCs = client.RunCommand(
                "cat /opt/gmtool/publish/appsettings.json 2>/dev/null | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d[\"ConnectionStrings\"][\"Default\"])' 2>/dev/null || " +
                "grep -Po 'Environment=ConnectionStrings__Default=\\K.*' /etc/systemd/system/gmtool.service"
            ).Result.Trim();
            string H="",U="",P="",D="",Port="3306";
            foreach (var part in getCs.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
                var kv = part.Split('=', 2); if (kv.Length<2) continue;
                var k=kv[0].Trim().ToLowerInvariant(); var v=kv[1].Trim();
                if (k=="server"||k=="host") H=v;
                else if (k=="port") Port=v;
                else if (k=="user"||k=="uid"||k=="user id") U=v;
                else if (k=="password"||k=="pwd") P=v;
                else if (k=="database"||k=="db") D=v;
            }
            string Q(string sql) {
                return client.RunCommand($"mysql -h{H} -P{Port} -u{U} -p'{P}' {D} --protocol=tcp -e \"{sql.Replace("\"","\\\"")}\" 2>&1 | grep -v 'password on the command line'").Result.Trim();
            }
            var key = args.Length > 2 ? args[2] : "CAT1987";
            Console.WriteLine("== csalogin 欄位 ==");
            Console.WriteLine(Q("DESC csalogin;"));
            Console.WriteLine("\n== csalogin 各欄位中凡含 'CAT' 前綴者（抽樣前 5 個文字欄位） ==");
            // 找出所有可能的字串欄位，然後各查一遍
            var colsRaw = Q("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='csalogin' AND DATA_TYPE IN ('varchar','char','text');");
            Console.WriteLine("文字欄位：");
            Console.WriteLine(colsRaw);
            // 取欄位名
            var textCols = new List<string>();
            foreach (var line in colsRaw.Split('\n')) {
                var tabs = line.Split('\t');
                if (tabs.Length >= 2 && tabs[0] != "COLUMN_NAME") textCols.Add(tabs[0]);
            }
            // 用單一 UNION 查詢避免多次 shell 逃逸問題
            var unionSql = string.Join(" UNION ALL ", textCols.Select(c =>
                $"SELECT '{c}' AS col, COUNT(*) AS cnt FROM csalogin WHERE {c} LIKE '%{key}%'"));
            Console.WriteLine("-- 各文字欄位中含 '" + key + "' 的筆數 --");
            Console.WriteLine(Q(unionSql + ";"));

            Console.WriteLine("\n-- 直接列出 Name LIKE 'CAT%' 的 20 筆 --");
            Console.WriteLine(Q($"SELECT Id, Name, OnlineName, MasterId, LoginTime FROM csalogin WHERE Name LIKE '{key}%' LIMIT 20;"));
            Console.WriteLine("\n-- 直接列出 OnlineName LIKE 'CAT%' 的 20 筆 --");
            Console.WriteLine(Q($"SELECT Id, Name, OnlineName, MasterId, LoginTime FROM csalogin WHERE OnlineName LIKE '{key}%' LIMIT 20;"));
            Console.WriteLine("\n== 回收桶中 JSON 含 'CAT' 的記錄 ==");
            Console.WriteLine(Q($"SELECT recycle_id, deleted_at, LEFT(original_data, 200) AS preview FROM csalogin_recycle WHERE original_data LIKE '%{key}%' ORDER BY deleted_at DESC LIMIT 20;"));
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "finddb")
        {
            var target = args.Length > 2 ? args[2] : "CAT1987 CAT1988";
            var keys = target.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            // 確保 mysql-client 裝了
            var hasMy = client.RunCommand("which mysql || echo NO").Result.Trim();
            if (hasMy.EndsWith("NO"))
            {
                Console.WriteLine("[install mysql-client] ...");
                var ins = client.RunCommand("DEBIAN_FRONTEND=noninteractive apt-get install -y mysql-client 2>&1 | tail -3");
                Console.WriteLine(ins.Result.Trim());
            }

            // 從 appsettings.json (非 example) 或 systemd 讀連線字串
            var getCs = client.RunCommand(
                "cat /opt/gmtool/publish/appsettings.json 2>/dev/null | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d[\"ConnectionStrings\"][\"Default\"])' 2>/dev/null || " +
                "grep -Po 'Environment=ConnectionStrings__Default=\\K.*' /etc/systemd/system/gmtool.service"
            ).Result.Trim();
            Console.WriteLine("[connstr] " + (getCs.Length > 0 ? getCs.Replace("Password=", "Password=***").Substring(0, Math.Min(getCs.Length, 120)) : "EMPTY"));

            string dbHost2 = "", dbUser2 = "", dbPass2 = "", dbName2 = "", dbPort2 = "3306";
            foreach (var p in getCs.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = p.Split('=', 2); if (kv.Length < 2) continue;
                var k = kv[0].Trim().ToLowerInvariant(); var v = kv[1].Trim();
                if (k == "server" || k == "host") dbHost2 = v;
                else if (k == "port") dbPort2 = v;
                else if (k == "user" || k == "uid" || k == "user id") dbUser2 = v;
                else if (k == "password" || k == "pwd") dbPass2 = v;
                else if (k == "database" || k == "db") dbName2 = v;
            }
            if (dbHost2.Length == 0) { Console.WriteLine("找不到連線字串，請提供。"); client.Disconnect(); return; }

            string MyQ(string sql) {
                var cmd = $"mysql -h{dbHost2} -P{dbPort2} -u{dbUser2} -p'{dbPass2}' {dbName2} --protocol=tcp -e \"{sql.Replace("\"", "\\\"")}\" 2>&1";
                return client.RunCommand(cmd).Result.Trim();
            }

            Console.WriteLine("\n== 回收桶表是否存在？ ==");
            Console.WriteLine(MyQ("SHOW TABLES LIKE 'csalogin_recycle';"));
            Console.WriteLine("\n== 回收桶目前筆數 ==");
            Console.WriteLine(MyQ("SELECT COUNT(*) AS total FROM csalogin_recycle;"));
            Console.WriteLine("\n== 回收桶最近 10 筆 ==");
            Console.WriteLine(MyQ("SELECT recycle_id, deleted_at, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name')) AS Name, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.OnlineName')) AS OnlineName, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.MasterId')) AS MasterId FROM csalogin_recycle ORDER BY deleted_at DESC LIMIT 10;"));

            foreach (var key in keys)
            {
                var k2 = key.Replace("'", "''");
                Console.WriteLine("\n============= 搜尋 " + key + " =============");
                Console.WriteLine("-- 回收桶 (MasterId / Name 完全符合或前綴) --");
                Console.WriteLine(MyQ(
                    "SELECT recycle_id, deleted_at, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name')) AS Name, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.OnlineName')) AS OnlineName, JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.MasterId')) AS MasterId " +
                    "FROM csalogin_recycle WHERE " +
                    $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.MasterId'))='{k2}' OR " +
                    $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name'))='{k2}' OR " +
                    $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name')) LIKE '{k2}%' " +
                    "ORDER BY deleted_at DESC LIMIT 20;"));
                Console.WriteLine("-- csalogin (目前仍存在的角色) --");
                Console.WriteLine(MyQ(
                    $"SELECT id, Name, OnlineName, MasterId, LoginTime FROM csalogin WHERE MasterId='{k2}' OR Name='{k2}' OR Name LIKE '{k2}%' ORDER BY LoginTime DESC LIMIT 20;"));
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "findapi")
        {
            var target = args.Length > 2 ? args[2] : "CAT1987 CAT1988";
            var keys = target.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            // 用本機 curl 透過 WebApi 登入並查回收桶
            var loginCmd = "curl -s -X POST http://localhost:5050/api/auth/login -H 'Content-Type: application/json' -d '{\"username\":\"admin\",\"password\":\"@Aa31178375\"}'";
            var loginRes = client.RunCommand(loginCmd).Result.Trim();
            Console.WriteLine("[login] " + loginRes);
            var tokenIdx = loginRes.IndexOf("\"token\":\"");
            if (tokenIdx < 0) { Console.WriteLine("登入失敗"); client.Disconnect(); return; }
            tokenIdx += "\"token\":\"".Length;
            var tokenEnd = loginRes.IndexOf('"', tokenIdx);
            var token = loginRes.Substring(tokenIdx, tokenEnd - tokenIdx);

            Console.WriteLine("\n=== 抓取回收桶 ===");
            var rb = client.RunCommand($"curl -s -H 'Authorization: Bearer {token}' http://localhost:5050/api/recycle").Result;
            foreach (var key in keys)
            {
                Console.WriteLine("\n--- 搜尋 \"" + key + "\" 的結果 ---");
                int found = 0;
                var lines = rb.Split(new[] { '}', '{' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine(line.Trim());
                        found++;
                    }
                }
                if (found == 0) Console.WriteLine("(回收桶裡沒找到含 \"" + key + "\" 的記錄)");
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "find")
        {
            // 讀取 appsettings 取連線字串，然後搜尋回收桶與 csalogin
            var target = args.Length > 2 ? args[2] : "CAT1987 CAT1988";
            var keys = target.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            // 取連線字串：嘗試讀 systemd env + appsettings.json
            var cs = client.RunCommand(
                "grep -Po 'ConnectionStrings__Default=\\K.*' /etc/systemd/system/gmtool.service 2>/dev/null; " +
                "[ -z \"$cs\" ] && grep -Po '\"Default\"\\s*:\\s*\"\\K[^\"]+' /opt/gmtool/publish/appsettings*.json 2>/dev/null | head -1"
            ).Result.Trim();
            Console.WriteLine("[conn] " + (string.IsNullOrEmpty(cs) ? "(not found inline; will try mysql default)" : cs.Substring(0, Math.Min(cs.Length, 80)) + "..."));

            // 從連線字串萃取 DB 連線資訊
            var parts = cs.Split(';', StringSplitOptions.RemoveEmptyEntries);
            string dbHost = "", dbUser = "", dbPass = "", dbName = "";
            foreach (var p in parts)
            {
                var kv = p.Split('=', 2);
                if (kv.Length < 2) continue;
                var k = kv[0].Trim().ToLowerInvariant();
                var v = kv[1].Trim();
                if (k == "server" || k == "host") dbHost = v;
                else if (k == "user" || k == "uid" || k == "user id") dbUser = v;
                else if (k == "password" || k == "pwd") dbPass = v;
                else if (k == "database" || k == "db") dbName = v;
            }

            foreach (var key in keys)
            {
                Console.WriteLine("\n=========== 搜尋 " + key + " ===========");
                var safeKey = key.Replace("'", "''");

                // 回收桶
                Console.WriteLine("-- csalogin_recycle (回收桶) --");
                var q1 = $"SELECT recycle_id, deleted_at, deleted_by, " +
                         $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name')) AS Name, " +
                         $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.OnlineName')) AS OnlineName, " +
                         $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.MasterId')) AS MasterId " +
                         $"FROM csalogin_recycle WHERE " +
                         $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.MasterId')) = '{safeKey}' OR " +
                         $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name'))     = '{safeKey}' OR " +
                         $"JSON_UNQUOTE(JSON_EXTRACT(original_data,'$.Name')) LIKE '{safeKey}%' " +
                         $"ORDER BY deleted_at DESC LIMIT 20;";
                var r1 = client.RunCommand($"mysql -h{dbHost} -u{dbUser} -p'{dbPass}' {dbName} -e \"{q1}\" 2>&1");
                Console.WriteLine(r1.Result.Trim());

                // 現有 csalogin (以防只是找不到，實際還在)
                Console.WriteLine("-- csalogin (目前仍存在的角色) --");
                var q2 = $"SELECT id, Name, OnlineName, MasterId, LoginTime FROM csalogin WHERE " +
                         $"MasterId = '{safeKey}' OR Name = '{safeKey}' OR Name LIKE '{safeKey}%' " +
                         $"ORDER BY LoginTime DESC LIMIT 20;";
                var r2 = client.RunCommand($"mysql -h{dbHost} -u{dbUser} -p'{dbPass}' {dbName} -e \"{q2}\" 2>&1");
                Console.WriteLine(r2.Result.Trim());
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "diag")
        {
            var dcmds = new[]
            {
                "dotnet --list-runtimes",
                "dotnet --list-sdks",
                "systemctl cat gmtool 2>/dev/null | head -60",
                "journalctl -u gmtool -n 80 --no-pager",
            };
            foreach (var c in dcmds)
            {
                Console.WriteLine($"\n===== {c} =====");
                var rr = client.RunCommand(c);
                Console.WriteLine(rr.Result.Trim());
                if (!string.IsNullOrWhiteSpace(rr.Error)) Console.WriteLine("[stderr] " + rr.Error.Trim());
            }
            client.Disconnect();
            return;
        }
        if (args.Length > 1 && args[1] == "check")
        {
            var checks = new[]
            {
                "ls -la /opt/gmtool/publish/wwwroot/assets/",
                "head -5 /opt/gmtool/publish/wwwroot/index.html",
                "systemctl is-active gmtool",
                "curl -s -o /dev/null -w '%{http_code}' http://localhost:5050/",
            };
            foreach (var c in checks)
            {
                Console.WriteLine($">>> {c}");
                var cr = client.RunCommand(c);
                Console.WriteLine(cr.Result.Trim());
                Console.WriteLine();
            }
            client.Disconnect();
            return;
        }

        if (args.Length > 1 && args[1] == "clean")
        {
            Console.WriteLine("=== 清理遠端舊版 assets ===");
            var keepFiles = new[] { "index-Co9J_6A9.js", "index-0UkjZ6QL.css" };
            var listResult = client.RunCommand("ls /opt/gmtool/publish/wwwroot/assets/");
            var files = listResult.Result.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int removed = 0;
            foreach (var f in files)
            {
                var fname = f.Trim();
                if (string.IsNullOrEmpty(fname)) continue;
                if (Array.IndexOf(keepFiles, fname) < 0)
                {
                    client.RunCommand($"rm -f /opt/gmtool/publish/wwwroot/assets/{fname}");
                    removed++;
                }
            }
            Console.WriteLine($"已刪除 {removed} 個舊檔案，保留 {keepFiles.Length} 個最新檔案");
            Console.WriteLine("重啟服務...");
            var restart = client.RunCommand("systemctl restart gmtool; sleep 2; systemctl is-active gmtool");
            Console.WriteLine(restart.Result.Trim());
            var verify = client.RunCommand("ls /opt/gmtool/publish/wwwroot/assets/");
            Console.WriteLine("剩餘檔案：");
            Console.WriteLine(verify.Result.Trim());
            client.Disconnect();
            return;
        }

        var serviceUnit = @"[Unit]
Description=SodaGM Web Tool
After=network.target

[Service]
WorkingDirectory=/opt/gmtool/publish
ExecStart=/usr/bin/dotnet --roll-forward LatestMajor /opt/gmtool/publish/WebApi.dll
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5050
Environment=GmAccounts__0__Username=admin
Environment=GmAccounts__0__Password=@Aa31178375
Environment=GmAccounts__0__Role=superadmin
Environment=Jwt__Secret=kQ7nRwXz3YpM8fH2sL4vBt6cE1gJuA9dNhT5bVaKoXeFmP0rCyZqDsIlUwGhJxNv
Environment=Jwt__ExpiryHours=12

[Install]
WantedBy=multi-user.target
";
            var writeUnitCmd = $"cat > /etc/systemd/system/gmtool.service <<'EOF'\n{serviceUnit}EOF\nsystemctl daemon-reload; echo unit-updated";

        var cmds = new List<(string desc, string cmd)>
        {
            ("write service unit (prod+GmAccounts+Jwt)", writeUnitCmd),
            ("git pull", "cd /opt/gmtool && git stash push -u -m auto-stash-before-pull 2>/dev/null; git pull origin master 2>&1 | tail -5"),
            ("publish", "cd /opt/gmtool/WebApi && dotnet publish --configuration Release --output /opt/gmtool/publish 2>&1 | tail -3"),
            ("restart", "systemctl restart gmtool; sleep 4; systemctl is-active gmtool"),
            ("port 5050", "curl -s -o /dev/null -w 'HTTP=%{http_code}' http://localhost:5050/"),
            ("login admin (new pw)", "curl -s -w '\\nHTTP:%{http_code}' -X POST http://localhost:5050/api/auth/login -H 'Content-Type: application/json' -d '{\"username\":\"admin\",\"password\":\"@Aa31178375\"}'"),
        };

        foreach (var (desc, cmd) in cmds)
        {
            Console.Write($"[{desc}] ");
            var r = client.RunCommand(cmd);
            Console.WriteLine(r.Result.Trim());
        }
        client.Disconnect();
        Console.WriteLine("\nDone! Press Ctrl+Shift+R to reload the web page.");
    }

    static void RunDbCheck()
    {
        var targets = new (string host, string label)[]
        {
            ("172.234.95.180", "web VPS"),
            ("141.140.14.61", "new DB IP"),
            ("162.245.220.106", "old DB IP"),
        };
        var user = "root";
        var pass = "~QbW(8c8tXAKM*f3v";

        foreach (var (host, label) in targets)
        {
            Console.WriteLine($"\n======== {label} ({host}) ========");
            try
            {
                using var c = new SshClient(host, user, pass);
                c.ConnectionInfo.Timeout = TimeSpan.FromSeconds(12);
                c.Connect();
                Console.WriteLine("SSH: OK");
                foreach (var cmd in new[]
                {
                    "hostname -f 2>/dev/null || hostname",
                    "ss -tlnp 2>/dev/null | grep -E ':3306|:22 ' || netstat -tlnp 2>/dev/null | grep 3306",
                    "command -v mysql >/dev/null && mysql --version | head -1 || echo mysql-cli-missing",
                })
                {
                    var r = c.RunCommand(cmd);
                    if (!string.IsNullOrWhiteSpace(r.Result)) Console.WriteLine(r.Result.TrimEnd());
                }
                if (host == "172.234.95.180")
                {
                    foreach (var db in new[] { "141.140.14.61", "162.245.220.106", "127.0.0.1" })
                    {
                        var t = c.RunCommand(
                            $"timeout 3 bash -c 'echo >/dev/tcp/{db}/3306' 2>/dev/null && echo tcp-{db}-3306-OPEN || echo tcp-{db}-3306-CLOSED");
                        Console.WriteLine(t.Result.TrimEnd());
                    }
                    var cs = c.RunCommand(
                        "python3 -c \"import json; d=json.load(open('/opt/gmtool/publish/appsettings.json')); print(d['ConnectionStrings']['Default'])\" 2>/dev/null"
                    ).Result.Trim();
                    if (!string.IsNullOrEmpty(cs))
                        Console.WriteLine("gmtool appsettings: " + cs);
                }
                c.Disconnect();
            }
            catch (Exception ex) { Console.WriteLine("SSH: FAIL - " + ex.Message); }
        }

        Console.WriteLine("\n======== MySQL from web VPS -> 141.140.14.61 ========");
        try
        {
            using var c = new SshClient("172.234.95.180", user, pass);
            c.Connect();
            var test = c.RunCommand(
                "mysql -h141.140.14.61 -P3306 -usqsd -p'sarFGSEKJdJrnaFc' sqsd --protocol=tcp -e \"SELECT 1 ok, DATABASE() db, @@hostname host\" 2>&1 | grep -v 'password on the command line'"
            );
            Console.WriteLine(string.IsNullOrWhiteSpace(test.Result) ? test.Error.Trim() : test.Result.Trim());
            test = c.RunCommand(
                "mysql -h162.245.220.106 -usqsd -p'sarFGSEKJdJrnaFc' sqsd --protocol=tcp -e \"SELECT 1 ok\" 2>&1 | grep -v 'password on the command line' | head -3"
            );
            Console.WriteLine("old IP test:\n" + (test.Result + test.Error).Trim());
            c.Disconnect();
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
    }
}
