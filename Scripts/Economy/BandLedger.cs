using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// A band's cache at its camp (design doc 6, 7.2): the Cree by the river,
// the Assiniboine up-country. Each keeps its own standing and debt with
// each company, its own seasonal round, and its own camp.
public partial class BandLedger : Household
{
	public Agents.Faction Nation = Agents.Faction.Cree;
	public string Name = "the band";
	public Vector2I Camp = WorldLayout.WinterCamp;
	public Vector2I WinterGround = WorldLayout.WinterCamp, SummerGround = WorldLayout.SummerCamp;
	public bool IsCree => Nation == Agents.Faction.Cree;
	// How this band stands with the other nation (design doc 7.2). Allies of
	// old, but the same herd and the same lodges wear on it.
	public float StandingWithOtherBand = 65f;
	public bool AtOddsWithOtherBand => StandingWithOtherBand < 25f;
	public BandLedger OtherBand => IsCree ? Sim.Assiniboine : Sim.Band;
	public IEnumerable<Vector2I> Tipis() => WorldLayout.TipiCellsAt(Camp);
	public int Mouths { get { int m = 0; foreach (var n in Sim.Npcs) if (n.HomeStores == this && !n.IsDead) m++; return m; } }
	// Plains provisioning (design doc 13.1): the Assiniboine cache fills with
	// pemmican from the buffalo country while they winter upland.
	private float _hoursToWeek = 24f * 7f;
	// How the band feels about the Company: fed and fairly paid, it trades;
	// starved or shot at, it stops coming (design doc 3.6, 10.5).
	public float StandingWithCompany = 50f;
	// ...and with the Nor'Westers across the river.
	public float StandingWithNwc = 50f;
	public float DebtNwcMb;
	public bool WillTradeWith(FortLedger post) => post is NwcLedger ? StandingWithNwc > 20f : WillTrade;
	// What the band owes the post for goods advanced against next season's furs.
	public float DebtMb;
	// A letter from the Factor invites them in to trade.
	public bool InvitedToTrade;
	public bool WillTrade => StandingWithCompany > 20f;
	private int _lastDay = -1;

	public override void _Process(double delta)
	{
		base._Process(delta);
		var cal = GameCalendar.Instance;
		if (cal == null || !Net.Net.IsServer) return;
		PlainsProvisions(cal.DeltaGameHours);
		if (cal.Day == _lastDay) return;
		_lastDay = cal.Day;
		int mouths = 0;
		mouths = Mouths;
		bool hungry = FoodDays(mouths) < 3f;
		float price = Sim.Fort?.PeltPrice ?? 1f;
		// Hunger is blamed on the Company when its price is poor.
		float drift = hungry ? (price < 0.9f ? -3f : -1f) : (price >= 1.2f ? 1f : 0.3f);
		bool was = WillTrade;
		StandingWithCompany = Mathf.Clamp(StandingWithCompany + drift, 0f, 100f);
		if (was && !WillTrade) Sim.Notice($"{Name} have had enough of the Company. They will not bring their furs to the post.");
		if (!was && WillTrade) Sim.Notice($"{Name} are willing to trade with the post again.");
		StandingWithOtherBand = Mathf.Min(100f, StandingWithOtherBand + 0.3f);   // old alliances mend slowly
		// The Assiniboine's seasonal round: down to the Bay for high summer, home for the buffalo in autumn.
		if (!IsCree)
		{
			if (cal.Month == 7 && cal.Day == 1 && Camp != SummerGround) MoveCamp(SummerGround, $"{Name} have come down the river road and pitched their tipis on the trail below York Factory to trade.");
			if (cal.Month == 9 && cal.Day == 1 && Camp != WinterGround) MoveCamp(WinterGround, $"{Name} have gone back up-country for the buffalo.");
		}
	}

	// Pemmican from the plains (design doc 13.1): what the Assiniboine bring to the trade.
	private void PlainsProvisions(float hours)
	{
		if (IsCree) return;
		_hoursToWeek -= hours;
		if (_hoursToWeek > 0f) return;
		_hoursToWeek += 24f * 7f;
		if (Camp == WinterGround) { Stores.Add(ItemId.Pemmican, 14); Stores.Add(ItemId.CaribouHide, 1); }
	}

	// Rum in the camp (design doc 13.4): the band comes to want it, and the
	// post that has it gets the furs.
	public float RumTaste;
	public void OnRum(int drams) { RumTaste = Mathf.Min(100f, RumTaste + drams * 4f); }

	public void OnInsult() { StandingWithCompany = Mathf.Max(0f, StandingWithCompany - 4f); }

