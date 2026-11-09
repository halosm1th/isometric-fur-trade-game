using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// A boat: a birchbark canoe of whatever size, or a York boat (World/Boats).
// The design doc's "enter vehicle" verb: board her from the shore, paddle on
// open water, step onto any bank and she stays where you left her. Frozen in
// all winter.
//
// The same node is the boat on her bed while she is being built (Stage less
// than her stages), so a half-built canoe is a thing in the world: it is
// seen, saved, worked at, and can be walked away from and come back to.
//
// She wears (design doc 12.2): rocks in a rapid open her skin and crack her
// ribs, a bad portage strains her gunwales, the gum dries and cracks in the
// sun and the cold, and every leak lets water into her bilge. Mended with gum,
// bark, watap and cedar (UI/BoatWork's patch games); a York boat with oakum,
// pitch and plank.
public partial class Canoe : Node2D
{
    public int Index;
    public int RiderNetId;          // 0 = empty
    public BoatKind Kind = BoatKind.Small;
    public BoatSpec Spec => Boats.Of(Kind);

    // Who she is: her name (if anyone gave her one), whose she is, who built
    // her, and when.
    public string BoatName = "", Owner = "", Builders = "", BuiltAt = "";
    public int BuiltDay = -1;

    // What's lashed in the bottom (design doc 12).
    public readonly Inventory Cargo = new();
    public float CapacityLbs => Spec.CapacityLbs * (0.85f + 0.15f * Shape) * (Parts[(int)HullPart.Ribs] < 0.35f ? 0.7f : 1f) * (Parts[(int)HullPart.Gunwales] < 0.3f ? 0.8f : 1f);
    public float LoadLbs => Cargo.TotalWeightLbs;
    public bool Overloaded => LoadLbs > CapacityLbs;

    // ---- the build ---------------------------------------------------------
    public int Stage = 99;           // stages done; at or past the spec's count she is afloat-ready
    public readonly float[] StageScores = new float[5];
    public bool Launched => Stage >= Spec.Stages.Length;
    public bool Building => !Launched;
    public BoatStage? NextStage => Launched ? null : Spec.Stages[Stage];
    public readonly Inventory Stock = new();   // materials laid by at the bed, for the build and for mending
    public float WorkHours;          // hours put into the stage now in hand (NPC builders)
    public string Yard = "";         // the camp or post whose builders are at her (World/Boatwrights)
    public string CommissionedBy = "";   // a player who has paid for her
    public float PaidMb;

    // ---- the hull ------------------------------------------------------------
    public readonly float[] Parts = { 1f, 1f, 1f, 1f };
    public float Shape = 0.8f;       // how fair and true her lines are (the stakes, the ribs)
    public float Quality = 0.7f;     // how well she was built, all told
    // Leaks: where along her length (0 bow .. 1 stern) and how bad (0..1).
    public readonly List<Vector2> Leaks = new();
    public float Bilge;              // 0 dry .. 1 swamped
    public bool Wrecked;

    public float Condition
    {
        get { float s = 0f; foreach (var p in Parts) s += p; return s / Parts.Length; }
    }
    public float Part(HullPart p) => Parts[(int)p];
    public void Wear(HullPart p, float by) => Parts[(int)p] = Mathf.Clamp(Parts[(int)p] - by, 0f, 1f);
    public void Mend(HullPart p, float to) => Parts[(int)p] = Mathf.Clamp(Mathf.Max(Parts[(int)p], to), 0f, 1f);

    // How she goes: her lines, her bones, the water in her.
    public float SpeedFactor => Spec.Speed * (0.8f + 0.2f * Shape) * (0.7f + 0.3f * Mathf.Min(Part(HullPart.Skin), Part(HullPart.Ribs))) * (1f - 0.45f * Bilge);
    // Bilge gained per second afloat.
    public float LeakRate
    {
        get
        {
            float r = 0f;
            foreach (var l in Leaks) r += l.Y;
            return r * 0.012f + Mathf.Max(0f, 0.55f - Part(HullPart.Seams)) * 0.02f + Mathf.Max(0f, 0.3f - Part(HullPart.Skin)) * 0.03f;
        }
    }

