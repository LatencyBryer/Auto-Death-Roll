using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoDeathRoll;

public sealed class MainWindow : Window
{
    private static readonly Vector4 HelpButtonPurple = new(196f / 255f, 34f / 255f, 245f / 255f, 1f); // #c422f5
    private static readonly Vector4 HelpPopupWhite = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 GithubButtonPurple = new(195f / 255f, 74f / 255f, 1f, 1f); // #c34aff

    private readonly Plugin plugin;
    private bool helpPopupOpen;
    private bool githubConfirmPopupOpen;

    public MainWindow(Plugin plugin)
        : base("Auto Death Roll###AutoDeathRollMain")
    {
        this.plugin = plugin;

        this.Size = new Vector2(460f, 620f);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.Flags = ImGuiWindowFlags.NoCollapse;
    }

    public override void Draw()
    {
        this.AdjustWindowHeight();

        this.DrawTopControls();
        this.DrawHelpPopup();

        ImGui.Separator();
        ImGui.Spacing();

        var displayStatusText = this.plugin.StatusText.Replace("No match found", "No target found", StringComparison.Ordinal);

        this.DrawCenteredColoredText(
            $"{displayStatusText}{this.plugin.AnimatedDots}",
            this.plugin.StatusColor,
            $"{displayStatusText}...",
            string.Equals(displayStatusText, "No target found", StringComparison.Ordinal));

        ImGui.Spacing();
        this.DrawCenteredSegments(this.plugin.GetSubtitleSegments());

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        this.DrawMatchActionButtons();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        this.DrawLogArea();

        if (this.plugin.ShowEndMatchButton)
        {
            ImGui.Dummy(new Vector2(0f, 8f));
            this.DrawEndMatchButton();
        }
    }

    private void AdjustWindowHeight()
    {
        var currentSize = ImGui.GetWindowSize();
        var desiredHeight = this.plugin.GetSuggestedWindowHeight();

        if (MathF.Abs(currentSize.Y - desiredHeight) > 1f)
            ImGui.SetWindowSize(new Vector2(currentSize.X, desiredHeight));
    }

    private void DrawTopControls()
    {
        const float sideButtonWidth = 30f;
        const float compactGap = 2f;

        var fullWidth = ImGui.GetContentRegionAvail().X;
        var leftColumnWidth = fullWidth - sideButtonWidth - compactGap;

        if (leftColumnWidth < 100f)
            leftColumnWidth = 100f;

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(compactGap, compactGap));

        ImGui.BeginGroup();

        ImGui.BeginGroup();
        this.DrawTrackedEnemyField(leftColumnWidth, ImGui.GetFrameHeight());
        this.DrawTargetEnemyButton(leftColumnWidth);
        ImGui.EndGroup();

        ImGui.SameLine(0f, compactGap);

        ImGui.BeginGroup();
        this.DrawHelpButton(sideButtonWidth);
        this.DrawClearButton(sideButtonWidth);
        ImGui.EndGroup();

        ImGui.EndGroup();

