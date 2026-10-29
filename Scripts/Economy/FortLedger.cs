using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// York Factory's books: provisions, trade goods, furs, and the prices London
// tells the Factor to pay. Prices are in Made Beaver.
public partial class FortLedger : Household
{
    // Goods paid per MB of fur brought in. London sets it; the Factor applies
    // it. 1.0 is the "standard of trade"; higher buys more pelts and faster
    // draws the country down.
    public float PeltPrice = 1.0f;
    // What London will send next ship, applied on arrival.
    public float LondonIncentiveNext = 1.0f;
    public float QuotaMb = 60f;
    public float QuotaNextMb = 60f;

	// How much of a fur payment goes out as drink. The Nor'Westers lean on it.
	public virtual float RumShare => 0.15f;

	// Trade goods per MB of food when a band buys provisions from the post.
	public float FoodPriceMultiplier = 1.5f;

	// Running totals for the year (for the Factor's report and the HUD).
    public float FursTakenMb;
    // Furs the band carried to the other company instead (the Factor hears of it).
    public float LostToRivalMb;
    public float GoodsPaidMb;
    public int Year;

	// The rivalry's ledger (design doc 7.1): when this post was last robbed,
	// when it last sent men across, and what the raids have cost it.
	public int LastRaidedDay = -999;
	public int LastRaidDay = -999;
	public int LotsLostToRaids;
	// What the leader has given out, for the overdue check (design doc 3.9).
	public readonly List<Agents.Assignment> Assignments = new();
	// The Tier 2 directive as last read (design doc 10.1.2): locked between read events.
	public string DirectiveText = "No orders read yet: the post runs on last year's standard.";
	public int DirectiveReadDay = -999;
	public int DirectiveYear;
	// Furs a shading Factor has set aside for himself (design doc 3.9 step 8).
	public float SkimmedMb;
	// The other company's minutes have been read here (design doc 10.3).
    public bool KnowsRivalPlan;
    // What the Bay post believes about its inland post, from the last report (design doc 3.9 step 6).
    public int LastOutpostReportDay = -999;
    public string OutpostReportText = "";
    public float OutpostBelievedFursMb, OutpostBelievedFoodDays;
    public int LastSupplyDay = -999;
    public bool KnowsOutpostMasterless;
    public int LastPostingDay = -999;
    public bool RaidedRecently(int today) => today - LastRaidedDay >= 0 && today - LastRaidedDay < 6;

    // Which company this counter belongs to, for standing and debt.
    public virtual Agents.Faction Company => Agents.Faction.HBC;
    public virtual float BandStanding
    {
        get => Sim.Band?.StandingWithCompany ?? 50f;
        set { if (Sim.Band != null) Sim.Band.StandingWithCompany = value; }
    }
    public virtual float BandDebt
    {
        get => Sim.Band?.DebtMb ?? 0f;
        set { if (Sim.Band != null) Sim.Band.DebtMb = value; }
    }

    // Standing and debt are between this counter and a particular band (design doc 3.6, 13.3).
	// The Americans keep their own books with a band, apart from either British company's.
	public float StandingOf(BandLedger b) => b == null ? 50f : Company switch { Agents.Faction.NWC => b.StandingWithNwc, Agents.Faction.American => b.StandingWithAmericans, _ => b.StandingWithCompany };
	public void SetStanding(BandLedger b, float v) { if (b == null) return; if (Company == Agents.Faction.NWC) b.StandingWithNwc = v; else if (Company == Agents.Faction.American) b.StandingWithAmericans = v; else b.StandingWithCompany = v; }
	public float DebtOf(BandLedger b) => b == null ? 0f : Company switch { Agents.Faction.NWC => b.DebtNwcMb, Agents.Faction.American => b.DebtAmericanMb, _ => b.DebtMb };
	public void SetDebt(BandLedger b, float v) { if (b == null) return; if (Company == Agents.Faction.NWC) b.DebtNwcMb = v; else if (Company == Agents.Faction.American) b.DebtAmericanMb = v; else b.DebtMb = v; }

	// The post buys provisions off a band that has them - the Assiniboine's
	// pemmican (design doc 13.1). Goods per piece; only when the mess is short.
	public int BuyProvisions(Inventory seller, Inventory goodsTo, int mouths)
	{
		int bought = 0;
		if (FoodDays(mouths + 1) > 60f) return 0;
		while (seller.Has(ItemId.Pemmican) && Stores.Count(ItemId.TradeGoods) >= 1 && bought < 20)
		{
			// Two pieces of pemmican for a piece of goods.
			if (seller.Count(ItemId.Pemmican) < 2) break;
			seller.Remove(ItemId.Pemmican, 2); Stores.Add(ItemId.Pemmican, 2);
			Stores.Remove(ItemId.TradeGoods); goodsTo.Add(ItemId.TradeGoods);
			bought += 2; GoodsPaidMb += 1;
		}
		return bought;
	}

