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

		// Where you stand, and whether that is up or down on last year.
		string moved = p.LastYearStanding < -900f ? ""
			: p.Standing > p.LastYearStanding + 2f ? $" (up {p.Standing - p.LastYearStanding:0})"
			: p.Standing < p.LastYearStanding - 2f ? $" (down {p.LastYearStanding - p.Standing:0})" : " (as before)";
		lines.Add($"   You are {p.Rank}, standing {p.Standing:0}{moved}.");
		if (p.LastYearRank != "" && p.LastYearRank != p.Rank) lines.Add($"   You came into the year a {p.LastYearRank}.");

		if (p.TasksDone + p.TasksRefused > 0)
			lines.Add($"   Work set you: {p.TasksDone} done, {p.TasksRefused} refused.");
		if (p.PrivateWorthMb > 0.5f)
			lines.Add($"   Your own account stands at {p.PrivateWorthMb:0} MB - the Company's books do not show it.");

		// The house.
		if (p.Spouse != null && !p.Spouse.IsDead)
		{
			int kids = 0;
			foreach (var n in Sim.Npcs) if (!n.IsDead && n.FatherName == p.DisplayName) kids++;
			lines.Add($"   {p.Spouse.DisplayName} is your wife" + (kids > 0 ? $", and there are {kids} of your children at the post." : "."));
		}
		foreach (var f in p.House) if (f.Died == (cal?.Year ?? 0)) lines.Add($"   {f.Name} died this year. {f.End}");

		// What you said you would do, measured.
		if (p.Ambitions.Count == 0)
			lines.Add("   You have declared nothing you mean to do. 1821 comes either way.");
		else
			foreach (var a in p.Ambitions)
			{
				if (a.Abandoned) continue;
				lines.Add("   " + a.Line(p));
			}

		// The country, as far as you have seen it.
		lines.Add($"   Between the houses: {Rivalry.Describe()}");

		foreach (string l in lines) p.Journal.Add(l);
		Sim.Headline(p.Ambitions.Count == 0
			? "The ship is away. The year's account is in your journal."
			: "The ship is away. Your year, and what you said you would do, are in your journal.");

		p.LastYearStanding = p.Standing;
		p.LastYearRank = p.Rank;
		p.TasksDone = 0; p.TasksRefused = 0;
		p.StoodGuardOverTheQuota = false;
	}
}
