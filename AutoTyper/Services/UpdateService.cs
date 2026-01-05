using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoTyper.Services
{
    public class UpdateInfo
    {
        public string LatestVersion { get; set; }
        public string ReleasePage { get; set; }
        public List<string> Changelog { get; set; }
        public bool Mandatory { get; set; }
    }

    public class UpdateService
    {
        private const string ManifestUrl = "https://raw.githubusercontent.com/goutham-11-16/Auto-Typer/main/update.json";
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AutoTyper-UpdateCheck");
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(ManifestUrl);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var info = JsonSerializer.Deserialize<UpdateInfo>(json, options);
                
                if (info != null && IsNewer(info.LatestVersion))
                {
                    return info;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
                throw; // Rethrow to let ViewModel handle the error (e.g. show "Check failed")
            }
        }

        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        private bool IsNewer(string latestVersionStr)
        {
            if (string.IsNullOrWhiteSpace(latestVersionStr)) return false;

            if (Version.TryParse(latestVersionStr, out var latestVersion))
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                if (currentVersion == null) return false;

                // Compare Major, Minor, Build. Ignore Revision for now.
                // Creating new Version objects with 3 components to compare safely
                var v1 = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);
                var v2 = new Version(latestVersion.Major, latestVersion.Minor, latestVersion.Build);

                return v2 > v1;
            }
            return false;
        }
    }
}
