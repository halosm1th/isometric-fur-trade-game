using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Economy;

// The Governor and Committee in London (design doc 10.7), as the simplest
// possible background agent: they see the returns when the ship gets home,
// and write next year's orders off them. Short returns buy a higher
// standard of trade (pay more for pelts); fat returns let them tighten it.
// The Committee never sees the country, only the numbers - which is the
// point. But it does see the numbers: the furs landed against what York's
// own books say went aboard. A Factor who shades the hold (World/Ship) is
// found out in time - the bigger the gap, the sooner - and is warned, and
// then recalled (design doc 3.9 step 8: the lie resolves at the reading).
public static class LondonCommittee
{
    public static bool Automatic = true;
    // What of the shaded furs London has already looked at, how often it has
    // found the books short, and whether the Factor is to come home.
    public static float SkimReviewed;
    public static int Suspicions;
    public static bool Recalled;

    public static void ReviewReturns(FortLedger fort, float shippedMb)
    {
        if (!Automatic) return;
        float ratio = fort.QuotaMb > 0f ? shippedMb / fort.QuotaMb : 1f;
        float price = fort.LondonIncentiveNext;
        float quota = fort.QuotaNextMb;
        string verdict;
        if (ratio < 0.6f)
        {
            price *= 1.35f; quota *= 0.9f;
            verdict = "The Committee is displeased with the returns and orders a more liberal standard of trade to draw the Indians in.";
        }
        else if (ratio < 0.9f)
        {
            price *= 1.15f;
            verdict = "The Committee notes the shortfall and allows a little more for beaver.";
        }
        else if (ratio > 1.4f)
        {
            price *= 0.85f; quota *= 1.25f;
            verdict = "The Committee is gratified by the returns, trims the standard of trade, and expects more next year.";
        }
        else
        {
            quota *= 1.1f;
            verdict = "The Committee approves the returns and looks for a modest increase.";
        }
        fort.LondonIncentiveNext = Mathf.Clamp(price, 0.3f, 3f);
        fort.QuotaNextMb = Mathf.Clamp(quota, 10f, 400f);
        verdict += Audit(fort, shippedMb);
        Sim.Notice($"(In London, the ship home:) {verdict} The orders that will sail next year: {fort.LondonIncentiveNext:0.00} MB per MB, quota {fort.QuotaNextMb:0}.");
    }

    // The furs landed against York's account of them: what went missing since
    // the last reading is seen with a chance that grows with its share of the
    // returns. Once is a stiff letter; twice, or a theft too gross to miss,
    // and the Factor is called home on the next ship.
    private static string Audit(FortLedger fort, float shippedMb)
    {
        float missing = fort.SkimmedMb - SkimReviewed;
        SkimReviewed = fort.SkimmedMb;
        if (missing <= 0f) return "";
        float share = missing / Mathf.Max(1f, shippedMb + missing);
        if (GD.Randf() > Mathf.Clamp(share * 5f, 0.1f, 0.9f)) return "";
        Suspicions++;
        Agents.Chronicle.Event($"The Committee found the furs landed short of York's account by {missing:0} made beaver.", World.IsoGrid.ToWorld(World.WorldLayout.GateOutside), fort.Leader, fort.Leader?.DisplayName, 2f);
        if (Suspicions >= 2 || share > 0.2f)
        {
            Recalled = true;
            return $" The furs landed are short of the York account by {missing:0} made beaver, and not for the first time: the Factor is to come home on the ship to answer for it.";
        }
        if (fort.Leader != null) fort.Leader.Standing -= 25f;
        return $" The furs landed are short of the York account by {missing:0} made beaver. The Committee requires the Factor's explanation by the next ship.";
    }
}
