using System.Text.Json;

namespace NDwgAutoTool.Infrastructure.Settings
{
    public static class UserSettingsStore
    {
        private static readonly string DirectoryPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NDwgAutoTool");

        private static readonly string FilePath =
            Path.Combine(DirectoryPath, "user-settings.json");

        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new UserSettings();

                string json = File.ReadAllText(FilePath);

                var settings = JsonSerializer.Deserialize<UserSettings>(json)
                               ?? new UserSettings();

                settings.OpenAll ??= new OpenAllPreferences();
                settings.WindowLocation ??= new WindowLocationPreferences();
                settings.CompactView ??= new CompactViewPreferences();
                settings.BatchGroups ??= new BatchGroupPreferences();
                return settings;
            }
            catch
            {
                return new UserSettings();
            }
        }

        public static void Save(UserSettings settings)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);

                settings.OpenAll ??= new OpenAllPreferences();
                settings.WindowLocation ??= new WindowLocationPreferences();
                settings.CompactView ??= new CompactViewPreferences();
                settings.BatchGroups ??= new BatchGroupPreferences();

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // User preferences should never block the main tool.
            }
        }
    }
}
