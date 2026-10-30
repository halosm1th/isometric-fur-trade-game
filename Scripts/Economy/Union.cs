using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Economy;

// The union of 1821 (design doc 1's ending of the rivalry): when the
// history's "merger" flag is raised, the North West Company goes into the
// Hudson's Bay Company, in the world and not only in the letters.
//  - A North West post within a short ride of a Company post is given up:
//    its stores carried across, its master and clerks taken on, half its men
//    let go (they walk off and are gone), the rest moved in.
//  - A North West post with no Company post near is kept, under the Company.
//  - Everyone of the North West Company, players included, is the Company's.
//  - Every company player is weighed (design doc 1, "the Reckoning of 1821"):
//    standing, the year's work, what he said he would do and what he did.
//    The best are given charge of others, most are kept on, the worst cut loose.
//  - The rivalry is over (Rivalry.United): no raids, no poaching, no tension.
//  - With one buyer in the country, the posts pay less for furs.
public static class Union
{
	public static bool Done;
	// Posts given up at the union, and posts that went over to the Company.
	public static readonly HashSet<string> Closed = new(), Converted = new();
	private const float PairPx = 2800f;      // 70 km: Fort Augustus and Edmonton, Fort Souris and Brandon House, the Forks
	private const float PriceAfter = 0.8f;   // one buyer: the tariff falls by a fifth

	public static void Reset() { Done = false; Closed.Clear(); Converted.Clear(); Rivalry.United = false; }

