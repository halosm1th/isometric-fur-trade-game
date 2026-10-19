using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using IsometricFurTradeGame.Scripts.Core;

namespace CampaignTool;

// Check a campaign script: things the game would warn about or silently
// ignore. Errors cannot work (a typo in an action, a beat that requires a beat
// that does not exist, a cycle). Warnings are probably not what you meant (a
// flag nothing sets, a beat that can never fire, a date that contradicts what
// it requires). Notes are only worth knowing.
public sealed class Report
{
	public readonly List<(string where, string msg)> Errors = new(), Warnings = new(), Infos = new();
	public void Error(string where, string msg) => Errors.Add((where, msg));
	public void Warn(string where, string msg) => Warnings.Add((where, msg));
	public void Info(string where, string msg) => Infos.Add((where, msg));

	public List<string> Lines(bool verbose = false)
	{
		var l = Errors.Select(e => $"error: {e.where}: {e.msg}").Concat(Warnings.Select(w => $"warning: {w.where}: {w.msg}")).ToList();
		if (verbose) l.AddRange(Infos.Select(i => $"note: {i.where}: {i.msg}"));
		return l;
	}
}

public sealed class Atom
{
	public char Kind;               // 'p' a fact, 'm' a number
	public string Name;
	public List<string> Args = new();
}

public static partial class Checker
{
	private static readonly string[] Kinds = BeatKinds.All;
	private static readonly string[] BeatKeys = { "id", "title", "kind", "date", "window_days", "lead_days", "delay_days", "repeat_days", "requires", "keywords", "rumour", "when", "on_awaken", "pressure", "activate_when", "happens", "done_when", "on_resolve", "cancel_when", "on_cancel" };
	private static readonly HashSet<string> FlagWords = new() { "heard", "late", "lost", "robbed", "turned", "out", "in" };
	private static readonly Regex CompareRe = new(@"^\s*([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$");
	private static readonly Regex CallRe = new(@"^\s*([a-z_]+)\s*\(([^)]*)\)\s*$");

	private sealed class Facts
	{
		public readonly Dictionary<string, List<string>> FlagsSet = new(), FlagsRead = new(), VarsSet = new(), VarsRead = new(), Triggers = new(), QuestsRead = new();
		public static void Add(Dictionary<string, List<string>> d, string k, string where) { if (!d.TryGetValue(k, out var l)) d[k] = l = new(); l.Add(where); }
	}

	public static List<Atom> Atoms(object c, List<Atom> outp = null)
	{
		outp ??= new List<Atom>();
		switch (c)
		{
			case null: case bool: break;
			case List<object> l: foreach (var x in l) Atoms(x, outp); break;
			case string str:
				{
					string s = str.Trim().TrimStart('!').Trim();
					var m = CompareRe.Match(s);
					if (m.Success) outp.Add(new Atom { Kind = 'm', Name = m.Groups[1].Value, Args = CampaignLogic.SplitArgs(m.Groups[2].Value).ToList() });
					else if (s.IndexOf(':') > 0 && FlagWords.Contains(s[..s.IndexOf(':')].Trim())) outp.Add(new Atom { Kind = 'p', Name = "flag", Args = { s } });
					else if (s.Contains(':')) { int i = s.IndexOf(':'); outp.Add(new Atom { Kind = 'p', Name = s[..i].Trim(), Args = { s[(i + 1)..].Trim() } }); }
					else if (CallRe.IsMatch(s)) { var cm = CallRe.Match(s); outp.Add(new Atom { Kind = 'p', Name = cm.Groups[1].Value, Args = CampaignLogic.SplitArgs(cm.Groups[2].Value).ToList() }); }
					else if (s.Length > 0) outp.Add(new Atom { Kind = 'p', Name = "flag", Args = { s } });
					break;
				}
			case Dictionary<string, object> d:
				foreach (var k in new[] { "all", "any" }) if (d.TryGetValue(k, out var v)) Atoms(v, outp);
				if (d.TryGetValue("not", out var nv)) Atoms(nv, outp);
				if (d.TryGetValue("p", out var p)) outp.Add(new Atom { Kind = 'p', Name = Tree.S(p), Args = Tree.AsList(d.GetValueOrDefault("a")).Select(Tree.S).ToList() });
				if (d.TryGetValue("m", out var m2)) outp.Add(new Atom { Kind = 'm', Name = Tree.S(m2), Args = Tree.AsList(d.GetValueOrDefault("a")).Select(Tree.S).ToList() });
				break;
		}
		return outp;
	}

