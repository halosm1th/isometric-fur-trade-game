using System.Collections.Generic;
using System.Linq;
using CampaignTool;
using Xunit;

namespace CampaignTests;

// The editor's Map tab (tools/CampaignTool/MapData.cs): the world and cast it
// draws, and every place a campaign names, resolved as the game resolves it.
public class MapTests
{
	private const string Sample = @"
campaign T(title: ""t"");

questline Road(title: ""The road"", forCompany: Company.NWC) {
    quest Souris(title: ""Fort Souris"", giver: Person.Partner) {
        reach Go(place: ""Fort Souris"", text: ""Go up the Assiniboine"");
        talk Report(who: ""leader@Fort Souris"");
        talk Camp(who: ""chief@Métis of the Souris"") { Says() => ""Sit.""; }
        talk Lost(who: ""chief@Nowhere at all"");
        OnComplete() { player.postTo(""Fort Souris""); }
    }
}

beat Raid(date: date(1812, 1, 1)) {
    OnActivate() { world.addRegard(""Métis of the Souris"", -5); }
}
";

	private static List<Dictionary<string, object>> Pins(string src) =>
		Tree.AsList(MapData.Pins(CampLang.Compile(src))["pins"]).Cast<Dictionary<string, object>>().ToList();

	[Fact]
	public void The_world_has_its_posts_camps_and_cast()
	{
		var w = MapData.World();
		var places = Tree.AsList(w["places"]).Cast<Dictionary<string, object>>().ToList();
		Assert.Contains(places, p => Tree.S(p["name"]) == "York Factory" && Tree.S(p["kind"]) == "post");
		Assert.Contains(places, p => Tree.S(p["name"]) == "Fort Manuel Lisa" && Tree.S(p["company"]) == "AMER");
		Assert.Contains(places, p => Tree.S(p["name"]) == "Métis of the Souris" && Tree.S(p["kind"]) == "camp");
		var cast = Tree.AsList(w["cast"]).Cast<Dictionary<string, object>>().ToList();
		var auld = Assert.Single(cast, c => Tree.S(c["name"]) == "Mr. William Auld");
		Assert.Equal("York Factory", Tree.S(auld["place"]));
		Assert.Equal("Factor", Tree.S(auld["role"]));
		Assert.True((bool)auld["leader"]);
		// Every marker's household is a place on the map.
		var names = places.Select(p => Tree.S(p["name"])).ToHashSet();
		Assert.All(cast, c => Assert.Contains(Tree.S(c["place"]), names));
	}

	[Fact]
	public void Quest_steps_are_pinned_in_order_with_their_lines()
	{
		var pins = Pins(Sample);
		var steps = pins.Where(p => Tree.S(p["quest"]) == "Souris" && p["step"] != null).ToList();
		Assert.Equal(new[] { "Go", "Report", "Camp", "Lost" }, steps.Select(p => Tree.S(p["step"])));
		Assert.Equal(new[] { 1.0, 2.0, 3.0, 4.0 }, steps.Select(p => (double)p["order"]));
		Assert.Equal("Fort Souris", Tree.S(steps[1]["at"]));
		Assert.Equal(6.0, (double)steps[0]["line"]);        // `reach Go(...)` is on line 6 of the sample
		var giver = Assert.Single(pins, p => Tree.S(p["kind"]) == "giver");
		Assert.Equal("Nor'Westers' house (York)", Tree.S(giver["at"]));
	}

	[Fact]
	public void A_name_that_is_nowhere_is_marked_like_the_checker_marks_it()
	{
		var lost = Assert.Single(Pins(Sample), p => !(bool)p["ok"]);
		Assert.Equal("Lost", Tree.S(lost["step"]));
		Assert.Contains("'Nowhere at all' is not a post or a camp", Tree.S(lost["why"]));
		Assert.Null(lost["x"]);
	}

	[Fact]
	public void Beats_and_quest_effects_mark_the_places_they_act_on()
	{
		var effects = Pins(Sample).Where(p => Tree.S(p["kind"]) == "effect").ToList();
		Assert.Contains(effects, p => Tree.S(p["group"]) == "beat" && Tree.S(p["at"]) == "Métis of the Souris");
		Assert.Contains(effects, p => Tree.S(p["group"]) == "quest" && Tree.S(p["at"]) == "Fort Souris");
	}
}
