using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CampaignTool;

public sealed class CompileResult
{
	public Dictionary<string, object> Json = new();
	public Dictionary<string, int> BeatLines = new();        // beat id -> line in the source
	public Dictionary<string, string> BeatNames = new();     // beat id -> the name it is written with
	public Dictionary<string, int> QuestLines = new();       // quest id -> line in the source
	public Dictionary<string, int> StepLines = new();        // "quest id/step id" -> line in the source
	public List<Diagnostic> Warnings = new();
	// The parts the header includes (src/parts/<name>.camp), each with the line its
	// lines are numbered from, and where the header names them.
	public List<(string Name, int Base)> Parts = new();
	public Span IncludeAt;

	// Which file a line of the program is in: (null, line) for the campaign's own
	// file, or (part name, its own line) for an included part.
	public (string Part, int Line) Where(int line)
	{
		for (int i = Parts.Count - 1; i >= 0; i--) if (line > Parts[i].Base) return (Parts[i].Name, line - Parts[i].Base);
		return (null, line);
	}
}

public static class CampLang
{
	public static Dictionary<string, object> CompileText(string src) => Compile(src).Json;

	// An included part's lines are numbered from k * PartStride, so a span says its file.
	public const int PartStride = 100000;
	public static string PartsDir => System.IO.Path.Combine(Known.Root, "tools", "CampaignTool", "src", "parts");
	public static string PartPath(string name) => $"tools/CampaignTool/src/parts/{name}.camp";

	// The parts a campaign's header includes: include: "american-road" (several: "a, b").
	public static List<string> IncludedParts(ScriptAst ast, out Span at)
	{
		at = default;
		var inc = ast.Campaign?.Args.FirstOrDefault(a => a.Name == "include");
		if (inc == null) return new List<string>();
		if (inc.Value is not LitExpr { Value: string s }) throw new CampError("include names parts in src/parts by their file name, as text: include: \"american-road\"", inc.Value.At);
		at = inc.Value.At;
		var names = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
		foreach (var n in names) if (!System.Text.RegularExpressions.Regex.IsMatch(n, @"^[\w\-]+$")) throw new CampError($"'{n}' is not a part's name (letters, digits, - and _)", at);
		return names;
	}

	// overrides: a part's text as it stands in the editor, in place of the file.
	public static CompileResult Compile(string src, IReadOnlyDictionary<string, string> overrides = null)
	{
		var ast = CampParser.Parse(src);
		var names = IncludedParts(ast, out var incAt);
		var parts = new List<(string, int)>();
		for (int k = 0; k < names.Count; k++)
		{
			string name = names[k], text;
			if (overrides != null && overrides.TryGetValue(name, out var o)) text = o;
			else
			{
				string file = System.IO.Path.Combine(PartsDir, name + ".camp");
				if (!System.IO.File.Exists(file)) throw new CampError($"include: there is no part called '{name}' ({PartPath(name)})", incAt);
				text = System.IO.File.ReadAllText(file);
			}
			int lineBase = (k + 1) * PartStride;
			parts.Add((name, lineBase));
			try { ast.Decls.AddRange(CampParser.ParsePart(text, lineBase).Decls); }
			catch (CampError e) { throw InPart(e, name, lineBase); }
		}
		try
		{
			var res = new Compiler(ast).Run();
			res.Parts = parts; res.IncludeAt = incAt;
			return res;
		}
		catch (CampError e) when (e.Part == null && e.At.Line > PartStride)
		{
			var (name, lineBase) = parts.Last(p => e.At.Line > p.Item2);
			throw InPart(e, name, lineBase);
		}
	}

	// An error in a part: its own line, and the part named in the message.
	private static CampError InPart(CampError e, string name, int lineBase) =>
		e.Part != null ? e : new CampError($"in {PartPath(name)}: {e.Msg}", new Span(Math.Max(1, e.At.Line - lineBase), e.At.Col, e.At.Length)) { Part = name };
}

// The kind of a beat is worked out from what it has, so that most beats never say it.
public static class BeatKinds
{
	public static readonly string[] All = { "fixed", "conditional", "event", "pressure" };
	public static string Derive(Dictionary<string, object> beat)
	{
		if (!beat.ContainsKey("happens")) return "pressure";
		if (!beat.ContainsKey("date")) return "conditional";
		if (beat.ContainsKey("window_days") || beat.ContainsKey("lead_days") || beat.ContainsKey("pressure") || beat.ContainsKey("done_when")) return "event";
		return "fixed";
	}
}

internal sealed partial class Compiler
{
	private sealed record Inst(BeatDecl Decl, Env Env, string Id);

	private static readonly (string hook, string key, bool cond)[] HookMap =
	{
		("AwakenWhen", "when", true), ("OnAwaken", "on_awaken", false), ("OnApproach", "pressure", false),
		("ActivateWhen", "activate_when", true), ("OnActivate", "happens", false),
		("ResolveWhen", "done_when", true), ("OnResolve", "on_resolve", false),
		("CancelWhen", "cancel_when", true), ("OnCancel", "on_cancel", false),
	};

	private static readonly string[] ValueTypes = { "int", "float", "string", "bool", "Date", "Days", "Company", "Person", "Item", "Season", "Kind", "Beat", "Brigade", "Quest", "Task", "string[]", "int[]" };

	private readonly ScriptAst _ast;
	private readonly Env _globals = new();
	private readonly Dictionary<string, double> _vars = new();
	private readonly Dictionary<string, FuncDecl> _funcs = new();
	private Dictionary<string, FuncDecl> _localFuncs = new();
	private readonly List<Inst> _insts = new();
	private readonly Dictionary<string, string> _byName = new();       // name -> id; null when two beats share the name
	private readonly HashSet<string> _ids = new();
	private readonly CompileResult _res = new();
	private List<object> _out;
	private int _depth;

