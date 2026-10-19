using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CampaignTool;

// The game's JSON -> the language, so an old script can be moved across and
// read back. compile(decompile(x)) is x: every condition and action is written
// in the nicest form that compiles back to exactly the same data, and in
// raw(...) / action(...) when no nicer form does.
public sealed partial class Decompiler
{
	private const int Wrap = 64;
	private const string Ind = "    ";

	public static string Decompile(Dictionary<string, object> doc, string name = "Campaign") => new Decompiler(doc, name).Run();

	private readonly Dictionary<string, object> _doc;
	private readonly string _name;
	private readonly Dictionary<string, string> _names = new();       // beat id -> name
	private readonly List<string> _vars = new();
	private string _prefix = "";
	private readonly Dictionary<string, bool> _probeCache = new();

	private Decompiler(Dictionary<string, object> doc, string name) { _doc = doc; _name = name; }

	private static readonly HashSet<string> Taken = new()
	{
		"campaign", "beat", "const", "void", "if", "else", "for", "foreach", "in", "true", "false", "return", "while", "var", "int", "float", "string", "bool",
		"world", "flags", "days", "date", "chance", "raw", "brigade", "news", "say", "shout", "trigger", "after", "random", "option", "action",
		"Company", "Person", "Season", "Kind", "Item", "Date", "Days", "Beat", "Brigade",
		"questline", "quest", "choice", "player", "Quest", "Task", "talk", "reach", "task", "bring", "have", "hunt", "when", "wait",
		"StartWhen", "FailWhen", "OnStart", "OnComplete", "OnFail", "Says", "Done", "OnDone",
	};

	private string Run()
	{
		var beats = Tree.AsList(_doc.GetValueOrDefault("beats")).Cast<Dictionary<string, object>>().ToList();
		var used = new HashSet<string>();
		for (int i = 0; i < beats.Count; i++)
		{
			string id = beats[i].TryGetValue("id", out var idv) ? Tree.S(idv) : $"beat{i}";
			_names[id] = Unique(NameFor(id), used);
		}
		var lines = Tree.AsList(_doc.GetValueOrDefault("questlines")).Cast<Dictionary<string, object>>().ToList();
		NameQuests(lines, used);
		CollectVars(beats);
		var header = Header();
		var stubs = new StringBuilder();
		foreach (var kv in _names) stubs.Append($"beat {kv.Value}(id: {Quote(kv.Key)});\n");
		stubs.Append(QuestStubs());
		var varDecls = VarDecls();
		_prefix = header + "\n" + varDecls + stubs;

		var sb = new StringBuilder(header);
		if (varDecls.Length > 0) sb.Append('\n').Append(varDecls);
		foreach (var b in beats) { sb.Append('\n'); Beat(sb, b); }
		foreach (var l in lines) { sb.Append('\n'); QuestLine(sb, l); }
		return sb.ToString();
	}

	private static string Unique(string name, HashSet<string> used)
	{
		string n = name;
		for (int k = 2; !used.Add(n); k++) n = name + "_" + k;
		return n;
	}

	private static string NameFor(string id)
	{
		if (Regex.IsMatch(id, @"^[A-Za-z_]\w*$") && !Taken.Contains(id)) return id;
		var words = Regex.Split(id, @"[^A-Za-z0-9]+").Where(w => w.Length > 0).ToList();
		string n = string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
		if (n.Length == 0) n = "Beat";
		if (char.IsDigit(n[0])) n = "B" + n;
		if (Taken.Contains(n)) n += "_";
		return n;
	}

	private void CollectVars(List<Dictionary<string, object>> beats)
	{
		var names = new SortedSet<string>(StringComparer.Ordinal);
		void Walk(object o)
		{
			switch (o)
			{
				case Dictionary<string, object> m:
					if (m.GetValueOrDefault("do") is "set" or "add" && m.TryGetValue("var", out var v)) names.Add(Tree.S(v));
					if (m.GetValueOrDefault("m") is "var" && m.GetValueOrDefault("a") is List<object> a && a.Count == 1) names.Add(Tree.S(a[0]));
					foreach (var x in m.Values) Walk(x);
					break;
				case List<object> l: foreach (var x in l) Walk(x); break;
				case string s:
					foreach (Match mm in Regex.Matches(s, @"\bvar\(([^)]+)\)")) names.Add(mm.Groups[1].Value.Trim());
					break;
			}
		}
		foreach (var b in beats) Walk(b);
		if (_doc.GetValueOrDefault("vars") is Dictionary<string, object> vars) foreach (var k in vars.Keys) names.Add(k);
		_vars.AddRange(names.Where(n => Regex.IsMatch(n, @"^[A-Za-z_]\w*$") && !Taken.Contains(n) && !_names.Values.Contains(n)));
	}

