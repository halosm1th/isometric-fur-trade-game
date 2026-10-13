using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IsometricFurTradeGame.Scripts.Core;

// The campaign's language (see tools/CampaignTool/README.md): conditions
// and actions as plain data, evaluated here with nothing of the engine in
// sight, so the same cases can be run by a unit test and by the Python
// authoring tool (tools/CampaignTool, tests/CampaignTests) all use it.
//
// A condition is
//   "flag:x"  "!flag:x"  "tension>=40"  "stock(Cumberland House, TradeGoods) < 10"   (the compact string)
//   { "all": [c...] }  { "any": [c...] }  { "not": c }
//   { "p": "convoy_late", "a": ["the York boats"] }                  a fact
//   { "m": "stock", "a": ["York Factory", "Rum"], "op": "<", "v": 10 }  a number compared
// A list of conditions means all of them.
//
// An action is { "do": "name", ... }. The language owns flag, unflag, set,
// add, if, random, after and trigger; anything else is the world's to carry out.
public interface ICampaignWorld
{
	// A number the world knows (tension, a post's stock...). NaN if it knows no such thing.
	double Metric(string name, string[] args);
	// A fact the world knows (a post stands, a man is dead...).
	bool Predicate(string name, string[] args);
	// Carry out one action the language does not own.
	void Effect(string name, Dictionary<string, object> action);
}

public sealed class CampaignTimer
{
	public int Day;
	public List<object> Actions = new();
}

// Everything the language remembers between days; saved with the world.
public sealed class CampaignState
{
	public int Today;
	public readonly HashSet<string> Flags = new();
	public readonly Dictionary<string, double> Vars = new();
	public readonly HashSet<string> Done = new();
	public readonly Dictionary<string, int> DoneDay = new();
	public readonly List<CampaignTimer> Timers = new();
	public readonly List<string> Forced = new();   // beats an action asked to happen now
	public uint Seed = 20240611;
}

public static class CampaignLogic
{
	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	// ---- days -----------------------------------------------------------------

	private static readonly DateTime Epoch = new(1700, 1, 1);
	public static int DayNumber(int y, int m, int d) => (new DateTime(Math.Clamp(y, 1700, 2100), Math.Clamp(m, 1, 12), 1).AddDays(Math.Max(0, d - 1)) - Epoch).Days;
	public static DateTime DateOf(int day) => Epoch.AddDays(day);
	public static int DayNumber(string iso)
	{
		var p = iso.Trim().Split('-');
		int y = p.Length > 0 && int.TryParse(p[0], out int a) ? a : 1811, m = p.Length > 1 && int.TryParse(p[1], out int b) ? b : 1, d = p.Length > 2 && int.TryParse(p[2], out int c) ? c : 1;
		return DayNumber(y, m, d);
	}

	// ---- the roll ----------------------------------------------------------------
	// A small linear generator, so a script simulated by tools/CampaignTool
	// simulated campaign rolls what the game will roll.

	public static double Roll(CampaignState st)
	{
		st.Seed = unchecked(st.Seed * 1664525u + 1013904223u);
		return st.Seed / 4294967296.0;
	}

	// ---- conditions -----------------------------------------------------------------

