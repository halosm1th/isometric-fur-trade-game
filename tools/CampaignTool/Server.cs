using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using IsometricFurTradeGame.Scripts.Core;

namespace CampaignTool;

// The web editor: a small local server (http://localhost:PORT) that serves
// tools/CampaignTool/web and answers the editor's questions with the same
// compiler, checker and simulator the command line uses.
public sealed class WebServer
{
	private readonly string _root = Known.Root;
	private string Tool => Path.GetFullPath(Path.Combine(_root, "tools", "CampaignTool"));
	private string Web => Path.GetFullPath(Path.Combine(Tool, "web"));
	private readonly HttpListener _http = new();
	private readonly int _port;

	public WebServer(int port) { _port = port; }

	public int Run()
	{
		_http.Prefixes.Add($"http://localhost:{_port}/");
		try { _http.Start(); }
		catch (HttpListenerException e) { Console.Error.WriteLine($"cannot listen on port {_port}: {e.Message}"); return 1; }
		Console.WriteLine($"Campaign editor: http://localhost:{_port}/   (Ctrl+C to stop)");
		while (true)
		{
			var ctx = _http.GetContext();
			try { Handle(ctx); }
			catch (Exception e)
			{
				try { Send(ctx, e is CampError or System.Text.Json.JsonException or FormatException ? 400 : 500, "application/json", Tree.Write(Tree.Dict(("error", e is CampError ce ? ce.Msg : e.Message)))); } catch { }
			}
		}
	}

	// ---------------------------------------------------------------- routing

	public void Handle(HttpListenerContext ctx)
	{
		var req = ctx.Request;
		string path = req.Url.AbsolutePath;
		if (path.StartsWith("/api/")) { Api(ctx, path[5..]); return; }
		if (path == "/") path = "/index.html";
		if (path == "/favicon.ico") { ctx.Response.StatusCode = 204; ctx.Response.Close(); return; }
		string rel = path.TrimStart('/');
		string file = Path.GetFullPath(Path.Combine(Web, rel));
		if (!file.StartsWith(Web + Path.DirectorySeparatorChar) || !File.Exists(file)) { Send(ctx, 404, "text/plain", "not found"); return; }
		string type = Path.GetExtension(file) switch { ".html" => "text/html; charset=utf-8", ".js" => "text/javascript; charset=utf-8", ".css" => "text/css; charset=utf-8", ".svg" => "image/svg+xml", _ => "application/octet-stream" };
		Send(ctx, 200, type, File.ReadAllBytes(file));
	}

	private static string Body(HttpListenerRequest req)
	{
		using var r = new StreamReader(req.InputStream, Encoding.UTF8);
		return r.ReadToEnd();
	}

	private void Api(HttpListenerContext ctx, string route)
	{
		var req = ctx.Request;
		switch (route)
		{
			case "library": Json(ctx, LangDocs.LibraryJson()); return;
			case "files": Json(ctx, Tree.Dict(("files", Files()))); return;
			case "file":
				{
					string rel = req.QueryString["path"] ?? "";
					if (req.HttpMethod == "GET") { Json(ctx, ReadFile(rel)); return; }
					string full = Writable(rel);
					Directory.CreateDirectory(Path.GetDirectoryName(full));
					File.WriteAllText(full, Body(req));
					Json(ctx, Tree.Dict(("saved", rel)));
					return;
				}
			case "check": Json(ctx, Check(Body(req))); return;
			case "sim": Json(ctx, Sim(Tree.ParseObject(Body(req)))); return;
			case "graph": Json(ctx, GraphOf(Body(req))); return;
			case "install": Json(ctx, Install(Tree.ParseObject(Body(req)))); return;
			// The Map tab: the world and its cast, its ground, and a campaign's places (MapData.cs).
			case "world": Json(ctx, MapData.World()); return;
			case "ground": Send(ctx, 200, "image/png", File.ReadAllBytes(Path.Combine(_root, "Data/World/ground.png"))); return;
			case "map": Json(ctx, MapData.Pins(CampLang.Compile(Body(req)))); return;
		}
		Send(ctx, 404, "application/json", Tree.Write(Tree.Dict(("error", "no such route"))));
	}

	// ---------------------------------------------------------------- files

	private IEnumerable<string> Dirs() => new[] { "tools/CampaignTool/src", "tools/CampaignTool/examples" };

