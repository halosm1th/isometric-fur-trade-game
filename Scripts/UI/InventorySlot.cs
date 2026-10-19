using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// One square of the pack: the hotbar and the pack grid are both made of these.
// Drag a stack onto another square to move it (an empty square takes it, an
// occupied one swaps). Right-click uses the item (or wears / takes off clothing).
public partial class InventorySlot : Panel
{
    public int Index;
    public bool Hotbar;
    public bool Selected;
    public event System.Action<int> LeftClicked;

    private readonly TextureRect _icon = new();
    private readonly Label _count = new();
    private readonly Label _key = new();
    private ItemId? _shown;

    private static readonly StyleBoxFlat Plain = Make(new Color(0.08f, 0.07f, 0.06f, 0.7f), new Color(0.45f, 0.38f, 0.28f));
    private static readonly StyleBoxFlat HotbarStyle = Make(new Color(0.12f, 0.1f, 0.06f, 0.75f), new Color(0.6f, 0.5f, 0.3f));
    private static readonly StyleBoxFlat SelectedStyle = Make(new Color(0.16f, 0.13f, 0.08f, 0.85f), new Color(0.95f, 0.8f, 0.45f));
    private static readonly StyleBoxFlat DropStyle = Make(new Color(0.18f, 0.2f, 0.1f, 0.9f), new Color(0.7f, 0.95f, 0.5f));
    private static readonly System.Collections.Generic.Dictionary<ItemId, Texture2D> IconCache = new();

