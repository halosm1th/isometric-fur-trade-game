using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// Rebinding keys. project.godot's [input] holds the defaults; the player's
// changes are kept in settings.cfg [keys] as the physical keys for each
// action they altered, and put into the InputMap at startup. Every action
// has two slots (W and Up both walk up). A key belongs to one action only:
// giving it to another takes it from the first.
public static class Keybinds
{
    public const int Slots = 2;

    public static readonly (string action, string what)[] Actions =
    {
        ("move_up", "Walk up"), ("move_down", "Walk down"), ("move_left", "Walk left"), ("move_right", "Walk right"),
        ("sprint", "Run"), ("sneak", "Crouch / sneak (and deliberate arson with flint)"), ("interact", "Talk / use what's in front of you"),
        ("attack_nearest", "Strike the nearest hostile"), ("drink", "Drink"), ("mount", "Mount / get down off your horse (E on your horse does it too)"), ("wait", "Wait an hour / sleep at night"),
        ("hotbar_use", "Use the selected hotbar slot"), ("hotbar_prev", "Previous hotbar slot (or the mouse wheel)"), ("hotbar_next", "Next hotbar slot (or the mouse wheel)"),
        ("use_ahead", "Use what's in hand on the ground ahead (the mouse does it with a left click)"),
        ("hotbar_1", "Hotbar slot 1"), ("hotbar_2", "Hotbar slot 2"), ("hotbar_3", "Hotbar slot 3"), ("hotbar_4", "Hotbar slot 4"), ("hotbar_5", "Hotbar slot 5"),
        ("hotbar_6", "Hotbar slot 6"), ("hotbar_7", "Hotbar slot 7"), ("hotbar_8", "Hotbar slot 8"), ("hotbar_9", "Hotbar slot 9"),
        ("inventory", "Your pack"), ("character", "Yourself (body, skills, standing)"), ("journal", "Journal"), ("map", "Map"), ("log", "The log"), ("chat", "Chat"), ("help", "Help"),
        ("time_fast", "Hold to fast-forward"), ("pause_clock", "Stop / start the clock (host)"), ("clock_slower", "Slower clock (host)"), ("clock_faster", "Faster clock (host)"),
        ("restart", "Begin again after death"), ("debug_menu", "Test console (host)"),
    };

    // Keys the game keeps for itself.
    public static readonly Key[] Reserved = { Key.Escape, Key.F5, Key.F9, Key.F11 };

    private static Dictionary<string, Key[]> _defaults;
    private static Dictionary<string, int> _padDefaults;

