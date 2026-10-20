using System;
using System.Collections.Generic;
using System.Linq;

namespace CampaignTool;

// Quest lines (Scripts/Core/QuestScript.cs has the shape the game reads):
//
//   questline Servant(title: "The Company's Servant", forCompany: Company.HBC) {
//       quest Landfall(title: "Report to the Factor", summary: "...") [Crossing] {
//           StartWhen() => world.month >= 8;
//           OnComplete() { player.addStanding(2); }
//           talk Report(who: Person.Factor, text: "Report to Mr. Auld") {
//               Says() => "Mr. Auld looks you over.";
//               choice Keen(label: "Put me to work, sir.", reply: "...", goto: Unload) { player.addStanding(1); }
//           }
//           task Unload(task: Task.CarryCargo, count: 6, text: "Carry six lots to the warehouse");
//           reach Inland(place: "Edmonton House");
//           when Boats(text: "Wait for the boats") => brigade("the York boats").isOut;
//       }
//   }
internal sealed partial class Compiler
{
	private readonly List<(QuestLineDecl Decl, Env Env)> _lines = new();
	private readonly Dictionary<string, string> _questIds = new();   // quest name -> id
	private Dictionary<string, string> _steps;                       // the quest being compiled: step name -> id

	private static readonly string[] LineArgs = { "id", "title", "summary", "forCompany" };
	private static readonly string[] QuestArgs = { "id", "title", "summary", "giver", "forCompany" };
	private static readonly string[] StepArgs = { "id", "text", "who", "place", "radius", "item", "count", "task", "convoy", "dest", "species", "days", "next" };
	private static readonly string[] ChoiceArgs = { "label", "reply", "goto", "when" };
	private static readonly (string hook, string key, bool cond)[] QuestHookMap =
	{
		("StartWhen", "start_when", true), ("FailWhen", "fail_when", true),
		("OnStart", "on_start", false), ("OnComplete", "on_complete", false), ("OnFail", "on_fail", false),
	};

	private void DeclareLine(QuestLineDecl l, Env env)
	{
		_lines.Add((l, env));
		foreach (var q in l.Quests)
		{
			if (_questIds.ContainsKey(q.Name)) throw new CampError($"there are two quests called {q.Name}", q.NameAt);
			string id = IdArg(q.Args, env, q.Name, "quest");
			if (_questIds.ContainsValue(id)) throw new CampError($"there are two quests with the id \"{id}\"", q.NameAt);
			_questIds[q.Name] = id;
		}
	}

	private string IdArg(List<Arg> args, Env env, string name, string what)
	{
		var a = args.FirstOrDefault(x => x.Name == "id");
		if (a == null) return name;
		var v = Eval(a.Value, env);
		return v is string s && s.Length > 0 ? s : throw new CampError($"a {what}'s id is a non-empty string", a.Value.At);
	}

	private string ForCompany(object v, Arg a) =>
		v is EnumV { Type: "Company" } e && e.Value != "All" ? Library.CompanyJson[e.Value] : throw new CampError($"forCompany is Company.HBC, Company.NWC or Company.American, but got {Describe(v)}", a.Value.At);

	private void Named(Arg a, string where)
	{
		if (a.Name == null) throw new CampError($"a {where}'s header values are named ({where} Name(title: \"...\"))", a.Value.At);
	}

	private List<object> CompileLines()
	{
		var list = new List<object>();
		var ids = new HashSet<string>();
		foreach (var (l, env) in _lines)
		{
			string id = IdArg(l.Args, env, l.Name, "questline");
			if (!ids.Add(id)) throw new CampError($"there are two quest lines with the id \"{id}\"", l.NameAt);
			var d = Tree.Dict(("id", id));
			string title = null;
			var seen = new HashSet<string>();
			foreach (var a in l.Args)
			{
				Named(a, "questline");
				if (!seen.Add(a.Name)) throw new CampError($"{a.Name} is given twice", a.NameAt);
				if (a.Name == "id") continue;
				var v = Eval(a.Value, env);
				switch (a.Name)
				{
					case "title": title = Text(v, a); break;
					case "summary": d["summary"] = Text(v, a); break;
					case "forCompany": d["for"] = ForCompany(v, a); break;
					default: throw new CampError($"a questline has no '{a.Name}'.{Suggest(a.Name, LineArgs)} Its header takes {string.Join(", ", LineArgs)}", a.NameAt);
				}
			}
			if (title == null) throw new CampError($"the questline needs a title: questline {l.Name}(title: \"...\")", l.NameAt);
			var ordered = Tree.Dict(("id", id), ("title", title));
			foreach (var k in new[] { "summary", "for" }) if (d.TryGetValue(k, out var x)) ordered[k] = x;
			ordered["quests"] = l.Quests.Select(q => (object)CompileQuest(q, env)).ToList();
			list.Add(ordered);
		}
		return list;
	}

