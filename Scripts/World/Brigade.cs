using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// The brigade (design doc 12.1, 12.2, 18.3): the outfit goes inland after the
// ship and the returns come down in spring, by water, in company. A brigade
// is men, canoes and a manifest moving along the river road - the open water
// resolved in numbers, and the portages where things actually happen to it.
//
// A portage is the chokepoint: the canoes come out of the water, the pieces go
// over on men's backs two at a time, and anyone who means to stop you knows
// exactly where you will be. Hulls wear on the rocks and the drag; spruce gum
// holds them together; a hull that fails with no gum left is an emergency.
public class Brigade
{
	public enum State { Paddling, Portaging, Camped, Arrived, Broken }

	public string Name = "the brigade";
	public FortLedger From, To;
	public bool Upstream;                        // against the current, half the pace
	public readonly List<string> Crew = new();   // by name, for the manifest and the save
	public readonly List<Agents.Npc> Men = new();
	public readonly Inventory Cargo = new();
	public readonly Inventory Provisions = new();   // what the canoes eat on the way
	public bool Homeward;                           // the men coming back empty
	public float Along;                          // 0..1 of the road
	public float Condition = 1f;                 // hulls and pitch
	public int GumLeft = 2;                      // patches they carry
	public State Now = State.Paddling;
	public int DayLeft;                          // days still owed to the portage or the repair
	public string At = "";                       // the portage they are on, if any
	public int DaysOut;
	public bool PlayerWith;                      // the player went with them

	// The carrying places of the Hayes, as the brigade meets them going up.
	// Read from the world atlas where it has them (their positions there are
	// approximate and want correcting from a source); the fractions below are
	// the fallback if the atlas is silent.
	private static (float at, string name, bool heavy)[] _portages;
	public static void ForgetPortages() => _portages = null;
	public static (float at, string name, bool heavy)[] Portages
	{
		get
		{
			if (_portages != null) return _portages;
			var fromMap = WorldMap.Portages();
			if (fromMap.Count > 0)
			{
				var list = new List<(float, string, bool)>();
				foreach (var (name, fraction, _) in fromMap)
					list.Add((fraction, name, name.Contains("Falls") || name.Contains("Gates")));
				list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
				_portages = list.ToArray();
			}
			else
				_portages = new (float, string, bool)[]
				{
					(0.218f, "the Rock portage", false),
					(0.367f, "Borrowick Falls", false),
					(0.499f, "Trout Falls", true),
					(0.634f, "the Hill Gates", true),
				};
			return _portages;
		}
	}

	public Vector2 Where
	{
		get
		{
			var road = WorldLayout.RiverRoad;
			float t = Mathf.Clamp(Upstream ? Along : 1f - Along, 0f, 1f) * (road.Length - 1);
			int i = Mathf.Clamp((int)t, 0, road.Length - 2);
			return road[i].Lerp(road[i + 1], t - i);
		}
	}

	public string Describe() => Now switch
	{
		State.Portaging => $"{Name} is over the packs at {At}",
		State.Broken => $"{Name} is held up with a hull to mend",
		State.Camped => $"{Name} is camped on the bank",
		State.Arrived => $"{Name} is in at {To?.Title}",
		_ => $"{Name} is on the water, {Mathf.RoundToInt(Along * 100f)} of the way",
	};
}

public static class Brigades
{
	public static readonly List<Brigade> All = new();
	private static int _lastDay = -1;