	public Compiler(ScriptAst ast) { _ast = ast; }

	// ---------------------------------------------------------------- the script

	public CompileResult Run()
	{
		var root = _res.Json;
		root["title"] = ""; root["description"] = "";
		Header(root);
		Declare(_ast.Decls, _globals);
		foreach (var inst in _insts)
		{
			if (!_ids.Add(inst.Id)) throw new CampError($"there are two beats with the id \"{inst.Id}\"", inst.Decl.NameAt);
			var n = inst.Decl.Name;
			_byName[n] = _byName.ContainsKey(n) ? null : inst.Id;
		}
		var vars = _vars.Where(kv => kv.Value != 0).ToList();
		if (vars.Count > 0) { var vd = new Dictionary<string, object>(); foreach (var kv in vars) vd[kv.Key] = kv.Value; root["vars"] = vd; }
		var beats = new List<object>();
		foreach (var inst in _insts)
		{
			beats.Add(CompileBeat(inst));
			_res.BeatLines[inst.Id] = inst.Decl.At.Line;
			_res.BeatNames[inst.Id] = inst.Decl.Name;
		}
		root["beats"] = beats;
		if (_lines.Count > 0) root["questlines"] = CompileLines();
		return _res;
	}

	private void Header(Dictionary<string, object> root)
	{
		var c = _ast.Campaign;
		bool titled = false;
		foreach (var a in c.Args)
		{
			if (a.Name == null) throw new CampError("the header's values are named: campaign " + c.Name + "(title: \"...\", description: \"...\")", a.Value.At);
			var v = Eval(a.Value, _globals);
			switch (a.Name)
			{
				case "title": root["title"] = Text(v, a); titled = true; break;
				case "description": root["description"] = Text(v, a); break;
				case "note": root["note"] = Text(v, a); break;
				case "seed": root["seed"] = v is double d && d >= 0 && d == Math.Floor(d) ? d : throw new CampError("seed must be a whole number", a.Value.At); break;
				case "postsByFoundingYear": if (v is true) root["posts_by_founding_year"] = true; else if (v is not false) throw new CampError("postsByFoundingYear is true or false", a.Value.At); break;
				case "include": break;   // read before compiling (CampLang.Compile): the parts' declarations are the script's own
				case "extends": root["extends"] = v is string ex && ex.Length > 0 ? ex : throw new CampError("extends names another campaign by its id (its file name), e.g. \"historical\"", a.Value.At); break;
				case "start":
					if (root.ContainsKey("start_year")) throw new CampError("give start or startYear, not both", a.NameAt);
					root["start"] = v is DateV sdv ? sdv.ToString() : throw new CampError($"start is the world's first day, date(year, month, day), but got {Describe(v)}", a.Value.At);
					break;
				case "startYear":
					if (root.ContainsKey("start")) throw new CampError("give start or startYear, not both", a.NameAt);
					root["start_year"] = v is double sy && sy == Math.Floor(sy) && sy >= 1700 && sy <= 2100 ? sy : throw new CampError($"startYear is a year (1700-2100), but got {Describe(v)}", a.Value.At);
					break;
				case "startPath":
					root["start_path"] = v is EnumV { Type: "Path" } pv ? pv.Value : throw new CampError($"startPath is a road in ({string.Join(", ", Library.Paths.Select(p => "Path." + p))}), but got {Describe(v)}", a.Value.At);
					break;
				case "startAt":
					root["start_at"] = v is string sa && sa.Trim().Length > 0 ? sa : throw new CampError($"startAt is a place by its name (a post, a town, a camp or a place on the map), but got {Describe(v)}", a.Value.At);
					break;
				default: throw new CampError($"the campaign header has no '{a.Name}'.{Suggest(a.Name, HeaderArgs)} (its values are {string.Join(", ", HeaderArgs)})", a.NameAt);
			}
		}
		if (!titled) throw new CampError("the campaign needs a title: campaign " + c.Name + "(title: \"...\")", c.At);
	}

	private static readonly string[] HeaderArgs = { "title", "description", "note", "seed", "postsByFoundingYear", "extends", "include", "start", "startYear", "startPath", "startAt" };

	private string Text(object v, Arg a) => v is string s ? s : throw new CampError($"{a.Name} is text, but got {Describe(v)}", a.Value.At);

	// ---------------------------------------------------------------- declarations

