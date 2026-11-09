using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// A game controller (laid out for an Xbox pad; any pad SDL knows maps onto
// the same buttons). Each action has one pad slot beside its two keys, kept
// as a code: a button is its JoyButton number, a stick or trigger pushed one
// way is 1000 + axis * 2 (+1 for the positive side). Whichever was touched
// last - the keyboard and mouse or the pad - is what the hints name.
public static class Pad
{
    public const int None = -1;
    private const int AxisBase = 1000;

    // The pad layout the game comes with.
    public static readonly (string action, int code)[] Defaults =
    {
        ("move_left", Axis(JoyAxis.LeftX, -1)), ("move_right", Axis(JoyAxis.LeftX, 1)),
        ("move_up", Axis(JoyAxis.LeftY, -1)), ("move_down", Axis(JoyAxis.LeftY, 1)),
        ("interact", (int)JoyButton.A), ("restart", (int)JoyButton.A),
        ("sneak", (int)JoyButton.B),
        ("attack_nearest", (int)JoyButton.X),
        ("hotbar_use", (int)JoyButton.Y),
        ("sprint", Axis(JoyAxis.TriggerLeft, 1)),
        ("use_ahead", Axis(JoyAxis.TriggerRight, 1)),
        ("time_fast", (int)JoyButton.LeftShoulder),
        ("mount", (int)JoyButton.RightShoulder),
        ("drink", (int)JoyButton.LeftStick),
        ("pause_clock", (int)JoyButton.RightStick),
        ("map", (int)JoyButton.DpadUp),
        ("wait", (int)JoyButton.DpadDown),
        ("hotbar_prev", (int)JoyButton.DpadLeft),
        ("hotbar_next", (int)JoyButton.DpadRight),
        ("inventory", (int)JoyButton.Back),
    };

    // Start is the menu, always; it is not offered for rebinding.
    public const JoyButton MenuButton = JoyButton.Start;

    public static int Axis(JoyAxis axis, int sign) => AxisBase + (int)axis * 2 + (sign > 0 ? 1 : 0);

    public static InputEvent EventFor(int code)
    {
        if (code < 0) return null;
        if (code < AxisBase) return new InputEventJoypadButton { ButtonIndex = (JoyButton)code, Device = -1 };
        int a = code - AxisBase;
        return new InputEventJoypadMotion { Axis = (JoyAxis)(a / 2), AxisValue = a % 2 == 1 ? 1f : -1f, Device = -1 };
    }

    public static int CodeOf(InputEvent ev) => ev switch
    {
        InputEventJoypadButton b => (int)b.ButtonIndex,
        InputEventJoypadMotion m => Axis(m.Axis, m.AxisValue > 0 ? 1 : -1),
        _ => None,
    };

    public static bool IsPad(InputEvent ev) => ev is InputEventJoypadButton or InputEventJoypadMotion;

    public static string Name(int code)
    {
        if (code < 0) return "-";
        if (code < AxisBase)
            return (JoyButton)code switch
            {
                JoyButton.A => "A", JoyButton.B => "B", JoyButton.X => "X", JoyButton.Y => "Y",
                JoyButton.Back => "View", JoyButton.Start => "Menu", JoyButton.Guide => "Xbox",
                JoyButton.LeftStick => "LS", JoyButton.RightStick => "RS",
                JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB",
                JoyButton.DpadUp => "D-pad up", JoyButton.DpadDown => "D-pad down",
                JoyButton.DpadLeft => "D-pad left", JoyButton.DpadRight => "D-pad right",
                var b => $"Button {(int)b}",
            };
        int a = code - AxisBase;
        bool pos = a % 2 == 1;
        return (JoyAxis)(a / 2) switch
        {
            JoyAxis.TriggerLeft => "LT", JoyAxis.TriggerRight => "RT",
            JoyAxis.LeftX => pos ? "L stick right" : "L stick left",
            JoyAxis.LeftY => pos ? "L stick down" : "L stick up",
            JoyAxis.RightX => pos ? "R stick right" : "R stick left",
            JoyAxis.RightY => pos ? "R stick down" : "R stick up",
            var x => $"Axis {(int)x}{(pos ? "+" : "-")}",
        };
    }

    // ---- which device is in use ----------------------------------------------

    public static bool Active { get; private set; }

    // Called with every input event (SettingsRuntime): a pad button or a firm
    // push of a stick means the pad; a key, a click or a real mouse movement
    // means the keyboard. The pad's own warping of the pointer is ignored.
    public static void Saw(InputEvent ev)
    {
        bool pad;
        switch (ev)
        {
            case InputEventJoypadButton { Pressed: true }: pad = true; break;
            case InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) > 0.5f: pad = true; break;
            case InputEventKey { Pressed: true }: pad = false; break;
            case InputEventMouseButton { Pressed: true } mb when !Synthetic: pad = false; break;
            case InputEventMouseMotion mm when !Synthetic && !WarpedLately: _mouseTravel += mm.Relative.Length(); if (_mouseTravel < 40f) return; pad = false; break;
            default: return;
        }
        if (pad) _mouseTravel = 0f;
        if (pad == Active) return;
        _mouseTravel = 0f;
        Active = pad;
        Keybinds.Touch();
    }

    // Set while the pad itself moves the pointer or clicks, so that isn't taken for the mouse.
    // A hand brushing the mouse isn't picking it up: it has to travel a little first.
    private static float _mouseTravel;
    public static bool Synthetic;
    public static ulong LastWarpMs;
    private static bool WarpedLately => Time.GetTicksMsec() - LastWarpMs < 120;

    // Any stick held off centre or trigger squeezed, on any pad (the quiet days
    // count it as hands on the controls: Input.IsAnythingPressed only sees buttons).
    public static bool AnyAxisHeld()
    {
        foreach (int d in Input.GetConnectedJoypads())
            for (int a = 0; a <= (int)JoyAxis.TriggerRight; a++)
                if (Mathf.Abs(Input.GetJoyAxis(d, (JoyAxis)a)) > 0.3f) return true;
        return false;
    }

    // The right stick, with a dead zone, across every pad.
    public static Vector2 RightStick()
    {
        var v = Vector2.Zero;
        foreach (int d in Input.GetConnectedJoypads())
            v += new Vector2(Input.GetJoyAxis(d, JoyAxis.RightX), Input.GetJoyAxis(d, JoyAxis.RightY));
        return v.Length() < 0.2f ? Vector2.Zero : v.LimitLength(1f);
    }

    public static float Trigger(JoyAxis which)
    {
        float t = 0f;
        foreach (int d in Input.GetConnectedJoypads()) t = Mathf.Max(t, Input.GetJoyAxis(d, which));
        return t < 0.15f ? 0f : t;
    }

    // The first thing in a menu the pad can land on: a visible, enabled, focusable control.
    public static Control FirstFocusable(Node root)
    {
        if (root is Control c && !c.IsVisibleInTree()) return null;
        if (root is Control f && f.FocusMode == Control.FocusModeEnum.All && f is not ScrollContainer && f is not TabContainer && !(f is BaseButton { Disabled: true }) && f is not RichTextLabel && f is not LineEdit)
            return f;
        foreach (var child in root.GetChildren())
        {
            var hit = FirstFocusable(child);
            if (hit != null) return hit;
        }
        return null;
    }
}
