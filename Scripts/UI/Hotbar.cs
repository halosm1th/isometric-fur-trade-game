using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// Nine slots along the bottom showing what the player carries, in the order
// it was picked up. Scroll wheel moves the selection, Enter uses it, and
// the number keys use a slot directly.
public partial class Hotbar : Control
{
    [Export] public NodePath PlayerPath;
    public const int Slots = 9;

    private PlayerController _player;
    private readonly InventorySlot[] _slots = new InventorySlot[Slots];
    private int _keysVersion = -1;
    private Label _name;
    private Label _notice;
    private Label _hint;
    private float _hintTimer;
    private float _noticeTimer;
    private int _selected;
    public static int SelectedSlot { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => { _player = p; OnPlayerBound(); };
        if (_player != null) CallDeferred(nameof(OnPlayerBound));


        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.SetAnchorsPreset(LayoutPreset.CenterBottom);
        column.GrowHorizontal = GrowDirection.Both;
        column.GrowVertical = GrowDirection.Begin;
        column.Position = new Vector2(0, -12);
        column.Alignment = BoxContainer.AlignmentMode.End;
        column.AddThemeConstantOverride("separation", 4);
        AddChild(column);

        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.7f));
        _hint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        column.AddChild(_hint);
        _notice = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(500, 0), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _notice.AddThemeFontSizeOverride("font_size", 15);
        _notice.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        column.AddChild(_notice);

        _name = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _name.AddThemeFontSizeOverride("font_size", 13);
        _name.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        column.AddChild(_name);

        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 4);
        column.AddChild(row);

        for (int i = 0; i < Slots; i++)
        {
            var slot = new InventorySlot { Index = i, Hotbar = true };
            slot.LeftClicked += idx => Select(idx);
            row.AddChild(slot);
            _slots[i] = slot;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_player == null || _player.IsDead) return;

        if (@event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelDown) Select(_selected + 1);
            else if (mb.ButtonIndex == MouseButton.WheelUp) Select(_selected - 1);
            return;
        }

        if (@event.IsActionPressed("hotbar_use"))
        {
            Use(_selected);
            return;
        }
        for (int i = 0; i < Slots; i++)
        {
            if (@event.IsActionPressed($"hotbar_{i + 1}"))
            {
                Select(i);
                Use(i);
                return;
            }
        }
    }

    public static Hotbar Instance;
    public static void SelectSlot(int i) => Instance?.Select(i);

    private void Select(int i) { _selected = (i + Slots) % Slots; SelectedSlot = _selected; }

    private void Use(int i) => _player.UseSlot(i);

    private void OnPlayerBound()
    {
        if (_player != null) _player.Notice += Notice;
    }

    public void Notice(string text)
    {
        _notice.Text = text;
        _noticeTimer = 2.5f;
    }

    public override void _Process(double delta)
    {
        // Each slot shows whatever key it is bound to now.
        if (_keysVersion != Core.Keybinds.Version)
        {
            _keysVersion = Core.Keybinds.Version;
        }
        _hintTimer -= (float)delta;
        if (_hintTimer <= 0f && _hint != null && _player != null) { _hintTimer = 0.2f; _hint.Text = _player.IsLocal ? _player.InteractHint() : ""; }
        if (_player == null) return;
        var inv = _player.Inventory;

        for (int i = 0; i < Slots; i++)
        {
            _slots[i].Selected = i == _selected;
            _slots[i].Refresh(_player, Core.Keybinds.KeyFor($"hotbar_{i + 1}"));
        }

        var sel = inv.Slot(_selected);
        _name.Text = sel is var (sid, _) && sel != null ? Items.Items.Of(sid).Name : "";

        if (_noticeTimer > 0f)
        {
            _noticeTimer -= (float)delta;
            _notice.Modulate = new Color(1, 1, 1, Mathf.Clamp(_noticeTimer, 0f, 1f));
        }
    }
}
