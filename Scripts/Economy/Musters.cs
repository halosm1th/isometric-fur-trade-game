using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

public enum MusterPhase { Marching, Holding, Attacking, Fighting, Over }

// A post's men under arms, at a campaign's word (Core/Campaign.cs: muster,
// march, attack, stand_down). Not a war party (War.cs), which is a camp's own
// quarrel: this is a script moving men about - called from their work to a
// place, marched to another, sent against a post or a camp, and stood down
// again to go back to what they were doing.
public sealed class Muster
{
	public Household Post;
	public string Name = "";                  // the post as the script named it, for the flags
	public readonly List<Npc> Men = new();
	public Vector2 Dest;                      // where they are going, or stand
	public string DestName = "";
	public Household Foe;                     // what they attack, once told to
	public string FoeName = "", Why = "";
	public MusterPhase Phase = MusterPhase.Marching;
	public int Order;                         // goes up with every new order, so each man re-plans
	public List<Vector2> Route;
	public System.Threading.Tasks.Task<List<Vector2>> RouteTask;
	public float Hours, Quiet;
	public int Called, Taken;                 // how many were called out; how many have been taken prisoner since
	public Npc Leader;
}

public static class Musters
{
	public const string NwcYork = "Nor'Westers' house (York)";
	private const float ArrivedPx = 600f, AtFoePx = 1300f;
	public static readonly List<Muster> All = new();

	public static void Reset() => All.Clear();

	// A post (York Factory, the Nor'Westers' house at York, any post with books)
	// or a camp, by the name a script gives it.
	public static Household PostNamed(string name) => name == NwcYork ? Sim.Nwc : Quests.HouseholdAt(name);

	// Where a place is: a post's door, "the landing at <post>", a camp, a place
	// on the map, or a person ("leader@York Factory").
	public static Vector2? Where(string place)
	{
		if (string.IsNullOrEmpty(place)) return null;
		if (place == NwcYork || Convoys.Ledger(place) != null) return PostPos(PostNamed(place));
		if (place.StartsWith("the landing at ")) { var post = place["the landing at ".Length..]; return post == NwcYork ? PostPos(Sim.Nwc) : Convoys.LandNear(Convoys.Landing(post)); }
		return Quests.WhereIs(place);
	}
	// A post's men stand outside its gate (York Factory's, or an inland post's), else by its door.
	private static Vector2? PostPos(Household h) =>
		h == null ? null
		: h == Sim.Fort ? IsoGrid.ToWorld(WorldLayout.GateOutside)
		: World.Posts.All.Find(p => p.Ledger == h) is { } post ? IsoGrid.ToWorld(post.GateOutside)
		: IsoGrid.ToWorld(h.AccessCell);

	// The men a post can put under arms: its working men, not its master.
	public static List<Npc> MenOf(Household h)
	{
		var list = new List<Npc>();
		if (h == null) return list;
		foreach (var n in Sim.Npcs)
			if (n.HomeStores == h && !n.IsDead && !n.IsFemale && !n.Away && n.HeldBy == null && n != h.Leader
			    && n.Role is Role.Labourer or Role.Voyageur or Role.Hunter or Role.Sailor or Role.Soldier or Role.Farmer)
				list.Add(n);
		return list;
	}

	public static Muster Of(Household h) { foreach (var m in All) if (m.Post == h && m.Phase != MusterPhase.Over) return m; return null; }
	public static int UnderArms(string post) => PostNamed(post) is { } h && Of(h) is { } m ? Alive(m) : 0;
	public static int Fighters(string post) => PostNamed(post) is { } h ? MenOf(h).Count : -1;

	// ---- the orders ------------------------------------------------------------------------

	// So many of a post's men (all of them, for 0) leave their work and gather
	// at a place, arming themselves from the post's stores on the way.
	public static string Call(string post, string at, int count)
	{
		var h = PostNamed(post);
		if (h == null) return $"muster: no post or camp '{post}'";
		var where = string.IsNullOrEmpty(at) ? PostPos(h) : Where(at);
		if (where == null) return $"muster: no place '{at}'";
		var m = Of(h);
		if (m == null)
		{
			m = new Muster { Post = h, Name = post };
			foreach (var n in MenOf(h))
			{
				if (count > 0 && m.Men.Count >= count) break;
				Arm(n, h);
				n.Sleeping = false;
				n.Brain.Clear(n);
				n.Brain.Push(n, new MusterGoal(m));
				m.Men.Add(n);
			}
			if (m.Men.Count == 0) return $"muster: {post} has no men to call out";
			m.Leader = m.Men[0];
			m.Called = m.Men.Count;
			All.Add(m);
			Campaign.Flag($"muster:{post}");
			Sim.Notice($"{War.NameOf(h)}: {m.Men.Count} men are called from their work and take up arms.");
		}
		Send(m, where.Value, string.IsNullOrEmpty(at) ? post : at, MusterPhase.Marching);
		return null;
	}

