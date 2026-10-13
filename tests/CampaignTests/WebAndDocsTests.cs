using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

public class DocsTests
{
	private static IEnumerable<string> Pages() => Directory.GetFiles(Path.Combine(Root, "docs/wiki"), "*.md");

	[Fact]
	public void TheLibraryReferenceIsWhatTheTableWrites() =>
		Assert.True(Read("docs/wiki/Library-Reference.md") == LangDocs.ReferenceMarkdown(), "docs/wiki/Library-Reference.md is stale: run  dotnet run --project tools/CampaignTool -- docs");

	[Fact]
	public void EveryCompleteScriptInTheDocsCompilesAndChecks()
	{
		int n = 0;
		foreach (var page in Pages().Append(Path.Combine(Root, "tools/CampaignTool/README.md")))
			foreach (Match m in Regex.Matches(File.ReadAllText(page), "```c\n(campaign .*?)```", RegexOptions.Singleline))
			{
				n++;
				var doc = CampLang.CompileText(m.Groups[1].Value);
				Assert.Empty(Checker.Check(doc).Errors);
			}
		Assert.True(n >= 2, "expected some complete scripts in the docs");
	}

	[Fact]
	public void EveryLinkAndImageInTheWikiPointsSomewhere()
	{
		var pages = Pages().Select(p => Path.GetFileNameWithoutExtension(p)).ToHashSet();
		foreach (var page in Pages())
		{
			string text = File.ReadAllText(page);
			foreach (Match m in Regex.Matches(text, @"\]\(([^)\s]+)\)"))
			{
				string target = m.Groups[1].Value;
				if (target.StartsWith("http") || target.StartsWith("images/") || target.StartsWith("<")) continue;
				string name = target.Split('#')[0];
				Assert.True(pages.Contains(name), $"{Path.GetFileName(page)} links to {target}, which is not a wiki page");
			}
			foreach (Match m in Regex.Matches(text, @"!\[[^\]]*\]\((images/[^)]+)\)"))
				Assert.True(File.Exists(Path.Combine(Root, "docs/wiki", m.Groups[1].Value)), $"{Path.GetFileName(page)} shows {m.Groups[1].Value}, which is missing");
		}
	}

	[Fact]
	public void EveryHookTheParserKnowsIsDocumented() =>
		Assert.Equal(CampParser.Hooks.OrderBy(x => x), LangDocs.Hooks.Select(h => h.Name).OrderBy(x => x));

	[Fact]
	public void TheSidebarListsEveryPage()
	{
		string side = Read("docs/wiki/_Sidebar.md");
		foreach (var p in Pages().Select(Path.GetFileNameWithoutExtension).Where(n => !n.StartsWith("_") && n != "Home"))
			Assert.Contains($"]({p})", side);
	}
}

public class LongCampaignTests
{
	private static Dictionary<string, object> Doc() => CampLang.CompileText(Read("tools/CampaignTool/src/company-years.camp"));
	private static Dictionary<string, object> World() => Json("tools/CampaignTool/examples/company-years.scenario.json");

	[Fact]
	public void ItIsBigAndUsesEveryHook()
	{
		var beats = ((List<object>)Doc()["beats"]).Cast<Dictionary<string, object>>().ToList();
		Assert.InRange(beats.Count, 50, 100);
		foreach (var key in new[] { "when", "on_awaken", "pressure", "activate_when", "happens", "done_when", "on_resolve", "cancel_when", "on_cancel" })
			Assert.Contains(beats, b => b.ContainsKey(key));
	}

	[Fact]
	public void TheHistoryHappensOnItsDays()
	{
		var r = Sim.Simulate(Doc(), World(), 3);
		var fired = r.Runner.Fired;
		string On(string id) => CampaignLogic.DateOf(fired.First(f => f.Id == id).Day).ToString("yyyy-MM-dd");
		Assert.Equal("1811-09-24", On("settlers-winter-at-york"));
		Assert.Equal("1816-06-19", On("seven-oaks"));
		Assert.Equal("1821-03-26", On("merger"));
		Assert.Equal("1821-07-10", On("simpson-council"));
		Assert.Equal("1826-05-05", On("red-river-flood"));
		Assert.Equal(19, fired.Count(f => f.Id.StartsWith("ship-")));
		Assert.Equal(3, fired.Count(f => f.Id.StartsWith("interior-war-") && f.Id != "interior-war-ambush"));
	}

