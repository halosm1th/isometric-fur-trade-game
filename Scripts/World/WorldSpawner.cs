using Godot;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// Puts the loose things into the generated world: items on tables, and
// (as the systems land) the people and animals. Lives inside YSort so what
// it spawns sorts with the player.
public partial class WorldSpawner : Node2D
{
    [Export] public PackedScene WorldItemScene;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        CallDeferred(nameof(Spawn));
    }

    private void Spawn()
    {
        // Bread and ale on the trading-room table.
        var table = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.Depot.Top, 4, 2));
        PlaceItem(ItemId.Bread, 1, table + new Vector2(-26, 6), 52f);
        PlaceItem(ItemId.Ale, 1, table + new Vector2(22, 8), 52f);

        // The men's house table has a bite too.
        var mens = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.MensHouse.Top, 3, 1));
        PlaceItem(ItemId.Bread, 1, mens + new Vector2(0, 6), 46f);
    }

    public WorldItem PlaceItem(ItemId id, int count, Vector2 worldPos, float lift = 0f, float scale = 0.5f)
    {
        if (WorldItemScene == null) return null;
        var item = WorldItemScene.Instantiate<WorldItem>();
        item.Position = worldPos;
        item.Count = count;
        item.SpriteScale = scale;
        item.LiftPixels = lift;
        AddChild(item);
        item.Item = id;
        return item;
    }
}