	// The men under arms go to another place and stand there.
	public static string March(string post, string to)
	{
		if (PostNamed(post) is not { } h || Of(h) is not { } m) return $"march: {post} has no men under arms (muster them first)";
		if (Where(to) is not { } where) return $"march: no place '{to}'";
		Send(m, where, to, MusterPhase.Marching);
		return null;
	}

	// The men under arms go against a post or a camp and fight its men.
	public static string Attack(string post, string against, string why)
	{
		if (PostNamed(post) is not { } h || Of(h) is not { } m) return $"attack: {post} has no men under arms (muster them first)";
		var foe = PostNamed(against);
		if (foe == null || foe == h) return $"attack: no post or camp '{against}' to attack";
		m.Foe = foe; m.FoeName = against; m.Why = why ?? "";
		Send(m, PostPos(foe) ?? Where(against) ?? m.Dest, against, MusterPhase.Attacking);
		Chronicle.Event($"The men of {War.NameOf(h)} went against {War.NameOf(foe)}{(m.Why != "" ? ", " + m.Why : "")}.", m.Dest, m.Leader, subject: War.NameOf(h), weight: 1.4f);
		Sim.Notice($"{War.NameOf(h)}'s men go against {War.NameOf(foe)}!");
		return null;
	}

	// Back to their work, wherever they are.
	public static string StandDown(string post)
	{
		if (PostNamed(post) is not { } h || Of(h) is not { } m) return null;   // nobody under arms: nothing to do
		End(m, null);
		Sim.Notice($"{War.NameOf(h)}'s men are stood down and go back to their work.");
		return null;
	}

	private static void Send(Muster m, Vector2 to, string name, MusterPhase phase)
	{
		m.Dest = Convoys.LandNear(to); m.DestName = name; m.Phase = phase; m.Hours = 0f; m.Quiet = 0f; m.Order++;
		var from = m.Leader != null && GodotObject.IsInstanceValid(m.Leader) ? m.Leader.GlobalPosition : m.Dest;
		m.Route = null;
		m.RouteTask = from.DistanceTo(m.Dest) > 1500f ? Routes.PlanAsync(from, m.Dest, Sim.Terrain?.FrozenAt(from) ?? false) : null;
		// Out of every player's sight the men do not walk: they are simply there.
		foreach (var n in m.Men) if (Ok(n) && n.Tier != Tier.Live) n.GlobalPosition = Slot(m, n);
	}

	// A man without a weapon takes a musket from the post's stores (or an axe, or a knife).
	private static void Arm(Npc n, Household h)
	{
		if (n.Weapon != null) return;
		foreach (var w in new[] { ItemId.Musket, ItemId.Axe, ItemId.Knife, ItemId.Tomahawk })
			if (h.Stores.TakeUpTo(w, 1) == 1) { n.Inventory.Add(w); if (w == ItemId.Musket) n.Inventory.Add(ItemId.Powder, h.Stores.TakeUpTo(ItemId.Powder, 2)); return; }
	}

	// Each man his own place: ranks of four, a little apart.
	public static Vector2 Slot(Muster m, Npc n)
	{
		int i = Mathf.Max(0, m.Men.IndexOf(n));
		return m.Dest + new Vector2((i % 4 - 1.5f) * 80f, i / 4 * 70f);
	}

	// ... as a cell a man can stand on.
	public static Vector2I SlotCell(Muster m, Npc n)
	{
		var c = IsoGrid.FromWorld(Slot(m, n));
		return Sim.Paths?.NearestWalkable(c) is Vector2I w ? w : c;
	}

	private static bool Ok(Npc n) => n != null && GodotObject.IsInstanceValid(n) && !n.IsDead;
	private static int Alive(Muster m) { int a = 0; foreach (var n in m.Men) if (Ok(n)) a++; return a; }

