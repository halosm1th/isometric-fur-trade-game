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

    [Export] public float SailSpeed = 90f;   // px a real second at the normal clock (GameCalendar.NormalSecondsPerGameHour)
    private Sprite2D _sprite;
    private Population _population;
    private float _dockedHours;
    private bool _ordersRead;
	// London's packet, aboard till the bos'n carries it up (design doc 10.1.2).
    public Message Packet;
    public float DockedHours => _dockedHours;
    // Lying to off the coast (the prologue aboard her, Core/Starts): she
    // stands in only when told to.
    public bool HeldOffshore;
    public bool OrdersRead => _ordersRead;

    // Put her back where a save left her.
    public void Restore(State state, Vector2 pos, Inventory cargo, Inventory hold, float dockedHours, bool ordersRead)
    {
        Current = state; Position = pos; Visible = state != State.Away;
        Cargo.Clear(); foreach (var (id, n) in cargo.All()) Cargo.Add(id, n);
        Hold.Clear(); foreach (var (id, n) in hold.All()) Hold.Add(id, n);
        _dockedHours = dockedHours; _ordersRead = ordersRead;
        _leg = -1;   // found again from where she lies
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
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--ship") { Summon(); Position = Route[^2]; _leg = Route.Count - 1; }
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
		// The rest of London's outfit: what the country trades for (Economy/
		// StandardOfTrade), landed piece by piece and carried up the roads
		// to the depots (Economy/Outfits, World/Convoys).
		// As much as last year's furs and the Company's purse allow (Economy/Companies).
		float order = Economy.Companies.Order(Faction.HBC, Economy.Outfits.YearMb);
		foreach (var (id, mb) in Sim.Fort != null ? Economy.PostWants.YearOrder(Sim.Fort) : new System.Collections.Generic.List<(ItemId, float)>(Economy.Outfits.Mix))
			Cargo.Add(id, Mathf.RoundToInt(mb * order / 100f / Items.Items.Of(id).ValueMb));
		Position = Route[0];
		_leg = 1;
		Visible = true;
		Current = State.Arriving;
		_ordersRead = false;
		if (Sim.Fort != null) Packet = Letters.Directive(Sim.Fort.LondonIncentiveNext, Sim.Fort.QuotaNextMb, GameCalendar.Instance?.Year ?? 1811);
		Sim.Notice("A sail on the bay: the ship from England is standing in.");
	}

	// The new hand's last day of the crossing: out in the Bay where her
	// way in begins, waiting on the morning tide.
	public void StandOffshore()
	{
		Summon();
		HeldOffshore = true;
	}

	// In on the morning tide and fast at the wharf.
	public void ComeIn()
	{
		HeldOffshore = false;
		if (Current != State.Arriving) return;
		Position = WorldLayout.ShipBerth;
		_leg = Route.Count;
		Dock();
	}

	// ---- her way in -------------------------------------------------------

	// From the open Bay, in at the mouth of the Hayes and up to the wharf,
	// by water all the way (she once sailed a straight line in over the
	// marsh). Found once, the first time she sails: a search outward along
	// the water from the berth to sea that is open all round and well out.
	private static List<Vector2> _route;
	private int _leg;   // the point of Route she is making for
	public static List<Vector2> Route => _route ??= SeaRoute();
	private const float OffingReach = 8000f;   // px out from the wharf, beyond sight of the post

	private static List<Vector2> SeaRoute()
	{
		var berth = WorldLayout.ShipBerth;
		var start = IsoGrid.FromWorld(berth);
		var prev = new Dictionary<Vector2I, Vector2I> { [start] = start };
		var open = new Queue<Vector2I>();
		open.Enqueue(start);
		Vector2I? offing = null;
		while (open.Count > 0 && prev.Count < 2_000_000)
		{
			var c = open.Dequeue();
			if (WorldMap.IsSea(c) && IsoGrid.ToWorld(c).DistanceTo(berth) > OffingReach && OpenSea(c)) { offing = c; break; }
			foreach (var n in Water(c))
			{
				if (prev.ContainsKey(n) || !WorldMap.InBounds(n) || !WorldMap.IsWater(n)) continue;
				prev[n] = c;
				open.Enqueue(n);
			}
		}
		if (offing == null)
		{
			GD.PushWarning("Ship: no way to the sea from the wharf; she comes in from the old offing.");
			return new List<Vector2> { WorldLayout.ShipOffscreen, berth };
		}
		var cells = new List<Vector2I>();
		for (var c = offing.Value; c != start; c = prev[c]) cells.Add(c);
		// Every few cells is enough to steer by, and the berth last.
		var pts = new List<Vector2>();
		for (int i = 0; i < cells.Count; i += 4) pts.Add(IsoGrid.ToWorld(cells[i]));
		pts.Add(berth);
		return pts;
	}

	// Cells sharing an edge, and those touching at a corner: a river one
	// cell wide runs corner to corner as often as edge to edge.
	private static IEnumerable<Vector2I> Water(Vector2I c)
	{
		foreach (var n in IsoGrid.EdgeNeighbours(c)) yield return n;
		foreach (var n in IsoGrid.CornerNeighbours(c)) yield return n;
	}

	private static bool OpenSea(Vector2I c)
	{
		for (int dx = -4; dx <= 4; dx++)
			for (int dy = -8; dy <= 8; dy++)
				if (!WorldMap.IsSea(new Vector2I(c.X + dx, c.Y + dy))) return false;
		return true;
	}

	// The leg she is on, if a save put her somewhere along the way.
	private int NearestLeg()
	{
		int best = 0; float bd = float.MaxValue;
		for (int i = 0; i < Route.Count; i++) { float d = Route[i].DistanceSquaredTo(Position); if (d < bd) { bd = d; best = i; } }
		return best;
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
		_leg = Route.Count - 2;
		Npc factor = null; foreach (var n in Sim.Npcs) if (n.Role == Role.Factor && !n.IsDead) factor = n;
		// A Factor with little loyalty and some ambition shades the books:
		// a share of the furs never reaches the hold (design doc 3.9 step 8).
		if (factor != null && factor.Personality.Loyalty < 0.35f && factor.Personality.Ambition > 0.5f && Sim.Fort != null)
		{
			float skim = 0f;
			foreach (var id in new[] { Items.ItemId.BeaverPelt, Items.ItemId.WolfPelt })
			{
				int n = Hold.Count(id); int take = Mathf.FloorToInt(n * 0.1f);
				if (take > 0) { Hold.Remove(id, take); skim += Items.Items.Of(id).ValueMb * take; }
			}
			if (skim > 0f) { Sim.Fort.SkimmedMb += skim; Chronicle.Event($"{factor.DisplayName} kept back furs of his own from the ship's hold.", factor.GlobalPosition, factor, weight: 2f); }
		}
		// A Factor London has recalled goes home on her (Economy/LondonCommittee).
		if (Economy.LondonCommittee.Recalled && factor != null)
		{
			Economy.LondonCommittee.Recalled = false;
			Sim.Headline($"{factor.DisplayName} sails for London on the ship, recalled to answer for the accounts.");
			factor.Recall("recalled to London to answer for the accounts");
		}
		float mb = Hold.TotalValueMb;
		Economy.Companies.Sell(Faction.HBC, mb, "at the London sale");
		Sim.Notice($"The ship has sailed for London with {mb:0} MB of furs aboard" + (Sim.Fort != null ? $" (quota was {Sim.Fort.QuotaMb:0})." : "."));
		foreach (var pc in Sim.Players())
		{
			if (pc.Faction != Faction.HBC) continue;
			// The year's unexplained sightings roll up into standing at the same
            // cadence as the accounts (design doc 10.5).
            if (factor != null)
            {
                float pile = 0f; int seen = 0;
				foreach (var m in factor.Memories) if (m.Subject == pc.DisplayName && m.Text.Contains("was seen at")) { pile += m.Weight; seen++; }
                if (seen > 0)
                {
                    float hit = Mathf.Min(25f, pile * 4f);
                    pc.Standing -= hit;
					Core.Reveal.Lands(pc, $"Your standing falls by {hit:0}: the Factor's report names {seen} crossing{(seen == 1 ? "" : "s")} of the river you never accounted for.", "was seen at", 1.5f);
					factor.Memories.RemoveAll(m => m.Subject == pc.DisplayName && m.Text.Contains("was seen at"));
                }
            }
            // Private trade, if the word reached him (design doc 3.9 step 8).
            if (factor != null)
            {
                int onHisOwn = 0;
				foreach (var m in factor.Memories) if (m.Subject == pc.DisplayName && m.Text.Contains("on his own account")) onHisOwn++;
                if (onHisOwn > 0)
                {
                    float hit = Mathf.Min(20f, 4f + onHisOwn * 3f);
                    pc.Standing -= hit;
					Core.Reveal.Lands(pc, $"Your standing falls by {hit:0}: the Factor's report says you have been trading on your own account.", "on his own account", 1.5f);
					factor.Memories.RemoveAll(m => m.Subject == pc.DisplayName && m.Text.Contains("on his own account"));
                }
            }
            if (pc.ParoleUntilDay > 0 && !pc.ParoleConfessed && factor != null)
            {
                pc.ParoleConfessed = true;
                pc.Standing -= 12f;
				pc.Say("The Factor has learned you gave the Canadians your parole and said nothing of it. It goes into his report. (standing -12)");
            }
            string was = pc.Rank;
			pc.Rank = pc.Standing >= 60f ? "postmaster" : pc.Standing >= 35f ? "clerk" : pc.Standing >= 15f ? "labourer" : "on notice";
			if (pc.Rank != was) pc.Say(pc.Standing >= 35f ? $"The Factor writes well of you in the accounts that sail today. You are rated {pc.Rank}." : $"The Factor's report on you is cool. You are rated {pc.Rank}.");
			if (pc.Rank == "clerk" || pc.Rank == "postmaster") pc.Inventory.Add(Items.ItemId.TradeGoods, pc.Rank == "postmaster" ? 12 : 6);
			// The trade year closes with her: the player's own account of it
			// goes into his book (design doc P.3).
			Core.Reckoning.AtShipSailing(pc, mb, Sim.Fort);
		}
		if (factor != null) Agents.Delegation.OrderSupply(factor);
		if (Sim.Fort != null)
		{
			// The books go home with her; London reads them in the autumn (design doc 10.1.2).
			Mail.Send(Letters.Accounts(mb, Sim.Fort.QuotaMb), 24f * 75f);
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
		// She sails in game time, at the pace she makes at the normal clock:
		// at any clock - the test clock, a skip, a slow day - she stands in
		// over the same hours (once she sailed by the real second, and at a
		// fast clock took three weeks to berth while the inland posts starved).
		float step = SailSpeed * GameCalendar.NormalSecondsPerGameHour * cal.DeltaGameHours;

		switch (Current)
		{
			case State.Arriving:
				if (HeldOffshore) break;
				if (_leg <= 0 || _leg > Route.Count) _leg = Mathf.Min(NearestLeg() + 1, Route.Count);
				while (step > 0f && _leg < Route.Count)
				{
					float d = Position.DistanceTo(Route[_leg]);
					Position = Position.MoveToward(Route[_leg], step);
					step -= d;
					if (step >= 0f) _leg++;
				}
				if (_leg >= Route.Count) Dock();
				break;
			case State.Departing:
				if (_leg < 0 || _leg >= Route.Count) _leg = Mathf.Max(NearestLeg() - 1, 0);
				while (step > 0f && _leg >= 0)
				{
					float d = Position.DistanceTo(Route[_leg]);
					Position = Position.MoveToward(Route[_leg], step);
					step -= d;
					if (step >= 0f) _leg--;
				}
				if (_leg < 0) { Current = State.Away; Visible = false; }
				break;
			case State.Docked:
				_dockedHours += cal.DeltaGameHours;
				_ordersRead = true;
				break;
		}
	}

	private void Dock()
	{
		_population?.LandRecruits(6, Economy.Companies.Hires(Faction.HBC, 6));
		Current = State.Docked;
		_dockedHours = 0f;
		Sim.Notice("The ship is alongside the wharf. All hands to the landing.");
		Agents.Chronicle.Shared(Sim.Fort, "the ship came in to the wharf, and everyone ran to the shore", 1.3f);
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
		// The bos'n carries the Committee's packet up to the Factor; it is
		// inert until he opens it (design doc 10.1.1). Kill the bos'n on the
		// way and the packet is on his body.
		if (Packet != null && Crew.Count > 0)
		{
			var bosun = Crew[0];
			bosun.Mail.Add(Packet);
			bosun.Brain.Clear(bosun);
			bosun.Brain.Push(bosun, new Agents.Goals.DeliverMailGoal());
			Sim.Notice($"{bosun.DisplayName} comes ashore with the Committee's packet under his arm.");
			Packet = null;
		}
		// An event the Factor replans on (design doc 3.9 step 7).
		foreach (var n in Sim.Npcs) if (n.Role == Role.Factor && !n.IsDead) { Agents.Delegation.AssignDailyTasks(n); break; }
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
