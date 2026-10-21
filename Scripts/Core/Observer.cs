using System;
using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// Watching the world without a character in it (the observer). The player
// is still there - the live simulation, the terrain and the weather all
// follow the local player - but as a ghost: no body, no needs, no place in
// anyone's sight or anyone's reach (PlayerController.Ghost), free to fly over
// the country at any zoom, with the whole map and what is moving on it drawn
// underneath when the camera is far out (UI/ObserverView, UI/ObserverHud).
// Started from the new game page ("Watch the world") or with `-- --observe`.
public static class Observer
{
    public static bool On = Array.IndexOf(OS.GetCmdlineUserArgs(), "--observe") >= 0;

    // What the camera is following, if anything: a name and where it is now.
    public static string FollowName = "";
    public static Func<Vector2?> Follow;

    // Things happening in the country worth watching, for Tab to step through.
    public static List<(string Name, Func<Vector2?> Where)> Happenings()
    {
        var list = new List<(string, Func<Vector2?>)>();
        foreach (var p in Economy.War.Parties)
        {
            var party = p;
            list.Add(($"{Economy.War.NameOf(p.From)}'s war party against {Economy.War.NameOf(p.Against)}", () =>
            {
                foreach (var m in party.Men) if (m != null && GodotObject.IsInstanceValid(m) && !m.IsDead && m.Visible) return m.GlobalPosition;
                return party.Phase == Economy.WarPhase.Over ? null : party.Target;
            }));
        }
        foreach (var r in Ecology.BuffaloRun.Active)
        {
            var run = r;
            list.Add(($"{run.Band.Name} running the buffalo at {run.Site.Name}", () => run.Phase == Ecology.RunPhase.Over ? null : run.Site.WorldPos));
        }
        foreach (var (raider, target, lead) in Economy.Plains.LiveRaids())
        {
            var who = lead;
            list.Add(($"{Economy.War.NameOf(raider)}'s horse thieves at {Economy.War.NameOf(target)}", () => who != null && GodotObject.IsInstanceValid(who) && !who.IsDead ? who.GlobalPosition : null));
        }
        foreach (var t in Economy.TradeTrips.All)
        {
            var trip = t;
            list.Add(($"{trip.Camp.Name}'s men going in to trade at {trip.Post.Post.Name}", () => Economy.TradeTrips.Where(trip)));
        }
        foreach (var c in World.Convoys.All)
        {
            if (c.Now == World.ConvoyState.Arrived) continue;
            var cv = c;
            list.Add(($"{cv.Name}, {cv.FromName} to {cv.ToName}", () => cv.Now == World.ConvoyState.Arrived ? null : cv.Where));
        }
        return list;
    }
}
