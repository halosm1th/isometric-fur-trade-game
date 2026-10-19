using System;
using System.Collections.Generic;

namespace IsometricFurTradeGame.Scripts.Core;

// Quests: what a campaign asks of a player, as opposed to what it does to the
// world (beats). A campaign file carries quest lines; a line is a run of
// quests; a quest is a chain of objectives, one after another, which a
// choice in a conversation can send elsewhere in the chain (or end the quest
// well or badly). Conditions and actions are the campaign language's own, so
// a quest can wait on anything a beat can, and a beat can ask whether a quest
// is done (quest:id). Read from the JSON tree like the beats, with nothing
// here needing the engine: the authoring tool and the tests read the same.
//
//   "questlines": [ { "id", "title", "summary", "for": "HBC", "quests": [
//     { "id", "title", "summary", "giver": "<who>", "requires": [quest ids],
//       "start_when": cond, "fail_when": cond,
//       "on_start": [actions], "on_complete": [actions], "on_fail": [actions],
//       "objectives": [
//         { "id", "kind": "talk|reach|task|bring|have|hunt|when|wait", "text",
//           "who", "place", "radius", "item", "count", "task", "convoy", "dest",
//           "species", "days", "cond",
//           "dialogue": { "text", "choices": [ { "label", "reply", "when", "do", "goto" } ] },
//           "on_done": [actions], "next": "<objective id>|complete|fail" } ] } ] } ]
//
// Who: a person's name ("Hugh Rendall"), a role at a place ("leader@Edmonton
// House", "chief@<camp>", "clerk@York Factory", "partner@Fort Augustus"), or
// one of the start's people ("Factor", "Partner", "Chief").
public sealed class QuestLine
{
	public string Id = "", Title = "", Summary = "", For = "";
	public readonly List<QuestDef> Quests = new();
}

public sealed class QuestDef
{
	public string Id = "", Line = "", Title = "", Summary = "", Giver = "", For = "";
	public string[] Requires = Array.Empty<string>();
	public List<object> StartWhen, FailWhen, OnStart, OnComplete, OnFail;
	public readonly List<QuestObjective> Objectives = new();

	public int IndexOf(string id)
	{
		for (int i = 0; i < Objectives.Count; i++) if (Objectives[i].Id == id) return i;
		return -1;
	}

	// Where the chain goes after objective `at`: a goto (from a choice, or the
	// objective's own next) or simply the one after. Complete or Fail end it.
	public const int Complete = -1, Fail = -2;
	public int After(int at, string go = null)
	{
		if (string.IsNullOrEmpty(go) && at >= 0 && at < Objectives.Count) go = Objectives[at].Next;
		if (go == "complete") return Complete;
		if (go == "fail") return Fail;
		if (!string.IsNullOrEmpty(go)) { int i = IndexOf(go); if (i >= 0) return i; }
		return at + 1 < Objectives.Count ? at + 1 : Complete;
	}
}

public sealed class QuestObjective
{
	public string Id = "", Kind = "talk", Text = "", Who = "", Place = "", Item = "", Task = "", Convoy = "", Dest = "", Species = "", Next = "";
	public int Count = 1, Days;
	public float Radius = 400f;
	public List<object> Cond, OnDone;
	public QuestDialogue Dialogue;
}

public sealed class QuestDialogue
{
	public string Text = "";
	public readonly List<QuestChoice> Choices = new();
}

public sealed class QuestChoice
{
	public string Label = "", Reply = "", Goto = "";
	public List<object> When, Do;
}

public static class QuestParse
{
	public static readonly string[] Kinds = { "talk", "reach", "task", "bring", "have", "hunt", "when", "wait" };

	public static List<QuestLine> Lines(object tree)
	{
		var lines = new List<QuestLine>();
		if (tree is not List<object> list) return lines;
		foreach (var lo in list)
		{
			if (lo is not Dictionary<string, object> l) continue;
			var line = new QuestLine { Id = Text(l, "id", $"line{lines.Count}"), Title = Text(l, "title", ""), Summary = Text(l, "summary", ""), For = Text(l, "for", "") };
			if (l.GetValueOrDefault("quests") is List<object> qs)
				foreach (var qo in qs)
					if (qo is Dictionary<string, object> q) line.Quests.Add(Quest(q, line));
			lines.Add(line);
		}
		return lines;
	}

	private static QuestDef Quest(Dictionary<string, object> q, QuestLine line)
	{
		var def = new QuestDef
		{
			Id = Text(q, "id", $"{line.Id}-{line.Quests.Count}"), Line = line.Id, Title = Text(q, "title", ""), Summary = Text(q, "summary", ""),
			Giver = Text(q, "giver", ""), For = Text(q, "for", line.For), Requires = Strings(q, "requires"),
			StartWhen = ListOf(q, "start_when"), FailWhen = ListOf(q, "fail_when"),
			OnStart = ListOf(q, "on_start"), OnComplete = ListOf(q, "on_complete"), OnFail = ListOf(q, "on_fail"),
		};
		if (q.GetValueOrDefault("objectives") is List<object> os)
			foreach (var oo in os)
			{
				if (oo is not Dictionary<string, object> o) continue;
				var obj = new QuestObjective
				{
					Id = Text(o, "id", $"step{def.Objectives.Count + 1}"), Kind = Text(o, "kind", "talk"), Text = Text(o, "text", ""),
					Who = Text(o, "who", ""), Place = Text(o, "place", ""), Item = Text(o, "item", ""), Task = Text(o, "task", ""),
					Convoy = Text(o, "convoy", ""), Dest = Text(o, "dest", ""), Species = Text(o, "species", ""), Next = Text(o, "next", ""),
					Count = Math.Max(1, (int)Num(o, "count", 1)), Days = (int)Num(o, "days", 0), Radius = (float)Num(o, "radius", 400),
					Cond = ListOf(o, "cond"), OnDone = ListOf(o, "on_done"),
				};
				if (o.GetValueOrDefault("dialogue") is Dictionary<string, object> d)
				{
					obj.Dialogue = new QuestDialogue { Text = Text(d, "text", "") };
					if (d.GetValueOrDefault("choices") is List<object> cs)
						foreach (var co in cs)
							if (co is Dictionary<string, object> c)
								obj.Dialogue.Choices.Add(new QuestChoice { Label = Text(c, "label", ""), Reply = Text(c, "reply", ""), Goto = Text(c, "goto", ""), When = ListOf(c, "when"), Do = ListOf(c, "do") });
				}
				def.Objectives.Add(obj);
			}
		return def;
	}

	private static string Text(Dictionary<string, object> d, string k, string def) => d.TryGetValue(k, out var v) && v != null ? CampaignLogic.Str(v) : def;
	private static double Num(Dictionary<string, object> d, string k, double def) => d.TryGetValue(k, out var v) && v != null ? CampaignLogic.Num(v) : def;
	private static string[] Strings(Dictionary<string, object> d, string k)
	{
		if (!d.TryGetValue(k, out var v) || v == null) return Array.Empty<string>();
		if (v is List<object> l) { var r = new string[l.Count]; for (int i = 0; i < r.Length; i++) r[i] = CampaignLogic.Str(l[i]); return r; }
		return new[] { CampaignLogic.Str(v) };
	}
	// A condition or an action list, as the beats keep them: absent is null,
	// one thing is a list of one.
	private static List<object> ListOf(Dictionary<string, object> d, string k)
	{
		if (!d.TryGetValue(k, out var v) || v == null) return null;
		return v is List<object> l ? l : new List<object> { v };
	}
}
