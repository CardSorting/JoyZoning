namespace JoyZoning.Domain.Entities;

public class AppConfigEntry
{
    public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
