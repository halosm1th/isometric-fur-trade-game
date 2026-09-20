using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// Puts the people into the world: the post's complement, the Cree band at
// the camp, and (on demand) a ship's crew. Everyone is named and has a bed.
public partial class Population : Node2D
{
    [Export] public PackedScene NpcScene;

    private static readonly string[] OrkneyMen =
    {
        "James Isbister", "William Flett", "Magnus Spence", "John Linklater", "Thomas Corrigal",
        "Peter Sinclair", "Andrew Setter", "George Tait", "Robert Garrick", "Malcolm Sabiston",
    };
    private static readonly string[] Women = { "Nancy Isbister", "Margaret Flett", "Jane Spence", "Mary Linklater" };
    // Placeholder Cree given names (Cree words in common romanisation), to be
    // replaced with properly researched period names.
    private static readonly string[] CreeMen = { "Maskwa", "Wapistan", "Mahihkan", "Pisiskiw", "Kihiw", "Mistatim" };
    private static readonly string[] CreeWomenNames = { "Niska", "Sakihitowin", "Wapun", "Atim-iskwew", "Pihesiw" };
    private static readonly string[] CreeDaughters = { "Kimiwan", "Wawatew" };
    private static readonly string[] Sailors = { "Bos'n Hurst", "Ned Carrow", "Tom Ellery", "Will Pascoe", "Sam Rudd", "Jack Tremayne", "Davy Roskilly", "Harry Penrose" };

        private SpriteFrames _hbcFrames, _factorFrames, _creeFrames, _labourerFrames, _creeWomanFrames;

    public override void _Ready()
    {
        if (Engine.IsEditorHint()) return;
        _hbcFrames = GD.Load<SpriteFrames>("res://Scenes/Player/PlayerFrames.tres");
        _factorFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/RivalFrames.tres");
        _creeFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/CreeFrames.tres");
        _labourerFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/LabourerFrames.tres");
        _creeWomanFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/CreeWomanFrames.tres");
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--trace") _trace = true;
        if (Net.Net.IsServer) CallDeferred(nameof(SpawnAll));
    }

    private void SpawnAll()
    {
        var fort = Sim.Fort; var band = Sim.Band;
        var depot = WorldLayout.Depot; var mens = WorldLayout.MensHouse;

        // The Factor and his clerks live in the trading house.
        var factor = Spawn("Mr. William Auld", Role.Factor, Faction.HBC, IsoGrid.Cell(depot.Top, 3, 1), IsoGrid.Cell(depot.Top, 2, 2), fort, _factorFrames, Colors.White);
        factor.Inventory.Add(ItemId.Musket); factor.Inventory.Add(ItemId.Capote); factor.Inventory.Add(ItemId.Pemmican, 2);
        var clerkA = Spawn("John Fidler (clerk)", Role.Clerk, Faction.HBC, IsoGrid.Cell(depot.Top, 4, 1), IsoGrid.Cell(depot.Top, 3, 3), fort, _hbcFrames, new Color(0.9f, 0.9f, 1f));
        var clerkB = Spawn("Alex Kennedy (clerk)", Role.Clerk, Faction.HBC, IsoGrid.Cell(depot.Top, 2, 3), IsoGrid.Cell(depot.Top, 5, 2), fort, _hbcFrames, new Color(0.85f, 0.95f, 0.9f));
        foreach (var c in new[] { clerkA, clerkB }) { c.Inventory.Add(ItemId.Capote); c.Inventory.Add(ItemId.Knife); c.Inventory.Add(ItemId.Bread); }

        // Labourers in the men's house.
        for (int i = 0; i < 6; i++)
        {
            var bed = i < 5 ? IsoGrid.Cell(mens.Top, 1 + i, 2) : IsoGrid.Cell(mens.Top, 1, 3);
            var m = Spawn(OrkneyMen[i], Role.Labourer, Faction.HBC, bed, IsoGrid.Cell(WorldLayout.FortTop, 8 + i, 8), fort, _labourerFrames, Colors.White);
            m.Inventory.Add(ItemId.Capote); m.Inventory.Add(ItemId.Knife); m.Inventory.Add(ItemId.DriedGoose, 2);
            if (i < 2) m.Inventory.Add(ItemId.Musket);
        }
        // Women of the post: country wives of the men, keeping house.
        for (int i = 0; i < 3; i++)
        {
            var bed = IsoGrid.Cell(mens.Top, 2 + i, 3);
            var w = Spawn(Women[i], Role.Woman, Faction.HBC, bed, IsoGrid.Cell(mens.Top, 3, 2), fort, _hbcFrames, new Color(1f, 0.8f, 0.85f), 0.72f, true);
            w.Inventory.Add(ItemId.PointBlanket); w.Inventory.Add(ItemId.Knife);
        }

        // The Cree band at the camp.
        var tipis = new List<Vector2I>(WorldLayout.TipiCells());
        var chief = Spawn(CreeMen[0] + " (chief)", Role.CreeChief, Faction.Cree, tipis[0], IsoGrid.Cell(WorldLayout.CampCentre, 1, 0), band, _creeFrames, Colors.White);
        chief.Inventory.Add(ItemId.Musket); chief.Inventory.Add(ItemId.HideCoat); chief.Inventory.Add(ItemId.Venison, 2);
        var hunters = new List<Npc>();
        for (int i = 0; i < 4; i++)
        {
            var h = Spawn(CreeMen[1 + i], Role.CreeHunter, Faction.Cree, tipis[1 + i % 4], IsoGrid.Cell(WorldLayout.CampCentre, -1, 1), band, _creeFrames, new Color(0.95f, 0.92f, 0.9f));
            h.Inventory.Add(i % 2 == 0 ? ItemId.Bow : ItemId.Musket); h.Inventory.Add(ItemId.Knife); h.Inventory.Add(ItemId.HideCoat); h.Inventory.Add(ItemId.Venison, 2);
            hunters.Add(h);
        }
        for (int i = 0; i < 4; i++)
        {
            var w = Spawn(CreeWomenNames[i], Role.CreeWoman, Faction.Cree, tipis[i % tipis.Count], IsoGrid.Cell(WorldLayout.CampCentre, 0, 1), band, _creeWomanFrames, Colors.White, 0.78f, true);
            w.Inventory.Add(ItemId.HideCoat); w.Inventory.Add(ItemId.Knife);
            if (i == 0) { w.Spouse = chief; chief.Spouse = w; }
            else { w.Spouse = hunters[i - 1]; hunters[i - 1].Spouse = w; }
        }
        // Two unmarried daughters - the chief's and a hunter's (design doc 8).
        var d1 = Spawn(CreeDaughters[0], Role.CreeDaughter, Faction.Cree, tipis[0], IsoGrid.Cell(WorldLayout.CampCentre, 1, 2), band, _creeWomanFrames, new Color(1f, 0.96f, 0.9f), 0.74f, true);
        d1.Father = chief; chief.Children.Add(d1); d1.Inventory.Add(ItemId.HideCoat);
        var d2 = Spawn(CreeDaughters[1], Role.CreeDaughter, Faction.Cree, tipis[1], IsoGrid.Cell(WorldLayout.CampCentre, -1, 2), band, _creeWomanFrames, new Color(1f, 0.96f, 0.9f), 0.74f, true);
        d2.Father = hunters[0]; hunters[0].Children.Add(d2); d2.Inventory.Add(ItemId.HideCoat);

        Sim.Notice($"York Factory: {Sim.Npcs.Count} souls in the world.");
    }

