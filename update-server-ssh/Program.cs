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