    public string Describe() => BoatName != "" ? BoatName : (Owner != "" ? $"{Owner}'s {Spec.Short}" : $"the {Spec.Short}");

    // A new leak where she was struck (or a worse one where she already leaks).
    public void Hole(float at, float size)
    {
        for (int i = 0; i < Leaks.Count; i++)
            if (Mathf.Abs(Leaks[i].X - at) < 0.06f) { Leaks[i] = new Vector2(Leaks[i].X, Mathf.Min(1f, Leaks[i].Y + size)); return; }
        if (Leaks.Count < 12) Leaks.Add(new Vector2(Mathf.Clamp(at, 0.04f, 0.96f), Mathf.Clamp(size, 0.05f, 1f)));
    }

    public bool IsFree => RiderNetId == 0;

    private Sprite2D _sprite;
    private string _spritePath = "";

    public override void _Ready()
    {
        _sprite = new Sprite2D { Offset = new Vector2(0, -14) };
        AddChild(_sprite);
        AddToGroup("canoes");
        Refresh();
    }

    // Her picture for her kind, and how far along she is on the bed.
    public void Refresh()
    {
        if (_sprite == null) return;
        if (_spritePath != Spec.Sprite) { _sprite.Texture = GD.Load<Texture2D>(Spec.Sprite); _spritePath = Spec.Sprite; }
        _sprite.Scale = Vector2.One * Spec.SpriteScale;
        float done = Launched ? 1f : (float)Stage / Spec.Stages.Length;
        // On the bed: a pale sheet of bark staked out, filling in as she is made.
        float worn = Launched ? Mathf.Lerp(0.72f, 1f, Condition) : 1f;
        _sprite.Modulate = Launched ? new Color(worn, worn * 0.98f, worn * 0.95f) : new Color(1f, 0.95f, 0.85f, Stage >= 2 ? 0.3f + 0.6f * done : 0f);
        _sprite.ZIndex = 0;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Building) return;
        // The beaten-earth bed, the bark (or the keel and frames) on it, the
        // stakes round it.
        float half = 55f * Spec.SpriteScale, depth = 9f * Spec.SpriteScale;
        if (Spec.Planked) { half = 80f; depth = 14f; }
        var stake = new Color(0.32f, 0.22f, 0.12f);
        var bed = new List<Vector2>();
        for (int i = 0; i <= 16; i++) { float a = Mathf.Pi * 2f * i / 16f; bed.Add(new Vector2(Mathf.Cos(a) * (half + 14f), -14f + Mathf.Sin(a) * (depth + 12f))); }
        DrawColoredPolygon(bed.ToArray(), new Color(0.36f, 0.28f, 0.2f, 0.55f));
        if (Stage >= 1)
        {
            var sheet = new List<Vector2>();
            for (int i = 0; i <= 20; i++) { float t = i / 20f; sheet.Add(new Vector2(Mathf.Lerp(-half, half, t), -14f - Mathf.Sin(t * Mathf.Pi) * depth)); }
            for (int i = 20; i >= 0; i--) { float t = i / 20f; sheet.Add(new Vector2(Mathf.Lerp(-half, half, t), -14f + Mathf.Sin(t * Mathf.Pi) * depth)); }
            var col = Spec.Planked ? new Color(0.6f, 0.43f, 0.26f) : new Color(0.93f, 0.9f, 0.82f);
            for (int i = 0; i < 20; i++) DrawColoredPolygon(new[] { sheet[i], sheet[i + 1], sheet[sheet.Count - 2 - i], sheet[sheet.Count - 1 - i] }, col);
            sheet.Add(sheet[0]);
            DrawPolyline(sheet.ToArray(), new Color(0.3f, 0.2f, 0.1f), 1.5f);
            // Ribs across her once they are in.
            if (Stage >= 4) for (int i = 1; i < 8; i++) { float t = i / 8f, x = Mathf.Lerp(-half, half, t), w = Mathf.Sin(t * Mathf.Pi) * depth; DrawLine(new Vector2(x, -14 - w), new Vector2(x, -14 + w), new Color(0.65f, 0.42f, 0.25f), 2f); }
        }
        int n = Kind == BoatKind.Small ? 6 : 9;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n, x = Mathf.Lerp(-half, half, t), w = Mathf.Sin(t * Mathf.Pi) * depth;
            DrawLine(new Vector2(x, -14 - w - 14), new Vector2(x, -14 - w + 2), stake, 3f);
            DrawLine(new Vector2(x, -14 + w - 2), new Vector2(x, -14 + w + 14), stake, 3f);
        }
        // Stock laid by: a little pile beside the bed.
        if (Stock.SlotCount > 0) DrawRect(new Rect2(half + 8f, -18f, 14f, 10f), new Color(0.85f, 0.82f, 0.74f));
    }

    public void SetState(Vector2 pos, int rider)
    {
        RiderNetId = rider;
        Position = pos;
        Visible = rider == 0;
    }

    public override void _Process(double delta)
    {
        Visible = RiderNetId == 0 && !Wrecked;
    }

    // ---- the wire and the save ---------------------------------------------

    // Everything a status page needs to show her, for a client's copy.
    public Godot.Collections.Dictionary Pack()
    {
        var leaks = new Godot.Collections.Array();
        foreach (var l in Leaks) { leaks.Add(l.X); leaks.Add(l.Y); }
        var cargo = new Godot.Collections.Array();
        foreach (var (id, n) in Cargo.All()) { cargo.Add((int)id); cargo.Add(n); }
        var stock = new Godot.Collections.Array();
        foreach (var (id, n) in Stock.All()) { stock.Add((int)id); stock.Add(n); }
        return new Godot.Collections.Dictionary
        {
            ["i"] = Index, ["k"] = (int)Kind, ["st"] = Stage, ["sc"] = new Godot.Collections.Array { StageScores[0], StageScores[1], StageScores[2], StageScores[3], StageScores[4] },
            ["p"] = new Godot.Collections.Array { Parts[0], Parts[1], Parts[2], Parts[3] }, ["sh"] = Shape, ["q"] = Quality, ["lk"] = leaks, ["b"] = Bilge,
            ["n"] = BoatName, ["o"] = Owner, ["by"] = Builders, ["at"] = BuiltAt, ["d"] = BuiltDay, ["c"] = cargo, ["s"] = stock, ["wh"] = WorkHours,
            ["y"] = Yard, ["cm"] = CommissionedBy, ["w"] = Wrecked,
        };
    }

    public void Unpack(Godot.Collections.Dictionary d)
    {
        Kind = (BoatKind)d["k"].AsInt32(); Stage = d["st"].AsInt32();
        var sc = d["sc"].AsGodotArray(); for (int i = 0; i < 5 && i < sc.Count; i++) StageScores[i] = sc[i].AsSingle();
        var p = d["p"].AsGodotArray(); for (int i = 0; i < 4 && i < p.Count; i++) Parts[i] = p[i].AsSingle();
        Shape = d["sh"].AsSingle(); Quality = d["q"].AsSingle(); Bilge = d["b"].AsSingle();
        Leaks.Clear(); var lk = d["lk"].AsGodotArray(); for (int i = 0; i + 1 < lk.Count; i += 2) Leaks.Add(new Vector2(lk[i].AsSingle(), lk[i + 1].AsSingle()));
        BoatName = d["n"].AsString(); Owner = d["o"].AsString(); Builders = d["by"].AsString(); BuiltAt = d["at"].AsString(); BuiltDay = d["d"].AsInt32();
        Cargo.Clear(); var c = d["c"].AsGodotArray(); for (int i = 0; i + 1 < c.Count; i += 2) Cargo.Add((ItemId)c[i].AsInt32(), c[i + 1].AsInt32());
        Stock.Clear(); var s = d["s"].AsGodotArray(); for (int i = 0; i + 1 < s.Count; i += 2) Stock.Add((ItemId)s[i].AsInt32(), s[i + 1].AsInt32());
        WorkHours = d.TryGetValue("wh", out var wh) ? wh.AsSingle() : 0f;
        Yard = d.TryGetValue("y", out var y) ? y.AsString() : ""; CommissionedBy = d.TryGetValue("cm", out var cm) ? cm.AsString() : "";
        Wrecked = d.TryGetValue("w", out var w) && w.AsBool();
        Refresh();
    }
}
