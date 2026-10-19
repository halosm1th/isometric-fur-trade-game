using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

public enum QuestStatus { Active, Done, Failed }

// Where one player stands in one quest.
public sealed class QuestState
{
	public string Id = "";
	public QuestStatus Status;
	public int At;          // the objective being worked
	public int Count;       // progress on it (lots carried, beasts killed)
	public int StartDay, StepDay;
	public bool TaskSet;    // a task objective has handed its task to the player
}

// One player's quests: what is under way, what is done, what they turned down.
public sealed class QuestLog
{
	public readonly Dictionary<string, QuestState> States = new();
	public readonly HashSet<string> Declined = new();

	public bool IsDone(string id) => States.TryGetValue(id, out var s) && s.Status == QuestStatus.Done;
	public bool IsActive(string id) => States.TryGetValue(id, out var s) && s.Status == QuestStatus.Active;

	public Godot.Collections.Dictionary Save()
	{
		var a = new Godot.Collections.Array();
		foreach (var s in States.Values) a.Add(new Godot.Collections.Array { s.Id, (int)s.Status, s.At, s.Count, s.StartDay, s.StepDay, s.TaskSet });
		var d = new Godot.Collections.Array();
		foreach (var x in Declined) d.Add(x);
		return new Godot.Collections.Dictionary { ["states"] = a, ["declined"] = d };
	}

	public void Restore(Godot.Collections.Dictionary d)
	{
		States.Clear(); Declined.Clear();
		if (d.TryGetValue("states", out var a))
			foreach (var row in a.AsGodotArray())
			{
				var r = row.AsGodotArray();
				if (r.Count < 7) continue;
				var s = new QuestState { Id = r[0].AsString(), Status = (QuestStatus)r[1].AsInt32(), At = r[2].AsInt32(), Count = r[3].AsInt32(), StartDay = r[4].AsInt32(), StepDay = r[5].AsInt32(), TaskSet = r[6].AsBool() };
				States[s.Id] = s;
			}
		if (d.TryGetValue("declined", out var dv)) foreach (var x in dv.AsGodotArray()) Declined.Add(x.AsString());
	}
}

// The campaign's quests, run for each player on the server (Core/QuestScript.cs
// has the shape). A quest starts when what it requires is done and its
// condition holds - at once if nobody gives it, or when its giver is spoken to
// and it is taken up. Its objectives are worked one at a time.
public static class Quests
{
	private static readonly Dictionary<string, QuestDef> _defs = new();
	private static IReadOnlyList<QuestLine> _lines = Array.Empty<QuestLine>();

	public static IReadOnlyList<QuestLine> Lines => _lines;
	public static bool Any => _defs.Count > 0;

	public static void Load(IReadOnlyList<QuestLine> lines)
	{
		_lines = lines ?? Array.Empty<QuestLine>();
		_defs.Clear();
		foreach (var l in _lines) foreach (var q in l.Quests) _defs[q.Id] = q;
	}

	public static QuestDef Def(string id) => id != null && _defs.TryGetValue(id, out var q) ? q : null;
	public static QuestLine LineOf(QuestDef q) { foreach (var l in _lines) if (l.Id == q.Line) return l; return null; }

	private static int Today => Campaign.DayOf(GameCalendar.Instance?.Year ?? 1811, GameCalendar.Instance?.Month ?? 1, GameCalendar.Instance?.Day ?? 1);

	// ---- who is meant ---------------------------------------------------------------

	// The household a place name stands for: a post's books or a camp's.
	public static Economy.Household HouseholdAt(string place)
	{
		if (string.IsNullOrEmpty(place)) return null;
		if (Convoys.Ledger(place) is { } led) return led;
		foreach (var c in Camps.All) if (c.Name == place) return c.Ledger;
		return null;
	}

