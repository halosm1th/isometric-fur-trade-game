using System;
using System.Collections.Generic;
using System.Linq;

namespace CampaignTool;

// The quest lines, against the world they name (Scripts/Core/QuestScript.cs).
public static partial class Checker
{
	private static readonly string[] LineKeys = { "id", "title", "summary", "for", "quests" };
	private static readonly string[] QuestKeys = { "id", "title", "summary", "giver", "for", "requires", "start_when", "fail_when", "on_start", "on_complete", "on_fail", "objectives" };
	private static readonly string[] StepKeys = { "id", "kind", "text", "who", "place", "radius", "item", "count", "task", "convoy", "dest", "species", "days", "cond", "dialogue", "on_done", "next" };
	private static readonly string[] ChoiceKeys = { "label", "reply", "when", "do", "goto" };
	private static readonly string[] WhoRoles = { "leader", "master", "factor", "chief", "partner", "clerk", "hunter", "man", "woman", "anyone" };

	// A person a quest names: one of the start's people, a role at a place, or a name.
	private static void CheckWho(string who, string where, Report rep, Known.WorldData world)
	{
		if (who.Length == 0) { rep.Error(where, "names nobody (who is empty)"); return; }
		if (Library.Persons.ContainsKey(who)) return;
		int at = who.IndexOf('@');
		if (at < 0) { rep.Info(where, $"'{who}' is a person's name: it works only while someone of exactly that name is alive"); return; }
		string role = who[..at].Trim().ToLowerInvariant(), place = who[(at + 1)..].Trim();
		if (!WhoRoles.Contains(role)) rep.Warn(where, $"'{role}' is not a role the game knows (it will match anyone at {place}; the roles are {string.Join(", ", WhoRoles)})");
		if (!world.Ledgers.Contains(place) && !world.Camps.Contains(place)) rep.Error(where, $"'{place}' is not a post or a camp, so nobody is {role} there");
	}

	private static bool KnownPlace(string place, Known.WorldData world) =>
		world.Ledgers.Contains(place) || world.Camps.Contains(place) || world.Places.ContainsKey(place);

