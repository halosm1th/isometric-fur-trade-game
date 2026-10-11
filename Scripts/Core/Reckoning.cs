using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.Core;

// The year has a close, and the close is the ship (design doc P.3, 14.1): the
// trade year ends when she sails with the returns, and that is the moment to
// set down what the year came to. Nothing here is knowledge the player did not
// have - it is his own book, written up, with his ambitions measured against
// what he actually did (design doc P.2, P.4).
public static class Reckoning
{
	public static void AtShipSailing(Player.PlayerController p, float shippedMb, FortLedger fort)
	{
		if (p == null || p.IsDead) return;
		var cal = GameCalendar.Instance;
		string date = cal?.DateString ?? "";
		var lines = new List<string>();

		lines.Add($"{date}: the ship is away, and the year's account.");
		if (fort != null && p.Faction == Faction.HBC)
			lines.Add($"   The post shipped {shippedMb:0} MB against a quota of {fort.QuotaMb:0}" +
				(shippedMb >= fort.QuotaMb ? " - made, and then some." : $" - {fort.QuotaMb - shippedMb:0} short.") +
				(p.StoodGuardOverTheQuota ? " You had a hand in it." : ""));

		Standing(p, lines);
		Work(p, lines);
		House(p, lines);
		Ambitions(p, lines);
		Country(lines);
		Close(p, lines, p.Ambitions.Count == 0
			? "The ship is away. The year's account is in your journal."
			: "The ship is away. Your year, and what you said you would do, are in your journal.");
	}

	// The Nor'Wester's year closes when the canoes take the returns off to
	// Montreal and the council's minutes come north in their place (design
	// doc 10.1.3): the same reckoning, a different sail.
	public static void AtBrigadeAway(Player.PlayerController p, FortLedger house, float shippedMb)
	{
		if (p == null || p.IsDead || p.Faction != Faction.NWC) return;
		var cal = GameCalendar.Instance;
		var lines = new List<string>
		{
			$"{cal?.DateString}: the brigade is away for Fort William, and the year's account.",
		};
		if (house != null)
			lines.Add($"   The house sent off {shippedMb:0} MB of furs against the {house.QuotaMb:0} the partners looked for" +
				(shippedMb >= house.QuotaMb ? " - made." : $" - {house.QuotaMb - shippedMb:0} short.") +
				(p.StoodGuardOverTheQuota ? " You had a hand in it." : ""));
		Standing(p, lines);
		Work(p, lines);
		House(p, lines);
		Ambitions(p, lines);
		Country(lines);
		Close(p, lines, "The brigade is away for Fort William. Your year is in your journal.");
	}

