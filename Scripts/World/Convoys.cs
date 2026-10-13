using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.World;

// The country's brigades (design doc 12.1, 12.2, 18.3). The York-Oxford
// brigade (Brigade.cs) is one road, resolved in fractions; this is every
// other road. A run is a company's boats on a named route (Data/World/
// brigade_runs.json); each leg of it is a crossing on the year's schedule
// that takes real goods off one post's books and puts them on another's.
//
// Far from every player a convoy is one row of numbers resolved once a day
// - a distance along its road, a state, a cargo - which is the same lazy
// resolution the people get (Tiers.cs). Near a player, ConvoyView shows the
// same numbers as boats and men: the position is a function of the clock,
// so nothing jumps when the picture appears.
public enum ConvoyState { Paddling, Portaging, Calling, Held, Wintering, Arrived }

public sealed class CraftKind
{
	public string Name = "canoe", Sprite = "";
	public float Up = 50f, Down = 80f, Scale = 0.6f;   // km a day, against the current and with it
	public int LotsPerBoat = 70;
}

public sealed class ConvoyLeg
{
	public string From = "", To = "", Cargo = "outfit";
	public int Month, Day;
}

public sealed class ConvoyRun
{
	public string Name = "";
	public Faction Company = Faction.HBC;
	public CraftKind Craft = new();
	public int Boats = 3, Men = 7;
	public readonly List<string> Stops = new();
	public readonly List<ConvoyLeg> Legs = new();
}

// A place on the road where the brigade stops: a carrying place, or a post it calls at.
public sealed class RoadEvent
{
	public float At;
	public string Name = "";
	public bool Call;       // a post it lies at a night, not a portage
	public int Days = 1;
}

public sealed class RoadPath
{
	public Vector2[] Pts = Array.Empty<Vector2>();
	public float[] Cum = Array.Empty<float>();
	public float Length;
	public readonly List<RoadEvent> Events = new();

	public void Measure()
	{
		Cum = new float[Pts.Length];
		for (int i = 1; i < Pts.Length; i++) Cum[i] = Cum[i - 1] + Pts[i - 1].DistanceTo(Pts[i]);
		Length = Pts.Length > 0 ? Cum[^1] : 0f;
	}

	public Vector2 At(float s)
	{
		if (Pts.Length == 0) return Vector2.Zero;
		if (s <= 0f) return Pts[0];
		if (s >= Length) return Pts[^1];
		int lo = 0, hi = Pts.Length - 1;
		while (hi - lo > 1) { int mid = (lo + hi) / 2; if (Cum[mid] <= s) lo = mid; else hi = mid; }
		float seg = Cum[hi] - Cum[lo];
		return seg < 0.01f ? Pts[lo] : Pts[lo].Lerp(Pts[hi], (s - Cum[lo]) / seg);
	}

	public Vector2 Dir(float s)
	{
		var a = At(s - 60f); var b = At(s + 60f);
		var d = b - a;
		return d.LengthSquared() < 1f ? Vector2.Right : d.Normalized();
	}

	// The same road walked the other way (a convoy turned back).
	public RoadPath Reversed()
	{
		var r = new RoadPath { Pts = new Vector2[Pts.Length] };
		for (int i = 0; i < Pts.Length; i++) r.Pts[i] = Pts[Pts.Length - 1 - i];
		r.Measure();
		foreach (var e in Events) r.Events.Add(new RoadEvent { At = Length - e.At, Name = e.Name, Call = e.Call, Days = e.Days });
		r.Events.Sort((a, b) => a.At.CompareTo(b.At));
		return r;
	}
}

public sealed class Convoy
{
	public int Id;
	public ConvoyRun Run;
	public ConvoyLeg Leg;
	public string FromName = "", ToName = "";
	public FortLedger From, To;
	public RoadPath Path;
	public bool Upstream, TurnedBack;
	public float Along, Step;               // px from the start; and how far today's paddling carries her
	public ConvoyState Now = ConvoyState.Paddling;
	public int DayLeft, NextEvent, DaysOut, DueDay, ShortDays;
	public string At = "";                  // the carrying place or post she lies at
	public bool Late;
	public int ArrivedDay;                  // boats lie at the landing for the rest of the day they come in
	public int Crew, Boats;
	public string Guide = "";
	public readonly Inventory Cargo = new(), Provisions = new();
	public int LotsAtStart;
	public Vector2 CampAt;                  // the bank she is drawn up on, when ashore
	public bool HasCamp;
	public readonly List<Npc> Men = new();  // the men you can see (ConvoyView); the rest are numbers

	public string Name => Run.Name;
	public Faction Company => Run.Company;
	public bool Moving => Now == ConvoyState.Paddling;

	public float AlongNow
	{
		get
		{
			if (Now != ConvoyState.Paddling || Step <= 0f) return Along;
			var cal = GameCalendar.Instance;
			float h = cal?.Hour ?? 6f;
			return Mathf.Min(Path.Length, Along + Step * Mathf.Clamp((h - 6f) / 12f, 0f, 1f));
		}
	}

	// Where the boats are, to the pixel: at the landing of the post she is at,
	// or along the road.
	public Vector2 Where
	{
		get
		{
			if (Now == ConvoyState.Calling) return Convoys.Landing(At);
			if (Now == ConvoyState.Arrived) return Convoys.Landing(ToName);
			float s = AlongNow;
			var p = Path.At(s);
			const float Blend = 2400f;
			if (s < Blend) p = Convoys.Landing(FromName).Lerp(p, s / Blend);
			else if (Path.Length - s < Blend) p = Convoys.Landing(ToName).Lerp(p, (Path.Length - s) / Blend);
			return p;
		}
	}

	public Vector2 Heading => Path.Dir(AlongNow);
	public float KmToGo => Mathf.Max(0f, Path.Length - Along) / WorldMap.PxPerKm;

	public int Lots { get { int n = 0; foreach (var (_, c) in Cargo.All()) n += c; return n; } }
	public string State => Now switch
	{
		ConvoyState.Portaging => $"over the packs at {At}",
		ConvoyState.Calling => $"lying at {At}",
		ConvoyState.Held => At != "" ? At : "held up on the bank",
		ConvoyState.Wintering => "frozen in on the bank",
		ConvoyState.Arrived => $"in at {ToName}",
		_ => $"on the water, {Mathf.RoundToInt(100f * Along / Mathf.Max(1f, Path.Length))} of the way",
	};
}

public static class Convoys
{
	public static readonly List<Convoy> All = new();
	public static readonly List<ConvoyRun> Runs = new();
	// A player who has taken a place with a convoy.
	public static readonly Dictionary<PlayerController, Convoy> Riders = new();

	private static readonly Dictionary<string, RoadPath> _paths = new();
	private static int _nextId = 1, _lastDay = -1;
	private static bool _loaded, _seeded;
	private static readonly Dictionary<string, int> _launched = new();   // "run|leg" -> the year it last left

	// ---- the runs (Data/World/brigade_runs.json) ---------------------------

