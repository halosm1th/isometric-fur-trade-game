using System.Collections.Generic;
using System.IO;
using CampaignTool;

namespace CampaignTests;

internal static class U
{
	public static string Root => Known.Root;
	public static string Read(string rel) => File.ReadAllText(Path.Combine(Root, rel));
	public static Dictionary<string, object> Json(string rel) => Tree.ParseObject(Read(rel));

	// The first beat of: campaign T; <decls> beat <head>; <clauses> end; end.
	public static Dictionary<string, object> Beat(string clauses, string head = "'b' : event on 1816-06-19", string decls = "") =>
		(Dictionary<string, object>)((List<object>)Lang.CompileText($"campaign T;\n{decls}\nbeat {head};\n{clauses}\nend;\nend.\n")["beats"])[0];

	public static List<object> Happens(string statements, string decls = "") =>
		(List<object>)Beat($"happens begin {statements} end;", decls: decls)["happens"];

	public static object When(string cond, string decls = "") =>
		((List<object>)Beat($"when {cond};", decls: decls)["when"])[0];

	public static Dictionary<string, object> Doc(params Dictionary<string, object>[] beats) =>
		Tree.Dict(("title", "t"), ("description", ""), ("beats", new List<object>(beats)));

	public static Dictionary<string, object> B(string id, string kind, string date = null, params (string, object)[] more)
	{
		var b = Tree.Dict(("id", id), ("title", id), ("kind", kind));
		if (date != null) b["date"] = date;
		foreach (var (k, v) in more) b[k] = v;
		return b;
	}

	public static Dictionary<string, object> Act(string verb, params (string, object)[] kv)
	{
		var d = Tree.Dict(("do", verb));
		foreach (var (k, v) in kv) d[k] = v;
		return d;
	}

	public static List<object> L(params object[] xs) => new(xs);
}
