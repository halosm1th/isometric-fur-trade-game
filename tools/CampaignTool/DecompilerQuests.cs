using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CampaignTool;

// Quest lines back into the language (CompilerQuests.cs is the other way).
public sealed partial class Decompiler
{
	private readonly Dictionary<string, string> _quests = new();                       // quest id -> name
	private readonly Dictionary<string, string> _lineNames = new();                    // line id -> name
	private readonly Dictionary<string, Dictionary<string, string>> _stepNames = new(); // quest id -> (step id -> name)

	// Names for the lines, quests and steps. Quests share one namespace with
	// the beats (both are named in conditions); a step's name must not hide a
	// quest or beat its own quest's conditions mention, so steps keep clear of
	// every global name too.
	private void NameQuests(List<Dictionary<string, object>> lines, HashSet<string> used)
	{
		for (int i = 0; i < lines.Count; i++)
		{
			string lid = Tree.S(lines[i].GetValueOrDefault("id"));
			if (lid.Length == 0) lid = $"line{i}";
			_lineNames[lid] = Unique(NameFor(lid), used);
			foreach (var q in Quests(lines[i]))
			{
				string qid = Tree.S(q["id"]);
				_quests[qid] = Unique(NameFor(qid), used);
			}
		}
		foreach (var l in lines)
			foreach (var q in Quests(l))
			{
				var stepUsed = new HashSet<string>(used);
				var names = new Dictionary<string, string>();
				foreach (var o in Steps(q)) names[Tree.S(o["id"])] = Unique(NameFor(Tree.S(o["id"])), stepUsed);
				_stepNames[Tree.S(q["id"])] = names;
			}
	}

	private static IEnumerable<Dictionary<string, object>> Quests(Dictionary<string, object> line) =>
		Tree.AsList(line.GetValueOrDefault("quests")).Cast<Dictionary<string, object>>();
	private static IEnumerable<Dictionary<string, object>> Steps(Dictionary<string, object> quest) =>
		Tree.AsList(quest.GetValueOrDefault("objectives")).Cast<Dictionary<string, object>>();

	// Every quest declared (with a placeholder step) so a probe can name them.
	private string QuestStubs()
	{
		if (_quests.Count == 0) return "";
		var sb = new StringBuilder("questline __Stubs(title: \"stubs\") {\n");
		foreach (var kv in _quests) sb.Append($"quest {kv.Value}(id: {Quote(kv.Key)}, title: \"stub\") {{ wait __Step(days: 0); }}\n");
		return sb.Append("}\n").ToString();
	}

	private static string WhoText(string who) => Library.Persons.ContainsKey(who) ? $"Person.{who}" : Quote(who);
	private static string CompanyText(string c) => c is "HBC" or "NWC" ? $"Company.{c}" : throw new CampError($"'{c}' is not a company a quest can be for");

	private static string Header(string word, string name, List<string> args)
	{
		string flat = string.Join(", ", args);
		return args.Count <= 2 && !flat.Contains('\n') && flat.Length < 70
			? $"{word} {name}({flat})"
			: $"{word} {name}(\n{Ind}{string.Join(",\n" + Ind, args)}\n)";
	}

	// Indent every line of a block of text by one level.
	private static string Indent(string text) => string.Join("\n", text.TrimEnd('\n').Split('\n').Select(l => l.Length == 0 ? l : Ind + l)) + "\n";

	private void QuestLine(StringBuilder sb, Dictionary<string, object> l)
	{
		string id = Tree.S(l.GetValueOrDefault("id"));
		string name = _lineNames[id];
		var args = new List<string>();
		if (name != id) args.Add($"id: {Quote(id)}");
		args.Add($"title: {Str(Tree.S(l.GetValueOrDefault("title")), Ind.Length + 7)}");
		if (Tree.S(l.GetValueOrDefault("summary")).Length > 0) args.Add($"summary: {Str(Tree.S(l["summary"]), Ind.Length + 9)}");
		if (l.TryGetValue("for", out var f)) args.Add($"forCompany: {CompanyText(Tree.S(f))}");
		sb.Append(Header("questline", name, args)).Append(" {\n");
		bool first = true;
		foreach (var q in Quests(l))
		{
			if (!first) sb.Append('\n');
			first = false;
			sb.Append(Indent(Quest(q)));
		}
		sb.Append("}\n");
	}

	private string Quest(Dictionary<string, object> q)
	{
		string id = Tree.S(q["id"]);
		string name = _quests[id];
		var args = new List<string>();
		if (name != id) args.Add($"id: {Quote(id)}");
		args.Add($"title: {Str(Tree.S(q.GetValueOrDefault("title")), Ind.Length + 7)}");
		if (Tree.S(q.GetValueOrDefault("summary")).Length > 0) args.Add($"summary: {Str(Tree.S(q["summary"]), Ind.Length + 9)}");
		if (Tree.S(q.GetValueOrDefault("giver")).Length > 0) args.Add($"giver: {WhoText(Tree.S(q["giver"]))}");
		if (q.TryGetValue("for", out var f)) args.Add($"forCompany: {CompanyText(Tree.S(f))}");
		var sb = new StringBuilder(Header("quest", name, args));
		var reqs = Tree.AsList(q.GetValueOrDefault("requires"));
		if (reqs.Count > 0) sb.Append(" [").Append(string.Join(", ", reqs.Select(r => _quests.TryGetValue(Tree.S(r), out var rn) ? rn : Quote(Tree.S(r))))).Append(']');
		sb.Append(" {\n");
		foreach (var (hook, key, cond) in new[] { ("StartWhen", "start_when", true), ("FailWhen", "fail_when", true), ("OnStart", "on_start", false), ("OnComplete", "on_complete", false), ("OnFail", "on_fail", false) })
			HookText(sb, q, hook, key, cond, 1);
		var steps = _stepNames[id];
		foreach (var o in Steps(q)) sb.Append(Step(o, steps));
		sb.Append("}\n");
		return sb.ToString();
	}

