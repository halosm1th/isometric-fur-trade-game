using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// What the pad needs that the keyboard and mouse don't (Core/Pad): a menu
// that is up always has something under the pad's thumb, the focused thing
// is kept scrolled into view, the right stick scrolls a page of text, and
// in play the right stick moves the pointer, for aiming what's in hand.
// Lives under SettingsRuntime for the whole run.
public partial class PadDriver : Node
{
    public static PadDriver Instance { get; private set; }

    // When the right stick last moved the pointer in play: for a while after,
    // the pad's "use" goes where the pointer is, not just ahead of you.
    public static ulong AimedAtMs;
    public static bool Aiming => Pad.Active && (GunReady || Time.GetTicksMsec() - AimedAtMs < 4000);
    // The musket brought up (Player/PlayerController, the right-stick click
    // or X): the pointer is the sights, and nothing fires till you pull.
    public static bool GunReady;

    private Control _lastFocus;

    public override void _EnterTree() => Instance = this;

    // Dev: `-- --padsim[=SECONDS]` plays a run of pad presses through Godot's own input
    // and prints what each did (nothing from outside reaches the window).
    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--padtalk") SimulateTalk();
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--padsim"))Simulate(arg.StartsWith("--padsim=") && double.TryParse(arg.Substring(9), out double s) ? s : 12.0);
    }

    // Dev: `-- --talk --padtalk` works the conversation `--talk` opens from the pad.
    private void SimulateTalk()
    {
        double t = 40.0;
        void At(double dt, System.Action a) { t += dt; GetTree().CreateTimer(t, true, false, true).Timeout += a; }
        void Btn(JoyButton b) { Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = true, Device = 0 }); Input.FlushBufferedEvents(); Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = false, Device = 0 }); Input.FlushBufferedEvents(); }
        string Focus() => GetViewport().GuiGetFocusOwner() is { } f ? $"{f.GetType().Name} '{(f as Button)?.Text ?? f.Name}'" : "none";
        void Say(string s) => GD.Print($"[padtalk] {s}");
        At(0, () => { Btn(JoyButton.DpadDown); });
        At(0.3, () => Say($"talking={DialogueMenu.Instance?.IsOpen}, focus={Focus()}"));
        At(0.1, () => Btn(JoyButton.DpadDown));
        At(0.3, () => Say($"D-pad down -> focus={Focus()}"));
        At(0.1, () => Btn(JoyButton.A));
        At(1.0, () => Say($"A -> talking={DialogueMenu.Instance?.IsOpen}, options={DialogueMenu.Instance?.OptionCount}, focus={Focus()}"));
        At(0.1, () => Btn(JoyButton.B));
        At(0.8, () => Say($"B -> talking={DialogueMenu.Instance?.IsOpen}"));
    }

    private void Simulate(double from)
    {
        double t = from;
        void At(double dt, System.Action a) { t += dt; GetTree().CreateTimer(t, true, false, true).Timeout += a; }
        void Btn(JoyButton b) { Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = true, Device = 0 }); Input.FlushBufferedEvents(); Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = false, Device = 0 }); Input.FlushBufferedEvents(); }
        void Ax(JoyAxis a, float v) { Input.ParseInputEvent(new InputEventJoypadMotion { Axis = a, AxisValue = v, Device = 0 }); Input.FlushBufferedEvents(); }
        string Focus() => GetViewport().GuiGetFocusOwner() is { } f ? $"{f.GetType().Name} '{(f as Button)?.Text ?? f.Name}'" : "none";
        void Say(string s) => GD.Print($"[padsim] {s}");
        void Snap(string name) { var path = ProjectSettings.GlobalizePath($"user://padsim_{name}.png"); GetViewport().GetTexture().GetImage().SavePng(path); Say("saved " + path); }
        Vector2 start = default;
        At(0, () => { Btn(JoyButton.A); Say($"pad active={Pad.Active}; interact hint key={Keybinds.KeyFor("interact")}, use_ahead={Keybinds.KeyFor("use_ahead")}"); });
        At(0.5, () => { Btn(JoyButton.Start); });
        At(0.3, () => Say($"Menu -> pause open={PauseMenu.IsOpen}, focus={Focus()}"));
        At(0.3, () => { Btn(JoyButton.DpadDown); });
        At(0.3, () => Say($"D-pad down -> focus={Focus()}"));
        At(0.3, () => { Btn(JoyButton.B); });
        At(0.3, () => Say($"B -> pause open={PauseMenu.IsOpen}"));
        At(0.3, () => { Btn(JoyButton.Back); });
        At(0.8, () => Say($"View -> book open={JournalPanel.IsOpen}, focus={Focus()}"));
        At(0.1, () => Btn(JoyButton.DpadRight));
        At(0.3, () => { Say($"D-pad right in the pack -> focus={(GetViewport().GuiGetFocusOwner() as InventorySlot)?.Index.ToString() ?? Focus()}"); Snap("pack"); Btn(JoyButton.Y); });
        At(0.2, () => Say($"Y -> carrying slot {InventorySlot.Carrying}"));
        At(0.1, () => Btn(JoyButton.B));
        At(0.2, () => Say($"B -> carrying slot {InventorySlot.Carrying}, book open={JournalPanel.IsOpen}"));
        At(0.3, () => { Ax(JoyAxis.TriggerRight, 1f); Ax(JoyAxis.TriggerRight, 0f); });
        At(0.3, () => Say($"RT -> book page={(FirstTabs(JournalPanel.Instance.PadRoot)?.CurrentTab.ToString() ?? "?")}"));
        At(0.3, () => { Btn(JoyButton.B); });
        At(0.3, () => Say($"B -> book open={JournalPanel.IsOpen}"));
        At(0.3, () => { start = Sim.Player?.GlobalPosition ?? default; Ax(JoyAxis.LeftX, 1f); });
        At(1.0, () => { Ax(JoyAxis.LeftX, 0f); Say($"left stick right 1 s -> moved {(Sim.Player?.GlobalPosition ?? default) - start}"); });
        At(0.3, () => { Btn(JoyButton.DpadRight); Say($"D-pad right -> hotbar slot {Hotbar.SelectedSlot}"); });
        At(0.3, () => { Btn(JoyButton.DpadLeft); Say($"D-pad left -> hotbar slot {Hotbar.SelectedSlot}"); });
        At(0.3, () => { Btn(JoyButton.DpadUp); });
        At(0.3, () => { Say($"D-pad up -> map open={MapPanel.IsOpen}"); Snap("map"); Btn(JoyButton.A); Say($"A on map -> waypoint={Waypoint.Target}"); });
        At(0.3, () => { Btn(JoyButton.B); });
        At(0.3, () => Say($"B -> map open={MapPanel.IsOpen}"));
        At(0.3, () => { Ax(JoyAxis.RightX, 1f); });
        At(0.5, () => { Ax(JoyAxis.RightX, 0f); Say($"right stick -> aiming={Aiming}, pointer={GetViewport().GetMousePosition()}"); });
        At(0.3, () => { Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = true }); Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Shift, Keycode = Key.Shift, Pressed = false }); Input.FlushBufferedEvents(); Say($"a key -> pad active={Pad.Active}; interact hint key={Keybinds.KeyFor("interact")}"); });
        // Talk to whoever is nearest, then work the conversation from the pad.
        At(0.3, () => { if (Sim.Player is Player.PlayerController pc) GD.Print("[padsim] hint: " + pc.InteractHint()); Btn(JoyButton.A); });
        At(1.5, () => Say($"A -> talking={DialogueMenu.Instance?.IsOpen}, focus={Focus()}"));
        At(0.2, () => Btn(JoyButton.DpadDown));
        At(0.3, () => Say($"D-pad down -> focus={Focus()}"));
        At(0.2, () => Btn(JoyButton.B));
        At(0.5, () => Say($"B -> talking={DialogueMenu.Instance?.IsOpen}"));
        At(0.5, () => Say("done"));
    }

    // The menu the pad is working in now, topmost first; null in play.
    public static Control TopMenu()
    {
        if (EndScreen.PadRoot is { } end) return end;
        if (PauseMenu.IsOpen && PauseMenu.Instance != null) return PauseMenu.Instance.PadRoot;
        if (DialogueMenu.Instance != null && GodotObject.IsInstanceValid(DialogueMenu.Instance) && DialogueMenu.Instance.IsOpen) return DialogueMenu.Instance.PadRoot;
        if (JournalPanel.IsOpen && JournalPanel.Instance != null) return JournalPanel.Instance.PadRoot;
        if (HelpPanel.IsOpen && HelpPanel.Instance != null) return HelpPanel.Instance.PadRoot;
        var scene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
        if (scene is MainMenu mm) return mm;
        return null;
    }

    // A menu is up that the pad's sticks and buttons belong to instead of the walking.
    public static bool MenuUp => Pad.Active && (TopMenu() != null || MapPanel.IsOpen);

    // LT / RT turn the pages of whatever tabbed thing is up (the book, the settings).
    private bool _lt, _rt;
    public override void _Input(InputEvent e)
    {
        if (e is not InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } m) return;
        bool left = m.Axis == JoyAxis.TriggerLeft;
        bool down = m.AxisValue > 0.6f;
        bool was = left ? _lt : _rt;
        if (left) _lt = m.AxisValue > (was ? 0.3f : 0.6f); else _rt = m.AxisValue > (was ? 0.3f : 0.6f);
        if (!down || was) return;
        var menu = TopMenu();
        if (menu == null || FirstTabs(menu) is not { } tabs || tabs.GetTabCount() < 2) return;
        int n = tabs.GetTabCount();
        int i = tabs.CurrentTab;
        for (int step = 0; step < n; step++)
        {
            i = (i + (left ? -1 : 1) + n) % n;
            if (!tabs.IsTabHidden(i) && !tabs.IsTabDisabled(i)) break;
        }
        tabs.CurrentTab = i;
        Core.Audio.Ui("ui_page_turn");
        GetViewport().SetInputAsHandled();
    }

    private static TabContainer FirstTabs(Node root)
    {
        if (root is Control c && !c.IsVisibleInTree()) return null;
        if (root is TabContainer t) return t;
        foreach (var child in root.GetChildren()) if (FirstTabs(child) is { } hit) return hit;
        return null;
    }

    public override void _Process(double delta)
    {
        if (!Pad.Active) { _lastFocus = null; return; }
        var vp = GetViewport();
        var owner = vp.GuiGetFocusOwner();
        var menu = TopMenu();
        float dt = (float)delta;
        // Pause menu stops Engine.TimeScale: the pad still has to move.
        if (Engine.TimeScale > 0.0) dt /= (float)Engine.TimeScale; else dt = (float)Mathf.Min(delta, 0.05);
        dt = Mathf.Min(dt, 0.1f);

        if (menu != null)
        {
            bool ok = owner != null && owner.IsVisibleInTree() && (menu == owner || menu.IsAncestorOf(owner));
            if (!ok && Pad.FirstFocusable(menu) is { } first) { first.GrabFocus(); owner = first; }
        }
        else if (owner != null && owner is not LineEdit and not TextEdit && !MapPanel.IsOpen)
        {
            // In play nothing holds focus, or A would press a HUD button.
            vp.GuiReleaseFocus();
            owner = null;
        }

        // Keep what the pad is on in sight inside its scrolling list.
        if (owner != _lastFocus)
        {
            _lastFocus = owner;
            for (Node n = owner?.GetParent(); n != null; n = n.GetParent())
                if (n is ScrollContainer sc) { sc.EnsureControlVisible(owner); break; }
        }

        var rs = Pad.RightStick();
        if (rs == Vector2.Zero) return;
        if (menu != null)
        {
            // The right stick scrolls the page: the focused thing's list, or the first that can scroll.
            ScrollContainer sc = null;
            for (Node n = owner?.GetParent(); n != null && n != menu.GetParent(); n = n.GetParent()) if (n is ScrollContainer s) { sc = s; break; }
            sc ??= FirstScrollable(menu);
            if (sc != null) sc.ScrollVertical += (int)(rs.Y * 900f * dt);
            return;
        }
        if (MapPanel.IsOpen) return;   // the map pans itself
        // In play: the pointer follows the right stick.
        var pos = vp.GetMousePosition() + rs * rs.Length() * 1100f * dt;
        var size = vp.GetVisibleRect().Size;
        pos = pos.Clamp(Vector2.Zero, size - Vector2.One);
        Pad.LastWarpMs = Time.GetTicksMsec();
        AimedAtMs = Pad.LastWarpMs;
        vp.WarpMouse(pos);
    }

    private static ScrollContainer FirstScrollable(Node root)
    {
        if (root is Control c && !c.IsVisibleInTree()) return null;
        if (root is ScrollContainer sc && sc.GetVScrollBar().MaxValue > sc.Size.Y + 1) return sc;
        foreach (var child in root.GetChildren()) if (FirstScrollable(child) is { } hit) return hit;
        return null;
    }
}