	private static void LoadRuns()
	{
		if (_loaded) return;
		_loaded = true;
		Runs.Clear();
		var root = GD.Load<Json>("res://Data/World/brigade_runs.json")?.Data.AsGodotDictionary();
		if (root == null) { GD.PushWarning("[convoys] no Data/World/brigade_runs.json"); return; }
		var kinds = new Dictionary<string, CraftKind>();
		if (root.TryGetValue("craft", out var cv))
			foreach (var kv in cv.AsGodotDictionary())
			{
				var d = kv.Value.AsGodotDictionary();
				kinds[kv.Key.AsString()] = new CraftKind
				{
					Name = kv.Key.AsString(),
					Up = d.TryGetValue("up", out var u) ? (float)u.AsDouble() : 50f,
					Down = d.TryGetValue("down", out var dn) ? (float)dn.AsDouble() : 80f,
					LotsPerBoat = d.TryGetValue("lots_per_boat", out var l) ? l.AsInt32() : 70,
					Sprite = d.TryGetValue("sprite", out var sp) ? sp.AsString() : "",
					Scale = d.TryGetValue("scale", out var sc) ? (float)sc.AsDouble() : 0.6f,
				};
			}
		if (!root.TryGetValue("runs", out var rv)) return;
		foreach (var v in rv.AsGodotArray())
		{
			var d = v.AsGodotDictionary();
			var run = new ConvoyRun
			{
				Name = d["name"].AsString(),
				Company = d["company"].AsString() == "NWC" ? Faction.NWC : Faction.HBC,
				Boats = d.TryGetValue("boats", out var b) ? b.AsInt32() : 3,
				Men = d.TryGetValue("men", out var m) ? m.AsInt32() : 7,
			};
			if (d.TryGetValue("craft", out var ck) && kinds.TryGetValue(ck.AsString(), out var kind)) run.Craft = kind;
			foreach (var s in d["stops"].AsGodotArray()) run.Stops.Add(s.AsString());
			foreach (var lv in d["legs"].AsGodotArray())
			{
				var ld = lv.AsGodotDictionary();
				var date = ld["depart"].AsString().Split('-');
				run.Legs.Add(new ConvoyLeg
				{
					From = ld["from"].AsString(), To = ld["to"].AsString(), Cargo = ld.TryGetValue("cargo", out var c) ? c.AsString() : "outfit",
					Month = int.Parse(date[0]), Day = int.Parse(date[1]),
				});
			}
			Runs.Add(run);
		}
	}

	public static void Reset()
	{
		foreach (var c in All) DropMen(c);
		All.Clear(); Riders.Clear(); _paths.Clear(); _launched.Clear();
		_lastDay = -1; _nextId = 1; _seeded = false; _loaded = false;
		_devLaunched = false;
	}

	public static Convoy ById(int id) { foreach (var c in All) if (c.Id == id) return c; return null; }
	public static Convoy Find(string name) { foreach (var c in All) if (c.Name == name && c.Now != ConvoyState.Arrived) return c; return null; }

	// ---- places ---------------------------------------------------------------

	public static FortLedger Ledger(string name)
	{
		if (name == "York Factory") return Sim.Fort;
		if (name == "Oxford House") return Sim.Outpost;
		return Posts.Named(name)?.Ledger;
	}

	public static Vector2 PostPos(string name)
	{
		if (name == "York Factory") return WorldLayout.FortCentre;
		if (name == "Oxford House") return IsoGrid.ToWorld(WorldLayout.OutpostTop);
		if (Posts.Named(name) is { } p) return p.Centre;
		foreach (var l in WorldMap.Landmarks()) if (l.Name == name) return l.At;
		if (WorldMap.Place(name) is { } pl) return IsoGrid.ToWorld(pl.Cell);
		return Vector2.Zero;
	}

	// Where boats come in to a post: its landing, outside the palisade.
	public static Vector2 Landing(string name)
	{
		if (name == "York Factory") return IsoGrid.ToWorld(WorldLayout.WharfEnd);
		if (name == "Oxford House") return IsoGrid.ToWorld(WorldLayout.RoadHeadOutpost);
		if (Posts.Named(name) is { } p) return IsoGrid.ToWorld(p.GateOutside);
		return PostPos(name);
	}

	// The nearest ground a man can stand on, for a camp or a landing.
	public static Vector2 LandNear(Vector2 at)
	{
		var c0 = IsoGrid.FromWorld(at);
		for (int r = 0; r < 14; r++)
			for (int dx = -r; dx <= r; dx++)
				for (int dy = -2 * r; dy <= 2 * r; dy++)
				{
					var n = new Vector2I(c0.X + dx, c0.Y + dy);
					if (Routes.Passable(n, false)) return IsoGrid.ToWorld(n);
				}
		return at;
	}

	// ---- roads -------------------------------------------------------------------

	private const float MaxFeeder = 8000f;   // 200 km: a post further off the route than this is reached in a straight line

	private static RoadPath PathFor(ConvoyRun run, string from, string to)
	{
		string key = $"{run.Name}|{from}|{to}";
		if (_paths.TryGetValue(key, out var cached)) return cached;
		var path = BuildPath(run, from, to);
		_paths[key] = path;
		return path;
	}

	private static RoadPath BuildPath(ConvoyRun run, string from, string to)
	{
		int a = run.Stops.IndexOf(from), b = run.Stops.IndexOf(to);
		if (a < 0 || b < 0 || a == b) return null;
		int dir = b > a ? 1 : -1;
		var names = new List<string>();
		for (int i = a; i != b + dir; i += dir) names.Add(run.Stops[i]);
		var pts = new List<Vector2>();
		var spots = new List<Vector2>();
		foreach (var n in names)
		{
			var p = PostPos(n);
			if (p == Vector2.Zero) { GD.PushWarning($"[convoys] {run.Name}: no place called {n}"); return null; }
			spots.Add(p);
		}
		pts.Add(spots[0]);
		for (int i = 1; i < spots.Count; i++) Reach(pts, spots[i - 1], spots[i]);
		var path = new RoadPath { Pts = pts.ToArray() };
		path.Measure();
		if (path.Length < 100f) return null;

		// The places along it: posts it calls at, and every carrying place within sixty kilometres.
		var seen = new HashSet<string>();
		for (int i = 1; i < names.Count - 1; i++)
		{
			var (s, _) = Project(path, spots[i]);
			bool portage = IsPortage(names[i]);
			path.Events.Add(new RoadEvent { At = s, Name = names[i], Call = !portage && Posts.Named(names[i]) != null || names[i] == "Oxford House", Days = portage ? PortageDays(names[i]) : 1 });
			seen.Add(names[i]);
		}
		foreach (var l in WorldMap.Landmarks())
		{
			if (l.Kind != "portage" || seen.Contains(l.Name)) continue;
			var (s, d) = Project(path, l.At);
			if (d > 2400f) continue;
			path.Events.Add(new RoadEvent { At = s, Name = l.Name, Days = PortageDays(l.Name) });
		}
		path.Events.RemoveAll(e => e.At < 500f || e.At > path.Length - 500f);
		path.Events.Sort((x, y) => x.At.CompareTo(y.At));
		return path;
	}

	private static bool IsPortage(string name)
	{
		foreach (var l in WorldMap.Landmarks()) if (l.Name == name) return l.Kind == "portage";
		return false;
	}

