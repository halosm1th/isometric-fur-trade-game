using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

public enum WarPhase { Riding, Fighting, Home, Over }

// A war party (design doc 15.1, the plains spec's Act 6): the fighting men of
// a camp ride against another camp - or a post - to answer for their dead or
// their horses. Not a horse raid (Plains.cs), whose object is the herd: this
// one's object is the enemy, and it ends in a fight won, a fight lost, or a
// ride home without one.
public sealed class WarParty
{
	public BandLedger From;
	public Household Against;
	public string Why = "";
	public readonly List<Npc> Men = new();
	public Npc Leader;
	public Player.PlayerController Rider;     // a player who rides with them
	public WarPhase Phase = WarPhase.Riding;
	public float Hours, Quiet, HomeHours;
	public int Started;
	public float Morale = 1f;
	public string Outcome = "";               // won, lost, turned_back
	public Vector2 Target => IsoGrid.ToWorld(War.CampOf(Against));
	// The way there over the country (World/Routes.cs: round the lakes, over
	// the rivers at the fords), planned once in the background for the party.
	public System.Threading.Tasks.Task<List<Vector2>> RouteTask;
	public List<Vector2> Route;
	public Vector2 Home => IsoGrid.ToWorld(From.Camp);
}

// War parties and truces between the country's peoples (and its posts).
// Far from every player a war party is resolved in numbers the day it rides
// out; near one, or with one riding along, its men are live and fight.
public static class War
{
	public static readonly List<WarParty> Parties = new();
	private static readonly Dictionary<string, int> _truces = new();   // "a|b" -> the day it ends (Plains.Today)

	public static string NameOf(Household h) => h is BandLedger b ? b.Name : h.Title;
	public static Vector2I CampOf(Household h) => h is BandLedger b ? b.Camp : h.AccessCell;

	private static string Pair(Household a, Household b)
	{
		string x = NameOf(a), y = NameOf(b);
		return string.CompareOrdinal(x, y) < 0 ? $"{x}|{y}" : $"{y}|{x}";
	}

	// ---- truces -------------------------------------------------------------------------

	public static bool AtPeace(Household a, Household b) => a != null && b != null && _truces.TryGetValue(Pair(a, b), out int until) && Plains.Today < until;

	public static void Truce(Household a, Household b, int days)
	{
		if (a == null || b == null) return;
		_truces[Pair(a, b)] = Plains.Today + Mathf.Max(1, days);
		// The scores between them are laid down with the truce.
		Plains.CoolFeud(a, b); Plains.CoolFeud(b, a);
		Chronicle.Event($"{NameOf(a)} and {NameOf(b)} made peace for {(days >= 300 ? "a year" : $"{days} days")}.", IsoGrid.ToWorld(CampOf(a)), subject: NameOf(a), weight: 1.5f);
		Sim.Notice($"A truce: {NameOf(a)} and {NameOf(b)} will not raid or fight each other for {(days >= 300 ? "a year" : $"{days} days")}.");
	}

	// ---- who fights ---------------------------------------------------------------------

	public static List<Npc> Fighters(Household h)
	{
		var list = new List<Npc>();
		foreach (var n in Sim.Npcs)
			if (n.HomeStores == h && !n.IsDead && !n.IsFemale && !n.Away && n.HeldBy == null && (n.IsFighter || n.Role == Role.CreeChief || h is FortLedger && n.Weapon != null))
				list.Add(n);
		return list;
	}

	private static float Guns(Household h, int men) => Mathf.Clamp(h.Stores.Count(ItemId.Musket) / (float)Mathf.Max(1, men), 0f, 1f);

	// ---- riding out -----------------------------------------------------------------------

