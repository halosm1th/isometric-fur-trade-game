using Godot;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Tab opens a line to type; Enter sends it to everyone; Esc drops it.
// Lines arrive through the world notices, prefixed with the sender's name.
public partial class ChatBox : Control
{
    public static ChatBox Instance { get; private set; }
    private LineEdit _line;
    public bool IsTyping => _line != null && _line.Visible;

    public override void _Ready()
    {
        Instance = this;
        _line = new LineEdit { Visible = false, PlaceholderText = "say something... (Enter to send, Esc to cancel)", CustomMinimumSize = new Vector2(520, 30) };
        _line.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _line.GrowHorizontal = GrowDirection.Both; _line.GrowVertical = GrowDirection.Begin;
        _line.Position = new Vector2(-260, -190);
        _line.TextSubmitted += t => { NetHub.I?.SendChat(t); _line.Text = ""; _line.Visible = false; };
        AddChild(_line);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsTyping)
        {
            if (@event.IsActionPressed("ui_cancel")) { _line.Text = ""; _line.Visible = false; GetViewport().SetInputAsHandled(); }
            return;
        }
        // Watching the world, Tab is the next thing happening (PlayerObserver).
        if (Core.Sim.Player is Player.PlayerController { Ghost: true }) return;
        if (@event.IsActionPressed("chat"))
        {
            _line.Visible = true;
            _line.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }
}
