using System.Collections.Generic;
using System.Text;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// Boats at the bank (World/Canoe, World/Boatbuilding, World/Boatwrights):
// the menu of a boat on her bed or drawn up on the shore, and talk of canoes
// with the people who build them.
public partial class Conversation
{
    public Godot.Collections.Dictionary StartCanoe(Canoe canoe, string said = "")
    {
        if (_npc != null && GodotObject.IsInstanceValid(_npc)) _npc.Conversing = false;
        _npc = null; _scene = null; _stores = null; _pile = null; _canoe = canoe; _closed = false;
        _said = said ?? "";
        ShowCanoe();
        return State();
    }

    private string CanoeTitle => _canoe == null ? "" : _canoe.Building ? $"A {_canoe.Spec.Name} on the bed" : char.ToUpperInvariant(_canoe.Describe()[0]) + _canoe.Describe()[1..];

    private void ShowCanoe()
    {
        var c = _canoe;
        ClearOptions();
        if (c == null || !GodotObject.IsInstanceValid(c) || c.Wrecked) { Close(); return; }
        string said = _said != "" ? _said + "\n\n" : "";
        _said = "";
        if (c.Building) { ShowBed(c, said); return; }
        _text = said + BoatStatus(c);
        bool mine = c.Owner == "" || c.Owner == _player.DisplayName;
        if (_player.Aboard == null) AddOption(mine ? "Push off" : $"Push off in her (she is {c.Owner}'s)", () => { Close(); Net.Net.I?.Act("canoe"); });
        if (c.Bilge > 0.05f) AddOption(_player.Aboard == c ? "Bail her out" : "Tip the water out of her", () => { _said = Bail(c); ShowCanoe(); });
        AddOption("Load your goods and furs into her", () => { _said = _player.CanoeLoad(c, true); ShowCanoe(); });
        if (c.Cargo.SlotCount > 0) AddOption("Take everything out", () => { _said = _player.CanoeLoad(c, false); ShowCanoe(); });
        if (_player.Aboard == null && _player.Carrying == null)
            AddOption(c.Spec.Planked ? "Haul her over the portage on rollers" : "Shoulder her and carry her over the portage", () =>
            {
                string s = _player.ShoulderCanoe(c);
                if (_player.Carrying == c) { Close(); _player.Say(s); }
                else { _said = s; ShowCanoe(); }
            });
        if (_player.Aboard == null)
            foreach (var m in Boatbuilding.Mends(c))
            {
                var mm = m;
                AddOption($"Mend: {m.Label}", () =>
                {
                    string s = Boatbuilding.BeginMend(_player, c, mm.Key);
                    if (s == "") Close(); else { _said = s; ShowCanoe(); }
                });
            }
        if (mine && c.BoatName == "" && c.Owner == _player.DisplayName) AddOption("Give her a name...", () => ShowNaming(c));
        int spare = SpareFor(c);
        if (spare > 0) AddOption($"Stow mending stuff in her ({spare} lots of gum, bark, watap...)", () => { _said = StowMending(c); ShowCanoe(); });
        AddOption("Leave her", Close);
    }

