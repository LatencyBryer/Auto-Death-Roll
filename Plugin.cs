using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace AutoDeathRoll;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Auto Death Roll";

    private const string MainCommandName = "/deathroll";
    private const string ShortCommandName = "/dr";
    private const string ShortAltCommandName = "/drt";
    private const string DirectConfigCommandName = "/drc";

    private static readonly Regex SelfRollRegex = new(
        @"^Random!\s+You roll a(?:\s+[^\d]+)?\s*(?<roll>\d+)(?: \(out of (?<max>\d+)\))?\.?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OtherRollRegex = new(
        @"^Random!\s+(?<name>.+?) rolls a(?:\s+[^\d]+)?\s*(?<roll>\d+)(?: \(out of (?<max>\d+)\))?\.?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;

    private readonly WindowSystem windowSystem;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;

    private readonly List<LogLine> logLines = [];

    private MatchState matchState = MatchState.Idle;
    private string trackedEnemyName = string.Empty;
    private bool isTrackingEnemy;
    private bool hasOpeningRollRegistered;
    private bool hasMainRoundRollRegistered;
    private bool showEndMatchButton;

    private int? selfOpeningRoll;
    private int? enemyOpeningRoll;
    private int? expectedSelfMax;
    private int? expectedEnemyMax;

    private int selfMainRollCount;
    private DateTime? lastAutoSelfCommandDueUtc;

    private DateTime targetButtonOverrideUntilUtc = DateTime.MinValue;
    private string targetButtonOverrideText = string.Empty;

    private DateTime startButtonOverrideUntilUtc = DateTime.MinValue;
    private string startButtonOverrideText = string.Empty;

    public Configuration Configuration { get; }

    public IReadOnlyList<LogLine> LogLines => this.logLines;
    public bool IsTrackingEnemy => this.isTrackingEnemy;
    public string TrackedEnemyName => this.trackedEnemyName;
    public bool ShowEndMatchButton => this.showEndMatchButton;
    public bool CanStartMatch => this.isTrackingEnemy && !this.IsMatchRunning && !this.showEndMatchButton;
    public bool CanCancelMatch =>
        !this.showEndMatchButton &&
        this.matchState == MatchState.WaitingForOpeners &&
        this.hasOpeningRollRegistered;
    public bool CanAbortMatch =>
        !this.showEndMatchButton &&
        this.IsMatchRunning &&
        this.hasMainRoundRollRegistered;
    public bool IsMatchRunning => this.matchState != MatchState.Idle && this.matchState != MatchState.Finished;

    public string LocalPlayerName
    {
        get
        {
            var name = this.objectTable.LocalPlayer?.Name.TextValue;
            return string.IsNullOrWhiteSpace(name) ? "Waiting for login" : name;
        }
    }

    public string AnimatedDots => ((Environment.TickCount64 / 500) % 3) switch
    {
        0 => ".",
        1 => "..",
        _ => "...",
    };

    public string StatusText
    {
        get
        {
            if (this.IsMatchRunning)
                return "♯ Match running";

            if (this.isTrackingEnemy)
                return "♯ Tracking Enemy";

            return "♯ No match found";
        }
    }

    public Vector4 StatusColor
    {
        get
        {
            if (this.IsMatchRunning)
                return DeathRollColors.Success;

            if (this.isTrackingEnemy)
                return DeathRollColors.PrimaryBlue;

            return DeathRollColors.LightRed;
        }
    }

    public string TargetButtonText
    {
        get
        {
            if (DateTime.UtcNow < this.targetButtonOverrideUntilUtc)
                return this.targetButtonOverrideText;

            return this.isTrackingEnemy ? "● Tracking Enemy" : "○ Target Enemy";
        }
    }

    public string StartButtonText
    {
        get
        {
            if (DateTime.UtcNow < this.startButtonOverrideUntilUtc)
                return this.startButtonOverrideText;

            return "Start Match";
        }
    }

    public Vector4 StartButtonColor => DateTime.UtcNow < this.startButtonOverrideUntilUtc
        ? DeathRollColors.Danger
        : DeathRollColors.MatchGreen;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IFramework framework,
        IObjectTable objectTable,
        ITargetManager targetManager)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.framework = framework;
        this.objectTable = objectTable;
        this.targetManager = targetManager;

        this.Configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Initialize(this.pluginInterface);

        this.windowSystem = new WindowSystem("AutoDeathRollWindows");
        this.mainWindow = new MainWindow(this);
        this.configWindow = new ConfigWindow(this);

        this.windowSystem.AddWindow(this.mainWindow);
        this.windowSystem.AddWindow(this.configWindow);

        this.commandManager.AddHandler(MainCommandName, new CommandInfo(this.OnMainCommand)
        {
            HelpMessage = "Open the Auto Death Roll main window.",
        });

        this.commandManager.AddHandler(ShortCommandName, new CommandInfo(this.OnShortCommand)
        {
            HelpMessage = "Open the main window or use /dr config or /dr settings.",
        });

        this.commandManager.AddHandler(ShortAltCommandName, new CommandInfo(this.OnShortCommand)
        {
            HelpMessage = "Open the main window or use /drt config or /drt settings.",
        });

        this.commandManager.AddHandler(DirectConfigCommandName, new CommandInfo(this.OnConfigCommand)
        {
            HelpMessage = "Open the Auto Death Roll config window.",
        });

        this.pluginInterface.UiBuilder.Draw += this.windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;

        this.framework.Update += this.OnFrameworkUpdate;
        this.chatGui.ChatMessage += this.OnChatMessage;
    }

    public void Dispose()
    {
        this.chatGui.ChatMessage -= this.OnChatMessage;
        this.framework.Update -= this.OnFrameworkUpdate;

        this.pluginInterface.UiBuilder.Draw -= this.windowSystem.Draw;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;

        this.commandManager.RemoveHandler(MainCommandName);
        this.commandManager.RemoveHandler(ShortCommandName);
        this.commandManager.RemoveHandler(ShortAltCommandName);
        this.commandManager.RemoveHandler(DirectConfigCommandName);

        this.windowSystem.RemoveAllWindows();
        this.Configuration.Save();
    }

    public void SaveConfiguration()
    {
        this.Configuration.Save();
    }

    public void TestConfiguredAction(string? emoteOrCommand, string? sayMessage)
    {
        this.TriggerConfiguredAction(emoteOrCommand, sayMessage, TimeSpan.Zero);
    }

    public void ToggleMainUi()
    {
        this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
    }

    public void ToggleConfigUi()
    {
        this.configWindow.IsOpen = !this.configWindow.IsOpen;
    }

    public void OpenMainUi()
    {
        this.mainWindow.IsOpen = true;
    }

    public void OpenConfigUi()
    {
        this.configWindow.IsOpen = true;
    }

    public void TryTrackCurrentTarget()
    {
        var target = this.targetManager.Target;
        var localPlayer = this.objectTable.LocalPlayer;

        if (target == null ||
            string.IsNullOrWhiteSpace(target.Name.TextValue) ||
            (localPlayer != null && target.GameObjectId == localPlayer.GameObjectId))
        {
            this.FlashTargetButton("Target not found");
            return;
        }

        this.trackedEnemyName = target.Name.TextValue.Trim();
        this.isTrackingEnemy = true;

        if (this.showEndMatchButton)
            this.showEndMatchButton = false;
    }

    public void ClearTrackedEnemy()
    {
        this.trackedEnemyName = string.Empty;
        this.isTrackingEnemy = false;
        this.ResetMatchSession(clearLog: true);
        this.showEndMatchButton = false;
    }

    public void StartMatch()
    {
        if (string.IsNullOrWhiteSpace(this.trackedEnemyName))
        {
            this.FlashStartButton("Target not set!");
            return;
        }

        this.ResetMatchSession(clearLog: true);
        this.matchState = MatchState.WaitingForOpeners;
        this.AddSimpleLine("Waiting for opening rolls (out of 10)...", DeathRollColors.White);
    }

    public void CancelMatch()
    {
        if (!this.CanCancelMatch)
            return;

        this.CompleteSpecialEnd("Match canceled", DeathRollColors.LightRed);
    }

    public void AbortMatch()
    {
        if (!this.CanAbortMatch)
            return;

        this.CompleteSpecialEnd("Match aborted", DeathRollColors.AbortPink);
    }

    public void EndMatch()
    {
        this.ClearTrackedEnemy();
    }

    public float GetSuggestedWindowHeight()
    {
        var lineHeight = 22f;
        var total = 320f;

        foreach (var line in this.logLines)
        {
            if (line.BlankBefore)
                total += lineHeight;

            total += lineHeight;

            if (line.BlankAfter)
                total += lineHeight;
        }

        if (this.showEndMatchButton)
            total += 44f;

        return MathF.Max(620f, total);
    }

    public IReadOnlyList<LogSegment> GetSubtitleSegments()
    {
        if (string.IsNullOrWhiteSpace(this.trackedEnemyName))
        {
            return
            [
                new LogSegment($"{this.LocalPlayerName}, waiting for a enemy", DeathRollColors.Faded),
            ];
        }

        return
        [
            new LogSegment(this.LocalPlayerName, DeathRollColors.Success),
            new LogSegment(" VS ", DeathRollColors.HighlightOrange),
            new LogSegment(this.trackedEnemyName, DeathRollColors.LightRed),
        ];
    }

    private void OnMainCommand(string command, string args)
    {
        this.ToggleMainUi();
    }

    private void OnShortCommand(string command, string args)
    {
        var normalizedArgs = (args ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedArgs is "config" or "settings")
        {
            this.OpenConfigUi();
            return;
        }

        this.OpenMainUi();
    }

    private void OnConfigCommand(string command, string args)
    {
        this.OpenConfigUi();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (string.IsNullOrWhiteSpace(this.trackedEnemyName) && this.isTrackingEnemy)
            this.isTrackingEnemy = false;
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!this.IsMatchRunning || string.IsNullOrWhiteSpace(this.trackedEnemyName))
            return;

        var rawMessage = message.TextValue.Trim();
        if (!this.TryParseRoll(rawMessage, out var roll))
            return;

        switch (this.matchState)
        {
            case MatchState.WaitingForOpeners:
                this.HandleOpeningRoll(roll);
                break;

            case MatchState.WaitingForSelfFirstRoll:
                this.HandleSelfFirstRoll(roll);
                break;

            case MatchState.WaitingForEnemyFirstRoll:
                this.HandleEnemyFirstRoll(roll);
                break;

            case MatchState.WaitingForSelfResponse:
                this.HandleSelfResponse(roll);
                break;

            case MatchState.WaitingForEnemyResponse:
                this.HandleEnemyResponse(roll);
                break;
        }
    }

    private void HandleOpeningRoll(ParsedRoll roll)
    {
        if (roll.Max != 10)
            return;

        this.hasOpeningRollRegistered = true;
        this.AddRollLine(roll);

        if (roll.IsSelf)
            this.selfOpeningRoll = roll.Roll;
        else
            this.enemyOpeningRoll = roll.Roll;

        if (!this.selfOpeningRoll.HasValue || !this.enemyOpeningRoll.HasValue)
            return;

        if (this.selfOpeningRoll.Value == this.enemyOpeningRoll.Value)
        {
            this.AddSimpleLine("Opening roll tied. Waiting for both players to reroll /random 10.", DeathRollColors.Warning);
            this.selfOpeningRoll = null;
            this.enemyOpeningRoll = null;
            return;
        }

        if (this.selfOpeningRoll.Value > this.enemyOpeningRoll.Value)
        {
            this.AddWinnerStartLine(true);
            this.TriggerConfiguredAction(this.Configuration.OpeningWinEmote, this.Configuration.OpeningWinMessage, TimeSpan.Zero);
            this.matchState = MatchState.WaitingForSelfFirstRoll;
            this.QueueSelfRandomCommand("/random");
            return;
        }

        this.AddWinnerStartLine(false);
        this.TriggerConfiguredAction(this.Configuration.OpeningLossEmote, this.Configuration.OpeningLossMessage, TimeSpan.Zero);
        this.matchState = MatchState.WaitingForEnemyFirstRoll;
    }

    private void HandleSelfFirstRoll(ParsedRoll roll)
    {
        if (!roll.IsSelf || roll.Max.HasValue)
            return;

        this.hasMainRoundRollRegistered = true;
        this.AddRollLine(roll);
        this.HandleSelfRollActionTrigger();

        if (roll.Roll == 1)
        {
            this.FinishLoss();
            return;
        }

        this.expectedEnemyMax = roll.Roll;
        this.matchState = MatchState.WaitingForEnemyResponse;
    }

    private void HandleEnemyFirstRoll(ParsedRoll roll)
    {
        if (roll.IsSelf || roll.Max.HasValue)
            return;

        this.hasMainRoundRollRegistered = true;
        this.AddRollLine(roll);

        if (roll.Roll == 1)
        {
            this.FinishWin();
            return;
        }

        this.expectedSelfMax = roll.Roll;
        this.matchState = MatchState.WaitingForSelfResponse;
        this.QueueSelfRandomCommand($"/random {roll.Roll}");
    }

    private void HandleSelfResponse(ParsedRoll roll)
    {
        if (!roll.IsSelf)
            return;

        if (!this.IsAcceptableResponseMax(roll.Max, this.expectedSelfMax))
            return;

        this.hasMainRoundRollRegistered = true;
        this.AddRollLine(roll);
        this.HandleSelfRollActionTrigger();

        if (roll.Roll == 1)
        {
            this.FinishLoss();
            return;
        }

        this.expectedEnemyMax = roll.Roll;
        this.matchState = MatchState.WaitingForEnemyResponse;
    }

    private void HandleEnemyResponse(ParsedRoll roll)
    {
        if (roll.IsSelf)
            return;

        if (!this.IsAcceptableResponseMax(roll.Max, this.expectedEnemyMax))
            return;

        this.hasMainRoundRollRegistered = true;
        this.AddRollLine(roll);

        if (roll.Roll == 1)
        {
            this.FinishWin();
            return;
        }

        this.expectedSelfMax = roll.Roll;
        this.matchState = MatchState.WaitingForSelfResponse;
        this.QueueSelfRandomCommand($"/random {roll.Roll}");
    }

    private void HandleSelfRollActionTrigger()
    {
        this.selfMainRollCount++;

        var emote = this.Configuration.GetRollEmote(this.selfMainRollCount);
        var message = this.Configuration.GetRollMessage(this.selfMainRollCount);

        var delay = TimeSpan.Zero;
        if (this.selfMainRollCount != 2 && this.lastAutoSelfCommandDueUtc.HasValue)
        {
            var targetTime = this.lastAutoSelfCommandDueUtc.Value.AddSeconds(1);
            if (targetTime > DateTime.UtcNow)
                delay = targetTime - DateTime.UtcNow;
        }

        this.TriggerConfiguredAction(emote, message, delay);
    }

    private bool TryParseRoll(string rawMessage, out ParsedRoll roll)
    {
        var selfMatch = SelfRollRegex.Match(rawMessage);
        if (selfMatch.Success)
        {
            roll = new ParsedRoll(
                true,
                this.LocalPlayerName,
                int.Parse(selfMatch.Groups["roll"].Value),
                selfMatch.Groups["max"].Success
                    ? int.Parse(selfMatch.Groups["max"].Value)
                    : null);

            return true;
        }

        var otherMatch = OtherRollRegex.Match(rawMessage);
        if (otherMatch.Success)
        {
            var actorName = otherMatch.Groups["name"].Value.Trim();
            if (!actorName.Equals(this.trackedEnemyName, StringComparison.OrdinalIgnoreCase))
            {
                roll = default;
                return false;
            }

            roll = new ParsedRoll(
                false,
                actorName,
                int.Parse(otherMatch.Groups["roll"].Value),
                otherMatch.Groups["max"].Success
                    ? int.Parse(otherMatch.Groups["max"].Value)
                    : null);

            return true;
        }

        roll = default;
        return false;
    }

    private void QueueSelfRandomCommand(string command)
    {
        var delay = TimeSpan.FromMilliseconds(120);
        this.lastAutoSelfCommandDueUtc = DateTime.UtcNow.Add(delay);
        this.QueueGameCommand(command, delay);
    }

    private void QueueGameCommand(string command, TimeSpan delay)
    {
        _ = this.framework.RunOnTick(
            () => this.ExecuteGameTextCommandNative(command),
            delay: delay);
    }

    private void TriggerConfiguredAction(string? emoteOrCommand, string? sayMessage, TimeSpan emoteDelay)
    {
        var normalizedEmote = this.NormalizeCommand(emoteOrCommand);
        var normalizedMessage = this.NormalizeSayMessage(sayMessage);

        if (!string.IsNullOrWhiteSpace(normalizedEmote))
            this.QueueGameCommand(normalizedEmote, emoteDelay);

        if (!string.IsNullOrWhiteSpace(normalizedMessage))
            this.QueueGameCommand(normalizedMessage, emoteDelay + TimeSpan.FromSeconds(1.5));
    }

    private string? NormalizeCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var trimmed = command.Trim();
        return trimmed.StartsWith("/") ? trimmed : $"/{trimmed}";
    }

    private string? NormalizeSayMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        var trimmed = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : $"/say {trimmed}";
    }

    private unsafe void ExecuteGameTextCommandNative(string command)
    {
        var shellModule = RaptureShellModule.Instance();
        var uiModule = UIModule.Instance();

        if (shellModule == null || uiModule == null)
            return;

        Utf8String* commandString = null;

        try
        {
            commandString = Utf8String.FromString(command);
            shellModule->ExecuteCommandInner(commandString, uiModule);
        }
        finally
        {
            if (commandString != null)
                commandString->Dtor(true);
        }
    }

    private void FinishWin()
    {
        this.AddFinalResultLine("You Win!", DeathRollColors.Success);
        this.TriggerConfiguredAction(this.Configuration.EndWinEmote, this.Configuration.EndWinMessage, TimeSpan.Zero);
        this.matchState = MatchState.Finished;
        this.showEndMatchButton = true;
    }

    private void FinishLoss()
    {
        this.AddFinalResultLine("You Lost!", DeathRollColors.LightRed);
        this.TriggerConfiguredAction(this.Configuration.EndLossEmote, this.Configuration.EndLossMessage, TimeSpan.Zero);
        this.matchState = MatchState.Finished;
        this.showEndMatchButton = true;
    }

    private void CompleteSpecialEnd(string text, Vector4 color)
    {
        this.AddFinalResultLine(text, color);
        this.matchState = MatchState.Finished;
        this.showEndMatchButton = true;
    }

    private void AddWinnerStartLine(bool selfStarts)
    {
        if (selfStarts)
        {
            this.AddLine(new LogLine(
            [
                new LogSegment(this.LocalPlayerName, DeathRollColors.Success),
                new LogSegment(" won and now starts the round!", DeathRollColors.White),
            ], blankBefore: true, blankAfter: true));

            return;
        }

        this.AddLine(new LogLine(
        [
            new LogSegment(this.trackedEnemyName, DeathRollColors.PrimaryBlue),
            new LogSegment(" won and now starts the round!", DeathRollColors.White),
        ], blankBefore: true, blankAfter: true));
    }

    private void AddRollLine(ParsedRoll roll)
    {
        var nameColor = roll.IsSelf ? DeathRollColors.Success : DeathRollColors.PrimaryBlue;
        var rollColor = roll.IsSelf ? DeathRollColors.HighlightOrange : DeathRollColors.PrimaryBlue;
        var segments = new List<LogSegment>
        {
            new($"{roll.ActorName}:", nameColor),
            new(" rolled ", DeathRollColors.White),
            new(roll.Roll.ToString(), rollColor),
        };

        if (roll.Max.HasValue)
        {
            var maxColor = roll.IsSelf ? DeathRollColors.HighlightOrange : DeathRollColors.PrimaryBlue;
            segments.Add(new(" (out of ", DeathRollColors.HighlightOrange, IsItalic: true));
            segments.Add(new(roll.Max.Value.ToString(), maxColor, IsItalic: true));
            segments.Add(new(")", DeathRollColors.HighlightOrange, IsItalic: true));
        }

        this.AddLine(new LogLine(segments));
    }

    private void AddSimpleLine(string text, Vector4 color)
    {
        this.AddLine(new LogLine(
        [
            new LogSegment(text, color),
        ]));
    }

    private void AddFinalResultLine(string text, Vector4 color)
    {
        this.AddLine(new LogLine(
        [
            new LogSegment(text, color, IsBold: true),
        ], centered: true, blankBefore: true));
    }

    private void AddLine(LogLine line)
    {
        this.logLines.Add(line);

        if (this.logLines.Count > 500)
            this.logLines.RemoveAt(0);
    }

    private void FlashTargetButton(string text)
    {
        this.targetButtonOverrideText = text;
        this.targetButtonOverrideUntilUtc = DateTime.UtcNow.AddSeconds(2);
    }

    private void FlashStartButton(string text)
    {
        this.startButtonOverrideText = text;
        this.startButtonOverrideUntilUtc = DateTime.UtcNow.AddSeconds(2);
    }

    private void ResetMatchSession(bool clearLog)
    {
        this.matchState = MatchState.Idle;
        this.selfOpeningRoll = null;
        this.enemyOpeningRoll = null;
        this.expectedSelfMax = null;
        this.expectedEnemyMax = null;
        this.hasOpeningRollRegistered = false;
        this.hasMainRoundRollRegistered = false;
        this.showEndMatchButton = false;
        this.selfMainRollCount = 0;
        this.lastAutoSelfCommandDueUtc = null;

        if (clearLog)
            this.logLines.Clear();
    }

    private bool IsAcceptableResponseMax(int? actual, int? expected)
    {
        if (!expected.HasValue)
            return true;

        if (!actual.HasValue)
            return true;

        return actual.Value == expected.Value;
    }
}