	private List<object> Files()
	{
		var list = new List<object>();
		var sources = new HashSet<string>();
		foreach (var dir in Dirs())
		{
			string d = Path.Combine(_root, dir);
			if (!Directory.Exists(d)) continue;
			foreach (var f in Directory.GetFiles(d).OrderBy(f => f, StringComparer.Ordinal))
			{
				string ext = Path.GetExtension(f);
				string kind = ext == ".camp" ? (dir.EndsWith("src") ? "source" : "example") : f.EndsWith(".scenario.json") ? "scenario" : null;
				if (kind == null) continue;
				string rel = Path.GetRelativePath(_root, f).Replace('\\', '/');
				if (kind == "source") sources.Add(Path.GetFileNameWithoutExtension(f));
				list.Add(Tree.Dict(("path", rel), ("name", Path.GetFileName(f)), ("kind", kind),
					("installed", kind == "source" && File.Exists(Path.Combine(_root, "Campaigns", Path.GetFileNameWithoutExtension(f) + ".json")))));
			}
		}
		foreach (var f in Directory.GetFiles(Path.Combine(_root, "Campaigns"), "*.json").OrderBy(f => f, StringComparer.Ordinal))
			if (!sources.Contains(Path.GetFileNameWithoutExtension(f)))
				list.Add(Tree.Dict(("path", Path.GetRelativePath(_root, f).Replace('\\', '/')), ("name", Path.GetFileName(f)), ("kind", "json"), ("installed", true)));
		return list;
	}

	private string Resolve(string rel)
	{
		if (string.IsNullOrEmpty(rel) || rel.Contains("..") || Path.IsPathRooted(rel)) throw new CampError("not a path in this project");
		return Path.GetFullPath(Path.Combine(_root, rel));
	}

	private string Writable(string rel)
	{
		string full = Resolve(rel);
		bool ok = Dirs().Any(d => full.StartsWith(Path.GetFullPath(Path.Combine(_root, d)) + Path.DirectorySeparatorChar)) && (full.EndsWith(".camp") || full.EndsWith(".scenario.json"));
		return ok ? full : throw new CampError("scripts are saved in tools/CampaignTool/src or examples, as .camp files");
	}

	private Dictionary<string, object> ReadFile(string rel)
	{
		string full = Resolve(rel);
		string allowed = Path.GetFullPath(Path.Combine(_root, "Campaigns")) + Path.DirectorySeparatorChar;
		bool inDirs = Dirs().Any(d => full.StartsWith(Path.GetFullPath(Path.Combine(_root, d)) + Path.DirectorySeparatorChar));
		if (!inDirs && !full.StartsWith(allowed)) throw new CampError("not a script");
		string text = File.ReadAllText(full);
		if (full.EndsWith(".json") && !full.EndsWith(".scenario.json"))
		{
			string name = Regex.Replace(Path.GetFileNameWithoutExtension(full), @"\W+", "_");
			return Tree.Dict(("path", rel), ("text", Decompiler.Decompile(Tree.ParseObject(text), name)), ("converted", true), ("saveAs", $"tools/CampaignTool/src/{Path.GetFileNameWithoutExtension(full).Replace(' ', '-')}.camp"));
		}
		return Tree.Dict(("path", rel), ("text", text), ("converted", false));
	}

	// ---------------------------------------------------------------- check / compile

	private static readonly Regex BeatWhere = new(@"^beat (.+?)( > |$)");

	public static Dictionary<string, object> Check(string source)
	{
		CompileResult res;
		try { res = CampLang.Compile(source); }
		catch (CampError e)
		{
			return Tree.Dict(("ok", false),
				("error", Tree.Dict(("line", (double)Math.Max(e.At.Line, 1)), ("col", (double)Math.Max(e.At.Col, 1)), ("length", (double)Math.Max(e.At.Length, 1)), ("message", e.Msg))),
				("problems", new List<object>()), ("beats", 0.0));
		}
		var rep = Checker.Check(res.Json);
		var problems = new List<object>();
		void Add(string severity, IEnumerable<(string where, string msg)> items)
		{
			foreach (var (where, msg) in items)
			{
				var m = BeatWhere.Match(where);
				int line = m.Success && res.BeatLines.TryGetValue(m.Groups[1].Value, out var l) ? l : 1;
				problems.Add(Tree.Dict(("severity", severity), ("message", msg), ("where", where), ("line", (double)line),
					("beat", m.Success ? m.Groups[1].Value : null)));
			}
		}
		Add("error", rep.Errors); Add("warning", rep.Warnings); Add("note", rep.Infos);
		var beatLines = new Dictionary<string, object>();
		foreach (var kv in res.BeatLines) beatLines[kv.Key] = (double)kv.Value;
		return Tree.Dict(("ok", rep.Errors.Count == 0), ("error", null), ("problems", problems), ("beats", (double)Tree.AsList(res.Json["beats"]).Count),
			("json", Tree.Write(res.Json)), ("beatLines", beatLines));
	}

