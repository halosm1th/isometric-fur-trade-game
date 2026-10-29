using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// What a deed earned, where you can see it (design doc P.5): the player's
// standing with the house and his skills, watched on his own machine, and
// any change drawn as a line rising over his head - "+2 standing", "Trapping
// improves". Works off what the server already sends, so joined players see
// their own.
public partial class RewardPopups : Control
{
    private PlayerController _player;
    private float _standing, _settle;
    private float[] _skills = new float[4];
    private float _pendingStanding, _pendingFor;

    private sealed class Pop { public string Text; public Color Col; public float Age; public float Offset; }
    private readonly List<Pop> _pops = new();
    private const float Life = 2.8f;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Sim.LocalPlayerChanged += p => { _player = p; _settle = 2f; };
        _player = Sim.Player as PlayerController;
        _settle = 2f;
    }

    private void Baseline()
    {
        _standing = _player.Standing;
        _skills = new[] { _player.Skills.Hunting, _player.Skills.Trapping, _player.Skills.Trade, _player.Skills.Woodcraft };
        _pendingStanding = 0f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _pops.Count - 1; i >= 0; i--) { _pops[i].Age += dt; if (_pops[i].Age > Life) _pops.RemoveAt(i); }
        if (_player == null || !IsInstanceValid(_player)) { _player = Sim.Player as PlayerController; QueueRedraw(); return; }
        // A load, a new character, a death: everything jumps at once and none of it was earned.
        if (_settle > 0f || _player.IsDead) { _settle -= dt; Baseline(); QueueRedraw(); return; }

        // Standing: small steps over a moment are gathered into one line.
        float ds = _player.Standing - _standing;
        _standing = _player.Standing;
        if (Mathf.Abs(ds) > 0.001f) { _pendingStanding += ds; _pendingFor = 0.4f; }
        if (_pendingFor > 0f && (_pendingFor -= dt) <= 0f)
        {
            if (Mathf.Abs(_pendingStanding) >= 0.25f) Add(Signed(_pendingStanding) + " standing", _pendingStanding > 0 ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.5f, 0.4f), _pendingStanding > 0);
            _pendingStanding = 0f;
        }

        var now = new[] { _player.Skills.Hunting, _player.Skills.Trapping, _player.Skills.Trade, _player.Skills.Woodcraft };
        for (int i = 0; i < 4; i++)
        {
            // Skills creep a hair at a time; show each whole step of 0.01.
            if (Mathf.FloorToInt(now[i] * 100f) > Mathf.FloorToInt(_skills[i] * 100f) && now[i] - _skills[i] < 0.2f)
                Add($"{Agents.Skills.Names[i]} improves", new Color(0.6f, 0.9f, 1f), true);
            _skills[i] = now[i];
        }
        QueueRedraw();
    }

    private static string Signed(float v)
    {
        float r = Mathf.Round(v * 2f) / 2f;
        if (r == 0f) r = v > 0 ? 0.5f : -0.5f;
        string n = r == Mathf.Floor(r) ? $"{Mathf.Abs(r):0}" : $"{Mathf.Abs(r):0.#}";
        return (r > 0 ? "+" : "-") + n;
    }

    private void Add(string text, Color col, bool good)
    {
        float offset = 0f;
        foreach (var p in _pops) if (p.Age < 0.8f) offset += 22f;
        _pops.Add(new Pop { Text = text, Col = col, Offset = offset });
        if (good) Audio.Play("ui_quill_write", -14f, 0.1f, Audio.EffectsBus);
    }

    public override void _Draw()
    {
        if (_pops.Count == 0 || _player == null || !IsInstanceValid(_player)) return;
        var xf = GetViewport().GetCanvasTransform();
        var head = xf * _player.GlobalPosition + new Vector2(0, -110);
        var font = ThemeDB.FallbackFont;
        foreach (var p in _pops)
        {
            float t = p.Age / Life;
            float a = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            var at = head + new Vector2(-120, -p.Offset - 40f * t);
            DrawString(font, at + new Vector2(1.5f, 1.5f), p.Text, HorizontalAlignment.Center, 240, 17, new Color(0, 0, 0, 0.75f * a));
            DrawString(font, at, p.Text, HorizontalAlignment.Center, 240, 17, new Color(p.Col, a));
        }
    }
}