	[Fact]
	public void ActsFollowFromEachOther()
	{
		var r = Sim.Simulate(Doc(), World(), 3);
		var order = r.Runner.Fired.Select(f => f.Id).ToList();
		foreach (var (before, after) in new[] { ("seven-oaks", "selkirk-fort-william"), ("selkirk-fort-william", "fort-william-stood-down"), ("merger", "canoes-stood-down"), ("simpson-council", "men-let-go"), ("colony-dispersed", "colony-restored") })
			Assert.True(order.IndexOf(before) >= 0 && order.IndexOf(before) < order.IndexOf(after), $"{before} should come before {after}");
	}

	[Fact]
	public void ASevenOaksTheWorldAvertsIsCancelled()
	{
		var calm = World();
		calm["tension"] = Tree.Dict(("1811-01-01", 5.0));
		calm["events"] = L();
		var r = Sim.Simulate(Doc(), calm, 3);
		Assert.DoesNotContain(r.Runner.Fired, f => f.Id == "seven-oaks");
		Assert.Contains("seven-oaks", r.Runner.Cancelled);
		Assert.Contains("seven_oaks_averted", r.Runner.State.Flags);
		Assert.DoesNotContain(r.Runner.Fired, f => f.Id == "selkirk-fort-william");   // nothing followed from it
	}

	[Fact]
	public void TheCountersKeepAnOpinion()
	{
		var r = Sim.Simulate(Doc(), World(), 3);
		Assert.True(r.Runner.State.Vars.GetValueOrDefault("goodYears") + r.Runner.State.Vars.GetValueOrDefault("hardYears") + r.Runner.Fired.Count(f => f.Id == "committee-loses-patience") > 5);
	}

	[Fact]
	public void ItPlaysTheSameEveryTime()
	{
		string Run() => string.Join(",", Sim.Simulate(Doc(), World(), 11).Runner.Fired.Select(f => f.Id + "@" + f.Day));
		Assert.Equal(Run(), Run());
	}
}

public class BranchingCampaignTests
{
	private static Dictionary<string, object> Peace() => CampLang.CompileText(Read("tools/CampaignTool/src/the-long-peace.camp"));
	private static HashSet<string> Fired(Dictionary<string, object> sc) => Sim.Simulate(Peace(), sc, 5).Runner.Fired.Select(f => f.Id).ToHashSet();

	[Fact]
	public void ATruceTurnsTheProclamationIntoABargain()
	{
		var f = Fired(Json("tools/CampaignTool/examples/the-long-peace.scenario.json"));
		Assert.Contains("pemmican-bargain", f);
		Assert.Contains("seven-oaks-averted", f);
		Assert.DoesNotContain("pemmican-proclamation", f);
		Assert.DoesNotContain("seven-oaks", f);
	}

	[Fact]
	public void WithoutOneTheHistoricalRoadIsWaiting()
	{
		var f = Fired(Json("tools/CampaignTool/examples/the-long-peace-at-war.scenario.json"));
		Assert.Contains("pemmican-proclamation", f);
		Assert.Contains("seven-oaks", f);
		Assert.DoesNotContain("pemmican-bargain", f);
		Assert.DoesNotContain("seven-oaks-averted", f);
	}

	[Fact]
	public void TheRiversRunReactsToThePlayersDeeds()
	{
		var doc = CampLang.CompileText(Read("tools/CampaignTool/src/the-rivers-run.camp"));
		var none = Json("tools/CampaignTool/examples/the-rivers-run.scenario.json");
		none["events"] = L();
		Assert.DoesNotContain(Sim.Simulate(doc, none, 5).Runner.Fired, f => f.Id == "the-boatmen-talk");
		Assert.Contains(Sim.Simulate(doc, Json("tools/CampaignTool/examples/the-rivers-run.scenario.json"), 5).Runner.Fired, f => f.Id == "the-boatmen-talk");
	}