	private void Declare(List<Decl> decls, Env env)
	{
		foreach (var d in decls)
		{
			switch (d)
			{
				case ConstDecl c:
					{
						var v = Eval(c.Value, env);
						CheckType(c.Type, v, c.Value.At, $"const {c.Name}");
						env.Set(c.Name, v, c.Type);
						break;
					}
				case VarDecl v:
					{
						if (env != _globals) throw new CampError($"variable {v.Name} must be declared at the top of the campaign, not inside a loop", v.At);
						if (v.Type != "int" && v.Type != "float") throw new CampError($"a campaign variable is an int or a float ('{v.Type} {v.Name}' is neither); text and dates are const", v.At);
						if (_vars.ContainsKey(v.Name)) throw new CampError($"variable {v.Name} is declared twice", v.At);
						double init = 0;
						if (v.Value != null)
						{
							var iv = Eval(v.Value, env);
							init = iv is double dd ? dd : throw new CampError($"a variable starts at a number, but got {Describe(iv)}", v.Value.At);
							if (v.Type == "int" && init != Math.Floor(init)) throw new CampError($"{v.Name} is an int but starts at {CJson.Num(init)}", v.Value.At);
						}
						_vars[v.Name] = init;
						break;
					}
				case FuncDecl f:
					if (_funcs.ContainsKey(f.Name) || Library.Find("", f.Name) != null) throw new CampError($"there is already a function called {f.Name}", f.At);
					_funcs[f.Name] = f;
					break;
				case BeatDecl b:
					_insts.Add(new Inst(b, env, BeatId(b, env)));
					break;
				case QuestLineDecl ql:
					if (env != _globals) throw new CampError("a questline belongs at the top of the campaign, not inside a loop", ql.At);
					DeclareLine(ql, env);
					break;
				case LoopDecl l:
					if (l.For != null)
						foreach (var i in Counting(l.For, env)) { var inner = new Env(env); inner.Set(l.For.Var, i); Declare(l.Body, inner); }
					else
						foreach (var item in Items(l.Foreach, env)) { var inner = new Env(env); inner.Set(l.Foreach.Var, item); Declare(l.Body, inner); }
					break;
				default: throw new CampError("this cannot be declared here", d.At);
			}
		}
	}

	private string BeatId(BeatDecl b, Env env)
	{
		var idArg = b.Args.FirstOrDefault(a => a.Name == "id");
		if (idArg == null) return b.Name;
		var v = Eval(idArg.Value, env);
		return v is string s && s.Length > 0 ? s : throw new CampError("a beat's id is a non-empty string", idArg.Value.At);
	}

	// ---------------------------------------------------------------- loops

	private IEnumerable<object> Counting(ForStmt f, Env env)
	{
		double from = Whole(Eval(f.From, env), f.From), limit = Whole(Eval(f.Limit, env), f.Limit);
		double step = f.Step == null ? 1 : Whole(Eval(f.Step, env), f.Step);
		if (step <= 0) throw new CampError("a loop step is a positive number", f.At);
		int guard = 0;
		for (double i = from; f.LimitOp switch { "<" => i < limit, "<=" => i <= limit, ">" => i > limit, _ => i >= limit }; i += f.Down ? -step : step)
		{
			if (++guard > 5000) throw new CampError("this loop runs more than 5000 times: is it counting the wrong way?", f.At);
			yield return i;
		}
	}

	private IEnumerable<object> Items(ForeachStmt f, Env env)
	{
		if (f.Source is RangeExpr r)
		{
			double a = Whole(Eval(r.From, env), r.From), b = Whole(Eval(r.To, env), r.To);
			if (b - a > 5000) throw new CampError("this range has more than 5000 numbers", r.At);
			for (double i = a; i <= b; i++) yield return i;
			yield break;
		}
		var v = Eval(f.Source, env);
		if (v is ArrV arr) { foreach (var x in arr.Items) yield return x; yield break; }
		throw new CampError($"foreach goes over a range (1818..1820) or a list ([\"a\", \"b\"]), not {Describe(v)}", f.Source.At);
	}

	private double Whole(object v, Expr at) => v is double d && d == Math.Floor(d) ? d : throw new CampError($"expected a whole number, but got {Describe(v)}", at.At);

	// ---------------------------------------------------------------- beats

	private Dictionary<string, object> CompileBeat(Inst inst)
	{
		var b = inst.Decl;
		var env = new Env(inst.Env);
		var saveFuncs = _localFuncs;
		_localFuncs = new();
		try
		{
			var fields = new Dictionary<string, object>();
			string explicitKind = null;
			string title = inst.Id;
			var seen = new HashSet<string>();
			foreach (var a in b.Args)
			{
				if (a.Name == null) throw new CampError($"a beat's header values are named: beat {b.Name}(date: date(1816, 6, 19), title: \"...\")", a.Value.At);
				if (!seen.Add(a.Name)) throw new CampError($"{a.Name} is given twice", a.NameAt);
				if (a.Name == "id") continue;
				var v = Eval(a.Value, env);
				switch (a.Name)
				{
					case "title": title = v as string ?? throw new CampError($"title is text, but got {Describe(v)}", a.Value.At); break;
					case "kind": explicitKind = v is EnumV { Type: "Kind" } ke ? ke.Value.ToLowerInvariant() : throw new CampError($"kind is Kind.Fixed, Kind.Conditional, Kind.Event or Kind.Pressure, but got {Describe(v)}", a.Value.At); break;
					case "date": fields["date"] = v is DateV dv ? dv.ToString() : throw new CampError($"date is date(year, month, day), but got {Describe(v)}", a.Value.At); break;
					case "window": fields["window_days"] = Dayz(v, a); break;
					case "lead": fields["lead_days"] = Dayz(v, a); break;
					case "delay": fields["delay_days"] = Dayz(v, a); break;
					case "repeat": fields["repeat_days"] = Dayz(v, a); break;
					case "rumour":
						fields["rumour"] = v is string rs && rs.Length > 0 ? rs : throw new CampError($"rumour is text, but got {Describe(v)}", a.Value.At);
						break;
					case "keywords":
						fields["keywords"] = v is ArrV { Items: var kw } && kw.All(x => x is string) ? kw.ToList() : throw new CampError($"keywords is a list of text, e.g. [\"war\", \"Red River\"], but got {Describe(v)}", a.Value.At);
						break;
					default:
						throw new CampError($"a beat has no '{a.Name}'.{Suggest(a.Name, BeatArgs)} Its header takes {string.Join(", ", BeatArgs)}", a.NameAt);
				}
			}
			if (b.Requires.Count > 0)
				fields["requires"] = b.Requires.Select(r =>
				{
					var v = Eval(r, env);
					return v is BeatV bv ? bv.Id : v is string s ? s : throw new CampError($"requires names beats, but got {Describe(v)}", r.At);
				}).Cast<object>().ToList();

			var hooks = new HashSet<string>();
			foreach (var m in b.Members)
			{
				switch (m)
				{
					case ConstDecl c:
						{
							var v = Eval(c.Value, env);
							CheckType(c.Type, v, c.Value.At, $"const {c.Name}");
							env.Set(c.Name, v, c.Type);
							break;
						}
					case VarDecl v: throw new CampError($"variable {v.Name} belongs at the top of the campaign, where every beat can see it", v.At);
					case FuncDecl f:
						if (_localFuncs.ContainsKey(f.Name)) throw new CampError($"{f.Name} is declared twice in this beat", f.At);
						_localFuncs[f.Name] = f;
						break;
					case HookDecl h:
						{
							if (!hooks.Add(h.Name)) throw new CampError($"{h.Name} is written twice in beat {b.Name}", h.At);
							var (_, key, cond) = HookMap.First(x => x.hook == h.Name);
							if (cond)
							{
								if (h.ExprBody == null) throw new CampError($"{h.Name} is a condition: write {h.Name}() => condition;", h.At);
								var list = CondList(h.ExprBody, env);
								if (list.Count > 0) fields[key] = list;
							}
							else
							{
								var outp = new List<object>();
								if (h.Block != null) ExecBlock(h.Block, new Env(env), outp);
								else Exec(new ExprStmt { At = h.ExprBody.At, Value = h.ExprBody }, env, outp);
								if (outp.Count > 0) fields[key] = outp;
							}
							break;
						}
				}
			}

			var beat = Tree.Dict(("id", inst.Id), ("title", title));
			string derived = BeatKinds.Derive(fields);
			beat["kind"] = explicitKind ?? derived;
			foreach (var key in new[] { "date", "window_days", "lead_days", "delay_days", "repeat_days", "requires", "keywords", "rumour", "when", "on_awaken", "pressure", "activate_when", "happens", "done_when", "on_resolve", "cancel_when", "on_cancel" })
				if (fields.TryGetValue(key, out var fv)) beat[key] = fv;
			return beat;
		}
		finally { _localFuncs = saveFuncs; }
	}