	public static Report Check(Dictionary<string, object> doc)
	{
		var rep = new Report();
		var facts = new Facts();
		var world = Known.World;
		if (doc.GetValueOrDefault("vars") is Dictionary<string, object> initial) foreach (var k in initial.Keys) Facts.Add(facts.VarsSet, k, "vars");
		var beats = Tree.AsList(doc.GetValueOrDefault("beats")).Cast<Dictionary<string, object>>().ToList();
		var ids = new Dictionary<string, Dictionary<string, object>>();
		for (int i = 0; i < beats.Count; i++)
		{
			string bid = beats[i].TryGetValue("id", out var idv) ? Tree.S(idv) : $"beat{i}";
			if (ids.ContainsKey(bid)) rep.Error($"beat {bid}", "duplicate id");
			ids[bid] = beats[i];
		}

		foreach (var b in beats)
		{
			string bid = b.TryGetValue("id", out var idv) ? Tree.S(idv) : "?";
			string where = $"beat {bid}";
			string kind = b.TryGetValue("kind", out var kv) ? Tree.S(kv) : "event";
			if (!Kinds.Contains(kind)) rep.Error(where, $"unknown kind '{kind}' (use {string.Join(", ", Kinds)})");
			bool hasDate = b.TryGetValue("date", out var dv) && Tree.S(dv).Length > 0;
			bool hasRequires = Tree.AsList(b.GetValueOrDefault("requires")).Count > 0, hasWhen = Tree.AsList(b.GetValueOrDefault("when")).Count > 0;
			if (hasDate)
			{
				string date = Tree.S(dv);
				if (!Regex.IsMatch(date, @"^\d{4}-\d\d-\d\d$")) rep.Error(where, $"date '{date}' is not YYYY-MM-DD");
				else if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) rep.Error(where, $"date '{date}' is not a real day");
			}
			else if (kind == "fixed") rep.Error(where, "a fixed beat needs a date");
			else if ((kind == "event" || kind == "pressure") && !hasRequires && !hasWhen) rep.Warn(where, $"{kind} beat with no date and nothing to wait for: it will happen on the first day");
			if (kind == "pressure" && Tree.AsList(b.GetValueOrDefault("happens")).Count > 0) rep.Warn(where, "a pressure beat's 'happens' never runs (only its pressure does)");
			if (Tree.AsList(b.GetValueOrDefault("done_when")).Count > 0 && (kind == "fixed" || kind == "pressure")) rep.Warn(where, "done_when is only used by event beats and conditional ones");
			if (b.TryGetValue("repeat_days", out var rd) && Tree.N(rd) != 0 && !hasDate && kind != "conditional") rep.Warn(where, "repeat without a date repeats only after its cooldown");
			foreach (var key in b.Keys)
				if (!BeatKeys.Contains(key)) rep.Error(where, $"unknown key '{key}' (a beat has {string.Join(", ", BeatKeys)})");
			foreach (var key in new[] { "when", "activate_when", "done_when", "cancel_when" })
			{
				var l = Tree.AsList(b.GetValueOrDefault(key));
				for (int j = 0; j < l.Count; j++) CheckCondition(l[j], $"{where} > {key}[{j}]", rep, facts, world);
			}
			foreach (var key in new[] { "on_awaken", "pressure", "happens", "on_resolve", "on_cancel" }) CheckActions(b.GetValueOrDefault(key), $"{where} > {key}", rep, facts, world);
			foreach (var r in Tree.AsList(b.GetValueOrDefault("requires"))) Facts.Add(facts.FlagsRead, Tree.S(r), where);
		}

		CheckQuests(doc, rep, facts, world);

