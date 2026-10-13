using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// The mouse pointer says what the click will do (design doc 10.8): a quill
// for ordinary pointing, an hourglass while the game is working through
// something, a scroll and quill over a man you can speak to, knife and
// tomahawk over one you would be striking, the ink and coin over a trade.
public static class Cursors
{
    public enum Look { Default, Busy, Talk, Attack, Trade }

    private const string Dir = "res://assets/ui/cursors/";
    private static readonly System.Collections.Generic.Dictionary<Look, Texture2D> Art = new();
    private static Look _now = (Look)(-1);
    private static bool _ready;

    public static void Install()
    {
        if (_ready || DisplayServer.GetName() == "headless") return;
        _ready = true;
        foreach (Look l in System.Enum.GetValues<Look>())
            Art[l] = GD.Load<Texture2D>(Dir + l.ToString().ToLower() + ".png");
        // The two Godot sets for itself: the arrow everywhere, and the
        // hourglass whenever it decides it is busy.
        if (Art[Look.Default] != null) Input.SetCustomMouseCursor(Art[Look.Default], Input.CursorShape.Arrow, Hot(Look.Default));
        if (Art[Look.Busy] != null)
        {
            Input.SetCustomMouseCursor(Art[Look.Busy], Input.CursorShape.Busy, Hot(Look.Busy));
            Input.SetCustomMouseCursor(Art[Look.Busy], Input.CursorShape.Wait, Hot(Look.Busy));
        }
        if (Art[Look.Talk] != null) Input.SetCustomMouseCursor(Art[Look.Talk], Input.CursorShape.PointingHand, Hot(Look.Talk));
        Show(Look.Default);
    }

    // The quill points with its nib; the rest are read whole.
    private static Vector2 Hot(Look l) => l == Look.Default ? new Vector2(7f, 41f) : new Vector2(24f, 24f);

    public static void Show(Look l)
    {
        if (!_ready || l == _now) return;
        var tex = Art.GetValueOrDefault(l);
        if (tex == null) return;
        _now = l;
        Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, Hot(l));
    }
}
