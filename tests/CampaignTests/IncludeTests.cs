using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampaignTool;
using Xunit;

namespace CampaignTests;

// A campaign's header can include parts (tools/CampaignTool/src/parts): their
// declarations are the campaign's own, compiled into one JSON, and every line
// still says which file it came from.
public class IncludeTests
{
	private static string Root => Known.Root;
	private static string Read(string rel) => File.ReadAllText(Path.Combine(Root, rel));

	private const string Host = @"campaign T(title: ""t"", include: ""bits"");
questline Main(title: ""Main"", forCompany: Company.HBC) {
    quest First(title: ""First"") { reach Go(place: ""York Factory""); }
}
";
	private const string Bits = @"// a part: no header
const string where = ""Fort Okanogan"";
questline Other(title: ""Other"", forCompany: Company.American) {
    quest Second(title: ""Second"") [First] {
        reach There(place: where);
    }
}
";

	[Fact]
	public void A_part_is_compiled_into_the_campaign()
	{
		var res = CampLang.Compile(Host, new Dictionary<string, string> { ["bits"] = Bits });
		var lines = Tree.AsList(res.Json["questlines"]).Cast<Dictionary<string, object>>().ToList();
		Assert.Equal(new[] { "Main", "Other" }, lines.Select(l => Tree.S(l["id"])));
		Assert.Equal("American", Tree.S(lines[1]["for"]));
		Assert.False(res.Json.ContainsKey("include"));                 // the game sees one campaign
		var (part, line) = res.Where(res.StepLines["Second/There"]);
		Assert.Equal(("bits", 5), (part, line));                       // the step is on line 5 of the part
		Assert.Equal(((string)null, 3), res.Where(res.StepLines["First/Go"]));
	}

	[Fact]
	public void An_error_in_a_part_names_the_part_and_its_own_line()
	{
		var e = Assert.Throws<CampError>(() => CampLang.Compile(Host, new Dictionary<string, string> { ["bits"] = Bits.Replace("reach There(place: where)", "reach There(place: nowhere)") }));
		Assert.Equal("bits", e.Part);
		Assert.Equal(5, e.At.Line);
		Assert.Contains("parts/bits.camp", e.Msg);
	}

	[Fact]
	public void A_missing_part_or_a_part_with_a_header_is_refused()
	{
		Assert.Contains("no part called 'not-there'", Assert.Throws<CampError>(() => CampLang.Compile(Host.Replace("\"bits\"", "\"not-there\""))).Msg);
		Assert.Contains("no campaign header", Assert.Throws<CampError>(() => CampLang.Compile(Host, new Dictionary<string, string> { ["bits"] = "campaign X(title: \"x\");" })).Msg);
	}

	[Fact]
	public void The_story_campaign_has_the_American_road()
	{
		var doc = CampLang.CompileText(Read("tools/CampaignTool/src/company-servant.camp"));
		var american = Tree.AsList(doc["questlines"]).Cast<Dictionary<string, object>>().Where(l => Tree.S(l.GetValueOrDefault("for")) == "American").ToList();
		Assert.Equal(new[] { "TheAstorians", "TheWarComesWest", "TheLastYears" }, american.Select(l => Tree.S(l["id"])));
		Assert.Empty(Checker.Check(doc).Errors);
		Assert.Empty(Checker.Check(doc).Warnings);
		// Every place the American road names is on the map.
		var pins = Tree.AsList(MapData.Pins(CampLang.Compile(Read("tools/CampaignTool/src/company-servant.camp")))["pins"]).Cast<Dictionary<string, object>>();
		Assert.All(pins.Where(p => Tree.S(p["quest"]).StartsWith("Am")), p => Assert.True((bool)p["ok"], Tree.S(p["why"])));
	}

	[Fact]
	public void The_editor_checks_a_part_inside_the_campaign_that_includes_it()
	{
		const string path = "tools/CampaignTool/src/parts/american-road.camp";
		var r = WebServer.Check(Read(path), path);
		Assert.True((bool)r["ok"]);
		Assert.Equal("tools/CampaignTool/src/company-servant.camp", Tree.S(r["host"]));
		var broken = WebServer.Check(Read(path).Replace("reach Ride(place: amSpokane", "reach Ride(place: \"Nowhereville\""), path);
		var err = Assert.Single(Tree.AsList(broken["problems"]).Cast<Dictionary<string, object>>(), p => Tree.S(p["severity"]) == "error" && Tree.S(p["message"]).Contains("Nowhereville") && (double)p["line"] < 200);
		Assert.Equal("", Tree.S(err["message"]).StartsWith("in ") ? "prefixed" : "");    // its own file: no "in other.camp" prefix
	}
}
