using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Economy;

// The barter (design doc 3.6.1): trade is the core fantasy, so a trade is a
// session rather than a shop. You open with a gift, you read what you can
// actually see of the other man's position, you offer and he counters, and
// every push spends his patience. Push past it and he walks, possibly across
// the river. What you can read of him is the skill; the numbers only follow.
public class Barter
{
	private const float ShipDay = 232f;         // 20 August, as the post counts it

	public readonly Npc With;
	public readonly Inventory Counter;
	public readonly FortLedger Post;            // null at a camp
	public readonly BandLedger Band;            // null at a post
	public readonly Player.PlayerController Player;
	public readonly bool Crude;                 // no words between you: signs and fingers

	public float Patience = 1f;
	public int Presses;
	public float Margin;                        // the price moved your way, as a fraction
	public bool GiftGiven;
	public bool Walked;
	public bool HeldBackGuns;                   // you kept the muskets off the table
	public readonly List<string> Tells = new();

	// A man who has been pushed too hard will not sit down again the same day.
	private static readonly Dictionary<Npc, float> Sulking = new();

	// A new world, or a loaded one: nobody is sulking yet.
	public static void Forget() => Sulking.Clear();

	public static bool WillSitDown(Npc npc, out float hoursLeft)
	{
		hoursLeft = 0f;
		// Men who have died since do not sulk.
		foreach (var gone in new List<Npc>(Sulking.Keys)) if (!GodotObject.IsInstanceValid(gone) || gone.IsDead) Sulking.Remove(gone);
		if (npc == null || !Sulking.TryGetValue(npc, out float until)) return true;
		float now = Now();
		if (now >= until) { Sulking.Remove(npc); return true; }
		hoursLeft = until - now;
		return false;
	}

	private static float Now()
	{
		var cal = GameCalendar.Instance;
		return cal == null ? 0f : cal.Day * 24f + cal.Hour;
	}

	public Barter(Player.PlayerController player, Npc npc, Inventory counter, FortLedger post, BandLedger band)
	{
		Player = player; With = npc; Counter = counter; Post = post; Band = band;
		Crude = npc.IsBandPerson && player.FluencyWith(npc) < 0.5f;
		Margin = 0.08f * player.Skills.Trade + Mathf.Clamp(npc.RapportWith(player), -40f, 60f) / 500f;
		if (Crude) Margin -= 0.08f;
		Read();
	}

	// ---- what you can see of him -------------------------------------------

	// How badly he wants what you are carrying, 0..1. A hungry camp, a post
	// short of its quota with the ship coming, a band at odds with its
	// neighbours and asking after guns.
	public float Need()
	{
		float need = 0f;
		if (Band != null)
		{
			float days = Band.FoodDays(Band.Mouths);
			if (days < 10f) need += Mathf.Clamp((10f - days) / 10f, 0f, 1f) * 0.7f;
			if (Band.AtOddsWithOtherBand && Player.Inventory.Has(ItemId.Musket)) need += 0.4f;
		}
		if (Post != null && Post.QuotaMb > 0f)
		{
			float doy = GameCalendar.Instance?.DayOfYear ?? 0f;
			float short_ = Mathf.Clamp(1f - Post.FursInStoreMb / Mathf.Max(1f, Post.QuotaMb), 0f, 1f);
			float pressure = doy > ShipDay ? 0f : Mathf.Clamp(1f - (ShipDay - doy) / 180f, 0f, 1f);
			need += short_ * pressure * 0.8f;
			if (Post.FoodDays(8) < 12f) need += 0.2f;
		}
		return Mathf.Clamp(need, 0f, 1f);
	}

