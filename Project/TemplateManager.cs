using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SQ_Email_Tools
{
    public class TemplateManager
    {
        private static TemplateManager _instance;
        public static TemplateManager Instance => _instance ??= new TemplateManager();

        private readonly string _filePath;
        private List<MailTemplate> _templates = new List<MailTemplate>();

        public IReadOnlyList<MailTemplate> Templates => _templates.AsReadOnly();

        public TemplateManager()
        {
            _filePath = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath)
                ?? AppDomain.CurrentDomain.BaseDirectory,
                "templates.json");
            Load();
        }

        public void Save(List<MailTemplate> templates)
        {
            _templates = templates;
            File.WriteAllText(_filePath,
                JsonSerializer.Serialize(_templates, new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Add(MailTemplate t)
        {
            _templates.Add(t);
            Persist();
        }

        public void Remove(MailTemplate t)
        {
            _templates.Remove(t);
            Persist();
        }

        /// <summary>取代既有範本（編輯後呼叫，保留 Cart 可傳入更新後的 t）</summary>
        public void Replace(MailTemplate old, MailTemplate updated)
        {
            int i = _templates.IndexOf(old);
            if (i < 0) return;
            _templates[i] = updated;
            Persist();
        }

        private void Persist()
        {
            File.WriteAllText(_filePath,
                JsonSerializer.Serialize(_templates, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                    _templates = JsonSerializer.Deserialize<List<MailTemplate>>(File.ReadAllText(_filePath))
                                 ?? new List<MailTemplate>();
            }
            catch { _templates = new List<MailTemplate>(); }
        }
    }
}