	public void OnMemberKilledBy(Agents.Agent killer)
	{
		if (killer == null) return;
		if (killer.Faction == Agents.Faction.HBC) StandingWithCompany = Mathf.Max(0f, StandingWithCompany - 25f);
		else if (killer.Faction == Agents.Faction.NWC) StandingWithNwc = Mathf.Max(0f, StandingWithNwc - 25f);
		else return;
		Sim.Notice($"{Name} will remember who killed one of theirs.");
	}

	public override void _Ready()
	{
		if (IsCree) { Title = "the band's cache"; Sim.Band = this; }
		else { Title = "the Assiniboine cache"; Name = "the Assiniboine"; Sim.Assiniboine = this; WinterGround = WorldLayout.AssiniboineWinterCamp; SummerGround = WorldLayout.AssiniboineSummerCamp; Camp = WinterGround; }
		Sim.Bands.Add(this);
		AccessCell = IsoGrid.Cell(Camp, 1, 1);
		WoodpileCell = IsoGrid.Cell(Camp, 2, -2);
		HearthCells.Add(Camp);
		if (!IsCree)
		{
			Stores.Add(ItemId.Pemmican, 40); Stores.Add(ItemId.CaribouHide, 6); Stores.Add(ItemId.Firewood, 20); Stores.Add(ItemId.BeaverPelt, 4);
			Stores.Add(ItemId.WolfPelt, 3); Stores.Add(ItemId.Kettle, 1); Stores.Add(ItemId.Water, 10); Stores.Add(ItemId.TradeGoods, 6);
			foreach (var st in WorldLayout.Sites) KnownSites.Add(st.Name);
			KnowsTheCountry = true;
			StandingWithCompany = 40f; StandingWithNwc = 45f;
			return;
		}

		Stores.Add(ItemId.Venison, 30);
		Stores.Add(ItemId.DriedGoose, 20);
		Stores.Add(ItemId.Firewood, 20);
		Stores.Add(ItemId.BeaverPelt, 6);
		Stores.Add(ItemId.TradeGoods, 4);
		Stores.Add(ItemId.SteelTrap, 8);
		Stores.Add(ItemId.Kettle, 2);
		Stores.Add(ItemId.Water, 12);
		foreach (var s in WorldLayout.Sites) KnownSites.Add(s.Name);   // it's their country
		KnowsTheCountry = true;
		var cal = GameCalendar.Instance;
		if (cal != null) cal.CalendarEvent += OnEvent;
		// Starting mid-season: the band is already where the season puts them.
		CallDeferred(nameof(InitialCamp));
	}

	private void InitialCamp()
	{
		if (!Net.Net.IsServer || Core.SaveGame.HasPending) return;
		int m = GameCalendar.Instance?.Month ?? 1;
		float day = GameCalendar.Instance?.Day ?? 1;
		bool summer = IsCree ? (m == 6 && day >= 5) || (m >= 7 && m <= 10) || (m == 11 && day < 10) : m == 7 || m == 8;
		if (summer) MoveCamp(SummerGround);
	}

	private void OnEvent(string name)
	{
		if (!Net.Net.IsServer) return;
		if (!IsCree) return;
		if (name == "Breakup") MoveCamp(SummerGround, "The band has struck camp and pitched their tipis on the trail beside the post for the trading season.");
		if (name == "FreezeUp") MoveCamp(WinterGround, "The band has gone back into the bush to their winter camp.");
	}

	// Strike camp (design doc 6): move the cache, the fire and everyone's
	// bed, repaint the tipis, and send the people walking.
	public void MoveCamp(Vector2I centre, string notice = null)
	{
		if (Camp == centre) return;
		var from = Camp;
		Camp = centre;
		if (IsCree) WorldLayout.SetCamp(centre);
		AccessCell = IsoGrid.Cell(centre, 1, 1);
		WoodpileCell = IsoGrid.Cell(centre, 2, -2);
		HearthCells.Clear(); HearthCells.Add(centre);
		Sim.Structures?.MoveCamp(Name, centre);
		var tipis = new System.Collections.Generic.List<Vector2I>(Tipis());
		int i = 0;
		bool far = IsoGrid.ToWorld(from).DistanceTo(IsoGrid.ToWorld(centre)) > 3500f;
		foreach (var n in Sim.Npcs)
		{
			if (n.HomeStores != this || n.IsDead) continue;
			n.BedCell = tipis[i++ % tipis.Count];
			n.WorkCell = IsoGrid.Cell(centre, (i % 3) - 1, 1 + i % 2);
			n.Brain.Clear(n);
			// A long move goes by the river road, tipis and all.
			n.Brain.Push(n, far ? new Agents.Goals.RoadGoal(n.WorkCell) : new Agents.Goals.GoToGoal(n.WorkCell, 120f));
		}
		if (notice != null) { Sim.Notice(notice); Agents.Chronicle.Event($"{Name} moved camp.", IsoGrid.ToWorld(centre), subject: Name, weight: 0.8f); }
	}
}