	// A band man's year does not close with anybody's sail. It closes when the
	// ice goes out and the geese come back: the camp counts what it came
	// through the winter with, and the summer ground is ahead of it.
	private static int _springYear = -1;
	public static void Spring()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		if (cal.DayOfYear < 135f || cal.Year == _springYear) return;
		_springYear = cal.Year;
		foreach (var p in Sim.Players()) AtBreakUp(p);
	}

	public static void AtBreakUp(Player.PlayerController p)
	{
		if (p == null || p.IsDead || !p.IsBandPerson) return;
		var cal = GameCalendar.Instance;
		var band = BandOf(p);
		var lines = new List<string>
		{
			$"{cal?.DateString}: the ice is out and the geese are back. The year turns.",
		};
		if (band != null)
		{
			int mouths = band.Mouths;
			lines.Add($"   {band.Name} came through the winter {mouths} strong, with {band.FoodDays(mouths):0} days' food in the cache.");
			string dealings = band.StandingWithCompany > 65f ? "well" : band.StandingWithCompany > 35f ? "civilly" : band.WillTrade ? "coldly" : "not at all";
			lines.Add($"   You stand {dealings} with the Company at the post" +
				(band.DebtMb > 0.5f ? $", and {band.DebtMb:0.#} MB of the band's debt is still on their books." : "."));
			if (band.AtOddsWithOtherBand) lines.Add($"   You are at odds with {band.OtherBand?.Name ?? "the other band"}; their hunters and yours will not pass each other quietly.");
		}
		if (p.PrivateWorthMb > 0.5f) lines.Add($"   What you have of your own: {p.PrivateWorthMb:0} MB in furs and goods.");
		House(p, lines);
		Ambitions(p, lines);
		Country(lines);
		Close(p, lines, "The ice is out. Your year is in your journal.");
	}

	// A player belongs to a band by his nation, not by a household link. The
	// York country's two bands are in Sim.Bands; the wider country's camps are
	// not, so they are found through their own people.
	private static BandLedger BandOf(Player.PlayerController p)
	{
		foreach (var b in Sim.Bands) if (b != null && b.Nation == p.Faction) return b;
		BandLedger nearest = null; float best = float.MaxValue;
		foreach (var n in Sim.Npcs)
		{
			if (n.IsDead || n.HomeStores is not BandLedger camp || camp.Nation != p.Faction) continue;
			float d = n.GlobalPosition.DistanceTo(p.GlobalPosition);
			if (d < best) { best = d; nearest = camp; }
		}
		return nearest;
	}

	// ---- the parts every close shares --------------------------------------

	private static void Standing(Player.PlayerController p, List<string> lines)
	{
		string moved = p.LastYearStanding < -900f ? ""
			: p.Standing > p.LastYearStanding + 2f ? $" (up {p.Standing - p.LastYearStanding:0})"
			: p.Standing < p.LastYearStanding - 2f ? $" (down {p.LastYearStanding - p.Standing:0})" : " (as before)";
		lines.Add($"   You are {p.Rank}, standing {p.Standing:0}{moved}.");
		if (p.LastYearRank != "" && p.LastYearRank != p.Rank) lines.Add($"   You came into the year a {p.LastYearRank}.");
	}

	private static void Work(Player.PlayerController p, List<string> lines)
	{
		if (p.TasksDone + p.TasksRefused > 0)
			lines.Add($"   Work set you: {p.TasksDone} done, {p.TasksRefused} refused.");
		if (p.PrivateWorthMb > 0.5f)
			lines.Add($"   Your own account stands at {p.PrivateWorthMb:0} MB - the Company's books do not show it.");
	}

	private static void House(Player.PlayerController p, List<string> lines)
	{
		var cal = GameCalendar.Instance;
		if (p.Spouse != null && !p.Spouse.IsDead)
		{
			int kids = 0;
			foreach (var n in Sim.Npcs) if (!n.IsDead && n.FatherName == p.DisplayName) kids++;
			lines.Add($"   {p.Spouse.DisplayName} is your wife" + (kids > 0 ? $", and there are {kids} of your children at the post." : "."));
		}
		foreach (var f in p.House) if (f.Died == (cal?.Year ?? 0)) lines.Add($"   {f.Name} died this year. {f.End}");
	}

	private static void Ambitions(Player.PlayerController p, List<string> lines)
	{
		if (p.Ambitions.Count == 0)
		{
			lines.Add("   You have declared nothing you mean to do. 1821 comes either way.");
			return;
		}
		foreach (var a in p.Ambitions)
		{
			if (a.Abandoned) continue;
			lines.Add("   " + a.Line(p));
		}
	}

	private static void Country(List<string> lines) => lines.Add($"   Between the houses: {Rivalry.Describe()}");

	// Write it up, say so, and start the year's counters again.
	private static void Close(Player.PlayerController p, List<string> lines, string headline)
	{
		foreach (string l in lines) p.Journal.Add(l);
		Sim.Headline(headline);
		YearStory.Write(p);   // the same year told rather than tallied
		p.LastYearStanding = p.Standing;
		p.LastYearRank = p.Rank;
		p.TasksDone = 0; p.TasksRefused = 0;
		p.StoodGuardOverTheQuota = false;
	}
}
