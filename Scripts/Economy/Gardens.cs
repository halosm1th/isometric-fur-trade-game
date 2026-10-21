using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The gardens of the Missouri, and the berry season (design doc 13.1, 13.5).
//
// The Mandan, Hidatsa and Arikara towns were not hunting bands: they were
// farming towns in earth lodges, growing corn, beans, squash and tobacco on
// the bottomlands, and they were the great market of the plains. The nomadic
// peoples came in with meat, hides and European goods and went away with
// corn; the traders came up from the Assiniboine River posts for the same
// thing. That is the trade that moved vegetable food across the country.
//
// Berries are the other half of it. Scurvy is not answered by dried meat, and
// a country that eats nothing else dies of it: the answer was berries in
// their season - saskatoons, cranberries, rosehips - and spruce beer brewed
// at the post through the winter.
public static class Gardens
{
	// The farming towns, by the names the atlas gives their camps.
	private static readonly string[] Towns = { "Mandan", "Hidatsa", "Arikara" };

	public static bool IsTown(Household h) => h is CountryBand cb && cb.Country != null && Named(cb.Country.Name);

	// The same question asked of a camp's name, for the painter.
	public static bool IsTownNamed(string camp) => Named(camp);

	private static bool Named(string camp)
	{
		foreach (string t in Towns) if (camp != null && camp.StartsWith(t)) return true;
		return false;
	}

	private static int _lastWeek = -99;

	// One town's year in the fields. The corn is not conjured at harvest: it is
	// sown in May out of the town's own seed corn, tended through the summer
	// under whatever sky the town's region rolls, and ripens all at once in
	// September. Like wild rice it is gathered by hand in a short window, and
	// what is not brought in shatters and is lost - to the frost, or to the
	// week the hands were not there for. How many sow, tend and gather is how
	// many the town has left.
	private class Plot
	{
		public int Year;                 // the year it was sown
		public int Planted;              // seed corn put in the ground
		public float Stand = 1f;         // the stand's health: 1 is a fair summer
		public bool Ripened, Frosted, Told, Closed;
		public int Ripe;                 // standing and ripe, not yet brought in
		public int Harvested;            // in the lodges
		public int Need;                 // what the town hoped for (150 a mouth)
		public string LastResult = "";   // how the last closed crop came in
		public int LastResultYear = -1;
	}
	private static readonly Dictionary<string, Plot> Plots = new();
	private static readonly Dictionary<string, int> PendingCorn = new();   // restored on a load, once the lodges exist
	// A seed puts back a hundred and fifty: the towns grew far beyond their own mouths, and that is what made them a market.
	private const int CornPerMouth = 150, HarvestPerMouthWeek = 30;

	public static void Reset() { Plots.Clear(); PendingCorn.Clear(); _lastWeek = -99; }

	// The harvest a month belongs to: from September the crop of this year, before that last year's.
	public static int CropYear(GameCalendar cal) => cal.Month >= 9 ? cal.Year : cal.Year - 1;

	// How the crop looks to a man at the town: judged on what has been brought
	// in and what stands, in the harvest months, and on the closed crop after.
	public static (string word, int year) CropWord(CountryBand town, GameCalendar cal)
	{
		if (!Plots.TryGetValue(town.Name, out var p)) return ("", CropYear(cal));
		if (cal.Month is 9 or 10 && p.Year == cal.Year && p.Ripened) return (Judge(p.Harvested + p.Ripe, p.Need), cal.Year);
		return (p.LastResult, p.LastResultYear);
	}

	private static string Judge(int got, int need)
	{
		float r = got / (float)Mathf.Max(1, need);
		return r >= 0.8f ? "good" : r >= 0.5f ? "fair" : r >= 0.2f ? "poor" : "failed";
	}

	// Driven once a week off the world's daily tick.
	public static void Tick()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		int week = cal.Day / 7;
		if (week == _lastWeek) return;
		if (week < _lastWeek) _lastWeek = -99;   // a new world, or a load
		_lastWeek = week;

