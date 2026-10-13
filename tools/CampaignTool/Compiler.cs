using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CampaignTool;

// The language, in the end: Pascal-flavoured source -> the JSON tree the game reads.
public static class Lang
{
	public static readonly string[] BeatKinds = { "fixed", "conditional", "event", "pressure" };

	public static readonly Dictionary<string, string> Addressees = new(StringComparer.OrdinalIgnoreCase)
	{
		["Factor"] = "the Factor at York Factory", ["Postmaster"] = "the postmaster at Oxford House",
		["Partner"] = "the wintering partner", ["Chief"] = "the chief of the band",
	};
	public static readonly Dictionary<string, string> AddresseeNames = Addressees.ToDictionary(kv => kv.Value, kv => kv.Key);

	// Names that are values: the companies, what a brigade can be made to do, the seasons.
	public static readonly Dictionary<string, object> Builtins = new(StringComparer.OrdinalIgnoreCase)
	{
		["true"] = true, ["false"] = false,
		["HBC"] = "HBC", ["NWC"] = "NWC", ["All"] = "all",
		["Delay"] = "delay", ["TurnBack"] = "turn_back", ["Lose"] = "lose", ["Cancel"] = "cancel", ["Launch"] = "launch",
		["Breakup"] = "Breakup", ["OpenWater"] = "OpenWater", ["FreezeUp"] = "FreezeUp", ["Winter"] = "Winter",
	};
	public static readonly Dictionary<string, string> EffectNames = new() { ["delay"] = "Delay", ["turn_back"] = "TurnBack", ["lose"] = "Lose", ["cancel"] = "Cancel", ["launch"] = "Launch" };

	// Numbers the world can be asked: Pascal name -> the engine's.
	public static readonly Dictionary<string, (string json, int args)> Metrics = new(StringComparer.OrdinalIgnoreCase)
	{
		["Tension"] = ("tension", 0), ["Standing"] = ("standing", 0), ["Month"] = ("month", 0), ["Year"] = ("year", 0), ["Day"] = ("day", 0),
		["DayOfYear"] = ("day_of_year", 0), ["ConvoysActive"] = ("convoys_active", 0),
		["Stock"] = ("stock", 2), ["Furs"] = ("furs", 1), ["Count"] = ("count", 1), ["BeatAge"] = ("beat_age", 1),
		["ConvoyDaysLate"] = ("convoy_days_late", 1), ["ConvoyDaysOut"] = ("convoy_days_out", 1),
	};

	// Facts the world can be asked.
	public static readonly Dictionary<string, string> Predicates = new(StringComparer.OrdinalIgnoreCase)
	{
		["Flag"] = "flag", ["Beat"] = "beat", ["Post"] = "post", ["Dead"] = "dead", ["Season"] = "season", ["PlayerFaction"] = "player_faction",
		["ConvoyLate"] = "convoy_late", ["ConvoyLost"] = "convoy_lost", ["ConvoyRobbed"] = "convoy_robbed", ["ConvoyTurnedBack"] = "convoy_turned_back",
		["ConvoyOut"] = "convoy_out", ["ConvoyIn"] = "convoy_in", ["ConvoyWintering"] = "convoy_wintering",
	};
	public static readonly Dictionary<string, string> PredicateNames = Predicates.ToDictionary(kv => kv.Value, kv => kv.Key);
	public static readonly Dictionary<string, string> MetricNames = Metrics.ToDictionary(kv => kv.Value.json, kv => kv.Key);

	public static Dictionary<string, object> CompileText(string src) => new Compiler(Parser.ParseText(src)).Compile();

	public static string FmtNum(double d) => Tree.Num(d);

	public static List<string> TopCommas(string s)
	{
		var outp = new List<string>();
		if (s.Trim().Length == 0) return outp;
		var cur = new System.Text.StringBuilder();
		bool quote = false;
		for (int i = 0; i < s.Length; i++)
		{
			char ch = s[i];
			if (ch == '\\' && quote && i + 1 < s.Length) { cur.Append(s, i, 2); i++; continue; }
			if (ch == '"') quote = !quote;
			if (ch == ',' && !quote) { outp.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch);
		}
		outp.Add(cur.ToString());
		return outp;
	}

