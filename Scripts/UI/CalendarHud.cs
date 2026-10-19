using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

public partial class CalendarHud : Label
{
    private string _lastEvent = "";
    private float _eventTimer;

    // The hand on the clock, under the date: slower, stop/start, faster, and
    // how fast it runs against the usual pace. The same as the clock keys -
    // the host's clock, which every player's follows (Net.TogglePause,
    // Net.ChangeSpeed) - so only the host's buttons work.
    private HBoxContainer _clock;
    private Button _slower, _pause, _faster;
    private Label _pace;

    public override void _Ready()
    {
        var cal = GameCalendar.Instance;
        if (cal != null)
        {
            cal.CalendarEvent += name =>
            {
                _lastEvent = name switch
                {
                    "ShipArrives" => "The ship is in from England",
                    "Brigade" => "The Nor'Westers' brigade is in from Fort William",
                    "ShipDeparts" => "The ship has sailed",
                    "FreezeUp" => "The bay has frozen",
                    "Breakup" => "The ice is going out",
                    "SummerBisonHunt" => "The summer hunt is out on the plains",
                    "FallBisonHunt" => "The fall hunt is out on the plains",
                    _ => name,
                };
                _eventTimer = 8f;
            };
        }
        BuildClock();
    }

    private void BuildClock()
    {
        _clock = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _clock.AddThemeConstantOverride("separation", 4);
        AddChild(_clock);
        _slower = ClockButton("<<", () => Net.Net.I?.ChangeSpeed(2f));
        _pause = ClockButton("||", () => Net.Net.I?.TogglePause());
        _faster = ClockButton(">>", () => Net.Net.I?.ChangeSpeed(0.5f));
        _pace = new Label { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Pass };
        _pace.AddThemeFontSizeOverride("font_size", 14);
        _pace.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.85f));
        _pace.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        _clock.AddChild(_pace);
    }

    private Button ClockButton(string text, System.Action pressed)
    {
        // No keyboard focus: Space and Enter belong to the game, not to the
        // last button clicked.
        var b = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(30, 22) };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Pressed += pressed;
        _clock.AddChild(b);
        return b;
    }

    public override void _Process(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;

        Text = $"{cal.DateString}   {cal.TimeString}\n{cal.ChapterName} - {cal.Weather.Describe()}";

        if (_eventTimer > 0f)
        {
            _eventTimer -= (float)delta;
            // The blank line is where the clock's buttons sit.
            Text += $"\n\n{_lastEvent}";
        }
        UpdateClock(cal);
    }

    // What the clock row last showed; it is only rewritten when this changes
    // (building its strings every frame was garbage for the collector).
    private (bool paused, bool host, bool held, float secondsPerHour, int keys) _shown = (false, false, false, -1f, -1);

    private void UpdateClock(GameCalendar cal)
    {
        if (_clock == null) return;
        _clock.Position = new Vector2(0, GetLineHeight() * 2 + 4);

        var now = (Net.Net.HostPaused, Net.Net.IsServer, Input.IsActionPressed("time_fast"), cal.SecondsPerGameHour, Keybinds.Version);
        if (now == _shown) return;
        _shown = now;
        var (paused, host, held, secondsPerHour, _) = now;

        float pace = GameCalendar.NormalSecondsPerGameHour / secondsPerHour;
        if (paused) _pace.Text = "Paused";
        else if (held) _pace.Text = $"x{pace * cal.FastForwardMultiplier:0.#} (held)";
        else _pace.Text = pace >= 1f ? $"x{pace:0.#}" : $"x{pace:0.##}";
        _pause.Text = paused ? ">" : "||";

        // At the ends of the host's range (Net.ChangeSpeed: 0.5 to 120 s an
        // hour) a button would do nothing more.
        _slower.Disabled = !host || secondsPerHour >= 120f;
        _pause.Disabled = !host;
        _faster.Disabled = !host || secondsPerHour <= 0.5f;

        string who = host ? "" : "\n(Only the host can change the clock.)";
        _slower.TooltipText = $"Slower clock ({Keybinds.KeyFor("clock_slower")}){who}";
        _pause.TooltipText = (paused ? "Start the clock" : "Stop the clock") + $" ({Keybinds.KeyFor("pause_clock")}){who}";
        _faster.TooltipText = $"Faster clock ({Keybinds.KeyFor("clock_faster")}){who}";
        _pace.TooltipText = $"How fast time runs against the usual pace ({GameCalendar.NormalSecondsPerGameHour:0} s to the hour). Hold {Keybinds.KeyFor("time_fast")} to fast-forward.";
    }
}
