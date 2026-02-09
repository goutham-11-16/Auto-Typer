using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoTyper.Models
{
    public class GlobalState
    {
        [System.Text.Json.Serialization.JsonPropertyName("app_enabled")]
        public bool AppEnabled { get; set; } = true;

        [System.Text.Json.Serialization.JsonPropertyName("kill_message")]
        public string KillMessage { get; set; } = "Application disabled.";
    }

    public class RemoteConfig
    {
        [JsonPropertyName("users")]
        public List<RemoteUser> Users { get; set; } = new List<RemoteUser>();
    }

    public class RemoteUser
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }

    public class RequestLog
    {
        [JsonPropertyName("requests")]
        public List<AccessRequest> Requests { get; set; } = new List<AccessRequest>();
    }

    public class AccessRequest
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; }

        [JsonPropertyName("requested_on")]
        public DateTime RequestedOn { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } // "pending", "approved", "rejected"

        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

    // Reuse existing UpdateInfo but ensure it matches the new structure if needed, or create a new one.
    // The existing UpdateInfo in UpdateService.cs might be specific to the old format. 
    // Let's create a dedicated model here to match the user's requested structure exactly.
    public class AppVersionInfo
    {
        [JsonPropertyName("latestVersion")]
        public string LatestVersion { get; set; }

        [JsonPropertyName("releasePage")] // Using releasePage to match existing update.json if that's preferred, or download_url
        public string DownloadUrl { get; set; } 

        [JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; }
        
        // Mapping existing fields if they differ slightly
        // The user's prompt used "latest_version", "download_url". 
        // But the EXISTING update.json uses "latestVersion", "releasePage".
        // I will stick to the snake_case for the NEW fields if checking a NEW file, 
        // but the user said "already we have update.json file use only it".
        // So I must match the EXISTING update.json format.
    }
}