	private Dictionary<string, object> CompileQuest(QuestDecl q, Env outer)
	{
		var env = new Env(outer);
		var saveSteps = _steps;
		_steps = new();
		try
		{
			var stepIds = new HashSet<string>();
			foreach (var st in q.Steps)
			{
				if (_steps.ContainsKey(st.Name)) throw new CampError($"quest {q.Name} has two steps called {st.Name}", st.NameAt);
				string sid = IdArg(st.Args, env, st.Name, "step");
				if (!stepIds.Add(sid)) throw new CampError($"quest {q.Name} has two steps with the id \"{sid}\"", st.NameAt);
				_steps[st.Name] = sid;
			}
			var f = new Dictionary<string, object>();
			string title = null;
			var seen = new HashSet<string>();
			foreach (var a in q.Args)
			{
				Named(a, "quest");
				if (!seen.Add(a.Name)) throw new CampError($"{a.Name} is given twice", a.NameAt);
				if (a.Name == "id") continue;
				var v = Eval(a.Value, env);
				switch (a.Name)
				{
					case "title": title = Text(v, a); break;
					case "summary": f["summary"] = Text(v, a); break;
					case "giver": f["giver"] = Library.Who(v); break;
					case "forCompany": f["for"] = ForCompany(v, a); break;
					default: throw new CampError($"a quest has no '{a.Name}'.{Suggest(a.Name, QuestArgs)} Its header takes {string.Join(", ", QuestArgs)}", a.NameAt);
				}
			}
			if (title == null) throw new CampError($"the quest needs a title: quest {q.Name}(title: \"...\")", q.NameAt);
			if (q.Requires.Count > 0)
				f["requires"] = q.Requires.Select(r =>
				{
					var v = Eval(r, env);
					return v is QuestV qv ? qv.Id : v is string s ? s : throw new CampError($"a quest requires other quests, but got {Describe(v)}", r.At);
				}).Cast<object>().ToList();
			var hooks = new HashSet<string>();
			foreach (var h in q.Hooks)
			{
				if (!hooks.Add(h.Name)) throw new CampError($"{h.Name} is written twice in quest {q.Name}", h.At);
				var (_, key, cond) = QuestHookMap.First(x => x.hook == h.Name);
				var compiled = HookBody(h, env, cond);
				if (compiled.Count > 0) f[key] = compiled;
			}
			if (q.Steps.Count == 0) throw new CampError($"quest {q.Name} has no steps: give it at least one (talk, reach, task ...)", q.NameAt);

			var d = Tree.Dict(("id", _questIds[q.Name]), ("title", title));
			foreach (var k in new[] { "summary", "giver", "for", "requires", "start_when", "fail_when", "on_start", "on_complete", "on_fail" })
				if (f.TryGetValue(k, out var x)) d[k] = x;
			d["objectives"] = q.Steps.Select(st => (object)CompileStep(q, st, env)).ToList();
			return d;
		}
		finally { _steps = saveSteps; }
	}

	private List<object> HookBody(HookDecl h, Env env, bool cond)
	{
		if (cond)
		{
			if (h.ExprBody == null) throw new CampError($"{h.Name} is a condition: write {h.Name}() => condition;", h.At);
			return CondList(h.ExprBody, env);
		}
		var outp = new List<object>();
		if (h.Block != null) ExecBlock(h.Block, new Env(env), outp);
		else Exec(new ExprStmt { At = h.ExprBody.At, Value = h.ExprBody }, env, outp);
		return outp;
	}

	private object StepRef(object v, Arg a) => v switch
	{
		StepV s => s.Id,
		string s when s is "complete" or "fail" => s,
		_ => throw new CampError($"{a.Name} names a step of this quest, or \"complete\" or \"fail\", but got {Describe(v)}", a.Value.At),
	};

	private double Whole(object v, Arg a, double min) =>
		v is double d && d == Math.Floor(d) && d >= min ? d : throw new CampError($"{a.Name} is a whole number of at least {CJson.Num(min)}, but got {Describe(v)}", a.Value.At);

