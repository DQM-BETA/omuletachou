namespace AfiliadoBot.Domain.Entities;

public class AppSetting
{
    public int Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }

    // Construtor para EF Core
    private AppSetting() { }

    public AppSetting(string key, string value)
    {
        Key = key;
        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateValue(string value)
    {
        Value = value;
        UpdatedAt = DateTime.UtcNow;
    }
}
