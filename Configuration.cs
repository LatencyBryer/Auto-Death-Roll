using Dalamud.Configuration;
using Dalamud.Plugin;

namespace AutoDeathRoll;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public string OpeningWinEmote { get; set; } = string.Empty;
    public string OpeningWinMessage { get; set; } = string.Empty;
    public string OpeningLossEmote { get; set; } = string.Empty;
    public string OpeningLossMessage { get; set; } = string.Empty;

    public string[] RollEmotes { get; set; } = new string[10];
    public string[] RollMessages { get; set; } = new string[10];

    public string EndWinEmote { get; set; } = string.Empty;
    public string EndWinMessage { get; set; } = string.Empty;
    public string EndLossEmote { get; set; } = string.Empty;
    public string EndLossMessage { get; set; } = string.Empty;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        this.EnsureDefaults();
    }

    public void EnsureDefaults()
    {
        this.RollEmotes ??= new string[10];
        this.RollMessages ??= new string[10];

        if (this.RollEmotes.Length != 10)
        {
            var rollEmotes = this.RollEmotes;
            Array.Resize(ref rollEmotes, 10);
            this.RollEmotes = rollEmotes;
        }

        if (this.RollMessages.Length != 10)
        {
            var rollMessages = this.RollMessages;
            Array.Resize(ref rollMessages, 10);
            this.RollMessages = rollMessages;
        }

        for (var i = 0; i < 10; i++)
        {
            this.RollEmotes[i] ??= string.Empty;
            this.RollMessages[i] ??= string.Empty;
        }

        this.OpeningWinEmote ??= string.Empty;
        this.OpeningWinMessage ??= string.Empty;
        this.OpeningLossEmote ??= string.Empty;
        this.OpeningLossMessage ??= string.Empty;
        this.EndWinEmote ??= string.Empty;
        this.EndWinMessage ??= string.Empty;
        this.EndLossEmote ??= string.Empty;
        this.EndLossMessage ??= string.Empty;
    }

    public string GetRollEmote(int index)
    {
        this.EnsureDefaults();
        if (index < 1 || index > 10)
            return string.Empty;

        return this.RollEmotes[index - 1] ?? string.Empty;
    }

    public string GetRollMessage(int index)
    {
        this.EnsureDefaults();
        if (index < 1 || index > 10)
            return string.Empty;

        return this.RollMessages[index - 1] ?? string.Empty;
    }

    public void SetRollEmote(int index, string value)
    {
        this.EnsureDefaults();
        if (index < 1 || index > 10)
            return;

        this.RollEmotes[index - 1] = value ?? string.Empty;
    }

    public void SetRollMessage(int index, string value)
    {
        this.EnsureDefaults();
        if (index < 1 || index > 10)
            return;

        this.RollMessages[index - 1] = value ?? string.Empty;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
