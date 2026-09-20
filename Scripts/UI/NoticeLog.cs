using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// The world's running commentary. Nothing is flashed on screen; the
// history lives in the Log tab of your book (L) and in the F1 console.
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

        // Nothing on screen: the world's news is read in the Log (L) or the F1 console.
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
