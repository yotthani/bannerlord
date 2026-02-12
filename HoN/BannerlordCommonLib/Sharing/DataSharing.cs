using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BannerlordCommonLib.Sharing
{
    public static class DataSharing
    {
        private static readonly HttpClient _http = new HttpClient();
        
        static DataSharing()
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "BannerlordCommonLib/1.0");
        }
        
        public static async Task<string> UploadToGistAsync(string json, string filename, string description = null)
        {
            try
            {
                var gist = new {
                    description = description ?? $"BCL Data - {DateTime.UtcNow:yyyy-MM-dd}",
                    @public = false,
                    files = new Dictionary<string, object> { { filename, new { content = json } } }
                };
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/gists") {
                    Content = new StringContent(JsonSerializer.Serialize(gist), Encoding.UTF8, "application/json")
                };
                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return doc.RootElement.GetProperty("html_url").GetString();
            }
            catch { return null; }
        }

        public static async Task<bool> SendToDiscordAsync(string json, string webhookUrl, string message)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "file", $"data_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                content.Add(new StringContent(message), "content");
                return (await _http.PostAsync(webhookUrl, content)).IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<string> UploadToPasteAsync(string text)
        {
            try
            {
                var content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("f:1", text) });
                var response = await _http.PostAsync("http://ix.io", content);
                return response.IsSuccessStatusCode ? (await response.Content.ReadAsStringAsync()).Trim() : null;
            }
            catch { return null; }
        }
    }
}