	public float FursInStoreMb
	{
		get
		{
			float mb = 0f;
			foreach (var (id, n) in Stores.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) mb += Items.Items.Of(id).ValueMb * n;
			return mb;
		}
	}

	public override void _Ready()
	{
		Title = "the Company's stores";
		Sim.Fort = this;
		AccessCell = IsoGrid.Cell(WorldLayout.Depot.Top, 2, 2);
		WoodpileCell = WorldLayout.WoodpileCell;
		HearthCells.Add(IsoGrid.Cell(WorldLayout.Depot.Top, 1, 1));
		HearthCells.Add(IsoGrid.Cell(WorldLayout.MensHouse.Top, 1, 1));

		// What a post holds at midwinter, after a ship in August.
		Stores.Add(ItemId.Pemmican, 60);
		Stores.Add(ItemId.DriedGoose, 120);
		Stores.Add(ItemId.Bread, 30);
		Stores.Add(ItemId.Ale, 20);
		Stores.Add(ItemId.Firewood, 40);
		Stores.Add(ItemId.TradeGoods, 90);
		Stores.Add(ItemId.Musket, 6);
		Stores.Add(ItemId.SteelTrap, 10);
		Stores.Add(ItemId.Kettle, 3);
		Stores.Add(ItemId.Water, 24);
		Stores.Add(ItemId.Rum, 12);
		Stores.Add(ItemId.PointBlanket, 12);
		Stores.Add(ItemId.BeaverPelt, 22);
		Stores.Add(ItemId.CaribouHide, 4);
		Stores.Add(ItemId.Tumpline, 4);
		Stores.Add(ItemId.Snowshoes, 4);
		Stores.Add(ItemId.FishingLine, 6);   // the winter's fresh food comes through the ice
		Stores.Add(ItemId.FishingNet, 2);
		Stores.Add(ItemId.IceChisel, 3);
		// The Standard of Trade's small goods, and tobacco for the pipe.
		Stores.Add(ItemId.Tobacco, 30);
		Stores.Add(ItemId.Beads, 20);
		Stores.Add(ItemId.Awl, 24);
		Stores.Add(ItemId.Stroud, 12);
		Stores.Add(ItemId.Powder, 20);
		Stores.Add(ItemId.Axe, 4);
		Stores.Add(ItemId.PackBasket, 2);
		Year = GameCalendar.Instance?.Year ?? 1811;

		// The post knows its goose grounds and the lodge nearest the fort;
		// the rest of the country it has to be told about, or stumble on.
		KnownSites.Add("Goose marsh");
		KnownSites.Add("Beaver lodge (upper)");
	}

	// A post lives on country provisions through the winter (design doc 13.1,
	// 17.1). Besides whatever the hunters bring in and what band parties sell
	// at the counter, the master sends out for meat when the store gets low:
	// a week's traffic of small purchases, in numbers, from any band that will
	// still deal with him. A post that has neither food nor goods says so.
	private float _hoursToProvisions = 24f * 7f;
	public override void _Process(double delta)
	{
		base._Process(delta);
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		_hoursToProvisions -= cal.DeltaGameHours;
		if (_hoursToProvisions > 0f) return;
		_hoursToProvisions += 24f * 7f;

		int mouths = 0;
		foreach (var n in Sim.Npcs) if (n.HomeStores == this && !n.IsDead) mouths++;
		if (mouths == 0 || FoodDays(mouths) > 25f) return;
		BandLedger from = null;
		foreach (var b in Sim.Bands) if (b != null && b.WillTradeWith(this) && b.Stores.Count(ItemId.Pemmican) + b.Stores.Count(ItemId.DriedMeat) > 10) { from = b; break; }
		if (from == null)
		{
			Sim.Notice($"{Title}: {FoodDays(mouths):0} days' food in the store and nobody selling provisions.");
			return;
		}
		int goods = Mathf.Min(Stores.Count(ItemId.TradeGoods), Mathf.Max(4, mouths / 2));
		if (goods <= 0) { Sim.Notice($"{Title}: no goods left to buy provisions with."); return; }
		int got = 0;
		for (int i = 0; i < goods; i++)
		{
			if (from.Stores.Remove(ItemId.Pemmican, 2)) { Stores.Add(ItemId.Pemmican, 2); got += 2; }
			else if (from.Stores.Remove(ItemId.DriedMeat, 2)) { Stores.Add(ItemId.DriedMeat, 2); got += 2; }
			else break;
			Stores.Remove(ItemId.TradeGoods); from.Stores.Add(ItemId.TradeGoods);
			GoodsPaidMb += 1;
		}
		if (got > 0) Sim.Notice($"{Title} bought {got} pieces of provisions from {from.Name} - {FoodDays(mouths):0} days' food in hand.");
	}

