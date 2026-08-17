using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace ExtremeInjector.Config
{
    [XmlRoot("Settings")]
    public class Settings
    {
        public DateTime LastUpdateCheck { get; set; } = DateTime.UtcNow;

        [XmlArray("Modules")]
        [XmlArrayItem("Module")]
        public List<ModuleItem> Modules { get; set; } = new List<ModuleItem>();

        public OptionsConfig Options { get; set; } = new OptionsConfig();
        public string ProcessName { get; set; } = "";
        public WarningConfig Warnings { get; set; } = new WarningConfig();
    }

    public class ModuleItem
    {
        public bool Enable { get; set; } = true;
        public string? Export { get; set; } = null;
        public string? Parameters { get; set; } = null;
        public string Path { get; set; } = "";
    }

    public class OptionsConfig
    {
        public AdvancedConfig Advanced { get; set; } = new AdvancedConfig();
        public bool AutoInject { get; set; } = false;
        public string Background1 { get; set; } = "DodgerBlue";
        public string Background2 { get; set; } = "DeepSkyBlue";
        public bool CloseOnInject { get; set; } = false;
        public int Delay { get; set; } = 0;
        public int DelayBetween { get; set; } = 0;
        public bool ErasePE { get; set; } = false;
        public bool HideModule { get; set; } = false;
        public int Method { get; set; } = 0; // 0: Standard, 1: LdrLoadDll, 2: Thread Hijacking, 3: Manual Map
        public ScrambleConfig Scramble { get; set; } = new ScrambleConfig();
        public bool StealthInject { get; set; } = false;
        public string TextColor { get; set; } = "White";
    }

    public class AdvancedConfig
    {
        public bool DisableExceptionSupport { get; set; } = false;
        public bool DisableSEHValidation { get; set; } = false;
        public bool HideFromDebugger { get; set; } = false;
        public bool ManualResolveImports { get; set; } = false;
    }

    public class ScrambleConfig
    {
        public bool CreateFakeDebugDirectory { get; set; } = false;
        public bool CreateNewEntryPoint { get; set; } = false;
        public bool InsertExtraSections { get; set; } = false;
        public bool ModifyAssemblyCode { get; set; } = false;
        public bool ModifyImportTable { get; set; } = false;
        public bool MoveRelocationTable { get; set; } = false;
        public bool RemoveDebugData { get; set; } = false;
        public bool RemoveUselessData { get; set; } = false;
        public bool RenameSections { get; set; } = false;
        public bool ScrambleHeaderFields { get; set; } = false;
        public bool ShiftSectionData { get; set; } = false;
        public bool ShiftSectionMemory { get; set; } = false;
        public bool StripSectionCharacteristics { get; set; } = false;
    }

    public class WarningConfig
    {
        public bool LdrpLoadDll { get; set; } = false;
        public bool ManualMap { get; set; } = false;
        public bool Scramble { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

        public static Settings Current { get; set; } = new Settings();

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var serializer = new XmlSerializer(typeof(Settings));
                    using var stream = File.OpenRead(SettingsFilePath);
                    Current = (Settings)serializer.Deserialize(stream)!;
                }
            }
            catch
            {
                Current = new Settings();
            }
        }

        public static void Save()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                using var stream = File.Create(SettingsFilePath);
                serializer.Serialize(stream, Current);
            }
            catch
            {
            }
        }
    }
}