    // What she is and how she is, in words a man at the bank would use.
    private string BoatStatus(Canoe c)
    {
        var sb = new StringBuilder();
        sb.Append(c.BoatName != "" ? $"{char.ToUpperInvariant(c.BoatName[0]) + c.BoatName[1..]}, a {c.Spec.Name}" : $"A {c.Spec.Name}");
        if (c.Owner != "") sb.Append(c.Owner == _player.DisplayName ? ", yours" : $", {c.Owner}'s");
        if (c.Builders != "") sb.Append($". Built {c.BuiltAt} by {c.Builders}: {Boats.Grade(c.Quality)} work");
        sb.Append(".\n");
        sb.Append($"Bark/planking {Boats.Condition(c.Part(HullPart.Skin))}, seams {Boats.Condition(c.Part(HullPart.Seams))}, ribs {Boats.Condition(c.Part(HullPart.Ribs))}, gunwales {Boats.Condition(c.Part(HullPart.Gunwales))}.");
        sb.Append(c.Leaks.Count == 0 ? " Tight." : $" She leaks at {c.Leaks.Count} place{(c.Leaks.Count == 1 ? "" : "s")}.");
        if (c.Bilge > 0.05f) sb.Append($" {(c.Bilge > 0.6f ? "Half full of water" : "Water in her bilge")}.");
        sb.Append($"\nShe carries {c.LoadLbs:0} of {c.CapacityLbs:0} lb{(c.Overloaded ? " and rides dangerously low" : "")}; {c.Spec.Seats} to paddle her{(c.Spec.Carriers > 1 ? $", {c.Spec.Carriers} to carry her" : "")}.");
        if (c.Cargo.SlotCount > 0) sb.Append(" In her: " + c.Cargo.Summary());
        if (c.Stock.SlotCount > 0) sb.Append("\nKept in her for mending: " + c.Stock.Summary());
        return sb.ToString();
    }

    private string Bail(Canoe c)
    {
        float was = c.Bilge;
        c.Bilge = _player.Aboard == c ? Mathf.Max(0f, c.Bilge - 0.5f) : 0f;
        _player.RequestSkip(_player.Aboard == c ? 0.25f : 0.1f, "");
        return was > 0.6f ? "You bail till your arms ache." : "You bail her out.";
    }

    private void ShowNaming(Canoe c)
    {
        ClearOptions();
        _text = "What will you call her?";
        var used = new HashSet<string>();
        foreach (var o in Sim.Spawner.Canoes) if (o.BoatName != "") used.Add(o.BoatName);
        int shown = 0;
        foreach (var n in Boatbuilding.Names)
        {
            if (used.Contains(n) || shown >= 8) continue;
            string nn = n; shown++;
            AddOption(char.ToUpperInvariant(n[0]) + n[1..], () => { c.BoatName = nn; _said = $"You cut the name into her thwart: {nn}."; _player.Journal.Add($"{GameCalendar.Instance?.DateString}: Named your {c.Spec.Name} {nn}."); ShowCanoe(); });
        }
        AddOption("Leave her nameless", ShowCanoe);
    }

    private static readonly ItemId[] MendingStuff = { ItemId.SpruceGum, ItemId.BirchBark, ItemId.Watap, ItemId.Cedar, ItemId.Oakum, ItemId.Pitch, ItemId.Plank, ItemId.Nails };
    private int SpareFor(Canoe c)
    {
        int n = 0;
        foreach (var id in MendingStuff) n += _player.Inventory.Count(id);
        return c.Launched ? n : 0;
    }
    private string StowMending(Canoe c)
    {
        int n = 0;
        foreach (var id in MendingStuff) { int k = _player.Inventory.Count(id); if (k > 0) { _player.Inventory.Remove(id, k); c.Stock.Add(id, k); n += k; } }
        return $"You stow {n} lots of mending stuff under her thwarts. You'll draw on it when you mend her.";
    }

    // ---- on the bed --------------------------------------------------------------

