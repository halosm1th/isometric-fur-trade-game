using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampaignTool;
using Xunit;

namespace CampaignTests;

// "Play in game" (tools/CampaignTool/Launch.cs): how the editor starts the
// game at a quest step - the road, the place, the day - without starting it.
public class LaunchTests
{
	private static string Read(string rel) => File.ReadAllText(Path.Combine(Known.Root, rel));
	private static readonly CompileResult Story = CampLang.Compile(Read("tools/CampaignTool/src/company-servant.camp"));
	private static Launch.Plan For(string quest, string step) => Launch.For("company-servant", Story.Json, quest, step);

	[Fact]
	public void A_step_starts_on_its_road_at_its_place()
	{
		var p = For("Neighbours", "Call");
		Assert.Equal(new[] { "--campaign=company-servant", "--start=HBC", "--quest=Neighbours:Call", "--spawn=Fort Augustus" }, p.Args);
		Assert.Equal("--start=NWC", For("FortSouris", "Report").Args[1]);
		Assert.Equal("--start=American", For("AmSyilxTrade", "Council").Args[1]);
		Assert.Equal("Syilx (Okanagan)", For("AmSyilxTrade", "Council").Spawn);
		Assert.Equal("York Factory", For("Landfall", null).Spawn);            // Person.Factor: the Factor's house
	}

	[Fact]
	public void It_starts_the_day_after_what_the_quest_or_its_requirements_wait_for()
	{
		Assert.Equal("1814-04-02", For("AmTheBlackfoot", "Council").Date);      // its own date
		Assert.Equal("1813-01-16", For("AmTheRiverNations", "Mandan").Date);   // the war chapter it follows
		Assert.Equal("1821-03-27", For("OneCompany", "Hear").Date);            // the merger beat of the history
		Assert.Null(For("Landfall", null).Date);
	}

	[Fact]
	public void The_cursor_picks_the_step_it_is_in()
	{
		const string part = "tools/CampaignTool/src/parts/american-road.camp";
		var view = WebServer.View(Read(part), part);
		int line = Read(part).Split('\n').ToList().FindIndex(l => l.Contains("talk Mandan(")) + 2;
		Assert.Equal(("AmTheRiverNations", "Mandan"), Launch.StepAt(view.Res, view.PathOf, part, line));
	}
}
