using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// The goose hunt (design doc 14.1, 16.1): the autumn flight is the one event
// in the year a Bay post cannot afford to miss. For a fortnight or three the
// whole establishment turns out to the marsh, the women salt the birds down by
// the cask, and what is in the cellar when the flight passes over is what the
// post eats until the geese come back. A bad autumn is a hungry February, and
// the hungry February is three months later - which is the point.
//
// The spring flight is smaller and comes when the cellar is empty, so it is
// relief rather than a stock.
public static class GooseHunt
{
	// 20 August to 15 October; spring, 5 May to 30 May.
	private const int AutumnFrom = 232, AutumnTo = 288;
	private const int SpringFrom = 125, SpringTo = 150;

	public static bool Autumn
	{
		get { float d = GameCalendar.Instance?.DayOfYear ?? 0f; return d >= AutumnFrom && d <= AutumnTo; }
	}

	public static bool Spring
	{
		get { float d = GameCalendar.Instance?.DayOfYear ?? 0f; return d >= SpringFrom && d <= SpringTo; }
	}

	public static bool Season => Autumn || Spring;

	// Days left of the flight, for the orders and the countdown.
	public static int DaysLeft
	{
		get
		{
			float d = GameCalendar.Instance?.DayOfYear ?? 0f;
			if (Autumn) return Mathf.CeilToInt(AutumnTo - d);
			if (Spring) return Mathf.CeilToInt(SpringTo - d);
			return 0;
		}
	}

	// Casks salted this season, by house, and what the house was asked for.
	private static readonly Dictionary<Household, int> _casks = new();
	private static readonly Dictionary<Household, int> _birds = new();
	private static int _seasonYear = -1;
	private static bool _opened, _closed;

	public static void Salted(Household home, int casks)
	{
		if (home == null) return;
		_casks[home] = _casks.GetValueOrDefault(home) + casks;
		_birds[home] = _birds.GetValueOrDefault(home) + casks * 8;
	}

	public static int CasksThisSeason(Household home) => home == null ? 0 : _casks.GetValueOrDefault(home);

	// What a house ought to lay in: a cask is eight birds and about seven
	// man-days, and the aim is the hard part of the winter - some three months
	// of the five between the flight going over and the geese coming back -
	// with the deer that pass, what comes through the ice, and the bands'
	// provisions expected to make up the rest. A post never did live on geese
	// alone, and a target it could not reach would only read as failure.
	public static int Wanted(Household home)
	{
		int mouths = 0;
		foreach (var n in Sim.Npcs) if (n.HomeStores == home && !n.IsDead) mouths++;
		return Mathf.Max(4, Mathf.RoundToInt(mouths * 90f * 33.6f / Items.Items.Of(ItemId.SaltGeese).Nutrition));
	}

	// How long the cellar would carry the house if nothing else came in.
	public static float WeeksInHand(Household home)
	{
		int mouths = 0;
		foreach (var n in Sim.Npcs) if (n.HomeStores == home && !n.IsDead) mouths++;
		return home == null || mouths == 0 ? 0f : home.FoodDays(mouths) / 7f;
	}

	// Called every day from the world's tick: opens the flight, counts it down,
	// and closes the books on it when the birds go over.
	public static void Tick()
	{
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		if (cal.Year < _seasonYear) Reset();   // a loaded save put the clock back

		if (Autumn && !_opened)
		{
			_opened = true; _closed = false;
			// The passage itself: for these weeks the marsh holds every bird
			// going south, which is how a post took its thousands.
			if (Sim.Ecology != null)
				foreach (var site in Sim.Ecology.Sites)
				{
					if (site.Species != Ecology.Species.Goose || site.IsDead) continue;
					site.Count = site.Capacity * 8f;
					Sim.Notice($"The flight is on {site.Name}: the water black with geese.");
				}
			if (cal.Year != _seasonYear) { _casks.Clear(); _birds.Clear(); _seasonYear = cal.Year; }
			foreach (var post in Sim.Posts())
			{
				if (post == null) continue;
				int want = Wanted(post);
				Sim.Headline($"The geese are on the marsh. {post.Leader?.DisplayName ?? post.Title} turns the post out: {want} casks in the cellar before the flight goes over, or we eat short.");
				break;   // one headline is enough; every house hears its own master
			}
		}
		else if (Autumn && _opened)
		{
			// A word every few days on how the flight is going, which is the
			// one number at a Bay post everybody watched.
			if (cal.Day % 3 == 0)
				foreach (var post in Sim.Posts())
				{
					if (post == null || post != Sim.Fort) continue;
					int birds = post.Stores.Count(ItemId.Goose), casks = post.Stores.Count(ItemId.SaltGeese);
					Sim.Notice($"The flight: {casks} casks down, {birds} birds on the floor, {DaysLeft} days of geese left (wanted {Wanted(post)}).");
				}
		}
		else if (!Autumn && _opened && !_closed)
		{
			_opened = false; _closed = true;
			if (Sim.Ecology != null)
				foreach (var site in Sim.Ecology.Sites)
				{
					if (site.Species != Ecology.Species.Goose || site.IsDead) continue;
					site.Count = Mathf.Min(site.Count, site.Capacity * 0.4f);
				}
			Sim.Notice("The flight has passed over; only the late birds are left on the marsh.");
			CloseTheBooks();
		}
	}

	// The reckoning: what went into the cellar against what was wanted, said
	// in weeks of winter rather than numbers, and set down in the journal
	// (design doc P.4) so February has a cause the player can point at.
	private static void CloseTheBooks()
	{
		foreach (var post in Sim.Posts())
		{
			if (post == null) continue;
			int got = CasksThisSeason(post);
			int want = Wanted(post);
			float weeks = WeeksInHand(post);
			string verdict = got >= want ? "a good autumn"
				: got >= want / 2 ? "a thin autumn"
				: got > 0 ? "a bad autumn" : "no autumn at all";
			Sim.Notice($"The flight is over. {post.Title}: {got} casks salted of {want} wanted - {verdict}, {weeks:0} weeks' food in hand.");
			foreach (var p in Sim.Players())
			{
				if (p == null || p.IsDead || p.HomePost != post && Sim.Fort != post) continue;
				Reveal.Word(p, $"{post.Title} has {got} casks of salt geese in the cellar of the {want} wanted: {weeks:0} weeks' food.",
					got >= want ? "the men were at the marsh while the flight lasted" : "the flight went over with the work half done",
					got >= want ? 1.2f : 1.8f);
			}
			if (got < want / 2) Sim.Headline($"{post.Title} goes into the winter short: {got} casks where {want} were wanted.");
		}
	}

	// A line for the HUD and the journal while the flight is on.
	public static string Countdown(Household home)
	{
		if (!Season) return "";
		int got = CasksThisSeason(home);
		int want = Wanted(home);
		return Autumn
			? $"The flight: {got}/{want} casks salted, {DaysLeft} days of geese left."
			: $"The spring flight: {DaysLeft} days of geese.";
	}

	public static void Reset() { _casks.Clear(); _birds.Clear(); _opened = _closed = false; _seasonYear = -1; }
}
