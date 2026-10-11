using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// A tile layer whose cells are painted by code (the structure painter's
// floor and front layers). Its tiles are never written into the scene file:
// the world is streamed, and an editor save would otherwise bake whatever
// happened to be on screen into the .tscn.
[Tool, GlobalClass]
public partial class GeneratedTileLayer : TileMapLayer
{
    public override void _ValidateProperty(Godot.Collections.Dictionary property) => DontStore(property);

    public static void DontStore(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsStringName() != TileMapLayer.PropertyName.TileMapData) return;
        var usage = (PropertyUsageFlags)property["usage"].AsInt64();
        property["usage"] = (long)(usage & ~PropertyUsageFlags.Storage);
    }
}
