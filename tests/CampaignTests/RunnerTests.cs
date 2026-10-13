using System;
using System.Collections.Generic;
using System.Linq;
using CampaignTool;
using IsometricFurTradeGame.Scripts.Core;
using Xunit;
using static CampaignTests.U;

namespace CampaignTests;

// The beat lifecycle, played directly on the game's CampaignRunner.
public class RunnerTests
{
	private sealed class Host : ICampaignHost
	{
		public double Tension;
		public readonly List<string> Said = new();
		public double Metric(string name, string[] args) => name == "tension" ? Tension : double.NaN;
		public bool Predicate(string name, string[] args) => false;
		public void Effect(string name, Dictionary<string, object> action) => Said.Add(Tree.S(action.GetValueOrDefault("text")));
		public void Note(string text) { }
		public void NewYear(int year) { }
		public void Founded(string place) { }
	}

	private static Dictionary<string, object> Say(string text) => Act("say", ("text", text));

	private static (CampaignRunner runner, Host host) Make(Dictionary<string, object> doc, int day, Adherence level = Adherence.Steady)
	{
		var runner = new CampaignRunner { Level = level };
		runner.Load(CampaignScript.Parse(doc));
		var host = new Host();
		runner.Begin(day, host);
		return (runner, host);
	}

	private static void Days(CampaignRunner r, Host h, int from, int to) { for (int d = from; d <= to; d++) r.Tick(d, h); }
	private static int D(string iso) => CampaignLogic.DayNumber(iso);

