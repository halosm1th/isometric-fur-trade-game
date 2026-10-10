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
        ("attack_nearest", "Strike the nearest hostile"), ("drink", "Drink"), ("wait", "Wait an hour / sleep at night"),
        ("hotbar_use", "Use the selected hotbar slot"),
        ("hotbar_1", "Hotbar slot 1"), ("hotbar_2", "Hotbar slot 2"), ("hotbar_3", "Hotbar slot 3"), ("hotbar_4", "Hotbar slot 4"), ("hotbar_5", "Hotbar slot 5"),
        ("hotbar_6", "Hotbar slot 6"), ("hotbar_7", "Hotbar slot 7"), ("hotbar_8", "Hotbar slot 8"), ("hotbar_9", "Hotbar slot 9"),
        ("inventory", "Your pack"), ("journal", "Journal"), ("map", "Map"), ("log", "The log"), ("chat", "Chat"), ("help", "Help"),
        ("time_fast", "Hold to fast-forward"), ("pause_clock", "Stop / start the clock (host)"), ("clock_slower", "Slower clock (host)"), ("clock_faster", "Faster clock (host)"),
        ("restart", "Begin again after death"), ("debug_menu", "Test console (host)"),
    };

    // Keys the game keeps for itself.
    public static readonly Key[] Reserved = { Key.Escape, Key.F5, Key.F9, Key.F11 };

    private static Dictionary<string, Key[]> _defaults;

    // Goes up whenever a binding changes, so on-screen hints can tell they're stale.
    public static int Version { get; private set; }

    private static void SnapshotDefaults()
    {
        if (_defaults != null) return;
        _defaults = new();
        foreach (var (action, _) in Actions) if (InputMap.HasAction(action)) _defaults[action] = Current(action);
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
        Version++;
        Settings.Save();
    }

    public static void Read(ConfigFile cfg)
    {
        SnapshotDefaults();
        if (!cfg.HasSection("keys")) return;
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
    }

    // What the key says on this keyboard's layout.
    public static string Name(Key physical)
    {
        if (physical == Key.None) return "-";
        bool headless = DisplayServer.GetName().Contains("headless");
        return OS.GetKeycodeString(headless ? physical : DisplayServer.KeyboardGetKeycodeFromPhysical(physical));
    }

    // The first key for an action, for hints like "E  Talk to ...".
    public static string KeyFor(string action)
    {
        foreach (var k in Current(action)) if (k != Key.None) return Name(k);
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
        return names.Count == 0 ? "unbound" : string.Join(" / ", names);
    }
}