    private void ShowBed(Canoe c, string said)
    {
        var sb = new StringBuilder(said);
        var yard = Boatwrights.YardOf(c);
        sb.Append($"A {c.Spec.Name} on her bed {c.BuiltAt}");
        sb.Append(yard != null ? $" - the {yard.Name} people's work{(c.CommissionedBy != "" ? $", for {c.CommissionedBy}" : "")}." : c.Owner == _player.DisplayName ? " - yours." : $" - {c.Owner}'s.");
        sb.Append('\n');
        for (int i = 0; i < c.Spec.Stages.Length; i++)
        {
            var s = c.Spec.Stages[i];
            string mark = i < c.Stage ? $"done, {Boats.Grade(c.StageScores[i])}" : i == c.Stage ? (yard != null && c.WorkHours > 0f ? $"under way ({c.WorkHours:0}/{c.Spec.Hours[i]:0} h)" : "next") : "";
            sb.Append($"\n  {(i < c.Stage ? "x" : i == c.Stage ? ">" : "-")} {Boats.StageName(s)}{(mark != "" ? $" ({mark})" : "")}");
        }
        var helpers = Boatbuilding.HelpersAt(_player, c);
        if (c.NextStage is BoatStage next)
        {
            var needs = new List<string>();
            foreach (var (item, n) in Boatbuilding.NeedsFor(c, next)) needs.Add($"{Items.Items.Lot(item, n)} (by her {c.Stock.Count(item)}, in your pack {_player.Inventory.Count(item)})");
            sb.Append($"\n\nThe next stage wants: {(needs.Count > 0 ? string.Join("; ", needs) : "nothing but hands")}.");
            if (Boats.ToolFor(next) is { } tool) sb.Append($" And {tool.Why}.");
            string lacks = Boatbuilding.Lacks(_player, c, helpers);
            if (lacks != "") sb.Append($"\nStill wanted: {lacks}.");
        }
        if (helpers.Count > 0) sb.Append($"\nAt hand to help: {string.Join(", ", helpers.ConvertAll(h => h.DisplayName))}.");
        if (c.Stock.SlotCount > 0) sb.Append($"\nLaid by the bed: {c.Stock.Summary()}.");
        _text = sb.ToString();

        if (c.NextStage is BoatStage st)
        {
            AddOption($"{Boats.StageName(st)}{(helpers.Count > 0 ? $" (with {helpers.Count} helping)" : "")}", () =>
            {
                string s = Boatbuilding.BeginStage(_player, c);
                if (s == "") Close(); else { _said = s; ShowCanoe(); }
            });
            // What you carry that she still wants.
            int lay = 0;
            foreach (var stage in c.Spec.Needs) foreach (var (item, _) in stage) lay += _player.Inventory.Count(item);
            if (lay > 0) AddOption("Lay your bark, roots, gum and wood by the bed", () => { _said = LayBy(c); ShowCanoe(); });
            foreach (var h in helpers)
                if (yard != null && Boatwrights.IsBuilderFor(h, c) && h.Skills.Crafting > 0.2f)
                {
                    var hh = h;
                    AddOption($"Watch {h.DisplayName} at the work", () => { string s = Boatbuilding.BeginWatch(_player, hh, c); if (s == "") Close(); else { _said = s; ShowCanoe(); } });
                    break;
                }
        }
        if (yard == null && c.Owner == _player.DisplayName)
            AddOption("Break up the bed and take back what's laid by", () =>
            {
                foreach (var (id, n) in new List<(ItemId, int)>(c.Stock.All())) { c.Stock.Remove(id, n); _player.Inventory.Add(id, n); }
                Sim.Spawner.RemoveCanoe(c);
                _player.Say("You pull up the stakes. The work done is lost; what was laid by you take back.");
                Close();
            });
        AddOption("Leave it", Close);
    }

    private string LayBy(Canoe c)
    {
        var moved = new List<string>();
        foreach (var stage in c.Spec.Needs)
            foreach (var (item, _) in stage)
            {
                int k = _player.Inventory.Count(item);
                if (k <= 0) continue;
                // Only what she still wants, all stages told.
                int want = 0;
                for (int i = c.Stage; i < c.Spec.Stages.Length; i++) foreach (var (it, n) in c.Spec.Needs[i]) if (it == item) want += n;
                int give = Mathf.Min(k, Mathf.Max(0, want - c.Stock.Count(item)));
                if (give <= 0) continue;
                _player.Inventory.Remove(item, give); c.Stock.Add(item, give);
                moved.Add(Items.Items.Lot(item, give));
            }
        if (moved.Count == 0) return "She wants nothing more of what you carry.";
        if (Boatwrights.YardOf(c) != null)
            foreach (var a in Sim.AgentsWithin(c.GlobalPosition, 600f)) if (a is Npc n && Boatwrights.IsBuilderFor(n, c)) n.ChangeRapport(_player, 1.5f);
        return $"You lay by the bed: {string.Join(", ", moved)}.";
    }

