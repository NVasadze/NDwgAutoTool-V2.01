using System.Text.Json;

namespace NDwgAutoTool.Infrastructure.Settings
{
    public static class SettingsStore
    {
        private static readonly string FilePath =
            Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new AppSettings();

                string json = File.ReadAllText(FilePath);

                return JsonSerializer.Deserialize<AppSettings>(json)
                       ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // fail silently (do not break app if disk is locked)
            }
        }
    }
}