	// Daily, on the server.
	public static void Daily()
	{
		if (Done || !Net.Net.IsServer) return;
		// Dev: `-- --union` makes the companies one the first morning, to watch it.
		// One company exhausted by the rivalry (Economy/Companies): its creditors
		// will have a union, whatever year it is.
		bool exhausted = Campaign.Has("nwc_exhausted") || Campaign.Has("hbc_exhausted");
		if (!Campaign.Has("merger") && !exhausted && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--union") < 0) return;
		if (exhausted && !Campaign.Has("merger")) Sim.Headline("The companies are to be one: the rivalry has ruined them both, and the creditors will not wait for 1821.");
		Apply();
	}

	public static void Apply()
	{
		Done = true;
		Rivalry.United = true;
		Rivalry.Tension = 0f;
		int closed = 0, kept = 0, letGo = 0;

		// The posts of the wider country.
		var company = new List<TradingPost>();
		foreach (var p in Posts.Standing()) if (p.Faction == Faction.HBC) company.Add(p);
		foreach (var p in Posts.Standing())
		{
			if (p.Faction != Faction.NWC) continue;
			TradingPost near = null; float nd = PairPx;
			foreach (var h in company) { float d = h.Centre.DistanceTo(p.Centre); if (d < nd) { nd = d; near = h; } }
			if (near != null)
			{
				letGo += Merge(p.Ledger, near.Ledger, near.House.Top, near.AccessCell);
				CloseRecord(p);
				closed++;
				Sim.Notice($"{p.Name} is given up; its people and stores go across to {near.Name}.");
			}
			else
			{
				Convert(p);
				kept++;
			}
		}
		// The North West house across the river from York.
		if (Sim.Nwc != null && Sim.Fort != null)
			letGo += Merge(Sim.Nwc, Sim.Fort, WorldLayout.MensHouse.Top, Sim.Fort.AccessCell);

		// Everyone else of the North West Company (men away, players).
		foreach (var n in Sim.Npcs) if (n.Faction == Faction.NWC) ToCompany(n);
		var wereNorWesters = new HashSet<Player.PlayerController>();
		foreach (var pc in Sim.Players()) if (pc.Faction == Faction.NWC) wereNorWesters.Add(pc);
		foreach (var pc in Sim.Players())
		{
			if (pc.Faction != Faction.NWC) continue;
			pc.Faction = Faction.HBC;
			if (pc.PostedTo != "" && Closed.Contains(pc.PostedTo) && NearestCompanyPost(Posts.Named(pc.PostedTo)) is { } to) pc.PostTo(to.Name);
			Marriage.IntoTheCompany(pc);
			pc.Journal.Add($"{GameCalendar.Instance?.DateString}: The North West Company is gone into the Hudson's Bay Company. You are the Company's servant now.");
			pc.Say("You are on the Hudson's Bay Company's books now: the North West Company is no more.");
		}

		// One buyer in the country.
		foreach (var f in AllPosts()) f.PeltPrice *= PriceAfter;

		foreach (var pc in Sim.Players()) if (pc.Faction == Faction.HBC && !pc.IsDead) Weigh(pc, wereNorWesters.Contains(pc));

		Chronicle.Event("The North West Company went into the Hudson's Bay Company.", Sim.Fort != null ? IsoGrid.ToWorld(Sim.Fort.AccessCell) : Vector2.Zero, subject: "the companies", weight: 3f);
		Sim.Notice($"The union: {closed} North West posts given up, {kept} kept under the Company, {letGo} men let go. The Company pays less for furs now that it is the only buyer.");
	}

	public enum Verdict { GivenCharge, KeptOn, CutLoose }

	// What the new order makes of a man (design doc 1). There are twice as
	// many men as places, and a Nor'Wester starts behind: the Company is
	// keeping its own first.
	public static float Worth(Player.PlayerController pc, bool wasNorWester)
	{
		float w = pc.Standing;
		w += pc.Rank switch { "postmaster" => 25f, "clerk" => 12f, "on notice" => -15f, _ => 0f };
		w += 2f * pc.TasksDone - 4f * pc.TasksRefused;
		foreach (var a in pc.Ambitions) if (!a.Abandoned) w += 15f * a.Best;
		if (pc.Deserter) w -= 30f;
		if (wasNorWester) w -= 10f;
		return w;
	}

	public static Verdict Judge(float worth) => worth >= 60f ? Verdict.GivenCharge : worth >= 15f ? Verdict.KeptOn : Verdict.CutLoose;

	private static void Weigh(Player.PlayerController pc, bool wasNorWester)
	{
		float worth = Worth(pc, wasNorWester);
		string date = GameCalendar.Instance?.DateString ?? "";
		switch (Judge(worth))
		{
			case Verdict.GivenCharge:
				// Set over others: a chief trader's place, and the men he must let go.
				pc.Rank = "postmaster";
				pc.Standing = Mathf.Max(pc.Standing, 60f);
				pc.Journal.Add($"{date}: The new Company has weighed its men, and you are kept on and given charge - a chief trader's place. The men you are set over are fewer than they were, and you will choose which go.");
				pc.Say("Kept on, and given charge of others. Half the men under you are to go.");
				Chronicle.Event($"{pc.DisplayName} was kept on at the union and given charge.", pc.GlobalPosition, pc, weight: 2f);
				break;
			case Verdict.KeptOn:
				pc.Journal.Add($"{date}: The new Company has weighed its men, and you are kept on, as you were. Not every man was.");
				pc.Say("Kept on at the union, as you were.");
				break;
			default:
				// Cut loose: off the books, a freeman in a country with one buyer.
				pc.Faction = Faction.Free;
				pc.PostedTo = "";
				pc.Rank = "freeman";
				pc.Standing = 0f;
				pc.Task.Kind = Player.TaskKind.None;
				pc.Journal.Add($"{date}: The new Company has weighed its men, and you are not one it keeps. You are off the books - a freeman now, and the Company the only buyer for what you trap.");
				pc.Say("Let go at the union. You are a freeman now.");
				Chronicle.Event($"{pc.DisplayName} was let go at the union.", pc.GlobalPosition, pc, weight: 2f);
				break;
		}
	}

	// The people and stores of a North West house go into a Company one.
	// Returns how many men were let go.
	private static int Merge(FortLedger from, FortLedger into, Vector2I house, Vector2I work)
	{
		if (from == null || into == null) return 0;
		for (int i = 0; i < from.Stores.SlotSpan; i++)
			if (from.Stores.Slot(i) is var (id, count)) into.Stores.Add(id, count);
		from.Stores.Clear();

		var people = new List<Npc>();
		foreach (var n in Sim.Npcs) if (n.HomeStores == from && !n.IsDead) people.Add(n);
		// The master and clerks are taken on; of the men, every other one is
		// let go, and his wife and children with him.
		var gone = new HashSet<Npc>();
		int men = 0;
		foreach (var n in people)
		{
			if (n.IsFemale || n == from.Leader || n.Role is Role.Clerk or Role.NwcPartner or Role.Postmaster) continue;
			if (n.Role is not (Role.Voyageur or Role.Labourer)) continue;
			if (men++ % 2 == 1) gone.Add(n);
		}
		foreach (var n in people)
			if (!gone.Contains(n) && (n.Spouse is Npc s && gone.Contains(s) || n.Father != null && gone.Contains(n.Father)))
				gone.Add(n);

		int slot = 0;
		foreach (var n in people)
		{
			if (gone.Contains(n)) { LetGo(n); continue; }
			n.HomeStores = into;
			n.BedCell = IsoGrid.Cell(house, 1 + slot % 3, 1 + slot / 3 % 3);
			n.WorkCell = work;
			slot++;
			if (n == from.Leader)
			{
				n.Role = Role.Clerk;
				n.DisplayName = Bare(n.DisplayName) + " (chief trader)";
			}
			ToCompany(n);
			n.ResetBehaviour(); n.Brain.Clear(n);
		}
		from.Leader = null;
		return gone.Count;
	}

	// Paid off: he walks away down the river road, and is gone.
	private static void LetGo(Npc n)
	{
		n.HomeStores = null;
		n.Brain.Clear(n);
		var away = n.GlobalPosition + new Vector2(GD.RandRange(-1f, 1f) > 0 ? 2400f : -2400f, 1200f);
		n.Brain.Push(n, new GoToGoal(IsoGrid.FromWorld(away), 200f));
		if (n.GetTree() is { } tree)
			tree.CreateTimer(GameCalendar.Instance.SecondsPerGameHour * 24f).Timeout += () => { if (GodotObject.IsInstanceValid(n)) n.QueueFree(); };
	}

	private static void ToCompany(Npc n)
	{
		n.Faction = Faction.HBC;
		n.DisplayName = n.DisplayName.Replace(" (NWC)", "");
	}

	// A North West post kept, under the Company's flag.
	private static void Convert(TradingPost p)
	{
		p.Company = "HBC";
		Converted.Add(p.Name);
		if (p.Ledger != null) foreach (var n in Sim.Npcs) if (n.HomeStores == p.Ledger) ToCompany(n);
	}

	private static void CloseRecord(TradingPost p)
	{
		p.Company = "HBC";
		Closed.Add(p.Name);
		p.Closed = GameCalendar.Instance?.Year ?? 1821;
	}

	private static TradingPost NearestCompanyPost(TradingPost of)
	{
		if (of == null) return null;
		TradingPost best = null; float bd = float.MaxValue;
		foreach (var p in Posts.Standing())
		{
			if (p == of || Closed.Contains(p.Name) || p.Faction != Faction.HBC) continue;
			float d = p.Centre.DistanceTo(of.Centre);
			if (d < bd) { bd = d; best = p; }
		}
		return best;
	}

	private static IEnumerable<FortLedger> AllPosts()
	{
		if (Sim.Fort != null) yield return Sim.Fort;
		if (Sim.Outpost != null) yield return Sim.Outpost;
		foreach (var p in Posts.Standing()) if (!Closed.Contains(p.Name) && p.Ledger != null) yield return p.Ledger;
	}

	private static string Bare(string name) => System.Text.RegularExpressions.Regex.Replace(name ?? "", @"\s*\([^)]*\)", "");

	// ---- the save ----------------------------------------------------------------

	public static Godot.Collections.Dictionary Save()
	{
		var closed = new Godot.Collections.Array(); foreach (var c in Closed) closed.Add(c);
		var conv = new Godot.Collections.Array(); foreach (var c in Converted) conv.Add(c);
		return new Godot.Collections.Dictionary { ["done"] = Done, ["closed"] = closed, ["converted"] = conv };
	}

	// Before the posts are raised: a post given up at the union stays given
	// up, and one taken over keeps the Company's flag. (The people are in the
	// save as they were, homes and all.)
	public static void Load(Godot.Collections.Dictionary d)
	{
		Reset();
		if (d == null) return;
		Done = d.ContainsKey("done") && d["done"].AsBool();
		Rivalry.United = Done;
		if (d.ContainsKey("closed")) foreach (var v in d["closed"].AsGodotArray()) if (Posts.Named(v.AsString()) is { } p) CloseRecord(p);
		if (d.ContainsKey("converted")) foreach (var v in d["converted"].AsGodotArray()) if (Posts.Named(v.AsString()) is { } p) { p.Company = "HBC"; Converted.Add(p.Name); }
	}
}
