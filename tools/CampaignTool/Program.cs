using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IsometricFurTradeGame.Scripts.Core;

namespace CampaignTool;

public static class Program
{
	private const string Usage = @"Campaign scripts: write them as a program, check them, draw them, play them forward.

  CampaignTool compile   story.camp [-o story.json]   the script -> the JSON the game reads (checks it first)
  CampaignTool decompile story.json [-o story.camp]   the other way, to move an old script across
  CampaignTool check     story.camp|story.json        errors and warnings (-v for notes, --strict to fail on warnings)
  CampaignTool graph     story.camp|story.json        the beats and what leads to what (mermaid; --dot for graphviz)
  CampaignTool sim       story.camp|story.json        play it forward: --scenario s.json --runs N --seed S
                                                      --adherence Gentle|Steady|Firm|Strict --start D --end D --log --state
  CampaignTool install   story.camp                   compile into Campaigns/ where the game finds it
  CampaignTool build                                  compile every tools/CampaignTool/src/*.camp into Campaigns/
  CampaignTool serve     [--port 8765]                the web editor, at http://localhost:8765/
  CampaignTool docs      [-o file]                    write the wiki's library reference (docs/wiki/Library-Reference.md)

Run as: dotnet run --project tools/CampaignTool -- check story.camp";

	public static int Main(string[] args)
	{
		try { return Run(args); }
		catch (CampError e) { Console.Error.WriteLine($"error: {e.Message}"); return 1; }
		catch (FileNotFoundException e) { Console.Error.WriteLine($"error: {e.Message}"); return 1; }
	}

	private sealed class Opts
	{
		public List<string> Files = new();
		public string Output, Scenario, Start, End, Adherence;
		public int Runs = 1, Port = 8765;
		public uint? Seed;
		public bool Force, Strict, Verbose, Dot, Log, State, Json;
	}

	private static Opts Parse(string[] a)
	{
		var o = new Opts();
		for (int i = 1; i < a.Length; i++)
		{
			switch (a[i])
			{
				case "-o": case "--output": o.Output = a[++i]; break;
				case "--scenario": o.Scenario = a[++i]; break;
				case "--start": o.Start = a[++i]; break;
				case "--end": o.End = a[++i]; break;
				case "--adherence": o.Adherence = a[++i]; if (!Sim.Levels.ContainsKey(o.Adherence)) throw new CampError($"adherence is one of {string.Join(", ", Sim.Levels.Keys)}"); break;
				case "--port": o.Port = int.Parse(a[++i]); break;
				case "--runs": o.Runs = int.Parse(a[++i]); break;
				case "--seed": o.Seed = uint.Parse(a[++i]); break;
				case "--force": o.Force = true; break;
				case "--strict": o.Strict = true; break;
				case "-v": case "--verbose": o.Verbose = true; break;
				case "--dot": o.Dot = true; break;
				case "--log": o.Log = true; break;
				case "--state": o.State = true; break;
				case "--json": o.Json = true; break;
				default:
					if (a[i].StartsWith("-")) throw new CampError($"unknown option {a[i]}");
					o.Files.Add(a[i]); break;
			}
		}
		return o;
	}

	private static string File1(Opts o)
	{
		if (o.Files.Count != 1) throw new CampError("give one file");
		return o.Files[0];
	}

	public static Dictionary<string, object> Load(string path)
	{
		string text = File.ReadAllText(path);
		if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return Tree.ParseObject(text);
		try { return CampLang.CompileText(text); }
		catch (CampError e) { throw new CampError($"{Path.GetFileName(path)}: {e.Message}"); }
	}

	private static Report Report(Dictionary<string, object> doc, string path, bool quiet = false, bool verbose = false)
	{
		var rep = Checker.Check(doc);
		string name = Path.GetFileName(path);
		foreach (var line in rep.Lines(verbose)) Console.Error.WriteLine($"{name}: {line}");
		int quests = Tree.AsList(doc.GetValueOrDefault("questlines")).OfType<Dictionary<string, object>>().Sum(l => Tree.AsList(l.GetValueOrDefault("quests")).Count);
		if (!quiet) Console.Error.WriteLine($"{name}: {Tree.AsList(doc.GetValueOrDefault("beats")).Count} beats{(quests > 0 ? $", {quests} quests" : "")}, {rep.Errors.Count} errors, {rep.Warnings.Count} warnings");
		return rep;
	}

	private static string CampaignsDir => Path.Combine(Known.Root, "Campaigns");

	private static int Run(string[] args)
	{
		if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Console.WriteLine(Usage); return args.Length == 0 ? 1 : 0; }
		var o = Parse(args);
		switch (args[0])
		{
			case "compile":
				{
					string path = File1(o);
					var doc = Load(path);
					var rep = Report(doc, path);
					if (rep.Errors.Count > 0 && !o.Force) { Console.Error.WriteLine("not written: fix the errors (or --force)"); return 1; }
					string outp = o.Output ?? Path.ChangeExtension(path, ".json");
					File.WriteAllText(outp, Tree.Write(doc) + "\n");
					Console.WriteLine($"wrote {outp}");
					return 0;
				}
			case "decompile":
				{
					string path = File1(o);
					string outp = o.Output ?? Path.ChangeExtension(path, ".camp");
					File.WriteAllText(outp, Decompiler.Decompile(Tree.ParseObject(File.ReadAllText(path)), Regex.Replace(Path.GetFileNameWithoutExtension(outp), @"\W", "_")));
					Console.WriteLine($"wrote {outp}");
					return 0;
				}
			case "check":
				{
					string path = File1(o);
					var rep = Report(Load(path), path, verbose: o.Verbose);
					return rep.Errors.Count > 0 || (o.Strict && rep.Warnings.Count > 0) ? 1 : 0;
				}
			case "install":
				{
					string path = File1(o);
					var doc = Load(path);
					if (Report(doc, path).Errors.Count > 0) { Console.Error.WriteLine("not installed: fix the errors"); return 1; }
					string outp = Path.Combine(CampaignsDir, Path.GetFileNameWithoutExtension(path) + ".json");
					File.WriteAllText(outp, Tree.Write(doc) + "\n");
					Console.WriteLine($"installed {outp}");
					return 0;
				}
			case "build":
				{
					string src = Path.Combine(Known.Root, "tools/CampaignTool/src");
					int bad = 0;
					foreach (var path in Directory.GetFiles(src, "*.camp").OrderBy(p => p, StringComparer.Ordinal))
					{
						var doc = Load(path);
						if (Report(doc, path, quiet: true).Errors.Count > 0) { bad++; continue; }
						string name = Path.GetFileNameWithoutExtension(path);
						File.WriteAllText(Path.Combine(CampaignsDir, name + ".json"), Tree.Write(doc) + "\n");
						Console.WriteLine($"built Campaigns/{name}.json from src/{Path.GetFileName(path)}");
					}
					return bad > 0 ? 1 : 0;
				}
			case "serve":
				return new WebServer(o.Port).Run();
			case "docs":
				{
					string outp = o.Output ?? Path.Combine(Known.Root, "docs/wiki/Library-Reference.md");
					Directory.CreateDirectory(Path.GetDirectoryName(outp));
					File.WriteAllText(outp, LangDocs.ReferenceMarkdown());
					Console.WriteLine($"wrote {outp}");
					return 0;
				}
			case "graph":
				Console.Write(Graph.Render(Load(File1(o)), o.Dot));
				return 0;
			case "sim":
				return RunSim(o);
		}
		Console.Error.WriteLine($"unknown command '{args[0]}'\n\n{Usage}");
		return 1;
	}

	private static int RunSim(Opts o)
	{
		var doc = Load(File1(o));
		Dictionary<string, object> scenario = o.Scenario != null ? Tree.ParseObject(File.ReadAllText(o.Scenario)) : null;
		if (o.Start != null) { scenario ??= new(); scenario["start"] = o.Start; }
		if (o.End != null) { scenario ??= new(); scenario["end"] = o.End; }
		if (o.Runs > 1) return MonteCarlo(doc, scenario, o);

		var res = Sim.Simulate(doc, scenario, o.Seed, o.Adherence);
		var runner = res.Runner;
		var beats = runner.Script.Beats;
		if (o.Json)
		{
			var firedRows = new List<object>();
			foreach (var g in runner.Fired.GroupBy(f => f.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
				firedRows.Add(Tree.List(g.Key, g.Select(f => (object)Tree.List(CampaignLogic.DateOf(f.Day).ToString("yyyy-MM-dd"), f.How)).ToList()));
			var vars = new Dictionary<string, object>();
			foreach (var kv in runner.State.Vars.OrderBy(k => k.Key, StringComparer.Ordinal)) vars[kv.Key] = kv.Value;
			Console.WriteLine(Tree.Write(Tree.Dict(
				("fired", firedRows), ("history", runner.History.Cast<object>().ToList()),
				("flags", runner.State.Flags.OrderBy(f => f, StringComparer.Ordinal).Cast<object>().ToList()), ("vars", vars),
				("log", res.Log.Select(l => (object)Tree.List(l.date, l.beat, l.text)).ToList()),
				("founded", res.Founded.Select(f => (object)Tree.List(f.date, f.name)).ToList()))));
			return 0;
		}
		Console.WriteLine($"{res.Start} .. {res.End}  {o.Adherence ?? runner.Level.ToString()}  seed {o.Seed ?? runner.Script.Seed ?? 20240611}");
		var events = new List<(string date, string text)>();
		foreach (var f in runner.Fired)
		{
			var b = beats.First(x => x.Id == f.Id);
			events.Add((CampaignLogic.DateOf(f.Day).ToString("yyyy-MM-dd"), $"{f.Id,-28} {f.How,-24} {b.Title}"));
		}
		foreach (var (date, name) in res.Founded) events.Add((date, $"{"(post founded)",-28} {name}"));
		foreach (var (date, text) in events.OrderBy(e => e.date, StringComparer.Ordinal).ThenBy(e => e.text, StringComparer.Ordinal)) Console.WriteLine($"{date}  {text}");
		if (runner.History.Count > 0) Console.WriteLine($"\nalready history at the start: {string.Join(", ", runner.History)}");
		var fired = new HashSet<string>(runner.Fired.Select(f => f.Id));
		var never = beats.Where(b => !fired.Contains(b.Id) && !runner.History.Contains(b.Id)).Select(b => b.Id).ToList();
		Console.WriteLine($"\nnever happened: {(never.Count > 0 ? string.Join(", ", never) : "none")}");
		if (o.Log)
		{
			Console.WriteLine("\nwhat they did:");
			foreach (var (date, beat, text) in res.Log) Console.WriteLine($"{date}  {beat,-24} {text}");
		}
		if (o.State)
		{
			Console.WriteLine("\nflags: " + string.Join(", ", runner.State.Flags.OrderBy(f => f, StringComparer.Ordinal)));
			Console.WriteLine("variables: " + string.Join(", ", runner.State.Vars.Where(kv => !kv.Key.StartsWith("count:")).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={Tree.Num(kv.Value)}")));
		}
		return 0;
	}

	private static int MonteCarlo(Dictionary<string, object> doc, Dictionary<string, object> scenario, Opts o)
	{
		var seen = new Dictionary<string, List<int>>();
		var forced = new Dictionary<string, int>();
		var history = new HashSet<string>();
		for (int n = 0; n < o.Runs; n++)
		{
			var res = Sim.Simulate(doc, scenario, unchecked((o.Seed ?? 1) + (uint)n * 7919u), o.Adherence);
			history.UnionWith(res.Runner.History);
			foreach (var g in res.Runner.Fired.GroupBy(f => f.Id))
			{
				var first = g.First();
				if (!seen.TryGetValue(g.Key, out var l)) seen[g.Key] = l = new();
				l.Add(first.Day);
				if (first.How is "forced" or "triggered") forced[g.Key] = forced.GetValueOrDefault(g.Key) + 1;
			}
		}
		Console.WriteLine($"{o.Runs} runs, {o.Adherence ?? "script adherence"}");
		Console.WriteLine($"{"beat",-30} {"fired",6}  {"forced",6}  earliest / median / latest");
		string Iso(double d) => CampaignLogic.DateOf((int)d).ToString("yyyy-MM-dd");
		foreach (var b in Tree.AsList(doc.GetValueOrDefault("beats")).Cast<Dictionary<string, object>>())
		{
			string id = Tree.S(b["id"]);
			if (!seen.TryGetValue(id, out var days) || days.Count == 0)
			{
				Console.WriteLine($"{id,-30} {"-",6}  {"",6}  {(history.Contains(id) ? "(history: before the start)" : "never")}");
				continue;
			}
			var sorted = days.OrderBy(x => x).ToList();
			double median = sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;
			Console.WriteLine($"{id,-30} {100 * days.Count / o.Runs,5}%  {100 * forced.GetValueOrDefault(id) / days.Count,5}%  {Iso(sorted[0])} / {Iso(median)} / {Iso(sorted[^1])}");
		}
		return 0;
	}
}