    private static StyleBoxFlat Make(Color bg, Color border)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(2); s.SetCornerRadiusAll(3);
        return s;
    }

    public static Texture2D IconFor(ItemId id)
    {
        if (!IconCache.TryGetValue(id, out var tex)) { tex = GD.Load<Texture2D>(Items.Items.Of(id).Icon); IconCache[id] = tex; }
        return tex;
    }

    public InventorySlot()
    {
        CustomMinimumSize = new Vector2(52, 52);
        MouseFilter = MouseFilterEnum.Stop;
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _icon.TextureFilter = TextureFilterEnum.Nearest;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        _icon.SetAnchorsPreset(LayoutPreset.FullRect);
        _icon.OffsetLeft = 6; _icon.OffsetTop = 6; _icon.OffsetRight = -6; _icon.OffsetBottom = -6;
        AddChild(_icon);
        _key.Position = new Vector2(4, 1); _key.MouseFilter = MouseFilterEnum.Ignore;
        _key.AddThemeFontSizeOverride("font_size", 10); _key.Modulate = new Color(1, 1, 1, 0.6f);
        AddChild(_key);
        _count.HorizontalAlignment = HorizontalAlignment.Right; _count.MouseFilter = MouseFilterEnum.Ignore;
        _count.SetAnchorsPreset(LayoutPreset.BottomRight);
        _count.GrowHorizontal = GrowDirection.Begin; _count.GrowVertical = GrowDirection.Begin;
        _count.Position = new Vector2(-4, -2);
        _count.AddThemeFontSizeOverride("font_size", 12);
        _count.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        AddChild(_count);
    }

    // What the slot last showed. Refresh runs every frame for the hotbar, and
    // setting a label, an icon or a style is a call into the engine (a style
    // override relays the slot out), so only what changed is set.
    private string _keyShown, _countShown;
    private bool _wornShown;
    private StyleBox _styleShown;

    public void Refresh(PlayerController p, string keyLabel)
    {
        var slot = p?.Inventory.Slot(Index);
        keyLabel ??= "";
        if (keyLabel != _keyShown) { _key.Text = keyLabel; _keyShown = keyLabel; }
        if (slot is var (id, n) && slot != null)
        {
            if (_shown != id) _icon.Texture = IconFor(id);
            string count = n > 1 ? n.ToString() : "";
            if (count != _countShown) { _count.Text = count; _countShown = count; }
            bool worn = p.Inventory.IsWorn(id);
            if (_shown != id || worn != _wornShown) { _icon.Modulate = worn ? new Color(0.85f, 1f, 0.85f) : Colors.White; _wornShown = worn; }
            if (_shown != id || Engine.GetFramesDrawn() % 30 == 0) TooltipText = Describe(p, id, n);
            _shown = id;
        }
        else if (_shown != null || _countShown != "")
        {
            _icon.Texture = null; _count.Text = ""; TooltipText = ""; _shown = null; _countShown = "";
        }
        if (!_dropHover)
        {
            var style = Selected ? SelectedStyle : Hotbar ? HotbarStyle : Plain;
            if (style != _styleShown) { AddThemeStyleboxOverride("panel", style); _styleShown = style; }
        }
    }

    public static string Describe(PlayerController p, ItemId id, int n)
    {
        var d = Items.Items.Of(id);
        string extra = d.Kind switch
        {
            ItemKind.Food => $"feeds {d.Nutrition:0}",
            ItemKind.Drink => $"quenches {d.Thirst:0}" + (d.Intoxication > 0 ? $", {d.Intoxication:0} proof" : ""),
            ItemKind.Clothing => $"warmth {d.Warmth:0}",
            ItemKind.Fuel => $"{d.FuelHours:0} h on the fire",
            ItemKind.Weapon => $"{d.Damage:0} dmg, reach {d.Range:0}",
            ItemKind.Tool => d.Capacity > 0 ? $"+{d.Capacity:0} lb carry" : id == ItemId.Snowshoes ? "snow at full pace" : "use it (click / hotbar)",
            _ => "",
        };
        string s = $"{d.Name} x{n}\n{d.WeightLbs * n:0.#} lb   {d.ValueMb * n:0.#} MB";
        if (extra != "") s += "\n" + extra;
        if (p.Inventory.Wearable(id)) s += p.Inventory.IsWorn(id) ? "\n(worn - right-click to take off)" : "\n(stowed - right-click to wear)";
        else s += "\nRight-click to use. Drag to move.";
        return s;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mb) return;
        var p = Sim.Player as PlayerController;
        if (mb.ButtonIndex == MouseButton.Left) { LeftClicked?.Invoke(Index); AcceptEvent(); }
        else if (mb.ButtonIndex == MouseButton.Right && p != null)
        {
            var s = p.Inventory.Slot(Index);
            if (s == null) return;
            if (p.Inventory.Wearable(s.Value.id)) Net.Net.I?.Act("wear", new Godot.Collections.Array { Index });
            else p.UseSlot(Index);
            AcceptEvent();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var p = Sim.Player as PlayerController;
        var s = p?.Inventory.Slot(Index);
        if (s == null) return default;
        var preview = new TextureRect { Texture = IconFor(s.Value.id), CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, Modulate = new Color(1, 1, 1, 0.85f), MouseFilter = MouseFilterEnum.Ignore };
        var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        preview.Position = new Vector2(-20, -20);
        holder.AddChild(preview);
        SetDragPreview(holder);
        return new Godot.Collections.Dictionary { ["inv_slot"] = Index };
    }

    private bool _dropHover;
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool ok = data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("inv_slot");
        if (ok && !_dropHover) { _dropHover = true; AddThemeStyleboxOverride("panel", DropStyle); _styleShown = DropStyle; }
        return ok;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd || what == NotificationMouseExit) _dropHover = false;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        _dropHover = false;
        int from = (int)data.AsGodotDictionary()["inv_slot"];
        if (from == Index) return;
        var p = Sim.Player as PlayerController;
        // Apply locally at once so the square follows the cursor; the server's
        // next state sync carries the same layout.
        if (!Net.Net.IsServer) p?.Inventory.MoveSlot(from, Index);
        Net.Net.I?.Act("move_slot", new Godot.Collections.Array { from, Index });
    }
}