    // Dev aid: `-- --trace` prints everyone's goal and needs every 2 game hours.
    private bool _trace;
    private float _traceTimer;
    public override void _Process(double delta)
    {
        if (!_trace) return;
        _traceTimer -= GameCalendar.Instance?.DeltaGameHours ?? 0f;
        if (_traceTimer > 0f) return;
        _traceTimer = 1f;
        var cal = GameCalendar.Instance;
        var sb = new System.Text.StringBuilder($"[trace] {cal.DateString} {cal.TimeString} fps={Performance.GetMonitor(Performance.Monitor.TimeFps):0} paths={Pathfinder.Calls} ({Pathfinder.TotalMs:0} ms, {Pathfinder.SlowCalls} slow) nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
        sb.AppendLine();
        foreach (var n in Sim.Npcs)
            sb.AppendLine($"  {n.DisplayName,-24} {n.Cell} {(n.IsDead ? "DEAD" : n.GoalDescription)}  H{n.Needs.Hunger:0} W{n.Needs.Warmth:0} T{n.Needs.Thirst:0} HP{n.Needs.Health:0}");
        sb.Append($"  animals live: {Sim.Animals.Count}; fort furs {Sim.Fort?.FursInStoreMb:0} MB; goods {Sim.Fort?.Stores.Count(ItemId.TradeGoods)}; ship {Sim.Ship?.Status()}; band cache: {Sim.Band?.Stores.Summary()}");
        GD.Print(sb.ToString());
    }

    public Npc Spawn(string name, Role role, Faction faction, Vector2I bed, Vector2I work, Household home, SpriteFrames frames, Color tint, float scale = 0.8f, bool female = false)
    {
        var npc = NpcScene.Instantiate<Npc>();
        npc.DisplayName = name;
        npc.Role = role;
        npc.Faction = faction;
        npc.BedCell = bed;
        npc.WorkCell = work;
        npc.HomeStores = home;
        npc.IsFemale = female;
        npc.Position = IsoGrid.ToWorld(Sim.Paths?.NearestWalkable(bed) ?? bed);
        AddChild(npc);
        npc.SetAppearance(frames, tint, scale);
        npc.Name = $"npc_{npc.NetId}";
        Net.Net.I?.Announce(npc);
        return npc;
    }

    // A ship's crew, spawned on the wharf when she ties up.
    public List<Npc> SpawnCrew(int n)
    {
        var crew = new List<Npc>();
        var end = WorldLayout.WharfEnd;
        for (int i = 0; i < n; i++)
        {
            var cell = IsoGrid.Cell(end, 0, i / 2 + 1);
            var s = Spawn(Sailors[i % Sailors.Length], Role.Sailor, Faction.ShipsCrew, cell, cell, Sim.Fort, _hbcFrames, new Color(0.75f, 0.8f, 0.9f), 0.78f);
            s.Inventory.Add(ItemId.Knife); s.Inventory.Add(ItemId.Bread, 3); s.Inventory.Add(ItemId.Capote);
            if (i < 2) s.Inventory.Add(ItemId.Musket);
            crew.Add(s);
        }
        return crew;
    }
}