	// Does this person answer to `who` (QuestScript's forms)?
	public static bool Matches(string who, Npc npc)
	{
		if (string.IsNullOrEmpty(who) || npc == null || npc.IsDead) return false;
		switch (who)
		{
			case "Factor": return npc == Sim.Fort?.Leader;
			case "Partner": return npc == Sim.Nwc?.Leader;
			case "Chief": return npc == Sim.Band?.Leader;
		}
		int at = who.IndexOf('@');
		if (at < 0) return npc.DisplayName == who;
		string role = who[..at].Trim().ToLowerInvariant(), place = who[(at + 1)..].Trim();
		var house = HouseholdAt(place);
		if (house == null || npc.HomeStores != house) return false;
		return role switch
		{
			"leader" or "master" or "factor" => npc == house.Leader,
			"chief" => npc == house.Leader || npc.Role == Role.CreeChief,
			"partner" => npc == house.Leader || npc.Role == Role.NwcPartner,
			"clerk" => npc.Role == Role.Clerk,
			"hunter" => npc.Role == Role.CreeHunter,
			"man" => npc.Role is Role.Labourer or Role.Voyageur or Role.CreeHunter,
			"woman" => npc.IsFemale,
			_ => true,   // anyone of that place
		};
	}

	// A readable name for `who`, for objective text and the journal.
	public static string NameOf(string who)
	{
		if (string.IsNullOrEmpty(who)) return "";
		foreach (var n in Sim.Npcs) if (!n.IsDead && Matches(who, n)) return n.DisplayName;
		int at = who.IndexOf('@');
		return at < 0 ? who : $"the {who[..at]} at {who[(at + 1)..]}";
	}

	// Where a place is, in the world.
	public static Vector2? WhereIs(string place)
	{
		if (string.IsNullOrEmpty(place)) return null;
		if (Convoys.Ledger(place) is { } led) return IsoGrid.ToWorld(led.AccessCell);
		foreach (var c in Camps.All) if (c.Name == place) return IsoGrid.ToWorld(c.Centre);
		if (WorldMap.Place(place) is { } wp) return IsoGrid.ToWorld(wp.Cell);
		foreach (var n in Sim.Npcs) if (!n.IsDead && Matches(place, n)) return n.GlobalPosition;
		return null;
	}

	// ---- running ---------------------------------------------------------------------

	private static bool ForPlayer(QuestDef q, PlayerController p) =>
		string.IsNullOrEmpty(q.For) || q.For.Equals(p.Faction.ToString(), StringComparison.OrdinalIgnoreCase);

	private static bool Available(QuestDef q, PlayerController p)
	{
		if (p.Quests.States.ContainsKey(q.Id) || !ForPlayer(q, p)) return false;
		foreach (var r in q.Requires) if (!p.Quests.IsDone(r)) return false;
		return q.StartWhen == null || Campaign.Check(q.StartWhen, p);
	}

	// Offered by someone, and this is them.
	public static List<QuestDef> OffersFrom(PlayerController p, Npc npc)
	{
		var list = new List<QuestDef>();
		foreach (var q in _defs.Values)
			if (!string.IsNullOrEmpty(q.Giver) && !p.Quests.Declined.Contains(q.Id) && Matches(q.Giver, npc) && Available(q, p)) list.Add(q);
		return list;
	}

	public static void Start(PlayerController p, QuestDef q)
	{
		if (p.Quests.States.ContainsKey(q.Id)) return;
		var st = new QuestState { Id = q.Id, Status = QuestStatus.Active, At = 0, StartDay = Today, StepDay = Today };
		p.Quests.States[q.Id] = st;
		p.Quests.Declined.Remove(q.Id);
		var line = LineOf(q);
		p.Journal.Add($"{GameCalendar.Instance?.DateString}: {(line != null && line.Title != "" ? line.Title + " - " : "")}{q.Title}. {q.Summary}".Trim());
		Sim.Notice($"New: {q.Title}");
		Campaign.RunFor(p, q.OnStart);
		if (q.Objectives.Count == 0) { Finish(p, q, st, true); return; }
		Announce(p, q, st);
	}

