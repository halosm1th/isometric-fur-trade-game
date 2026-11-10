using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// Puts the loose things into the generated world: items on tables, and
// (as the systems land) the people and animals. Lives inside YSort so what
// it spawns sorts with the player.
public partial class WorldSpawner : Node2D
{
    [Export] public PackedScene WorldItemScene;

    private readonly System.Collections.Generic.List<WorldItem> _items = new();

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        Sim.Spawner = this;
        CallDeferred(nameof(Spawn));
    }

    // Server: an item was taken; tell everyone to drop the same one.
    public void Consume(WorldItem item)
    {
        int idx = _items.IndexOf(item);
        Net.Net.I?.BroadcastItemGone(idx);
        RemoveItem(idx);
    }

    public void RemoveItem(int idx)
    {
        if (idx < 0 || idx >= _items.Count) return;
        var it = _items[idx];
        if (it != null && IsInstanceValid(it)) it.QueueFree();
        _items[idx] = null;
    }

    private void Spawn()
    {
        // Bread and ale on the trading-room table.
        var table = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.Depot.Top, 4, 2));
        PlaceItem(ItemId.Bread, 1, table + new Vector2(-26, 6), 52f);
        PlaceItem(ItemId.Ale, 1, table + new Vector2(22, 8), 52f);

        // Canoes drawn up on the shore: one by the wharf, one outside the Nor'Westers' gate.
        PlaceCanoe(0, IsoGrid.ToWorld(IsoGrid.DownLeft(IsoGrid.DownLeft(WorldLayout.WharfRoot))) + new Vector2(-90, 10));
        PlaceCanoe(1, IsoGrid.ToWorld(IsoGrid.DownLeft(IsoGrid.DownLeft(IsoGrid.DownLeft(WorldLayout.NwcGateCell)))) + new Vector2(60, 10));

        // The men's house table has a bite too.
        var mens = IsoGrid.ToWorld(IsoGrid.Cell(WorldLayout.MensHouse.Top, 3, 1));
        PlaceItem(ItemId.Bread, 1, mens + new Vector2(0, 6), 46f);
    }

    // Which of the placed items have been picked up (for a save).
    public Godot.Collections.Array GoneItems()
    {
        var a = new Godot.Collections.Array();
        for (int i = 0; i < _items.Count; i++) if (_items[i] == null) a.Add(i);
        return a;
    }
    public void RestoreGone(Godot.Collections.Array gone)
    {
        foreach (var v in gone) RemoveItem(v.AsInt32());
    }

    // Every boat in the world, afloat-ready or on her bed (World/Canoe), by
    // index; a client makes its copy the first time the host mentions one.
    public readonly System.Collections.Generic.List<Canoe> Canoes = new();
    private readonly System.Collections.Generic.Dictionary<int, Canoe> _canoeByIndex = new();
    private int _nextCanoe = 2;
    public Canoe PlaceCanoe(int index, Vector2 pos, BoatKind kind = BoatKind.Small)
    {
        if (index < 0) index = _nextCanoe;
        if (_canoeByIndex.TryGetValue(index, out var had)) return had;
        var c = new Canoe { Index = index, Position = pos, Name = $"Canoe_{index}", Kind = kind };
        AddChild(c);
        Canoes.Add(c);
        _canoeByIndex[index] = c;
        _nextCanoe = Mathf.Max(_nextCanoe, index + 1);
        return c;
    }
    public Canoe CanoeAt(int index) => _canoeByIndex.TryGetValue(index, out var c) && IsInstanceValid(c) ? c : null;
    public void RemoveCanoe(Canoe c)
    {
        if (c == null) return;
        Canoes.Remove(c); _canoeByIndex.Remove(c.Index);
        if (IsInstanceValid(c)) c.QueueFree();
    }

    // A boat a man could get into: finished, whole, nobody in her.
    public Canoe NearestFreeCanoe(Vector2 pos, float reach)
    {
        Canoe best = null; float bestD = reach;
        foreach (var c in Canoes) { if (!c.IsFree || c.Building || c.Wrecked) continue; float d = c.GlobalPosition.DistanceTo(pos); if (d < bestD) { bestD = d; best = c; } }
        return best;
    }

    // Any boat at hand, on her bed or drawn up, for the bank menu.
    public Canoe NearestBoat(Vector2 pos, float reach)
    {
        Canoe best = null; float bestD = reach;
        foreach (var c in Canoes) { if (!c.IsFree || c.Wrecked) continue; float d = c.GlobalPosition.DistanceTo(pos); if (d < bestD) { bestD = d; best = c; } }
        return best;
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
        _items.Add(item);
        return item;
    }
}
