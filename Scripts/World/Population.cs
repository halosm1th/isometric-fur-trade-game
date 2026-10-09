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

		private SpriteFrames _hbcFrames, _factorFrames, _creeFrames, _labourerFrames, _creeWomanFrames, _assiniboineFrames;
	private static readonly string[] AssinMen = { "Tchatka", "Mato", "Wanbdi", "Cetan" };
	private static readonly string[] AssinWomen = { "Wiyaka", "Ptesan", "Wakanda" };

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;
		_hbcFrames = GD.Load<SpriteFrames>("res://Scenes/Player/PlayerFrames.tres");
		_factorFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/RivalFrames.tres");
		_creeFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/CreeFrames.tres");
		_labourerFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/LabourerFrames.tres");
		_creeWomanFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/CreeWomanFrames.tres");
		_assiniboineFrames = GD.Load<SpriteFrames>("res://Scenes/Npc/AssiniboineFrames.tres");
		foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--trace") _trace = true;
		// Dev aid: `-- --shot=SECONDS` saves what's on screen to user://shot_N.png
		// every SECONDS (up to six), so a run can be checked without a window capture.
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--shot=") && float.TryParse(arg.Substring(7), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float every))
				for (int i = 1; i <= 6; i++) { int n = i; GetTree().CreateTimer(every * i).Timeout += () => { var path = ProjectSettings.GlobalizePath($"user://shot_{n}.png"); GetViewport().GetTexture().GetImage().SavePng(path); GD.Print($"[shot] {path}"); }; }
		// Dev aid: `-- --kill=Name` - that person dies five seconds in (to watch
		// a household take a loss: mourning, the empty bunk, the sale of effects).
		foreach (string arg in OS.GetCmdlineUserArgs())
			if (arg.StartsWith("--kill=")) { string who = arg.Substring(7); GetTree().CreateTimer(5.0).Timeout += () => { foreach (var n in Sim.Npcs) if (n.DisplayName == who && !n.IsDead) { n.TakeDamage(9999f, null); break; } }; }
		if (Net.Net.IsServer) CallDeferred(nameof(SpawnAll));
	}

	private void SpawnAll()
	{
		if (SaveGame.HasPending) { SaveGame.Restore(this); return; }
		var fort = Sim.Fort; var band = Sim.Band;
		var depot = WorldLayout.Depot; var mens = WorldLayout.MensHouse;

		// The Factor and his clerks live in the trading house.
		var factor = Spawn("Mr. William Auld", Role.Factor, Faction.HBC, IsoGrid.Cell(depot.Top, 3, 1), IsoGrid.Cell(depot.Top, 2, 2), fort, _factorFrames, Colors.White);
		factor.Inventory.Add(ItemId.Musket); factor.Inventory.Add(ItemId.Capote); factor.Inventory.Add(ItemId.Pemmican, 2);
		if (fort != null) fort.Leader = factor;
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
		if (band != null) band.Leader = chief;
		var hunters = new List<Npc>();
		for (int i = 0; i < 4; i++)
		{
			var h = Spawn(CreeMen[1 + i], Role.CreeHunter, Faction.Cree, tipis[1 + i % 4], IsoGrid.Cell(WorldLayout.CampCentre, -1, 1), band, _creeFrames, new Color(0.95f, 0.92f, 0.9f));
			h.Inventory.Add(i % 2 == 0 ? ItemId.Bow : ItemId.Musket); h.Inventory.Add(ItemId.Knife); h.Inventory.Add(ItemId.HideCoat); h.Inventory.Add(ItemId.Venison, 2); h.Inventory.Add(ItemId.Snowshoes);
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

		// The Nor'Westers across the river: a wintering partner and his men.
		var nwc = Sim.Nwc; var house = WorldLayout.NwcHouse;
		if (nwc != null)
		{
			var partner = Spawn("Mr. Duncan Cameron (NWC)", Role.NwcPartner, Faction.NWC, IsoGrid.Cell(house.Top, 3, 1), IsoGrid.Cell(house.Top, 3, 2), nwc, _factorFrames, new Color(1f, 0.9f, 0.9f));
			partner.Inventory.Add(ItemId.Musket); partner.Inventory.Add(ItemId.Capote); partner.Inventory.Add(ItemId.Pemmican, 2);
			nwc.Leader = partner;
			string[] voyageurs = { "Baptiste Lagimodiere", "Pierre Falcon", "Antoine Desjarlais" };
			for (int i = 0; i < 3; i++)
			{
				var v = Spawn(voyageurs[i], Role.Voyageur, Faction.NWC, IsoGrid.Cell(house.Top, 2 + i, 3), IsoGrid.Cell(WorldLayout.NwcTop, 6, 5), nwc, _factorFrames, new Color(0.85f, 0.75f, 0.75f));
				v.Inventory.Add(ItemId.Capote); v.Inventory.Add(ItemId.Knife); v.Inventory.Add(ItemId.Pemmican, 2);
				if (i == 0) v.Inventory.Add(ItemId.Musket);
			}
		}

		// The Assiniboine (design doc 7.2): a plains band wintering upland
		// near Oxford House with pemmican to sell, down to the Bay in summer.
		var assin = Sim.Assiniboine;
		if (assin != null)
		{
			var at = new List<Vector2I>(assin.Tipis());
			var achief = Spawn(AssinMen[0] + " (chief)", Role.CreeChief, Faction.Assiniboine, at[0], IsoGrid.Cell(assin.Camp, 1, 0), assin, _assiniboineFrames, Colors.White);
			achief.Inventory.Add(ItemId.Musket); achief.Inventory.Add(ItemId.HideCoat); achief.Inventory.Add(ItemId.Pemmican, 3);
			assin.Leader = achief;
			var ahunters = new List<Npc>();
			for (int i = 0; i < 3; i++)
			{
				var h = Spawn(AssinMen[1 + i], Role.CreeHunter, Faction.Assiniboine, at[1 + i], IsoGrid.Cell(assin.Camp, -1, 1), assin, _assiniboineFrames, new Color(0.96f, 0.93f, 0.9f));
				h.Inventory.Add(i == 0 ? ItemId.Musket : ItemId.Bow); h.Inventory.Add(ItemId.Knife); h.Inventory.Add(ItemId.HideCoat); h.Inventory.Add(ItemId.Pemmican, 3); h.Inventory.Add(ItemId.Snowshoes);
				ahunters.Add(h);
			}
			for (int i = 0; i < 3; i++)
			{
				var w = Spawn(AssinWomen[i], Role.CreeWoman, Faction.Assiniboine, at[i % at.Count], IsoGrid.Cell(assin.Camp, 0, 1), assin, _creeWomanFrames, new Color(0.9f, 0.78f, 0.62f), 0.78f, true);
				w.Inventory.Add(ItemId.HideCoat); w.Inventory.Add(ItemId.Knife);
				if (i == 0) { w.Spouse = achief; achief.Spouse = w; } else { w.Spouse = ahunters[i - 1]; ahunters[i - 1].Spouse = w; }
			}
			var ad = Spawn("Winona", Role.CreeDaughter, Faction.Assiniboine, at[0], IsoGrid.Cell(assin.Camp, 1, 2), assin, _creeWomanFrames, new Color(0.95f, 0.85f, 0.7f), 0.74f, true);
			ad.Father = achief; achief.Children.Add(ad); ad.Inventory.Add(ItemId.HideCoat);
		}

		// Oxford House, up the river road: a postmaster, three men, a wife.
		var outpost = Sim.Outpost; var oh = WorldLayout.OutpostHouse;
		if (outpost != null)
		{
			var pm = Spawn("Mr. James Sutherland (postmaster)", Role.Postmaster, Faction.HBC, IsoGrid.Cell(oh.Top, 3, 1), IsoGrid.Cell(oh.Top, 2, 2), outpost, _factorFrames, new Color(0.95f, 0.95f, 1f));
			pm.Inventory.Add(ItemId.Musket); pm.Inventory.Add(ItemId.Capote); pm.Inventory.Add(ItemId.Pemmican, 2);
			outpost.Leader = pm;
			for (int i = 0; i < 3; i++)
			{
				var m = Spawn(OrkneyMen[6 + i], Role.Labourer, Faction.HBC, IsoGrid.Cell(oh.Top, 2 + i, 3), IsoGrid.Cell(WorldLayout.OutpostTop, 5 + i, 5), outpost, _labourerFrames, Colors.White);
				m.Inventory.Add(ItemId.Capote); m.Inventory.Add(ItemId.Knife); m.Inventory.Add(ItemId.Pemmican, 2); m.Inventory.Add(ItemId.Snowshoes);
				if (i == 0) m.Inventory.Add(ItemId.Musket);
			}
			var ow = Spawn(Women[3], Role.Woman, Faction.HBC, IsoGrid.Cell(oh.Top, 5, 3), IsoGrid.Cell(oh.Top, 4, 2), outpost, _hbcFrames, new Color(1f, 0.8f, 0.85f), 0.72f, true);
			ow.Inventory.Add(ItemId.PointBlanket); ow.Inventory.Add(ItemId.Knife);
		}

		Sim.Notice($"York Factory: {Sim.Npcs.Count} souls in the world.");
	}

	// Fever spreads between people who stand together (design doc 13.5),
	// and everyone is a year older each New Year (design doc 9.4).
	private float _contagionTimer;
	private int _lastYear = -1;
	private void SicknessAndYears(float hours)
	{
		var cal = GameCalendar.Instance;
		if (cal == null) return;
		if (_lastYear < 0) _lastYear = cal.Year;
		if (cal.Year != _lastYear)
		{
			_lastYear = cal.Year;
			foreach (var n in Sim.Npcs) if (!n.IsDead) { n.Age++; n.Immune = false; if (n.Age > 45) n.MoveSpeed = Mathf.Max(80f, n.MoveSpeed * 0.97f); }
			foreach (var p in Sim.Players()) { p.Age++; p.Immune = false; }
		}
		_contagionTimer -= hours;
		if (_contagionTimer > 0f) return;
		_contagionTimer = 1f;
		var everyone = new List<Agent>();
		foreach (var n in Sim.Npcs) if (!n.IsDead) everyone.Add(n);
		foreach (var p in Sim.Players()) if (!p.IsDead) everyone.Add(p);
		foreach (var a in everyone)
		{
			if (!a.IsSick) continue;
			foreach (var b in everyone)
			{
				if (b == a || b.IsSick || b.Immune) continue;
				if (a.GlobalPosition.DistanceTo(b.GlobalPosition) > 130f) continue;
				float chance = b.IsSheltered ? 0.10f : 0.05f;
				if (GD.Randf() < chance) b.CatchFever();
			}
		}
	}

	// A household whose food runs out goes hungry together, and remembers it
	// together (design doc P.9). Once a day; a spell is remembered once a fortnight.
	private int _hungerCheckDay = -1;
	private readonly System.Collections.Generic.Dictionary<Household, int> _hungrySince = new();
	private void HungrySpells()
	{
		int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
		if (day == _hungerCheckDay) return;
		_hungerCheckDay = day;
		foreach (var h in Chronicle.Households())
		{
			int mouths = 0;
			foreach (var n in Sim.Npcs) if (n.HomeStores == h && !n.IsDead) mouths++;
			if (mouths == 0 || h.FoodDays(mouths) >= 1f) continue;
			if (_hungrySince.TryGetValue(h, out int last) && Mathf.PosMod(day - last, 365) < 14) continue;
			_hungrySince[h] = day;
			Chronicle.Shared(h, "the food ran out and everyone went hungry", 1.3f);
		}
	}

	// Dev aid: `-- --trace` prints everyone's goal and needs every 2 game hours.
	private bool _trace;
	private float _traceTimer;
	public override void _Process(double delta)
	{
		if (Net.Net.IsServer) { SicknessAndYears(GameCalendar.Instance?.DeltaGameHours ?? 0f); Tiers.Tick((float)delta); HungrySpells(); Losses.Tick(); }
		if (!_trace) return;
		_traceTimer -= GameCalendar.Instance?.DeltaGameHours ?? 0f;
		if (_traceTimer > 0f) return;
		_traceTimer = 1f;
		var cal = GameCalendar.Instance;
		var sb = new System.Text.StringBuilder($"[trace] {cal.DateString} {cal.TimeString} fps={Performance.GetMonitor(Performance.Monitor.TimeFps):0} paths={Pathfinder.Calls} ({Pathfinder.TotalMs:0} ms, {Pathfinder.SlowCalls} slow) nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
		sb.AppendLine();
		foreach (var n in Sim.Npcs)
			sb.AppendLine($"  {n.DisplayName,-24} {n.Cell} {(n.IsDead ? "DEAD" : n.GoalDescription)}  H{n.Needs.Hunger:0} W{n.Needs.Warmth:0} T{n.Needs.Thirst:0} HP{n.Needs.Health:0}");
		sb.Append($"  {Tiers.Report()}; animals live: {Sim.Animals.Count}; fort furs {Sim.Fort?.FursInStoreMb:0} MB; goods {Sim.Fort?.Stores.Count(ItemId.TradeGoods)}; ship {Sim.Ship?.Status()}; band cache: {Sim.Band?.Stores.Summary()}");
		GD.Print(sb.ToString());
	}

	public Npc Spawn(string name, Role role, Faction faction, Vector2I bed, Vector2I work, Household home, SpriteFrames frames, Color tint, float scale = 0.8f, bool female = false)
	{
		var npc = NpcScene.Instantiate<Npc>();
		npc.DisplayName = name;
		npc.Role = role;
		npc.Age = role switch
		{
			Role.Factor => GD.RandRange(46, 58), Role.NwcPartner => GD.RandRange(35, 50), Role.CreeChief => GD.RandRange(40, 55),
			Role.Clerk => GD.RandRange(19, 28), Role.CreeDaughter => GD.RandRange(16, 20), Role.Sailor => GD.RandRange(18, 40),
			_ => GD.RandRange(20, 38),
		};
		npc.Faction = faction;
		npc.Skills.Seed(
			role is Role.CreeHunter or Role.CreeChief ? (float)GD.RandRange(0.5, 0.8) : role is Role.Labourer or Role.Voyageur ? (float)GD.RandRange(0.15, 0.4) : 0.1f,
			role is Role.CreeHunter or Role.CreeChief ? (float)GD.RandRange(0.5, 0.85) : role == Role.Voyageur ? 0.35f : (float)GD.RandRange(0.1, 0.35),
			role is Role.Factor or Role.Clerk or Role.NwcPartner or Role.Postmaster or Role.CreeChief ? (float)GD.RandRange(0.5, 0.8) : 0.15f,
			role is Role.Labourer or Role.Voyageur or Role.CreeHunter or Role.CreeWoman or Role.Woman ? (float)GD.RandRange(0.3, 0.6) : 0.15f);
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

	// The Company recruited in Orkney every year (design doc 5.5): the ship
	// lands enough new hands to bring the post back to strength.
	public int LandRecruits(int wanted)
	{
		var fort = Sim.Fort; var mens = WorldLayout.MensHouse;
		if (fort == null) return 0;
		int alive = 0;
		var taken = new HashSet<string>();
		foreach (var n in Sim.Npcs) { if (!n.IsDead) taken.Add(n.DisplayName); if (n.HomeStores == fort && n.Role == Role.Labourer && !n.IsDead) alive++; }
		int landed = 0;
		for (int i = 0; alive + landed < wanted && i < OrkneyMen.Length; i++)
		{
			if (taken.Contains(OrkneyMen[i])) continue;
			int slot = alive + landed;
			var bed = slot < 5 ? IsoGrid.Cell(mens.Top, 1 + slot, 2) : IsoGrid.Cell(mens.Top, 1 + slot % 5, 3);
			var m = Spawn(OrkneyMen[i], Role.Labourer, Faction.HBC, bed, IsoGrid.Cell(WorldLayout.FortTop, 8 + slot, 8), fort, _labourerFrames, Colors.White);
			m.Inventory.Add(ItemId.Capote); m.Inventory.Add(ItemId.Knife); m.Inventory.Add(ItemId.Bread, 2);
			m.Age = GD.RandRange(17, 24);
			m.GlobalPosition = IsoGrid.ToWorld(WorldLayout.WharfEnd) + new Vector2(GD.RandRange(-40, 40), 30 + i * 10);
			m.Brain.Push(m, new Agents.Goals.ReportForDutyGoal());   // design doc 5.5: his first order
			landed++;
		}
		if (landed > 0) Sim.Notice($"{landed} new hands off the ship from Stromness: {(landed == 1 ? "a green lad" : "green lads")} with a five-year contract.");
		return landed;
	}

	// Fort William sends men north with the brigade the same way.
	public int LandVoyageurs(int wanted)
	{
		var nwc = Sim.Nwc; var house = WorldLayout.NwcHouse;
		if (nwc == null) return 0;
		string[] names = { "Baptiste Lagimodiere", "Pierre Falcon", "Antoine Desjarlais", "Joseph Cadotte", "Louis Nolin", "Francois Deschamps", "Jean-Baptiste Lajimoniere" };
		int alive = 0;
		var taken = new HashSet<string>();
		foreach (var n in Sim.Npcs) { if (!n.IsDead) taken.Add(n.DisplayName); if (n.Faction == Faction.NWC && n.Role == Role.Voyageur && !n.IsDead) alive++; }
		int landed = 0;
		for (int i = 0; alive + landed < wanted && i < names.Length; i++)
		{
			if (taken.Contains(names[i])) continue;
			int slot = alive + landed;
			var v = Spawn(names[i], Role.Voyageur, Faction.NWC, IsoGrid.Cell(house.Top, 2 + slot % 3, 3), IsoGrid.Cell(WorldLayout.NwcTop, 6, 5), nwc, _factorFrames, new Color(0.85f, 0.75f, 0.75f));
			v.Inventory.Add(ItemId.Capote); v.Inventory.Add(ItemId.Knife); v.Inventory.Add(ItemId.Pemmican, 2);
			v.Brain.Push(v, new Agents.Goals.ReportForDutyGoal());
			landed++;
		}
		if (landed > 0) Sim.Notice($"{landed} voyageurs came north with the brigade to fill the Nor'Westers' ranks.");
		return landed;
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