	private Dictionary<string, object> CompileStep(QuestDecl q, StepDecl st, Env env)
	{
		var f = new Dictionary<string, object>();
		var seen = new HashSet<string>();
		foreach (var a in st.Args)
		{
			Named(a, "step");
			if (!seen.Add(a.Name)) throw new CampError($"{a.Name} is given twice", a.NameAt);
			if (a.Name == "id") continue;
			var v = Eval(a.Value, env);
			switch (a.Name)
			{
				case "text": f["text"] = Text(v, a); break;
				case "who": f["who"] = v is string or EnumV { Type: "Person" } ? Library.Who(v) : throw new CampError($"who is a person (a name, \"leader@Edmonton House\", or Person.Factor), but got {Describe(v)}", a.Value.At); break;
				case "place": f["place"] = Text(v, a); break;
				case "radius": f["radius"] = v is double rd && rd > 0 ? rd : throw new CampError($"radius is a distance in pixels, but got {Describe(v)}", a.Value.At); break;
				case "item": f["item"] = v is EnumV { Type: "Item" } ie ? ie.Value : throw new CampError($"item is an Item (Item.Tobacco ...), but got {Describe(v)}", a.Value.At); break;
				case "count": f["count"] = Whole(v, a, 1); break;
				case "task": f["task"] = v is EnumV { Type: "Task" } te ? te.Value : throw new CampError($"task is a Task (Task.CarryCargo ...), but got {Describe(v)}", a.Value.At); break;
				case "convoy": f["convoy"] = v is BrigadeV bv ? bv.Run : Text(v, a); break;
				case "dest": f["dest"] = Text(v, a); break;
				case "species": f["species"] = Text(v, a); break;
				case "days": f["days"] = Whole(v, a, 0); break;
				case "next": f["next"] = StepRef(v, a); break;
				default: throw new CampError($"a step has no '{a.Name}'.{Suggest(a.Name, StepArgs)} Its header takes {string.Join(", ", StepArgs)}", a.NameAt);
			}
		}
		string says = null;
		var hooks = new HashSet<string>();
		foreach (var h in st.Hooks)
		{
			if (!hooks.Add(h.Name)) throw new CampError($"{h.Name} is written twice in step {st.Name}", h.At);
			switch (h.Name)
			{
				case "Says":
					if (h.ExprBody == null) throw new CampError("Says is what they say: write Says() => \"...\";", h.At);
					says = Eval(h.ExprBody, env) as string ?? throw new CampError("Says() gives text", h.At);
					break;
				case "Done":
					if (st.Done != null) throw new CampError($"step {st.Name} has its condition twice (=> after the header, and Done)", h.At);
					var dc = HookBody(h, env, true); if (dc.Count > 0) f["cond"] = dc;
					break;
				case "OnDone":
					var od = HookBody(h, env, false); if (od.Count > 0) f["on_done"] = od;
					break;
			}
		}
		if (st.Done != null) { var dc = CondList(st.Done, env); if (dc.Count > 0) f["cond"] = dc; }

		var choices = new List<object>();
		foreach (var c in st.Choices)
		{
			if (st.Kind != "talk") throw new CampError($"only a talk step has choices (step {st.Name} is a {st.Kind})", c.At);
			choices.Add(CompileChoice(c, env));
		}

		// What each kind of step cannot do without.
		string Need(string key, string word) => f.ContainsKey(key) ? null : $"a {st.Kind} step needs {word}";
		string missing = st.Kind switch
		{
			"talk" => Need("who", "who: (the person to speak to)"),
			"bring" => Need("who", "who: (the person to give it to)") ?? Need("item", "item: (what to give)"),
			"reach" => Need("place", "place: (where to go)"),
			"have" => Need("item", "item: (what to have)"),
			"task" => Need("task", "task: (Task.CarryCargo ...)"),
			"when" => Need("cond", "a condition (when Name(...) => condition;)"),
			"wait" => Need("days", "days: (how long)"),
			_ => null,
		};
		if (missing != null) throw new CampError(missing, st.NameAt);
		if (f.ContainsKey("task") && f["task"] is "Brigade" && !f.ContainsKey("convoy")) throw new CampError("a Brigade task needs convoy: (the run to go with)", st.NameAt);

		var d = Tree.Dict(("id", _steps[st.Name]), ("kind", st.Kind));
		foreach (var k in new[] { "text", "who", "place", "radius", "item", "count", "task", "convoy", "dest", "species", "days", "cond" })
			if (f.TryGetValue(k, out var x)) d[k] = x;
		if (says != null || choices.Count > 0)
		{
			var dlg = Tree.Dict(("text", says ?? ""));
			if (choices.Count > 0) dlg["choices"] = choices;
			d["dialogue"] = dlg;
		}
		foreach (var k in new[] { "on_done", "next" }) if (f.TryGetValue(k, out var x)) d[k] = x;
		return d;
	}

	private Dictionary<string, object> CompileChoice(ChoiceDecl c, Env env)
	{
		string label = null;
		var f = new Dictionary<string, object>();
		var seen = new HashSet<string>();
		foreach (var a in c.Args)
		{
			Named(a, "choice");
			if (!seen.Add(a.Name)) throw new CampError($"{a.Name} is given twice", a.NameAt);
			switch (a.Name)
			{
				case "label": label = Text(Eval(a.Value, env), a); break;
				case "reply": f["reply"] = Text(Eval(a.Value, env), a); break;
				case "goto": f["goto"] = StepRef(Eval(a.Value, env), a); break;
				case "when": { var w = CondList(a.Value, env); if (w.Count > 0) f["when"] = w; break; }
				default: throw new CampError($"a choice has no '{a.Name}'.{Suggest(a.Name, ChoiceArgs)} It takes {string.Join(", ", ChoiceArgs)}", a.NameAt);
			}
		}
		if (label == null) throw new CampError($"choice {c.Name} needs a label: what the player says", c.NameAt);
		if (c.Block != null) { var outp = new List<object>(); ExecBlock(c.Block, new Env(env), outp); if (outp.Count > 0) f["do"] = outp; }
		var d = Tree.Dict(("label", label));
		foreach (var k in new[] { "reply", "when", "do", "goto" }) if (f.TryGetValue(k, out var x)) d[k] = x;
		return d;
	}
}
