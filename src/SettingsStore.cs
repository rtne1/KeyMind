using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Win32;

namespace KeyMind
{
    public sealed class KeyMindSettings
    {
        public bool Enabled { get; set; }
        public string UiLanguage { get; set; }
        public bool AutoMode { get; set; }
        public bool Startup { get; set; }
        public bool ShowNotifications { get; set; }
        public int Threshold { get; set; }
        public int Corrections { get; set; }
        public string ExtraEnglish { get; set; }
        public string ExtraArabic { get; set; }
        public string ExcludedApps { get; set; }

        public KeyMindSettings()
        {
            Enabled = true;
            UiLanguage = "ar";
            AutoMode = true;
            Startup = false;
            ShowNotifications = false;
            Threshold = 88;
            Corrections = 0;
            ExtraEnglish = "";
            ExtraArabic = "";
            ExcludedApps = "keepass,keepassxc,1password,bitwarden,windowssecurity,powershell,cmd,windowsterminal,putty";
        }
    }

    public static class SettingsStore
    {
        public static string SettingsDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMind"); }
        }
        private static string PathName { get { return Path.Combine(SettingsDir, "settings.json"); } }

        public static KeyMindSettings Load()
        {
            try
            {
                if (!File.Exists(PathName)) return new KeyMindSettings();
                KeyMindSettings o;
                using (var bytes = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(PathName))))
                    o = (KeyMindSettings)new DataContractJsonSerializer(typeof(KeyMindSettings)).ReadObject(bytes);
                if (o == null) return new KeyMindSettings();
                if (o.Threshold < 65 || o.Threshold > 99) o.Threshold = 88;
                if (o.ExtraArabic == null) o.ExtraArabic = "";
                if (o.ExtraEnglish == null) o.ExtraEnglish = "";
                if (o.ExcludedApps == null) o.ExcludedApps = "";
                return o;
            }
            catch (Exception e) { Program.LogError(e); return new KeyMindSettings(); }
        }

        public static void Save(KeyMindSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                using (var memory = new MemoryStream())
                {
                    new DataContractJsonSerializer(typeof(KeyMindSettings)).WriteObject(memory, settings);
                    File.WriteAllText(PathName, Encoding.UTF8.GetString(memory.ToArray()));
                }
            }
            catch (Exception e) { Program.LogError(e); }
        }

        public static void SetStartup(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key == null) return;
                if (enabled)
                {
                    string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KeyMind.exe");
                    // Only an existing compiled executable may be registered.
                    if (!File.Exists(executable)) return;
                    key.SetValue("KeyMind", "\"" + executable + "\"");
                }
                else key.DeleteValue("KeyMind", false);
            }
        }
    }
}
