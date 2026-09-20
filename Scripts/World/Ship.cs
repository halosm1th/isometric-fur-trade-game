using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// The annual supply ship from London (design doc 5.1, 10.1). She comes in
// on the calendar's ShipArrives event, ties up at the wharf with the year's
// outfit and the Committee's orders, takes on the furs, and sails on
// ShipDeparts - if she still has a crew to sail her.
public partial class Ship : Node2D
{
    public enum State { Away, Arriving, Docked, Departing, Stranded }
    public State Current { get; private set; } = State.Away;
    public bool IsDocked => Current == State.Docked || Current == State.Stranded;
    // Furs go aboard only once the outfit is off.
    public bool LoadingOpen => IsDocked && Cargo.SlotCount == 0;

    public readonly Inventory Cargo = new();   // outfit to be landed
    public readonly Inventory Hold = new();    // furs taken aboard
    public readonly List<Npc> Crew = new();

    [Export] public float SailSpeed = 90f;   // px/s at normal time
    private Sprite2D _sprite;
    private Population _population;
    private float _dockedHours;
    private bool _ordersRead;
    public float DockedHours => _dockedHours;
    public bool OrdersRead => _ordersRead;

    // Put her back where a save left her.
    public void Restore(State state, Vector2 pos, Inventory cargo, Inventory hold, float dockedHours, bool ordersRead)
    {
        Current = state; Position = pos; Visible = state != State.Away;
        Cargo.Clear(); foreach (var (id, n) in cargo.All()) Cargo.Add(id, n);
        Hold.Clear(); foreach (var (id, n) in hold.All()) Hold.Add(id, n);
        _dockedHours = dockedHours; _ordersRead = ordersRead;
        Crew.Clear();
    }

    public override void _Ready()
    {
        Sim.Ship = this;
        _sprite = new Sprite2D { Texture = GD.Load<Texture2D>("res://assets/sprites/ship.png"), Offset = new Vector2(-240 + 230, -245 + 170), ZIndex = 2 };
        AddChild(_sprite);
        Position = WorldLayout.ShipOffscreen;
        Visible = false;
        var cal = GameCalendar.Instance;
        if (cal != null) cal.CalendarEvent += OnCalendarEvent;
        CallDeferred(nameof(FindPopulation));
    }

    private void FindPopulation()
    {
        _population = GetTree().GetFirstNodeInGroup("population") as Population;
        // Dev aid: `-- --ship` brings her in at once.
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--ship") { Summon(); Position = WorldLayout.ShipBerth + new Vector2(400, -100); }
    }

    private void OnCalendarEvent(string name)
    {
        if (!Net.Net.IsServer) return;
        if (name == "ShipArrives") Summon();
        if (name == "ShipDeparts") Depart();
    }

    // Load the outfit London sends and set sail for the bay.
    public void Summon()
    {
        if (Current != State.Away) return;
        Cargo.Clear(); Hold.Clear();
        Cargo.Add(ItemId.TradeGoods, 120);
        Cargo.Add(ItemId.Pemmican, 30);
        Cargo.Add(ItemId.DriedGoose, 40);
        Cargo.Add(ItemId.Bread, 40);
        Cargo.Add(ItemId.Ale, 30);
        Cargo.Add(ItemId.Musket, 8);
        Cargo.Add(ItemId.SteelTrap, 12);
        Cargo.Add(ItemId.Rum, 24);
        Cargo.Add(ItemId.Kettle, 4);
        Cargo.Add(ItemId.PointBlanket, 20);
        Cargo.Add(ItemId.Capote, 6);
        Position = WorldLayout.ShipOffscreen;
        Visible = true;
        Current = State.Arriving;
        _ordersRead = false;
        Sim.Notice("A sail on the bay: the ship from England is standing in.");
    }