	// Send a camp's fighting men against a camp or a post. A player who is
	// given as the rider rides with them (and the fight is live).
	public static WarParty Launch(BandLedger from, Household against, string why, Player.PlayerController rider = null)
	{
		if (from == null || against == null || from == against) return null;
		foreach (var p in Parties) if (p.From == from && p.Phase != WarPhase.Over) return p;   // one at a time from a camp
		var party = new WarParty { From = from, Against = against, Why = why ?? "", Rider = rider, Started = Plains.Today };
		bool live = rider != null || Plains.NearPlayer(party.Target, 12000f) || Plains.NearPlayer(party.Home, 12000f);
		Chronicle.Event($"{from.Name} rode out against {NameOf(against)}{(party.Why != "" ? ", " + party.Why : "")}.", party.Home, subject: from.Name, weight: 1.2f);
		if (!live) { InNumbers(party); return party; }

		foreach (var n in Fighters(from))
		{
			if (party.Men.Count >= 7) break;
			n.Tier = Tier.Live;
			n.ProcessMode = Node.ProcessModeEnum.Inherit;
			n.Visible = true;
			if (n.GlobalPosition.DistanceTo(party.Home) > 2500f) n.GlobalPosition = party.Home + new Vector2(GD.RandRange(-120, 120), GD.RandRange(-60, 60));
			n.Brain.Clear(n);
			n.Brain.Push(n, new WarPartyGoal(party));
			Net.Net.I?.AnnounceTier(n, false);
			party.Men.Add(n);
		}
		if (party.Men.Count == 0) { InNumbers(party); return party; }
		party.RouteTask = Routes.PlanAsync(party.Home, party.Target, Sim.Terrain?.FrozenAt(party.Home) ?? false);
		party.Leader = from.Leader != null && party.Men.Contains(from.Leader) ? from.Leader : party.Men[0];
		Parties.Add(party);
		Sim.Notice($"{from.Name}'s war party rides out against {NameOf(against)}: {party.Men.Count} men{(rider != null ? ", and you with them" : "")}.");
		return party;
	}

	// ---- a fight in numbers (nobody to see it) ---------------------------------------------

	private static void InNumbers(WarParty p) => Finish(p, Settle(p));

	// The fight in numbers: men, guns and the defender's own ground. Kills
	// the fallen (only bodies nobody is standing beside), takes the horses,
	// and says how it went.
	private static string Settle(WarParty p)
	{
		var att = Fighters(p.From);
		var def = Fighters(p.Against);
		if (att.Count == 0) return "turned_back";
		float sa = att.Count * (1f + 0.6f * Guns(p.From, att.Count));
		float sd = Mathf.Max(1, def.Count) * (1f + 0.6f * Guns(p.Against, def.Count)) * 1.25f;   // the defender has his own ground
		bool won = GD.Randf() < sa / (sa + sd);
		KillQuietly(won ? def : att, won ? GD.RandRange(1, 3) : GD.RandRange(1, 3), p);
		if (GD.Randf() < 0.35f) KillQuietly(won ? att : def, 1, p);
		if (won) TakeHorses(p);
		return won ? "won" : "lost";
	}

	private static void KillQuietly(List<Npc> side, int n, WarParty p)
	{
		for (int i = 0; i < side.Count && n > 0; i++)
		{
			var m = side[i];
			if (m.IsDead || m.Tier == Tier.Live) continue;   // nobody the player could be standing beside
			Chronicle.Event($"{m.DisplayName} of {NameOf(m.HomeStores)} was killed when {p.From.Name} fought {NameOf(p.Against)}.", IsoGrid.ToWorld(CampOf(m.HomeStores)), m, NameOf(m.HomeStores), 2f);
			m.DieQuietly();
			n--;
		}
	}

	private static void TakeHorses(WarParty p)
	{
		if (p.Against.Herd.Free <= 0) return;
		int n = Mathf.Clamp(GD.RandRange(3, 12), 1, Mathf.Max(1, p.Against.Herd.Free / 3));
		p.Against.Herd.Lose(n);
		p.From.Herd.Horses += n;
	}

	// ---- a fight with people in it ---------------------------------------------------------