	// The men of the place attacked who can stand: armed or not, they turn out.
	public static List<Npc> Defenders(Muster m)
	{
		var list = new List<Npc>();
		if (m.Foe == null) return list;
		foreach (var n in Sim.Npcs)
			if (n.HomeStores == m.Foe && !n.IsDead && !n.IsFemale && !n.Away && n.HeldBy == null && n.Role != Role.Child && n.GlobalPosition.DistanceTo(m.Dest) < 2600f)
				list.Add(n);
		return list;
	}

	// ---- the hours ---------------------------------------------------------------------------

	public static void Tick(float hours)
	{
		// The clock racing (a player asleep, waiting or on the march, or a
		// fast clock): nobody thinks or walks (Npc.PhysicsStep), so the men
		// are simply where they were sent, as they are out of sight.
		var cal = GameCalendar.Instance;
		bool lazy = cal != null && (cal.MotionScale > GameCalendar.LazyMotion || cal.IsSkipping);
		for (int i = All.Count - 1; i >= 0; i--)
		{
			var m = All[i];
			if (m.Phase == MusterPhase.Over) { All.RemoveAt(i); continue; }
			m.Hours += hours;
			if (m.Route == null && m.RouteTask is { IsCompleted: true } rt) m.Route = !rt.IsFaulted && rt.Result is { Count: > 1 } r ? r : null;
			// Under arms means under arms: a man taken prisoner is out of it, and
			// one whose master (or anything else) has set him other work is
			// called back to the ranks, once he has done eating or fleeing.
			m.Taken += m.Men.RemoveAll(n => Ok(n) && n.HeldBy != null);
			if (m.Phase != MusterPhase.Fighting)
				foreach (var n in m.Men)
					if (Ok(n) && n.Tier == Tier.Live && !n.Brain.Has<MusterGoal>() && !n.Brain.HasTier0()) { n.Brain.Clear(n); n.Brain.Push(n, new MusterGoal(m)); }
			int alive = 0, near = 0, live = 0, dead = 0;
			foreach (var n in m.Men)
			{
				if (!Ok(n)) { dead++; continue; }
				alive++;
				if (n.Tier == Tier.Live) live++;
				if (n.GlobalPosition.DistanceTo(m.Dest) < (m.Phase == MusterPhase.Attacking ? AtFoePx : ArrivedPx)) near++;
			}
			if (Plains.Log && Mathf.FloorToInt(m.Hours / 2f) != Mathf.FloorToInt((m.Hours - hours) / 2f))
			{
				var where = new System.Text.StringBuilder();
				foreach (var n in m.Men) if (Ok(n)) where.Append($" {n.DisplayName}: {n.GlobalPosition.DistanceTo(m.Dest):0} px off, {n.Tier}, {n.Brain.Describe()};");
				GD.Print($"[muster] {m.Name} {m.Phase} -> {m.DestName} {m.Hours:0}h route {(m.Route?.Count.ToString() ?? (m.RouteTask == null ? "none" : "planning"))}:{where}");
			}
			if (alive == 0) { End(m, m.Phase is MusterPhase.Attacking or MusterPhase.Fighting ? "lost" : null); continue; }
			if (lazy && m.Phase == MusterPhase.Marching) { foreach (var n in m.Men) if (Ok(n)) { n.StopMoving(); n.GlobalPosition = IsoGrid.ToWorld(SlotCell(m, n)); } near = alive; }
			if (lazy && m.Phase == MusterPhase.Attacking) live = 0;   // settled in numbers below
			switch (m.Phase)
			{
				case MusterPhase.Marching:
					if (near * 2 >= alive)
					{
						m.Phase = MusterPhase.Holding;
						Campaign.Flag($"march:{m.Name}:{m.DestName}");
						Sim.Notice($"{War.NameOf(m.Post)}'s men are at {m.DestName}.");
					}
					break;
				case MusterPhase.Attacking:
					// Nobody there to see it: settled in numbers.
					if (live == 0 || Defenders(m).TrueForAll(d => d.Tier != Tier.Live) && near * 2 >= alive)
				{
					if (Plains.Log) GD.Print($"[muster] {m.Name} against {m.FoeName}: settled in numbers (live {live}, defenders {Defenders(m).Count}, near {near}/{alive})");
					End(m, Settle(m));
					break;
				}
					if (near * 2 >= alive) Engage(m);
					else if (m.Hours > 72f) End(m, "turned_back");   // three days and they never got there
					break;
				case MusterPhase.Fighting:
					// Losses break them (design doc 15.4), as they do a war party.
					// The dead and the taken count alike; a party with nobody left at the
					// place (they have run, or been carried off) is beaten off.
					float morale = 1f - (dead + m.Taken) / (float)Mathf.Max(1, m.Called) * 1.6f - (m.Leader != null && (!Ok(m.Leader) || !m.Men.Contains(m.Leader)) ? 0.4f : 0f);
					int there = 0;
					foreach (var n in m.Men) if (Ok(n) && n.GlobalPosition.DistanceTo(m.Dest) < 2600f) there++;
					if (morale < 0.35f || there == 0)
					{
						if (Plains.Log) GD.Print($"[muster] {m.Name} broke: morale {morale:0.00}, {dead} dead and {m.Taken} taken of {m.Called}, {there} still there");
						End(m, "lost");
						break;
					}
					int standing = 0;
					foreach (var d in Defenders(m)) if (d.Tier == Tier.Live && d.Needs.Health >= 50f) standing++;
					m.Quiet = standing > 0 ? 0f : m.Quiet + hours;
					if (m.Quiet > 0.25f) End(m, "won");
					break;
			}
		}
	}