	private string VarDecls()
	{
		var sb = new StringBuilder();
		var init = _doc.GetValueOrDefault("vars") as Dictionary<string, object>;
		foreach (var v in _vars)
		{
			double d = init != null && init.TryGetValue(v, out var iv) ? Tree.N(iv) : 0;
			string type = d == Math.Floor(d) ? "int" : "float";
			sb.Append(d == 0 ? $"{type} {v};\n" : $"{type} {v} = {CJson.Num(d)};\n");
		}
		return sb.ToString();
	}

	private string Header()
	{
		var args = new List<string> { $"title: {Str(Tree.S(_doc.GetValueOrDefault("title")), Ind.Length + 7)}" };
		if (Tree.S(_doc.GetValueOrDefault("description")).Length > 0) args.Add($"description: {Str(Tree.S(_doc["description"]), Ind.Length + 13)}");
		if (Tree.S(_doc.GetValueOrDefault("note")).Length > 0) args.Add($"note: {Str(Tree.S(_doc["note"]), Ind.Length + 6)}");
		if (_doc.TryGetValue("seed", out var seed)) args.Add($"seed: {Tree.S(seed)}");
		if (_doc.GetValueOrDefault("posts_by_founding_year") is true) args.Add("postsByFoundingYear: true");
		if (Tree.S(_doc.GetValueOrDefault("extends")).Length > 0) args.Add($"extends: {Quote(Tree.S(_doc["extends"]))}");
		return $"campaign {NameFor(_name)}(\n{Ind}{string.Join(",\n" + Ind, args)}\n);\n";
	}

	// ---------------------------------------------------------------- strings

	public static string Quote(string s)
	{
		var sb = new StringBuilder("\"");
		foreach (char c in s)
			sb.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\t' => "\\t", _ => c.ToString() });
		return sb.Append('"').ToString();
	}

	// "text", broken into pieces joined by + when it is long (continuation lines line up at `col`).
	public static string Str(string s, int col)
	{
		if (s.Length <= Wrap && !s.Contains('\n')) return Quote(s);
		var pieces = new List<string>();
		var cur = new StringBuilder();
		foreach (var (line, i) in s.Split('\n').Select((t, i) => (t, i)))
		{
			string rest = line;
			if (i > 0) cur.Append('\n');
			while (rest.Length > Wrap)
			{
				int cut = rest.LastIndexOf(' ', Wrap);
				cut = cut <= 0 ? Wrap : cut + 1;
				cur.Append(rest[..cut]);
				pieces.Add(cur.ToString()); cur.Clear();
				rest = rest[cut..];
			}
			cur.Append(rest);
			if (cur.Length > Wrap) { pieces.Add(cur.ToString()); cur.Clear(); }
		}
		if (cur.Length > 0 || pieces.Count == 0) pieces.Add(cur.ToString());
		return string.Join(" +\n" + new string(' ', col), pieces.Select(Quote));
	}

	// ---------------------------------------------------------------- verification

	private bool Probe(string hookSource, string key, object expected)
	{
		string src = _prefix + $"beat __probe(id: \"__probe\") {{\n{hookSource}\n}}\n";
		if (_probeCache.TryGetValue(src, out var hit)) return hit;
		bool ok;
		try
		{
			var res = new Compiler(CampParser.Parse(src)).Run();
			var beats = (List<object>)res.Json["beats"];
			var probe = (Dictionary<string, object>)beats[^1];
			ok = probe.TryGetValue(key, out var got) && Tree.Same(got, expected);
		}
		catch (CampError) { ok = false; }
		_probeCache[src] = ok;
		return ok;
	}

	private bool CondOk(string text, object expectedLeaf) => Probe($"AwakenWhen() => {text};", "when", new List<object> { expectedLeaf });
	private bool StmtOk(string text, object expected) => Probe($"OnActivate() {{ {text} }}", "happens", new List<object> { expected });