    // The actions project.godot doesn't hold (pad-era ones), and the pad's
    // layout put alongside the keys. Run once, before the saved keys are read.
    private static bool _installed;
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        foreach (var name in new[] { "hotbar_prev", "hotbar_next", "use_ahead", "pause_menu" })
            if (!InputMap.HasAction(name)) InputMap.AddAction(name, 0.4f);
        if (InputMap.ActionGetEvents("pause_menu").Count == 0)
        {
            InputMap.ActionAddEvent("pause_menu", new InputEventKey { PhysicalKeycode = Key.Escape });
            InputMap.ActionAddEvent("pause_menu", new InputEventJoypadButton { ButtonIndex = Pad.MenuButton, Device = -1 });
        }
        foreach (var (action, code) in Pad.Defaults)
            if (InputMap.HasAction(action) && CurrentPad(action) == Pad.None) SetPad(action, code);
        // The menus' own actions: Godot's defaults leave the pad out of some
        // (ui_cancel is Escape alone), so A chooses, B backs out, and the
        // d-pad and left stick move, everywhere.
        void Ui(string action, params InputEvent[] events)
        {
            if (!InputMap.HasAction(action)) return;
            foreach (var e in events)
            {
                bool has = false;
                foreach (var ev in InputMap.ActionGetEvents(action)) if (Pad.IsPad(ev) && Pad.CodeOf(ev) == Pad.CodeOf(e)) has = true;
                if (!has) InputMap.ActionAddEvent(action, e);
            }
        }
        Ui("ui_accept", Pad.EventFor((int)JoyButton.A));
        Ui("ui_cancel", Pad.EventFor((int)JoyButton.B));
        Ui("ui_up", Pad.EventFor((int)JoyButton.DpadUp), Pad.EventFor(Pad.Axis(JoyAxis.LeftY, -1)));
        Ui("ui_down", Pad.EventFor((int)JoyButton.DpadDown), Pad.EventFor(Pad.Axis(JoyAxis.LeftY, 1)));
        Ui("ui_left", Pad.EventFor((int)JoyButton.DpadLeft), Pad.EventFor(Pad.Axis(JoyAxis.LeftX, -1)));
        Ui("ui_right", Pad.EventFor((int)JoyButton.DpadRight), Pad.EventFor(Pad.Axis(JoyAxis.LeftX, 1)));
        SnapshotDefaults();
    }

    // Something about how keys are named changed (the pad was picked up or put down).
    public static void Touch() => Version++;

    // Goes up whenever a binding changes, so on-screen hints can tell they're stale.
    public static int Version { get; private set; }

    private static void SnapshotDefaults()
    {
        if (_defaults != null) return;
        _defaults = new(); _padDefaults = new();
        foreach (var (action, _) in Actions) if (InputMap.HasAction(action)) { _defaults[action] = Current(action); _padDefaults[action] = CurrentPad(action); }
    }

    // The pad button or stick direction an action answers to, or Pad.None.
    public static int CurrentPad(string action)
    {
        if (!InputMap.HasAction(action)) return Pad.None;
        foreach (var ev in InputMap.ActionGetEvents(action)) if (Pad.IsPad(ev)) return Pad.CodeOf(ev);
        return Pad.None;
    }

    private static void SetPad(string action, int code)
    {
        foreach (var ev in InputMap.ActionGetEvents(action)) if (Pad.IsPad(ev)) InputMap.ActionEraseEvent(action, ev);
        var e = Pad.EventFor(code);
        if (e != null) InputMap.ActionAddEvent(action, e);
    }

    // Puts a pad button on an action, taking it from any other that had it.
    public static string BindPad(string action, int code, out bool refused)
    {
        SnapshotDefaults();
        refused = false;
        if (code == (int)Pad.MenuButton) { refused = true; return "Menu is kept by the game."; }
        string takenFrom = null;
        if (code != Pad.None)
            foreach (var (other, what) in Actions)
                if (other != action && CurrentPad(other) == code) { SetPad(other, Pad.None); takenFrom = what; }
        SetPad(action, code);
        Version++;
        Settings.Save();
        return takenFrom;
    }

    // The keys in an action's slots, Key.None for an empty one.
    public static Key[] Current(string action)
    {
        var keys = new Key[Slots];
        int i = 0;
        if (!InputMap.HasAction(action)) return keys;
        foreach (var ev in InputMap.ActionGetEvents(action))
            if (ev is InputEventKey k && i < Slots) keys[i++] = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
        return keys;
    }

    private static void Set(string action, Key[] keys)
    {
        foreach (var ev in InputMap.ActionGetEvents(action)) if (ev is InputEventKey) InputMap.ActionEraseEvent(action, ev);
        foreach (var k in keys) if (k != Key.None) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
    }

    // Puts key into the slot; returns the description of the action it was
    // taken from, if any, or a reason it can't be had.
    public static string Bind(string action, int slot, Key key, out bool refused)
    {
        SnapshotDefaults();
        refused = false;
        if (System.Array.IndexOf(Reserved, key) >= 0) { refused = true; return $"{Name(key)} is kept by the game."; }
        string takenFrom = null;
        if (key != Key.None)
            foreach (var (other, what) in Actions)
            {
                if (!InputMap.HasAction(other)) continue;
                var ks = Current(other);
                bool had = false;
                for (int i = 0; i < Slots; i++) if (ks[i] == key && !(other == action && i == slot)) { ks[i] = Key.None; had = true; }
                if (had) { Set(other, Compact(ks)); if (other != action) takenFrom = what; }
            }
        var mine = Current(action);
        mine[slot] = key;
        Set(action, Compact(mine, slot));
        Version++;
        Settings.Save();
        return takenFrom;
    }

    // Empty slots go to the end, except the one just filled stays put.
    private static Key[] Compact(Key[] keys, int keep = -1)
    {
        if (keep >= 0) return keys;
        var list = new List<Key>();
        foreach (var k in keys) if (k != Key.None) list.Add(k);
        while (list.Count < Slots) list.Add(Key.None);
        return list.ToArray();
    }

    public static void ResetAll()
    {
        SnapshotDefaults();
        foreach (var (action, keys) in _defaults) Set(action, keys);
        foreach (var (action, code) in _padDefaults) SetPad(action, code);
        Version++;
        Settings.Save();
    }

    public static void Read(ConfigFile cfg)
    {
        SnapshotDefaults();
        if (cfg.HasSection("pad"))
            foreach (var action in cfg.GetSectionKeys("pad"))
                if (InputMap.HasAction(action)) SetPad(action, (int)cfg.GetValue("pad", action));
        if (!cfg.HasSection("keys")) { Version++; return; }
        foreach (var action in cfg.GetSectionKeys("keys"))
        {
            if (!InputMap.HasAction(action)) continue;
            var arr = (int[])cfg.GetValue("keys", action);
            var keys = new Key[Slots];
            for (int i = 0; i < Slots && i < arr.Length; i++) keys[i] = (Key)arr[i];
            Set(action, keys);
        }
        Version++;
    }

    public static void Write(ConfigFile cfg)
    {
        SnapshotDefaults();
        foreach (var (action, def) in _defaults)
        {
            var now = Current(action);
            bool same = true;
            for (int i = 0; i < Slots; i++) if (now[i] != def[i]) same = false;
            if (same) continue;
            var arr = new int[Slots];
            for (int i = 0; i < Slots; i++) arr[i] = (int)now[i];
            cfg.SetValue("keys", action, arr);
        }
        foreach (var (action, def) in _padDefaults)
        {
            int now = CurrentPad(action);
            if (now != def) cfg.SetValue("pad", action, now);
        }
    }

    // What the key says on this keyboard's layout.
    public static string Name(Key physical)
    {
        if (physical == Key.None) return "-";
        bool headless = DisplayServer.GetName().Contains("headless");
        return OS.GetKeycodeString(headless ? physical : DisplayServer.KeyboardGetKeycodeFromPhysical(physical));
    }

    // The first key for an action, for hints like "E  Talk to ...".
    // Remembered until a binding changes (Version): the HUD asks for its hints
    // every frame, and each answer was an array and a string from the engine.
    // An unbound action is not remembered, in case it is bound by code later.
    private static readonly Dictionary<string, string> _keyFor = new();
    private static int _keyForVersion = -1;
    public static string KeyFor(string action)
    {
        if (_keyForVersion != Version) { _keyFor.Clear(); _keyForVersion = Version; }
        if (_keyFor.TryGetValue(action, out var known)) return known;
        if (Pad.Active && CurrentPad(action) is var pc and not Pad.None) return _keyFor[action] = Pad.Name(pc);
        foreach (var k in Current(action)) if (k != Key.None) return _keyFor[action] = Name(k);
        return "(unbound)";
    }

    // "1-9" while the hotbar keys are the digits in order, else each one.
    public static string HotbarKeys()
    {
        bool digits = true;
        for (int i = 1; i <= 9; i++) if (KeyFor($"hotbar_{i}") != i.ToString()) digits = false;
        if (digits) return "1-9";
        var names = new List<string>();
        for (int i = 1; i <= 9; i++) names.Add(KeyFor($"hotbar_{i}"));
        return string.Join(" ", names);
    }

    public static string Describe(string action)
    {
        var names = new List<string>();
        foreach (var k in Current(action)) if (k != Key.None) names.Add(Name(k));
        if (CurrentPad(action) is var pc and not Pad.None) names.Add(Pad.Name(pc));
        return names.Count == 0 ? "unbound" : string.Join(" / ", names);
    }
}