    // ---- talk of canoes -----------------------------------------------------------

    private bool CanTalkBoats(Npc npc)
    {
        if (Boatwrights.YardOf(npc.HomeStores) is { } y) return !y.IsPost || npc.Role is Role.Factor or Role.NwcPartner or Role.Postmaster || Boatwrights.IsBuilder(npc, y);
        return false;
    }

    private void ShowBoatTalk(Npc npc, string said = null)
    {
        ClearOptions();
        var y = Boatwrights.YardOf(npc.HomeStores);
        if (y == null) { ShowTopics(Greeting(npc)); return; }
        var sb = new StringBuilder();
        if (said != null) sb.Append(said + "\n\n");
        var site = Boatwrights.Site(y);
        if (y.IsPost)
        {
            sb.Append($"\"The boats? {Boatwrights.FleetLine(y.Name) switch { "" => "We've not counted them this season.", var f => $"In the yard: {f}." }}\"");
            if (site != null) sb.Append($" A {site.Spec.Name} is building on the shore ({site.Stage}/{site.Spec.Stages.Length} done).");
            else sb.Append(Boatwrights.InSeason(y) ? " Nothing on the stocks just now." : $" The men build {Boats.Of(y.Kind).Name}s {(Boats.Of(y.Kind).Planked ? "in the winter" : "in the summer")}.");
        }
        else
        {
            sb.Append(site != null
                ? $"\"We are making a {site.Spec.Name}. {(site.NextStage is BoatStage s ? $"Now we {Boats.StageLabel(s).Replace("the canoe", "her")}." : "")}{(site.CommissionedBy != "" ? $" It is for {site.CommissionedBy}." : "")}\""
                : Boatwrights.InSeason(y) ? "\"Soon we make a canoe, when the bark is ready.\"" : "\"When the bark comes away from the birch, after the geese, we make canoes. Not now.\"");
            int sale = 0; foreach (var _ in Boatwrights.ForSale(y)) sale++;
            if (sale > 0) sb.Append($" {sale} of the camp's canoes lie on the shore.");
            if (y.Orders.Count > 0) sb.Append($" {y.Orders.Count} {(y.Orders.Count == 1 ? "is" : "are")} promised to others.");
        }
        _text = sb.ToString();
        // Commission a boat: the yard's kind, and a small canoe from any camp.
        var kinds = new List<BoatKind> { y.Kind };
        if (!y.IsPost && y.Kind != BoatKind.Small) kinds.Insert(0, BoatKind.Small);
        bool canOrder = !y.IsPost || npc.Faction == _player.Faction && npc.Role is Role.Factor or Role.NwcPartner or Role.Postmaster;
        if (canOrder)
            foreach (var k in kinds)
            {
                var kk = k; float price = Boatwrights.Price(k);
                AddOption($"Ask them to build you a {Boats.Of(k).Name} ({price:0} made beaver in goods and furs)", () => ShowBoatTalk(npc, Commission(npc, y, kk, price)));
            }
        foreach (var c in Boatwrights.ForSale(y))
        {
            var cc = c; float price = Mathf.Round(Boats.ValueMb(c.Kind, c.Quality, c.Condition));
            AddOption($"Buy a {c.Spec.Name} off the shore ({Boats.Grade(c.Quality)}, {Boats.Condition(c.Condition)}; {price:0} made beaver)", () =>
            {
                if (!PayGoods(price, out string paidWith)) { ShowBoatTalk(npc, "\"You have not enough for her.\""); return; }
                cc.Owner = _player.DisplayName;
                y.Afloat.Remove(cc.Index);
                npc.ChangeRapport(_player, 3f);
                Chronicle.Event($"{_player.DisplayName} bought a canoe at {y.Name}.", npc.GlobalPosition, _player, weight: 0.4f);
                _player.Journal.Add($"{GameCalendar.Instance?.DateString}: Bought a {cc.Spec.Name} at {y.Name} for {paidWith}.");
                ShowBoatTalk(npc, $"\"She is yours.\" You hand over {paidWith}.");
            });
            break;   // one at a time
        }
        if (site != null && !site.Launched) AddOption("Offer to help at the canoe bed", () =>
        {
            npc.ChangeRapport(_player, 1f);
            ShowBoatTalk(npc, $"\"Good. There is work for every pair of hands.\" (Go to the bed {site.BuiltAt} and take up the next stage with them; what you bring for her is laid by the bed.)");
        });
        if (npc.Skills.Crafting >= _player.Skills.Crafting + 0.15f && npc.RapportWith(_player) >= 10f)
            AddOption($"Ask {npc.DisplayName} to show you how a canoe is made", () =>
            {
                float gain = Mathf.Min(0.02f, (npc.Skills.Crafting - _player.Skills.Crafting) * 0.05f);
                _player.Skills.Learn(ref _player.Skills.Crafting, gain, "being shown by a builder");
                npc.ChangeRapport(_player, 1f);
                _player.RequestSkip(2f, "");
                ShowBoatTalk(npc, $"{npc.DisplayName} sits with you a long while, a strip of bark across both your knees, and shows you the stitch, the bend of a rib, how the gum is tempered with fat so it neither runs nor cracks.");
            });
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private string Commission(Npc npc, Boatwrights.Yard y, BoatKind k, float price)
    {
        if (y.Orders.Count >= 3) return "\"We have promised too many already. Ask after the summer.\"";
        if (!PayGoods(price, out string paidWith)) return $"\"For a {Boats.Of(k).Name}? That is {price:0} made beaver.\" You haven't that much in goods and furs about you.";
        Boatwrights.Order(y, _player, k, price);
        npc.ChangeRapport(_player, 2f);
        Chronicle.Event($"{_player.DisplayName} paid {y.Name} for a {Boats.Of(k).Name}.", npc.GlobalPosition, _player, weight: 0.5f);
        _player.Journal.Add($"{GameCalendar.Instance?.DateString}: Paid {y.Name} {paidWith} for a {Boats.Of(k).Name}.");
        string when = Boatwrights.InSeason(y) ? "They will begin soon." : Boats.Of(k).Planked ? "They will build her when the winter work begins." : "They will build her when the bark is ready, after the geese come.";
        return $"You hand over {paidWith}. \"It will be done.\" {when}";
    }

    // Pay in made beaver's worth of what the player carries: tokens, furs,
    // trade goods, the small goods of the trade.
    private bool PayGoods(float mb, out string paidWith)
    {
        paidWith = "";
        var order = new List<ItemId>();
        foreach (var (id, _) in _player.Inventory.All())
        {
            var d = Items.Items.Of(id);
            if (d.ValueMb <= 0f || id == ItemId.Coin) continue;
            if (d.Kind is ItemKind.Fur || id is ItemId.MbToken or ItemId.TradeGoods or ItemId.Tobacco or ItemId.Beads or ItemId.Stroud or ItemId.Powder or ItemId.Kettle or ItemId.PointBlanket or ItemId.Rum) order.Add(id);
        }
        order.Sort((a, b) => (b == ItemId.MbToken).CompareTo(a == ItemId.MbToken));
        float have = 0f; foreach (var id in order) have += Items.Items.Of(id).ValueMb * _player.Inventory.Count(id);
        if (have + 0.01f < mb) return false;
        float left = mb; var paid = new List<string>();
        foreach (var id in order)
        {
            if (left <= 0.01f) break;
            float v = Items.Items.Of(id).ValueMb;
            int n = Mathf.Min(_player.Inventory.Count(id), Mathf.CeilToInt(left / v));
            if (n <= 0) continue;
            _player.Inventory.Remove(id, n); left -= n * v;
            paid.Add(Items.Items.Lot(id, n));
        }
        paidWith = string.Join(", ", paid);
        return true;
    }
}
