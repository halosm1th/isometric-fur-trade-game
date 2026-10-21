using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Economy;

// Ship time at the bay (design doc 12.1, 12.5): the outfit comes ashore and
// the furs go aboard on men's backs, a piece or two at a time. Near a player
// the men do it as CarryCargoGoal; out of sight the daily reckoning does it
// in numbers here - the same pieces, at the same two a man a day - so a
// ship nobody watched still lands her goods and sails with the year's furs.
public static class Shipping
{
    // What goes home to London: the furs, and the robes.
    public static bool IsReturn(ItemId id) => Items.Items.Of(id).Kind == ItemKind.Fur || id == ItemId.BuffaloRobe;

    // The ship's own crew lands her cargo and stows the furs, every day she is
    // in, whether or not anyone is watching: a sailor's day is two pieces too.
    public static void CrewDay()
    {
        var ship = Sim.Ship;
        if (ship == null || !ship.IsDocked || Sim.Fort == null || !Net.Net.IsServer) return;
        int hands = 0;
        foreach (var c in ship.Crew) if (c != null && Godot.GodotObject.IsInstanceValid(c) && !c.IsDead) hands++;
        float lbs = hands * 180f;
        // The cargo comes off first; then the furs go aboard.
        if (ship.Cargo.SlotCount > 0) ship.Cargo.MoveByWeight(Sim.Fort.Stores, lbs);
        else Sim.Fort.Stores.MoveByWeight(ship.Hold, lbs, IsReturn);
    }

    // One background man's day at the ship, if the ship is in. True when he spent it there.
    public static bool WorkTheShip(Npc n, Household home)
    {
        var ship = Sim.Ship;
        if (ship == null || !ship.IsDocked || home != Sim.Fort || Sim.Fort == null) return false;
        const float day = 180f;   // two pieces
        if (ship.Cargo.SlotCount > 0) return ship.Cargo.MoveByWeight(Sim.Fort.Stores, day) > 0;
        return Sim.Fort.Stores.MoveByWeight(ship.Hold, day, IsReturn) > 0;
    }
}