	[Fact]
	public void OnAwakenRunsOnceAtTheMomentItWakes()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("when", L("tension>=50")), ("on_awaken", L(Say("woke")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-03-01"));
		Assert.Empty(h.Said);                       // not awake: the condition does not hold
		h.Tension = 60;
		Days(r, h, D("1814-03-02"), D("1814-06-01"));
		Assert.Equal(new[] { "woke" }, h.Said);     // woke once, not every day
	}

	[Fact]
	public void OnApproachRunsOnceInTheLeadUp()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("lead_days", 30.0), ("pressure", L(Say("looming")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-12-01"));
		Assert.Empty(h.Said);
		Days(r, h, D("1814-12-02"), D("1815-03-01"));
		Assert.Equal(new[] { "looming" }, h.Said);
	}

	[Fact]
	public void ActivateWhenHoldsAFixedBeatUntilItIsTrue()
	{
		var doc = Doc(B("a", "fixed", "1815-01-01", ("activate_when", L("tension>=50")), ("happens", L(Say("happened")))));
		var (r, h) = Make(doc, D("1814-06-01"));
		Days(r, h, D("1814-06-01"), D("1815-02-01"));
		Assert.Empty(h.Said);                       // due on the first, but the gate is shut
		h.Tension = 70;
		Days(r, h, D("1815-02-02"), D("1815-02-03"));
		Assert.Equal(new[] { "happened" }, h.Said);
	}

	[Fact]
	public void ActivateWhenAlsoGatesAnEventThatIsForced()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("window_days", 30.0), ("activate_when", L("tension>=50")), ("happens", L(Say("happened")))));
		var (r, h) = Make(doc, D("1814-06-01"), Adherence.Strict);
		Days(r, h, D("1814-06-01"), D("1815-06-01"));
		Assert.Empty(h.Said);                       // its grace has long run out, but the gate is shut
		h.Tension = 70;
		Days(r, h, D("1815-06-02"), D("1815-06-03"));
		Assert.Equal(new[] { "happened" }, h.Said);
		Assert.Equal("forced", r.Fired.Single().How);
	}

	[Fact]
	public void ResolveWhenSkipsTheBeatAndRunsOnResolve()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("done_when", L("tension>=90")), ("on_resolve", L(Say("resolved"))), ("happens", L(Say("happened")))));
		var (r, h) = Make(doc, D("1814-06-01"), Adherence.Strict);
		h.Tension = 95;
		Days(r, h, D("1814-06-01"), D("1816-01-01"));
		Assert.Equal(new[] { "resolved" }, h.Said);                      // never "happened", even at Strict
		Assert.Equal("by the world", r.Fired.Single().How);
	}

	[Fact]
	public void CancelWhenEndsTheBeatForGood()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("cancel_when", L("tension>=90")), ("on_cancel", L(Say("cancelled"))), ("happens", L(Say("happened")))));
		var (r, h) = Make(doc, D("1814-06-01"), Adherence.Strict);
		Days(r, h, D("1814-06-01"), D("1814-09-01"));
		h.Tension = 95;
		Days(r, h, D("1814-09-02"), D("1816-01-01"));
		Assert.Equal(new[] { "cancelled" }, h.Said);
		Assert.Empty(r.Fired);
		Assert.Contains("a", r.Cancelled);
	}

	[Fact]
	public void ARepeatingBeatWakesAgainEachRound()
	{
		var doc = Doc(B("a", "conditional", null, ("repeat_days", 30.0), ("on_awaken", L(Say("woke"))), ("happens", L(Say("happened")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-04-01"));
		// a round every thirty days: wakes, happens, rests, and wakes again
		Assert.Equal(Enumerable.Repeat(new[] { "woke", "happened" }, 4).SelectMany(x => x), h.Said);
	}

	[Fact]
	public void InitialVariablesStartWhereTheScriptSaid()
	{
		var doc = Doc(B("a", "conditional", null, ("when", L("var(mood) >= 3")), ("happens", L(Say("tense")))));
		doc["vars"] = Tree.Dict(("mood", 3.0));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-01-03"));
		Assert.Equal(new[] { "tense" }, h.Said);
	}

	[Fact]
	public void WhatHasWokenIsRememberedAcrossASaveAndLoad()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("when", L("tension>=50")), ("on_awaken", L(Say("woke")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		h.Tension = 60;
		Days(r, h, D("1814-01-01"), D("1814-01-10"));
		// a loaded game has Awakened and ReadySince restored, and so does not wake it a second time
		var (r2, h2) = Make(doc, D("1814-01-01"));
		foreach (var x in r.Awakened) r2.Awakened.Add(x);
		foreach (var kv in r.ReadySince) r2.ReadySince[kv.Key] = kv.Value;
		h2.Tension = 60;
		Days(r2, h2, D("1814-01-11"), D("1814-01-20"));
		Assert.Empty(h2.Said);
	}

	[Fact]
	public void ARumourIsToldOnlyWhileTheBeatIsAwakeAndNear()
	{
		var doc = Doc(B("a", "event", "1815-01-01", ("lead_days", 60.0), ("rumour", "Men are gathering at the Forks."), ("happens", L(Say("happened")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-06-01"));
		Assert.Empty(r.Stirring(D("1814-06-01")));                         // far off: nothing is said
		Days(r, h, D("1814-06-02"), D("1814-11-15"));
		var s = Assert.Single(r.Stirring(D("1814-11-15")));                  // inside the lead time
		Assert.Equal("Men are gathering at the Forks.", s.Rumour);
		Assert.Equal("approaching", s.Stage);
		Assert.InRange(s.DaysToDue, 40, 50);
		Days(r, h, D("1814-11-16"), D("1815-01-02"));
		Assert.Empty(r.Stirring(D("1815-01-02")) .Where(x => r.State.Done.Contains(x.Id)));
	}

	[Fact]
	public void ARumourlessBeatTellsNothing()
	{
		var doc = Doc(B("a", "conditional", null, ("delay_days", 10.0), ("happens", L(Say("x")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-01-03"));
		Assert.Empty(r.Stirring(D("1814-01-03")));
	}

	[Fact]
	public void ADatelessBeatIsTalkedOfOnceItWakes()
	{
		var doc = Doc(B("a", "conditional", null, ("when", L("tension>=50")), ("delay_days", 20.0), ("rumour", "The boats are late."), ("happens", L(Say("x")))));
		var (r, h) = Make(doc, D("1814-01-01"));
		Days(r, h, D("1814-01-01"), D("1814-02-01"));
		Assert.Empty(r.Stirring(D("1814-02-01")));
		h.Tension = 60;
		Days(r, h, D("1814-02-02"), D("1814-02-05"));
		Assert.Single(r.Stirring(D("1814-02-05")));
		Days(r, h, D("1814-02-06"), D("1814-03-01"));
		Assert.Empty(r.Stirring(D("1814-03-01")));                          // it happened: no longer a rumour
	}

	[Fact]
	public void ADeedRaisesAFlagAndCounts()
	{
		var st = new CampaignState();
		st.Deed("truce"); st.Deed("truce");
		Assert.Contains("deed:truce", st.Flags);
		Assert.Equal(2, st.Vars["deed:truce"]);
		Assert.True(CampaignLogic.Eval("deed:truce", st, new Host()));
		Assert.True(CampaignLogic.Eval("var(deed:truce) >= 2", st, new Host()));
		Assert.False(CampaignLogic.Eval("var(deed:truce) >= 3", st, new Host()));
	}
}