	private static void CheckQuests(Dictionary<string, object> doc, Report rep, Facts facts, Known.WorldData world)
	{
		var lines = Tree.AsList(doc.GetValueOrDefault("questlines"));
		var questIds = new Dictionary<string, Dictionary<string, object>>();
		var lineIds = new HashSet<string>();
		foreach (var lo in lines)
		{
			if (lo is not Dictionary<string, object> l) { rep.Error("questlines", "a quest line is an object"); continue; }
			string lid = Tree.S(l.GetValueOrDefault("id"));
			string lw = $"questline {lid}";
			if (!lineIds.Add(lid)) rep.Error(lw, "duplicate id");
			foreach (var k in l.Keys) if (!LineKeys.Contains(k)) rep.Error(lw, $"unknown key '{k}' (a quest line has {string.Join(", ", LineKeys)})");
			if (Tree.S(l.GetValueOrDefault("title")).Length == 0) rep.Warn(lw, "has no title");
			if (l.TryGetValue("for", out var lf) && !Known.Companies.Contains(Tree.S(lf))) rep.Error(lw, $"for must be HBC or NWC (got '{Tree.S(lf)}')");
			foreach (var qo in Tree.AsList(l.GetValueOrDefault("quests")))
			{
				if (qo is not Dictionary<string, object> q) { rep.Error(lw, "a quest is an object"); continue; }
				string qid = Tree.S(q.GetValueOrDefault("id"));
				if (questIds.ContainsKey(qid)) rep.Error($"quest {qid}", "duplicate id");
				questIds[qid] = q;
			}
		}

		foreach (var (qid, q) in questIds)
		{
			string qw = $"quest {qid}";
			foreach (var k in q.Keys) if (!QuestKeys.Contains(k)) rep.Error(qw, $"unknown key '{k}' (a quest has {string.Join(", ", QuestKeys)})");
			if (Tree.S(q.GetValueOrDefault("title")).Length == 0) rep.Warn(qw, "has no title");
			if (q.TryGetValue("for", out var qf) && !Known.Companies.Contains(Tree.S(qf))) rep.Error(qw, $"for must be HBC or NWC (got '{Tree.S(qf)}')");
			if (q.TryGetValue("giver", out var gv)) CheckWho(Tree.S(gv), $"{qw} > giver", rep, world);
			foreach (var r in Tree.AsList(q.GetValueOrDefault("requires")))
				if (!questIds.ContainsKey(Tree.S(r))) rep.Error(qw, $"requires '{Tree.S(r)}', which is not a quest");
			foreach (var key in new[] { "start_when", "fail_when" })
			{
				var cl = Tree.AsList(q.GetValueOrDefault(key));
				for (int j = 0; j < cl.Count; j++) CheckCondition(cl[j], $"{qw} > {key}[{j}]", rep, facts, world);
			}
			foreach (var key in new[] { "on_start", "on_complete", "on_fail" }) CheckActions(q.GetValueOrDefault(key), $"{qw} > {key}", rep, facts, world);

			var steps = Tree.AsList(q.GetValueOrDefault("objectives")).OfType<Dictionary<string, object>>().ToList();
			if (steps.Count == 0) rep.Warn(qw, "has no steps: it is done the moment it starts");
			var stepIds = new HashSet<string>();
			foreach (var o in steps) if (!stepIds.Add(Tree.S(o.GetValueOrDefault("id")))) rep.Error($"{qw} > step {Tree.S(o.GetValueOrDefault("id"))}", "duplicate id");
			void Target(string t, string where)
			{
				if (t.Length > 0 && t != "complete" && t != "fail" && !stepIds.Contains(t)) rep.Error(where, $"goes to '{t}', which is not a step of this quest");
			}
			foreach (var o in steps)
			{
				string sid = Tree.S(o.GetValueOrDefault("id"));
				string sw = $"{qw} > step {sid}";
				string kind = Tree.S(o.GetValueOrDefault("kind"));
				if (kind.Length == 0) kind = "talk";
				if (!IsometricFurTradeGame.Scripts.Core.QuestParse.Kinds.Contains(kind)) rep.Error(sw, $"'{kind}' is not a kind of step ({string.Join(", ", IsometricFurTradeGame.Scripts.Core.QuestParse.Kinds)})");
				foreach (var k in o.Keys) if (!StepKeys.Contains(k)) rep.Error(sw, $"unknown key '{k}' (a step has {string.Join(", ", StepKeys)})");
				string S(string k) => Tree.S(o.GetValueOrDefault(k));
				if (o.ContainsKey("who")) CheckWho(S("who"), sw, rep, world);
				if (o.ContainsKey("place") && !KnownPlace(S("place"), world)) rep.Error(sw, $"'{S("place")}' is not a post, a camp or a place on the map");
				if (o.ContainsKey("item") && !world.Items.Contains(S("item"))) rep.Error(sw, $"'{S("item")}' is not an item");
				if (o.ContainsKey("task") && !world.Tasks.Contains(S("task"))) rep.Error(sw, $"'{S("task")}' is not a task ({string.Join(", ", world.Tasks)})");
				if (o.ContainsKey("convoy") && !world.Runs.ContainsKey(S("convoy"))) rep.Error(sw, $"'{S("convoy")}' is not a brigade run");
				if (o.ContainsKey("dest") && !world.Ledgers.Contains(S("dest"))) rep.Warn(sw, $"dest '{S("dest")}' is not a post with books");
				switch (kind)
				{
					case "talk": case "bring": if (!o.ContainsKey("who")) rep.Error(sw, $"a {kind} step needs who"); break;
					case "reach": if (!o.ContainsKey("place")) rep.Error(sw, "a reach step needs a place"); break;
					case "task": if (!o.ContainsKey("task")) rep.Error(sw, "a task step needs a task"); else if (S("task") == "Brigade" && !o.ContainsKey("convoy")) rep.Error(sw, "a Brigade task needs a convoy"); break;
					case "have": if (!o.ContainsKey("item")) rep.Error(sw, "a have step needs an item"); break;
					case "when": if (Tree.AsList(o.GetValueOrDefault("cond")).Count == 0) rep.Error(sw, "a when step needs a condition"); break;
					case "wait": if (!o.ContainsKey("days")) rep.Error(sw, "a wait step needs days"); break;
				}
				if (kind == "bring" && !o.ContainsKey("item")) rep.Error(sw, "a bring step needs an item");
				var cl = Tree.AsList(o.GetValueOrDefault("cond"));
				for (int j = 0; j < cl.Count; j++) CheckCondition(cl[j], $"{sw} > cond[{j}]", rep, facts, world);
				CheckActions(o.GetValueOrDefault("on_done"), $"{sw} > on_done", rep, facts, world);
				Target(S("next"), sw);
				if (o.GetValueOrDefault("dialogue") is Dictionary<string, object> d)
				{
					var choices = Tree.AsList(d.GetValueOrDefault("choices"));
					if (choices.Count > 0 && kind != "talk") rep.Error(sw, "only a talk step has choices");
					for (int ci = 0; ci < choices.Count; ci++)
					{
						string cw = $"{sw} > choice {ci + 1}";
						if (choices[ci] is not Dictionary<string, object> c) { rep.Error(cw, "a choice is an object"); continue; }
						foreach (var k in c.Keys) if (!ChoiceKeys.Contains(k)) rep.Error(cw, $"unknown key '{k}' (a choice has {string.Join(", ", ChoiceKeys)})");
						if (Tree.S(c.GetValueOrDefault("label")).Length == 0) rep.Error(cw, "has no label");
						var wl = Tree.AsList(c.GetValueOrDefault("when"));
						for (int j = 0; j < wl.Count; j++) CheckCondition(wl[j], $"{cw} > when[{j}]", rep, facts, world);
						CheckActions(c.GetValueOrDefault("do"), $"{cw} > do", rep, facts, world);
						Target(Tree.S(c.GetValueOrDefault("goto")), cw);
					}
					if (choices.Count > 0 && choices.OfType<Dictionary<string, object>>().All(c => Tree.AsList(c.GetValueOrDefault("when")).Count > 0))
						rep.Warn(sw, "every choice has a when: if none holds, the player has nothing to say and the step waits");
				}
			}
		}

		// Quests named from a condition or an action must exist.
		foreach (var (name, wheres) in facts.QuestsRead)
			if (!questIds.ContainsKey(name)) foreach (var w in wheres) rep.Error(w, $"names quest '{name}', which this campaign does not have");
		QuestCycles(questIds, rep);
	}

	private static void QuestCycles(Dictionary<string, Dictionary<string, object>> quests, Report rep)
	{
		var state = new Dictionary<string, int>();
		bool Visit(string n, Stack<string> path)
		{
			if (state.GetValueOrDefault(n) == 2) return false;
			if (state.GetValueOrDefault(n) == 1) { rep.Error($"quest {n}", "requires itself through a cycle: " + string.Join(" -> ", path.Reverse().SkipWhile(x => x != n).Append(n))); return true; }
			state[n] = 1; path.Push(n);
			foreach (var r in Tree.AsList(quests[n].GetValueOrDefault("requires")).Select(Tree.S).Where(quests.ContainsKey))
				if (Visit(r, path)) break;
			path.Pop(); state[n] = 2;
			return false;
		}
		foreach (var q in quests.Keys) Visit(q, new Stack<string>());
	}
}