	private void HookText(StringBuilder sb, Dictionary<string, object> d, string hook, string key, bool cond, int depth)
	{
		var list = Tree.AsList(d.GetValueOrDefault(key));
		if (list.Count == 0) return;
		string pad = string.Concat(Enumerable.Repeat(Ind, depth));
		if (cond) { sb.Append($"{pad}{hook}() => {CondTop(list)};\n"); return; }
		sb.Append($"{pad}{hook}() {{\n");
		foreach (var line in Stmts(list, depth + 1)) sb.Append(line).Append('\n');
		sb.Append($"{pad}}}\n");
	}

	private static string StepRef(string target, Dictionary<string, string> steps) =>
		target is "complete" or "fail" ? Quote(target) : steps.TryGetValue(target, out var n) ? n : throw new CampError($"a quest goes to a step it does not have: '{target}'");

	private string Step(Dictionary<string, object> o, Dictionary<string, string> steps)
	{
		string id = Tree.S(o["id"]);
		string name = steps[id];
		string kind = Tree.S(o.GetValueOrDefault("kind"));
		if (kind.Length == 0) kind = "talk";
		if (!CampParser.StepKinds.Contains(kind)) throw new CampError($"step {id}: '{kind}' is not a kind of step");
		var args = new List<string>();
		if (name != id) args.Add($"id: {Quote(id)}");
		int inner = Ind.Length * 2;
		foreach (var key in new[] { "text", "who", "place", "radius", "item", "count", "task", "convoy", "dest", "species", "days", "next" })
		{
			if (!o.TryGetValue(key, out var v)) continue;
			string s = Tree.S(v);
			args.Add(key + ": " + key switch
			{
				"text" => Str(s, inner + 6),
				"who" => WhoText(s),
				"item" => ItemText(s),
				"task" => Known.World.Tasks.Contains(s) ? $"Task.{s}" : throw new CampError($"step {id}: '{s}' is not a task"),
				"radius" or "count" or "days" => CJson.Num(Tree.N(v)),
				"next" => StepRef(s, steps),
				_ => Quote(s),
			});
		}
		var known = new HashSet<string> { "id", "kind", "text", "who", "place", "radius", "item", "count", "task", "convoy", "dest", "species", "days", "next", "cond", "dialogue", "on_done" };
		foreach (var k in o.Keys) if (!known.Contains(k)) throw new CampError($"step {id}: the language has no word for '{k}'");

		var body = new StringBuilder();
		var cond = Tree.AsList(o.GetValueOrDefault("cond"));
		var dlg = o.GetValueOrDefault("dialogue") as Dictionary<string, object>;
		var choices = dlg != null ? Tree.AsList(dlg.GetValueOrDefault("choices")).Cast<Dictionary<string, object>>().ToList() : new();
		bool hasDone = Tree.AsList(o.GetValueOrDefault("on_done")).Count > 0;
		string head = Header(kind, name, args);
		string pad = Ind;
		// A step with only a condition is written step Name(...) => condition;
		if (cond.Count > 0 && dlg == null && !hasDone) return $"{pad}{IndentTail(head)} => {CondTop(cond)};\n";
		if (dlg != null) body.Append($"{pad}{Ind}Says() => {Str(Tree.S(dlg.GetValueOrDefault("text")), pad.Length + Ind.Length + 11)};\n");
		if (cond.Count > 0) body.Append($"{pad}{Ind}Done() => {CondTop(cond)};\n");
		HookText(body, o, "OnDone", "on_done", false, 2);
		for (int i = 0; i < choices.Count; i++) body.Append(Choice(choices[i], i + 1, steps));
		if (body.Length == 0) return $"{pad}{IndentTail(head)};\n";
		return $"{pad}{IndentTail(head)} {{\n{body}{pad}}}\n";
	}

	// A multi-line header gets the step's own indent on its later lines.
	private static string IndentTail(string head) => head.Replace("\n", "\n" + Ind);

	private string Choice(Dictionary<string, object> c, int n, Dictionary<string, string> steps)
	{
		foreach (var k in c.Keys) if (k is not ("label" or "reply" or "when" or "do" or "goto")) throw new CampError($"a choice has no '{k}'");
		string pad = Ind + Ind;
		var args = new List<string> { $"label: {Str(Tree.S(c.GetValueOrDefault("label")), pad.Length + Ind.Length + 7)}" };
		if (c.ContainsKey("reply")) args.Add($"reply: {Str(Tree.S(c["reply"]), pad.Length + Ind.Length + 7)}");
		var when = Tree.AsList(c.GetValueOrDefault("when"));
		if (when.Count > 0) args.Add($"when: {CondTop(when)}");
		if (c.ContainsKey("goto")) args.Add($"goto: {StepRef(Tree.S(c["goto"]), steps)}");
		string head = $"choice Choice{n}(\n{pad}{Ind}{string.Join(",\n" + pad + Ind, args)}\n{pad})";
		var acts = Tree.AsList(c.GetValueOrDefault("do"));
		if (acts.Count == 0) return $"{pad}{head};\n";
		var sb = new StringBuilder($"{pad}{head} {{\n");
		foreach (var line in Stmts(acts, 3)) sb.Append(line).Append('\n');
		return sb.Append($"{pad}}}\n").ToString();
	}
}
