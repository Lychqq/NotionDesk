using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace DesktopNotes
{
    public class NoteWindowState
    {
        public string FilePath { get; set; } = @"C:\Users\Lenovo\Desktop\перенос\на ноут\антигравити.txt";
        public List<string> FileList { get; set; } = new List<string>();
        public int FileIndex { get; set; } = 0;
        public double Width { get; set; } = 440;
        public double Height { get; set; } = 640;
        public double Left { get; set; } = -1;
        public double Top { get; set; } = -1;
        public bool IsAlwaysOnTop { get; set; } = false;
        public bool UseMonoFont { get; set; } = false;
    }

    public class AppConfig
    {
        public string Language { get; set; } = "ru"; // "ru" | "en"
        public bool AutoStart { get; set; } = false;
        public List<NoteWindowState> Notes { get; set; } = new List<NoteWindowState>();

        // Обратная совместимость
        public string? FilePath { get; set; }
        public double? WindowWidth { get; set; }
        public double? WindowHeight { get; set; }
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public bool? IsAlwaysOnTop { get; set; }
        public bool? UseMonoFont { get; set; }

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NotionDeskNotes";

        private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null)
                    {
                        if (cfg.Notes.Count == 0)
                        {
                            cfg.Notes.Add(new NoteWindowState
                            {
                                FilePath = !string.IsNullOrEmpty(cfg.FilePath) ? cfg.FilePath : @"C:\Users\Lenovo\Desktop\перенос\на ноут\антигравити.txt",
                                Width = cfg.WindowWidth ?? 440,
                                Height = cfg.WindowHeight ?? 640,
                                Left = cfg.WindowLeft ?? -1,
                                Top = cfg.WindowTop ?? -1,
                                IsAlwaysOnTop = cfg.IsAlwaysOnTop ?? false,
                                UseMonoFont = cfg.UseMonoFont ?? false
                            });
                        }
                        return cfg;
                    }
                }
            }
            catch { }

            var defaultCfg = new AppConfig();
            defaultCfg.Notes.Add(new NoteWindowState());
            return defaultCfg;
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NotionDesk.exe");
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch { }
        }
    }
}