	// ---------------------------------------------------------------- beats

	private void Beat(StringBuilder sb, Dictionary<string, object> b)
	{
		string id = Tree.S(b["id"]);
		string name = _names[id];
		var args = new List<string>();
		if (name != id) args.Add($"id: {Quote(id)}");
		string title = Tree.S(b.GetValueOrDefault("title"));
		if (title.Length > 0 && title != id) args.Add($"title: {Str(title, Ind.Length + 7)}");
		string kind = b.TryGetValue("kind", out var kv) ? Tree.S(kv) : "event";
		var without = b.Where(x => x.Key != "kind").ToDictionary(x => x.Key, x => x.Value);
		without = Normalize(without);
		if (kind != BeatKinds.Derive(without)) args.Add($"kind: Kind.{char.ToUpperInvariant(kind[0])}{kind[1..]}");
		if (b.TryGetValue("date", out var date) && Tree.S(date).Length > 0)
		{
			if (!DateV.TryParse(Tree.S(date), out var dv)) throw new CampError($"beat {id}: '{Tree.S(date)}' is not a date");
			args.Add($"date: date({dv.Y}, {dv.M}, {dv.D})");
		}
		foreach (var (key, word) in new[] { ("window_days", "window"), ("lead_days", "lead"), ("delay_days", "delay"), ("repeat_days", "repeat") })
			if (b.TryGetValue(key, out var n)) args.Add($"{word}: days({CJson.Num(Tree.N(n))})");
		var kws = Tree.AsList(b.GetValueOrDefault("keywords"));
		if (kws.Count > 0) args.Add($"keywords: [{string.Join(", ", kws.Select(k => Quote(Tree.S(k))))}]");
		if (Tree.S(b.GetValueOrDefault("rumour")).Length > 0) args.Add($"rumour: {Str(Tree.S(b["rumour"]), Ind.Length + 8)}");

		sb.Append($"beat {name}(");
		string flat = string.Join(", ", args);
		if (args.Count <= 1 && !flat.Contains('\n') && flat.Length < 80) sb.Append(flat);
		else sb.Append('\n').Append(Ind).Append(string.Join(",\n" + Ind, args)).Append('\n');
		sb.Append(')');
		var reqs = Tree.AsList(b.GetValueOrDefault("requires"));
		if (reqs.Count > 0) sb.Append(" [").Append(string.Join(", ", reqs.Select(r => _names.TryGetValue(Tree.S(r), out var rn) ? rn : Quote(Tree.S(r))))).Append(']');

		var hooks = new StringBuilder();
		foreach (var (hook, key, cond) in new[]
		{
			("AwakenWhen", "when", true), ("OnAwaken", "on_awaken", false), ("OnApproach", "pressure", false), ("ActivateWhen", "activate_when", true),
			("OnActivate", "happens", false), ("ResolveWhen", "done_when", true), ("OnResolve", "on_resolve", false), ("CancelWhen", "cancel_when", true), ("OnCancel", "on_cancel", false),
		})
		{
			var list = Tree.AsList(b.GetValueOrDefault(key));
			if (list.Count == 0) continue;
			if (cond) hooks.Append($"{Ind}{hook}() => {CondTop(list)};\n");
			else
			{
				hooks.Append($"{Ind}{hook}() {{\n");
				foreach (var line in Stmts(list, 2)) hooks.Append(line).Append('\n');
				hooks.Append($"{Ind}}}\n");
			}
		}
		if (hooks.Length == 0) sb.Append(";\n");
		else sb.Append(" {\n").Append(hooks).Append("}\n");
	}

	// The beat as the compiler will see it: empty lists are not there.
	private static Dictionary<string, object> Normalize(Dictionary<string, object> b)
	{
		var d = new Dictionary<string, object>();
		foreach (var kv in b) if (!(kv.Value is List<object> l && l.Count == 0)) d[kv.Key] = kv.Value;
		return d;
	}

	// ---------------------------------------------------------------- conditions

	private string CondTop(List<object> list)
	{
		if (list.Count == 1)
		{
			var only = list[0];
			string t = Cond(only, 0);
			bool compoundAll = only is List<object> || only is Dictionary<string, object> d && d.ContainsKey("all");
			return compoundAll && !t.StartsWith("(") && t.Contains(" && ") ? $"({t})" : t;
		}
		return string.Join(" && ", list.Select(x => Cond(x, 2)));
	}

