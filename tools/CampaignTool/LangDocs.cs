using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CampaignTool;

// What the language says about itself beyond the library: the beat's hooks and
// header, and the words that are not a library member. The editor completes
// from these, and the wiki is written from them.
public static class LangDocs
{
	public sealed record Hook(string Name, string Key, bool Condition, string Summary);
	public sealed record Arg(string Name, string Type, string Summary);

	public static readonly Hook[] Hooks =
	{
		new("AwakenWhen", "when", true, "A beat only does anything on the days this holds (and its requirements are met). The first such day it wakes, and OnAwaken runs. Leave it out and the beat is awake from the start."),
		new("OnAwaken", "on_awaken", false, "What happens at the moment the beat wakes, once. A repeating beat does it again each time round."),
		new("OnApproach", "pressure", false, "Pressure on the world as the beat's day draws near: it runs once, when the lead time before the date begins. This is how a beat makes the world ready for itself."),
		new("ActivateWhen", "activate_when", true, "A last condition the beat waits for once it is due. If it does not hold the beat is due but not yet happening."),
		new("OnActivate", "happens", false, "What the beat does when it happens."),
		new("ResolveWhen", "done_when", true, "The world got there by itself: while this holds, the beat is over without OnActivate running."),
		new("OnResolve", "on_resolve", false, "What happens when the world resolves the beat."),
		new("CancelWhen", "cancel_when", true, "The beat can no longer happen. Checked before everything else, on every day, whatever the adherence."),
		new("OnCancel", "on_cancel", false, "What happens when the beat is cancelled."),
	};

	public static readonly Arg[] HeaderArgs =
	{
		new("title", "string", "The campaign's name in the menu. Required."),
		new("description", "string", "A few sentences about the campaign, shown under its name."),
		new("note", "string", "A note kept in the JSON, for whoever opens it. Free text."),
		new("seed", "int", "The seed for the script's dice (chance, random). The same seed gives the same history."),
		new("postsByFoundingYear", "bool", "When true, posts appear in the world on 1 January of the year they were founded."),
	};

	public static readonly Arg[] BeatArgs =
	{
		new("id", "string", "The beat's id, if it is not to be its name. Needed when the id has a hyphen, or to give each beat of a loop its own."),
		new("title", "string", "What the log and the news call the beat. Defaults to the id."),
		new("kind", "Kind", "Fixed, Conditional, Event or Pressure. Almost never written: it is derived from what the beat has (see the guide)."),
		new("date", "Date", "The day the beat is due. A beat with no date is due the moment it wakes, plus its delay."),
		new("window", "Days", "How long the world has to do the beat itself after its date before it is forced (Firm and Strict adherence)."),
		new("lead", "Days", "How long before its date the beat's pressure (OnApproach) begins. Default 30."),
		new("delay", "Days", "For a beat with no date: days after waking before it is due."),
		new("repeat", "Days", "The beat comes round again this many days after it happened, and can happen again."),
		new("rumour", "string", "What the player is told while the beat is stirring or its day draws near: one line for the journal's \"What is stirring\" list, written as something a person at the post could have heard. The beat's own title is never shown there, so a rumour is the only thing that says anything before the beat happens."),
		new("keywords", "string[]", "Words the news director looks for when it chooses what to tell the player. Free text."),
	};

	public static readonly string[] Keywords = { "campaign", "beat", "const", "void", "if", "else", "for", "foreach", "in", "true", "false" };
	public static readonly string[] Types = { "int", "float", "string", "bool", "Date", "Days", "Company", "Person", "Item", "Season", "Kind", "Beat", "Brigade" };

	// The editor reads this once.
	public static Dictionary<string, object> LibraryJson()
	{
		var members = new List<object>();
		foreach (var m in Library.All)
			members.Add(Tree.Dict(
				("owner", m.Owner), ("name", m.Name), ("kind", m.Kind), ("signature", m.Signature), ("returns", m.Returns), ("doc", m.Doc), ("example", m.Example),
				("params", m.Params.Select(p => (object)Tree.Dict(("name", p.Name), ("type", p.Type), ("optional", p.Optional), ("rest", p.Rest))).ToList())));
		var enums = new Dictionary<string, object>();
		foreach (var e in Library.EnumDocs.Keys) enums[e] = Library.EnumValues(e).Cast<object>().ToList();
		var enumDocs = new Dictionary<string, object>();
		foreach (var kv in Library.EnumDocs) enumDocs[kv.Key] = kv.Value;
		return Tree.Dict(
			("members", members), ("enums", enums), ("enumDocs", enumDocs),
			("hooks", Hooks.Select(h => (object)Tree.Dict(("name", h.Name), ("key", h.Key), ("condition", h.Condition), ("summary", h.Summary))).ToList()),
			("beatArgs", BeatArgs.Select(a => (object)Tree.Dict(("name", a.Name), ("type", a.Type), ("summary", a.Summary))).ToList()),
			("headerArgs", HeaderArgs.Select(a => (object)Tree.Dict(("name", a.Name), ("type", a.Type), ("summary", a.Summary))).ToList()),
			("keywords", Keywords.Cast<object>().ToList()), ("types", Types.Cast<object>().ToList()),
			("objects", Tree.List("world", "flags", "player")));
	}