	public static void Tick(float hours)
	{
		if (Parties.Count == 0) return;
		for (int i = Parties.Count - 1; i >= 0; i--)
		{
			var p = Parties[i];
			p.Hours += hours;
			int alive = 0, near = 0;
			foreach (var m in p.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead) { alive++; if (m.GlobalPosition.DistanceTo(p.Target) < 1500f) near++; }
			int dead = p.Men.Count - alive;
			if (Plains.Log && Mathf.FloorToInt(p.Hours / 4f) != Mathf.FloorToInt((p.Hours - hours) / 4f))
			{
				var where = new System.Text.StringBuilder();
				foreach (var m in p.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead) where.Append($" {m.DisplayName}: {m.GlobalPosition.DistanceTo(p.Target):0} px off, {m.Brain.Top?.Label};");
				GD.Print($"[war] {p.From.Name} -> {NameOf(p.Against)} {p.Phase} {p.Hours:0}h rider {(p.Rider != null ? p.Rider.GlobalPosition.ToString() : "none")} route {(p.Route?.Count.ToString() ?? (p.RouteTask?.IsCompleted == true ? "done" : "planning"))}:{where}");
			}
			// Ridden out of every player's sight (the tiers have put its men in
			// the background): what is left of it is settled in numbers.
			int liveMen = 0;
			foreach (var m in p.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead && m.Tier == Tier.Live) liveMen++;
			if (alive > 0 && liveMen == 0 && p.Phase is WarPhase.Riding or WarPhase.Fighting)
			{
				foreach (var m in p.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead) Plains.ReturnHorse(m);
				InNumbers(p);
				Parties.RemoveAt(i);
				continue;
			}
			switch (p.Phase)
			{
				case WarPhase.Riding:
					if (alive == 0) { Retreat(p, "lost"); break; }
					// Riding with a player, he leads them: they are at the enemy's camp
					// when he is. If he falls, they go on by the route without him.
					if (p.Rider != null && (!GodotObject.IsInstanceValid(p.Rider) || p.Rider.IsDead)) p.Rider = null;
					if (p.Rider != null) near = p.Rider.GlobalPosition.DistanceTo(p.Target) < 2500f ? alive : 0;
					// At their camp: a fight a player is there to see is fought; any
					// other is settled in numbers and the riders come home with the news.
					if (near * 2 >= alive)
					{
						// Fought for real only if their men are live (a player is near
						// enough for the tiers to have woken them); else in numbers.
						bool seen = false;
						foreach (var f in Fighters(p.Against)) if (f.Tier == Tier.Live) { seen = true; break; }
						if (seen) Engage(p);
						else Retreat(p, Settle(p));
					}
					else if (p.Hours > 96f) Retreat(p, "turned_back");   // four days and they never came up with them
					break;
				case WarPhase.Fighting:
					// Morale (design doc 15.4): losses, and the leader falling, break a party.
					p.Morale = 1f - dead / (float)Mathf.Max(1, p.Men.Count) * 1.6f - (p.Leader != null && p.Leader.IsDead ? 0.4f : 0f);
					if (alive == 0 || p.Morale < 0.35f) { Retreat(p, "lost"); break; }
					// Their men still standing: live ones near their camp fight on;
					// if only background ones are left (the player has gone, the
					// tiers have put them away), the rest is settled in numbers.
					int liveFoes = 0, otherFoes = 0;
					foreach (var e in Fighters(p.Against))
					{
						if (e.Tier == Tier.Live) { if (e.GlobalPosition.DistanceTo(p.Target) < 2200f) liveFoes++; }
						else otherFoes++;
					}
					if (liveFoes == 0 && otherFoes > 0) { Retreat(p, Settle(p)); break; }
					p.Quiet = liveFoes > 0 ? 0f : p.Quiet + hours;
					if (p.Quiet > 0.25f) { TakeHorses(p); Retreat(p, "won"); }
					break;
				case WarPhase.Home:
					p.HomeHours += hours;
					bool anyAway = false;
					foreach (var m in p.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead && m.GlobalPosition.DistanceTo(p.Home) > 900f) anyAway = true;
					if (!anyAway || p.HomeHours > 24f) { Finish(p, p.Outcome); Parties.RemoveAt(i); }
					break;
			}
		}
	}

	private static void Engage(WarParty p)
	{
		p.Phase = WarPhase.Fighting;
		// The camp turns out: its armed men go for the nearest of them.
		foreach (var w in Fighters(p.Against))
		{
			if (w.Tier != Tier.Live) continue;
			Npc nearest = null; float nd = 5000f;
			foreach (var r in p.Men) if (r != null && GodotObject.IsInstanceValid(r) && !r.IsDead && r.GlobalPosition.DistanceTo(w.GlobalPosition) < nd) { nd = r.GlobalPosition.DistanceTo(w.GlobalPosition); nearest = r; }
			if (nearest == null) continue;
			w.Sleeping = false;
			w.Brain.Clear(w);
			w.Brain.Push(w, new AttackGoal(nearest));
		}
		Sim.Notice($"{p.From.Name}'s war party is at {NameOf(p.Against)}: they are fighting!");
	}

