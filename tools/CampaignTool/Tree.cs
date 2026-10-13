using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace CampaignTool;

// Campaign scripts as plain data: Dictionary<string, object>, List<object>,
// string, double, bool, null - the same tree the game's CampaignLogic reads.
public static class Tree
{
	public static Dictionary<string, object> Dict(params (string, object)[] kv)
	{
		var d = new Dictionary<string, object>();
		foreach (var (k, v) in kv) d[k] = v;
		return d;
	}

	public static List<object> List(params object[] xs) => new(xs);

	public static object Read(JsonElement e)
	{
		switch (e.ValueKind)
		{
			case JsonValueKind.Object: { var d = new Dictionary<string, object>(); foreach (var p in e.EnumerateObject()) d[p.Name] = Read(p.Value); return d; }
			case JsonValueKind.Array: { var l = new List<object>(); foreach (var x in e.EnumerateArray()) l.Add(Read(x)); return l; }
			case JsonValueKind.String: return e.GetString();
			case JsonValueKind.True: return true;
			case JsonValueKind.False: return false;
			case JsonValueKind.Number: return e.GetDouble();
			default: return null;
		}
	}

	public static object Parse(string json)
	{
		using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
		return Read(doc.RootElement);
	}

	public static Dictionary<string, object> ParseObject(string json) => Parse(json) as Dictionary<string, object> ?? throw new InvalidDataException("not a JSON object");

	// A string the way JSON (and the language) writes it: quotes, escapes, no \u for ordinary letters.
	public static string Quote(string s) => JsonSerializer.Serialize(s, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

	public static string Num(double d)
	{
		if (d == Math.Floor(d) && Math.Abs(d) < 1e15) return ((long)d).ToString(CultureInfo.InvariantCulture);
		return d.ToString("R", CultureInfo.InvariantCulture);
	}

	// Indented JSON, two spaces, keys in insertion order, whole numbers without a point.
	public static string Write(object o, int indent = 0)
	{
		var sb = new StringBuilder();
		Emit(sb, o, indent);
		return sb.ToString();
	}

	private static void Emit(StringBuilder sb, object o, int level)
	{
		string pad = new(' ', level * 2), inner = new(' ', (level + 1) * 2);
		switch (o)
		{
			case null: sb.Append("null"); break;
			case bool b: sb.Append(b ? "true" : "false"); break;
			case double d: sb.Append(Num(d)); break;
			case int i: sb.Append(i); break;
			case string s: sb.Append(Quote(s)); break;
			case List<object> l:
				if (l.Count == 0) { sb.Append("[]"); break; }
				sb.Append("[\n");
				for (int k = 0; k < l.Count; k++) { sb.Append(inner); Emit(sb, l[k], level + 1); sb.Append(k + 1 < l.Count ? ",\n" : "\n"); }
				sb.Append(pad).Append(']');
				break;
			case Dictionary<string, object> d:
				if (d.Count == 0) { sb.Append("{}"); break; }
				sb.Append("{\n");
				int n = 0;
				foreach (var kv in d) { sb.Append(inner).Append(Quote(kv.Key)).Append(": "); Emit(sb, kv.Value, level + 1); sb.Append(++n < d.Count ? ",\n" : "\n"); }
				sb.Append(pad).Append('}');
				break;
			default: sb.Append(Quote(o.ToString())); break;
		}
	}

	// One line, no indentation.
	public static string Compact(object o) => o switch
	{
		null => "null", bool b => b ? "true" : "false", double d => Num(d), int i => i.ToString(), string s => Quote(s),
		List<object> l => "[" + string.Join(", ", l.Select(Compact)) + "]",
		Dictionary<string, object> d => "{" + string.Join(", ", d.Select(kv => Quote(kv.Key) + ": " + Compact(kv.Value))) + "}",
		_ => Quote(o.ToString()),
	};

	// Structural equality, whole numbers equal however they were written.
	public static bool Same(object a, object b)
	{
		switch (a)
		{
			case null: return b == null;
			case double x: return b is double y && Math.Abs(x - y) < 1e-9;
			case string s: return b is string t && s == t;
			case bool p: return b is bool q && p == q;
			case List<object> l:
				if (b is not List<object> m || l.Count != m.Count) return false;
				for (int i = 0; i < l.Count; i++) if (!Same(l[i], m[i])) return false;
				return true;
			case Dictionary<string, object> d:
				if (b is not Dictionary<string, object> e || d.Count != e.Count) return false;
				foreach (var kv in d) if (!e.TryGetValue(kv.Key, out var v) || !Same(kv.Value, v)) return false;
				return true;
		}
		return false;
	}

	public static List<object> AsList(object x) => x == null ? new List<object>() : x is List<object> l ? l : new List<object> { x };
	public static string S(object o) => IsometricFurTradeGame.Scripts.Core.CampaignLogic.Str(o);
	public static double N(object o) => IsometricFurTradeGame.Scripts.Core.CampaignLogic.Num(o);
	public static object Get(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v : null;
}
