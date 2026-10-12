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
			Harvest(camp.Ledger, cal);
		}
		if (towns.Count > 0) Fair(towns, cal);
		Berries(cal);
	}

	// The gardens: the corn comes in through September and October, and the
	// town eats and trades off it all year. A town's harvest is far beyond its
	// own mouths - that is what made it a market.
	private static void Harvest(CountryBand town, GameCalendar cal)
	{
		int mouths = Mathf.Max(1, town.Mouths);
		if (cal.Month is 9 or 10)
		{
			town.Stores.Add(ItemId.Corn, mouths * 30);
			Sim.NoticeLocal($"{town.Title}: the corn is in, and the lodges are full of it.");
		}
		// Green corn, squash and beans in the summer are the fresh food that
		// keeps the towns out of the scurvy their neighbours get.
		if (cal.Month is 7 or 8) town.Stores.Add(ItemId.Berries, mouths * 3);
	}

	// The market: through the summer the nomadic camps within reach bring meat
	// and hides and take corn away. Resolved in numbers - these are places no
	// player has ever seen - but the corn really moves, and that is how it
	// reaches the northern posts by way of the Assiniboine.
	private static void Fair(List<CountryBand> towns, GameCalendar cal)
	{
		if (cal.Month is < 6 or > 9) return;
		foreach (var camp in Camps.All)
		{
			var band = camp.Ledger;
			if (band == null || Named(camp.Name)) continue;
			CountryBand town = null; float best = float.MaxValue;
			foreach (var t in towns)
			{
				float d = IsoGrid.ToWorld(t.Camp).DistanceTo(IsoGrid.ToWorld(band.Camp));
				if (d < best) { best = d; town = t; }
			}
			// A fortnight's travel each way is as far as anyone came to the market.
			if (town == null || best > 26000f) continue;
			int meat = band.Stores.TakeUpTo(ItemId.DriedMeat, Mathf.Max(2, band.Mouths));
			if (meat <= 0) continue;
			town.Stores.Add(ItemId.DriedMeat, meat);
			int corn = town.Stores.TakeUpTo(ItemId.Corn, meat * 2);
			band.Stores.Add(ItemId.Corn, corn);
		}
		// The northern leg: the Assiniboine were the middlemen between the
		// Missouri towns and the Bay, and what they carry north the posts buy.
		var middlemen = Sim.Assiniboine;
		if (middlemen == null) return;
		CountryBand nearest = null; float d2 = float.MaxValue;
		foreach (var t in towns)
		{
			float d = IsoGrid.ToWorld(t.Camp).DistanceTo(IsoGrid.ToWorld(middlemen.Camp));
			if (d < d2) { d2 = d; nearest = t; }
		}
		if (nearest == null) return;
		int bought = nearest.Stores.TakeUpTo(ItemId.Corn, 40);
		if (bought <= 0) return;
		middlemen.Stores.Add(ItemId.Corn, bought);
		int paid = middlemen.Stores.TakeUpTo(ItemId.Pemmican, bought / 3);
		nearest.Stores.Add(ItemId.Pemmican, paid);
		Sim.NoticeLocal($"{middlemen.Name} are back from the Missouri towns with {bought} of corn.");
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