	// How a value reads when a number is joined to a string.
	public static string Join(object v) => v is double d ? Tree.Num(d) : v is bool b ? (b ? "true" : "false") : v?.ToString() ?? "";

	public static object Predicate(string name, List<string> args)
	{
		if (name == "flag" && args.Count == 1 && !args[0].Contains(':') && !args[0].StartsWith("!")) return $"flag:{args[0]}";
		if (args.Count == 1) return $"{name}:{args[0]}";
		return Tree.Dict(("p", name), ("a", args.Cast<object>().ToList()));
	}

	public static object Metric(string name, List<string> args, string op, double v)
	{
		string num = Tree.Num(v);
		if (args.Count == 0 && name != "var" && name != "stock") return $"{name}{op}{num}";
		if (args.Count == 1 && !Regex.IsMatch(args[0], @"[(),]")) return $"{name}({args[0]}) {op} {num}";
		return Tree.Dict(("m", name), ("a", args.Cast<object>().ToList()), ("op", op), ("v", v));
	}
}

internal sealed class Env
{
	private readonly Dictionary<string, object> _v = new(StringComparer.OrdinalIgnoreCase);
	private readonly Env _parent;
	public Env(Env parent = null) { _parent = parent; }
	public void Set(string k, object v) => _v[k] = v;
	public bool TryGet(string k, out object v)
	{
		for (var e = this; e != null; e = e._parent) if (e._v.TryGetValue(k, out v)) return true;
		v = null;
		return false;
	}
}

internal sealed class Compiler
{
	private readonly ProgramAst _p;
	private readonly Env _globals = new();
	private readonly Dictionary<string, object> _out = Tree.Dict(("title", ""), ("description", ""));
	private readonly List<object> _beats = new();
	private int _depth;

	public Compiler(ProgramAst p) { _p = p; }

	public Dictionary<string, object> Compile()
	{
		foreach (var item in _p.Order)
		{
			if (item is ConstDecl c) DeclareConst(c);
			else CompileTop((TopItem)item, _globals);
		}
		_out["beats"] = _beats;
		return _out;
	}

	private void DeclareConst(ConstDecl c)
	{
		var v = Const(c.Value, _globals, false);
		_globals.Set(c.Name, v);
		switch (c.Name.ToLowerInvariant())
		{
			case "title": _out["title"] = Lang.Join(v); break;
			case "description": _out["description"] = Lang.Join(v); break;
			case "note": _out["note"] = Lang.Join(v); break;
			case "seed": _out["seed"] = v is double ? v : throw new CampError("Seed must be a number", c.Line); break;
			case "postsbyfoundingyear": if (v is true) _out["posts_by_founding_year"] = true; break;
		}
	}

	// ---------------------------------------------------------------- constants

	private bool IsConst(Expr e, Env env)
	{
		switch (e)
		{
			case LitE: return true;
			case NameE n: return env.TryGet(n.Name, out _) || Lang.Builtins.ContainsKey(n.Name);
			case UnE u: return IsConst(u.Operand, env);
			case BinE b: return IsConst(b.Left, env) && IsConst(b.Right, env);
			default: return false;
		}
	}

	// bare: an unknown name is its own text (Flag(governor_killed), Rum, Delay)
	private object Const(Expr e, Env env, bool bare)
	{
		switch (e)
		{
			case LitE l: return l.Value;
			case NameE n:
				if (env.TryGet(n.Name, out var v)) return v;
				if (Lang.Builtins.TryGetValue(n.Name, out var b)) return b;
				if (bare) return n.Name;
				throw new CampError($"'{n.Name}' is not a constant here (declare it under const, or use quotes)", n.Line);
			case UnE u:
				{
					var x = Const(u.Operand, env, false);
					if (u.Op == "-" && x is double d) return -d;
					if (u.Op == "not" && x is bool bb) return !bb;
					throw new CampError($"cannot apply {u.Op} to {Lang.Join(x)}", u.Line);
				}
			case BinE be:
				{
					var l = Const(be.Left, env, bare); var r = Const(be.Right, env, bare);
					switch (be.Op)
					{
						case "+":
							if (l is double a && r is double c) return a + c;
							return Lang.Join(l) + Lang.Join(r);
						case "-": return Num(l, be) - Num(r, be);
						case "*": return Num(l, be) * Num(r, be);
						case "/": return Num(l, be) / Num(r, be);
						case "and": return Bool(l, be) && Bool(r, be);
						case "or": return Bool(l, be) || Bool(r, be);
						case "=": return Equals(l, r);
						case "<>": return !Equals(l, r);
						case "<": return Num(l, be) < Num(r, be);
						case ">": return Num(l, be) > Num(r, be);
						case "<=": return Num(l, be) <= Num(r, be);
						case ">=": return Num(l, be) >= Num(r, be);
					}
					break;
				}
			case CallE c2:
				throw new CampError($"{c2.Name}(...) is not a constant", c2.Line);
		}
		throw new CampError("not a constant", e.Line);
	}