	private static int PortageDays(string name) =>
		name.Contains("Methye") ? 3 : name.Contains("Grand") || name.Contains("Frog") ? 2 : 1;

	// Nearest point of the path to a place: how far along, and how far off.
	private static (float s, float dist) Project(RoadPath path, Vector2 p)
	{
		float bestD = float.MaxValue, bestS = 0f;
		for (int i = 0; i + 1 < path.Pts.Length; i++)
		{
			var q = Geometry2D.GetClosestPointToSegment(p, path.Pts[i], path.Pts[i + 1]);
			float d = q.DistanceTo(p);
			if (d < bestD) { bestD = d; bestS = path.Cum[i] + path.Pts[i].DistanceTo(q); }
		}
		return (bestS, bestD);
	}

	// From one place to the next by the atlas's brigade route that passes
	// nearest both; with none within reach of both, in a straight line.
	private static void Reach(List<Vector2> pts, Vector2 a, Vector2 b)
	{
		float best = float.MaxValue;
		List<Vector2> via = null;
		foreach (var (_, rp) in WorldMap.BrigadeRoutes())
		{
			var (ia, qa, da) = NearestOn(rp, a);
			var (ib, qb, db) = NearestOn(rp, b);
			if (da > MaxFeeder || db > MaxFeeder || da + db >= best) continue;
			best = da + db;
			via = new List<Vector2> { qa };
			float ka = ia + SegT(rp, ia, qa), kb = ib + SegT(rp, ib, qb);
			if (ka <= kb) { for (int k = ia + 1; k <= ib; k++) via.Add(rp[k]); }
			else { for (int k = ia; k >= ib + 1; k--) via.Add(rp[k]); }
			via.Add(qb);
		}
		if (via != null) foreach (var v in via) pts.Add(v);
		pts.Add(b);
	}

	private static (int seg, Vector2 q, float d) NearestOn(Vector2[] rp, Vector2 p)
	{
		int bi = 0; Vector2 bq = rp[0]; float bd = float.MaxValue;
		for (int i = 0; i + 1 < rp.Length; i++)
		{
			var q = Geometry2D.GetClosestPointToSegment(p, rp[i], rp[i + 1]);
			float d = q.DistanceTo(p);
			if (d < bd) { bd = d; bi = i; bq = q; }
		}
		return (bi, bq, bd);
	}

	private static float SegT(Vector2[] rp, int i, Vector2 q)
	{
		float len = rp[i].DistanceTo(rp[i + 1]);
		return len < 0.01f ? 0f : Mathf.Clamp(rp[i].DistanceTo(q) / len, 0f, 1f);
	}

	// ---- the year's schedule --------------------------------------------------------

	private static int Today(GameCalendar cal) => Campaign.DayOf(cal.Year, cal.Month, cal.Day);