	// A new world may have had its road and carrying places re-laid. A brigade
	// in flight is not yet saved with the world (that wants a block in
	// SaveGame), so rather than let a load destroy a loaded canoe, the pieces
	// go back on the books of the house that sent them.
	public static void Reset()
	{
		foreach (var b in All)
		{
			if (b.From == null || b.Now == Brigade.State.Arrived) continue;
			int lots = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(b.Cargo.All())) { b.Cargo.Remove(id, n); b.From.Stores.Add(id, n); lots += n; }
			foreach (var (id, n) in new List<(ItemId, int)>(b.Provisions.All())) { b.Provisions.Remove(id, n); b.From.Stores.Add(id, n); }
			foreach (var n in b.Men) PutAshore(n, b.From);
			if (lots > 0) Sim.Notice($"{b.Name} was never sent after all: {lots} pieces back on {b.From.Title}'s books.");
		}
		All.Clear();
		_lastDay = -1;
		_outfitYear = _returnsYear = -1;
		_devSent = false;
		Brigade.ForgetPortages();
	}

	// Load and send one. The men go on the books as away, not as bodies on the
	// water: a brigade on the open river is exactly what the tiers are for.
	public static Brigade Send(FortLedger from, FortLedger to, string name, bool upstream, int men, List<(ItemId, int)> manifest)
	{
		if (from == null || to == null) return null;
		var b = new Brigade { Name = name, From = from, To = to, Upstream = upstream };
		foreach (var (id, n) in manifest)
		{
			int got = from.Stores.TakeUpTo(id, n);
			if (got > 0) b.Cargo.Add(id, got);
		}
		if (b.Cargo.TotalWeightLbs < 1f) { Sim.Notice($"{from.Title} has nothing to send inland."); return null; }
		// The men: whoever can be spared. They go off the post's books for the
		// whole journey - no orders, no rations from the store, not at the fire -
		// which is what makes sending a brigade cost something.
		int taken = 0;
		foreach (var n in Sim.Npcs)
		{
			if (taken >= men) break;
			if (n.IsDead || n.Away || n.HomeStores != from) continue;
			if (n.Role != Role.Labourer && n.Role != Role.Voyageur) continue;
			b.Crew.Add(n.DisplayName);
			b.Men.Add(n);
			TakeAway(n, b);
			taken++;
		}
		// Provisions for the way: a man eats whatever is in the canoe, and a
		// brigade that set out light goes hungry on the water.
		int wantFood = Mathf.Max(8, taken * 22);
		foreach (var id in new[] { ItemId.Pemmican, ItemId.SaltGeese, ItemId.DriedGoose, ItemId.DriedMeat })
		{
			if (b.Provisions.FoodNutritionTotal >= wantFood * 35f) break;
			int got = from.Stores.TakeUpTo(id, id == ItemId.SaltGeese ? Mathf.Max(1, taken / 2) : wantFood);
			if (got > 0) b.Provisions.Add(id, got);
		}
		b.GumLeft = 1 + taken / 3;
		All.Add(b);
		int lots = 0; foreach (var (_, n) in b.Cargo.All()) lots += n;
		Sim.Headline($"{name} sets off {(upstream ? "up" : "down")} the river: {taken} men, {lots} pieces, {Brigade.Portages.Length} portages between here and {to.Title}.");
		Chronicle.Event($"{name} left {from.Title} for {to.Title} with {lots} pieces.", IsoGrid.ToWorld(from.AccessCell), null, name, 1.2f);
		return b;
	}

	// Off the post's books, and his body with the canoes.
	private static void TakeAway(Agents.Npc n, Brigade b)
	{
		n.Away = true;
		n.AwayWith = b.Name;
		n.Tier = Core.Tier.Background;
		n.Brain.Clear(n);
		n.StopMoving();
		n.Velocity = Vector2.Zero;
		n.AssignedTask = null;
		n.AssignedTaskName = $"away with {b.Name}";
		n.Visible = false;
		n.ProcessMode = Node.ProcessModeEnum.Disabled;
		Net.Net.I?.AnnounceTier(n, true);
	}

	// Landed: he is a man at a post again, wherever the brigade has put him.
	private static void PutAshore(Agents.Npc n, Economy.FortLedger at)
	{
		if (n == null || !GodotObject.IsInstanceValid(n)) return;
		n.Away = false;
		n.AwayWith = "";
		n.AssignedTaskName = "";
		if (at != null) n.GlobalPosition = IsoGrid.ToWorld(n.StandCellNear(at.AccessCell));
		n.Visible = true;
		n.ProcessMode = Node.ProcessModeEnum.Inherit;
		Net.Net.I?.AnnounceTier(n, false);
	}

	// A day of the journey, resolved where nobody can see it and dropped to
	// full fidelity at a portage (design doc 18.3).
	public static void Tick()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer || cal.Day == _lastDay) return;
		if (cal.Day < _lastDay) Reset();   // a new world, or a save put the clock back
		_lastDay = cal.Day;
		DevSend();
		// Anyone walking with the brigade is with it, and hears everything at
		// first hand (design doc 18.3).
		foreach (var br in All)
			foreach (var p in Sim.Players())
				if (p != null && !p.IsDead && p.GlobalPosition.DistanceTo(br.Where) < 1400f) br.PlayerWith = true;
		foreach (var b in new List<Brigade>(All))
		{
			if (b.Now == Brigade.State.Arrived) { All.Remove(b); continue; }
			b.DaysOut++;
			Feed(b);
			foreach (var n in b.Men)
				if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead) n.GlobalPosition = b.Where;
			switch (b.Now)
			{
				case Brigade.State.Portaging:
				case Brigade.State.Broken:
					if (--b.DayLeft > 0) continue;
					b.Now = Brigade.State.Paddling;
					b.At = "";
					continue;
				default:
					Paddle(b, cal);
					continue;
			}
		}
	}

	// A day's rations out of the canoe. A brigade with nothing left lives on
	// what the country gives, which is not much on the water.
	private static void Feed(Brigade b)
	{
		bool short_ = false;
		// A brigade fishes and snares at the carrying places and the camps, as
		// voyageurs did: thin, and not enough on its own for a long journey.
		if (b.Men.Count > 0 && b.Provisions.FoodNutritionTotal < b.Men.Count * 40f && GD.Randf() < 0.7f)
			b.Provisions.Add(ItemId.Fish, 1 + b.Men.Count / 3);
		foreach (var n in b.Men)
		{
			if (n == null || !GodotObject.IsInstanceValid(n) || n.IsDead) continue;
			bool ate = false;
			foreach (var id in new[] { ItemId.Pemmican, ItemId.DriedGoose, ItemId.DriedMeat, ItemId.Hare, ItemId.Fish })
				if (b.Provisions.Remove(id)) { n.Needs.Hunger = 100f; ate = true; break; }
			if (!ate && b.Provisions.Remove(ItemId.SaltGeese)) { b.Provisions.Add(ItemId.DriedGoose, 7); n.Needs.Hunger = 100f; ate = true; }
			if (ate) { n.Needs.Thirst = 100f; n.Needs.Warmth = Mathf.Max(40f, n.Needs.Warmth); continue; }
			n.Needs.Hunger = Mathf.Max(0f, n.Needs.Hunger - 26f);
			n.Needs.Thirst = 100f;   // there is no want of water on a river
			if (n.Needs.Hunger <= 0f) n.Needs.Health = Mathf.Max(0f, n.Needs.Health - 4f);
			if (n.Needs.Health <= 0f) { Sim.Notice($"{n.DisplayName} died on the water with {b.Name}."); n.Away = false; n.DieQuietly(); }
			else short_ = true;
		}
		if (short_ && GD.Randf() < 0.3f)
			Sim.Notice($"{b.Name} is out of provisions: the men are on short commons on the water.");
	}

	private static void Paddle(Brigade b, GameCalendar cal)
	{
		// Freeze-up ends the season: what is not in is wintered where it lies.
		if (cal.IsFrozen)
		{
			if (b.Now != Brigade.State.Camped)
			{
				b.Now = Brigade.State.Camped;
				Sim.Headline($"{b.Name} is frozen in short of {b.To?.Title}: the pieces winter on the bank.");
			}
			return;
		}

		float pace = (b.Upstream ? 0.055f : 0.095f) * Mathf.Lerp(0.6f, 1.1f, Mathf.Clamp(b.Crew.Count / 6f, 0.2f, 1f));
		if (b.Cargo.TotalWeightLbs > 240f * Mathf.Max(1, b.Crew.Count / 2)) pace *= 0.75f;
		if (cal.Weather.Current is SkyState.Storm or SkyState.Blizzard) pace *= 0.3f;
		float was = b.Along;
		b.Along = Mathf.Clamp(b.Along + pace, 0f, 1f);

		// Rapids wear the hulls; gum holds them (design doc 12.2).
		if (GD.Randf() < 0.3f) b.Condition -= (float)GD.RandRange(0.03, 0.09);
		if (b.Condition < 0.4f)
		{
			if (b.GumLeft > 0)
			{
				b.GumLeft--;
				b.Condition = Mathf.Min(1f, b.Condition + 0.35f);
				b.Now = Brigade.State.Broken; b.DayLeft = 1;
				Sim.Notice($"{b.Name} lies up a day gumming a hull - {b.GumLeft} patch{(b.GumLeft == 1 ? "" : "es")} left.");
				return;
			}
			// No gum: a canoe is lost, and what was in it with her.
			int lost = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(b.Cargo.All()))
			{
				int take = Mathf.Max(1, n / 3);
				b.Cargo.Remove(id, take); lost += take;
			}
			b.Condition = 0.6f;
			b.Now = Brigade.State.Broken; b.DayLeft = 2;
			Sim.Headline($"A canoe of {b.Name} is stove in with no gum left: {lost} pieces gone in the river.");
			Tell(b, $"{lost} pieces of {b.Name} went down in the rapids.", "a hull opened with no gum left to mend it", 1.6f);
			return;
		}

		// Portages: the one place a brigade can be caught (design doc 12.2, 15.1).
		foreach (var (at, name, heavy) in Brigade.Portages)
		{
			float mark = b.Upstream ? at : 1f - at;
			bool crossed = b.Upstream ? was < mark && b.Along >= mark : was < 1f - at && b.Along >= 1f - at;
			if (!crossed) continue;
			b.Now = Brigade.State.Portaging;
			b.At = name;
			b.DayLeft = heavy ? 2 : 1;
			b.Condition -= heavy ? 0.1f : 0.05f;
			Sim.Notice($"{b.Name} is at {name}: the canoes out of the water and the pieces over on men's backs{(heavy ? ", two days of it" : "")}.");
			Chronicle.Event($"{b.Name} was at {name} with the pieces on the bank.", b.Where, null, b.Name, 0.8f);
			Interception(b, name);
			return;
		}

		if (b.Along >= 1f) Arrive(b);
	}

	// Who knew where you would be, and what they did about it. The chance is
	// the standing bad blood between the houses; nothing is invented - the men
	// who do it are the other post's own.
	private static void Interception(Brigade b, string portage)
	{
		float tension = Rivalry.Tension;
		var rival = b.From != null && b.From.Company == Faction.HBC ? (FortLedger)Sim.Nwc : Sim.Fort;
		if (rival == null || tension < 45f) return;
		float chance = Mathf.Clamp((tension - 40f) / 150f, 0f, 0.4f);
		if (rival.KnowsRivalPlan) chance += 0.15f;
		if (!Director.MayActNow("portage", 2f, b.Where)) return;
		if (GD.Randf() > chance) return;
		Director.Saw("portage", 2f);

		int lots = 0;
		foreach (var (id, n) in new List<(ItemId, int)>(b.Cargo.All()))
		{
			int take = Mathf.Max(1, n / 2);
			b.Cargo.Remove(id, take); rival.Stores.Add(id, take); lots += take;
		}
		if (b.From != null) { b.From.LotsLostToRaids += lots; b.From.LostToRivalMb += lots * 0.5f; }
		Rivalry.Raise(8f, $"{(rival.Company == Faction.NWC ? "the Nor'Westers" : "the Company's men")} took {b.Name} at {portage}");
		Sim.Headline($"{b.Name} was taken at {portage}: {lots} pieces carried off by {(rival.Company == Faction.NWC ? "the Nor'Westers" : "the Company's men")}.");
		Tell(b, $"{b.Name} was waylaid at {portage} and lost {lots} pieces.", "the other house knew which day you would be on the carrying place", 2f);
		Chronicle.Event($"{(rival.Company == Faction.NWC ? "the Nor'Westers" : "the Company's men")} waylaid {b.Name} at {portage} and took {lots} pieces.", b.Where, null, b.Name, 2f);
	}

	private static void Arrive(Brigade b)
	{
		b.Now = Brigade.State.Arrived;
		int lots = 0;
		foreach (var (id, n) in new List<(ItemId, int)>(b.Cargo.All()))
		{
			b.Cargo.Remove(id, n); b.To.Stores.Add(id, n); lots += n;
		}
		if (b.To is OutpostLedger op) op.LastSupplyDay = GameCalendar.Instance?.Day ?? 0;
		foreach (var n in b.Men) PutAshore(n, b.To);
		// The men whose post is the other end go back down in their own time,
		// empty, and are off the books until they land there.
		if (!b.Homeward && b.From != null && b.Men.Count > 0)
		{
			var home = new Brigade
			{
				Name = b.Upstream ? "the outfit's men, homeward" : "the returns' men, homeward",
				From = b.To, To = b.From, Upstream = !b.Upstream, Homeward = true,
			};
			foreach (var n in b.Men)
			{
				if (n == null || !GodotObject.IsInstanceValid(n) || n.IsDead || n.HomeStores != b.From) continue;
				home.Crew.Add(n.DisplayName); home.Men.Add(n); TakeAway(n, home);
			}
			if (home.Men.Count > 0)
			{
				int rations = Mathf.Max(6, home.Men.Count * 16);
				foreach (var id in new[] { ItemId.Pemmican, ItemId.DriedGoose, ItemId.DriedMeat })
					if (home.Provisions.FoodNutritionTotal < rations * 35f)
					{
						int got = b.To.Stores.TakeUpTo(id, rations);
						if (got > 0) home.Provisions.Add(id, got);
					}
				home.GumLeft = 1 + home.Men.Count / 3;
				All.Add(home);
				Sim.Notice($"{home.Men.Count} of {b.Name}'s men start back down the river for {b.From.Title}.");
			}
		}
		Sim.Headline($"{b.Name} is in at {b.To.Title}: {lots} pieces landed after {b.DaysOut} days and {Brigade.Portages.Length} portages.");
		Tell(b, $"{b.Name} landed {lots} pieces at {b.To.Title}.", $"{b.DaysOut} days up the river", 1.2f);
		Chronicle.Event($"{b.Name} came in to {b.To.Title} with {lots} pieces.", IsoGrid.ToWorld(b.To.AccessCell), null, b.Name, 1.2f);
	}

	// What reaches the player: he hears of his own brigade at once if he went
	// with it, and otherwise when the word comes down the river.
	private static void Tell(Brigade b, string what, string cause, float weight)
	{
		foreach (var p in Sim.Players())
		{
			if (p == null || p.IsDead) continue;
			bool his = b.PlayerWith || b.From != null && b.From.Company == p.Faction;
			if (!his) continue;
			Reveal.Word(p, what, cause, weight);
		}
	}

	// ---- the world's own record -------------------------------------------
	// A brigade in flight is part of the world: where she is, what she carries,
	// what the men have left to eat, and which men they are. Saved and loaded
	// like anything else, so a journey survives a quicksave on the water.
	public static Godot.Collections.Array Save()
	{
		var a = new Godot.Collections.Array();
		foreach (var b in All)
		{
			var crew = new Godot.Collections.Array();
			foreach (string name in b.Crew) crew.Add(name);
			var cargo = new Godot.Collections.Dictionary();
			foreach (var (id, n) in b.Cargo.All()) cargo[id.ToString()] = n;
			var food = new Godot.Collections.Dictionary();
			foreach (var (id, n) in b.Provisions.All()) food[id.ToString()] = n;
			a.Add(new Godot.Collections.Dictionary
			{
				["name"] = b.Name, ["from"] = Key(b.From), ["to"] = Key(b.To),
				["up"] = b.Upstream, ["home"] = b.Homeward, ["along"] = b.Along,
				["cond"] = b.Condition, ["gum"] = b.GumLeft, ["state"] = (int)b.Now,
				["dayLeft"] = b.DayLeft, ["at"] = b.At, ["out"] = b.DaysOut,
				["with"] = b.PlayerWith, ["crew"] = crew, ["cargo"] = cargo, ["food"] = food,
			});
		}
		return a;
	}

	public static void Load(Godot.Collections.Array saved)
	{
		All.Clear();
		_lastDay = -1;
		_devSent = true;   // a loaded world has already had whatever was sent
		Brigade.ForgetPortages();
		if (saved == null) return;
		foreach (var v in saved)
		{
			var d = v.AsGodotDictionary();
			var b = new Brigade
			{
				Name = d["name"].AsString(),
				From = Post(d["from"].AsString()), To = Post(d["to"].AsString()),
				Upstream = d["up"].AsBool(), Homeward = d.ContainsKey("home") && d["home"].AsBool(),
				Along = (float)d["along"].AsDouble(), Condition = (float)d["cond"].AsDouble(),
				GumLeft = d["gum"].AsInt32(), Now = (Brigade.State)d["state"].AsInt32(),
				DayLeft = d["dayLeft"].AsInt32(), At = d["at"].AsString(),
				DaysOut = d["out"].AsInt32(), PlayerWith = d["with"].AsBool(),
			};
			foreach (var c in d["cargo"].AsGodotDictionary())
				if (System.Enum.TryParse<ItemId>(c.Key.AsString(), out var cid)) b.Cargo.Add(cid, c.Value.AsInt32());
			if (d.ContainsKey("food"))
				foreach (var c in d["food"].AsGodotDictionary())
					if (System.Enum.TryParse<ItemId>(c.Key.AsString(), out var fid)) b.Provisions.Add(fid, c.Value.AsInt32());
			foreach (var cv in d["crew"].AsGodotArray())
			{
				string name = cv.AsString();
				b.Crew.Add(name);
				foreach (var n in Sim.Npcs)
					if (!n.IsDead && n.DisplayName == name) { b.Men.Add(n); TakeAway(n, b); break; }
			}
			if (b.From != null && b.To != null) All.Add(b);
		}
		if (All.Count > 0) Sim.Notice($"{All.Count} brigade{(All.Count == 1 ? " is" : "s are")} still on the river.");
	}

	private static string Key(Economy.FortLedger f) =>
		f == null ? "" : f == Sim.Fort ? "fort" : f == (Economy.FortLedger)Sim.Nwc ? "nwc" : f == (Economy.FortLedger)Sim.Outpost ? "outpost" : "";

	private static Economy.FortLedger Post(string key) => key switch
	{
		"fort" => Sim.Fort,
		"nwc" => Sim.Nwc,
		"outpost" => Sim.Outpost,
		_ => null,
	};

	// Dev aid: `-- --brigade` sends the outfit up the moment the world starts,
	// whatever the date, to watch a journey and its carrying places.
	private static bool _devSent;
	private static void DevSend()
	{
		if (_devSent) return;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg != "--brigade") continue;
			_devSent = true;
			Send(Sim.Fort, Sim.Outpost, "the autumn outfit", true, 5, new List<(ItemId, int)>
			{
				(ItemId.TradeGoods, 40), (ItemId.Musket, 2), (ItemId.PointBlanket, 6), (ItemId.SteelTrap, 6),
			});
			GD.Print("[dev] a brigade is away up the river");
			return;
		}
		_devSent = true;
	}

	// The season's brigades, off the calendar: the outfit up once the ship has
	// been unloaded, the returns down as soon as the ice is out.
	private static int _outfitYear = -1, _returnsYear = -1;
	public static void Season()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer || Sim.Fort == null || Sim.Outpost == null) return;
		// Up the river in September, with the trade goods the ship brought.
		if (cal.Month == 9 && cal.Day >= 18 && _outfitYear != cal.Year)
		{
			_outfitYear = cal.Year;
			Send(Sim.Fort, Sim.Outpost, "the autumn outfit", true, 5, new List<(ItemId, int)>
			{
				(ItemId.TradeGoods, 40), (ItemId.Musket, 2), (ItemId.PointBlanket, 6),
				(ItemId.SteelTrap, 6), (ItemId.Kettle, 1), (ItemId.Rum, 4), (ItemId.SaltGeese, 6),
			});
		}
		// Down with the winter's furs once the river is open.
		if (cal.Month == 6 && cal.Day >= 8 && _returnsYear != cal.Year)
		{
			_returnsYear = cal.Year;
			Send(Sim.Outpost, Sim.Fort, "the spring returns", false, 4, new List<(ItemId, int)>
			{
				(ItemId.BeaverPelt, 60), (ItemId.WolfPelt, 10), (ItemId.CaribouHide, 8), (ItemId.ThinPelt, 10),
			});
		}
	}
}
