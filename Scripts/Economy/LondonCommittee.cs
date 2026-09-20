using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Economy;

// The Governor and Committee in London (design doc 10.7), as the simplest
// possible background agent: they see the returns when the ship gets home,
// and write next year's orders off them. Short returns buy a higher
// standard of trade (pay more for pelts); fat returns let them tighten it.
// The Committee never sees the country, only the numbers - which is the
// point.
public static class LondonCommittee
{
    public static bool Automatic = true;

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
        Sim.Notice($"(In London, the ship home:) {verdict} The orders that will sail next year: {fort.LondonIncentiveNext:0.00} MB per MB, quota {fort.QuotaNextMb:0}.");
    }
}