	private static readonly Regex Compare = new(@"^\s*([a-z_]+)\s*(?:\(([^)]*)\))?\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);
	private static readonly Regex Call = new(@"^\s*([a-z_]+)\s*\(([^)]*)\)\s*$", RegexOptions.Compiled);

	// Conditions that are really a flag the engine raises about a brigade.
	private static readonly Dictionary<string, string> ConvoyFlags = new()
	{
		["convoy_late"] = "late:", ["convoy_lost"] = "lost:", ["convoy_robbed"] = "robbed:",
		["convoy_turned_back"] = "turned:", ["convoy_out"] = "out:", ["convoy_in"] = "in:",
	};

	private static readonly HashSet<string> FlagPrefixes = new() { "heard", "late", "lost", "robbed", "turned", "out", "in" };

	public static bool AllIn(string[] xs, Func<string, bool> f) { foreach (var x in xs) if (!f(x)) return false; return true; }

	public static bool All(object conds, CampaignState st, ICampaignWorld w)
	{
		if (conds == null) return true;
		if (conds is List<object> list) { foreach (var c in list) if (!Eval(c, st, w)) return false; return true; }
		return Eval(conds, st, w);
	}

	public static bool Eval(object cond, CampaignState st, ICampaignWorld w)
	{
		switch (cond)
		{
			case null: return true;
			case bool b: return b;
			case string s: return EvalString(s, st, w);
			case List<object> list: return All(list, st, w);
			case Dictionary<string, object> d:
				if (d.TryGetValue("all", out var all)) return All(all, st, w);
				if (d.TryGetValue("any", out var any))
				{
					if (any is List<object> opts) { foreach (var c in opts) if (Eval(c, st, w)) return true; return false; }
					return Eval(any, st, w);
				}
				if (d.TryGetValue("not", out var not)) return !Eval(not, st, w);
				if (d.TryGetValue("p", out var p)) return Predicate(Str(p), Args(d), st, w);
				if (d.TryGetValue("m", out var m))
				{
					double v = Metric(Str(m), Args(d), st, w);
					return Compared(v, d.TryGetValue("op", out var op) ? Str(op) : ">=", d.TryGetValue("v", out var rhs) ? Num(rhs) : 0);
				}
				return false;
		}
		return false;
	}

	private static bool EvalString(string c, CampaignState st, ICampaignWorld w)
	{
		c = c.Trim();
		if (c.Length == 0) return true;
		if (c[0] == '!') return !EvalString(c[1..], st, w);
		var m = Compare.Match(c);
		if (m.Success)
			return Compared(Metric(m.Groups[1].Value, SplitArgs(m.Groups[2].Value), st, w), m.Groups[3].Value, double.Parse(m.Groups[4].Value, Inv));
		// tension>=40 written without a space is covered above; "name:arg" is a fact with one argument.
		int colon = c.IndexOf(':');
		// "heard:x", "late:the York boats": flags the engine raises, read as they always were.
		if (colon > 0 && FlagPrefixes.Contains(c[..colon].Trim())) return st.Flags.Contains(c);
		if (colon > 0) return Predicate(c[..colon].Trim(), new[] { c[(colon + 1)..].Trim() }, st, w);
		var call = Call.Match(c);
		if (call.Success) return Predicate(call.Groups[1].Value, SplitArgs(call.Groups[2].Value), st, w);
		return st.Flags.Contains(c);
	}

	private static bool Predicate(string name, string[] a, CampaignState st, ICampaignWorld w)
	{
		string arg = a.Length > 0 ? a[0] : "";
		switch (name)
		{
			case "flag": return st.Flags.Contains(arg);
			case "beat": return st.Done.Contains(arg);
			case "after": return st.Today >= DayNumber(arg);
			case "before": return st.Today < DayNumber(arg);
			case "chance": return Roll(st) * 100.0 < double.Parse(arg, Inv);
			case "month": return MonthIn(DateOf(st.Today).Month, arg);
		}
		if (ConvoyFlags.TryGetValue(name, out var prefix)) return st.Flags.Contains(prefix + arg);
		return w.Predicate(name, a);
	}

	// month:6 or month:6-8 (June to August) or month:11-3 (round the year's end)
	private static bool MonthIn(int month, string spec)
	{
		var p = spec.Split('-');
		int lo = int.Parse(p[0].Trim(), Inv), hi = p.Length > 1 ? int.Parse(p[1].Trim(), Inv) : lo;
		return lo <= hi ? month >= lo && month <= hi : month >= lo || month <= hi;
	}

	private static double Metric(string name, string[] a, CampaignState st, ICampaignWorld w)
	{
		string arg = a.Length > 0 ? a[0] : "";
		var date = DateOf(st.Today);
		switch (name)
		{
			case "var": return st.Vars.GetValueOrDefault(arg);
			case "month": return date.Month;
			case "year": return date.Year;
			case "day": return date.Day;
			case "day_of_year": return date.DayOfYear;
			case "count": return st.Vars.GetValueOrDefault("count:" + arg);
			// Days since a beat happened; a beat that has not is far in the future.
			case "beat_age": return st.DoneDay.TryGetValue(arg, out int d) ? st.Today - d : 99999;
		}
		return w.Metric(name, a);
	}

	private static bool Compared(double l, string op, double r) => op switch
	{
		">=" => l >= r, "<=" => l <= r, ">" => l > r, "<" => l < r, "==" => Math.Abs(l - r) < 1e-9, "!=" => Math.Abs(l - r) >= 1e-9, _ => false,
	};

	// ---- actions -------------------------------------------------------------------------

	public static void Run(object actions, CampaignState st, ICampaignWorld w, int depth = 0)
	{
		if (actions == null || depth > 12) return;
		if (actions is Dictionary<string, object> one) { RunOne(one, st, w, depth); return; }
		if (actions is not List<object> list) return;
		foreach (var a in list) if (a is Dictionary<string, object> d) RunOne(d, st, w, depth);
	}

	private static void RunOne(Dictionary<string, object> a, CampaignState st, ICampaignWorld w, int depth)
	{
		string name = a.TryGetValue("do", out var n) ? Str(n) : "";
		switch (name)
		{
			case "flag": st.Flags.Add(Interp(Str(a, "name"), st)); return;
			case "unflag": st.Flags.Remove(Interp(Str(a, "name"), st)); return;
			case "set": st.Vars[Str(a, "var")] = Num(a.GetValueOrDefault("to")); return;
			case "add": st.Vars[Str(a, "var")] = st.Vars.GetValueOrDefault(Str(a, "var")) + Num(a.GetValueOrDefault("by", 1.0)); return;
			case "if":
				Run(Eval(a.GetValueOrDefault("cond"), st, w) ? a.GetValueOrDefault("then") : a.GetValueOrDefault("else"), st, w, depth + 1);
				return;
			case "random":
				if (a.GetValueOrDefault("options") is not List<object> opts || opts.Count == 0) return;
				double total = 0;
				foreach (var o in opts) total += o is Dictionary<string, object> od ? Math.Max(0, Num(od.GetValueOrDefault("weight", 1.0))) : 0;
				double pick = Roll(st) * total;
				foreach (var o in opts)
				{
					if (o is not Dictionary<string, object> od) continue;
					pick -= Math.Max(0, Num(od.GetValueOrDefault("weight", 1.0)));
					if (pick < 0) { Run(od.GetValueOrDefault("do"), st, w, depth + 1); return; }
				}
				return;
			case "after":
				st.Timers.Add(new CampaignTimer { Day = st.Today + (int)Num(a.GetValueOrDefault("days", 1.0)), Actions = a.GetValueOrDefault("then") is List<object> l ? l : new List<object> { a.GetValueOrDefault("then") } });
				return;
			case "trigger": st.Forced.Add(Str(a, "beat")); return;
		}
		// The world's: its words get the numbers and names filled in first.
		var filled = new Dictionary<string, object>();
		foreach (var kv in a) filled[kv.Key] = kv.Value is string s ? Interp(s, st) : kv.Value;
		w.Effect(name, filled);
	}

	// Timers that have come due, taken off the list.
	public static List<object> Due(CampaignState st)
	{
		var due = new List<object>();
		for (int i = 0; i < st.Timers.Count; i++)
		{
			if (st.Timers[i].Day > st.Today) continue;
			due.Add(st.Timers[i].Actions);
			st.Timers.RemoveAt(i--);
		}
		return due;
	}

	// {var:name} and {count:beat} in a line of text become the number.
	public static string Interp(string s, CampaignState st)
	{
		if (s.IndexOf('{') < 0) return s;
		return Regex.Replace(s, @"\{(var|count):([^}]+)\}", m =>
		{
			string key = m.Groups[1].Value == "count" ? "count:" + m.Groups[2].Value : m.Groups[2].Value;
			return st.Vars.GetValueOrDefault(key).ToString("0.##", Inv);
		});
	}

	// ---- small helpers ----------------------------------------------------------------------

	public static string[] SplitArgs(string s)
	{
		if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
		var parts = s.Split(',');
		for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim().Trim('"');
		return parts;
	}

	private static string[] Args(Dictionary<string, object> d)
	{
		if (!d.TryGetValue("a", out var a)) return Array.Empty<string>();
		if (a is List<object> list) { var r = new string[list.Count]; for (int i = 0; i < r.Length; i++) r[i] = Str(list[i]); return r; }
		return new[] { Str(a) };
	}

	public static string Str(object o) => o switch { null => "", string s => s, double d => d.ToString("0.##", Inv), bool b => b ? "true" : "false", _ => o.ToString() };
	private static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? Str(v) : "";
	public static double Num(object o) => o switch { null => 0, double d => d, int i => i, long l => l, bool b => b ? 1 : 0, string s when double.TryParse(s, NumberStyles.Float, Inv, out var r) => r, _ => 0 };
}
