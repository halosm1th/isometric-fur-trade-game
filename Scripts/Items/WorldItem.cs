using Godot;

namespace IsometricFurTradeGame.Scripts.Items;

// An item lying in the world. Drop the WorldItem scene anywhere in the
// editor, pick the item and count in the inspector; the sprite follows.
[Tool]
public partial class WorldItem : Node2D
{
    private ItemId _item = ItemId.Bread;
    private int _count = 1;

    [Export] public ItemId Item
    {
        get => _item;
        set { _item = value; RefreshSprite(); }
    }

    [Export] public int Count
    {
        get => _count;
        set => _count = Mathf.Max(1, value);
    }

    // How far the picture sits above the node's ground point, e.g. to rest
    // on a table top.
    [Export] public float LiftPixels
    {
        get => _lift;
        set { _lift = value; RefreshSprite(); }
    }
    private float _lift;

    [Export] public float SpriteScale
    {
        get => _scale;
        set { _scale = value; RefreshSprite(); }
    }
    private float _scale = 0.55f;

    private Sprite2D _sprite;

    public override void _Ready()
    {
        RefreshSprite();
        if (!Engine.IsEditorHint()) AddToGroup("pickups");
    }

    private void RefreshSprite()
    {
        if (!IsInsideTree()) return;
        _sprite ??= GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite == null)
        {
            _sprite = new Sprite2D { Name = "Sprite2D" };
            AddChild(_sprite);
        }
        _sprite.Texture = GD.Load<Texture2D>(Items.Of(_item).Icon);
        _sprite.Scale = Vector2.One * _scale;
        _sprite.Centered = true;
        _sprite.Offset = new Vector2(0, -_lift / Mathf.Max(0.01f, _scale) - (_sprite.Texture?.GetHeight() ?? 0) / 2f);
    }

    public string Label => _count > 1 ? $"{Items.Of(_item).Name} x{_count}" : Items.Of(_item).Name;
}
