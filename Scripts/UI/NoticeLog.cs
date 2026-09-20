using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// The world's running commentary. Only the newest line or two show on
// screen, briefly, under the calendar; the whole history lives in the
// Log tab of your book (J / L).
public partial class NoticeLog : Control
{
    public static readonly List<(string when, string text)> History = new();
    private const int HistoryMax = 400;

    private readonly List<(Label label, float age)> _lines = new();
    private VBoxContainer _box;
    private const int MaxLines = 2;
    private const float LifeSeconds = 9f;

    public override void _Ready()
    {
        _box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _box.Position = new Vector2(16, 96);
        _box.AddThemeConstantOverride("separation", 1);
        AddChild(_box);
        Sim.OnNotice += Add;
    }

    public override void _ExitTree() => Sim.OnNotice -= Add;

    private void Add(string text)
    {
        var cal = GameCalendar.Instance;
        History.Add((cal != null ? $"{cal.Day} {cal.DateString.Split(' ')[1].Substring(0, 3)} {cal.TimeString}" : "", text));
        if (History.Count > HistoryMax) History.RemoveAt(0);

        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
        l.AddThemeFontSizeOverride("font_size", 12);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _box.AddChild(l);
        _lines.Add((l, 0f));
        while (_lines.Count > MaxLines) { _lines[0].label.QueueFree(); _lines.RemoveAt(0); }
    }

    public override void _Process(double delta)
    {
        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            var (l, age) = _lines[i];
            age += (float)delta;
            if (age > LifeSeconds) { l.QueueFree(); _lines.RemoveAt(i); continue; }
            l.Modulate = new Color(1, 1, 1, Mathf.Clamp((LifeSeconds - age) / 3f, 0f, 1f));
            _lines[i] = (l, age);
        }
    }
}