	[Fact]
	public void EveryShippedCampaignHasRumoursForWhatIsComing()
	{
		foreach (var src in new[] { "historical", "company-years", "the-long-peace", "the-rivers-run" })
		{
			var beats = ((List<object>)CampLang.CompileText(Read($"tools/CampaignTool/src/{src}.camp"))["beats"]).Cast<Dictionary<string, object>>();
			Assert.True(beats.Count(b => b.ContainsKey("rumour")) >= 4, $"{src} should tell the player of what is stirring");
		}
	}
}

public class ExampleTests
{
	[Fact]
	public void TheLifecycleExampleReallyRunsInTheOrderItDescribes()
	{
		var doc = CampLang.CompileText(Read("tools/CampaignTool/examples/02-the-lifecycle.camp"));
		var sc = Tree.Dict(("start", "1811-08-20"), ("end", "1814-06-01"), ("adherence", "Strict"), ("tension", Tree.Dict(("1811-01-01", 60.0))));
		var r = Sim.Simulate(doc, sc, 1);
		var log = r.Log.Select(l => l.beat + ": " + l.text).ToList();
		Assert.Contains(log, l => l.StartsWith("Embargo") && l.Contains("rumour"));            // OnAwaken
		Assert.Contains(log, l => l.StartsWith("Embargo") && l.Contains("the embargo is expected")); // OnApproach
		Assert.Contains(r.Runner.Fired, f => f.Id == "Embargo");                                // forced once its window ran out, because ActivateWhen (tension >= 50) held
		Assert.Contains(log, l => l.StartsWith("Embargo") && l.Contains("flags") == false && l.Contains("tension +20"));
	}

	[Fact]
	public void TheFirstExampleSendsItsLetter()
	{
		var doc = CampLang.CompileText(Read("tools/CampaignTool/examples/01-first-beat.camp"));
		var r = Sim.Simulate(doc, Tree.Dict(("start", "1812-01-01"), ("end", "1812-12-31")), 1);
		Assert.Equal(new[] { "TheLetter", "TheAnswer" }, r.Runner.Fired.Select(f => f.Id));
		Assert.Contains(r.Log, l => l.text.Contains("the spring letter"));
	}
}

public class WebServerTests : IDisposable
{
	private readonly HttpClient _http = new();
	private readonly string _base;

	public WebServerTests()
	{
		int port;
		var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
		var server = new WebServer(port);
		new Thread(() => server.Run()) { IsBackground = true }.Start();
		_base = $"http://localhost:{port}";
		for (int i = 0; i < 50; i++)
		{
			try { _http.GetAsync(_base + "/api/library").Result.Dispose(); break; } catch { Thread.Sleep(100); }
		}
	}

	public void Dispose() => _http.Dispose();

	private Dictionary<string, object> Get(string route) => Tree.ParseObject(_http.GetStringAsync(_base + route).Result);
	private (HttpStatusCode, Dictionary<string, object>) Send(HttpMethod m, string route, string body)
	{
		var r = _http.SendAsync(new HttpRequestMessage(m, _base + route) { Content = new StringContent(body, Encoding.UTF8, "text/plain") }).Result;
		return (r.StatusCode, Tree.ParseObject(r.Content.ReadAsStringAsync().Result));
	}

	[Fact]
	public void ServesTheEditorPage()
	{
		string html = _http.GetStringAsync(_base + "/").Result;
		Assert.Contains("Campaign Editor", html);
		Assert.Contains("editor.js", html);
		Assert.Contains("function tokenize", _http.GetStringAsync(_base + "/editor.js").Result);
	}

	[Fact]
	public void TheLibraryIsServed()
	{
		var lib = Get("/api/library");
		Assert.Contains(Tree.AsList(lib["members"]), m => Tree.S(((Dictionary<string, object>)m)["name"]) == "raiseTension");
		Assert.Equal(9, Tree.AsList(lib["hooks"]).Count);
	}