	public static void Decline(PlayerController p, QuestDef q) => p.Quests.Declined.Add(q.Id);

	private static void Announce(PlayerController p, QuestDef q, QuestState st)
	{
		var o = q.Objectives[st.At];
		if (o.Text != "") p.Say($"{q.Title}: {Describe(o, st)}");
	}

	// Called a few times a second for each player (server).
	public static void Tick(PlayerController p)
	{
		if (_defs.Count == 0 || p == null || p.IsDead || !Net.Net.IsServer) return;
		foreach (var q in _defs.Values)
			if (string.IsNullOrEmpty(q.Giver) && Available(q, p)) Start(p, q);
		foreach (var st in new List<QuestState>(p.Quests.States.Values))
		{
			if (st.Status != QuestStatus.Active || Def(st.Id) is not { } q) continue;
			if (q.FailWhen != null && Campaign.Check(q.FailWhen, p)) { Finish(p, q, st, false); continue; }
			if (st.At < 0 || st.At >= q.Objectives.Count) { Finish(p, q, st, true); continue; }
			var o = q.Objectives[st.At];
			switch (o.Kind)
			{
				case "reach":
					if (WhereIs(o.Place) is Vector2 at && p.GlobalPosition.DistanceTo(at) <= o.Radius) Advance(p, q, st);
					break;
				case "have":
					if (Enum.TryParse<ItemId>(o.Item, true, out var hid) && p.Inventory.Count(hid) >= o.Count) Advance(p, q, st);
					break;
				case "when":
					if (Campaign.Check(o.Cond, p)) Advance(p, q, st);
					break;
				case "wait":
					if (Today - st.StepDay >= o.Days) Advance(p, q, st);
					break;
				case "task":
					if (!st.TaskSet && Enum.TryParse<TaskKind>(o.Task, true, out var tk))
					{
						st.TaskSet = true;
						if (tk == TaskKind.Brigade)
						{
							var c = Convoys.Find(o.Convoy);
							p.AssignTask(TaskKind.Brigade, 1, "");
							// Bound where the step says; else where the boats are bound now;
							// else (not yet out) the far end of their road.
							string dest = o.Dest != "" ? o.Dest : c != null && c.ToName != "" ? c.ToName : "";
							if (dest == "") foreach (var run in Convoys.Runs) if (run.Name == o.Convoy && run.Stops.Count > 0) dest = run.Stops[^1];
							p.Task.Convoy = o.Convoy; p.Task.Dest = dest;
						}
						else p.AssignTask(tk, o.Count, "");   // the quest's own line says what it is for
					}
					if (st.Count >= o.Count) Advance(p, q, st);
					break;
				// talk, bring and hunt move on from what the player does (OnTalk, Bring, OnKill).
			}
		}
	}

	// Dev: `--quest=<quest>[:<step>]` starts a quest (whatever it requires) and
	// puts it at that step, so a chain can be tried from the middle.
	public static void DevJump(PlayerController p, string questId, string stepId)
	{
		if (Def(questId) is not { } q) { GD.Print($"[quest] no quest '{questId}'"); return; }
		if (!p.Quests.States.ContainsKey(q.Id)) Start(p, q);
		var st = p.Quests.States[q.Id];
		st.Status = QuestStatus.Active;
		if (!string.IsNullOrEmpty(stepId))
		{
			int at = q.IndexOf(stepId);
			if (at < 0) { GD.Print($"[quest] {q.Id} has no step '{stepId}'"); return; }
			st.At = at; st.Count = 0; st.TaskSet = false; st.StepDay = Today;
			Announce(p, q, st);
		}
		GD.Print($"[quest] {q.Id} at step {q.Objectives[st.At].Id}");
	}

