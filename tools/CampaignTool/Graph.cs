using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CampaignTool;

public static class Graph
{
	public static List<(string from, string to, string label)> Edges(Dictionary<string, object> doc, out List<Dictionary<string, object>> beats)
	{
		beats = Tree.AsList(doc.GetValueOrDefault("beats")).Cast<Dictionary<string, object>>().ToList();
		var edges = new List<(string, string, string)>();
		var sets = new Dictionary<string, SortedSet<string>>();
		var reads = new Dictionary<string, SortedSet<string>>();
		static void Add(Dictionary<string, SortedSet<string>> d, string k, string v) { if (!d.TryGetValue(k, out var s)) d[k] = s = new(StringComparer.Ordinal); s.Add(v); }
		void Walk(object acts, string bid)
		{
			foreach (var ao in Tree.AsList(acts))
			{
				if (ao is not Dictionary<string, object> a) continue;
				string verb = Tree.S(a.GetValueOrDefault("do"));
				if (verb == "flag" && a.ContainsKey("name")) Add(sets, Tree.S(a["name"]), bid);
				if (verb == "trigger") edges.Add((bid, Tree.S(a.GetValueOrDefault("beat")), "triggers"));
				Walk(a.GetValueOrDefault("then"), bid); Walk(a.GetValueOrDefault("else"), bid);
				foreach (var o in Tree.AsList(a.GetValueOrDefault("options"))) if (o is Dictionary<string, object> od) Walk(od.GetValueOrDefault("do"), bid);
			}
		}
		foreach (var b in beats)
		{
			string id = Tree.S(b["id"]);
			foreach (var r in Tree.AsList(b.GetValueOrDefault("requires"))) { edges.Add((Tree.S(r), id, "requires")); Add(reads, Tree.S(r), id); }
			foreach (var key in new[] { "when", "activate_when", "done_when", "cancel_when" })
				foreach (var c in Tree.AsList(b.GetValueOrDefault(key)))
					foreach (var atom in Checker.Atoms(c))
						if (atom.Kind == 'p' && (atom.Name == "flag" || atom.Name == "beat") && atom.Args.Count > 0) Add(reads, atom.Args[0], id);
			foreach (var key in new[] { "on_awaken", "pressure", "happens", "on_resolve", "on_cancel" }) Walk(b.GetValueOrDefault(key), id);
		}
		foreach (var (flag, setters) in sets)
			foreach (var s in setters)
				foreach (var r in reads.GetValueOrDefault(flag) ?? new SortedSet<string>())
					if (r != s && !edges.Contains((s, r, "requires"))) edges.Add((s, r, $"flag {flag}"));
		return edges;
	}

	private static string Ident(string s) => "n_" + new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

	public static string Render(Dictionary<string, object> doc, bool dot)
	{
		var edges = Edges(doc, out var beats);
		var sb = new StringBuilder();
		if (dot)
		{
			sb.Append("digraph campaign {\n  rankdir=LR; node [shape=box, style=rounded];\n");
			foreach (var b in beats) sb.Append($"  {Ident(Tree.S(b["id"]))} [label=\"{Tree.S(b["id"])}\\n{(b.TryGetValue("date", out var d) ? Tree.S(d) : Tree.S(b.GetValueOrDefault("kind")))}\"];\n");
			foreach (var (f, t, l) in edges)
				sb.Append($"  {Ident(f)} -> {Ident(t)} [label=\"{l}\"{(l == "requires" ? "" : l == "triggers" ? " style=dashed" : " style=dotted")}];\n");
			sb.Append("}\n");
		}
		else
		{
			sb.Append("flowchart LR\n");
			foreach (var b in beats)
			{
				string when = b.TryGetValue("date", out var d) && Tree.S(d).Length > 0 ? Tree.S(d) : Tree.S(b.GetValueOrDefault("kind"));
				string label = $"{Tree.S(b["id"])}<br/>{when}";
				string shape = Tree.S(b.GetValueOrDefault("kind")) switch { "fixed" => $"([\"{label}\"])", "pressure" => $">\"{label}\"]", "conditional" => "{{\"" + label + "\"}}", _ => $"[\"{label}\"]" };
				sb.Append($"  {Ident(Tree.S(b["id"]))}{shape}\n");
			}
			foreach (var (f, t, l) in edges)
				sb.Append($"  {Ident(f)} {(l == "requires" ? "-->" : "-.->")}{(l == "requires" ? "" : $"|{l}|")} {Ident(t)}\n");
		}
		return sb.ToString();
	}
}
