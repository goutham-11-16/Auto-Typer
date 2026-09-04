using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoTyper.Services
{
    /// <summary>
    /// Command sent from Game Bar Widget client to Auto-Typer engine.
    /// </summary>
    public class IpcCommand
    {
        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("delayPerChar")]
        public int? DelayPerChar { get; set; }

        [JsonPropertyName("delayPerWord")]
        public int? DelayPerWord { get; set; }

        [JsonPropertyName("countdownSeconds")]
        public int? CountdownSeconds { get; set; }

        [JsonPropertyName("snippetId")]
        public string? SnippetId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("hotkey")]
        public string? Hotkey { get; set; }
    }

    /// <summary>
    /// Response or event sent from Auto-Typer engine to Game Bar Widget.
    /// </summary>
    public class IpcResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "STATUS";

        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = "Ready";

        [JsonPropertyName("isPaused")]
        public bool IsPaused { get; set; }

        [JsonPropertyName("activeSnippet")]
        public string ActiveSnippet { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "HumanLike";

        [JsonPropertyName("delayPerChar")]
        public int DelayPerChar { get; set; } = 20;

        [JsonPropertyName("delayPerWord")]
        public int DelayPerWord { get; set; } = 100;

        [JsonPropertyName("countdown")]
        public int Countdown { get; set; } = 0;

        [JsonPropertyName("progress")]
        public int Progress { get; set; } = 0;

        [JsonPropertyName("total")]
        public int Total { get; set; } = 0;

        [JsonPropertyName("snippets")]
        public List<IpcSnippetDto>? Snippets { get; set; }
    }

    /// <summary>
    /// Snippet data transfer object for Game Bar widget selector.
    /// </summary>
    public class IpcSnippetDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "HumanLike";

        [JsonPropertyName("delayPerChar")]
        public int DelayPerChar { get; set; }

        [JsonPropertyName("delayPerWord")]
        public int DelayPerWord { get; set; }

        [JsonPropertyName("hotkey")]
        public string Hotkey { get; set; } = string.Empty;
    }
}
