using System;
using System.Collections.Generic;
using Renci.SshNet;

class Program
{
    static void Main()
    {
        var host = "172.234.95.180";
        var user = "root";
        var pass = "~QbW(8c8tXAKM*f3v";

        using var client = new SshClient(host, user, pass);
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
        client.Connect();
        Console.WriteLine("=== 連線成功，開始更新... ===\n");

        var args = Environment.GetCommandLineArgs();
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

        var cmds = new List<(string desc, string cmd)>
        {
            ("git pull", "cd /opt/gmtool; git pull origin master 2>&1 | tail -5"),
            ("dotnet publish", "cd /opt/gmtool/WebApi; dotnet publish --configuration Release --output /opt/gmtool/publish 2>&1 | tail -3"),
            ("restart", "systemctl restart gmtool; sleep 2; systemctl is-active gmtool"),
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
}
