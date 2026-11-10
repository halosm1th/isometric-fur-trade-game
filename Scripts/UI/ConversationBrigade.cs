using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// Going with the boats, asked of the master: it is an order, so it comes
// with his letter for the master at the far end and a place in the
// objective, and a man may ask to take his friends along. A friend he lets
// go follows the player down to the landing, into the boats, and up the
// country (Agents/Goals/FollowGoal rides the boats and the player's own
// canoe with him).
public partial class Conversation
{
    // The order and the letter, whichever way it was asked for.
    private void BrigadeOrders(Npc leader, string boats, string dest, string guide)
    {
        if (_player.Task.Kind != TaskKind.Brigade || _player.Task.Convoy != boats || _player.Task.IsComplete)
            _player.AssignTask(TaskKind.Brigade, 1, guide != "" ? $"{guide} is in charge of the boats" : "be at the landing when they go");
        _player.Task.Convoy = boats; _player.Task.Dest = dest;
        // He is the far post's man from now: York's morning orders are done
        // with him, and its master is who he reports to on arrival.
        if (World.Convoys.Ledger(dest) is { } there && there != leader.HomeStores && _player.PostedTo != dest) _player.PostTo(dest);
        foreach (var m in _player.Mail) if (m.Kind == "orders" && m.Subject == $"a hand with {boats}" && !m.Read) return;   // asked twice: one letter
        string from = leader.DisplayName, who = _player.DisplayName;
        var letter = new Message
        {
            Kind = "orders", From = from, To = $"the master at {dest}", Company = leader.Faction,
            Subject = $"a hand with {boats}",
            Text = $"{from} sends {who} up with {boats} to serve at {dest}. Give him work and his rations; the boats' account is to be squared at the year's end.",
            DayWritten = GameCalendar.Instance?.Day ?? 0,
        };
        letter.OnRead = r => Sim.NoticeAt(r, $"{r?.Describe() ?? "The master"} reads {from}'s letter: {who} is to serve here.");
        _player.Mail.Add(letter);
        _player.Journal.Add($"{GameCalendar.Instance?.DateString}: {from} gave you a letter for the master at {dest} and a place with {boats}.");
    }

    // Men of this post who would go with you, and that the master could spare.
    private List<Npc> FriendsWhoMightGo(Npc leader)
    {
        var list = new List<Npc>();
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead || n == leader || n.HomeStores != leader.HomeStores || n.Faction != _player.Faction) continue;
            // A friend already at your side for the day still needs the master's leave
            // to go up the country (he was left off this list, so Flett could not be asked for).
            if (n.Role is not (Role.Labourer or Role.Voyageur) || n.HeldBy != null || n.AssignedTaskName.StartsWith($"go with {_player.DisplayName} in")) continue;
            if (n.RapportWith(_player) < 10f || n.GlobalPosition.DistanceTo(leader.GlobalPosition) > 4000f) continue;
            list.Add(n);
        }
        list.Sort((a, b) => b.RapportWith(_player).CompareTo(a.RapportWith(_player)));
        if (list.Count > 3) list.RemoveRange(3, list.Count - 3);
        return list;
    }

    // The master has said yes: what he said, then "may I take Andrew Flett?".
    private void ShowBrigadeAgreed(Npc leader, string said, string boats)
    {
        _text = said;
        ClearOptions();
        foreach (var f in FriendsWhoMightGo(leader))
        {
            var friend = f;
            AddOption(friend.Following == _player ? $"\"{friend.DisplayName} is with me. May he come too?\"" : $"\"May I take {friend.DisplayName} with me?\"", () => ShowBrigadeAgreed(leader, TakeFriend(leader, friend, boats), boats));
        }
        AddOption("Back", () => ShowTopics(Greeting(leader)));
    }

    private string TakeFriend(Npc leader, Npc friend, string boats)
    {
        int hands = 0;
        foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == leader.HomeStores && n.Role is Role.Labourer or Role.Voyageur && !n.AssignedTaskName.StartsWith("go with ")) hands++;
        if (hands <= 4) return $"\"{friend.DisplayName}? I've hardly men enough to keep the fires in. No.\"";
        if (_player.Standing < 10f) return $"\"You're asking a favour on a fortnight's service. {friend.DisplayName} stays.\" (standing 10)";
        if (friend.RapportWith(_player) < 10f) return $"{friend.DisplayName} shakes his head. \"I'll not go up the country, not this year.\"";
        friend.AssignedTask = null;
        friend.AssignedTaskName = $"go with {_player.DisplayName} in {boats}";
        friend.Brain.Clear(friend);
        // A season, not a day: he stays at your side until you send him off.
        friend.Brain.Push(friend, new Agents.Goals.FollowGoal(_player, 24f * 120f));
        friend.ChangeRapport(_player, 3f);
        // Rations for the road, from the post's stores.
        if (leader.HomeStores is { } stores) { int got = stores.Stores.TakeUpTo(Items.ItemId.Pemmican, 4); if (got > 0) friend.Inventory.Add(Items.ItemId.Pemmican, got); }
        _player.Standing -= 1f;
        Chronicle.Event($"{leader.DisplayName} let {friend.DisplayName} go with {_player.DisplayName} in {boats}.", leader.GlobalPosition, _player, weight: 0.6f);
        return $"{leader.DisplayName} looks at you a long moment. \"Take him, then. One less mouth at the table.\" {friend.DisplayName} grins and goes for his bag. He will stay at your side, into the boats and up the country.";
    }
}