	private static readonly string[] BeatArgs = { "id", "title", "kind", "date", "window", "lead", "delay", "repeat", "keywords", "rumour" };

	private double Dayz(object v, Arg a) => v is DaysV d && d.N >= 0 && d.N == Math.Floor(d.N) ? d.N : throw new CampError($"{a.Name} is a number of days, e.g. days(45), but got {Describe(v)}", a.Value.At);

	// ---------------------------------------------------------------- conditions

	private List<object> CondList(Expr e, Env env)
	{
		var saved = _out; _out = null;
		try
		{
			var parts = new List<Expr>();
			if (e is BinaryExpr { Op: "&&", Paren: false }) Flatten(e, "&&", parts); else parts.Add(e);
			return parts.Select(p => ToCond(Eval(p, env), p)).ToList();
		}
		finally { _out = saved; }
	}

	private static void Flatten(Expr e, string op, List<Expr> into)
	{
		if (e is BinaryExpr b && b.Op == op && !b.Paren) { Flatten(b.Left, op, into); Flatten(b.Right, op, into); }
		else into.Add(e);
	}

	private object ToCond(object v, Expr at)
	{
		switch (v)
		{
			case bool b: return b;
			case CondV c: return c.Json;
			case MetricV or VarV: throw new CampError("this is a number, not a condition: compare it (for example > 10)", at.At);
			case QueryV q: throw new CampError($"compare this with a name (== Season.Winter)", at.At);
			case DateQ: throw new CampError("compare today's date with a date (world.date >= date(1816, 6, 19))", at.At);
		}
		throw new CampError($"expected a condition, but got {Describe(v)}", at.At);
	}

	// ---------------------------------------------------------------- statements

	private void ExecBlock(BlockStmt b, Env env, List<object> outp)
	{
		foreach (var s in b.Body) Exec(s, env, outp);
	}

	private void Exec(Stmt s, Env env, List<object> outp)
	{
		var saved = _out; _out = outp;
		try
		{
			switch (s)
			{
				case EmptyStmt: return;
				case BlockStmt b: ExecBlock(b, new Env(env), outp); return;
				case LocalStmt l:
					{
						var v = Eval(l.Value, env);
						CheckType(l.Type, v, l.Value.At, l.Name);
						env.Set(l.Name, v, l.Type);
						return;
					}
				case ExprStmt es:
					{
						var v = Eval(es.Value, env);
						if (v is not Unit) throw new CampError($"this does nothing: it only works out {Describe(v)}. An effect looks like flags.set(\"x\"), mood += 1, trigger(Beat), news(...)", es.Value.At);
						return;
					}
				case AssignStmt a: outp.Add(Assign(a, env)); return;
				case IfStmt i:
					{
						var cv = Eval(i.Cond, env);
						if (cv is bool cb)
						{
							var branch = cb ? i.Then : i.Else;
							if (branch != null) Exec(branch, new Env(env), outp);
							return;
						}
						var node = Tree.Dict(("do", "if"), ("cond", ToCond(cv, i.Cond)));
						var then = new List<object>();
						Exec(i.Then, new Env(env), then);
						node["then"] = then;
						if (i.Else != null) { var els = new List<object>(); Exec(i.Else, new Env(env), els); node["else"] = els; }
						outp.Add(node);
						return;
					}
				case ForStmt f:
					foreach (var k in Counting(f, env)) { var inner = new Env(env); inner.Set(f.Var, k); Exec(f.Body, inner, outp); }
					return;
				case ForeachStmt f:
					foreach (var k in Items(f, env)) { var inner = new Env(env); inner.Set(f.Var, k); Exec(f.Body, inner, outp); }
					return;
			}
			throw new CampError("this statement is not part of the language", s.At);
		}
		finally { _out = saved; }
	}

