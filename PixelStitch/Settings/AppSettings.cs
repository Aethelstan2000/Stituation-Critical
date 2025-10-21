
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StituationCritical.Settings
{
    public class AppSettings
    {
        private static readonly Lazy<AppSettings> _lazy = new Lazy<AppSettings>(Load);
        public static AppSettings Current => _lazy.Value;

        // ---- General ----
        public int DefaultCanvasWidth { get; set; } = 64;
        public int DefaultCanvasHeight { get; set; } = 64;
        public double DefaultZoom { get; set; } = 1.0;

        // ---- Reference Layer ----
        public double ReferenceOpacity { get; set; } = 0.5;
        public bool ReferenceVisible { get; set; } = true;
        public double PixelLayerOpacity { get; set; } = 1.0;

        // ---- Export ----
        public int ExportDpi { get; set; } = 144;
        public string ExportPageSize { get; set; } = "A4"; // A4, Letter, etc.
        public double GridCellMm { get; set; } = 4.0; // for PDF grid scaling
        public double ClothCount { get; set; } = 7.0; // for PDF grid scaling
        public int TrueSize_OverThreads { get; set; } = 1;


        // ---- Paths ----
        public string? LastOpenFolder { get; set; }
        public string? LastSaveFolder { get; set; }

        // Persistence
        [JsonIgnore]
        public static string SettingsFolder
        {
            get
            {
                // %LOCALAPPDATA%\DWSoftware\StituationCritical
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(baseDir, "DWSoftware", "StituationCritical");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        [JsonIgnore]
        public static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var obj = JsonSerializer.Deserialize<AppSettings>(json, opts);
                    if (obj != null) return obj;
                }
            }
            catch { /* ignore and fall back to defaults */ }
            return new AppSettings();
        }

        public void Save()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(this, opts);
            File.WriteAllText(SettingsPath, json);
        }
    }
}
