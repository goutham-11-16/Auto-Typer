using System;
using System.Collections.Generic;
using System.Linq; // Added for LINQ extension methods
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoTyper.Models;

namespace AutoTyper.Services
{
    public class AccessControlService
    {
        private readonly HttpClient _httpClient;
        
        // TODO: Replace with actual GitHub configuration
        private const string RepoOwner = "goutham-11-16";
        private const string RepoName = "Auto-Typer"; // Assuming this repo name based on previous context, adjust if needed
        private const string Branch = "main";
        
        // RAW URLs for reading
        private string UsersUrl => $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{Branch}/users.json";
        private string RequestsUrl => $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{Branch}/requests.json";
        private string UpdateUrl => $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{Branch}/update.json";

        // API URL for writing (Requests)
        // https://api.github.com/repos/{owner}/{repo}/contents/{path}
        private string RequestsApiUrl => $"https://api.github.com/repos/{RepoOwner}/{RepoName}/contents/requests.json";

        public AccessControlService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AutoTyper-AccessControl");
            // If using a Personal Access Token for public repo reads (high rate limit) or writes:
            // _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "YOUR_TOKEN");
        }

        public async Task<RemoteConfig> GetUsersConfigAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(UsersUrl);
                return JsonSerializer.Deserialize<RemoteConfig>(json) ?? new RemoteConfig();
            }
            catch (Exception)
            {
                // Fallback or rethrow depending on strictness. 
                // For security/control, failure to fetch = Access Denied usually.
                return new RemoteConfig(); 
            }
        }

        public async Task<RequestLog> GetRequestsConfigAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(RequestsUrl);
                return JsonSerializer.Deserialize<RequestLog>(json) ?? new RequestLog();
            }
            catch
            {
                return new RequestLog();
            }
        }

        public async Task<AppVersionInfo> GetUpdateConfigAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(UpdateUrl);
                // Handle case-insensitive property matching for the existing update.json structure
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<AppVersionInfo>(json, options);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SubmitAccessRequestAsync(string deviceId, string username)
        {
            // This requires a Write Token (PAT) which we shouldn't hardcode in a public app usually.
            // For this specific "functionality over security" request, we will outline the logic 
            // but might need the user to provide a mechanism or just log locally if they don't give a token.
            
            // LOGIC:
            // 1. GET requests.json (API) to get the 'sha' (required for update).
            // 2. Modify content.
            // 3. PUT requests.json (API).

            // For this specific demo/implementation where we don't have a secure backend or token:
            // We sleep to simulate network, then return TRUE to show the "Pending" UI state.
            await Task.Delay(1500); 
            return true; 
        }

        private async Task<bool> AppendRequestToGitHub(string deviceId, string username)
        {
            // Implementation sketch for writing to GitHub API
            // Requires Authentication Header with 'repo' scope token.
            return await Task.FromResult(true);
        }
    }
}