		foreach (var (t, wheres) in facts.Triggers)
			if (!ids.ContainsKey(t)) foreach (var w in wheres) rep.Error(w, $"trigger of unknown beat '{t}'");
		var settable = new HashSet<string>(facts.FlagsSet.Keys);
		settable.UnionWith(ids.Keys); settable.UnionWith(Known.EngineFlags);
		foreach (var b in beats)
			foreach (var ro in Tree.AsList(b.GetValueOrDefault("requires")))
			{
				string r = Tree.S(ro);
				if (!ids.ContainsKey(r) && !settable.Contains(r) && !Known.EngineFlagPrefixes.Any(r.StartsWith))
					rep.Error($"beat {Tree.S(b.GetValueOrDefault("id"))}", $"requires '{r}', which is not a beat and no action sets it as a flag");
			}
		foreach (var (name, wheres) in facts.FlagsRead)
		{
			if (settable.Contains(name) || Known.EngineFlagPrefixes.Any(name.StartsWith)) continue;
			rep.Warn(wheres[0], $"reads flag '{name}', which nothing sets");
		}
		foreach (var (name, wheres) in facts.FlagsSet)
			if (!facts.FlagsRead.ContainsKey(name) && !ids.ContainsKey(name)) rep.Info(wheres[0], $"sets flag '{name}', which nothing reads (fine if it is only for the log)");
		foreach (var (name, wheres) in facts.VarsRead)
			if (name.StartsWith("deed:")) continue;   // the game counts the player's deeds
			else
			if (!facts.VarsSet.ContainsKey(name)) rep.Warn(wheres[0], $"reads variable '{name}', which nothing sets (it will always be 0)");

