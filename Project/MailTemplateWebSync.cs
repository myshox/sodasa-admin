using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SQ_Email_Tools
{
    /// <summary>
    /// 與 GM 網頁版共用伺服器範本（GET/PUT /api/mail-templates），需先登入取得 JWT。
    /// </summary>
    public static class MailTemplateWebSync
    {
        private static readonly JsonSerializerOptions JsonRead = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions JsonWrite = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static async Task<string> LoginAsync(string baseUrl, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var url = Combine(baseUrl, "/api/auth/login");
            var body = JsonSerializer.Serialize(new { username, password }, JsonWrite);
            using var resp = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
                return null;
            var json = await resp.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<LoginDto>(json, JsonRead);
            return dto?.Token;
        }

        public static async Task<List<MailTemplate>> DownloadTemplatesAsync(string baseUrl, string token)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var url = Combine(baseUrl, "/api/mail-templates");
            using var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var wires = JsonSerializer.Deserialize<List<MailTemplateWireDto>>(json, JsonRead) ?? new List<MailTemplateWireDto>();
            return wires.Select(ToNative).ToList();
        }

        /// <summary>將本機範本上傳至伺服器（覆寫伺服器整份清單）。成功後寫回 WebId 至 templates.json。</summary>
        public static async Task UploadTemplatesAsync(string baseUrl, string token, IReadOnlyList<MailTemplate> templates)
        {
            var list = templates.ToList();
            var wires = list.Select(ToWire).ToList();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var url = Combine(baseUrl, "/api/mail-templates");
            var body = JsonSerializer.Serialize(wires, JsonWrite);
            using var req = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            using var resp = await client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            for (int i = 0; i < list.Count; i++)
                list[i].WebId = wires[i].Id ?? list[i].WebId;
            TemplateManager.Instance.Save(list);
        }

        public static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "https://gm.sodasa.org";
            return baseUrl.Trim().TrimEnd('/');
        }

        static string Combine(string baseUrl, string path)
        {
            var b = NormalizeBaseUrl(baseUrl);
            if (!path.StartsWith("/")) path = "/" + path;
            return b + path;
        }

        static MailTemplate ToNative(MailTemplateWireDto w)
        {
            return new MailTemplate
            {
                WebId = w.Id ?? "",
                Name = w.Name ?? "",
                Buff1 = w.Title ?? "",
                Buff2 = w.Content ?? "",
                Type = 0,
                Data = 0,
                Buff3 = null,
                CreatedAt = DateTime.Now,
                Cart = (w.Cart ?? new List<MailTemplateCartWireDto>()).Select(c => new MailTemplateCartItem
                {
                    ItemId = c.ItemId,
                    Qty = c.Qty,
                    Type = c.Type,
                    Name = c.Name ?? "",
                    Buff3 = c.Buff3 ?? "",
                }).ToList(),
            };
        }

        static MailTemplateWireDto ToWire(MailTemplate t)
        {
            var id = string.IsNullOrWhiteSpace(t.WebId)
                ? "t" + Guid.NewGuid().ToString("N")
                : t.WebId;
            return new MailTemplateWireDto
            {
                Id = id,
                Name = t.Name ?? "",
                Title = t.Buff1 ?? "",
                Content = t.Buff2 ?? "",
                Cart = (t.Cart ?? new List<MailTemplateCartItem>()).Select(c => new MailTemplateCartWireDto
                {
                    ItemId = c.ItemId,
                    Qty = c.Qty,
                    Type = c.Type,
                    Name = c.Name ?? "",
                    Buff3 = c.Buff3 ?? "",
                }).ToList(),
            };
        }

        private class LoginDto
        {
            public string Token { get; set; }
        }

        private class MailTemplateWireDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
            public List<MailTemplateCartWireDto> Cart { get; set; }
        }

        private class MailTemplateCartWireDto
        {
            public int ItemId { get; set; }
            public int Qty { get; set; }
            public int Type { get; set; }
            public string Name { get; set; }
            public string Buff3 { get; set; }
        }
    }
}
