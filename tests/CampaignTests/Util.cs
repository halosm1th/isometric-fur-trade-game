using System.Collections.Generic;
using System.IO;
using CampaignTool;

namespace CampaignTests;

internal static class U
{
	public static string Root => Known.Root;
	public static string Read(string rel) => File.ReadAllText(Path.Combine(Root, rel));
	public static Dictionary<string, object> Json(string rel) => Tree.ParseObject(Read(rel));

	// The beat B of: campaign T(title: "t"); <decls> beat B(id: "b", <header>) { <members> }
	public static Dictionary<string, object> Beat(string members, string header = "date: date(1816, 6, 19)", string decls = "") =>
		(Dictionary<string, object>)((List<object>)CampLang.CompileText($"campaign T(title: \"t\");\n{decls}\nbeat B(id: \"b\", {header}) {{\n{members}\n}}\n")["beats"])[^1];

	public static List<object> Happens(string statements, string decls = "") =>
		(List<object>)Beat($"OnActivate() {{ {statements} }}", decls: decls)["happens"];

	public static List<object> Whens(string cond, string decls = "") =>
		(List<object>)Beat($"AwakenWhen() => {cond};", decls: decls)["when"];

	public static object When(string cond, string decls = "") => Whens(cond, decls)[0];

	public static Dictionary<string, object> Compile(string src) => CampLang.CompileText(src);

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