		Cycles(beats, ids, rep);
		Reachability(beats, ids, facts, rep);
		DateOrder(beats, ids, rep);
		return rep;
	}

	private static void CheckCondition(object c, string where, Report rep, Facts facts, Known.WorldData world)
	{
		foreach (var a in Atoms(c))
		{
			string arg = a.Args.Count > 0 ? a.Args[0] : "";
			if (a.Kind == 'p')
			{
				if (!Known.Predicates.Contains(a.Name)) { rep.Error(where, $"unknown condition '{a.Name}' (known: {string.Join(", ", Known.Predicates.OrderBy(x => x, StringComparer.Ordinal))})"); continue; }
				if (a.Name == "flag" || a.Name == "beat") Facts.Add(facts.FlagsRead, arg, where);
				else if ((a.Name == "after" || a.Name == "before") && !Regex.IsMatch(arg, @"^\d{4}-\d\d-\d\d$")) rep.Error(where, $"{a.Name}:{arg} needs a date (YYYY-MM-DD)");
				else if (a.Name == "post" && !world.Posts.Contains(arg) && arg != "York Factory" && arg != "Oxford House") rep.Error(where, $"post '{arg}' is not on the map");
				else if (a.Name == "month" && !Regex.IsMatch(arg, @"^\d{1,2}(-\d{1,2})?$")) rep.Error(where, $"month:{arg} should be a month number or a range like 6-8");
				else if (a.Name == "chance" && !double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) rep.Error(where, $"chance:{arg} needs a percentage");
				else if (a.Name is "quest" or "quest_active" or "quest_failed") Facts.Add(facts.QuestsRead, arg, where);
				else if (a.Name == "posted" && !world.Ledgers.Contains(arg)) rep.Error(where, $"posted: '{arg}' is not a post with books");
				else if (a.Name.StartsWith("convoy_") && a.Name != "convoy_wintering" && !world.Runs.ContainsKey(arg)) rep.Error(where, $"'{arg}' is not a brigade run (known: {string.Join(", ", world.Runs.Keys.OrderBy(x => x, StringComparer.Ordinal))})");
			}
			else
			{
				if (!Known.Metrics.Contains(a.Name)) { rep.Error(where, $"unknown number '{a.Name}' (known: {string.Join(", ", Known.Metrics.OrderBy(x => x, StringComparer.Ordinal))})"); continue; }
				if (a.Name == "var" && a.Args.Count > 0) Facts.Add(facts.VarsRead, a.Args[0], where);
				if (a.Name == "stock" && (a.Args.Count != 2 || !world.Ledgers.Contains(a.Args[0]) || !world.Items.Contains(a.Args[1]))) rep.Error(where, $"stock(POST, ITEM): got ({string.Join(", ", a.Args)})");
				if ((a.Name == "convoy_days_late" || a.Name == "convoy_days_out") && (a.Args.Count == 0 || !world.Runs.ContainsKey(a.Args[0]))) rep.Error(where, $"{a.Name} needs a brigade run");
			}
		}
	}

	private static void CheckActions(object acts, string where, Report rep, Facts facts, Known.WorldData world)
	{
		var list = Tree.AsList(acts);
		for (int i = 0; i < list.Count; i++)
		{
			string w = $"{where}[{i}]";
			if (list[i] is not Dictionary<string, object> a) { rep.Error(w, $"not an action: {list[i]}"); continue; }
			string verb = a.TryGetValue("do", out var vo) ? Tree.S(vo) : null;
			if (verb == null || !Known.Actions.TryGetValue(verb, out var sig)) { rep.Error(w, $"unknown action '{verb}' (known: {string.Join(", ", Known.Actions.Keys.OrderBy(x => x, StringComparer.Ordinal))})"); continue; }
			foreach (var f in sig.req) if (!a.ContainsKey(f)) rep.Error(w, $"{verb} needs '{f}'");
			foreach (var f in a.Keys) if (f != "do" && f != "_beat" && !sig.req.Contains(f) && !sig.opt.Contains(f)) rep.Error(w, $"{verb} has no field '{f}'");
			if (verb == "flag" && a.ContainsKey("name")) Facts.Add(facts.FlagsSet, Tree.S(a["name"]), w);
			if (verb == "unflag" && a.ContainsKey("name")) Facts.Add(facts.FlagsRead, Tree.S(a["name"]), w);
			if ((verb == "set" || verb == "add") && a.ContainsKey("var")) Facts.Add(facts.VarsSet, Tree.S(a["var"]), w);
			if (verb == "trigger" && a.ContainsKey("beat")) Facts.Add(facts.Triggers, Tree.S(a["beat"]), w);
			switch (verb)
			{
				case "if":
					CheckCondition(a.GetValueOrDefault("cond"), w + " > cond", rep, facts, world);
					CheckActions(a.GetValueOrDefault("then"), w + " > then", rep, facts, world);
					CheckActions(a.GetValueOrDefault("else"), w + " > else", rep, facts, world);
					break;
				case "random":
					{
						var opts = Tree.AsList(a.GetValueOrDefault("options"));
						double total = opts.OfType<Dictionary<string, object>>().Sum(o => Math.Max(0, o.TryGetValue("weight", out var wt) ? Tree.N(wt) : 1));
						if (opts.Count == 0 || total <= 0) rep.Error(w, "random needs options with a weight above 0");
						for (int j = 0; j < opts.Count; j++) if (opts[j] is Dictionary<string, object> o) CheckActions(o.GetValueOrDefault("do"), $"{w} > option[{j}]", rep, facts, world);
						break;
					}
				case "after":
					if (a.GetValueOrDefault("days") is not double dd || dd < 0) rep.Error(w, "after needs a number of days");
					CheckActions(a.GetValueOrDefault("then"), w + " > then", rep, facts, world);
					break;
				case "found_post":
					if (!world.Posts.Contains(Tree.S(a.GetValueOrDefault("place")))) rep.Error(w, $"found_post: '{Tree.S(a.GetValueOrDefault("place"))}' is not a post on the map");
					break;
				case "brigade":
					{
						string run = Tree.S(a.GetValueOrDefault("run"));
						if (!world.Runs.ContainsKey(run)) rep.Error(w, $"brigade: '{run}' is not a brigade run (known: {string.Join(", ", world.Runs.Keys.OrderBy(x => x, StringComparer.Ordinal))})");
						string effect = a.TryGetValue("effect", out var ef) ? Tree.S(ef) : "delay";
						if (!Known.BrigadeEffects.Contains(effect)) rep.Error(w, $"brigade effect '{effect}' is not one of {string.Join(", ", Known.BrigadeEffects)}");
						else if (effect == "launch" && world.Runs.TryGetValue(run, out var rr))
						{
							int legs = Tree.AsList(rr["legs"]).Count;
							double leg = a.TryGetValue("leg", out var lv) ? Tree.N(lv) : 0;
							if (!(0 <= leg && leg < legs)) rep.Error(w, $"brigade launch: {run} has {legs} legs (0-based)");
						}
						break;
					}
				case "stock":
					if (!world.Ledgers.Contains(Tree.S(a.GetValueOrDefault("post")))) rep.Error(w, $"stock: '{Tree.S(a.GetValueOrDefault("post"))}' is not a post with books");
					if (!world.Items.Contains(Tree.S(a.GetValueOrDefault("item")))) rep.Error(w, $"stock: '{Tree.S(a.GetValueOrDefault("item"))}' is not an item");
					break;
				case "news":
					if (a.TryGetValue("to", out var to) && Tree.S(to).Length > 0 && !Known.Addressees.Contains(Tree.S(to)))
						rep.Warn(w, $"news to '{Tree.S(to)}': the usual addressees are {string.Join(", ", Known.Addressees)}; a person's name works only if they are alive and known");
					if (a.TryGetValue("company", out var co) && Tree.S(co).Length > 0 && !Known.Companies.Contains(Tree.S(co))) rep.Error(w, $"news company must be HBC or NWC (got '{Tree.S(co)}')");
					break;
				case "say":
					if (a.TryGetValue("to", out var sto) && !(Tree.S(sto) is "all" or "HBC" or "NWC")) rep.Error(w, "say: to must be all, HBC or NWC");
					break;
				case "tension":
					if (a.GetValueOrDefault("add") is double t && Math.Abs(t) > 100) rep.Warn(w, $"tension {t:+0.##;-0.##} is more than the whole 0-100 scale");
					break;
				case "posting":
					if (!world.Ledgers.Contains(Tree.S(a.GetValueOrDefault("place")))) rep.Error(w, $"posting: '{Tree.S(a.GetValueOrDefault("place"))}' is not a post with books");
					break;
				case "regard":
					if (!world.Camps.Contains(Tree.S(a.GetValueOrDefault("place")))) rep.Error(w, $"regard: '{Tree.S(a.GetValueOrDefault("place"))}' is not a camp of the country");
					break;
				case "give": case "take":
					if (!world.Items.Contains(Tree.S(a.GetValueOrDefault("item")))) rep.Error(w, $"{verb}: '{Tree.S(a.GetValueOrDefault("item"))}' is not an item");
					break;
				case "rapport":
					CheckWho(Tree.S(a.GetValueOrDefault("who")), w, rep, world);
					break;
				case "start_quest": case "end_quest":
					Facts.Add(facts.QuestsRead, Tree.S(a.GetValueOrDefault("quest")), w);
					break;
			}
		}
	}

	private static void Cycles(List<Dictionary<string, object>> beats, Dictionary<string, Dictionary<string, object>> ids, Report rep)
	{
		var graph = new Dictionary<string, List<string>>();
		foreach (var b in beats) if (b.ContainsKey("id")) graph[Tree.S(b["id"])] = Tree.AsList(b.GetValueOrDefault("requires")).Select(Tree.S).Where(ids.ContainsKey).ToList();
		var state = new Dictionary<string, int>();
		void Visit(string n, List<string> path)
		{
			if (state.GetValueOrDefault(n) == 2) return;
			if (state.GetValueOrDefault(n) == 1)
			{
				int at = path.IndexOf(n);
				rep.Error($"beat {n}", "requires itself through a cycle: " + string.Join(" -> ", path.Skip(at).Append(n)));
				return;
			}
			state[n] = 1;
			foreach (var m in graph.GetValueOrDefault(n) ?? new()) Visit(m, path.Append(n).ToList());
			state[n] = 2;
		}
		foreach (var n in graph.Keys.ToList()) Visit(n, new List<string>());
	}

	private static void Reachability(List<Dictionary<string, object>> beats, Dictionary<string, Dictionary<string, object>> ids, Facts facts, Report rep)
	{
		var possible = new HashSet<string>(Known.EngineFlags);
		possible.UnionWith(facts.FlagsSet.Keys);
		var dead = new HashSet<string>();
		bool changed = true;
		while (changed)
		{
			changed = false;
			foreach (var b in beats)
			{
				string bid = Tree.S(b.GetValueOrDefault("id"));
				if (dead.Contains(bid)) continue;
				foreach (var ro in Tree.AsList(b.GetValueOrDefault("requires")))
				{
					string r = Tree.S(ro);
					if (dead.Contains(r) || (!ids.ContainsKey(r) && !possible.Contains(r) && !Known.EngineFlagPrefixes.Any(r.StartsWith)))
					{
						dead.Add(bid);
						changed = true;
						rep.Warn($"beat {bid}", $"can never happen: it requires '{r}', which can never happen");
						break;
					}
				}
			}
		}
	}

	private static void DateOrder(List<Dictionary<string, object>> beats, Dictionary<string, Dictionary<string, object>> ids, Report rep)
	{
		foreach (var b in beats)
		{
			if (!b.TryGetValue("date", out var dv) || Tree.S(dv).Length == 0) continue;
			foreach (var ro in Tree.AsList(b.GetValueOrDefault("requires")))
			{
				if (ids.TryGetValue(Tree.S(ro), out var o) && o.TryGetValue("date", out var od) && Tree.S(od).Length > 0 && string.CompareOrdinal(Tree.S(od), Tree.S(dv)) > 0)
					rep.Warn($"beat {Tree.S(b["id"])}", $"dated {Tree.S(dv)} but requires {Tree.S(ro)} ({Tree.S(od)}): it will wait for it");
			}
		}
	}
}