	// A Cree party sells furs at the counter. Returns the MB of goods paid.
	// Debt on the books is settled first (design doc 13.3).
	public float BuyFurs(Inventory seller, Inventory buyerGoodsTo, BandLedger band = null)
	{
		float paidMb = 0f;
		band ??= Sim.Band;
		if (band != null && DebtOf(band) > 0f)
		{
			float settled = 0f;
			foreach (var (id, n) in new List<(ItemId, int)>(seller.All()))
			{
				if (Items.Items.Of(id).Kind != ItemKind.Fur) continue;
				while (DebtOf(band) > 0f && seller.Remove(id))
				{
					float v = Items.Items.Of(id).ValueMb * PeltPrice;
					Stores.Add(id); FursTakenMb += Items.Items.Of(id).ValueMb;
					SetDebt(band, Mathf.Max(0f, DebtOf(band) - v)); settled += v;
				}
			}
			if (settled > 0f) Sim.Notice($"{band.Name}'s debt at {Title} is worked down by {settled:0.#} MB of furs; {DebtOf(band):0.#} MB still owing.");
		}
		// A fair deal is remembered too.
		if (band != null) SetStanding(band, Mathf.Min(100f, StandingOf(band) + (PeltPrice >= 1f ? 2f : 0.5f)));
		var furs = new List<(ItemId, int)>();
		foreach (var (id, n) in seller.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) furs.Add((id, n));
		foreach (var (id, n) in furs)
		{
			for (int i = 0; i < n; i++)
			{
				float worth = Items.Items.Of(id).ValueMb * PeltPrice;
				int goods = Mathf.RoundToInt(worth);
				if (goods < 1 && GD.Randf() < worth) goods = 1;
				if (Stores.Count(ItemId.TradeGoods) < goods) return paidMb; // the post is bought out
				seller.Remove(id);
				Stores.Add(id);
				// Part of the price in rum, when there's rum and the band takes it.
				int rum = 0;
				if (RumShare > 0f && Stores.Has(ItemId.Rum) && !Core.Laws.CompanyDry(Company) && !Core.Laws.Dry(band) && GD.Randf() < RumShare * 2f) { rum = Mathf.Min(goods, Mathf.Max(1, Mathf.RoundToInt(goods * RumShare * 2f))); }
				Stores.Remove(ItemId.TradeGoods, goods - rum);
				buyerGoodsTo.Add(ItemId.TradeGoods, goods - rum);
				if (rum > 0)
				{
					rum = Stores.TakeUpTo(ItemId.Rum, rum);
					buyerGoodsTo.Add(ItemId.Rum, rum);
					// Drink buys goodwill quickly and cheaply - that's the trap.
					SetStanding(band, Mathf.Min(100f, StandingOf(band) + 1.5f * rum));
					band?.OnRum(rum);
				}
				paidMb += goods;
				FursTakenMb += Items.Items.Of(id).ValueMb;
				GoodsPaidMb += goods;
			}
		}
		return paidMb;
	}

	// A band buys food with trade goods; a band in good standing is
	// advanced food on credit against next season's furs (design doc 13.3).
	public int SellFood(Inventory buyer, int goodsToSpend, BandLedger band = null)
	{
		int sold = 0;
		band ??= Sim.Band;
		bool credit = band != null && StandingOf(band) > 40f && DebtOf(band) < 15f;
		int creditUnits = credit ? 5 : 0;
		while ((goodsToSpend > 0 && buyer.Has(ItemId.TradeGoods)) || creditUnits > 0)
		{
			bool onCredit = !(goodsToSpend > 0 && buyer.Has(ItemId.TradeGoods));
			if (onCredit) { creditUnits--; SetDebt(band, DebtOf(band) + 1f); }
			ItemId? food = null;
			foreach (var id in new[] { ItemId.DriedGoose, ItemId.DriedMeat, ItemId.Pemmican, ItemId.Bread })
				if (Stores.Count(id) > 20) { food = id; break; }   // keeps a reserve for its own men
			if (food == null) break;
			// One unit of goods buys a few days of food at the post's mark-up.
			int pieces = Mathf.Max(1, Mathf.RoundToInt(3f / FoodPriceMultiplier));
			if (!onCredit) { buyer.Remove(ItemId.TradeGoods); goodsToSpend--; }
			for (int i = 0; i < pieces && Stores.Remove(food.Value); i++) { buyer.Add(food.Value); sold++; }
		}
		return sold;
	}
}