	// Done with the objective being worked: on to the next, or a goto's.
	public static void Advance(PlayerController p, QuestDef q, QuestState st, string go = null)
	{
		if (st.Status != QuestStatus.Active) return;
		var o = st.At >= 0 && st.At < q.Objectives.Count ? q.Objectives[st.At] : null;
		if (o != null) Campaign.RunFor(p, o.OnDone);
		if (st.Status != QuestStatus.Active) return;   // an action ended it
		int next = q.After(st.At, go);
		if (next == QuestDef.Complete) { Finish(p, q, st, true); return; }
		if (next == QuestDef.Fail) { Finish(p, q, st, false); return; }
		st.At = next; st.Count = 0; st.TaskSet = false; st.StepDay = Today;
		GD.Print($"[quest] {q.Id} -> step {q.Objectives[next].Id}");
		Announce(p, q, st);
	}

	public static void Finish(PlayerController p, QuestDef q, QuestState st, bool done)
	{
		if (st.Status != QuestStatus.Active) return;
		st.Status = done ? QuestStatus.Done : QuestStatus.Failed;
		p.Journal.Add($"{GameCalendar.Instance?.DateString}: {q.Title} - {(done ? "done" : "failed")}.");
		Sim.Notice(done ? $"Done: {q.Title}" : $"Failed: {q.Title}");
		GD.Print($"[quest] {q.Id} {(done ? "done" : "failed")}");
		Campaign.RunFor(p, done ? q.OnComplete : q.OnFail);
	}

	// The campaign can end a quest from an action.
	public static void FinishById(PlayerController p, string id, bool done)
	{
		if (p == null || Def(id) is not { } q || !p.Quests.States.TryGetValue(id, out var st)) return;
		Finish(p, q, st, done);
	}

	// ---- what the player does --------------------------------------------------------

	public static void OnTask(PlayerController p, TaskKind kind, int n)
	{
		if (p == null || _defs.Count == 0) return;
		foreach (var st in p.Quests.States.Values)
		{
			if (st.Status != QuestStatus.Active || Def(st.Id) is not { } q || st.At >= q.Objectives.Count) continue;
			var o = q.Objectives[st.At];
			if (o.Kind == "task" && Enum.TryParse<TaskKind>(o.Task, true, out var tk) && tk == kind) st.Count += n;
		}
	}

	public static void OnKill(PlayerController p, string species)
	{
		if (p == null || _defs.Count == 0) return;
		foreach (var st in new List<QuestState>(p.Quests.States.Values))
		{
			if (st.Status != QuestStatus.Active || Def(st.Id) is not { } q || st.At >= q.Objectives.Count) continue;
			var o = q.Objectives[st.At];
			if (o.Kind != "hunt" || !(o.Species == "" || o.Species.Equals(species, StringComparison.OrdinalIgnoreCase))) continue;
			if (++st.Count >= o.Count) Advance(p, q, st);
		}
	}

	// The talk and bring objectives this person can answer, under way now.
	public static List<(QuestDef Quest, QuestState State, QuestObjective Step)> TopicsWith(PlayerController p, Npc npc)
	{
		var list = new List<(QuestDef, QuestState, QuestObjective)>();
		foreach (var st in p.Quests.States.Values)
		{
			if (st.Status != QuestStatus.Active || Def(st.Id) is not { } q || st.At >= q.Objectives.Count) continue;
			var o = q.Objectives[st.At];
			if ((o.Kind == "talk" || o.Kind == "bring") && Matches(o.Who, npc)) list.Add((q, st, o));
		}
		return list;
	}

	// The choices of a talk objective that may be offered now.
	public static List<QuestChoice> ChoicesFor(PlayerController p, QuestObjective o)
	{
		var list = new List<QuestChoice>();
		if (o.Dialogue == null) return list;
		foreach (var c in o.Dialogue.Choices) if (c.When == null || Campaign.Check(c.When, p)) list.Add(c);
		return list;
	}

	public static void Choose(PlayerController p, QuestDef q, QuestState st, QuestChoice c)
	{
		Campaign.RunFor(p, c.Do);
		if (st.Status == QuestStatus.Active) Advance(p, q, st, c.Goto);
	}

