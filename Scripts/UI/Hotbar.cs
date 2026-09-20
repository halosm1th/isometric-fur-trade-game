using Godot;
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
    private readonly Panel[] _panels = new Panel[Slots];
    private readonly TextureRect[] _icons = new TextureRect[Slots];
    private readonly Label[] _counts = new Label[Slots];
    private Label _name;
    private Label _notice;
    private float _noticeTimer;
    private int _selected;

    private static readonly StyleBoxFlat SlotStyle = Make(new Color(0.08f, 0.07f, 0.06f, 0.7f), new Color(0.45f, 0.38f, 0.28f));
    private static readonly StyleBoxFlat SelectedStyle = Make(new Color(0.16f, 0.13f, 0.08f, 0.85f), new Color(0.95f, 0.8f, 0.45f));

    private static StyleBoxFlat Make(Color bg, Color border)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(2);
        s.SetCornerRadiusAll(3);
        return s;
    }

    public override void _Ready()
    {
        _player = GetNodeOrNull<PlayerController>(PlayerPath);
        if (_player != null) _player.Notice += Notice;

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.SetAnchorsPreset(LayoutPreset.CenterBottom);
        column.GrowHorizontal = GrowDirection.Both;
        column.GrowVertical = GrowDirection.Begin;
        column.Position = new Vector2(0, -12);
        column.Alignment = BoxContainer.AlignmentMode.End;
        column.AddThemeConstantOverride("separation", 4);
        AddChild(column);

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
            var panel = new Panel { CustomMinimumSize = new Vector2(52, 52), MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(panel);

            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsPreset(LayoutPreset.FullRect);
            icon.OffsetLeft = 6; icon.OffsetTop = 6; icon.OffsetRight = -6; icon.OffsetBottom = -6;
            panel.AddChild(icon);

            var key = new Label { Text = (i + 1).ToString(), Position = new Vector2(4, 1) };
            key.AddThemeFontSizeOverride("font_size", 10);
            key.Modulate = new Color(1, 1, 1, 0.6f);
            panel.AddChild(key);

            var count = new Label { HorizontalAlignment = HorizontalAlignment.Right };
            count.SetAnchorsPreset(LayoutPreset.BottomRight);
            count.GrowHorizontal = GrowDirection.Begin;
            count.GrowVertical = GrowDirection.Begin;
            count.Position = new Vector2(-4, -2);
            count.AddThemeFontSizeOverride("font_size", 12);
            count.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
            panel.AddChild(count);

            _panels[i] = panel; _icons[i] = icon; _counts[i] = count;
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

    private void Select(int i) => _selected = (i + Slots) % Slots;

    private void Use(int i)
    {
        string result = _player.UseSlot(i);
        if (!string.IsNullOrEmpty(result)) Notice(result);
    }

    public void Notice(string text)
    {
        _notice.Text = text;
        _noticeTimer = 2.5f;
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;
        var inv = _player.Inventory;

        for (int i = 0; i < Slots; i++)
        {
            var slot = inv.Slot(i);
            _panels[i].AddThemeStyleboxOverride("panel", i == _selected ? SelectedStyle : SlotStyle);
            if (slot is var (id, count) && slot != null)
            {
                _icons[i].Texture = IconFor(id);
                _counts[i].Text = count > 1 ? count.ToString() : "";
            }
            else
            {
                _icons[i].Texture = null;
                _counts[i].Text = "";
            }
        }

        var sel = inv.Slot(_selected);
        _name.Text = sel is var (sid, _) && sel != null ? Items.Items.Of(sid).Name : "";

        if (_noticeTimer > 0f)
        {
            _noticeTimer -= (float)delta;
            _notice.Modulate = new Color(1, 1, 1, Mathf.Clamp(_noticeTimer, 0f, 1f));
        }
    }

    private static readonly System.Collections.Generic.Dictionary<ItemId, Texture2D> IconCache = new();

    private static Texture2D IconFor(ItemId id)
    {
        if (!IconCache.TryGetValue(id, out var tex))
        {
            tex = GD.Load<Texture2D>(Items.Items.Of(id).Icon);
            IconCache[id] = tex;
        }
        return tex;
    }
}