	// The tells (design doc 3.5, 3.6.1): what the player can perceive, said as
	// a thing seen and not as a number. A sharper trader notices more, and
	// without the words you only get what the body shows.
	private void Read()
	{
		float skill = Player.Skills.Trade;
		float rapport = With.RapportWith(Player);
		bool sharp = skill >= 0.35f || rapport > 30f;
		bool sharper = skill >= 0.6f || rapport > 55f;

		if (Band != null)
		{
			float days = Band.FoodDays(Band.Mouths);
			if (days < 4f) Tells.Add("The children at the fire looked thin as you came in.");
			else if (days < 10f && sharp) Tells.Add("There is little hanging in the smoke for the time of year.");
			if (Band.AtOddsWithOtherBand) Tells.Add("He asks after guns and ball before he looks at anything else.");
			if (Band.DebtNwcMb > 8f && sharp && !Crude) Tells.Add("He lets slip he is already in the Nor'Westers' book.");
			if (Band.RumTaste > 40f && sharp) Tells.Add("His eyes keep going to the kegs.");
			if (Band.StandingWithCompany < 25f) Tells.Add("He has not forgotten how the Company last dealt with his people.");
			if (Band.Stores.Count(ItemId.BeaverPelt) + Band.Stores.Count(ItemId.CaribouHide) > 20 && sharper)
				Tells.Add("There are more packs behind him than he means you to count.");
		}
		if (Post != null)
		{
			float doy = GameCalendar.Instance?.DayOfYear ?? 0f;
			if (doy <= ShipDay && Post.FursInStoreMb < Post.QuotaMb * 0.7f)
				Tells.Add(sharp ? $"He glances at the calendar: {(int)(ShipDay - doy)} days to the ship, and the warehouse not full." : "He glances at the calendar while he talks.");
			if (Post.FoodDays(8) < 12f && sharp) Tells.Add("The stores are thin and he knows it.");
			if (Post.RaidedRecently(GameCalendar.Instance?.Day ?? 0)) Tells.Add("He is short with you; the other house was at his door lately.");
			if (Post.SkimmedMb > 10f && sharper) Tells.Add("The book in front of him does not quite add up.");
		}
		if (With.Personality.Ambition > 0.65f && sharper) Tells.Add("He is a hard man over a price and makes no secret of it.");
		if (With.Needs.Health < 60f) Tells.Add("He is hurt, and wants this done with.");
		if (With.Needs.Hunger < 40f && sharp) Tells.Add("He is hungry and keeps looking at your pack.");
		if (Tells.Count == 0) Tells.Add(Crude ? "Without the words you can read only his face, and it tells you nothing." : "He gives you nothing: no hurry, no want.");
		if (!Crude && !GiftGiven && Band != null) Tells.Add("Nothing has been given and no pipe filled. He notices.");
	}

	// ---- the prices --------------------------------------------------------

	private float Swing => 1f + Mathf.Clamp(Margin, -0.45f, 0.75f);

	// Prices are for a lot, not a piece: a pelt at a time hides the haggling
	// in the rounding, and a lot is how the trade was actually done.
	public int SellPrice(ItemId id, int n = 1)
	{
		var def = Items.Items.Of(id);
		float price = Post != null ? Post.PeltPrice : 1f;
		float want = def.Kind == ItemKind.Food && Need() > 0.2f ? 1f + Need() * 0.5f : 1f;
		return Mathf.Max(n, Mathf.RoundToInt(def.ValueMb * n * price * Swing * want));
	}

	public int BuyPrice(ItemId id, int n = 1)
	{
		var def = Items.Items.Of(id);
		float mult = def.Kind == ItemKind.Food ? (Post?.FoodPriceMultiplier ?? 1.2f) : 1f;
		// What he is short of, he parts with dearly.
		if (def.Kind == ItemKind.Weapon && Band != null && Band.AtOddsWithOtherBand) mult *= 1.3f;
		int cost = Mathf.Max(n, Mathf.CeilToInt(def.ValueMb * n * mult / Swing));
		// Without the words you pay for the misunderstanding - a part more on
		// the lot, not a piece more on every article.
		return Crude ? cost + Mathf.Max(1, Mathf.RoundToInt(cost * 0.2f)) : cost;
	}

	// ---- the rounds --------------------------------------------------------

