using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoDeathRoll;

public sealed class ConfigWindow : Window
{
    private const string GithubUrl = "https://github.com/seventity7/Auto-Death-Roll";

    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin)
        : base("Auto Death Roll Settings###AutoDeathRollConfig")
    {
        this.plugin = plugin;

        this.Size = new Vector2(980f, 760f);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags = ImGuiWindowFlags.NoCollapse;
    }

    public override void Draw()
    {
        this.plugin.Configuration.EnsureDefaults();

        this.DrawOpeningHeader();
        this.DrawActionRow(
            "Win out of 10:",
            this.plugin.Configuration.OpeningWinEmote,
            "Emote when win /random 10",
            this.plugin.Configuration.OpeningWinMessage,
            "Message to send in /say when won /random 10",
            value => this.plugin.Configuration.OpeningWinEmote = value,
            value => this.plugin.Configuration.OpeningWinMessage = value,
            "opening_win");

        this.DrawActionRow(
            "Loss out of 10:",
            this.plugin.Configuration.OpeningLossEmote,
            "Emote when lose /random 10",
            this.plugin.Configuration.OpeningLossMessage,
            "Message to send in /say when lose /random 10",
            value => this.plugin.Configuration.OpeningLossEmote = value,
            value => this.plugin.Configuration.OpeningLossMessage = value,
            "opening_loss");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        this.DrawTitle("★ Rolls phase");
        for (var i = 1; i <= 10; i++)
        {
            var index = i;
            this.DrawActionRow(
                $"{index}° Roll:",
                this.plugin.Configuration.GetRollEmote(index),
                $"{index}° /random roll Emote to use",
                this.plugin.Configuration.GetRollMessage(index),
                $"{index}° /random roll Message to send in /say",
                value => this.plugin.Configuration.SetRollEmote(index, value),
                value => this.plugin.Configuration.SetRollMessage(index, value),
                $"roll_{index}");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        this.DrawTitle("♯ End match");
        this.DrawActionRow(
            "You win:",
            this.plugin.Configuration.EndWinEmote,
            "Emote when you win",
            this.plugin.Configuration.EndWinMessage,
            "Message to send in /say when won",
            value => this.plugin.Configuration.EndWinEmote = value,
            value => this.plugin.Configuration.EndWinMessage = value,
            "end_win");

        this.DrawActionRow(
            "You lost:",
            this.plugin.Configuration.EndLossEmote,
            "Emote when you lose",
            this.plugin.Configuration.EndLossMessage,
            "Message to send in /say when lost",
            value => this.plugin.Configuration.EndLossEmote = value,
            value => this.plugin.Configuration.EndLossMessage = value,
            "end_loss");

        this.DrawGithubPopup();
    }

    private void DrawOpeningHeader()
    {
        var contentMaxX = ImGui.GetContentRegionMax().X;

        this.DrawTitle("♯ Opening", inline: true);

        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), contentMaxX - 24f));

        if (this.DrawQuestionButton("opening_help_button"))
            ImGui.OpenPopup("auto_death_roll_github_popup");

        ImGui.Spacing();
    }

    private void DrawGithubPopup()
    {
        if (!ImGui.BeginPopup("auto_death_roll_github_popup"))
            return;

        ImGui.TextUnformatted("Acess Auto-Death-Roll Github?");
        ImGui.Spacing();

        if (ImGui.Button("OK", new Vector2(90f, 0f)))
        {
            this.TryOpenGithub();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel", new Vector2(90f, 0f)))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private void TryOpenGithub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GithubUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private void DrawTitle(string text, bool inline = false)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, DeathRollColors.Gold);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();

        if (!inline)
            ImGui.Spacing();
    }

    private void DrawActionRow(
        string label,
        string emoteValue,
        string emotePlaceholder,
        string messageValue,
        string messagePlaceholder,
        Action<string> setEmote,
        Action<string> setMessage,
        string idPrefix)
    {
        var labelWidth = 120f;
        var emoteWidth = 260f;
        var testButtonWidth = 28f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rowWidth = ImGui.GetContentRegionAvail().X;
        var messageWidth = rowWidth - labelWidth - emoteWidth - testButtonWidth - (spacing * 3f);

        if (messageWidth < 180f)
            messageWidth = 180f;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(labelWidth);

        var localEmote = emoteValue ?? string.Empty;
        var localMessage = messageValue ?? string.Empty;
        var changed = false;

        changed |= this.DrawInputWithPlaceholder($"##{idPrefix}_emote", ref localEmote, 128, emoteWidth, emotePlaceholder);
        ImGui.SameLine();
        changed |= this.DrawInputWithPlaceholder($"##{idPrefix}_message", ref localMessage, 256, messageWidth, messagePlaceholder);
        ImGui.SameLine();

        if (this.DrawPlayStyleButton($"{idPrefix}_test_button", DeathRollColors.TestGreen))
            this.plugin.TestConfiguredAction(localEmote, localMessage);

        if (changed)
        {
            setEmote(localEmote);
            setMessage(localMessage);
            this.plugin.SaveConfiguration();
        }
    }

    private bool DrawInputWithPlaceholder(string id, ref string value, int maxLength, float width, string placeholder)
    {
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.InputText(id, ref value, maxLength);

        if (string.IsNullOrEmpty(value) && !ImGui.IsItemActive())
        {
            var min = ImGui.GetItemRectMin();
            var padding = ImGui.GetStyle().FramePadding;
            var placeholderPos = new Vector2(min.X + padding.X, min.Y + padding.Y);
            ImGui.GetWindowDrawList().AddText(
                placeholderPos,
                ImGui.GetColorU32(DeathRollColors.Faded),
                placeholder);
        }

        return changed;
    }

    private bool DrawQuestionButton(string id)
    {
        var size = new Vector2(24f, ImGui.GetFrameHeight());
        ImGui.PushStyleColor(ImGuiCol.Button, DeathRollColors.HelpPurple);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, DeathRollColors.HelpPurple + new Vector4(0.08f, 0.08f, 0.08f, 0.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, DeathRollColors.HelpPurple - new Vector4(0.08f, 0.08f, 0.08f, 0.00f));

        var clicked = ImGui.Button($"?##{id}", size);

        ImGui.PopStyleColor(3);
        return clicked;
    }

    private bool DrawPlayStyleButton(string id, Vector4 color)
    {
        var size = new Vector2(28f, ImGui.GetFrameHeight());
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color + new Vector4(0.08f, 0.08f, 0.08f, 0.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color - new Vector4(0.08f, 0.08f, 0.08f, 0.00f));

        var clicked = ImGui.Button($"##{id}", size);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        var triangleColor = ImGui.GetColorU32(DeathRollColors.White);
        var triangleHalfWidth = 4.5f;
        var triangleHalfHeight = 5.5f;

        drawList.AddTriangleFilled(
            new Vector2(center.X - triangleHalfWidth, center.Y - triangleHalfHeight),
            new Vector2(center.X - triangleHalfWidth, center.Y + triangleHalfHeight),
            new Vector2(center.X + triangleHalfWidth, center.Y),
            triangleColor);

        ImGui.PopStyleColor(3);
        return clicked;
    }
}