	// The wiki's Library-Reference page, written from the table.
	public static string ReferenceMarkdown()
	{
		var sb = new StringBuilder();
		sb.Append("<!-- Generated by `CampaignTool docs` from the library table (tools/CampaignTool/Library.cs). Do not edit by hand. -->\n\n");
		sb.Append("# Library reference\n\nEvery name the language gives you, with what it does and an example. ");
		sb.Append("Anything in `world`, `flags` and the beat and brigade types is a **member**: write it after a dot. ");
		sb.Append("The rest are **functions** you can call anywhere.\n\n");
		sb.Append("Types: `int` is a whole number, `float` any number, `string` text, `bool` a condition or true/false. ");
		sb.Append("A *Days* is a length of time, `days(10)`; a *Date* is `date(1816, 6, 19)`. ");
		sb.Append("*effect* means the call changes the world, so it belongs in an effect hook (`OnActivate` ...) and not in a condition.\n\n");
		sb.Append("## Beat header\n\n| Argument | Type | What it is |\n|---|---|---|\n");
		foreach (var a in BeatArgs) sb.Append($"| `{a.Name}` | `{a.Type}` | {a.Summary} |\n");
		sb.Append("\n## Campaign header\n\n| Argument | Type | What it is |\n|---|---|---|\n");
		foreach (var a in HeaderArgs) sb.Append($"| `{a.Name}` | `{a.Type}` | {a.Summary} |\n");
		sb.Append("\n## Hooks\n\n| Hook | Kind | What it does |\n|---|---|---|\n");
		foreach (var h in Hooks) sb.Append($"| `{h.Name}()` | {(h.Condition ? "condition (`=>`)" : "effects (`{ }`)")} | {h.Summary} |\n");

		void Section(string title, string intro, IEnumerable<Member> ms)
		{
			var list = ms.ToList();
			if (list.Count == 0) return;
			sb.Append($"\n## {title}\n\n{intro}\n\n");
			foreach (var m in list)
			{
				sb.Append($"### `{m.Full}{(m.Kind == "function" ? "(" + string.Join(", ", m.Params.Select(p => p.Name + (p.Optional ? "?" : "") + (p.Rest ? "..." : ""))) + ")" : "")}`\n\n");
				sb.Append($"`{m.Signature}`\n\n{m.Doc}\n\n```c\n{m.Example}\n```\n\n");
			}
		}
		Section("Functions", "Called by name, anywhere.", Library.Of("").Where(m => m.Kind == "function"));
		Section("world", "The state of the world: asked in conditions, changed in effects.", Library.Of("world"));
		Section("flags", "Named switches the campaign raises and lowers.", Library.Of("flags"));
		Section("player", "What the player has done, as the campaign has been told of it: the game records a *deed* when the player brokers a truce (`truce`), leads or joins a raid (`raid`), rides a brigade to its landing (`escort`) or carries a packet through (`courier`).", Library.Of("player"));
		Section("A beat", "A beat's own name is a value: `SevenOaks.done`. Beats are written in `[...]` after the header to be required, and passed to `trigger(...)`.", Library.Of("Beat"));
		Section("A brigade", "`brigade(\"name\")` is one of the runs in `Data/World/brigade_runs.json`.", Library.Of("Brigade"));

		sb.Append("\n## Named values\n\n");
		foreach (var kv in Library.EnumDocs)
			sb.Append($"- **`{kv.Key}`**: {kv.Value}\n" + (kv.Key == "Item" ? "" : "  Values: " + string.Join(", ", Library.EnumValues(kv.Key).Select(v => $"`{kv.Key}.{v}`")) + "\n"));
		sb.Append($"\nItems: {string.Join(", ", Library.EnumValues("Item").Select(v => $"`{v}`"))}.\n");
		return sb.ToString();
	}
}