		var towns = new List<CountryBand>();
		foreach (var camp in Camps.All)
		{
			if (camp.Ledger == null || !Named(camp.Name)) continue;
			towns.Add(camp.Ledger);
			Season(camp.Ledger, cal);
		}
		if (towns.Count > 0) Fair(towns, cal);
		Berries(cal);
	}

	// A town the world has only just met - a new game, or one saved before the
	// fields were kept - is put where its year would have brought it.
	private static Plot Bootstrap(CountryBand town, GameCalendar cal)
	{
		int mouths = Mathf.Max(1, town.Mouths), m = cal.Month;
		var p = new Plot { Year = cal.Year - 1, LastResult = "fair", LastResultYear = cal.Year - 1, Need = mouths * CornPerMouth };
		int carried;
		if (m <= 8) carried = Mathf.RoundToInt(Mathf.Lerp(120f, 20f, (m - 1) / 7f)) * mouths;   // last year's, eaten down to the new
		else if (m <= 10) carried = mouths * 10;
		else carried = mouths * 120;
		if (m >= 5) { p.Year = cal.Year; p.Planted = mouths; }
		if (m >= 9) { p.Ripened = true; p.Ripe = m >= 11 ? 0 : mouths * CornPerMouth; p.Harvested = m >= 11 ? mouths * 140 : 0; }
		if (m >= 11) { p.Closed = true; p.LastResultYear = cal.Year; }
		town.Stores.Add(ItemId.Corn, Mathf.Max(0, carried - town.Stores.Count(ItemId.Corn)));
		return p;
	}

	private static void Season(CountryBand town, GameCalendar cal)
	{
		// A saved game's corn goes back into the lodges the first week they exist.
		if (PendingCorn.Remove(town.Name, out int saved)) { town.Stores.TakeUpTo(ItemId.Corn, town.Stores.Count(ItemId.Corn)); town.Stores.Add(ItemId.Corn, saved); }
		if (!Plots.TryGetValue(town.Name, out var p)) Plots[town.Name] = p = Bootstrap(town, cal);
		int mouths = Mathf.Max(1, town.Mouths);
		var at = IsoGrid.ToWorld(town.Camp);
		float mean = Climate.MeanAt(at, cal.DayOfYear, out _, out _);

		// Sowing: May, or June if the spring was late. What goes in the ground is
		// the town's own seed corn - a town that ate or traded its seed sows less.
		if (cal.Month is 5 or 6 && p.Year != cal.Year)
		{
			int seed = town.Stores.TakeUpTo(ItemId.Corn, mouths);
			p = Plots[town.Name] = new Plot { Year = cal.Year, Planted = seed, Stand = cal.Month == 6 ? 0.85f : 1f, Need = mouths * CornPerMouth, LastResult = p.LastResult, LastResultYear = p.LastResultYear };
			if (seed < mouths / 2) Sim.NoticeLocal($"{town.Title}: not seed enough to plant the gardens this year.");
			if (Plains.Log) GD.Print($"[plains] {cal.DateString} {town.Name} sowed {seed} seed corn ({mouths} mouths)");
			return;
		}
		if (p.Year != cal.Year) return;   // nothing sown yet this year

		// Tending, May to August.
		if (cal.Month is >= 5 and <= 8)
		{
			var sky = cal.Weather.At(at);
			if (sky == SkyState.Rain) p.Stand += 0.02f;
			else if (sky == SkyState.Clear && cal.Month >= 6) p.Stand -= 0.015f;   // a dry summer burns it
			else if (sky == SkyState.Storm && GD.Randf() < 0.35f) { p.Stand -= 0.2f; Sim.NoticeLocal($"{town.Title}: hail in the gardens."); }
			else if (sky is SkyState.Snow or SkyState.Blizzard) p.Stand -= 0.3f;
			if (mean < 1f) { p.Stand *= 0.5f; Sim.NoticeLocal($"{town.Title}: a late frost has caught the corn."); }
			// The weeding is the women's: with fewer hands than rows, the weeds win.
			p.Stand -= (1f - Mathf.Min(1f, mouths / (float)Mathf.Max(1, p.Planted))) * 0.03f;
			p.Stand = Mathf.Clamp(p.Stand, 0f, 1.15f);
		}

		// The corn ripens together, in the first week of September.
		if (cal.Month == 9 && !p.Ripened)
		{
			p.Ripened = true;
			p.Need = mouths * CornPerMouth;
			p.Ripe = Mathf.RoundToInt(p.Planted * CornPerMouth * p.Stand);
		}

		// Gathering, September and October: the hands bring in so much a week,
		// the ripe ears shatter a little every day they stand, and the first hard
		// frost takes most of what is left.
		if (cal.Month is 9 or 10 && p.Ripened && !p.Closed)
		{
			if (mean < 0f && !p.Frosted) { p.Frosted = true; p.Ripe -= Mathf.RoundToInt(p.Ripe * 0.7f); }
			int got = Mathf.Min(p.Ripe, mouths * HarvestPerMouthWeek);
			p.Ripe -= got; p.Harvested += got;
			town.Stores.Add(ItemId.Corn, got);
			p.Ripe -= Mathf.RoundToInt(p.Ripe * 0.08f);
			if (got > 0 && !p.Told) { p.Told = true; Sim.NoticeLocal($"{town.Title}: the corn is being brought in."); }
		}

		// What is still standing in November is lost, and the year is judged.
		if (cal.Month >= 11 && p.Ripened && !p.Closed)
		{
			p.Closed = true; p.Ripe = 0;
			p.LastResult = Judge(p.Harvested, p.Need); p.LastResultYear = p.Year;
			if (Plains.Log) GD.Print($"[plains] {cal.DateString} {town.Name}: {p.Harvested} corn brought in of {p.Need} hoped for ({p.LastResult}); stand {p.Stand:0.00}, frost {p.Frosted}");
			Chronicle.Event($"The corn at {town.Name} came in {p.LastResult}: {p.Harvested} brought in.", at, subject: town.Name, weight: p.LastResult is "failed" or "poor" ? 0.8f : 0.4f);
		}
	}

	// The market: through the summer the nomadic camps that know of a town
	// bring meat and hides and take corn away - and learn how the town stands
	// by being there. Resolved in numbers - these are places no player has ever
	// seen - but the corn really moves, and that is how it reaches the northern
	// posts by way of the Assiniboine.
	private static void Fair(List<CountryBand> towns, GameCalendar cal)
	{
		if (cal.Month is < 6 or > 9) return;
		foreach (var camp in Camps.All)
		{
			var band = camp.Ledger;
			if (band == null || Named(camp.Name)) continue;
			// A fortnight's travel each way is as far as anyone came to the market,
			// and a camp goes where it believes there is corn to be had.
			var town = MarketNews.Best(band, towns, 26000f, 5f, cal);
			if (town == null) continue;
			int meat = band.Stores.TakeUpTo(ItemId.DriedMeat, Mathf.Max(2, band.Mouths));
			if (meat <= 0) continue;
			town.Stores.Add(ItemId.DriedMeat, meat);
			// The town keeps its own back until the harvest.
			int spare = Mathf.Max(0, town.Stores.Count(ItemId.Corn) - town.Mouths * 20);
			int corn = town.Stores.TakeUpTo(ItemId.Corn, Mathf.Min(meat * 2, spare));
			band.Stores.Add(ItemId.Corn, corn);
			MarketNews.Observe(band, town); MarketNews.Meet(band, town);
		}
		// The northern leg: the Assiniboine were the middlemen between the
		// Missouri towns and the Bay, and what they carry north the posts buy.
		var middlemen = Sim.Assiniboine;
		if (middlemen == null) return;
		var nearest = MarketNews.Best(middlemen, towns, float.MaxValue, 20f, cal);
		if (nearest == null) return;
		int bought = nearest.Stores.TakeUpTo(ItemId.Corn, Mathf.Min(40, Mathf.Max(0, nearest.Stores.Count(ItemId.Corn) - nearest.Mouths * 20)));
		if (bought > 0)
		{
			middlemen.Stores.Add(ItemId.Corn, bought);
			int paid = middlemen.Stores.TakeUpTo(ItemId.Pemmican, bought / 3);
			nearest.Stores.Add(ItemId.Pemmican, paid);
			Sim.NoticeLocal($"{middlemen.Name} are back from the Missouri towns with {bought} of corn.");
		}
		MarketNews.Observe(middlemen, nearest); MarketNews.Meet(middlemen, nearest);
	}

	public static Godot.Collections.Dictionary Save()
	{
		var all = new Godot.Collections.Dictionary();
		foreach (var (name, p) in Plots)
		{
			int corn = 0;
			foreach (var c in Camps.All) if (c.Name == name && c.Ledger != null) corn = c.Ledger.Stores.Count(ItemId.Corn);
			all[name] = new Godot.Collections.Dictionary
			{
				["year"] = p.Year, ["planted"] = p.Planted, ["stand"] = p.Stand, ["ripened"] = p.Ripened, ["frosted"] = p.Frosted, ["told"] = p.Told, ["closed"] = p.Closed,
				["ripe"] = p.Ripe, ["harvested"] = p.Harvested, ["need"] = p.Need, ["result"] = p.LastResult, ["resultYear"] = p.LastResultYear, ["corn"] = corn,
			};
		}
		return all;
	}

	public static void Load(Godot.Collections.Dictionary all)
	{
		Plots.Clear(); PendingCorn.Clear();
		if (all == null) return;
		foreach (var kv in all)
		{
			var d = kv.Value.AsGodotDictionary();
			string name = kv.Key.AsString();
			Plots[name] = new Plot
			{
				Year = d["year"].AsInt32(), Planted = d["planted"].AsInt32(), Stand = d["stand"].AsSingle(), Ripened = d["ripened"].AsBool(), Frosted = d["frosted"].AsBool(),
				Told = d["told"].AsBool(), Closed = d["closed"].AsBool(), Ripe = d["ripe"].AsInt32(), Harvested = d["harvested"].AsInt32(), Need = d["need"].AsInt32(),
				LastResult = d["result"].AsString(), LastResultYear = d["resultYear"].AsInt32(),
			};
			PendingCorn[name] = d["corn"].AsInt32();
		}
	}

	// Dev: one line a town for the weekly log.
	public static string Report(CountryBand town)
	{
		if (!Plots.TryGetValue(town.Name, out var p)) return "no fields yet";
		return $"sown {p.Year}: {p.Planted} seed, stand {p.Stand:0.00}, {p.Ripe} standing, {p.Harvested} in; last crop {p.LastResult} ({p.LastResultYear})";
	}

	// Berry season: every camp and post gathers while it lasts, and what is
	// gathered is what keeps the scurvy off. Out of season the posts brew
	// spruce beer instead, which is what the Company actually did.
	private static void Berries(GameCalendar cal)
	{
		bool season = cal.Month is 7 or 8 or 9;
		foreach (var camp in Camps.All)
		{
			if (camp.Ledger == null || Named(camp.Name)) continue;
			if (season) camp.Ledger.Stores.Add(ItemId.Berries, Mathf.Max(2, camp.Ledger.Mouths * 4));
		}
		foreach (var b in Sim.Bands)
			if (b != null && season) b.Stores.Add(ItemId.Berries, Mathf.Max(2, b.Mouths * 3));
		foreach (var post in Sim.Posts())
		{
			if (post == null) continue;
			int mouths = 0;
			foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == post) mouths++;
			if (mouths == 0) continue;
			if (season) { post.Stores.Add(ItemId.Berries, mouths * 2); continue; }
			// Spruce beer: a kettle, a fire and the woods outside. The post
			// brews it when its men have been too long without anything fresh.
			if (!post.Stores.Has(ItemId.Kettle) || !post.Stores.Has(ItemId.Firewood)) continue;
			int wanting = 0;
			foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == post && n.DaysSinceFresh > 14f) wanting++;
			if (wanting == 0) continue;
			post.Stores.Add(ItemId.SpruceBeer, wanting * 3);
			post.Stores.TakeUpTo(ItemId.Firewood, 2);
			Sim.NoticeLocal($"{post.Title}: spruce beer brewed against the scurvy - {wanting} of the men want it.");
		}
	}
}
