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
}