	// Once a game day, from the daily batch (Tiers.cs). Server only.
	public static void Tick()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		LoadRuns();
		int today = Today(cal);
		if (_lastDay < 0) _lastDay = today - 1;
		DevLaunch();
		if (!_seeded) { _seeded = true; if (All.Count == 0) Seed(cal, today); }
		for (int guard = 0; _lastDay < today && guard < 90; guard++)
		{
			_lastDay++;
			var date = Campaign.DateOf(_lastDay);
			foreach (var c in new List<Convoy>(All)) DayOf(c, cal, _lastDay);
			All.RemoveAll(c => { if (c.Now != ConvoyState.Arrived || c.ArrivedDay >= _lastDay) return false; DropMen(c); return true; });
			Depart(date.Year, date.Month, date.Day, _lastDay, 0);
		}
		_lastDay = today;
		// A man who rode with a convoy that has been dissolved is left ashore.
		foreach (var p in new List<PlayerController>(Riders.Keys)) if (!All.Contains(Riders[p])) Leave(p, "");
	}

	// Anything due to leave on this date leaves, once a year; a save loaded a
	// few days late still sends it. `elapsed` is how far on it is (a world that
	// begins in the middle of the season).
	private static void Depart(int year, int month, int day, int absDay, int elapsed)
	{
		foreach (var run in Runs)
			for (int i = 0; i < run.Legs.Count; i++)
			{
				var leg = run.Legs[i];
				string key = $"{run.Name}|{i}";
				if (_launched.GetValueOrDefault(key) == year) continue;
				int due = Campaign.DayOf(year, leg.Month, leg.Day);
				if (absDay < due || absDay > due + 12) continue;
				_launched[key] = year;
				Launch(run, leg, absDay - due + elapsed);
			}
	}

	// A new world in the middle of the season: whatever is on the road by now is already on it.
	private static void Seed(GameCalendar cal, int today)
	{
		foreach (var run in Runs)
			for (int i = 0; i < run.Legs.Count; i++)
			{
				var leg = run.Legs[i];
				string key = $"{run.Name}|{i}";
				int due = Campaign.DayOf(cal.Year, leg.Month, leg.Day);
				if (today <= due || _launched.GetValueOrDefault(key) == cal.Year) continue;
				var path = PathFor(run, leg.From, leg.To);
				if (path == null) continue;
				bool up = leg.Cargo == "outfit";
				float km = up ? run.Craft.Up : run.Craft.Down;
				int est = Mathf.CeilToInt(path.Length / WorldMap.PxPerKm / km * 1.3f) + EventDays(path);
				if (today - due >= est) continue;
				_launched[key] = cal.Year;
				Launch(run, leg, today - due);
			}
	}

	private static int EventDays(RoadPath p) { int n = 0; foreach (var e in p.Events) n += e.Days; return n; }

	// ---- loading a leg -----------------------------------------------------------------

	private static readonly (ItemId id, float share)[] Outfit =
	{
		(ItemId.TradeGoods, 0.55f), (ItemId.Musket, 0.4f), (ItemId.PointBlanket, 0.5f), (ItemId.SteelTrap, 0.5f),
		(ItemId.Kettle, 0.4f), (ItemId.Rum, 0.4f), (ItemId.Tobacco, 0.5f), (ItemId.Powder, 0.4f),
		(ItemId.Beads, 0.5f), (ItemId.Awl, 0.5f), (ItemId.Stroud, 0.5f), (ItemId.Axe, 0.3f), (ItemId.Capote, 0.4f),
	};

	private static readonly ItemId[] Rations = { ItemId.Pemmican, ItemId.DriedMeat, ItemId.DriedGoose, ItemId.SaltGeese };

	public static Convoy Launch(ConvoyRun run, ConvoyLeg leg, int elapsedDays)
	{
		var from = Ledger(leg.From); var to = Ledger(leg.To);
		if (from == null || to == null || from == to) return null;
		var path = PathFor(run, leg.From, leg.To);
		if (path == null) return null;

		var c = new Convoy
		{
			Id = _nextId++, Run = run, Leg = leg, FromName = leg.From, ToName = leg.To, From = from, To = to, Path = path,
			Upstream = leg.Cargo == "outfit", Boats = run.Boats, Crew = run.Boats * run.Men,
			Guide = GuideFor(run),
		};
		int cap = run.Craft.LotsPerBoat * run.Boats;
		if (leg.Cargo == "returns")
		{
			foreach (var (id, n) in new List<(ItemId, int)>(from.Stores.All()))
			{
				if (Items.Items.Of(id).Kind != ItemKind.Fur || cap <= 0) continue;
				int take = from.Stores.TakeUpTo(id, Mathf.Min(n, cap));
				if (take > 0) { c.Cargo.Add(id, take); cap -= take; }
			}
		}
		else
		{
			foreach (var (id, share) in Outfit)
			{
				int have = from.Stores.Count(id);
				int want = Mathf.Min(cap, Mathf.FloorToInt(have * share));
				if (want <= 0) continue;
				int take = from.Stores.TakeUpTo(id, want);
				if (take > 0) { c.Cargo.Add(id, take); cap -= take; }
			}
		}
		c.LotsAtStart = c.Lots;
		var cal = GameCalendar.Instance;
		if (c.Lots < 1)
		{
			string idle = leg.Cargo == "returns" ? $"{c.Name} lay at {leg.From} with nothing to carry down: no furs on the books." : $"{c.Name} could not be loaded at {leg.From}: the shelves were bare.";
			Chronicle.Event(idle, Landing(leg.From), null, c.Name, 1.2f);
			Hear(c, idle, "the stores were empty at the landing", 1f);
			Raise($"late:{c.Name}");
			return null;
		}
		// Rations for the road, out of the sending post's food.
		int wantFood = Mathf.Max(8, c.Crew * 2);
		foreach (var id in Rations)
		{
			if (c.Provisions.FoodNutritionTotal >= wantFood * 35f) break;
			int give = from.Stores.TakeUpTo(id, Mathf.Max(1, wantFood / 3));
			if (give > 0) c.Provisions.Add(id, give);
		}
		float kmPerDay = c.Upstream ? run.Craft.Up : run.Craft.Down;
		int est = Mathf.CeilToInt(path.Length / WorldMap.PxPerKm / kmPerDay) + EventDays(path);
		c.DueDay = Today(cal) - elapsedDays + Mathf.CeilToInt(est * 1.3f);
		if (elapsedDays > 0)
		{
			// Already some way down the road: put her where the days have taken her, past the places already behind.
			c.Along = Mathf.Min(path.Length * 0.9f, elapsedDays * kmPerDay * WorldMap.PxPerKm * 0.8f);
			c.DaysOut = elapsedDays;
			while (c.NextEvent < path.Events.Count && path.Events[c.NextEvent].At < c.Along) c.NextEvent++;
		}
		c.Step = 0f;
		All.Add(c);
		Raise($"out:{c.Name}"); Unraise($"in:{c.Name}"); Unraise($"late:{c.Name}");
		Unraise($"lost:{c.Name}"); Unraise($"robbed:{c.Name}"); Unraise($"turned:{c.Name}");   // a new crossing starts clean
		string what = leg.Cargo == "returns" ? "the winter's returns" : "the outfit";
		string line = $"{c.Name} leave {leg.From} for {leg.To}: {c.Boats} {run.Craft.Name}s, {c.Crew} men under {c.Guide}, {c.Lots} pieces of {what}.";
		Chronicle.Event(line, Landing(leg.From), null, c.Name, 1.2f);
		Hear(c, line, "", 1f);
		return c;
	}

	private static readonly string[] HbcGuides = { "Mr. Isbister", "Mr. Spence", "Mr. Linklater", "Mr. Flett", "Mr. Harper", "Mr. Bruce", "Mr. Sinclair", "Mr. Kennedy" };
	private static readonly string[] NwcGuides = { "M. Cadotte", "M. Desjarlais", "M. Beaulieu", "M. Lagimodiere", "M. Pangman", "M. Bouchard", "M. Faribault", "M. Cournoyer" };
	private static string GuideFor(ConvoyRun run)
	{
		var pool = run.Company == Faction.NWC ? NwcGuides : HbcGuides;
		return pool[(int)(GD.Randi() % (uint)pool.Length)];
	}

	// ---- a day on the road ----------------------------------------------------------------

	private static void DayOf(Convoy c, GameCalendar cal, int today)
	{
		c.Along = Mathf.Min(c.Path.Length, c.Along + c.Step);
		c.Step = 0f;
		c.DaysOut++;
		if (c.Along >= c.Path.Length - 1f) { Arrive(c, today); return; }

		// A place on the road reached: the portage, or the post.
		if (c.Now == ConvoyState.Paddling && c.NextEvent < c.Path.Events.Count && c.Along >= c.Path.Events[c.NextEvent].At - 1f)
		{
			var ev = c.Path.Events[c.NextEvent];
			c.NextEvent++;   // before Reach: turning back re-lays the road and the count with it
			if (Reach(c, ev)) return;
		}

		Feed(c, cal);
		if (!c.Late && today > c.DueDay)
		{
			c.Late = true;
			Raise("brigade_late"); Raise($"late:{c.Name}");
			string late = $"{c.Name} are overdue at {c.ToName}: they were looked for by now.";
			Chronicle.Event(late, c.Where, null, c.Name, 1.4f);
			Hear(c, late, c.ShortDays > 2 ? "short of food on the road" : "the road has been long", 1.2f);
		}

		switch (c.Now)
		{
			case ConvoyState.Portaging:
			case ConvoyState.Calling:
			case ConvoyState.Held:
				if (--c.DayLeft > 0) return;
				c.Now = ConvoyState.Paddling; c.At = ""; c.HasCamp = false;
				break;
			case ConvoyState.Wintering:
				if (cal.IsFrozen) return;
				c.Now = ConvoyState.Paddling; c.HasCamp = false;
				Hear(c, $"The ice is out and {c.Name} push off again for {c.ToName}.", "", 1f);
				break;
		}

		if (cal.IsFrozen)
		{
			c.Now = ConvoyState.Wintering; c.CampAt = Convoys.LandNear(c.Path.At(c.Along)); c.HasCamp = true;
			string frozen = $"{c.Name} are frozen in short of {c.ToName}: the pieces winter on the bank.";
			Chronicle.Event(frozen, c.CampAt, null, c.Name, 1.4f);
			Hear(c, frozen, "freeze-up came before they were in", 1.5f);
			Raise("brigade_late"); Raise($"late:{c.Name}");
			return;
		}

		float km = c.Upstream ? c.Run.Craft.Up : c.Run.Craft.Down;
		km *= Mathf.Lerp(0.75f, 1.05f, Mathf.Clamp(c.Crew / (float)Mathf.Max(1, c.Run.Boats * c.Run.Men), 0f, 1f));
		if (c.Lots > c.Run.Craft.LotsPerBoat * c.Boats * 0.8f) km *= 0.85f;
		if (c.ShortDays > 0) km *= 0.7f;
		var sky = cal.Weather.Current;
		if (sky is SkyState.Storm or SkyState.Blizzard)
		{
			if (GD.Randf() < 0.45f)
			{
				c.Now = ConvoyState.Held; c.DayLeft = 1; c.At = "wind-bound on the bank";
				c.CampAt = Convoys.LandNear(c.Path.At(c.Along)); c.HasCamp = true;
				return;
			}
			km *= 0.4f;
		}
		float step = km * WorldMap.PxPerKm;
		if (c.NextEvent < c.Path.Events.Count) step = Mathf.Min(step, Mathf.Max(0f, c.Path.Events[c.NextEvent].At - c.Along));
		c.Step = Mathf.Min(step, c.Path.Length - c.Along);
	}

	// The brigade comes to a carrying place or a post.
	private static bool Reach(Convoy c, RoadEvent e)
	{
		var at = c.Path.At(e.At);
		if (e.Call)
		{
			var ledger = Ledger(e.Name);
			// She calls only at a house of her own company that stands.
			if (ledger == null || ledger.Company != c.Company) return false;
			c.Now = ConvoyState.Calling; c.At = e.Name; c.DayLeft = 1;
			c.CampAt = LandNear(Landing(e.Name)); c.HasCamp = true;
			// A night at the post: a few days' rations drawn, and the news exchanged.
			foreach (var id in Rations)
			{
				int give = ledger.Stores.TakeUpTo(id, 3);
				if (give > 0) { c.Provisions.Add(id, give); c.ShortDays = 0; break; }
			}
			ledger.LastSupplyDay = Today(GameCalendar.Instance);
			Chronicle.Event($"{c.Name} lay a night at {e.Name} on their way {(c.Upstream ? "up" : "down")}.", Landing(e.Name), null, c.Name, 0.8f);
			Hear(c, $"{c.Name} are in at {e.Name} for the night.", "", 0.8f);
			return true;
		}

		c.Now = ConvoyState.Portaging; c.At = e.Name; c.DayLeft = e.Days;
		c.CampAt = LandNear(at); c.HasCamp = true;
		Chronicle.Event($"{c.Name} were at {e.Name} with the pieces on the bank.", at, null, c.Name, 0.8f);
		Hear(c, $"{c.Name} are at {e.Name}: the boats out of the water and the pieces over on men's backs.", "", 0.6f);
		if (!Intercept(c, e, at)) Mishap(c, e, at);
		return true;
	}

	// Who knew where they would be, and what they did about it. The same bad
	// blood as the York road (Brigade.cs), thinner the further from the Bay.
	private static bool Intercept(Convoy c, RoadEvent e, Vector2 at)
	{
		float tension = Rivalry.Tension;
		if (tension < 45f) return false;
		FortLedger rival = c.Company == Faction.HBC ? Sim.Nwc : Sim.Fort;
		if (rival == null) return false;
		float chance = Mathf.Clamp((tension - 40f) / 250f, 0f, 0.3f);
		if (rival.KnowsRivalPlan) chance += 0.1f;
		if (!Director.MayActNow("portage", 2f, at) || GD.Randf() > chance) return false;
		Director.Saw("portage", 2f);
		string who = rival.Company == Faction.NWC ? "the Nor'Westers" : "the Company's men";

		if (GD.Randf() < 0.5f)
		{
			int lots = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All()))
			{
				int take = Mathf.Max(1, n / 2);
				c.Cargo.Remove(id, take); rival.Stores.Add(id, take); lots += take;
			}
			if (c.From != null) { c.From.LotsLostToRaids += lots; c.From.LostToRivalMb += lots * 0.5f; }
			Rivalry.Raise(6f, $"{who} took {c.Name} at {e.Name}");
			Raise("brigade_robbed"); Raise($"robbed:{c.Name}");
			string line = $"{c.Name} were waylaid at {e.Name} by {who}: {lots} pieces carried off.";
			Chronicle.Event(line, at, null, c.Name, 2f);
			Hear(c, line, "the other house knew which day they would be on the carrying place", 2f, headline: true);
			c.DayLeft += 1;
		}
		else
		{
			TurnBack(c);
			Rivalry.Raise(4f, $"{who} turned {c.Name} back at {e.Name}");
			Raise("brigade_turned_back"); Raise($"turned:{c.Name}");
			string line = $"{c.Name} were turned back at {e.Name} by {who}; they are going home to {c.ToName} with the pieces still in the boats.";
			Chronicle.Event(line, at, null, c.Name, 2f);
			Hear(c, line, "the carrying place was held against them", 2f, headline: true);
		}
		return true;
	}

	// Rock and rapid: a hull opened, a pack wet, once in a long while a boat lost.
	private static void Mishap(Convoy c, RoadEvent e, Vector2 at)
	{
		float r = GD.Randf();
		if (r < 0.015f)
		{
			int lost = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All()))
			{
				int take = Mathf.Max(1, n / Mathf.Max(2, c.Boats));
				c.Cargo.Remove(id, take); lost += take;
			}
			int drowned = Mathf.Min(c.Crew - 1, 1 + (int)(GD.Randi() % 2));
			c.Crew -= Mathf.Max(0, drowned); c.Boats = Mathf.Max(1, c.Boats - 1);
			c.DayLeft += 2;
			Raise("brigade_lost"); Raise($"lost:{c.Name}");
			string line = $"A boat of {c.Name} was lost at {e.Name}: {lost} pieces gone in the river and {drowned} men drowned.";
			Chronicle.Event(line, at, null, c.Name, 2f);
			Hear(c, line, "the carrying place has taken a boat before", 2f, headline: true);
		}
		else if (r < 0.09f)
		{
			int spoiled = 0;
			foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All()))
			{
				int take = n / 12;
				if (take > 0) { c.Cargo.Remove(id, take); spoiled += take; }
			}
			c.DayLeft += 1 + (int)(GD.Randi() % 2);
			Chronicle.Event($"{c.Name} gummed a hull at {e.Name}; {spoiled} pieces were spoiled.", at, null, c.Name, 0.8f);
			Hear(c, $"{c.Name} lie up a day at {e.Name} gumming a hull: {spoiled} pieces were wet.", "", 0.8f);
		}
	}

	// Going home: the same road the other way, the pieces back to the house that sent them.
	public static void TurnBack(Convoy c)
	{
		(c.From, c.To) = (c.To, c.From);
		(c.FromName, c.ToName) = (c.ToName, c.FromName);
		c.Path = c.Path.Reversed();
		c.Along = Mathf.Max(0f, c.Path.Length - c.Along);
		c.Step = 0f;
		c.Upstream = !c.Upstream; c.TurnedBack = true;
		c.NextEvent = 0;
		while (c.NextEvent < c.Path.Events.Count && c.Path.Events[c.NextEvent].At <= c.Along) c.NextEvent++;
		c.DueDay += (int)(c.Along / WorldMap.PxPerKm / (c.Upstream ? c.Run.Craft.Up : c.Run.Craft.Down)) + 4;
		c.Late = false;
	}

	// A day's food out of the boats, and what the men can catch.
	private static void Feed(Convoy c, GameCalendar cal)
	{
		if (c.Crew <= 0) return;
		if (GD.Randf() < 0.55f) c.Provisions.Add(ItemId.Fish, 1 + c.Crew / 12);
		int need = Mathf.Max(1, c.Crew / 8);
		foreach (var id in new[] { ItemId.Fish, ItemId.Pemmican, ItemId.DriedMeat, ItemId.DriedGoose, ItemId.SaltGeese })
		{
			if (need <= 0) break;
			int got = c.Provisions.TakeUpTo(id, need);
			need -= got;
		}
		if (need > 0)
		{
			c.ShortDays++;
			if (c.ShortDays == 3) Hear(c, $"{c.Name} are on short commons on the road.", "the boats set out light on provisions", 1f);
			if (c.ShortDays >= 6 && GD.Randf() < 0.25f) c.Crew = Mathf.Max(c.Run.Boats * 2, c.Crew - 1);
		}
		else c.ShortDays = Mathf.Max(0, c.ShortDays - 1);
	}

	// Landed: the pieces go onto the books of the house they were bound for.
	private static void Arrive(Convoy c, int today)
	{
		c.Now = ConvoyState.Arrived;
		c.Along = c.Path.Length;
		int lots = 0;
		foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All())) { c.Cargo.Remove(id, n); c.To.Stores.Add(id, n); lots += n; }
		foreach (var (id, n) in new List<(ItemId, int)>(c.Provisions.All())) { c.Provisions.Remove(id, n); c.To.Stores.Add(id, n); }
		c.To.LastSupplyDay = today;
		c.ArrivedDay = today; c.HasCamp = false;
		Unraise($"late:{c.Name}"); Raise($"in:{c.Name}"); Raise("brigade_in");
		string line = c.TurnedBack
			? $"{c.Name} are back at {c.ToName} after being turned back, with {lots} pieces still aboard."
			: $"{c.Name} are in at {c.ToName}: {lots} pieces landed after {c.DaysOut} days.";
		Chronicle.Event(line, Landing(c.ToName), null, c.Name, 1.2f);
		Hear(c, line, "", 1.2f);

		var riders = new List<PlayerController>();
		foreach (var kv in Riders) if (kv.Value == c) riders.Add(kv.Key);
		foreach (var p in riders)
		{
			if (!c.TurnedBack) Campaign.Deed("escort");
			if (GodotObject.IsInstanceValid(p)) p.SetStartPosition(LandNear(Landing(c.ToName)));
			Leave(p, $"You come to {c.ToName} with {c.Name}.");
		}
		foreach (var p in Sim.Players())
		{
			if (p == null || p.IsDead || p.Task == null || p.Task.Kind != TaskKind.Brigade || p.Task.Convoy != c.Name) continue;
			if (riders.Contains(p) && !c.TurnedBack) p.Task.Progress(TaskKind.Brigade, 1);
			else if (!p.Task.IsComplete)
			{
				p.Standing -= 3f; p.Task.Kind = TaskKind.None;
				Reveal.Word(p, $"{c.Name} came in to {c.ToName} without you: you were to go with them.", "", 1f);
			}
		}
	}

	// ---- news ---------------------------------------------------------------------------------

	private static void Raise(string flag) => Campaign.Flag(flag);
	private static void Unraise(string flag) => Campaign.Unflag(flag);

	// What reaches a player: his own house's brigade at once, anything near him,
	// and whatever he is riding with. Everything else is the chronicle's, and
	// comes down the river as talk.
	private static void Hear(Convoy c, string what, string cause, float weight, bool headline = false)
	{
		foreach (var p in Sim.Players())
		{
			if (p == null || p.IsDead) continue;
			bool mine = p.HomePost != null && (p.HomePost == c.From || p.HomePost == c.To) || Riders.GetValueOrDefault(p) == c;
			bool near = p.GlobalPosition.DistanceTo(c.Where) < 7000f;
			if (!mine && !near) continue;
			if (cause == "") { p.Journal.Add($"{GameCalendar.Instance?.DateString}: {what}"); p.Say(what); }
			else Reveal.Word(p, what, cause, headline ? 1.5f : weight);
		}
	}

	// A line for the Factor, the postmaster or the clerk to say: who is on the road to this house.
	public static List<Convoy> Expecting(FortLedger house)
	{
		var list = new List<Convoy>();
		foreach (var c in All) if (c.To == house && c.Now != ConvoyState.Arrived) list.Add(c);
		return list;
	}

	public static int DaysToGo(Convoy c)
	{
		float km = c.Upstream ? c.Run.Craft.Up : c.Run.Craft.Down;
		int days = Mathf.CeilToInt(c.KmToGo / km) + Mathf.Max(0, c.DayLeft);
		for (int i = c.NextEvent; i < c.Path.Events.Count; i++) days += c.Path.Events[i].Days;
		// Frozen in: nothing moves till the ice goes out in May.
		var cal = GameCalendar.Instance;
		if (cal != null && c.Now == ConvoyState.Wintering) days += (cal.Month >= 11 ? 12 - cal.Month + 5 : Mathf.Max(0, 5 - cal.Month)) * 30;
		return days;
	}

	public static string Expected(Convoy c)
	{
		int days = DaysToGo(c);
		var cal = GameCalendar.Instance;
		if (cal == null) return $"in about {days} days";
		var dt = new DateTime(Mathf.Clamp(cal.Year, 1700, 2100), cal.Month, Mathf.Min(cal.Day, DateTime.DaysInMonth(Mathf.Clamp(cal.Year, 1700, 2100), cal.Month))).AddDays(days);
		return $"in about {days} days, by {GameCalendar.MonthNames[dt.Month - 1]} {dt.Day}";
	}

	public static string Describe(Convoy c)
	{
		string cargo = c.Cargo.SlotCount > 0 ? c.Cargo.Summary() : "nothing";
		string dir = c.Upstream ? "up" : "down";
		return $"{c.Name}: {c.Boats} {c.Run.Craft.Name}{(c.Boats == 1 ? "" : "s")}, {c.Crew} men under {c.Guide}, bound {dir} from {c.FromName} to {c.ToName} with {c.Lots} pieces ({cargo}). They are {c.State}, {c.DaysOut} days out; look for them {Expected(c)}{(c.Late ? " - overdue" : "")}.";
	}

	// ---- the player and the convoys ---------------------------------------------------------------

	public static Convoy BoardedBy(PlayerController p) => p != null && Riders.TryGetValue(p, out var c) ? c : null;

	// What the guide has to say about the road.
	private static string RoadNews(Convoy c)
	{
		var sb = new System.Text.StringBuilder();
		sb.Append($"\"{c.Guide}\" gives the road as he has found it. ");
		if (c.Now == ConvoyState.Wintering) sb.Append("\"We are frozen in; the ice goes out in April, and not an hour before.\" ");
		else if (c.Now == ConvoyState.Portaging) sb.Append($"\"We are over the packs at {c.At}; it is one man's day to a hundred pounds and we are not done.\" ");
		else if (c.Late) sb.Append("\"Late, yes. The road has been long this year, and I will not be told otherwise by a man who has not paddled it.\" ");
		else sb.Append($"\"{Mathf.RoundToInt(c.KmToGo)} kilometres to go to {c.ToName}.\" ");
		if (c.ShortDays > 2) sb.Append("\"The men are on short commons; if you have food to spare it is welcome.\" ");
		if (Rivalry.Tension >= 45f) sb.Append("\"There is talk that the other house means to hold the carrying places against us.\" ");
		return sb.ToString();
	}

	// The scene at the boats (Core/Starts.cs's ScenePage is the plain screen with choices).
	public static ScenePage Meet(PlayerController p, Convoy c, string said = null)
	{
		var page = new ScenePage { Title = c.Name, CanLeave = true, Text = (said != null ? said + "\n\n" : "") + Describe(c) };
		bool mine = c.Company == p.Faction;
		bool rival = Rivalry.AreRivals(c.Company, p.Faction);
		page.Add("Ask how the road has been", () => Meet(p, c, RoadNews(c)));
		if (BoardedBy(p) == c)
		{
			if (c.Moving) page.Add("Step ashore", () => { Leave(p, "You step ashore."); return null; });
			else page.Add("Leave the brigade here", () => { Leave(p, $"You leave {c.Name}."); return null; });
		}
		else if (rival) page.Add("Ask for a place in the boats", () => Meet(p, c, $"\"{c.Guide}\" looks at your coat. \"We take no passengers of the other house.\""));
		else page.Add(mine ? $"Ask to go along to {c.ToName}" : $"Ask for a place in the boats to {c.ToName} (two pieces of goods)", () => Meet(p, c, Board(p, c)));
		if (c.Provisions.FoodNutritionTotal < c.Crew * 20f && p.Inventory.FirstOfKind(ItemKind.Food) is ItemId food)
			page.Add($"Give them {Items.Items.Of(food).Name} for the road", () =>
			{
				p.Inventory.Remove(food); c.Provisions.Add(food); c.ShortDays = Mathf.Max(0, c.ShortDays - 1);
				return Meet(p, c, "The men take it gladly. They will remember who fed them.");
			});
		page.Add("Step back", () => null);
		return page;
	}

	// A place in the boats. A joined player's feet are his own machine's, so the
	// server tells it (RideId) and it follows the boats the server sends.
	private static string Board(PlayerController p, Convoy c)
	{
		if (c.Now == ConvoyState.Wintering) return $"\"{c.Guide}\" shakes his head. \"We are not going anywhere till the ice goes. Come back in the spring.\"";
		if (c.Company != p.Faction)
		{
			if (p.Inventory.Count(ItemId.TradeGoods) < 2) return "\"A place costs two pieces of goods, and you have not got them.\"";
			p.Inventory.Remove(ItemId.TradeGoods, 2); c.Provisions.Add(ItemId.Pemmican, 1);
		}
		else if (p.Standing < 4f) return $"\"{c.Guide}\" looks you over. \"I know nothing of you yet. Earn the Company's good opinion first.\" (standing 4)";
		Riders[p] = c;
		p.Journal.Add($"{GameCalendar.Instance?.DateString}: You took a place with {c.Name}, bound for {c.ToName}.");
		return c.Moving
			? $"\"Take a thwart and keep your head down in the rapids.\" You climb in. Any step of your own takes you ashore; the boats stop you themselves at each carrying place."
			: $"\"We push off when the work is done. Stay close.\" You are with {c.Name} now.";
	}

	public static void Leave(PlayerController p, string why)
	{
		if (p == null) return;
		Riders.Remove(p);
		var cal = GameCalendar.Instance;
		if (GodotObject.IsInstanceValid(p))
		{
			p.RideId = 0; p.TravellingServer = false;
			if (p == Sim.Player) cal?.CancelSkip();
		}
		if (GodotObject.IsInstanceValid(p) && !p.IsDead)
		{
			var land = LandNear(p.GlobalPosition);
			if (p.GlobalPosition.DistanceTo(land) > 1f && Routes.Passable(IsoGrid.FromWorld(p.GlobalPosition), false) == false) p.SetStartPosition(land);
			if (why != "") p.Say(why);
		}
	}

	// The Factor sends a man with a brigade (his task, with a place to be and a day to be there).
	public static List<Convoy> OpenFor(PlayerController p)
	{
		var list = new List<Convoy>();
		foreach (var c in All)
			if (c.Company == p.Faction && c.Now != ConvoyState.Arrived && c.Now != ConvoyState.Wintering && !c.TurnedBack && c.Along < c.Path.Length * 0.6f)
				list.Add(c);
		return list;
	}

	public static string Assign(PlayerController p, Npc leader, Convoy c)
	{
		if (p.Standing < 8f) return "\"A man I have known a fortnight, with the Company's goods? Prove yourself here first.\" (standing 8)";
		p.AssignTask(TaskKind.Brigade, 1, $"{c.Guide} is in charge of the boats");
		p.Task.Convoy = c.Name; p.Task.Dest = c.ToName;
		p.Standing += 1f;
		Chronicle.Event($"{leader.DisplayName} sent {p.DisplayName} with {c.Name}.", leader.GlobalPosition, p, weight: 0.6f);
		return $"\"{c.Name} are {c.State}, bound for {c.ToName}. Catch them up, ask {c.Guide} for a place, and see the pieces in. {Expected(c)} is when I look for them.\"";
	}

	public static CraftKind KindNamed(string name)
	{
		LoadRuns();
		foreach (var r in Runs) if (r.Craft.Name == name) return r.Craft;
		return null;
	}

	// The boats feed a hungry passenger out of their own provisions.
	public static bool FeedRider(PlayerController p, Convoy c)
	{
		foreach (var id in new[] { ItemId.Fish, ItemId.Pemmican, ItemId.DriedMeat, ItemId.DriedGoose })
			if (c.Provisions.Remove(id)) { p.Inventory.Add(id); p.Eat(); return true; }
		return false;
	}

	// ---- the men you can see ---------------------------------------------------------------------------

	private static void DropMen(Convoy c)
	{
		foreach (var n in c.Men) if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead) n.QueueFree();
		c.Men.Clear();
	}

	public static void DropMenOf(Convoy c) => DropMen(c);

	// ---- a campaign beat acts on a convoy ---------------------------------------------------------------

	// Campaign scripts: {"do": "brigade", "run": "the York boats", "effect": "delay" | "turn_back" | "lose" | "cancel" | "launch", "days": 5, "leg": 0}
	public static void Affect(string run, string effect, int days, int leg = 0)
	{
		LoadRuns();
		if (effect == "launch")
		{
			foreach (var r in Runs)
				if (r.Name == run && leg >= 0 && leg < r.Legs.Count)
				{
					var started = Launch(r, r.Legs[leg], 0);
					if (started != null) Hear(started, $"Word: {started.Name} have been sent off ahead of their day.", "", 1.2f);
				}
			return;
		}
		foreach (var c in new List<Convoy>(All))
		{
			if (c.Name != run || c.Now == ConvoyState.Arrived) continue;
			switch (effect)
			{
				case "delay":
					c.Now = ConvoyState.Held; c.DayLeft = Mathf.Max(1, days); c.At = "held up on the bank";
					c.CampAt = LandNear(c.Path.At(c.Along)); c.HasCamp = true; c.Step = 0f;
					Chronicle.Event($"{c.Name} are held up on the road.", c.Where, null, c.Name, 1.4f);
					break;
				case "turn_back":
					TurnBack(c);
					Chronicle.Event($"{c.Name} have been turned back.", c.Where, null, c.Name, 1.8f);
					Raise("brigade_turned_back"); Raise($"turned:{c.Name}");
					break;
				case "lose":
					int lost = 0;
					foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All())) { int take = (n + 1) / 2; c.Cargo.Remove(id, take); lost += take; }
					Chronicle.Event($"{c.Name} have lost {lost} pieces on the road.", c.Where, null, c.Name, 1.8f);
					Raise("brigade_lost"); Raise($"lost:{c.Name}");
					break;
				case "cancel":
					// Never sent after all: the pieces go back on the books of the house that loaded them.
					int back = 0;
					foreach (var (id, n) in new List<(ItemId, int)>(c.Cargo.All())) { c.Cargo.Remove(id, n); c.From.Stores.Add(id, n); back += n; }
					foreach (var (id, n) in new List<(ItemId, int)>(c.Provisions.All())) { c.Provisions.Remove(id, n); c.From.Stores.Add(id, n); }
					foreach (var p in new List<PlayerController>(Riders.Keys)) if (Riders[p] == c) Leave(p, $"{c.Name} are stood down.");
					DropMen(c);
					c.Now = ConvoyState.Arrived; c.ArrivedDay = int.MinValue;
					Chronicle.Event($"{c.Name} were stood down with {back} pieces still on the bank at {c.FromName}.", Landing(c.FromName), null, c.Name, 1.6f);
					break;
			}
			if (effect != "cancel") Hear(c, $"Word: {c.Name} are {c.State}{(effect == "turn_back" ? ", turned back" : "")}.", "", 1.5f, headline: true);
		}
	}

	// ---- save and load ---------------------------------------------------------------------------------------

	public static Godot.Collections.Dictionary Save()
	{
		var launched = new Godot.Collections.Dictionary();
		foreach (var kv in _launched) launched[kv.Key] = kv.Value;
		var rows = new Godot.Collections.Array();
		foreach (var c in All)
		{
			var cargo = new Godot.Collections.Dictionary();
			foreach (var (id, n) in c.Cargo.All()) cargo[id.ToString()] = n;
			var food = new Godot.Collections.Dictionary();
			foreach (var (id, n) in c.Provisions.All()) food[id.ToString()] = n;
			rows.Add(new Godot.Collections.Dictionary
			{
				["run"] = c.Run.Name, ["leg"] = c.Run.Legs.IndexOf(c.Leg), ["from"] = c.FromName, ["to"] = c.ToName,
				["back"] = c.TurnedBack, ["along"] = c.Along, ["step"] = c.Step, ["state"] = (int)c.Now, ["dayLeft"] = c.DayLeft,
				["next"] = c.NextEvent, ["out"] = c.DaysOut, ["due"] = c.DueDay, ["late"] = c.Late, ["short"] = c.ShortDays,
				["at"] = c.At, ["crew"] = c.Crew, ["boats"] = c.Boats, ["guide"] = c.Guide, ["start"] = c.LotsAtStart,
				["cargo"] = cargo, ["food"] = food,
			});
		}
		return new Godot.Collections.Dictionary { ["launched"] = launched, ["convoys"] = rows, ["next"] = _nextId, ["lastDay"] = _lastDay };
	}

	public static void Load(Godot.Collections.Dictionary d)
	{
		foreach (var c in All) DropMen(c);
		All.Clear(); Riders.Clear(); _paths.Clear(); _launched.Clear();
		_loaded = false; _seeded = true; _devLaunched = true;
		LoadRuns();
		if (d == null || d.Count == 0) { _lastDay = -1; return; }
		if (d.TryGetValue("launched", out var lv)) foreach (var kv in lv.AsGodotDictionary()) _launched[kv.Key.AsString()] = kv.Value.AsInt32();
		_nextId = d.TryGetValue("next", out var nx) ? nx.AsInt32() : 1;
		_lastDay = d.TryGetValue("lastDay", out var ld) ? ld.AsInt32() : -1;
		if (!d.TryGetValue("convoys", out var cv)) return;
		foreach (var v in cv.AsGodotArray())
		{
			var r = v.AsGodotDictionary();
			ConvoyRun run = null;
			foreach (var x in Runs) if (x.Name == r["run"].AsString()) run = x;
			if (run == null) continue;
			int li = r["leg"].AsInt32();
			string from = r["from"].AsString(), to = r["to"].AsString();
			bool back = r["back"].AsBool();
			var leg = li >= 0 && li < run.Legs.Count ? run.Legs[li] : null;
			if (leg == null) continue;
			// The path as it was laid at departure (before any turning back).
			var path = PathFor(run, back ? to : from, back ? from : to);
			if (path == null) continue;
			if (back) path = path.Reversed();
			var c = new Convoy
			{
				Id = _nextId++, Run = run, Leg = leg, FromName = from, ToName = to, From = Ledger(from), To = Ledger(to), Path = path,
				TurnedBack = back, Upstream = (leg.Cargo == "outfit") ^ back, Along = (float)r["along"].AsDouble(), Step = (float)r["step"].AsDouble(),
				Now = (ConvoyState)r["state"].AsInt32(), DayLeft = r["dayLeft"].AsInt32(), NextEvent = r["next"].AsInt32(), DaysOut = r["out"].AsInt32(),
				DueDay = r["due"].AsInt32(), Late = r["late"].AsBool(), ShortDays = r["short"].AsInt32(), At = r["at"].AsString(),
				Crew = r["crew"].AsInt32(), Boats = r["boats"].AsInt32(), Guide = r["guide"].AsString(), LotsAtStart = r["start"].AsInt32(),
			};
			if (c.From == null || c.To == null) continue;
			foreach (var kv in r["cargo"].AsGodotDictionary()) if (Enum.TryParse<ItemId>(kv.Key.AsString(), out var cid)) c.Cargo.Add(cid, kv.Value.AsInt32());
			foreach (var kv in r["food"].AsGodotDictionary()) if (Enum.TryParse<ItemId>(kv.Key.AsString(), out var fid)) c.Provisions.Add(fid, kv.Value.AsInt32());
			if (c.Now != ConvoyState.Paddling) { c.CampAt = LandNear(c.Now == ConvoyState.Calling ? Landing(c.At) : c.Path.At(c.Along)); c.HasCamp = true; }
			All.Add(c);
		}
		if (All.Count > 0) Sim.Notice($"{All.Count} brigade{(All.Count == 1 ? " is" : "s are")} on the country's rivers.");
	}

	// ---- dev aids ---------------------------------------------------------------------------------------------------

	private static bool _devLaunched;
	// `-- --convoys` prints every run's roads and sends the first leg of each now,
	// whatever the date (and walk to one with --spawn=<a landmark on its road>).
	private static void DevLaunch()
	{
		if (_devLaunched) return;
		_devLaunched = true;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg != "--convoys") continue;
			foreach (var run in Runs)
			{
				foreach (var leg in run.Legs)
				{
					var path = PathFor(run, leg.From, leg.To);
					GD.Print(path == null
						? $"[convoys] {run.Name}: {leg.From} -> {leg.To}: no road"
						: $"[convoys] {run.Name}: {leg.From} -> {leg.To} {path.Length / WorldMap.PxPerKm:0} km, {path.Events.Count} places on it, leaves {leg.Month:00}-{leg.Day:00} ({leg.Cargo})");
				}
				if (run.Legs.Count > 0 && Launch(run, run.Legs[0], 0) is { } c) GD.Print($"[dev] {c.Name} away at {c.Where}");
			}
			return;
		}
	}
}