	// Open with a gift and a pipe. The ceremony came before the goods, and
	// skipping it is noticed (design doc 3.6.1).
	public string Gift(ItemId id)
	{
		if (!Player.Inventory.Remove(id)) return "You have none to give.";
		var def = Items.Items.Of(id);
		Counter.Add(id);
		GiftGiven = true;
		Margin += 0.05f + def.ValueMb * 0.01f;
		Patience = Mathf.Min(1.3f, Patience + 0.25f);
		With.ChangeRapport(Player, 4f + def.ValueMb);
		With.GiftsFromPlayerMb += def.ValueMb;
		Tells.RemoveAll(t => t.StartsWith("Nothing has been given"));
		Chronicle.Event($"{Player.DisplayName} opened the trade with a gift to {With.DisplayName}.", Player.GlobalPosition, Player, With.DisplayName, 0.6f);
		return Band != null
			? $"You put {def.Name.ToLower()} in his hands before a word of trade. He takes his time over it, and the talk comes easier."
			: $"He pockets the {def.Name.ToLower()} without remarking on it, and finds he can do a little better on the price.";
	}

	// Press him for a better price. Skill, standing and his own need carry it;
	// each round spends patience whether it lands or not.
	public string Press()
	{
		if (Walked) return "";
		Presses++;
		float chance = 0.42f + 0.4f * Player.Skills.Trade
			+ Mathf.Clamp(With.RapportWith(Player), -40f, 60f) / 400f
			+ Need() * 0.3f + (GiftGiven ? 0.08f : -0.05f)
			- Presses * 0.15f - (Crude ? 0.12f : 0f)
			- With.Personality.Ambition * 0.1f;
		bool landed = GD.Randf() < Mathf.Clamp(chance, 0.03f, 0.92f);
		Patience -= landed ? 0.22f : 0.34f;
		Player.Skills.Learn(ref Player.Skills.Trade, 0.012f);

		if (Patience <= 0f)
		{
			Walked = true;
			Sulking[With] = Now() + 6f;
			With.ChangeRapport(Player, -5f);
			if (Band != null) Band.OnInsult("haggled at past all patience");
			Chronicle.Event($"{With.DisplayName} broke off the trade with {Player.DisplayName}.", Player.GlobalPosition, Player, With.DisplayName, 0.8f);
			return Band != null
				? "He gathers his furs, says a short word you do not need translated, and walks. The Nor'Westers' house is across the river, and he knows the way."
				: "\"That will do. The counter is closed to you today.\" He turns his back and you have had the last of him for six hours.";
		}
		if (landed)
		{
			Margin += 0.07f + 0.05f * Player.Skills.Trade;
			return Patience < 0.35f
				? "He gives a little more, but his hand is on the packs. One more word and he is gone."
				: "He chews it over, allows that your furs are better than most, and moves the price.";
		}
		return Patience < 0.35f
			? "He will not move, and he is done being asked."
			: "He shakes his head. \"That is the price.\"";
	}

	// Meeting him halfway buys back some patience at the cost of the margin.
	public string Relent()
	{
		Margin -= 0.05f;
		Patience = Mathf.Min(1.2f, Patience + 0.3f);
		Presses = Mathf.Max(0, Presses - 1);
		With.ChangeRapport(Player, 1.5f);
		return "You allow that he has a point, and the two of you settle back down to it.";
	}

	// Holding back (design doc 3.6.1): the guns stay in the pack, whatever he
	// offers. The band remembers it either way.
	public string HoldBackGuns()
	{
		HeldBackGuns = true;
		if (Band != null)
		{
			Band.StandingWithCompany = Mathf.Max(0f, Band.StandingWithCompany - 5f);
			With.ChangeRapport(Player, -6f);
			Band.LastInsultCause = "would not trade guns while we are at war";
		}
		Reveal.Lands(Player, $"{With.DisplayName}'s people know you will not arm them.", "you kept the muskets off the table", 1f);
		return "You tell him the muskets are not for trade. He looks at you a long moment and says nothing at all.";
	}

	public string Mood()
	{
		if (Patience > 0.85f) return "He has all day.";
		if (Patience > 0.6f) return "He is still easy about it.";
		if (Patience > 0.35f) return "He is beginning to shift his feet.";
		return "He has had about enough of this.";
	}

	// What the player knows of how the price stands, in words.
	public string Standing()
	{
		if (Margin > 0.3f) return "You have him well under his own price.";
		if (Margin > 0.1f) return "You have talked him down a little.";
		if (Margin > -0.05f) return "He is asking his own price.";
		return "He is asking more of you than he would of another man.";
	}
}