	private object Assign(AssignStmt a, Env env)
	{
		if (a.Target is not NameExpr n) throw new CampError("only a campaign variable can be assigned to", a.Target.At);
		if (env.Has(n.Name)) throw new CampError($"{n.Name} is a const and cannot change", a.Target.At);
		if (!_vars.ContainsKey(n.Name)) throw new CampError($"no variable called {n.Name}.{Suggest(n.Name, _vars.Keys)} Declare it at the top: int {n.Name};", a.Target.At);
		double Num(Expr e) => Eval(e, env) is double d ? d : throw new CampError("a variable changes by a number known when the script is compiled", e.At);
		switch (a.Op)
		{
			case "++": return Tree.Dict(("do", "add"), ("var", n.Name));
			case "--": return Tree.Dict(("do", "add"), ("var", n.Name), ("by", -1.0));
			case "+=": return Tree.Dict(("do", "add"), ("var", n.Name), ("by", Num(a.Value)));
			case "-=": return Tree.Dict(("do", "add"), ("var", n.Name), ("by", -Num(a.Value)));
			case "=":
				if (a.Value is BinaryExpr { Op: "+" or "-" } be)
				{
					if (be.Left is NameExpr l && l.Name == n.Name) return Tree.Dict(("do", "add"), ("var", n.Name), ("by", be.Op == "+" ? Num(be.Right) : -Num(be.Right)));
					if (be.Op == "+" && be.Right is NameExpr r && r.Name == n.Name) return Tree.Dict(("do", "add"), ("var", n.Name), ("by", Num(be.Left)));
				}
				return Tree.Dict(("do", "set"), ("var", n.Name), ("to", Num(a.Value)));
		}
		throw new CampError($"{a.Op} is not supported on a campaign variable (use =, +=, -=, ++, --)", a.At);
	}

	internal void RunLambda(LambdaV l, List<object> outp)
	{
		var saved = _out; _out = outp;
		try
		{
			if (l.Block != null) ExecBlock(l.Block, new Env(l.Env), outp);
			else
			{
				var v = Eval(l.Body, l.Env);
				if (v is not Unit) throw new CampError($"this does nothing: it only works out {Describe(v)}", l.Body.At);
			}
		}
		finally { _out = saved; }
	}

	// ---------------------------------------------------------------- expressions

	private Ctx Mk(Span at, Env env) => new() { C = this, Out = _out, At = at, Env = env };

	internal object Eval(Expr e, Env env)
	{
		switch (e)
		{
			case LitExpr l: return l.Value is long n ? (double)n : l.Value;
			case NameExpr nm: return Resolve(nm, env);
			case LambdaExpr lam: return new LambdaV(lam.Body, lam.Block, env);
			case ArrayExpr arr: return new ArrV(arr.Items.Select(x => Eval(x, env)).ToList());
			case RangeExpr r:
				{
					double a = Whole(Eval(r.From, env), r.From), b = Whole(Eval(r.To, env), r.To);
					if (b - a > 5000) throw new CampError("this range has more than 5000 numbers", r.At);
					var list = new List<object>();
					for (double i = a; i <= b; i++) list.Add(i);
					return new ArrV(list);
				}
			case IndexExpr ix:
				{
					var t = Eval(ix.Target, env);
					var i = Eval(ix.Index, env);
					if (t is ArrV a && i is double d && d == Math.Floor(d) && d >= 0 && d < a.Items.Count) return a.Items[(int)d];
					throw new CampError(t is ArrV ? "that position is not in the list" : $"{Describe(t)} cannot be indexed", ix.At);
				}
			case TernaryExpr t:
				{
					var c = Eval(t.Cond, env);
					if (c is bool b) return Eval(b ? t.Then : t.Else, env);
					throw new CampError("a ? : needs a condition known when the script is compiled; use if for the game's own conditions", t.At);
				}
			case UnaryExpr u: return Unary(u, env);
			case BinaryExpr b: return Binary(b, env);
			case MemberExpr m: return Member(m, env);
			case CallExpr c: return Call(c, env);
		}
		throw new CampError("this cannot be evaluated", e.At);
	}

	private object Resolve(NameExpr n, Env env)
	{
		if (env.TryGet(n.Name, out var v)) return v;
		if (_steps != null && _steps.TryGetValue(n.Name, out var stepId)) return new StepV(n.Name, stepId);
		if (_questIds.TryGetValue(n.Name, out var questId)) return new QuestV(n.Name, questId);
		if (_vars.ContainsKey(n.Name)) return new VarV(n.Name);
		if (_byName.TryGetValue(n.Name, out var id))
			return id != null ? new BeatV(n.Name, id) : throw new CampError($"more than one beat is called {n.Name}; use its id as a string", n.At);
		// beats not yet numbered (a reference before the beat is declared, during Declare)
		var inst = _insts.FirstOrDefault(i => i.Decl.Name == n.Name);
		if (inst != null) return new BeatV(n.Name, inst.Id);
		if (n.Name == "world" || n.Name == "flags" || n.Name == "player") return new ObjV(n.Name);
		if (Library.IsEnum(n.Name)) return new TypeNameV(n.Name);
		var forward = _ast.Decls.OfType<BeatDecl>().FirstOrDefault(x => x.Name == n.Name);
		if (forward != null) return new BeatV(n.Name, forward.Args.FirstOrDefault(a => a.Name == "id")?.Value is LitExpr { Value: string fid } ? fid : n.Name);
		var cands = env.Names().Concat(_vars.Keys).Concat(_byName.Keys).Concat(_insts.Select(i => i.Decl.Name)).Concat(new[] { "world", "flags", "player" }).Concat(Library.EnumDocs.Keys);
		throw new CampError($"'{n.Name}' is not defined.{Suggest(n.Name, cands)}", n.At);
	}

