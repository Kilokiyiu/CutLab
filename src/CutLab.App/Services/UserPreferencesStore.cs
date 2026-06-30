namespace CutLab.App.Services;

public sealed class UserPreferences
{
    public double ShotColCutWidth { get; set; } = 90;

    public double ShotColSourceWidth { get; set; } = 200;

    public double ShotColTargetWidth { get; set; } = 200;

    public double ShotColStatusWidth { get; set; } = 80;

    public int ConflictResolutionStrategy { get; set; }
}

public interface IUserPreferencesStore
{
    UserPreferences Load();

    void Save(UserPreferences preferences);
}

public sealed class JsonUserPreferencesStore : IUserPreferencesStore
{
    private static readonly string PreferencesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CutLab");

    private static readonly string PreferencesPath = Path.Combine(PreferencesDirectory, "preferences.json");

    public UserPreferences Load()
    {
        if (!File.Exists(PreferencesPath))
        {
            return new UserPreferences();
        }

        try
        {
            var json = File.ReadAllText(PreferencesPath);
            return System.Text.Json.JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        catch (IOException)
        {
            return new UserPreferences();
        }
        catch (System.Text.Json.JsonException)
        {
            return new UserPreferences();
        }
    }

    public void Save(UserPreferences preferences)
    {
        Directory.CreateDirectory(PreferencesDirectory);
        var json = System.Text.Json.JsonSerializer.Serialize(preferences, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(PreferencesPath, json);
    }
}