	// The place turns out: its men go for the nearest of the attackers.
	private static void Engage(Muster m)
	{
		m.Phase = MusterPhase.Fighting;
		foreach (var d in Defenders(m))
		{
			if (d.Tier != Tier.Live) continue;
			Npc nearest = null; float nd = 5000f;
			foreach (var a in m.Men) if (Ok(a) && a.GlobalPosition.DistanceTo(d.GlobalPosition) < nd) { nd = a.GlobalPosition.DistanceTo(d.GlobalPosition); nearest = a; }
			if (nearest == null) continue;
			Arm(d, m.Foe);
			d.Sleeping = false;
			d.Brain.Clear(d);
			d.Brain.Push(d, new AttackGoal(nearest));
		}
		Sim.Notice($"{War.NameOf(m.Post)}'s men are at {War.NameOf(m.Foe)}: they are fighting!");
	}

	// Out of sight: men and arms against men and arms, with the walls on the defender's side.
	private static string Settle(Muster m)
	{
		var att = new List<Npc>(); foreach (var n in m.Men) if (Ok(n)) att.Add(n);
		var def = Defenders(m);
		float sa = att.Count * (1f + 0.6f * Armed(att));
		float sd = Mathf.Max(1, def.Count) * (1f + 0.6f * Armed(def)) * 1.25f;
		bool won = GD.Randf() < sa / (sa + sd);
		Fall(won ? def : att, GD.RandRange(1, 2), m);
		if (GD.Randf() < 0.35f) Fall(won ? att : def, 1, m);
		return won ? "won" : "lost";
	}
	private static float Armed(List<Npc> men) { if (men.Count == 0) return 0f; int a = 0; foreach (var n in men) if (n.Weapon != null) a++; return a / (float)men.Count; }
	private static void Fall(List<Npc> side, int count, Muster m)
	{
		for (int i = 0; i < side.Count && count > 0; i++)
		{
			var n = side[i];
			if (!Ok(n) || n.Tier == Tier.Live) continue;   // nobody the player could be standing beside
			Chronicle.Event($"{n.DisplayName} was killed when the men of {War.NameOf(m.Post)} went against {War.NameOf(m.Foe)}.", n.GlobalPosition, n, War.NameOf(m.Foe), 2f);
			n.DieQuietly();
			count--;
		}
	}

	// It is over: the flags for the story, and every man back to his work.
	private static void End(Muster m, string outcome)
	{
		m.Phase = MusterPhase.Over;
		Campaign.Unflag($"muster:{m.Name}");
		if (outcome != null && m.Foe != null)
		{
			Campaign.Flag($"attack:{m.Name}:{outcome}");
			Campaign.Flag($"attack:{m.Name}:{m.FoeName}:{outcome}");
			string how = outcome switch { "won" => "carried the day", "lost" => "were beaten off", _ => "turned back without a fight" };
			Chronicle.Event($"The men of {War.NameOf(m.Post)} {how} at {War.NameOf(m.Foe)}.", m.Dest, m.Leader, subject: War.NameOf(m.Post), weight: 2f);
			Sim.Notice($"The men of {War.NameOf(m.Post)} {how} at {War.NameOf(m.Foe)}.");
		}
		foreach (var n in m.Men) if (Ok(n) && n.Brain.Has<MusterGoal>()) n.Brain.Clear(n);
	}
}
