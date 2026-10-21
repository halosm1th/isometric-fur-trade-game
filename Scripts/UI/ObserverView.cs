using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// The observer's view of the country (Core/Observer): under the camera, the
// whole map as the map panel draws it, so that far out - past where the
// ground is painted in tiles - the country is still there; over it, what is
// moving: the posts and camps, the brigades on the rivers, the trading
// parties on their way, the war parties and the horse thieves, the buffalo
// runs and the herds. Markers keep their size on screen at any zoom.
public partial class ObserverView : Node2D
{
    private Sprite2D _ground;
    private Markers _marks;

    public override void _Ready()
    {
        ZIndex = -50;
        var src = WorldMap.GroundImage;
        if (src != null)
        {
            var img = (Image)src.Duplicate();
            img.Convert(Image.Format.Rgba8);
            img.Resize(WorldMap.Columns * 4, WorldMap.Rows, Image.Interpolation.Nearest);
            _ground = new Sprite2D { Texture = ImageTexture.CreateFromImage(img), Centered = false, Scale = Vector2.One * 32f, Modulate = new Color(0.85f, 0.85f, 0.85f) };
            AddChild(_ground);
        }
        _marks = new Markers { ZIndex = 4000, ZAsRelative = false };
        AddChild(_marks);
    }

    public override void _Process(double delta) => _marks.QueueRedraw();

    // The markers, drawn over everything.
    private partial class Markers : Node2D
    {
        private static readonly Color Hbc = new(0.85f, 0.22f, 0.2f), Nwc = new(0.25f, 0.65f, 0.35f), Amer = new(0.35f, 0.5f, 0.95f);
        private static Color CompanyColour(Agents.Faction f) => f == Agents.Faction.NWC ? Nwc : f == Agents.Faction.American ? Amer : Hbc;

        public override void _Draw()
        {
            var cam = GetViewport()?.GetCamera2D();
            float zoom = cam?.Zoom.X ?? 1f;
            if (zoom > 0.55f) return;   // close in, the country speaks for itself
            float px = 1f / Mathf.Max(0.005f, zoom);   // one screen pixel in world units
            var font = ThemeDB.FallbackFont;
            bool names = zoom > 0.035f;

            // The herds.
            if (Sim.Ecology != null)
                foreach (var s in Sim.Ecology.Sites)
                {
                    if (!s.IsMobile || s.IsDead || s.Count < 1f) continue;
                    var c = s.Species == Ecology.Species.Buffalo ? new Color(0.45f, 0.3f, 0.18f, 0.55f) : new Color(0.75f, 0.75f, 0.7f, 0.45f);
                    DrawCircle(s.WorldPos, Mathf.Max(s.RadiusPx, 4f * px), c);
                }
            // The camps.
            foreach (var camp in Camps.All)
            {
                if (camp.Ledger == null) continue;
                float r = (camp.IsFamily ? 3f : 5f) * px;
                DrawCircle(IsoGrid.ToWorld(camp.Centre), r, new Color(0.95f, 0.85f, 0.55f));
                DrawArc(IsoGrid.ToWorld(camp.Centre), r, 0f, Mathf.Tau, 12, new Color(0.25f, 0.18f, 0.1f), 1.2f * px);
            }
            // The posts.
            void Post(Vector2 at, Agents.Faction f, string name)
            {
                float r = 5f * px;
                DrawRect(new Rect2(at - Vector2.One * r, Vector2.One * r * 2f), CompanyColour(f));
                DrawRect(new Rect2(at - Vector2.One * r, Vector2.One * r * 2f), new Color(0.1f, 0.08f, 0.06f), false, 1.2f * px);
                if (names && font != null) DrawString(font, at + new Vector2(r * 1.6f, r * 0.6f), name, HorizontalAlignment.Left, -1, Mathf.RoundToInt(12f * px), new Color(1f, 0.97f, 0.9f));
            }
            Post(WorldLayout.FortCentre, Agents.Faction.HBC, "York Factory");
            Post(IsoGrid.ToWorld(WorldLayout.NwcTop), Agents.Faction.NWC, "Nor'Westers");
            Post(IsoGrid.ToWorld(WorldLayout.OutpostTop), Agents.Faction.HBC, "Oxford House");
            foreach (var p in Posts.Standing()) Post(p.Centre, p.Faction, p.Name);
            // The buffalo runs.
            foreach (var run in Ecology.BuffaloRun.Active)
                DrawArc(run.Site.WorldPos, run.Site.RadiusPx * 1.2f, 0f, Mathf.Tau, 24, new Color(0.9f, 0.55f, 0.2f), 2.5f * px);
            // The trading parties on the road.
            foreach (var t in TradeTrips.All)
            {
                var at = TradeTrips.Where(t);
                DrawLine(at, IsoGrid.ToWorld(t.Traded ? t.Camp.Camp : t.Post.Post.GateOutside), new Color(1f, 0.85f, 0.3f, 0.35f), 1f * px);
                DrawCircle(at, 3.5f * px, new Color(1f, 0.85f, 0.3f));
            }
            // The brigades.
            foreach (var cv in Convoys.All)
            {
                if (cv.Now == ConvoyState.Arrived) continue;
                var at = cv.Where; float r = 6f * px;
                var tri = new[] { at + new Vector2(0, -r), at + new Vector2(r * 0.9f, r * 0.6f), at + new Vector2(-r * 0.9f, r * 0.6f) };
                DrawColoredPolygon(tri, CompanyColour(cv.Run.Company));
                DrawPolyline(new[] { tri[0], tri[1], tri[2], tri[0] }, Colors.White, 1.2f * px);
            }
            // The war parties and the horse thieves.
            foreach (var w in War.Parties)
            {
                if (w.Phase == WarPhase.Over) continue;
                DrawLine(w.Home, w.Target, new Color(0.9f, 0.15f, 0.1f, 0.5f), 1.5f * px);
                var at = w.Target;
                foreach (var m in w.Men) if (m != null && IsInstanceValid(m) && !m.IsDead && m.Visible) { at = m.GlobalPosition; break; }
                DrawCircle(at, 5f * px, new Color(0.9f, 0.15f, 0.1f));
            }
            foreach (var (_, target, lead) in Plains.LiveRaids())
                DrawCircle(lead != null && IsInstanceValid(lead) ? lead.GlobalPosition : IsoGrid.ToWorld(target.AccessCell), 4f * px, new Color(1f, 0.5f, 0.1f));
            // What the camera is following.
            if (Observer.Follow?.Invoke() is Vector2 f) DrawArc(f, 14f * px, 0f, Mathf.Tau, 24, Colors.White, 2f * px);
        }
    }
}
