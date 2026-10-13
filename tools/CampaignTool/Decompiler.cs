using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CampaignTool;

// The game's JSON -> the language, so an old script can be moved across and
// read back. compile(decompile(x)) is x.
public static class Decompiler
{
	private const int Wrap = 64;

	public static string Decompile(Dictionary<string, object> d, string name = "Script")
	{
		var sb = new StringBuilder();
		sb.Append($"campaign {name};\n\nconst\n");
		void Const(string n, string value) => sb.Append($"  {n} = {value};\n");
		Const("Title", StrExpr(Tree.S(d.GetValueOrDefault("title")), 10));
		if (Tree.S(d.GetValueOrDefault("description")).Length > 0) Const("Description", StrExpr(Tree.S(d["description"]), 16));
		if (Tree.S(d.GetValueOrDefault("note")).Length > 0) Const("Note", StrExpr(Tree.S(d["note"]), 9));
		if (d.TryGetValue("seed", out var seed)) Const("Seed", Tree.S(seed));
		if (d.GetValueOrDefault("posts_by_founding_year") is true) Const("PostsByFoundingYear", "true");

		var vars = Variables(d);
		if (vars.Count > 0) sb.Append($"\nvar\n  {string.Join(", ", vars)} : integer;\n");

		foreach (var bo in Tree.AsList(d.GetValueOrDefault("beats")))
		{
			sb.Append('\n');
			Beat(sb, (Dictionary<string, object>)bo);
		}
		sb.Append("\nend.\n");
		return sb.ToString();
	}