	// level: 0 anything, 1 inside ||, 2 inside &&, 3 inside !
	private string Cond(object c, int level)
	{
		switch (c)
		{
			case bool b: return b ? "true" : "false";
			case string s: return Bracket(Leaf(s.Trim()), level);
			case List<object> l: return l.Count < 2 ? Raw(c) : Join(l, "&&", level);
			case Dictionary<string, object> d:
				if (d.Count == 1 && d.TryGetValue("all", out var all) && all is List<object> la && la.Count >= 2) return Join(la, "&&", level);
				if (d.Count == 1 && d.TryGetValue("any", out var any) && any is List<object> lo && lo.Count >= 2) return Join(lo, "||", level);
				if (d.Count == 1 && d.TryGetValue("not", out var not)) return "!" + Cond(not, 3);
				return Bracket(Leaf(d), level);
		}
		return Raw(c);
	}

	private string Join(List<object> parts, string op, int level)
	{
		int mine = op == "||" ? 1 : 2;
		var kids = parts.Select(x => Child(x, op)).ToList();
		string text = string.Join($" {op} ", kids);
		if (text.Length > 80) text = string.Join($"\n{Ind}{Ind}{op} ", kids);
		return level >= mine ? $"({text})" : text;
	}

	private string Child(object x, string parentOp)
	{
		bool isAll = x is List<object> || x is Dictionary<string, object> d && d.Count == 1 && d.ContainsKey("all");
		bool isAny = x is Dictionary<string, object> d2 && d2.Count == 1 && d2.ContainsKey("any");
		if (parentOp == "&&" && (isAll || isAny)) return Cond(x, 2);
		if (parentOp == "||" && isAny) return Cond(x, 1);
		return Cond(x, parentOp == "||" ? 1 : 2);
	}

	// a comparison needs brackets under ! ; calls and members never do
	private static string Bracket(string text, int level) => level == 3 && Regex.IsMatch(text, @"\s(>=|<=|==|!=|>|<)\s") && !text.StartsWith("raw(") ? $"({text})" : text;

	private string Raw(object c) => $"raw({Quote(c is string s ? s : Tree.Compact(c))})";

	private static readonly Regex CmpRe = new(@"^([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)$");

