using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CampaignTool;

// What an expression is worth while a script is being compiled. Plain numbers,
// strings and bools are .NET's own (double, string, bool); the rest are these.
public readonly record struct DateV(int Y, int M, int D) : IComparable<DateV>
{
	public override string ToString() => $"{Y:D4}-{M:D2}-{D:D2}";
	public DateTime ToDateTime() => new(Y, M, D);
	public static DateV From(DateTime d) => new(d.Year, d.Month, d.Day);
	public int CompareTo(DateV o) => ToDateTime().CompareTo(o.ToDateTime());
	public static bool TryParse(string s, out DateV d)
	{
		d = default;
		var m = Regex.Match(s, @"^(\d{4})-(\d\d)-(\d\d)$");
		if (!m.Success) return false;
		int y = int.Parse(m.Groups[1].Value), mo = int.Parse(m.Groups[2].Value), da = int.Parse(m.Groups[3].Value);
		if (mo < 1 || mo > 12 || da < 1 || da > DateTime.DaysInMonth(y, mo)) return false;
		d = new DateV(y, mo, da);
		return true;
	}
}

public readonly record struct DaysV(double N);
public sealed record BeatV(string Name, string Id);
public sealed record BrigadeV(string Run);
public sealed record EnumV(string Type, string Value);
public sealed record ObjV(string Type);                 // world, flags
public sealed record TypeNameV(string Name);            // Company, Person, Item ...
public sealed record VarV(string Name);                 // a script variable
public sealed record MetricV(string Name, List<string> Args);   // a number the world is asked
public sealed record QueryV(string Pred);               // season, playerFaction: compared with a name
public sealed record DateQ;                             // world.date
public sealed record CondV(object Json);                // a condition, in the game's own form
public sealed record Unit;                              // what an action gives back
public sealed record LambdaV(Expr Body, BlockStmt Block, Env Env);
public sealed record OptionV(double Weight, LambdaV Then);
public sealed record ArrV(List<object> Items);

public sealed class Env
{
	private readonly Dictionary<string, (object val, string type)> _v = new();
	private readonly Env _parent;
	public Env(Env parent = null) { _parent = parent; }
	public void Set(string k, object v, string type = null) => _v[k] = (v, type);
	public bool Has(string k) { for (var e = this; e != null; e = e._parent) if (e._v.ContainsKey(k)) return true; return false; }
	public bool TryGet(string k, out object v)
	{
		for (var e = this; e != null; e = e._parent) if (e._v.TryGetValue(k, out var t)) { v = t.val; return true; }
		v = null;
		return false;
	}
	public IEnumerable<string> Names() { for (var e = this; e != null; e = e._parent) foreach (var k in e._v.Keys) yield return k; }
}

// The game's JSON forms for conditions, kept in one place so the compiler and
// the decompiler cannot disagree.
public static class CJson
{
	public static string Num(double d) => Tree.Num(d);

	public static object Predicate(string name, List<string> args)
	{
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

	// How a value reads when joined to a string.
	public static string Join(object v) => v switch
	{
		double d => Tree.Num(d), bool b => b ? "true" : "false", string s => s, DateV d => d.ToString(),
		EnumV e => e.Value, BeatV b => b.Id, null => "", _ => v.ToString(),
	};
}
