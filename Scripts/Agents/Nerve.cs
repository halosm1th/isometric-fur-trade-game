using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Agents;

// Fights are won before the first shot (design doc 15.6, 15.4). With one shot
// in the barrel and a long gap to reload, what decides a fight is position,
// surprise, numbers and nerve - not clicking. A man weighs who is standing
// with him, who has already fallen, whether anyone is giving orders, and
// whether he was ready for this at all; when the sum goes against him he
// breaks, and a party that breaks does not come back that afternoon.
public static class Nerve
{
	private const float Sees = 600f;

	// A house that has just been fired on without warning is shaken for a
	// while, whatever the numbers say.
	private static readonly Dictionary<Faction, float> _shaken = new();

	public static void Shock(Faction f, float hours = 0.25f)
	{
		float now = Now();
		_shaken[f] = Mathf.Max(_shaken.GetValueOrDefault(f), now + hours);
	}

	private static float Now()
	{
		var cal = GameCalendar.Instance;
		return cal == null ? 0f : cal.Day * 24f + cal.Hour;
	}

	private static bool Shaken(Faction f) => _shaken.TryGetValue(f, out float until) && Now() < until;

	// Does he stand? Courage is the floor; the rest is the situation.
	public static bool Stands(Npc n, Agent threat)
	{
		if (n == null || n.IsDead) return false;
		if (n.Weapon == null) return false;                       // nothing to stand with

		int friends = 0, foes = 0; bool leader = false;
		foreach (var a in Sim.AgentsWithin(n.GlobalPosition, Sees))
		{
			if (a is not Npc o || o == n || o.IsDead) continue;
			if (o.Faction == n.Faction)
			{
				friends++;
				if (o.Role is Role.Factor or Role.Postmaster or Role.NwcPartner or Role.CreeChief or Role.Clerk) leader = true;
			}
			else if (threat != null && o.Faction == threat.Faction) foes++;
		}
		if (threat is Player.PlayerController) foes++;

		float nerve = n.Personality.Courage;
		nerve += 0.12f * (friends - foes);
		if (leader) nerve += 0.15f;
		if (n.Needs.Health < 55f) nerve -= 0.3f;
		if (n.Needs.Health < 30f) nerve -= 0.3f;
		if (n.Weapon == Items.ItemId.Musket) nerve += 0.1f;
		if (Shaken(n.Faction)) nerve -= 0.25f;
		if (n.Role == Role.CreeHunter) nerve += 0.1f;             // his own country
		if (n.Intoxication > 40f) nerve += 0.15f;                 // rum courage
		return nerve > 0.45f;
	}

	// A man of a party goes down. Everyone who saw it weighs it again, and the
	// ones whose nerve fails run - together, which is what breaking is.
	public static void TheyBreak(Npc fallen, Agent killer)
	{
		if (fallen == null) return;
		var broke = new List<Npc>();
		foreach (var a in Sim.AgentsWithin(fallen.GlobalPosition, Sees))
		{
			if (a is not Npc o || o == fallen || o.IsDead || o.Faction != fallen.Faction) continue;
			if (o.HeldBy != null || o.Sleeping) continue;   // a prisoner has nowhere to run
			if (o.Brain.Tier0Goal() is FleeGoal) continue;
			// Seeing a messmate shot is worth a long look at the odds.
			if (Stands(o, killer) && o.Personality.Courage > 0.3f) continue;
			o.Brain.Clear(o);
			o.Brain.Push(o, new FleeGoal(killer, 14f));
			broke.Add(o);
		}
		if (broke.Count == 0) return;
		Shock(fallen.Faction, 0.5f);
		var names = new List<string>();
		foreach (var o in broke) names.Add(o.DisplayName);
		string who = fallen.Faction switch
		{
			Faction.NWC => "the Nor'Westers",
			Faction.HBC => "the Company's men",
			_ => "the band's men",
		};
		Sim.Notice(broke.Count > 2
			? $"{fallen.DisplayName} is down and {who} break: {string.Join(", ", names)} run for it."
			: $"{fallen.DisplayName} is down and {string.Join(", ", names)} will not stand it.");
		Chronicle.Event($"{who} broke when {fallen.DisplayName} fell to {killer?.Describe() ?? "a shot"}.",
			fallen.GlobalPosition, killer, fallen.DisplayName, 1.5f);
	}
}
