using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// One man's part in a war party (Economy/War.cs): mount, ride to the enemy's
// camp, fight whoever of theirs is nearest, and ride home when the party has
// won, been beaten, or given it up.
public class WarPartyGoal : Goal
{
	// A little behind and to one side of the rider, each man his own place.
	public static Vector2 Slot(WarParty party, Npc npc)
	{
		int slot = party.Men.IndexOf(npc);
		return party.Rider.GlobalPosition + new Vector2((slot % 3 - 1) * 90f, 120f + slot / 3 * 70f);
	}

	private readonly WarParty _party;
	private int _phase;   // 0 setting out, 1 riding the route, 2 at the camp, 3 home, 4 riding with the player
	private List<Vector2I> _legs;
	private int _leg, _failed;
	public WarPartyGoal(WarParty party) { _party = party; }
	public override string Label => _phase == 3 ? "ride home from the war party" : $"ride with the war party against {War.NameOf(_party.Against)}";

	public override void OnEnter(Npc npc) => Plains.TakeHorse(npc);
	public override void OnExit(Npc npc) { npc.StopMoving(); Plains.ReturnHorse(npc); }

	public override GoalStatus Tick(Npc npc, float dt, float hours)
	{
		if (_party.Phase is WarPhase.Home or WarPhase.Over && _phase != 3)
		{
			_phase = 3;
			npc.StopMoving();
			// Home the way they came.
			var back = _party.Route != null ? new List<Vector2>(_party.Route) : new List<Vector2> { _party.Target, _party.Home };
			back.Reverse();
			_legs = Legs(npc.GlobalPosition, back, _party.From.Camp);
			_leg = 0;
			return Delegate(new GoToGoal(_legs[0], 300f));
		}
		if (_phase == 3)
		{
			if (!ChildFinished(out _)) return GoalStatus.Running;
			if (++_leg < _legs.Count) return Delegate(new GoToGoal(_legs[_leg], 300f));
			return GoalStatus.Done;
		}
		switch (_phase)
		{
			case 0:
				// Riding with a player: he leads, they keep with him.
				if (_party.Rider is { } r0 && GodotObject.IsInstanceValid(r0) && !r0.IsDead) { _phase = 4; return GoalStatus.Running; }
				// Wait for the way to be worked out, then ride it a leg at a time:
				// the yard's pathfinder is for a few hundred yards, not a hundred miles.
				if (_party.Route == null)
				{
					if (_party.RouteTask != null && !_party.RouteTask.IsCompleted) return GoalStatus.Running;
					var planned = _party.RouteTask != null && !_party.RouteTask.IsFaulted ? _party.RouteTask.Result : null;
					_party.Route = planned != null && planned.Count > 1 ? planned : new List<Vector2> { _party.Home, _party.Target };
				}
				_legs = Legs(npc.GlobalPosition, _party.Route, War.CampOf(_party.Against));
				_leg = 0;
				_phase = 1;
				if (Plains.Log) GD.Print($"[war] {npc.DisplayName}: route {_party.Route.Count} points, {_legs.Count} legs, first {_legs[0]} from {npc.Cell}");
				return Delegate(new GoToGoal(_legs[0], 300f));
			case 1:
				if (_party.Phase == WarPhase.Fighting) { _phase = 2; return GoalStatus.Running; }
				if (!ChildFinished(out var legResult)) return GoalStatus.Running;
				if (Plains.Log) GD.Print($"[war] {npc.DisplayName}: leg {_leg} {legResult} at {npc.Cell}");
				if (legResult != GoalStatus.Done) _failed++;
				// A man who cannot get going at all (boxed in at the lodges) stays home.
				if (_failed >= _legs.Count && npc.GlobalPosition.DistanceTo(_party.Target) > 3000f)
				{
					_party.Men.Remove(npc);
					if (Plains.Log) GD.Print($"[war] {npc.DisplayName} could not get away and stays at home");
					return GoalStatus.Done;
				}
				// A leg that failed (a river, a thicket) is pressed on past.
				if (++_leg < _legs.Count) return Delegate(new GoToGoal(_legs[_leg], _leg == _legs.Count - 1 ? 700f : 300f));
				_phase = 2;
				return GoalStatus.Running;
			case 4:
				if (_party.Phase == WarPhase.Fighting) { _phase = 2; return GoalStatus.Running; }
				var rider = _party.Rider;
				if (rider == null || !GodotObject.IsInstanceValid(rider) || rider.IsDead) { _phase = 0; return GoalStatus.Running; }   // he is gone: ride the route
				var spot = Slot(_party, npc);
				if (npc.GlobalPosition.DistanceTo(spot) > 220f) npc.ChaseToward(spot, dt);
				else npc.StopMoving();
				return GoalStatus.Running;
			case 2:
				if (Child != null) return GoalStatus.Running;
				// The nearest of theirs who can fight, or on into their camp.
				Npc best = null; float bd = 1600f;
				foreach (var e in War.Fighters(_party.Against))
				{
					float d = e.GlobalPosition.DistanceTo(npc.GlobalPosition);
					if (d < bd) { bd = d; best = e; }
				}
				if (best != null) return Delegate(new AttackGoal(best));
				npc.ChaseToward(_party.Target, dt);
				return GoalStatus.Running;
			default:
				if (!ChildFinished(out _)) return GoalStatus.Running;
				return GoalStatus.Done;
		}
	}

	// Points along the route about a day's short walk apart (1500 px), from
	// where he is, ending at the enemy's camp.
	private static List<Vector2I> Legs(Vector2 from, List<Vector2> route, Vector2I end)
	{
		var legs = new List<Vector2I>();
		int start = 0; float best = float.MaxValue;
		for (int i = 0; i < route.Count; i++) { float d = route[i].DistanceTo(from); if (d < best) { best = d; start = i; } }
		var last = from;
		for (int i = start; i < route.Count; i++)
		{
			var p = route[i];
			// A straight run is cut into pieces too.
			while (p.DistanceTo(last) > 1500f) { last = last + (p - last).Normalized() * 1500f; legs.Add(IsoGrid.FromWorld(last)); }
			if (p.DistanceTo(last) > 900f) { last = p; legs.Add(IsoGrid.FromWorld(p)); }
		}
		legs.Add(end);
		return legs;
	}
}
