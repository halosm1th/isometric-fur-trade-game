using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// One man under arms (Economy/Musters.cs): go where the muster is sent and
// stand in his place there; when it is sent against a post or a camp, fight
// whoever of theirs is nearest. Done when the men are stood down, or the
// fight is over: then he goes back to his work.
public class MusterGoal : Goal
{
	private const float LongWay = 8000f;   // further than this, the road is walked a leg at a time
	private readonly Muster _muster;
	private int _order = -1;
	private List<Vector2I> _legs;
	private int _leg;
	private float _retry;
	public MusterGoal(Muster muster) { _muster = muster; }
	public override string Label => _muster.Phase is MusterPhase.Attacking or MusterPhase.Fighting
		? $"go against {War.NameOf(_muster.Foe)}" : $"stand under arms at {_muster.DestName}";

	public override GoalStatus Tick(Npc npc, float dt, float hours)
	{
		if (_muster.Phase == MusterPhase.Over || !_muster.Men.Contains(npc)) { npc.StopMoving(); return GoalStatus.Done; }
		if (_muster.Phase == MusterPhase.Fighting)
		{
			if (Child != null) return GoalStatus.Running;
			ChildFinished(out _);
			Npc best = null; float bd = 1600f;
			foreach (var e in Musters.Defenders(_muster))
			{
				float d = e.GlobalPosition.DistanceTo(npc.GlobalPosition);
				if (d < bd) { bd = d; best = e; }
			}
			if (best != null) return Delegate(new AttackGoal(best));
			return Delegate(new GoToGoal(Musters.SlotCell(_muster, npc), 200f));
		}
		if (Child != null) return GoalStatus.Running;
		// A new order: the way there, once the road is worked out.
		if (_order != _muster.Order)
		{
			if (_muster.RouteTask is { IsCompleted: false }) return GoalStatus.Running;
			ChildFinished(out _);
			_order = _muster.Order;
			var end = Musters.SlotCell(_muster, npc);
			_legs = npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(end)) > LongWay && _muster.Route != null
				? Legs(npc.GlobalPosition, _muster.Route, end) : new List<Vector2I> { end };
			_leg = 0;
			return Delegate(new GoToGoal(_legs[0], _legs.Count == 1 ? 120f : 300f));
		}
		if (ChildFinished(out var result))
		{
			// A leg on the road that could not be walked is pressed on past; his
			// own place is tried again in a moment.
			if (_leg < _legs.Count - 1) { _leg++; return Delegate(new GoToGoal(_legs[_leg], _leg == _legs.Count - 1 ? 120f : 300f)); }
			if (result != GoalStatus.Done) _retry = 3f;
		}
		// In his place: stand, and keep there.
		var spot = Musters.SlotCell(_muster, npc);
		npc.StopMoving();
		if (npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(spot)) > 250f && (_retry -= dt) <= 0f)
		{
			_leg = _legs.Count - 1;
			return Delegate(new GoToGoal(spot, 120f));
		}
		return GoalStatus.Running;
	}

	public override void OnExit(Npc npc) => npc.StopMoving();

	// Points along the road about 1500 px apart, ending at his place: the
	// yard's pathfinder is for a few hundred yards, not a hundred miles.
	private static List<Vector2I> Legs(Vector2 from, List<Vector2> route, Vector2I end)
	{
		var legs = new List<Vector2I>();
		var last = from;
		foreach (var p in route)
		{
			while (p.DistanceTo(last) > 1500f) { last += (p - last).Normalized() * 1500f; legs.Add(IsoGrid.FromWorld(last)); }
			if (p.DistanceTo(last) > 300f) { last = p; legs.Add(IsoGrid.FromWorld(p)); }
		}
		legs.Add(end);
		return legs;
	}
}