	// ---------------------------------------------------------------- sim / graph / install

	private Dictionary<string, object> Sim(Dictionary<string, object> req)
	{
		var doc = CampLang.CompileText(Tree.S(req["source"]));
		Dictionary<string, object> scenario = req.TryGetValue("scenario", out var sc) && Tree.S(sc).Trim().Length > 0 ? Tree.ParseObject(Tree.S(sc)) : null;
		string adherence = req.TryGetValue("adherence", out var ad) && Tree.S(ad).Length > 0 ? Tree.S(ad) : null;
		if (adherence != null && !CampaignTool.Sim.Levels.ContainsKey(adherence)) throw new CampError($"adherence is one of {string.Join(", ", CampaignTool.Sim.Levels.Keys)}");
		uint? seed = req.TryGetValue("seed", out var sd) && Tree.S(sd).Length > 0 ? uint.Parse(Tree.S(sd)) : null;
		var res = CampaignTool.Sim.Simulate(doc, scenario, seed, adherence);
		var runner = res.Runner;
		var fired = runner.Fired.Select(f =>
		{
			var b = runner.Script.Beats.First(x => x.Id == f.Id);
			return (object)Tree.Dict(("date", CampaignLogic.DateOf(f.Day).ToString("yyyy-MM-dd")), ("id", f.Id), ("how", f.How), ("title", b.Title));
		}).ToList();
		var done = new HashSet<string>(runner.Fired.Select(f => f.Id));
		return Tree.Dict(
			("start", res.Start), ("end", res.End), ("fired", fired),
			("founded", res.Founded.Select(f => (object)Tree.Dict(("date", f.date), ("name", f.name))).ToList()),
			("history", runner.History.Cast<object>().ToList()),
			("never", runner.Script.Beats.Where(b => !done.Contains(b.Id) && !runner.History.Contains(b.Id)).Select(b => (object)b.Id).ToList()),
			("log", res.Log.Select(l => (object)Tree.Dict(("date", l.date), ("beat", l.beat), ("text", l.text))).ToList()),
			("flags", runner.State.Flags.OrderBy(f => f, StringComparer.Ordinal).Cast<object>().ToList()),
			("vars", runner.State.Vars.Where(kv => !kv.Key.StartsWith("count:")).OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => (object)kv.Value)));
	}

	private static Dictionary<string, object> GraphOf(string source)
	{
		var res = CampLang.Compile(source);
		var edges = Graph.Edges(res.Json, out var beats);
		return Tree.Dict(
			("nodes", beats.Select(b => (object)Tree.Dict(("id", Tree.S(b["id"])), ("title", Tree.S(b.GetValueOrDefault("title"))), ("kind", Tree.S(b.GetValueOrDefault("kind"))),
				("date", Tree.S(b.GetValueOrDefault("date"))), ("line", (double)res.BeatLines.GetValueOrDefault(Tree.S(b["id"]), 1)))).ToList()),
			("edges", edges.Select(e => (object)Tree.Dict(("from", e.from), ("to", e.to), ("label", e.label))).ToList()));
	}

	private Dictionary<string, object> Install(Dictionary<string, object> req)
	{
		string name = Regex.Replace(Tree.S(req["name"]), @"\.camp$", "");
		if (!Regex.IsMatch(name, @"^[\w\- ]+$")) throw new CampError("a campaign file name is letters, digits, spaces, - and _");
		var doc = CampLang.CompileText(Tree.S(req["source"]));
		var rep = Checker.Check(doc);
		if (rep.Errors.Count > 0) throw new CampError($"not installed: {rep.Errors.Count} errors (see Problems)");
		string outp = Path.Combine(_root, "Campaigns", name + ".json");
		File.WriteAllText(outp, Tree.Write(doc) + "\n");
		return Tree.Dict(("installed", $"Campaigns/{name}.json"));
	}

	// ---------------------------------------------------------------- plumbing

	private static void Json(HttpListenerContext ctx, object o) => Send(ctx, 200, "application/json; charset=utf-8", Tree.Write(o));

	private static void Send(HttpListenerContext ctx, int status, string type, string body) => Send(ctx, status, type, Encoding.UTF8.GetBytes(body));

	private static void Send(HttpListenerContext ctx, int status, string type, byte[] body)
	{
		ctx.Response.StatusCode = status;
		ctx.Response.ContentType = type;
		ctx.Response.Headers["Cache-Control"] = "no-store";
		ctx.Response.ContentLength64 = body.Length;
		ctx.Response.OutputStream.Write(body, 0, body.Length);
		ctx.Response.Close();
	}
}
