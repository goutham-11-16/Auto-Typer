using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AutoTyper.Models;

namespace AutoTyper.Services
{
    public class StorageService
    {
        private readonly string _filePath;
        private readonly string _settingsFilePath;

        public StorageService()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoTyper");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "snippets.json");
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath)) return new AppSettings();
            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        public List<Snippet> LoadSnippets()
        {
            if (!File.Exists(_filePath)) return new List<Snippet>();

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<Snippet>>(json) ?? new List<Snippet>();
            }
            catch
            {
                return new List<Snippet>();
            }
        }

        public void SaveSnippets(List<Snippet> snippets)
        {
            try
            {
                string json = JsonSerializer.Serialize(snippets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                // Can't do much here, maybe log
                System.Diagnostics.Debug.WriteLine($"Failed to save snippets: {ex.Message}");
            }
        }

    }
}