    public void Depart()
    {
        if (!IsDocked) return;
        int alive = 0;
        foreach (var c in Crew) if (c != null && IsInstanceValid(c) && !c.IsDead) alive++;
        if (alive < 3)
        {
            Current = State.Stranded;
            Sim.Notice("The ship cannot sail: too few of her crew are left alive. She lies at the wharf.");
            return;
        }
        foreach (var c in Crew) if (c != null && IsInstanceValid(c)) c.QueueFree();
        Crew.Clear();
        Current = State.Departing;
        float mb = Hold.TotalValueMb;
        Sim.Notice($"The ship has sailed for London with {mb:0} MB of furs aboard" + (Sim.Fort != null ? $" (quota was {Sim.Fort.QuotaMb:0})." : "."));
        foreach (var pc in Sim.Players())
        {
            if (pc.Faction != Faction.HBC) continue;
            string was = pc.Rank;
            pc.Rank = pc.Standing >= 60f ? "postmaster" : pc.Standing >= 35f ? "clerk" : pc.Standing >= 15f ? "labourer" : "on notice";
            if (pc.Rank != was) pc.Say(pc.Standing >= 35f ? $"The Factor writes well of you in the accounts that sail today. You are rated {pc.Rank}." : $"The Factor's report on you is cool. You are rated {pc.Rank}.");
            if (pc.Rank == "clerk" || pc.Rank == "postmaster") pc.Inventory.Add(Items.ItemId.TradeGoods, pc.Rank == "postmaster" ? 12 : 6);
        }
        if (Sim.Fort != null)
        {
            Economy.LondonCommittee.ReviewReturns(Sim.Fort, mb);
            Sim.Fort.FursTakenMb = 0f; Sim.Fort.GoodsPaidMb = 0f;
        }
    }

    public void ClientSync(Vector2 pos, State state)
    {
        if (Net.Net.IsServer) return;
        Current = state;
        Position = pos;
        Visible = state != State.Away;
    }

    public override void _Process(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        // Sailing speed follows game time so a skip still brings her in.
        float step = SailSpeed * cal.DeltaGameHours * cal.SecondsPerGameHour;

        switch (Current)
        {
            case State.Arriving:
                Position = Position.MoveToward(WorldLayout.ShipBerth, step);
                if (Position.DistanceTo(WorldLayout.ShipBerth) < 1f) Dock();
                break;
            case State.Departing:
                Position = Position.MoveToward(WorldLayout.ShipOffscreen, step);
                if (Position.DistanceTo(WorldLayout.ShipOffscreen) < 1f) { Current = State.Away; Visible = false; }
                break;
            case State.Docked:
                _dockedHours += cal.DeltaGameHours;
                if (!_ordersRead && _dockedHours > 1f) ReadOrders();
                break;
        }
    }

    private void Dock()
    {
        _population?.LandRecruits(6);
        Current = State.Docked;
        _dockedHours = 0f;
        Sim.Notice("The ship is alongside the wharf. All hands to the landing.");
        if (_population != null)
        {
            Crew.Clear();
            Crew.AddRange(_population.SpawnCrew(8));
            // The ship is how the fever comes to the Bay (design doc 13.5).
            if (GD.Randf() < 0.35f && Crew.Count > 0)
            {
                var sick = Crew[(int)(GD.Randi() % (uint)Crew.Count)];
                sick.CatchFever();
                Sim.Notice("There is fever aboard, the mate says. Keep the men off her if you can.");
            }
        }
        // An event the Factor replans on (design doc 3.9 step 7).
        foreach (var n in Sim.Npcs) if (n.Role == Role.Factor && !n.IsDead) { Agents.Delegation.AssignDailyTasks(n); break; }
    }

    // The Committee's letter takes effect when it's read (design doc 10.1).
    private void ReadOrders()
    {
        _ordersRead = true;
        var fort = Sim.Fort;
        if (fort == null) return;
        fort.PeltPrice = fort.LondonIncentiveNext;
        fort.QuotaMb = fort.QuotaNextMb;
        Sim.Notice($"The Factor reads the Committee's orders: pay {fort.PeltPrice:0.00} MB in goods per MB of fur; London expects {fort.QuotaMb:0} MB of returns.");
    }

    public string Status() => Current switch
    {
        State.Away => "at sea",
        State.Arriving => "standing in",
        State.Docked => $"alongside: {Cargo.CountOfKind(ItemKind.Goods) + Cargo.SlotCount} lots to land, {Hold.TotalValueMb:0} MB aboard",
        State.Departing => "outward bound",
        State.Stranded => "stranded at the wharf",
        _ => "?",
    };
}