public enum MatchState
{
    Idle,
    WaitingForOpeners,
    WaitingForSelfFirstRoll,
    WaitingForEnemyFirstRoll,
    WaitingForSelfResponse,
    WaitingForEnemyResponse,
    Finished,
}

public readonly record struct ParsedRoll(
    bool IsSelf,
    string ActorName,
    int Roll,
    int? Max);

public readonly record struct LogSegment(
    string Text,
    Vector4 Color,
    bool IsBold = false,
    bool IsItalic = false);

public sealed class LogLine
{
    public LogLine(IEnumerable<LogSegment> segments, bool centered = false, bool blankBefore = false, bool blankAfter = false)
    {
        this.Segments = new List<LogSegment>(segments);
        this.Centered = centered;
        this.BlankBefore = blankBefore;
        this.BlankAfter = blankAfter;
    }

    public List<LogSegment> Segments { get; }
    public bool Centered { get; }
    public bool BlankBefore { get; }
    public bool BlankAfter { get; }
}

public static class DeathRollColors
{
    public static readonly Vector4 PrimaryBlue = new(0.4118f, 0.6353f, 1.0000f, 1.0000f);       // #69a2ff
    public static readonly Vector4 MatchGreen = new(0.0980f, 0.6588f, 0.0000f, 1.0000f);        // #19a800
    public static readonly Vector4 Success = new(0.3600f, 0.9000f, 0.4300f, 1.0000f);
    public static readonly Vector4 Danger = new(0.8500f, 0.2200f, 0.2200f, 1.0000f);
    public static readonly Vector4 LightRed = new(1.0000f, 0.5800f, 0.5800f, 1.0000f);
    public static readonly Vector4 Warning = new(1.0000f, 0.8200f, 0.4200f, 1.0000f);
    public static readonly Vector4 White = new(1.0000f, 1.0000f, 1.0000f, 1.0000f);
    public static readonly Vector4 Faded = new(1.0000f, 1.0000f, 1.0000f, 0.5500f);
    public static readonly Vector4 HighlightOrange = new(1.0000f, 0.6314f, 0.1490f, 1.0000f);   // #ffa126
    public static readonly Vector4 CancelRed = new(0.9686f, 0.3176f, 0.2039f, 1.0000f);         // #f75134
    public static readonly Vector4 AbortPink = new(0.9098f, 0.2471f, 0.4353f, 1.0000f);         // #e83f6f
    public static readonly Vector4 Gold = new(0.9569f, 0.8314f, 0.0000f, 1.0000f);              // #d49400
    public static readonly Vector4 TestGreen = new(0.1294f, 0.5490f, 0.0000f, 1.0000f);         // #218c00
    public static readonly Vector4 HelpPurple = new(0.7765f, 0.2471f, 0.9098f, 1.0000f);        // #c63fe8
}