        ImGui.PopStyleVar();
    }

    private void DrawTrackedEnemyField(float width, float height)
    {
        var size = new Vector2(width, height);
        var pos = ImGui.GetCursorScreenPos();
        var padding = ImGui.GetStyle().FramePadding;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(
            pos,
            new Vector2(pos.X + size.X, pos.Y + size.Y),
            ImGui.GetColorU32(new Vector4(0.10f, 0.12f, 0.18f, 1.00f)),
            4f);

        drawList.AddRect(
            pos,
            new Vector2(pos.X + size.X, pos.Y + size.Y),
            ImGui.GetColorU32(new Vector4(0.42f, 0.60f, 0.95f, 1.00f)),
            4f);

        var text = this.plugin.TrackedEnemyName;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var textPos = new Vector2(
                pos.X + padding.X,
                pos.Y + ((size.Y - ImGui.GetTextLineHeight()) * 0.5f));

            drawList.AddText(textPos, ImGui.GetColorU32(DeathRollColors.White), text);
        }

        ImGui.Dummy(size);
    }

    private void DrawTargetEnemyButton(float width)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, DeathRollColors.PrimaryBlue);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.48f, 0.71f, 1.00f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.34f, 0.57f, 0.92f, 1.00f));

        if (ImGui.Button("##TargetEnemyButton", new Vector2(width, 0f)))
            this.plugin.TryTrackCurrentTarget();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var text = this.plugin.TargetButtonText;
        var textSize = ImGui.CalcTextSize(text);
        var textPos = new Vector2(
            min.X + ((max.X - min.X - textSize.X) * 0.5f),
            min.Y + ((max.Y - min.Y - textSize.Y) * 0.5f));

        var drawList = ImGui.GetWindowDrawList();
        var textColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));

        drawList.AddText(textPos + new Vector2(0.8f, 0f), textColor, text);
        drawList.AddText(textPos, textColor, text);

        ImGui.PopStyleColor(3);
    }

    private void DrawHelpButton(float width)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, HelpButtonPurple);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.84f, 0.28f, 0.98f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.64f, 0.07f, 0.82f, 1.00f));

        if (ImGui.Button("##DeathRollHelpButton", new Vector2(width, 0f)))
        {
            this.helpPopupOpen = true;
            ImGui.OpenPopup("AutoDeathRollHelpPopup");
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var text = "?";
        var textSize = ImGui.CalcTextSize(text);
        var textPos = new Vector2(
            min.X + ((max.X - min.X - textSize.X) * 0.5f),
            min.Y + ((max.Y - min.Y - textSize.Y) * 0.5f));

        var drawList = ImGui.GetWindowDrawList();
        var textColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));

        drawList.AddText(textPos + new Vector2(0.8f, 0f), textColor, text);
        drawList.AddText(textPos, textColor, text);

        ImGui.PopStyleColor(3);
    }

    private void DrawClearButton(float width)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, DeathRollColors.Danger);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.30f, 0.30f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.70f, 0.15f, 0.15f, 1.00f));

        if (ImGui.Button("X", new Vector2(width, 0f)))
            this.plugin.ClearTrackedEnemy();

        ImGui.PopStyleColor(3);
    }

    private void DrawHelpPopup()
    {
        if (this.helpPopupOpen)
            ImGui.SetNextWindowSize(new Vector2(430f, 0f), ImGuiCond.Appearing);

        if (!ImGui.BeginPopupModal("AutoDeathRollHelpPopup", ref this.helpPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        this.DrawHelpCenteredPurpleItalicText("You dont need to do any of this! Auto-Death-Roll does everything automaticaly for you");
        this.DrawHelpCenteredMixedCustomText("Use ", "/dr config", " for customizing options");

        ImGui.Spacing();
        ImGui.TextColored(DeathRollColors.Gold, "♯ How Death Roll works");
        ImGui.Spacing();

        ImGui.TextColored(DeathRollColors.Success, "・ Preparation:");
        ImGui.Spacing();
        ImGui.TextWrapped("Players agree on the rules beforehand, such as the maximum number (typically 999, since /random goes from 1 to 999).");
        this.DrawInlineWrappedSegments(
            new LogSegment("They decide who will start typing ", DeathRollColors.White),
            new LogSegment("/random 10", DeathRollColors.Gold),
            new LogSegment(" (highest number starts).", DeathRollColors.White));
        ImGui.Spacing();

        ImGui.TextColored(DeathRollColors.Success, "・ Execution:");
        ImGui.Spacing();
        this.DrawInlineWrappedSegments(
            new LogSegment("The starting player types ", DeathRollColors.White),
            new LogSegment("/random", DeathRollColors.Gold),
            new LogSegment(" in chat, which generates a random number between 1 and 999.", DeathRollColors.White));
        ImGui.TextWrapped("The next player rolls a maximum of the previous number, decreasing until someone hits 1 and loses.");
        ImGui.Spacing();

        ImGui.TextColored(DeathRollColors.Success, "・ Practical Examples:");
        ImGui.Spacing();

        this.DrawInlineWrappedSegments(
            new LogSegment("Latency Bryer types ", DeathRollColors.White),
            new LogSegment("/random 10", DeathRollColors.Gold),
            new LogSegment(" and gets ", DeathRollColors.White),
            new LogSegment("10", DeathRollColors.Gold),
            new LogSegment(".", DeathRollColors.White));

        this.DrawInlineWrappedSegments(
            new LogSegment("Phaeris Ellie types ", DeathRollColors.White),
            new LogSegment("/random 10", DeathRollColors.Gold),
            new LogSegment(" and gets ", DeathRollColors.White),
            new LogSegment("5", DeathRollColors.Gold),
            new LogSegment(".", DeathRollColors.White));

        this.DrawInlineWrappedSegments(
            new LogSegment("Latency Bryer Wins and follows typing ", DeathRollColors.White),
            new LogSegment("/random", DeathRollColors.Gold),
            new LogSegment(" and gets ", DeathRollColors.White),
            new LogSegment("790", DeathRollColors.Gold),
            new LogSegment(".", DeathRollColors.White));

        this.DrawInlineWrappedSegments(
            new LogSegment("Phaeris Ellie follows typing ", DeathRollColors.White),
            new LogSegment("/random 790", DeathRollColors.Gold),
            new LogSegment(" and gets ", DeathRollColors.White),
            new LogSegment("70", DeathRollColors.Gold),
            new LogSegment(".", DeathRollColors.White));

        this.DrawInlineWrappedSegments(
            new LogSegment("Latency Bryer follows typing ", DeathRollColors.White),
            new LogSegment("/random 70", DeathRollColors.Gold),
            new LogSegment(" and so on.", DeathRollColors.White));

        this.DrawInlineWrappedSegments(
            new LogSegment("First to get ", DeathRollColors.White),
            new LogSegment("[1]", DeathRollColors.Gold),
            new LogSegment(" loses.", DeathRollColors.White));

        var githubButtonWidth = 78f;
        var remainingWidth = ImGui.GetContentRegionAvail().X;
        var targetPosX = ImGui.GetCursorPosX() + remainingWidth - githubButtonWidth;

        if (targetPosX > ImGui.GetCursorPosX())
            ImGui.SameLine(targetPosX);
        else
            ImGui.SameLine();

        this.DrawGithubButton(githubButtonWidth);
        this.DrawGithubConfirmPopup();

        ImGui.EndPopup();
    }

    private void DrawGithubButton(float width)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, GithubButtonPurple);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.84f, 0.45f, 1.00f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.68f, 0.23f, 0.94f, 1.00f));

        if (ImGui.Button("##GithubButton", new Vector2(width, 0f)))
        {
            this.githubConfirmPopupOpen = true;
            ImGui.OpenPopup("AutoDeathRollGithubConfirmPopup");
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var text = "Github";
        var textSize = ImGui.CalcTextSize(text);
        var textPos = new Vector2(
            min.X + ((max.X - min.X - textSize.X) * 0.5f),
            min.Y + ((max.Y - min.Y - textSize.Y) * 0.5f));

        var drawList = ImGui.GetWindowDrawList();
        var textColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));

        drawList.AddText(textPos + new Vector2(0.8f, 0f), textColor, text);
        drawList.AddText(textPos, textColor, text);

        ImGui.PopStyleColor(3);
    }

    private void DrawGithubConfirmPopup()
    {
        if (this.githubConfirmPopupOpen)
            ImGui.SetNextWindowSize(new Vector2(250f, 0f), ImGuiCond.Appearing);

        var open = this.githubConfirmPopupOpen;
        if (!ImGui.BeginPopupModal("AutoDeathRollGithubConfirmPopup", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            this.githubConfirmPopupOpen = open;
            return;
        }

        this.githubConfirmPopupOpen = open;

        ImGui.TextUnformatted("Acess Github page?");
        ImGui.Spacing();

        var buttonWidth = 90f;
        if (ImGui.Button("Yes", new Vector2(buttonWidth, 0f)))
        {
            this.OpenGithubPage();
            this.githubConfirmPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button("No", new Vector2(buttonWidth, 0f)))
        {
            this.githubConfirmPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private void OpenGithubPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/seventity7/Auto-Death-Roll",
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }

    private void DrawHelpCenteredPurpleItalicText(string text)
    {
        var color = HelpPopupWhite;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var cursorX = ImGui.GetCursorPosX();

        if (textWidth < availableWidth)
            ImGui.SetCursorPosX(cursorX + ((availableWidth - textWidth) * 0.5f));

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private void DrawHelpCenteredMixedCustomText(string prefix, string highlight, string suffix)
    {
        var white = HelpPopupWhite;
        var green = DeathRollColors.Success;

        var prefixWidth = ImGui.CalcTextSize(prefix).X;
        var highlightWidth = ImGui.CalcTextSize(highlight).X;
        var suffixWidth = ImGui.CalcTextSize(suffix).X;
        var totalWidth = prefixWidth + highlightWidth + suffixWidth;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var cursorX = ImGui.GetCursorPosX();

        if (totalWidth < availableWidth)
            ImGui.SetCursorPosX(cursorX + ((availableWidth - totalWidth) * 0.5f));

        ImGui.PushStyleColor(ImGuiCol.Text, white);
        ImGui.TextUnformatted(prefix);
        ImGui.PopStyleColor();

        ImGui.SameLine(0f, 0f);
        ImGui.PushStyleColor(ImGuiCol.Text, green);
        ImGui.TextUnformatted(highlight);
        ImGui.PopStyleColor();

        ImGui.SameLine(0f, 0f);
        ImGui.PushStyleColor(ImGuiCol.Text, white);
        ImGui.TextUnformatted(suffix);
        ImGui.PopStyleColor();
    }

    private void DrawInlineWrappedSegments(params LogSegment[] segments)
    {
        var first = true;

        foreach (var segment in segments)
        {
            if (!first)
                ImGui.SameLine(0f, 0f);

            ImGui.PushStyleColor(ImGuiCol.Text, segment.Color);
            ImGui.TextUnformatted(segment.Text);
            ImGui.PopStyleColor();

            first = false;
        }
    }

    private void DrawMatchActionButtons()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4f, 2f));

        this.DrawStartButton();
        this.DrawSecondaryButtons();

        ImGui.PopStyleVar();
    }

    private void DrawStartButton()
    {
        this.DrawEnabledAwareButton(
            this.plugin.StartButtonText,
            new Vector2(-1f, 0f),
            this.plugin.StartButtonColor,
            new Vector4(0.14f, 0.75f, 0.05f, 1.00f),
            new Vector4(0.07f, 0.55f, 0.00f, 1.00f),
            this.plugin.CanStartMatch,
            this.plugin.StartMatch);
    }

    private void DrawSecondaryButtons()
    {
        var totalWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var halfWidth = (totalWidth - spacing) * 0.5f;

        this.DrawEnabledAwareButton(
            "Cancel Match",
            new Vector2(halfWidth, 0f),
            DeathRollColors.CancelRed,
            new Vector4(1.00f, 0.42f, 0.30f, 1.00f),
            new Vector4(0.82f, 0.20f, 0.12f, 1.00f),
            this.plugin.CanCancelMatch,
            this.plugin.CancelMatch);

        ImGui.SameLine();

        this.DrawEnabledAwareButton(
            "Abort Match",
            new Vector2(halfWidth, 0f),
            DeathRollColors.AbortPink,
            new Vector4(0.98f, 0.38f, 0.53f, 1.00f),
            new Vector4(0.75f, 0.18f, 0.35f, 1.00f),
            this.plugin.CanAbortMatch,
            this.plugin.AbortMatch);
    }

    private void DrawEndMatchButton()
    {
        this.DrawEnabledAwareButton(
            "End Match",
            new Vector2(-1f, 0f),
            DeathRollColors.Danger,
            new Vector4(0.95f, 0.30f, 0.30f, 1.00f),
            new Vector4(0.70f, 0.15f, 0.15f, 1.00f),
            true,
            this.plugin.EndMatch);
    }

    private void DrawEnabledAwareButton(string text, Vector2 size, Vector4 normal, Vector4 hovered, Vector4 active, bool enabled, Action onClick)
    {
        var buttonColor = enabled ? normal : new Vector4(normal.X, normal.Y, normal.Z, 0.45f);
        var hoveredColor = enabled ? hovered : buttonColor;
        var activeColor = enabled ? active : buttonColor;

        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoveredColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, activeColor);

        if (!enabled)
            ImGui.BeginDisabled();

        if (ImGui.Button(text, size) && enabled)
            onClick();

        if (!enabled)
            ImGui.EndDisabled();

        ImGui.PopStyleColor(3);
    }

    private void DrawLogArea()
    {
        foreach (var line in this.plugin.LogLines)
        {
            if (line.BlankBefore)
                ImGui.Dummy(new Vector2(0f, ImGui.GetTextLineHeight()));

            if (line.Centered)
                this.DrawCenteredSegments(line.Segments);
            else
                this.DrawSegments(line.Segments);

            if (line.BlankAfter)
                ImGui.Dummy(new Vector2(0f, ImGui.GetTextLineHeight()));
        }
    }

    private void DrawSegments(IReadOnlyList<LogSegment> segments)
    {
        var first = true;

        foreach (var segment in segments)
        {
            if (!first)
                ImGui.SameLine(0f, 0f);

            this.DrawSegment(segment);
            first = false;
        }
    }

    private void DrawCenteredSegments(IReadOnlyList<LogSegment> segments)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var cursorX = ImGui.GetCursorPosX();
        var width = 0f;

        foreach (var segment in segments)
            width += ImGui.CalcTextSize(segment.Text).X;

        if (width < availableWidth)
            ImGui.SetCursorPosX(cursorX + ((availableWidth - width) * 0.5f));

        this.DrawSegments(segments);
    }

    private void DrawSegment(LogSegment segment)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, segment.Color);

        if (segment.IsBold)
        {
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var color = ImGui.GetColorU32(segment.Color);
            drawList.AddText(pos + new Vector2(0.75f, 0f), color, segment.Text);
        }

        ImGui.TextUnformatted(segment.Text);
        ImGui.PopStyleColor();
    }

    private void DrawCenteredColoredText(string text, Vector4 color, string? stableWidthReference = null, bool isBold = false)
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var cursorX = ImGui.GetCursorPosX();
        var reference = stableWidthReference ?? text;
        var textWidth = ImGui.CalcTextSize(reference).X;

        if (textWidth < availableWidth)
            ImGui.SetCursorPosX(cursorX + ((availableWidth - textWidth) * 0.5f));

        ImGui.PushStyleColor(ImGuiCol.Text, color);

        if (isBold)
        {
            var pos = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var drawColor = ImGui.GetColorU32(color);
            drawList.AddText(pos + new Vector2(0.75f, 0f), drawColor, text);
        }

        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }
}