	[Fact]
	public void FilesAreListedAndReadable()
	{
		var files = Tree.AsList(Get("/api/files")["files"]).Cast<Dictionary<string, object>>().ToList();
		Assert.Contains(files, f => Tree.S(f["path"]) == "tools/CampaignTool/src/historical.camp" && Tree.S(f["kind"]) == "source");
		Assert.Contains(files, f => Tree.S(f["kind"]) == "example");
		Assert.Contains(files, f => Tree.S(f["kind"]) == "scenario");
		var file = Get("/api/file?path=tools/CampaignTool/src/historical.camp");
		Assert.Contains("campaign Historical", Tree.S(file["text"]));
		var converted = Get("/api/file?path=Campaigns/Gemini%20and%20Clark.json");
		Assert.Equal(true, converted["converted"]);
		Assert.Contains("campaign ", Tree.S(converted["text"]));
		Assert.EndsWith(".camp", Tree.S(converted["saveAs"]));
	}

	[Fact]
	public void CheckSaysWhereAnErrorIs()
	{
		var (status, res) = Send(HttpMethod.Post, "/api/check", "campaign T(title: \"t\");\n\nbeat B() {\n    OnActivate() { world.raiseTensio(3); }\n}\n");
		Assert.Equal(HttpStatusCode.OK, status);
		Assert.Equal(false, res["ok"]);
		var err = (Dictionary<string, object>)res["error"];
		Assert.Equal(4.0, err["line"]);
		Assert.Contains("Did you mean 'raiseTension'", Tree.S(err["message"]));
	}

	[Fact]
	public void CheckGivesProblemsTheirLine()
	{
		var (_, res) = Send(HttpMethod.Post, "/api/check", "campaign T(title: \"t\");\n\nbeat A() {\n    AwakenWhen() => flags.has(\"never_set\");\n    OnActivate() { say(\"x\"); }\n}\n");
		Assert.Equal(true, res["ok"]);
		var warn = Tree.AsList(res["problems"]).Cast<Dictionary<string, object>>().First(p => Tree.S(p["severity"]) == "warning");
		Assert.Equal(3.0, warn["line"]);
		Assert.Contains("nothing sets", Tree.S(warn["message"]));
		Assert.Contains("\"beats\"", Tree.S(res["json"]));
	}

	[Fact]
	public void SimulateAndGraphWork()
	{
		string src = Read("tools/CampaignTool/examples/01-first-beat.camp");
		var (_, sim) = Send(HttpMethod.Post, "/api/sim", Tree.Write(Tree.Dict(("source", src), ("scenario", "{\"start\":\"1812-01-01\",\"end\":\"1812-12-31\"}"))));
		Assert.Equal(2, Tree.AsList(sim["fired"]).Count);
		var (_, g) = Send(HttpMethod.Post, "/api/graph", src);
		Assert.Equal(2, Tree.AsList(g["nodes"]).Count);
		Assert.Single(Tree.AsList(g["edges"]));
	}

	[Theory]
	[InlineData("../../etc/passwd")]
	[InlineData("/etc/passwd")]
	[InlineData("Scripts/Core/CampaignLogic.cs")]
	public void ReadingOutsideTheScriptsIsRefused(string path)
	{
		var r = _http.GetAsync(_base + "/api/file?path=" + Uri.EscapeDataString(path)).Result;
		Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
	}

	[Theory]
	[InlineData("Scripts/Core/Evil.camp")]
	[InlineData("tools/CampaignTool/src/../../../x.camp")]
	[InlineData("tools/CampaignTool/src/notascript.txt")]
	[InlineData("Campaigns/historical.json")]
	public void WritingOutsideTheScriptsIsRefused(string path)
	{
		var (status, _) = Send(HttpMethod.Put, "/api/file?path=" + Uri.EscapeDataString(path), "x");
		Assert.Equal(HttpStatusCode.BadRequest, status);
	}

	[Fact]
	public void InstallRefusesAScriptWithErrors()
	{
		var (status, res) = Send(HttpMethod.Post, "/api/install", Tree.Write(Tree.Dict(("name", "zz-test"), ("source", "campaign T(title: \"t\"); beat B() { OnActivate() { nope(); } }"))));
		Assert.Equal(HttpStatusCode.BadRequest, status);
		Assert.False(File.Exists(Path.Combine(Root, "Campaigns/zz-test.json")));
	}
}
