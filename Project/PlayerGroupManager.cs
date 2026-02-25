using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SQ_Email_Tools
{
    /// <summary>玩家批量操作群組 — 儲存命名帳號清單，供批量金幣/郵件重複使用</summary>
    public class PlayerGroup
    {
        public string        Name      { get; set; } = "";
        public string        Note      { get; set; } = "";
        public List<string>  Accounts  { get; set; } = new();
        public List<string>  CharNames { get; set; } = new();  // 對應角色名（僅顯示用）
        public DateTime      CreatedAt { get; set; } = DateTime.Now;
        public DateTime      UpdatedAt { get; set; } = DateTime.Now;
    }

    public class PlayerGroupManager
    {
        private static PlayerGroupManager _instance;
        public  static PlayerGroupManager Instance => _instance ??= new PlayerGroupManager();

        private readonly string _path = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath)
            ?? AppDomain.CurrentDomain.BaseDirectory,
            "player_groups.json");

        private List<PlayerGroup> _groups = new();

        public IReadOnlyList<PlayerGroup> Groups => _groups;

        private PlayerGroupManager() => Load();

        public void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                _groups = JsonSerializer.Deserialize<List<PlayerGroup>>(json) ?? new();
            }
            catch { _groups = new(); }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_groups,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch { }
        }

        /// <summary>新增或更新群組（同名則覆蓋）</summary>
        public void AddOrUpdate(PlayerGroup group)
        {
            group.UpdatedAt = DateTime.Now;
            int idx = _groups.FindIndex(g => g.Name == group.Name);
            if (idx >= 0) _groups[idx] = group;
            else          _groups.Add(group);
            Save();
        }

        public void Remove(string name)
        {
            _groups.RemoveAll(g => g.Name == name);
            Save();
        }

        public PlayerGroup Get(string name) => _groups.FirstOrDefault(g => g.Name == name);
    }
}
