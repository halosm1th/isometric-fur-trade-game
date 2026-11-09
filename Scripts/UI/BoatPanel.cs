using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// The boats page (K): your boats and beds, and the boats about you, each
// with her hull part by part, where she leaks, the water in her, her load,
// and - on the bed - how far along she is and what she still wants.
// Client side: it reads the boats as this machine has them (the host sends
// each player the boats near him in full: Net.BoatsRpc).
public partial class BoatPanel : Control
{
    public static BoatPanel Instance { get; private set; }
    public static bool IsOpen => Instance != null && IsInstanceValid(Instance) && Instance.Visible;

    private readonly List<Canoe> _list = new();
    private int _sel;
    private float _refresh;

    public override void _Ready()
    {
        Instance = this;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        // Dev: `--boatcount=S` prints the boats this machine knows of after S seconds (a client's view).
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--boatcount=") && double.TryParse(arg.Substring(12), out double when))
                GetTree().CreateTimer(when).Timeout += () =>
                {
                    var bits = new List<string>();
                    if (Sim.Spawner != null) foreach (var c in Sim.Spawner.Canoes) bits.Add($"{c.Index}:{c.Kind}/{c.Stage}{(c.Owner != "" ? "/" + c.Owner : "")}{(c.Leaks.Count > 0 ? $"/{c.Leaks.Count}leaks" : "")}");
                    GD.Print($"[boats] this machine knows {bits.Count} boats: {string.Join(", ", bits)}");
                };
        // Dev: `--boats` opens this page a few seconds in; `--boats=S` after S seconds.
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == "--boats" || arg.StartsWith("--boats=")) GetTree().CreateTimer(arg.Length > 8 && double.TryParse(arg.Substring(8), out double at) ? at : 3.0).Timeout += Toggle;
    }

    public void Toggle()
    {
        Visible = !Visible;
        MouseFilter = Visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (Visible) { Gather(); _sel = 0; }
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("boats") && !(UI.DialogueMenu.Instance?.IsOpen ?? false) && !HandWork.IsOpen && !(UI.ChatBox.Instance?.IsTyping ?? false)) { Toggle(); GetViewport().SetInputAsHandled(); return; }
        if (!Visible) return;
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("pause_menu")) { Toggle(); GetViewport().SetInputAsHandled(); return; }
        if (e.IsActionPressed("ui_down") || e.IsActionPressed("move_down")) { _sel = Mathf.Min(_sel + 1, Mathf.Max(0, _list.Count - 1)); QueueRedraw(); GetViewport().SetInputAsHandled(); }
        if (e.IsActionPressed("ui_up") || e.IsActionPressed("move_up")) { _sel = Mathf.Max(0, _sel - 1); QueueRedraw(); GetViewport().SetInputAsHandled(); }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].HasPoint(mb.Position)) { _sel = i; QueueRedraw(); }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        if ((_refresh -= (float)delta) <= 0f) { _refresh = 0.5f; Gather(); QueueRedraw(); }
    }

    // Yours first (afloat, then on the bed), then the boats about you, nearest first.
    private void Gather()
    {
        var me = Sim.Player as PlayerController;
        var keep = _sel >= 0 && _sel < _list.Count ? _list[_sel] : null;
        _list.Clear();
        if (me == null || Sim.Spawner == null) return;
        var mine = new List<Canoe>(); var near = new List<Canoe>();
        foreach (var c in Sim.Spawner.Canoes)
        {
            if (!IsInstanceValid(c) || c.Wrecked) continue;
            bool own = c.Owner == me.DisplayName || c.CommissionedBy == me.DisplayName || c.RiderNetId == me.NetId;
            if (own) mine.Add(c);
            else if (c.GlobalPosition.DistanceTo(me.GlobalPosition) < 2600f) near.Add(c);
        }
        mine.Sort((a, b) => a.Building.CompareTo(b.Building));
        near.Sort((a, b) => a.GlobalPosition.DistanceTo(me.GlobalPosition).CompareTo(b.GlobalPosition.DistanceTo(me.GlobalPosition)));
        _list.AddRange(mine); _list.AddRange(near);
        if (_list.Count > 14) _list.RemoveRange(14, _list.Count - 14);
        if (keep != null && _list.IndexOf(keep) is var k && k >= 0) _sel = k;
        _sel = Mathf.Clamp(_sel, 0, Mathf.Max(0, _list.Count - 1));
    }

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private readonly List<Rect2> _rows = new();
    private static readonly Color Ink = new(0.22f, 0.13f, 0.05f), Faint = new(0.4f, 0.3f, 0.2f), Red = new(0.65f, 0.15f, 0.08f), Green = new(0.25f, 0.5f, 0.15f);

    public override void _Draw()
    {
        if (!Visible) return;
        var font = ThemeDB.FallbackFont;
        var vp = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0f, 0f, 0f, 0.4f));
        float w = Mathf.Min(980f, vp.X - 40f), h = Mathf.Min(600f, vp.Y - 40f);
        var panel = new Rect2((vp.X - w) / 2f, (vp.Y - h) / 2f, w, h);
        DrawRect(panel, new Color(0.92f, 0.86f, 0.72f));
        DrawRect(panel, new Color(0.35f, 0.24f, 0.12f), false, 3f);
        DrawString(font, panel.Position + new Vector2(18, 30), "Boats", HorizontalAlignment.Left, -1, 22, Ink);
        DrawString(font, new Vector2(panel.End.X - 330, panel.Position.Y + 28), $"{Keybinds.KeyFor("boats")} or Esc: close    up/down: choose", HorizontalAlignment.Left, -1, 12, Faint);
        _rows.Clear();
        var me = Sim.Player as PlayerController;
        if (_list.Count == 0)
        {
            DrawMultilineString(font, panel.Position + new Vector2(18, 70), "No boat of yours, and none near.\n\nBuy or commission a canoe from a camp that builds them (talk to them: \"Canoes and boats...\"), or build your own: with a crooked knife in hand, use it by the water to gather bark, watap, gum and cedar, and to lay out a bed.", HorizontalAlignment.Left, w - 36, 14, 8, Ink);
            return;
        }
        // The list.
        float lx = panel.Position.X + 14, ly = panel.Position.Y + 52, lw = 280f;
        for (int i = 0; i < _list.Count; i++)
        {
            var c = _list[i];
            var row = new Rect2(lx, ly + i * 36f, lw, 32f);
            _rows.Add(row);
            DrawRect(row, i == _sel ? new Color(0.8f, 0.68f, 0.45f) : new Color(0.86f, 0.78f, 0.6f));
            bool own = me != null && (c.Owner == me.DisplayName || c.CommissionedBy == me.DisplayName);
            string name = c.BoatName != "" ? Cap(c.BoatName) : c.Building ? $"{Cap(c.Spec.Name)} (on the bed)" : Cap(c.Spec.Name);
            DrawString(font, row.Position + new Vector2(8, 14), name, HorizontalAlignment.Left, lw - 16, 13, Ink);
            string sub = c.Building ? $"{c.Stage}/{c.Spec.Stages.Length} stages {c.BuiltAt}" : $"{(own ? "yours" : c.Owner != "" ? c.Owner + "'s" : "nobody's")} - {Boats.Condition(c.Condition)}{(c.Leaks.Count > 0 ? $", {c.Leaks.Count} leak{(c.Leaks.Count == 1 ? "" : "s")}" : "")}";
            DrawString(font, row.Position + new Vector2(8, 28), sub, HorizontalAlignment.Left, lw - 16, 11, Faint);
        }
        if (_sel >= _list.Count) return;
        var b = _list[_sel];
        var area = new Rect2(panel.Position.X + lw + 34, panel.Position.Y + 52, w - lw - 52, h - 70);
        if (b.Building) DrawBed(b, area, font, me); else DrawBoat(b, area, font, me);
    }

    private void DrawBoat(Canoe c, Rect2 a, Font font, PlayerController me)
    {
        float y = a.Position.Y;
        string title = c.BoatName != "" ? $"{Cap(c.BoatName)}, a {c.Spec.Name}" : $"A {c.Spec.Name}";
        DrawString(font, new Vector2(a.Position.X, y + 16), title, HorizontalAlignment.Left, a.Size.X, 18, Ink); y += 26;
        string who = c.Owner == "" ? "Nobody's" : me != null && c.Owner == me.DisplayName ? "Yours" : $"{c.Owner}'s";
        string built = c.Builders != "" ? $"Built {c.BuiltAt} by {c.Builders}: {Boats.Grade(c.Quality)} work." : "";
        DrawMultilineString(font, new Vector2(a.Position.X, y + 12), $"{who}. {built}", HorizontalAlignment.Left, a.Size.X, 12, 2, Faint); y += 36;
        // The hull from the side, with her leaks.
        var hull = new Rect2(a.Position.X, y, a.Size.X * 0.62f, 110f);
        DrawHull(hull, c);
        // The water in her.
        Bar(new Rect2(hull.End.X + 20, y + 10, a.Size.X - hull.Size.X - 30, 12), c.Bilge, new Color(0.3f, 0.45f, 0.65f), $"Water in her: {(c.Bilge < 0.05f ? "dry" : c.Bilge < 0.3f ? "a little" : c.Bilge < 0.6f ? "a good deal" : "near swamped")}", font);
        float leak = c.LeakRate;
        DrawString(font, new Vector2(hull.End.X + 20, y + 50), leak < 0.0005f ? "Tight." : $"Takes in water {(leak < 0.003f ? "slowly" : leak < 0.008f ? "steadily" : "fast")}.", HorizontalAlignment.Left, -1, 12, leak < 0.003f ? Faint : Red);
        DrawString(font, new Vector2(hull.End.X + 20, y + 70), $"{c.Leaks.Count} leak{(c.Leaks.Count == 1 ? "" : "s")}", HorizontalAlignment.Left, -1, 12, Faint);
        y += 124;
        // Her parts.
        for (int i = 0; i < 4; i++)
        {
            var part = (HullPart)i;
            float v = c.Part(part);
            var col = v >= 0.65f ? Green : v >= 0.35f ? new Color(0.7f, 0.5f, 0.1f) : Red;
            Bar(new Rect2(a.Position.X, y + 16, 220, 12), v, col, $"{Boats.PartName(part, c.Spec.Planked)}: {Boats.Condition(v)}", font);
            DrawString(font, new Vector2(a.Position.X + 236, y + 27), PartNote(c, part), HorizontalAlignment.Left, a.Size.X - 240, 11, Faint);
            y += 34;
        }
        y += 6;
        // Load and going.
        Bar(new Rect2(a.Position.X, y + 16, 220, 12), c.LoadLbs / Mathf.Max(1f, c.CapacityLbs), c.Overloaded ? Red : new Color(0.55f, 0.4f, 0.2f), $"Load: {c.LoadLbs:0} of {c.CapacityLbs:0} lb", font);
        DrawString(font, new Vector2(a.Position.X + 236, y + 27), $"Pace {c.SpeedFactor * 100f:0}% of a sound {c.Spec.Short}'s best. {c.Spec.Seats} to paddle her, {c.Spec.Carriers} to carry her.", HorizontalAlignment.Left, a.Size.X - 240, 11, Faint);
        y += 40;
        if (c.Cargo.SlotCount > 0) { DrawMultilineString(font, new Vector2(a.Position.X, y + 12), "In her: " + c.Cargo.Summary(), HorizontalAlignment.Left, a.Size.X, 12, 2, Ink); y += 34; }
        if (c.Stock.SlotCount > 0) { DrawMultilineString(font, new Vector2(a.Position.X, y + 12), "Mending stuff kept in her: " + c.Stock.Summary(), HorizontalAlignment.Left, a.Size.X, 12, 2, Ink); y += 34; }
        var mends = Boatbuilding.Mends(c);
        if (mends.Count > 0)
        {
            var words = new List<string>(); foreach (var m in mends) words.Add(m.Label.ToLower());
            DrawMultilineString(font, new Vector2(a.Position.X, y + 12), "She wants: " + string.Join("; ", words) + ". (At the bank, E on her.)", HorizontalAlignment.Left, a.Size.X, 12, 3, Red);
        }
    }

    private static string PartNote(Canoe c, HullPart p) => p switch
    {
        HullPart.Skin => c.Part(p) < 0.35f ? "One more rock and she's through." : c.Spec.Planked ? "Planks split on rocks; a new plank and nails mend it." : "Torn on rocks and shallows; a bark patch mends it.",
        HullPart.Seams => c.Part(p) < 0.5f ? "Dried and cracking: she'll leak more every day." : c.Spec.Planked ? "Oakum and pitch, re-done when she weeps." : "Gum dries in the sun and cracks in the cold; gum her often.",
        HullPart.Ribs => c.Part(p) < 0.35f ? "Cracked ribs: she carries less and flexes." : "Cracked by rocks and bad landings; new cedar mends them.",
        _ => c.Part(p) < 0.35f ? "Working loose: she carries less." : "Strained by long portages; re-lash with watap.",
    };

    private void DrawBed(Canoe c, Rect2 a, Font font, PlayerController me)
    {
        float y = a.Position.Y;
        DrawString(font, new Vector2(a.Position.X, y + 16), $"A {c.Spec.Name} on her bed", HorizontalAlignment.Left, a.Size.X, 18, Ink); y += 24;
        var yard = Boatwrights.YardOf(c);
        string whose = yard != null ? $"The {yard.Name} people's work{(c.CommissionedBy != "" ? $", for {c.CommissionedBy}" : "")}." : me != null && c.Owner == me.DisplayName ? "Yours." : $"{c.Owner}'s.";
        DrawString(font, new Vector2(a.Position.X, y + 14), $"{whose} {c.BuiltAt}.", HorizontalAlignment.Left, a.Size.X, 12, Faint); y += 30;
        var hull = new Rect2(a.Position.X, y, a.Size.X * 0.62f, 100f);
        DrawHull(hull, c, (float)c.Stage / c.Spec.Stages.Length);
        y += 112;
        for (int i = 0; i < c.Spec.Stages.Length; i++)
        {
            var s = c.Spec.Stages[i];
            bool done = i < c.Stage, now = i == c.Stage;
            var col = done ? Green : now ? Ink : Faint;
            string mark = done ? $"{Boats.Grade(c.StageScores[i])}" : now ? (c.WorkHours > 0f ? $"under way, {c.WorkHours:0} of {c.Spec.Hours[i]:0} h" : "next") : "";
            DrawString(font, new Vector2(a.Position.X, y + 14), $"{(done ? "x" : now ? ">" : "-")}  {Boats.StageName(s)}", HorizontalAlignment.Left, 300, 13, col);
            DrawString(font, new Vector2(a.Position.X + 310, y + 14), mark, HorizontalAlignment.Left, a.Size.X - 310, 12, col);
            if (now)
            {
                var needs = new List<string>();
                foreach (var (item, n) in c.Spec.Needs[i]) needs.Add($"{Items.Items.Lot(item, n)} ({c.Stock.Count(item)} laid by)");
                string tool = Boats.ToolFor(s) is { } t ? $"; {t.Why}" : "";
                string fire = Boats.NeedsFire(s) ? "; a fire near the bed" : "";
                DrawMultilineString(font, new Vector2(a.Position.X + 18, y + 32), $"Wants {string.Join(", ", needs)}{tool}{fire}.", HorizontalAlignment.Left, a.Size.X - 20, 11, 2, Faint);
                y += 30;
            }
            y += 24;
        }
        if (c.Leaks.Count > 0) DrawString(font, new Vector2(a.Position.X, y + 20), $"{c.Leaks.Count} weak place{(c.Leaks.Count == 1 ? "" : "s")} in the seams so far: the gum must cover them.", HorizontalAlignment.Left, a.Size.X, 12, Red);
    }

    private void DrawHull(Rect2 r, Canoe c, float built = 1f)
    {
        bool planked = c.Spec.Planked;
        var pts = new List<Vector2>();
        float cx = r.Position.X + r.Size.X / 2f, top = r.Position.Y + r.Size.Y * 0.3f, len = r.Size.X * 0.46f, depth = r.Size.Y * 0.55f;
        for (int i = 0; i <= 24; i++) { float t = i / 24f; pts.Add(new Vector2(cx - len + 2f * len * t, top - (planked ? 0.1f : 0.35f) * depth * Mathf.Pow(Mathf.Abs(2f * t - 1f), 4f))); }
        for (int i = 24; i >= 0; i--) { float t = i / 24f; pts.Add(new Vector2(cx - len + 2f * len * t, top + depth * Mathf.Pow(Mathf.Sin(t * Mathf.Pi), planked ? 0.5f : 0.8f))); }
        float worn = c.Building ? 1f : Mathf.Lerp(0.7f, 1f, c.Part(HullPart.Skin));
        var fill = planked ? new Color(0.62f * worn, 0.45f * worn, 0.28f * worn) : new Color(0.93f * worn, 0.9f * worn, 0.8f * worn);
        fill.A = c.Building ? 0.3f + 0.7f * built : 1f;
        int k = pts.Count / 2;
        for (int i = 0; i + 1 < k; i++)
            DrawColoredPolygon(new[] { pts[i], pts[i + 1], pts[pts.Count - 2 - i], pts[pts.Count - 1 - i] }, fill);
        pts.Add(pts[0]);
        DrawPolyline(pts.ToArray(), new Color(0.25f, 0.16f, 0.08f), 2f);
        // The water in her, a line across.
        if (c.Bilge > 0.02f)
        {
            float wy = top + depth * (1f - c.Bilge * 0.8f);
            DrawLine(new Vector2(cx - len * 0.6f, wy), new Vector2(cx + len * 0.6f, wy), new Color(0.3f, 0.45f, 0.65f, 0.8f), 3f);
        }
        foreach (var l in c.Leaks)
        {
            var at = new Vector2(cx - len + 2f * len * l.X, top + depth * Mathf.Sin(l.X * Mathf.Pi) * (0.45f + 0.4f * Mathf.Abs(Mathf.Sin(l.X * 37.1f))));
            DrawCircle(at, 3f + 6f * l.Y, new Color(0.2f, 0.35f, 0.6f, 0.85f));
        }
    }

    private void Bar(Rect2 at, float v, Color col, string label, Font font)
    {
        DrawRect(at, new Color(0.3f, 0.22f, 0.12f, 0.3f));
        DrawRect(new Rect2(at.Position, new Vector2(at.Size.X * Mathf.Clamp(v, 0f, 1f), at.Size.Y)), col);
        DrawRect(at, new Color(0.25f, 0.16f, 0.08f), false, 1f);
        DrawString(font, at.Position + new Vector2(0, -3), label, HorizontalAlignment.Left, -1, 12, Ink);
    }
}