	private static double Num(object v, Expr at) => v is double d ? d : throw new CampError($"expected a number, got '{Lang.Join(v)}'", at.Line);
	private static bool Bool(object v, Expr at) => v is bool b ? b : throw new CampError($"expected true or false, got '{Lang.Join(v)}'", at.Line);
	private string Str(Expr e, Env env) => Lang.Join(Const(e, env, true));
	private double Number(Expr e, Env env) => Num(Const(e, env, false), e);

	// ---------------------------------------------------------------- beats

	private void CompileTop(TopItem item, Env env)
	{
		if (item.Beat != null) { _beats.Add(CompileBeat(item.Beat, env)); return; }
		int from = (int)Number(item.From, env), to = (int)Number(item.To, env);
		for (int i = from; i <= to; i++)
		{
			var inner = new Env(env);
			inner.Set(item.LoopVar, (double)i);
			foreach (var b in item.Body) CompileTop(b, inner);
		}
	}

	private Dictionary<string, object> CompileBeat(BeatDecl b, Env env)
	{
		string id = Str(b.Id, env);
		var beat = Tree.Dict(("id", id), ("title", b.Title != null ? Str(b.Title, env) : id), ("kind", b.Kind));
		if (b.Date != null)
		{
			string date = Str(b.Date, env);
			if (!Regex.IsMatch(date, @"^\d{4}-\d\d-\d\d$")) throw new CampError($"beat {id}: '{date}' is not a date (YYYY-MM-DD)", b.Line);
			beat["date"] = date;
		}
		foreach (var (name, expr) in b.Numbers)
			beat[name switch { "window" => "window_days", "lead" => "lead_days", "delay" => "delay_days", _ => "repeat_days" }] = Math.Truncate(Number(expr, env));
		if (b.Requires.Count > 0) beat["requires"] = b.Requires.Select(r => (object)Str(r, env)).ToList();
		if (b.Keywords.Count > 0) beat["keywords"] = b.Keywords.Select(r => (object)Str(r, env)).ToList();
		if (b.When.Count > 0) beat["when"] = b.When.Select(c => Cond(c, env)).ToList();
		if (b.DoneWhen.Count > 0) beat["done_when"] = b.DoneWhen.Select(c => Cond(c, env)).ToList();
		if (b.Pressure != null) beat["pressure"] = Stmts(b.Pressure, env);
		if (b.Happens != null) beat["happens"] = Stmts(b.Happens, env);
		return beat;
	}

	// ---------------------------------------------------------------- conditions

	private object Cond(Expr e, Env env)
	{
		switch (e)
		{
			case LitE { Value: bool b }: return b;
			case NameE n:
				if (env.TryGet(n.Name, out var cv) && cv is bool cb) return cb;
				if (Lang.Builtins.TryGetValue(n.Name, out var bv) && bv is bool bb) return bb;
				throw new CampError($"'{n.Name}' is not a condition (a flag is written Flag('{n.Name}'))", n.Line);
			case UnE { Op: "not" } u: return Tree.Dict(("not", Cond(u.Operand, env)));
			case BinE { Op: "and" or "or" } be:
				{
					string key = be.Op == "and" ? "all" : "any";
					var parts = new List<object>();
					if (be.Left is BinE lb && lb.Op == be.Op && !lb.Paren) parts.AddRange((List<object>)((Dictionary<string, object>)Cond(be.Left, env))[key]);
					else parts.Add(Cond(be.Left, env));
					parts.Add(Cond(be.Right, env));
					return Tree.Dict((key, parts));
				}
			case BinE be when be.Op is "=" or "<>" or "<" or ">" or "<=" or ">=": return Compare(be, env);
			case CallE c: return Predicate(c, env);
		}
		throw new CampError("this is not a condition", e.Line);
	}