	private object Member(MemberExpr m, Env env)
	{
		var tv = Eval(m.Target, env);
		if (tv is TypeNameV tn)
		{
			if (Library.EnumValues(tn.Name).Contains(m.Name)) return new EnumV(tn.Name, m.Name);
			throw new CampError($"{tn.Name} has no '{m.Name}'.{Suggest(m.Name, Library.EnumValues(tn.Name))} It has {string.Join(", ", Library.EnumValues(tn.Name).Take(12))}{(Library.EnumValues(tn.Name).Count() > 12 ? ", ..." : "")}", m.NameAt);
		}
		string owner = OwnerOf(tv, m.Target);
		var mem = Library.Find(owner, m.Name) ?? throw new CampError($"{Describe(tv)} has no '{m.Name}'.{Suggest(m.Name, Library.Of(owner).Select(x => x.Name))} It has {string.Join(", ", Library.Of(owner).Select(x => x.Name))}", m.NameAt);
		if (mem.Kind == "function") throw new CampError($"{mem.Full} is a function: write {mem.Full}(...)", m.NameAt);
		return mem.Run(Mk(m.At, env), tv, new Args());
	}

	private string OwnerOf(object tv, Expr at) => tv switch
	{
		ObjV o => o.Type, BeatV => "Beat", BrigadeV => "Brigade", QuestV => "Quest",
		_ => throw new CampError($"{Describe(tv)} has no members", at.At),
	};

	private object Call(CallExpr c, Env env)
	{
		if (c.Callee is NameExpr n)
		{
			if (_localFuncs.TryGetValue(n.Name, out var lf) || _funcs.TryGetValue(n.Name, out lf)) return CallUser(lf, c, env);
			var g = Library.Find("", n.Name);
			if (g == null || g.Kind != "function")
				throw new CampError($"there is no function called {n.Name}.{Suggest(n.Name, Library.Of("").Where(x => x.Kind == "function").Select(x => x.Name).Concat(_funcs.Keys).Concat(_localFuncs.Keys))}", n.At);
			return g.Run(Mk(c.At, env), null, BindArgs(g, c.Args, env, c));
		}
		if (c.Callee is MemberExpr m)
		{
			var tv = Eval(m.Target, env);
			string owner = OwnerOf(tv, m.Target);
			var mem = Library.Find(owner, m.Name) ?? throw new CampError($"{Describe(tv)} has no '{m.Name}'.{Suggest(m.Name, Library.Of(owner).Select(x => x.Name))} It has {string.Join(", ", Library.Of(owner).Select(x => x.Name))}", m.NameAt);
			if (mem.Kind != "function") throw new CampError($"{mem.Full} is not a function: leave off the ()", m.NameAt);
			return mem.Run(Mk(c.At, env), tv, BindArgs(mem, c.Args, env, c));
		}
		throw new CampError("only a function can be called", c.At);
	}

	private Args BindArgs(Member m, List<Arg> args, Env env, CallExpr at)
	{
		var a = new Args();
		var ps = m.Params;
		int pos = 0;
		foreach (var arg in args)
		{
			var val = Eval(arg.Value, env);
			if (arg.Name == null)
			{
				if (pos >= ps.Length) throw new CampError($"{m.Full} takes {(ps.Length == 0 ? "no arguments" : $"{ps.Length} argument{(ps.Length == 1 ? "" : "s")}")}: {m.Signature}", arg.Value.At);
				var p = ps[pos];
				if (p.Rest) { a.Rest.Add(Coerce(p.Type, val, arg.Value.At, m, p)); continue; }
				pos++;
				if (a.V.ContainsKey(p.Name)) throw new CampError($"{p.Name} is given twice", arg.Value.At);
				a.V[p.Name] = Coerce(p.Type, val, arg.Value.At, m, p);
			}
			else
			{
				var p = ps.FirstOrDefault(x => x.Name == arg.Name && !x.Rest);
				if (p == null)
				{
					if (!m.OpenNamed) throw new CampError($"{m.Full} has no argument '{arg.Name}'.{Suggest(arg.Name, ps.Select(x => x.Name))} It is {m.Signature}", arg.NameAt);
					a.Extra[arg.Name] = val;
					continue;
				}
				if (a.V.ContainsKey(p.Name)) throw new CampError($"{p.Name} is given twice", arg.NameAt);
				a.V[p.Name] = Coerce(p.Type, val, arg.Value.At, m, p);
			}
		}
		foreach (var p in ps)
			if (!p.Optional && !p.Rest && !a.V.ContainsKey(p.Name))
				throw new CampError($"{m.Full} needs {p.Name}: {m.Signature}", at.At);
		return a;
	}

	private object Coerce(string type, object v, Span at, Member m, LibParam p)
	{
		if (Conforms(type, v)) return v;
		throw new CampError($"{m.Full}: {p.Name} should be {Article(type)}, but got {Describe(v)}", at);
	}