	private static List<string> Variables(Dictionary<string, object> d)
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
		Walk(d.GetValueOrDefault("beats"));
		return names.Where(n => Regex.IsMatch(n, @"^[A-Za-z_]\w*$")).ToList();
	}

	// ---------------------------------------------------------------- strings

	// 'text', with #10 for a new line, and long ones broken into pieces joined by +
	public static string StrExpr(string s, int indent)
	{
		var pieces = new List<string>();
		var cur = new StringBuilder();
		void Flush() { if (cur.Length > 0) { pieces.Add(cur.ToString()); cur.Clear(); } }
		foreach (var line in s.Split('\n').Select((text, i) => (text, i)))
		{
			if (line.i > 0) cur.Append('\u0001');   // a new line inside the run
			string rest = line.text;
			while (rest.Length > Wrap)
			{
				int cut = rest.LastIndexOf(' ', Wrap);
				if (cut <= 0) cut = Wrap; else cut++;
				cur.Append(rest[..cut]);
				Flush();
				rest = rest[cut..];
			}
			cur.Append(rest);
		}
		Flush();
		if (pieces.Count == 0) pieces.Add("");
		string pad = new(' ', indent);
		return string.Join(" +\n" + pad, pieces.Select(Lit));
	}

	// \u0001 marks a new line inside a piece
	private static string Lit(string piece)
	{
		var parts = piece.Split('\u0001');
		var sb = new StringBuilder();
		for (int i = 0; i < parts.Length; i++)
		{
			if (i > 0) sb.Append("#10");
			if (parts[i].Length > 0 || parts.Length == 1) sb.Append('\'').Append(parts[i].Replace("'", "''")).Append('\'');
		}
		return sb.ToString();
	}

	public static string Q(string s) => StrExpr(s, 0);

	private static string Name(object o) => Q(Tree.S(o));

	// ---------------------------------------------------------------- beats

	private static void Beat(StringBuilder sb, Dictionary<string, object> b)
	{
		string id = Tree.S(b["id"]);
		string kind = b.TryGetValue("kind", out var k) ? Tree.S(k) : "event";
		sb.Append($"beat {Q(id)} : {kind}");
		if (b.TryGetValue("date", out var date) && Tree.S(date).Length > 0) sb.Append($" on {Tree.S(date)}");
		string title = Tree.S(b.GetValueOrDefault("title"));
		if (title.Length > 0 && title != id) sb.Append($"\n    {StrExpr(title, 4)}");
		sb.Append(";\n");
		foreach (var (key, word) in new[] { ("window_days", "window"), ("lead_days", "lead"), ("delay_days", "delay"), ("repeat_days", "repeat") })
			if (b.TryGetValue(key, out var n)) sb.Append($"  {word} {Lang.FmtNum(Tree.N(n))};\n");
		foreach (var (key, word) in new[] { ("requires", "requires"), ("keywords", "keywords") })
			if (b.TryGetValue(key, out var v) && Tree.AsList(v).Count > 0) sb.Append($"  {word} {string.Join(", ", Tree.AsList(v).Select(Name))};\n");
		foreach (var (key, word) in new[] { ("when", "when"), ("done_when", "done when") })
			if (b.TryGetValue(key, out var v))
				foreach (var c in Tree.AsList(v)) sb.Append($"  {word} {Cond(c, 0)};\n");
		foreach (var (key, word) in new[] { ("pressure", "pressure"), ("happens", "happens") })
			if (b.TryGetValue(key, out var v) && Tree.AsList(v).Count > 0)
			{
				sb.Append($"  {word}\n  begin\n");
				foreach (var line in Stmts(Tree.AsList(v), 4)) sb.Append(line).Append('\n');
				sb.Append("  end;\n");
			}
		sb.Append("end;\n");
	}

	// ---------------------------------------------------------------- conditions
	// level: 0 anything, 1 inside or, 2 inside and, 3 inside not

	private static string Cond(object c, int level)
	{
		switch (c)
		{
			case bool b: return b ? "true" : "false";
			case string s: return Leaf(s.Trim(), level);
			case List<object> l: return l.Count == 0 ? "true" : Join(l, "and", level);
			case Dictionary<string, object> d:
				if (d.TryGetValue("all", out var all)) { var l = Tree.AsList(all); return l.Count == 0 ? "true" : Join(l, "and", level); }
				if (d.TryGetValue("any", out var any)) { var l = Tree.AsList(any); return l.Count == 0 ? "false" : Join(l, "or", level); }
				if (d.TryGetValue("not", out var not)) return "not " + Cond(not, 3);
				if (d.TryGetValue("p", out var p))
				{
					var args = Tree.AsList(d.GetValueOrDefault("a")).Select(Tree.S).ToList();
					// a one-argument fact compiles back to the compact string, not this form
					if (args.Count != 1 && Lang.PredicateNames.TryGetValue(Tree.S(p), out var pn) && !HasKeys(d, "p", "a")) return Raw(d);
					if (args.Count != 1 || !Lang.PredicateNames.ContainsKey(Tree.S(p))) return Raw(d);
				}
				if (d.TryGetValue("m", out var m))
				{
					string mname = Tree.S(m);
					var args = Tree.AsList(d.GetValueOrDefault("a")).Select(Tree.S).ToList();
					string op = d.TryGetValue("op", out var o) ? Tree.S(o) : ">=";
					if (HasKeys(d, "m", "a", "op", "v") && Lang.MetricNames.TryGetValue(mname, out var pas) && !(args.Count == 1 && !Regex.IsMatch(args[0], @"[(),]")) && !(args.Count == 0 && mname != "stock"))
						return $"{pas}({string.Join(", ", args.Select((x, i) => mname == "stock" && i == 1 ? Item(x) : Q(x)))}) {PasOp(op)} {Lang.FmtNum(Tree.N(d.GetValueOrDefault("v")))}";
				}
				return Raw(d);
		}
		return "true";
	}

	// An item reads as a bare word when it can, as in Stock('York Factory', Rum) < 10.
	private static string Item(string s) => Regex.IsMatch(s, @"^[A-Z][A-Za-z0-9]*$") && !Lang.Builtins.ContainsKey(s) && !Lang.Addressees.ContainsKey(s) && !Lang.Metrics.ContainsKey(s) && !Lang.Predicates.ContainsKey(s) ? s : Q(s);

	private static bool HasKeys(Dictionary<string, object> d, params string[] keys) => d.Keys.All(keys.Contains);

	private static string Join(List<object> parts, string op, int level)
	{
		int mine = op == "or" ? 1 : 2;
		var texts = parts.Select((x, i) => Child(x, op, i == 0)).ToList();
		string text = string.Join($" {op} ", texts);
		return level > mine || (level == mine) ? $"({text})" : text;
	}

	// A child of and/or is bracketed when it is itself compound, so its grouping survives.
	private static string Child(object x, string parentOp, bool first)
	{
		string t = Cond(x, parentOp == "or" ? 1 : 2);
		bool compound = x is Dictionary<string, object> d && (d.ContainsKey("all") || d.ContainsKey("any")) || x is List<object>;
		if (!compound) return t;
		string childOp = x is Dictionary<string, object> dd && dd.ContainsKey("any") ? "or" : "and";
		bool needs = childOp == parentOp || (parentOp == "and" && childOp == "or");
		if (needs && !t.StartsWith("(")) return $"({t})";
		return t;
	}

	private static string Raw(object c) => $"Raw({Q(c is string s ? s : Tree.Write(c).Replace("\n", " ").Replace("  ", " "))})";
	private static string PasOp(string op) => op switch { "==" => "=", "!=" => "<>", _ => op };

	private static readonly Regex CmpRe = new(@"^([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)$");

	private static string Leaf(string s, int level)
	{
		if (s.StartsWith("!")) return "not " + Leaf(s[1..].Trim(), 3);
		var m = CmpRe.Match(s);
		if (m.Success)
		{
			string name = m.Groups[1].Value, op = m.Groups[3].Value;
			var args = Lang.TopCommas(m.Groups[2].Value).Select(a => a.Trim().Trim('"')).ToList();
			string num = Lang.FmtNum(double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture));
			string canon = (string)Lang.Metric(name, args, op, double.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture)) is var cs ? cs : "";
			if (canon != s) return Raw(s);   // would not read back the same
			if (name == "var" && args.Count == 1 && Regex.IsMatch(args[0], @"^[A-Za-z_]\w*$")) return $"{args[0]} {PasOp(op)} {num}";
			if (Lang.MetricNames.TryGetValue(name, out var pas))
				return args.Count == 0 ? $"{pas} {PasOp(op)} {num}" : $"{pas}({string.Join(", ", args.Select((x, i) => name == "stock" && i == 1 ? Item(x) : Q(x)))}) {PasOp(op)} {num}";
			return Raw(s);
		}
		int colon = s.IndexOf(':');
		if (colon > 0)
		{
			string name = s[..colon].Trim(), arg = s[(colon + 1)..].Trim();
			if (arg.Contains(':') && name == "flag") return Raw(s);
			if (Lang.PredicateNames.TryGetValue(name, out var pas) && Lang.Predicate(name, new() { arg }) is string back && back == s) return $"{pas}({Q(arg)})";
			if (name == "after" && Regex.IsMatch(arg, @"^\d{4}-\d\d-\d\d$")) return $"Today >= {arg}";
			if (name == "before" && Regex.IsMatch(arg, @"^\d{4}-\d\d-\d\d$")) return $"Today < {arg}";
			if (name == "chance" && double.TryParse(arg, out _)) return $"Chance({arg})";
			if (name == "month" && Regex.IsMatch(arg, @"^\d{1,2}(-\d{1,2})?$")) return $"InMonths({arg.Replace("-", ", ")})";
		}
		return Raw(s);
	}

	// ---------------------------------------------------------------- statements

	private static List<string> Stmts(List<object> acts, int indent)
	{
		var lines = new List<string>();
		foreach (var a in acts) lines.AddRange(Stmt((Dictionary<string, object>)a, indent));
		return lines;
	}

	private static bool Keys(Dictionary<string, object> a, params string[] allowed) => a.Keys.Where(k => k != "do").All(allowed.Contains);

	private static List<string> One(int indent, string text) => new() { new string(' ', indent) + text + ";" };

	private static string Ident(string s, Dictionary<string, string> map) => map.TryGetValue(s, out var n) ? n : Q(s);

	private static List<string> Stmt(Dictionary<string, object> a, int indent)
	{
		string pad = new(' ', indent);
		string verb = a.TryGetValue("do", out var vo) ? Tree.S(vo) : "";
		switch (verb)
		{
			case "flag" or "unflag" when Keys(a, "name") && a.ContainsKey("name"):
				return One(indent, $"{(verb == "flag" ? "Flag" : "Unflag")}({Name(a["name"])})");
			case "set" when Keys(a, "var", "to") && a.ContainsKey("var") && a.ContainsKey("to"):
				return One(indent, $"{Tree.S(a["var"])} := {Lang.FmtNum(Tree.N(a["to"]))}");
			case "add" when Keys(a, "var", "by") && a.ContainsKey("var"):
				{
					string v = Tree.S(a["var"]);
					if (!a.ContainsKey("by")) return One(indent, $"Inc({v})");
					double by = Tree.N(a["by"]);
					if (by == -1) return One(indent, $"Dec({v})");
					return One(indent, by >= 0 ? $"Inc({v}, {Lang.FmtNum(by)})" : $"Dec({v}, {Lang.FmtNum(-by)})");
				}
			case "tension" when Keys(a, "add", "why") && a.ContainsKey("add"):
				return One(indent, $"Tension({Lang.FmtNum(Tree.N(a["add"]))}{(a.ContainsKey("why") ? ", " + StrExpr(Tree.S(a["why"]), indent + 9 + Lang.FmtNum(Tree.N(a["add"])).Length) : "")})");
			case "london" when Keys(a, "price", "quota") && a.ContainsKey("price") && a.ContainsKey("quota"):
				return One(indent, $"London({Lang.FmtNum(Tree.N(a["price"]))}, {Lang.FmtNum(Tree.N(a["quota"]))})");
			case "found_post" when Keys(a, "place") && a.ContainsKey("place"):
				return One(indent, $"FoundPost({Name(a["place"])})");
			case "trigger" when Keys(a, "beat") && a.ContainsKey("beat"):
				return One(indent, $"Trigger({Name(a["beat"])})");
			case "stock" when Keys(a, "post", "item", "add") && a.ContainsKey("post") && a.ContainsKey("item") && a.ContainsKey("add"):
				{
					string item = Tree.S(a["item"]);
					string itemText = Item(item);
					return One(indent, $"Stock({Name(a["post"])}, {itemText}, {Lang.FmtNum(Tree.N(a["add"]))})");
				}
			case "say" when Keys(a, "text", "to", "headline") && a.ContainsKey("text") && (!a.ContainsKey("headline") || a["headline"] is true):
				{
					string who = a.TryGetValue("to", out var t) ? ", " + (Tree.S(t) is "HBC" or "NWC" ? Tree.S(t) : Tree.S(t) == "all" ? "All" : Q(Tree.S(t))) : "";
					string call = a.GetValueOrDefault("headline") is true ? "Shout" : "Say";
					return One(indent, $"{call}({StrExpr(Tree.S(a["text"]), indent + call.Length + 1)}{who})");
				}
			case "brigade" when Keys(a, "run", "effect", "days", "leg") && a.ContainsKey("run") && !(a.ContainsKey("days") && a.ContainsKey("leg")):
				{
					string effect = a.TryGetValue("effect", out var e) ? Tree.S(e) : null;
					if (effect == null && (a.ContainsKey("days") || a.ContainsKey("leg"))) break;
					if (a.ContainsKey("days") && effect == "launch") break;
					if (a.ContainsKey("leg") && effect != "launch") break;
					string eff = effect == null ? "" : ", " + Ident(effect, Lang.EffectNames);
					string extra = a.ContainsKey("days") ? ", " + Lang.FmtNum(Tree.N(a["days"])) : a.ContainsKey("leg") ? ", " + Lang.FmtNum(Tree.N(a["leg"])) : "";
					return One(indent, $"Brigade({Name(a["run"])}{eff}{extra})");
				}
			case "news" when Keys(a, "from", "to", "subject", "text", "days", "company") && new[] { "from", "to", "subject", "text", "days" }.All(a.ContainsKey):
				{
					string to = Lang.AddresseeNames.TryGetValue(Tree.S(a["to"]), out var sn) ? sn : Q(Tree.S(a["to"]));
					string pad2 = new string(' ', indent + 5);
					var args = new List<string> { Name(a["from"]), to, StrExpr(Tree.S(a["subject"]), indent + 5), StrExpr(Tree.S(a["text"]), indent + 5), Lang.FmtNum(Tree.N(a["days"])) };
					if (a.ContainsKey("company")) args.Add(Tree.S(a["company"]) is "HBC" or "NWC" ? Tree.S(a["company"]) : Q(Tree.S(a["company"])));
					return new() { $"{pad}News({string.Join(",\n" + pad2, args)});" };
				}
			case "after" when Keys(a, "days", "then") && a.ContainsKey("days"):
				return Body($"{pad}after {Lang.FmtNum(Tree.N(a["days"]))} days do", Tree.AsList(a.GetValueOrDefault("then")), indent, "");
			case "if" when Keys(a, "cond", "then", "else"):
				{
					var lines = new List<string>();
					string head = $"{pad}if {Cond(a.GetValueOrDefault("cond"), 0)} then";
					var cur = a;
					bool first = true;
					while (true)
					{
						lines.AddRange(Body(first ? head : $"{pad}else if {Cond(cur.GetValueOrDefault("cond"), 0)} then", Tree.AsList(cur.GetValueOrDefault("then")), indent, null, noSemi: true));
						first = false;
						var el = Tree.AsList(cur.GetValueOrDefault("else"));
						if (el.Count == 0) break;
						if (el.Count == 1 && el[0] is Dictionary<string, object> n && Tree.S(n.GetValueOrDefault("do")) == "if" && Keys(n, "cond", "then", "else") && n.ContainsKey("cond")) { cur = n; continue; }
						lines.AddRange(Body($"{pad}else", el, indent, null, noSemi: true));
						break;
					}
					lines[^1] += ";";
					return lines;
				}
			case "random" when Keys(a, "options"):
				{
					var lines = new List<string> { $"{pad}case random of" };
					foreach (var oo in Tree.AsList(a.GetValueOrDefault("options")))
					{
						var o = (Dictionary<string, object>)oo;
						var inner = Tree.AsList(o.GetValueOrDefault("do"));
						lines.AddRange(Body($"{pad}  {Lang.FmtNum(Tree.N(o.TryGetValue("weight", out var w) ? w : 1.0))}:", inner, indent + 2, ""));
					}
					lines.Add($"{pad}end;");
					return lines;
				}
		}
		// anything else: Action('name', 'key', value, ...)
		var parts = new List<string> { Q(verb) };
		foreach (var (k, v) in a)
		{
			if (k == "do") continue;
			parts.Add(Q(k));
			parts.Add(v switch { string s => Q(s), double d => Lang.FmtNum(d), bool b => b ? "true" : "false", _ => throw new CampError($"cannot write {k} of {verb}") });
		}
		return One(indent, $"Action({string.Join(", ", parts)})");
	}

	// head, then a begin ... end block (or one statement, kept in a block for clarity)
	private static List<string> Body(string head, List<object> acts, int indent, string tail, bool noSemi = false)
	{
		string pad = new(' ', indent);
		var lines = new List<string> { head, $"{pad}begin" };
		lines.AddRange(Stmts(acts, indent + 2));
		lines.Add($"{pad}end");
		if (!noSemi) lines[^1] += ";";
		return lines;
	}
}