	private static string FlipOp(string op) => op switch { "<" => ">", ">" => "<", "<=" => ">=", ">=" => "<=", _ => op };
	private static string JsonOp(string op) => op switch { "=" => "==", "<>" => "!=", _ => op };

	private object Compare(BinE be, Env env)
	{
		if (IsConst(be.Left, env) && IsConst(be.Right, env)) return Const(be, env, false);
		string op = be.Op;
		Expr metric = be.Left, value = be.Right;
		if (!IsMetric(metric, env) && IsMetric(value, env)) { (metric, value) = (value, metric); op = FlipOp(op); }
		if (!IsMetric(metric, env)) throw new CampError("a comparison needs a number the game knows (Tension, Stock(...), a declared var...) on one side", be.Line);
		if (metric is NameE { Name: var tn } && tn.Equals("Today", StringComparison.OrdinalIgnoreCase)) return TodayCompare(be, op, value, env);
		var (name, args) = MetricOf(metric, env);
		var v = Const(value, env, false);
		if (v is not double d) throw new CampError($"compare with a number, not '{Lang.Join(v)}'", be.Line);
		return Lang.Metric(name, args, JsonOp(op), d);
	}

	// Today >= 1816-06-19 reads as after:, Today < ... as before:.
	private object TodayCompare(BinE be, string op, Expr value, Env env)
	{
		string date = Lang.Join(Const(value, env, false));
		if (!Regex.IsMatch(date, @"^\d{4}-\d\d-\d\d$")) throw new CampError("Today is compared with a date (1816-06-19)", be.Line);
		var next = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1).ToString("yyyy-MM-dd");
		return op switch
		{
			">=" => Lang.Predicate("after", new() { date }),
			"<" => Lang.Predicate("before", new() { date }),
			">" => Lang.Predicate("after", new() { next }),
			"<=" => Lang.Predicate("before", new() { next }),
			_ => throw new CampError("Today can be compared with <, <=, > and >= only", be.Line),
		};
	}

	private bool IsMetric(Expr e, Env env) => e switch
	{
		NameE n => n.Name.Equals("Today", StringComparison.OrdinalIgnoreCase) || (!env.TryGet(n.Name, out _) && (Lang.Metrics.TryGetValue(n.Name, out var m) && m.args == 0 || _p.Vars.ContainsKey(n.Name))),
		CallE c => Lang.Metrics.TryGetValue(c.Name, out var m) && m.args > 0,
		_ => false,
	};

	private (string name, List<string> args) MetricOf(Expr e, Env env)
	{
		if (e is NameE n)
		{
			if (!env.TryGet(n.Name, out _) && Lang.Metrics.TryGetValue(n.Name, out var m)) return (m.json, new List<string>());
			if (_p.Vars.ContainsKey(n.Name)) return ("var", new List<string> { n.Name });
		}
		var c = (CallE)e;
		var (json, count) = Lang.Metrics[c.Name];
		if (c.Args.Count != count) throw new CampError($"{c.Name} takes {count} argument{(count == 1 ? "" : "s")}", c.Line);
		return (json, c.Args.Select(a => Str(a, env)).ToList());
	}

	private object Predicate(CallE c, Env env)
	{
		string lower = c.Name.ToLowerInvariant();
		if (Lang.Predicates.TryGetValue(c.Name, out var json))
		{
			if (c.Args.Count != 1) throw new CampError($"{c.Name} takes one argument", c.Line);
			return Lang.Predicate(json, new() { Str(c.Args[0], env) });
		}
		if (lower == "chance")
		{
			if (c.Args.Count != 1) throw new CampError("Chance takes a percentage", c.Line);
			return Lang.Predicate("chance", new() { Lang.FmtNum(Number(c.Args[0], env)) });
		}
		if (lower == "inmonths")
		{
			if (c.Args.Count is < 1 or > 2) throw new CampError("InMonths(first[, last])", c.Line);
			string a = Lang.FmtNum(Number(c.Args[0], env));
			string spec = c.Args.Count == 2 ? $"{a}-{Lang.FmtNum(Number(c.Args[1], env))}" : a;
			return Lang.Predicate("month", new() { spec });
		}
		if (lower == "raw")
		{
			if (c.Args.Count != 1) throw new CampError("Raw('condition text')", c.Line);
			string raw = Str(c.Args[0], env);
			return raw.TrimStart().StartsWith("{") ? Tree.Parse(raw) : raw;
		}
		if (IsMetric(c, env)) throw new CampError($"{c.Name}(...) is a number: compare it (e.g. {c.Name}(...) > 0)", c.Line);
		throw new CampError($"unknown condition {c.Name}", c.Line);
	}

	// ---------------------------------------------------------------- statements

	private List<object> Stmts(Stmt s, Env env)
	{
		var outp = new List<object>();
		Emit(s, env, outp);
		return outp;
	}

	private void Emit(Stmt s, Env env, List<object> outp)
	{
		switch (s)
		{
			case null: return;
			case BlockS b: foreach (var x in b.Body) Emit(x, env, outp); return;
			case IfS i:
				{
					var node = Tree.Dict(("do", "if"), ("cond", Cond(i.Cond, env)), ("then", Stmts(i.Then, env)));
					if (i.Else != null) node["else"] = Stmts(i.Else, env);
					outp.Add(node);
					return;
				}
			case AssignS a: outp.Add(Assign(a, env)); return;
			case ForS f:
				{
					int from = (int)Number(f.From, env), to = (int)Number(f.To, env);
					for (int k = from; k <= to; k++)
					{
						var inner = new Env(env);
						inner.Set(f.Var, (double)k);
						Emit(f.Body, inner, outp);
					}
					return;
				}
			case AfterS af:
				outp.Add(Tree.Dict(("do", "after"), ("days", Number(af.Days, env)), ("then", Stmts(af.Body, env))));
				return;
			case ChooseS ch:
				{
					var opts = new List<object>();
					foreach (var (w, body) in ch.Options) opts.Add(Tree.Dict(("weight", Number(w, env)), ("do", Stmts(body, env))));
					outp.Add(Tree.Dict(("do", "random"), ("options", opts)));
					return;
				}
			case CallS c: Call(c, env, outp); return;
		}
	}

	private void NeedVar(string name, int line)
	{
		if (!_p.Vars.ContainsKey(name)) throw new CampError($"variable {name} is not declared (add it under var)", line);
	}

	private object Assign(AssignS a, Env env)
	{
		NeedVar(a.Var, a.Line);
		// x := x + 3 and x := x - 1 are additions
		if (a.Value is BinE { Op: "+" or "-" } be)
		{
			bool leftIsVar = be.Left is NameE ln && ln.Name.Equals(a.Var, StringComparison.OrdinalIgnoreCase);
			bool rightIsVar = be.Right is NameE rn && rn.Name.Equals(a.Var, StringComparison.OrdinalIgnoreCase);
			if (leftIsVar && IsConst(be.Right, env)) { double by = Num(Const(be.Right, env, false), be); return Tree.Dict(("do", "add"), ("var", a.Var), ("by", be.Op == "+" ? by : -by)); }
			if (rightIsVar && be.Op == "+" && IsConst(be.Left, env)) return Tree.Dict(("do", "add"), ("var", a.Var), ("by", Num(Const(be.Left, env, false), be)));
		}
		return Tree.Dict(("do", "set"), ("var", a.Var), ("to", Number(a.Value, env)));
	}

	private void Need(CallS c, int min, int max, string usage)
	{
		if (c.Args.Count < min || c.Args.Count > max) throw new CampError($"{c.Name}: {usage}", c.Line);
	}

	private void Call(CallS c, Env env, List<object> outp)
	{
		if (_p.Procs.TryGetValue(c.Name, out var proc)) { Expand(proc, c, env, outp); return; }
		string name = c.Name.ToLowerInvariant();
		switch (name)
		{
			case "flag": case "unflag":
				Need(c, 1, 1, $"{c.Name}('name')");
				outp.Add(Tree.Dict(("do", name), ("name", Str(c.Args[0], env))));
				return;
			case "inc": case "dec":
				{
					Need(c, 1, 2, $"{c.Name}(variable[, amount])");
					string v = (c.Args[0] as NameE)?.Name ?? throw new CampError($"{c.Name} needs a variable", c.Line);
					NeedVar(v, c.Line);
					var act = Tree.Dict(("do", "add"), ("var", v));
					if (c.Args.Count == 2) { double by = Number(c.Args[1], env); act["by"] = name == "inc" ? by : -by; }
					else if (name == "dec") act["by"] = -1.0;
					outp.Add(act);
					return;
				}
			case "tension":
				{
					Need(c, 1, 2, "Tension(amount[, 'why'])");
					var act = Tree.Dict(("do", "tension"), ("add", Number(c.Args[0], env)));
					if (c.Args.Count == 2) act["why"] = Str(c.Args[1], env);
					outp.Add(act);
					return;
				}
			case "london":
				Need(c, 2, 2, "London(price, quota)");
				outp.Add(Tree.Dict(("do", "london"), ("price", Number(c.Args[0], env)), ("quota", Number(c.Args[1], env))));
				return;
			case "foundpost":
				Need(c, 1, 1, "FoundPost('place')");
				outp.Add(Tree.Dict(("do", "found_post"), ("place", Str(c.Args[0], env))));
				return;
			case "news":
				{
					Need(c, 5, 6, "News(from, to, subject, text, days[, company])");
					string to = Str(c.Args[1], env);
					if (Lang.Addressees.TryGetValue(to, out var full)) to = full;
					var act = Tree.Dict(("do", "news"), ("from", Str(c.Args[0], env)), ("to", to), ("subject", Str(c.Args[2], env)), ("text", Str(c.Args[3], env)), ("days", Number(c.Args[4], env)));
					if (c.Args.Count == 6) act["company"] = Str(c.Args[5], env);
					outp.Add(act);
					return;
				}
			case "brigade":
				{
					Need(c, 1, 3, "Brigade('run'[, Delay|TurnBack|Lose|Cancel|Launch[, days or leg]])");
					var act = Tree.Dict(("do", "brigade"), ("run", Str(c.Args[0], env)));
					if (c.Args.Count >= 2) act["effect"] = Str(c.Args[1], env);
					if (c.Args.Count == 3) act[Equals(act.GetValueOrDefault("effect"), "launch") ? "leg" : "days"] = Number(c.Args[2], env);
					outp.Add(act);
					return;
				}
			case "stock":
				Need(c, 3, 3, "Stock('post', Item, amount)");
				outp.Add(Tree.Dict(("do", "stock"), ("post", Str(c.Args[0], env)), ("item", Str(c.Args[1], env)), ("add", Number(c.Args[2], env))));
				return;
			case "say": case "shout":
				{
					Need(c, 1, 2, $"{c.Name}('text'[, HBC|NWC|All])");
					var act = Tree.Dict(("do", "say"), ("text", Str(c.Args[0], env)));
					if (c.Args.Count == 2) act["to"] = Str(c.Args[1], env);
					if (name == "shout") act["headline"] = true;
					outp.Add(act);
					return;
				}
			case "trigger":
				Need(c, 1, 1, "Trigger('beat')");
				outp.Add(Tree.Dict(("do", "trigger"), ("beat", Str(c.Args[0], env))));
				return;
			case "action":
				{
					// Action('name', 'key', value, 'key', value ...): anything the game does that the language has no word for
					if (c.Args.Count < 1 || c.Args.Count % 2 == 0) throw new CampError("Action('name', 'key', value, ...)", c.Line);
					var act = Tree.Dict(("do", Str(c.Args[0], env)));
					for (int i = 1; i < c.Args.Count; i += 2) act[Str(c.Args[i], env)] = Const(c.Args[i + 1], env, false);
					outp.Add(act);
					return;
				}
		}
		throw new CampError($"unknown statement {c.Name} (declare it with 'procedure {c.Name};' or check the spelling)", c.Line);
	}

	private void Expand(ProcDecl proc, CallS c, Env env, List<object> outp)
	{
		if (c.Args.Count != proc.Params.Count) throw new CampError($"{proc.Name} takes {proc.Params.Count} argument{(proc.Params.Count == 1 ? "" : "s")}", c.Line);
		if (++_depth > 30) throw new CampError($"procedure {proc.Name} calls itself without end", c.Line);
		try
		{
			var inner = new Env(_globals);
			for (int i = 0; i < proc.Params.Count; i++) inner.Set(proc.Params[i], Const(c.Args[i], env, true));
			Emit(proc.Body, inner, outp);
		}
		finally { _depth--; }
	}
}