	private static string Article(string type) => type switch
	{
		"int" => "a whole number", "float" => "a number", "string" => "text", "bool" => "a condition", "Date" => "a date, date(year, month, day)",
		"Days" => "a length of time, days(n)", "Beat" => "a beat", "Lambda" => "a () => ... block", "Option" => "an option(weight, () => ...)",
		"Company" => "Company.HBC, Company.NWC or Company.All", "Person" => "a Person (Person.Factor ...) or text", "Item" => "an Item (Item.Rum ...)",
		"Season" => "a Season", "Kind" => "a Kind", "Brigade" => "a brigade(\"name\")", _ => type,
	};

	private static bool Conforms(string type, object v) => type switch
	{
		"any" or "var" => true,
		"int" => v is double d && d == Math.Floor(d),
		"float" or "number" => v is double,
		"string" => v is string,
		"bool" => v is bool or CondV,
		"Date" => v is DateV,
		"Days" => v is DaysV,
		"Beat" => v is BeatV or string,
		"Brigade" => v is BrigadeV,
		"Lambda" => v is LambdaV,
		"Option" => v is OptionV,
		"Person" => v is string or EnumV { Type: "Person" },
		"Company" or "Item" or "Season" or "Kind" => v is EnumV e && e.Type == type,
		"string[]" => v is ArrV a && a.Items.All(x => x is string),
		"int[]" => v is ArrV a2 && a2.Items.All(x => x is double d2 && d2 == Math.Floor(d2)),
		_ => false,
	};

	private void CheckType(string type, object v, Span at, string what)
	{
		if (type == "var") return;
		if (!ValueTypes.Contains(type)) throw new CampError($"'{type}' is not a type.{Suggest(type, ValueTypes)} The types are {string.Join(", ", ValueTypes)}", at);
		if (!Conforms(type, v)) throw new CampError($"{what} is declared {type}, but its value is {Describe(v)}", at);
	}

	private object CallUser(FuncDecl f, CallExpr c, Env env)
	{
		var inner = new Env(_globals);
		var bound = new Dictionary<string, object>();
		int pos = 0;
		foreach (var arg in c.Args)
		{
			var val = Eval(arg.Value, env);
			Param p;
			if (arg.Name == null)
			{
				if (pos >= f.Params.Count) throw new CampError($"{f.Name} takes {f.Params.Count} argument{(f.Params.Count == 1 ? "" : "s")}", arg.Value.At);
				p = f.Params[pos++];
			}
			else p = f.Params.FirstOrDefault(x => x.Name == arg.Name) ?? throw new CampError($"{f.Name} has no argument '{arg.Name}'.{Suggest(arg.Name, f.Params.Select(x => x.Name))}", arg.NameAt);
			if (bound.ContainsKey(p.Name)) throw new CampError($"{p.Name} is given twice", arg.Value.At);
			CheckType(p.Type, val, arg.Value.At, $"{f.Name}: {p.Name}");
			bound[p.Name] = val;
		}
		foreach (var p in f.Params)
		{
			if (!bound.TryGetValue(p.Name, out var bv)) throw new CampError($"{f.Name} needs {p.Name} ({p.Type})", c.At);
			inner.Set(p.Name, bv, p.Type);
		}
		if (++_depth > 40) throw new CampError($"{f.Name} calls itself without end", c.At);
		try
		{
			if (f.ReturnType == "void")
			{
				if (_out == null) throw new CampError($"{f.Name} changes the world, so it belongs in an effect, not in a condition", c.At);
				if (f.Block != null) ExecBlock(f.Block, inner, _out);
				else
				{
					var v = Eval(f.ExprBody, inner);
					if (v is not Unit) throw new CampError($"{f.Name} is void but its body gives back {Describe(v)}", f.ExprBody.At);
				}
				return new Unit();
			}
			if (f.ExprBody == null) throw new CampError($"a function that gives back a value is written with =>: {f.ReturnType} {f.Name}(...) => value;", f.At);
			var r = Eval(f.ExprBody, inner);
			CheckType(f.ReturnType, r, f.ExprBody.At, $"{f.Name}'s result");
			return r;
		}
		finally { _depth--; }
	}

	// ---------------------------------------------------------------- operators

	private object Unary(UnaryExpr u, Env env)
	{
		var x = Eval(u.Operand, env);
		if (u.Op == "-") return x is double d ? -d : throw new CampError($"cannot negate {Describe(x)}", u.At);
		if (x is bool b) return !b;
		return new CondV(Tree.Dict(("not", ToCond(x, u.Operand))));
	}

	private object Binary(BinaryExpr b, Env env)
	{
		if (b.Op is "&&" or "||")
		{
			var parts = new List<Expr>();
			Flatten(b.Left, b.Op, parts);
			Flatten(b.Right, b.Op, parts);
			var items = new List<object>();
			foreach (var p in parts)
			{
				var v = ToCond(Eval(p, env), p);
				if (v is bool bv)
				{
					if (b.Op == "&&" && !bv) return false;
					if (b.Op == "||" && bv) return true;
					continue;
				}
				items.Add(v);
			}
			if (items.Count == 0) return b.Op == "&&";
			if (items.Count == 1) return new CondV(items[0]);
			return new CondV(Tree.Dict((b.Op == "&&" ? "all" : "any", items)));
		}
		var l = Eval(b.Left, env);
		var r = Eval(b.Right, env);
		switch (b.Op)
		{
			case "+":
				if (l is double a1 && r is double a2) return a1 + a2;
				if (l is DateV d && r is DaysV dd) return DateV.From(d.ToDateTime().AddDays(dd.N));
				if (l is DaysV x1 && r is DaysV x2) return new DaysV(x1.N + x2.N);
				if (l is string || r is string) return CJson.Join(l) + CJson.Join(r);
				break;
			case "-":
				if (l is double s1 && r is double s2) return s1 - s2;
				if (l is DateV d2 && r is DaysV dd2) return DateV.From(d2.ToDateTime().AddDays(-dd2.N));
				if (l is DaysV y1 && r is DaysV y2) return new DaysV(y1.N - y2.N);
				break;
			case "*":
				if (l is double m1 && r is double m2) return m1 * m2;
				if (l is DaysV dl && r is double mr) return new DaysV(dl.N * mr);
				break;
			case "/":
				if (l is double q1 && r is double q2) return q2 == 0 ? throw new CampError("division by zero", b.At) : q1 / q2;
				break;
			case "%":
				if (l is double p1 && r is double p2) return p2 == 0 ? throw new CampError("division by zero", b.At) : p1 % p2;
				break;
			case "==" or "!=" or "<" or ">" or "<=" or ">=": return Compare(b, l, r);
		}
		throw new CampError($"cannot use {b.Op} with {Describe(l)} and {Describe(r)}", b.At);
	}