	private static void Retreat(WarParty p, string outcome)
	{
		p.Outcome = outcome;
		p.Phase = WarPhase.Home;
		Sim.Notice(outcome switch
		{
			"won" => $"{NameOf(p.Against)} have broken: {p.From.Name}'s war party has the field.",
			"lost" => $"{p.From.Name}'s war party is beaten and riding for home.",
			_ => $"{p.From.Name}'s war party is turning for home without a fight.",
		});
	}

	// ---- what it comes to -------------------------------------------------------------------

	private static void Finish(WarParty p, string outcome)
	{
		p.Phase = WarPhase.Over;
		p.Outcome = outcome;
		// The scores: a beaten camp owes the winner its dead; a winner's grievance is paid.
		if (outcome == "won") { Plains.HeatFeud(p.Against, p.From, 3f); Plains.CoolFeud(p.From, p.Against); }
		else if (outcome == "lost") Plains.HeatFeud(p.From, p.Against, 3f);
		foreach (var m in p.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead) Plains.ReturnHorse(m);
		// A player who rode with them is remembered by both sides.
		if (p.Rider != null && GodotObject.IsInstanceValid(p.Rider))
			foreach (var n in Sim.Npcs)
			{
				if (n.IsDead) continue;
				if (n.HomeStores == p.From) n.ChangeRapport(p.Rider, outcome == "won" ? 10f : 4f);
				else if (n.HomeStores == p.Against) n.ChangeRapport(p.Rider, -25f);
			}
		string how = outcome switch { "won" => "won the fight", "lost" => "were beaten", _ => "came home without a fight" };
		Chronicle.Event($"{p.From.Name}'s war party against {NameOf(p.Against)} {how}.", p.Home, subject: p.From.Name, weight: 2f);
		if (Plains.NearPlayer(p.Home, 20000f) || Plains.NearPlayer(p.Target, 20000f)) Sim.Notice($"{p.From.Name}'s war party against {NameOf(p.Against)} {how}.");
		// For the story: war:<band>:won / lost / turned_back, and the same against the target.
		Campaign.Flag($"war:{p.From.Name}:{outcome}");
		Campaign.Flag($"war:{p.From.Name}:{NameOf(p.Against)}:{outcome}");
	}

	// ---- the weekly reckoning ---------------------------------------------------------------

	// An old score hot enough is answered with a war party, not a horse raid.
	public static void Weekly(GameCalendar cal)
	{
		if (cal.Month is < 4 or > 10) return;   // no war parties in the deep snow
		foreach (var (key, heat) in new List<KeyValuePair<string, float>>(Plains.Feuds))
		{
			if (heat < 4f) continue;
			var names = key.Split('|');
			if (names.Length != 2) continue;
			var from = Resolve(names[0]) as BandLedger;
			var against = Resolve(names[1]);
			if (from == null || against == null || from is CountryBand { Country.IsFamily: true } || AtPeace(from, against)) continue;
			if (GD.Randf() < 0.05f * heat) Launch(from, against, "to answer for their dead");
		}
	}

	// A name as the feuds keep it: a camp's, or a post's books ("Brandon House stores").
	private static Household Resolve(string name) =>
		Quests.HouseholdAt(name) ?? (name.EndsWith(" stores") ? Quests.HouseholdAt(name[..^7]) : null);

	public static Godot.Collections.Dictionary Save()
	{
		var t = new Godot.Collections.Dictionary();
		foreach (var kv in _truces) t[kv.Key] = kv.Value;
		return new Godot.Collections.Dictionary { ["truces"] = t };
	}

	public static void Load(Godot.Collections.Dictionary d)
	{
		_truces.Clear(); Parties.Clear();
		if (d != null && d.ContainsKey("truces")) foreach (var kv in d["truces"].AsGodotDictionary()) _truces[kv.Key.AsString()] = kv.Value.AsInt32();
	}

	public static void Reset() { _truces.Clear(); Parties.Clear(); }
}