	// Hand over what a bring objective asks for. False if the player hasn't it.
	public static bool Bring(PlayerController p, Npc npc, QuestDef q, QuestState st, QuestObjective o)
	{
		if (!Enum.TryParse<ItemId>(o.Item, true, out var id) || p.Inventory.Count(id) < o.Count) return false;
		p.Inventory.Remove(id, o.Count);
		npc.Inventory.Add(id, o.Count);
		npc.ChangeRapport(p, 4f);
		Advance(p, q, st);
		return true;
	}

	// ---- reading back ----------------------------------------------------------------

	public static string Describe(QuestObjective o, QuestState st)
	{
		string text = o.Text != "" ? o.Text : o.Kind switch
		{
			"talk" => $"Speak to {NameOf(o.Who)}",
			"reach" => $"Go to {o.Place}",
			"bring" => $"Bring {o.Count} {o.Item} to {NameOf(o.Who)}",
			"have" => $"Get {o.Count} {o.Item}",
			"hunt" => $"Kill {o.Count} {o.Species}",
			"task" => o.Task,
			"wait" => $"Wait {o.Days} days",
			_ => "",
		};
		if (o.Kind is "task" or "hunt" && o.Count > 1) text += $" ({Math.Min(st.Count, o.Count)}/{o.Count})";
		return text;
	}

	// One line per quest, for the journal and the owner's screen: under way
	// first (title, then the objective), then the finished ones.
	public static List<string> Summary(PlayerController p)
	{
		var list = new List<string>();
		foreach (var done in new[] { false, true })
			foreach (var st in p.Quests.States.Values)
			{
				if ((st.Status != QuestStatus.Active) != done || Def(st.Id) is not { } q) continue;
				var line = LineOf(q);
				string head = (line != null && line.Title != "" ? line.Title + ": " : "") + q.Title;
				if (st.Status == QuestStatus.Active && st.At < q.Objectives.Count) list.Add($"{head} - {Describe(q.Objectives[st.At], st)}");
				else list.Add($"{head} ({(st.Status == QuestStatus.Done ? "done" : "failed")})");
			}
		return list;
	}

	// The objective to put on the HUD: the newest quest under way.
	public static string Focus(PlayerController p)
	{
		QuestState best = null;
		foreach (var st in p.Quests.States.Values)
			if (st.Status == QuestStatus.Active && (best == null || st.StepDay >= best.StepDay)) best = st;
		if (best == null || Def(best.Id) is not { } q || best.At >= q.Objectives.Count) return null;
		return $"{q.Title}: {Describe(q.Objectives[best.At], best)}";
	}

	// Is this the person the HUD's step (or an offer) is about? (Dev: --talkto=@quest.)
	public static bool IsFocusPerson(PlayerController p, Npc npc)
	{
		QuestState best = null;
		foreach (var st in p.Quests.States.Values)
			if (st.Status == QuestStatus.Active && (best == null || st.StepDay >= best.StepDay)) best = st;
		if (best != null && Def(best.Id) is { } q && best.At < q.Objectives.Count && Matches(q.Objectives[best.At].Who, npc)) return true;
		return OffersFrom(p, npc).Count > 0;
	}

	// Where the HUD's objective is, if it is somewhere.
	public static Vector2? FocusTarget(PlayerController p)
	{
		QuestState best = null;
		foreach (var st in p.Quests.States.Values)
			if (st.Status == QuestStatus.Active && (best == null || st.StepDay >= best.StepDay)) best = st;
		if (best == null || Def(best.Id) is not { } q || best.At >= q.Objectives.Count) return null;
		var o = q.Objectives[best.At];
		return o.Kind switch
		{
			"reach" => WhereIs(o.Place),
			"talk" or "bring" => WhereIs(o.Who),
			_ => null,
		};
	}
}