	private string Leaf(object leaf)
	{
		var candidates = new List<string>();
		if (leaf is string s)
		{
			var m = CmpRe.Match(s);
			if (m.Success)
			{
				var args = Commas(m.Groups[2].Value).Select(a => a.Trim().Trim('"')).ToList();
				candidates.Add(MetricText(m.Groups[1].Value, args, m.Groups[3].Value, double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture)));
			}
			else
			{
				int colon = s.IndexOf(':');
				if (colon > 0) candidates.Add(PredText(s[..colon].Trim(), s[(colon + 1)..].Trim()));
			}
		}
		else if (leaf is Dictionary<string, object> d)
		{
			if (d.ContainsKey("m") && d.Keys.All(k => k is "m" or "a" or "op" or "v"))
				candidates.Add(MetricText(Tree.S(d["m"]), Tree.AsList(d.GetValueOrDefault("a")).Select(Tree.S).ToList(), d.TryGetValue("op", out var op) ? Tree.S(op) : ">=", Tree.N(d.GetValueOrDefault("v"))));
		}
		foreach (var cand in candidates)
			if (cand != null && CondOk(cand, leaf)) return cand;
		return Raw(leaf);
	}

	private static List<string> Commas(string s)
	{
		var outp = new List<string>();
		if (s.Trim().Length == 0) return outp;
		var cur = new StringBuilder();
		bool quote = false;
		foreach (char ch in s)
		{
			if (ch == '"') quote = !quote;
			if (ch == ',' && !quote) { outp.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch);
		}
		outp.Add(cur.ToString());
		return outp;
	}

	private string ItemText(string item) => Known.World.Items.Contains(item) ? $"Item.{item}" : Quote(item);

	private string MetricText(string name, List<string> args, string op, double v)
	{
		string rhs = $" {op} {CJson.Num(v)}";
		string lhs = name switch
		{
			"tension" or "standing" or "month" or "year" or "day" or "convoys_active" when args.Count == 0 => "world." + name switch { "convoys_active" => "convoysActive", _ => name },
			"day_of_year" when args.Count == 0 => "world.dayOfYear",
			"var" when args.Count == 1 && Regex.IsMatch(args[0], @"^[A-Za-z_]\w*$") => args[0],
			"stock" when args.Count == 2 => $"world.stock({Quote(args[0])}, {ItemText(args[1])})",
			"furs" when args.Count == 1 => $"world.furs({Quote(args[0])})",
			"count" when args.Count == 1 && _names.TryGetValue(args[0], out var cn) => $"{cn}.count",
			"beat_age" when args.Count == 1 && _names.TryGetValue(args[0], out var an) => $"{an}.age",
			"convoy_days_late" when args.Count == 1 => $"brigade({Quote(args[0])}).daysLate",
			"convoy_days_out" when args.Count == 1 => $"brigade({Quote(args[0])}).daysOut",
			"rapport" when args.Count == 1 => $"player.rapport({Quote(args[0])})",
			"regard" when args.Count == 1 => $"world.regard({Quote(args[0])})",
			_ => null,
		};
		return lhs == null ? null : lhs + rhs;
	}

	private string PredText(string name, string arg)
	{
		string BrigadeP(string prop) => $"brigade({Quote(arg)}).{prop}";
		switch (name)
		{
			case "flag": return $"flags.has({Quote(arg)})";
			case "beat": return _names.TryGetValue(arg, out var bn) ? $"{bn}.done" : null;
			case "post": return $"world.hasPost({Quote(arg)})";
			case "dead": return $"world.isDead({Quote(arg)})";
			case "season": return $"world.season == Season.{arg}";
			case "player_faction": return $"world.playerFaction == Company.{arg}";
			case "chance": return $"chance({arg})";
			case "month": return Regex.IsMatch(arg, @"^\d{1,2}(-\d{1,2})?$") ? $"world.inMonths({arg.Replace("-", ", ")})" : null;
			case "after" when DateV.TryParse(arg, out var ad): return $"world.date >= date({ad.Y}, {ad.M}, {ad.D})";
			case "before" when DateV.TryParse(arg, out var bd): return $"world.date < date({bd.Y}, {bd.M}, {bd.D})";
			case "convoy_late": return BrigadeP("isLate");
			case "convoy_lost": return BrigadeP("isLost");
			case "convoy_robbed": return BrigadeP("isRobbed");
			case "convoy_turned_back": return BrigadeP("wasTurnedBack");
			case "convoy_out": return BrigadeP("isOut");
			case "convoy_in": return BrigadeP("isIn");
			case "convoy_wintering": return BrigadeP("isWintering");
			case "quest": return _quests.TryGetValue(arg, out var qd) ? $"{qd}.done" : null;
			case "quest_active": return _quests.TryGetValue(arg, out var qa) ? $"{qa}.active" : null;
			case "quest_failed": return _quests.TryGetValue(arg, out var qf) ? $"{qf}.failed" : null;
			case "posted": return $"player.posted({Quote(arg)})";
		}
		return null;
	}

	// ---------------------------------------------------------------- actions

	private List<string> Stmts(List<object> acts, int depth)
	{
		var lines = new List<string>();
		foreach (var a in acts) lines.AddRange(Stmt((Dictionary<string, object>)a, depth));
		return lines;
	}

	private static bool Only(Dictionary<string, object> a, params string[] allowed) => a.Keys.Where(k => k != "do").All(allowed.Contains);
	private string Pad(int depth) => string.Concat(Enumerable.Repeat(Ind, depth));
	private List<string> One(int depth, string text) => new() { Pad(depth) + text };

	private List<string> Stmt(Dictionary<string, object> a, int depth)
	{
		string verb = a.TryGetValue("do", out var vo) ? Tree.S(vo) : "";
		string pad = Pad(depth);
		string Q(string key) => Quote(Tree.S(a[key]));
		string N(string key) => CJson.Num(Tree.N(a[key]));
		bool Has(params string[] keys) => keys.All(a.ContainsKey);
		int col = pad.Length;

		switch (verb)
		{
			case "if" when Only(a, "cond", "then", "else") && a.ContainsKey("cond"):
				{
					var lines = new List<string>();
					var cur = a;
					bool first = true;
					while (true)
					{
						string head = first ? $"{pad}if ({Cond(cur["cond"], 0)}) {{" : $"{pad}}} else if ({Cond(cur["cond"], 0)}) {{";
						lines.Add(head);
						lines.AddRange(Stmts(Tree.AsList(cur.GetValueOrDefault("then")), depth + 1));
						first = false;
						var el = Tree.AsList(cur.GetValueOrDefault("else"));
						if (el.Count == 0) break;
						if (el.Count == 1 && el[0] is Dictionary<string, object> n && Tree.S(n.GetValueOrDefault("do")) == "if" && Only(n, "cond", "then", "else") && n.ContainsKey("cond")) { cur = n; continue; }
						lines.Add($"{pad}}} else {{");
						lines.AddRange(Stmts(el, depth + 1));
						break;
					}
					lines.Add($"{pad}}}");
					return lines;
				}
			case "after" when Only(a, "days", "then") && a.ContainsKey("days"):
				{
					var body = Tree.AsList(a.GetValueOrDefault("then"));
					string head = $"{pad}after(days({N("days")}), () => ";
					if (body.Count == 1 && body[0] is Dictionary<string, object> one)
					{
						var inner = Stmt(one, depth);
						if (inner.Count == 1 && inner[0].EndsWith(";") && !Regex.IsMatch(inner[0].TrimStart(), @"^\w+\s*(\+\+|--|[+\-]?=)") && !inner[0].TrimStart().StartsWith("if "))
						{
							string cand = $"{head}{inner[0].TrimStart()[..^1]});";
							if (cand.Length <= 100 && StmtOk(cand.TrimStart(), a)) return new() { cand };
						}
					}
					var lines = new List<string> { head + "{" };
					lines.AddRange(Stmts(body, depth + 1));
					lines.Add($"{pad}}});");
					return lines;
				}
			case "random" when Only(a, "options") && a.GetValueOrDefault("options") is List<object> opts && opts.Count > 0 && opts.All(o => o is Dictionary<string, object> od && od.Keys.All(k => k is "weight" or "do")):
				{
					var lines = new List<string> { $"{pad}random(" };
					for (int i = 0; i < opts.Count; i++)
					{
						var od = (Dictionary<string, object>)opts[i];
						string w = CJson.Num(od.TryGetValue("weight", out var wv) ? Tree.N(wv) : 1.0);
						lines.Add($"{pad}{Ind}option({w}, () => {{");
						lines.AddRange(Stmts(Tree.AsList(od.GetValueOrDefault("do")), depth + 2));
						lines.Add($"{pad}{Ind}}}){(i + 1 < opts.Count ? "," : "")}");
					}
					lines.Add($"{pad});");
					return lines;
				}
			case "news" when Only(a, "from", "to", "subject", "text", "days", "company") && Has("from", "to", "subject", "text", "days"):
				{
					string to = Library.Persons.FirstOrDefault(p => p.Value == Tree.S(a["to"])).Key is { } pk ? $"Person.{pk}" : Q("to");
					string inner = pad + Ind;
					var args = new List<string>
					{
						$"from: {Quote(Tree.S(a["from"]))}", $"to: {to}", $"subject: {Str(Tree.S(a["subject"]), inner.Length + 9)}",
						$"text: {Str(Tree.S(a["text"]), inner.Length + 6)}", $"days: {N("days")}",
					};
					if (a.ContainsKey("company")) args.Add($"company: {(Tree.S(a["company"]) is "HBC" or "NWC" ? "Company." + Tree.S(a["company"]) : Q("company"))}");
					string text = $"{pad}news(\n{inner}{string.Join(",\n" + inner, args)}\n{pad});";
					var cand = text.Split('\n').ToList();
					if (StmtOk(text.Trim(), a)) return cand;
					break;
				}
		}

		var simple = new List<string>();
		switch (verb)
		{
			case "flag" when Only(a, "name") && Has("name"): simple.Add($"flags.set({Q("name")});"); break;
			case "unflag" when Only(a, "name") && Has("name"): simple.Add($"flags.clear({Q("name")});"); break;
			case "set" when Only(a, "var", "to") && Has("var", "to"): simple.Add($"{Tree.S(a["var"])} = {N("to")};"); break;
			case "add" when Only(a, "var", "by") && Has("var"):
				{
					string v = Tree.S(a["var"]);
					if (!a.ContainsKey("by")) simple.Add($"{v}++;");
					else
					{
						double by = Tree.N(a["by"]);
						simple.Add(by == -1 ? $"{v}--;" : by >= 0 ? $"{v} += {CJson.Num(by)};" : $"{v} -= {CJson.Num(-by)};");
					}
					break;
				}
			case "tension" when Only(a, "add", "why") && Has("add"): simple.Add($"world.raiseTension({N("add")}{(a.ContainsKey("why") ? ", " + Str(Tree.S(a["why"]), col + 21 + N("add").Length) : "")});"); break;
			case "london" when Only(a, "price", "quota") && Has("price", "quota"): simple.Add($"world.setLondon({N("price")}, {N("quota")});"); break;
			case "found_post" when Only(a, "place") && Has("place"): simple.Add($"world.foundPost({Q("place")});"); break;
			case "trigger" when Only(a, "beat") && Has("beat"): simple.Add(_names.TryGetValue(Tree.S(a["beat"]), out var tn) ? $"trigger({tn});" : $"trigger({Q("beat")});"); break;
			case "stock" when Only(a, "post", "item", "add") && Has("post", "item", "add"): simple.Add($"world.addStock({Q("post")}, {ItemText(Tree.S(a["item"]))}, {N("add")});"); break;
			case "say" when Only(a, "text", "to", "headline") && Has("text"):
				{
					string who = a.TryGetValue("to", out var t) ? ", " + (Tree.S(t) is "HBC" or "NWC" ? "Company." + Tree.S(t) : Tree.S(t) == "all" ? "Company.All" : Q("to")) : "";
					string call = a.GetValueOrDefault("headline") is true ? "shout" : "say";
					simple.Add($"{call}({Str(Tree.S(a["text"]), col + call.Length + 1)}{who});");
					break;
				}
			case "posting" when Only(a, "place") && Has("place"): simple.Add($"player.postTo({Q("place")});"); break;
			case "standing" when Only(a, "add") && Has("add"): simple.Add($"player.addStanding({N("add")});"); break;
			case "rapport" when Only(a, "who", "add") && Has("who", "add"): simple.Add($"player.addRapport({Q("who")}, {N("add")});"); break;
			case "regard" when Only(a, "place", "add") && Has("place", "add"): simple.Add($"world.addRegard({Q("place")}, {N("add")});"); break;
			case "give" or "take" when Only(a, "item", "count") && Has("item"): simple.Add($"player.{verb}({ItemText(Tree.S(a["item"]))}{(a.ContainsKey("count") ? ", " + N("count") : "")});"); break;
			case "note" when Only(a, "text") && Has("text"): simple.Add($"player.note({Str(Tree.S(a["text"]), col + 12)});"); break;
			case "start_quest" when Only(a, "quest") && Has("quest") && _quests.TryGetValue(Tree.S(a["quest"]), out var sq): simple.Add($"{sq}.start();"); break;
			case "end_quest" when Only(a, "quest", "failed") && Has("quest") && _quests.TryGetValue(Tree.S(a["quest"]), out var eq): simple.Add(a.GetValueOrDefault("failed") is true ? $"{eq}.fail();" : $"{eq}.complete();"); break;
			case "brigade" when Only(a, "run", "effect", "days", "leg") && Has("run") && a.ContainsKey("effect"):
				{
					string method = Tree.S(a["effect"]) switch { "delay" => "delay", "turn_back" => "turnBack", "lose" => "lose", "cancel" => "cancel", "launch" => "launch", _ => null };
					string arg = a.ContainsKey("days") ? N("days") : a.ContainsKey("leg") ? N("leg") : "";
					if (method != null) simple.Add($"brigade({Q("run")}).{method}({arg});");
					break;
				}
		}
		foreach (var cand in simple)
			if (StmtOk(cand, a)) return One(depth, cand);

		// anything else: action("name", key: value, ...)
		var parts = new List<string> { Quote(verb) };
		foreach (var (k, v) in a)
		{
			if (k == "do") continue;
			parts.Add($"{k}: " + v switch { string s => Quote(s), double d => CJson.Num(d), bool b => b ? "true" : "false", _ => throw new CampError($"cannot write {k} of {verb}") });
		}
		return One(depth, $"action({string.Join(", ", parts)});");
	}
}