	private static string Flip(string op) => op switch { "<" => ">", ">" => "<", "<=" => ">=", ">=" => "<=", _ => op };

	private object Compare(BinaryExpr b, object l, object r)
	{
		string op = b.Op;
		bool IsLive(object x) => x is MetricV or VarV or QueryV or DateQ;
		if (!IsLive(l) && !IsLive(r))
		{
			int cmp;
			if (l is double x1 && r is double x2) cmp = x1.CompareTo(x2);
			else if (l is string s1 && r is string s2) cmp = string.CompareOrdinal(s1, s2);
			else if (l is DateV d1 && r is DateV d2) cmp = d1.CompareTo(d2);
			else if (l is bool && r is bool || l is EnumV && r is EnumV || l is BeatV && r is BeatV) cmp = Equals(l, r) ? 0 : 1;
			else throw new CampError($"cannot compare {Describe(l)} with {Describe(r)}", b.At);
			if (op is not ("==" or "!=") && (l is bool or EnumV or BeatV)) throw new CampError($"{op} only works on numbers, text and dates", b.At);
			return op switch { "==" => cmp == 0, "!=" => cmp != 0, "<" => cmp < 0, ">" => cmp > 0, "<=" => cmp <= 0, _ => cmp >= 0 };
		}
		if (!IsLive(l)) { (l, r) = (r, l); op = Flip(op); }
		if (IsLive(r)) throw new CampError("the game can compare a number it knows with a fixed number, not with another changing number", b.At);
		switch (l)
		{
			case QueryV q:
				{
					string want = q.Pred == "season" ? "Season" : "Company";
					if (r is not EnumV ev || ev.Type != want) throw new CampError($"{(q.Pred == "season" ? "world.season" : "world.playerFaction")} is compared with {(want == "Season" ? "Season.Winter and so on" : "Company.HBC or Company.NWC")}", b.At);
					if (op is not ("==" or "!=")) throw new CampError($"a name is compared with == or != only", b.At);
					var p = CJson.Predicate(q.Pred, new() { ev.Value });
					return new CondV(op == "==" ? p : Tree.Dict(("not", p)));
				}
			case DateQ:
				{
					if (r is not DateV dv) throw new CampError("today's date is compared with a date: date(1816, 6, 19)", b.At);
					var next = DateV.From(dv.ToDateTime().AddDays(1)).ToString();
					return op switch
					{
						">=" => new CondV(CJson.Predicate("after", new() { dv.ToString() })),
						"<" => new CondV(CJson.Predicate("before", new() { dv.ToString() })),
						">" => new CondV(CJson.Predicate("after", new() { next })),
						"<=" => new CondV(CJson.Predicate("before", new() { next })),
						_ => throw new CampError("today's date is compared with <, <=, > or >= (a day is a range, not a point)", b.At),
					};
				}
			case MetricV m:
				return new CondV(CJson.Metric(m.Name, m.Args, op, r is double n ? n : throw new CampError($"compare with a number, not {Describe(r)}", b.At)));
			case VarV v:
				return new CondV(CJson.Metric("var", new() { v.Name }, op, r is double n2 ? n2 : throw new CampError($"compare with a number, not {Describe(r)}", b.At)));
		}
		throw new CampError("this cannot be compared", b.At);
	}

	// ---------------------------------------------------------------- words

	internal static string Describe(object v) => v switch
	{
		double d => $"the number {CJson.Num(d)}", string s => $"the text \"{s}\"", bool b => $"{(b ? "true" : "false")}", DateV d => $"the date {d}",
		DaysV d => $"{CJson.Num(d.N)} days", BeatV b => $"the beat {b.Name}", BrigadeV b => $"the brigade \"{b.Run}\"", EnumV e => $"{e.Type}.{e.Value}",
		QuestV q => $"the quest {q.Name}", StepV st => $"the step {st.Name}",
		ObjV o => o.Type, TypeNameV t => $"the type {t.Name}", VarV vv => $"the variable {vv.Name}", MetricV m => "a number from the world",
		QueryV => "a name from the world", DateQ => "today's date", CondV => "a condition", Unit => "an effect", LambdaV => "a block", OptionV => "an option",
		ArrV => "a list", null => "nothing", _ => v.ToString(),
	};

	internal static string Suggest(string word, IEnumerable<string> candidates)
	{
		string best = null;
		int bd = int.MaxValue;
		foreach (var c in candidates.Distinct())
		{
			int d = CampParser.Distance(word.ToLowerInvariant(), c.ToLowerInvariant());
			if (d < bd) { bd = d; best = c; }
		}
		return best != null && bd <= Math.Max(2, word.Length / 3) ? $" Did you mean '{best}'?" : "";
	}
}
