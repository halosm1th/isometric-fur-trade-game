using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// The board UI/HandWork's games are drawn on.
public partial class HandWorkCanvas : Control
{
    public HandWork Owner_;
    public Rect2 Board;
    public override void _Draw()
    {
        var g = Owner_?._game; var req = Owner_?._req;
        if (g == null || req == null) return;
        var vp = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, vp), new Color(0f, 0f, 0f, 0.55f));
        float w = Mathf.Min(820f, vp.X - 40f), h = Mathf.Min(520f, vp.Y - 40f);
        var panel = new Rect2((vp.X - w) / 2f, (vp.Y - h) / 2f, w, h);
        DrawRect(panel, new Color(0.9f, 0.84f, 0.7f));
        DrawRect(panel, new Color(0.35f, 0.24f, 0.12f), false, 3f);
        var font = ThemeDB.FallbackFont;
        string title = req.TryGetValue("title", out var t) ? t.AsString() : "";
        DrawString(font, panel.Position + new Vector2(18, 30), title, HorizontalAlignment.Left, w - 36, 20, new Color(0.25f, 0.14f, 0.05f));
        string who = req.TryGetValue("who", out var wv) ? wv.AsString() : "";
        string sub = g.Auto ? $"Watching {who} at it." : req.TryGetValue("text", out var tx) ? tx.AsString() : "";
        DrawMultiline(font, panel.Position + new Vector2(18, 52), sub, w - 36, 13, new Color(0.3f, 0.2f, 0.1f));
        Board = new Rect2(panel.Position + new Vector2(24, 96), new Vector2(w - 48, h - 170));
        DrawRect(Board, new Color(0.82f, 0.74f, 0.58f));
        g.Draw(this, Board, font);
        // The line along the bottom: how to play it, or how it went.
        string help = g.Done ? $"{(g.Auto ? who + "'s" : "Your")} work: {Boats.Grade(g.Score)} ({g.Score * 100f:0}%)" : g.Auto ? "Esc: stop watching" : g.Help + "      Esc: leave off";
        DrawString(font, new Vector2(panel.Position.X + 18, panel.End.Y - 44), help, HorizontalAlignment.Left, w - 36, g.Done ? 20 : 13, g.Done ? new Color(0.2f, 0.35f, 0.1f) : new Color(0.3f, 0.2f, 0.1f));
        if (!g.Done && g.Status != "") DrawString(font, new Vector2(panel.Position.X + 18, panel.End.Y - 20), g.Status, HorizontalAlignment.Left, w - 36, 13, new Color(0.45f, 0.15f, 0.08f));
        if (g.Done && req.TryGetValue("helpers", out var hv))
        {
            var a = hv.AsGodotArray(); var parts = new List<string>();
            for (int i = 0; i + 1 < a.Count; i += 2) parts.Add($"{a[i].AsString()}: {Boats.Grade(a[i + 1].AsSingle())}");
            if (parts.Count > 0) DrawString(font, new Vector2(panel.Position.X + 18, panel.End.Y - 20), "Beside you - " + string.Join(", ", parts), HorizontalAlignment.Left, w - 36, 13, new Color(0.3f, 0.2f, 0.1f));
        }
    }

    public void DrawMultiline(Font font, Vector2 at, string text, float width, int size, Color c)
    {
        DrawMultilineString(font, at, text, HorizontalAlignment.Left, width, size, 3, c);
    }
}
