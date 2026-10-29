using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Player;

// The player's own horse (design doc 12.3): bought at a plains camp or lent
// from a post's horse guard, ridden, picketed when you get down, and lost if
// you leave it standing where the raiders or the wolves come by at night.
// A horse left on poor grass or ridden day after day goes thin, and a thin
// horse is a slow one.
public partial class PlayerController
{
    public string HorseName = "";
    public int HorseCoat = -1;
    public float HorseCondition = 70f;
    public Vector2 HorsePicket;
    // Whose horse it is: "" your own; a post's name when lent from its guard.
    public string HorseLentBy = "";
    public bool HasHorse => HorseCoat >= 0;
    private Mount _picketView;
    private int _horseNight = -1;

    public const float RemountReachPx = 200f;
    // Close enough to your picketed horse that E means "get up".
    public bool NearOwnHorse => HasHorse && !Mounted && !IsAfloat && GlobalPosition.DistanceTo(HorsePicket) < 140f;

    // The horse's shape slows him: a starving horse goes at a walk.
    public override float RidePace => HorsePace;
    public float HorsePace => Mathf.Lerp(0.45f, 1f, Mathf.Clamp(HorseCondition / 45f, 0f, 1f));

    // Mount or get down (server side).
    private string RideAction()
    {
        if (!HasHorse && Economy.Plains.TryCutOut(this) is { } cut) return cut;
        if (!HasHorse) return "You have no horse. The camps of the plains have horses to trade, and the posts of the parkland keep a horse guard.";
        if (Mounted)
        {
            GetDown();
            return $"You get down and picket {HorseName}.";
        }
        if (IsAfloat) return "Not from a canoe.";
        if (IsSheltered) return "Not indoors.";
        float d = GlobalPosition.DistanceTo(HorsePicket);
        if (d > RemountReachPx) return $"{HorseName} is picketed {Distance(d)} {Bearing(HorsePicket)} of you.";
        GlobalPosition = HorsePicket.DistanceTo(GlobalPosition) < 1f ? GlobalPosition : HorsePicket;
        SetMounted(HorseCoat);
        ShowPicket(false);
        return HorseCondition < 25f ? $"You get up on {HorseName}. The horse is thin; it will not carry you fast." : $"You get up on {HorseName}.";
    }

    // Straight up onto your horse wherever it is (a horse just handed over).
    public void MountNow()
    {
        if (!HasHorse) return;
        HorsePicket = GlobalPosition;
        SetMounted(HorseCoat);
        ShowPicket(false);
    }

    // Down off the horse, which stays where you leave it.
    public void GetDown()
    {
        if (!Mounted) return;
        HorsePicket = GlobalPosition;
        SetMounted(-1);
        ShowPicket(true);
    }

    // Killed in the saddle: he comes off, and the horse is left standing where
    // he fell. His heir starts afoot and can go and fetch it - if the raiders
    // or the wolves have not had it first (HorseTick's night losses). Before
    // this the dead man stayed on the horse, and his heir rose on it.
    protected override void OnDeath()
    {
        if (Mounted)
        {
            GetDown();
            if (NetHub.IsServer) Chronicle.Event($"{DisplayName} fell from {HorseName}.", GlobalPosition, this, weight: 0.3f);
        }
        base.OnDeath();
    }

    // A horse comes to you (bought, lent, given back): it stands at your side.
    public void GiveHorse(string name, int coat, float condition, string lentBy = "")
    {
        if (Mounted) SetMounted(-1);
        HorseName = name; HorseCoat = coat; HorseCondition = condition; HorseLentBy = lentBy;
        HorsePicket = GlobalPosition + new Vector2(60, 20);
        ShowPicket(true);
    }

    // The horse goes from you (sold, given away, stolen, dead).
    public void LoseHorse()
    {
        if (Mounted) SetMounted(-1);
        HorseCoat = -1; HorseName = ""; HorseLentBy = "";
        ShowPicket(false);
    }

    private void HorseTick(float delta)
    {
        if (!HasHorse) return;
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        float hours = cal.DeltaGameHours;
        // No horse into a canoe or through a door.
        if (Mounted && (Aboard != null || IsSheltered)) { GetDown(); Say($"You get down; {HorseName} stays outside."); }
        int m = cal.Month;
        float grass = m is >= 5 and <= 9 ? 0.4f : m is 4 or 10 ? 0.15f : -0.06f;
        bool working = Mounted && Velocity.LengthSquared() > 100f;
        HorseCondition = Mathf.Clamp(HorseCondition + hours * (working ? -0.5f * (IsSprinting ? 2f : 1f) : Mounted ? 0f : grass), 0f, 100f);
        if (HorseCondition <= 0f)
        {
            string name = HorseName;
            Economy.HorseTrade.OnLentHorseLost(this);
            LoseHorse();
            Say($"{name} is down and will not get up again. Ridden to death, or starved: there was nothing left in the horse.");
            Chronicle.Event($"{DisplayName}'s horse {name} died under him.", GlobalPosition, this, weight: 0.5f);
            return;
        }
        // Left standing far off at night: on the plains a picketed horse is
        // what the young men of another nation go out at night to bring home.
        if (!Mounted && cal.Hour >= 1f && cal.Hour < 2f && _horseNight != cal.Day && GlobalPosition.DistanceTo(HorsePicket) > 2500f)
        {
            _horseNight = cal.Day;
            var g = WorldMap.At(IsoGrid.FromWorld(HorsePicket));
            bool plains = g is Ground.Grassland or Ground.TallGrass or Ground.Parkland;
            if (GD.Randf() < (plains ? 0.06f : 0.02f))
            {
                string name = HorseName;
                Economy.HorseTrade.OnLentHorseLost(this);
                LoseHorse();
                Say(plains ? $"{name} is gone from where you left it: the picket rope cut clean. Somebody's young men have a horse to show." : $"{name} is gone: pulled down in the night, by the sign of it - wolves.");
                Chronicle.Event($"{DisplayName} lost his horse {name}, left picketed in the open.", HorsePicket, this, weight: 0.4f);
            }
        }
    }

    private void ShowPicket(bool show)
    {
        if (_picketView != null && IsInstanceValid(_picketView)) _picketView.QueueFree();
        _picketView = null;
        if (!show || !HasHorse || GetParent() == null) return;
        _picketView = new Mount { Name = "Picket", Coat = HorseCoat, Ridden = false, Position = HorsePicket };
        GetParent().CallDeferred(Node.MethodName.AddChild, _picketView);
    }

    public Godot.Collections.Array PackHorse() => new() { HorseName, HorseCoat, HorseCondition, MountCoat, HorsePicket.X, HorsePicket.Y, HorseLentBy };

    // The owner's copy hears from the server what his horse is doing.
    public void UnpackHorse(Godot.Collections.Array a)
    {
        if (a == null || a.Count < 7 || NetHub.IsServer) return;
        bool had = HasHorse; var picket = HorsePicket; int was = MountCoat;
        HorseName = (string)a[0]; HorseCoat = (int)a[1]; HorseCondition = (float)a[2];
        HorsePicket = new Vector2((float)a[4], (float)a[5]); HorseLentBy = (string)a[6];
        int coat = (int)a[3];
        if (coat != MountCoat) SetMounted(coat);
        if (had != HasHorse || picket != HorsePicket || was != MountCoat) ShowPicket(HasHorse && !Mounted);
    }

    // After a load: the horse is put back where it was.
    public void RestoreHorse(Godot.Collections.Array a)
    {
        LoseHorse();
        if (a == null || a.Count < 7 || (int)a[1] < 0) return;
        HorseName = (string)a[0]; HorseCoat = (int)a[1]; HorseCondition = (float)a[2];
        HorsePicket = new Vector2((float)a[4], (float)a[5]); HorseLentBy = (string)a[6];
        if ((int)a[3] >= 0) SetMounted(HorseCoat); else ShowPicket(true);
        if (Economy.Plains.Log) GD.Print($"[plains] {DisplayName}'s horse back from the save: {HorseName}, coat {HorseCoat}, condition {HorseCondition:0}, mounted {Mounted}");
    }

    private static string Distance(float px) => WorldMap.Distance(px);

    private string Bearing(Vector2 to)
    {
        var d = to - GlobalPosition;
        string ns = d.Y < -Mathf.Abs(d.X) * 0.4f ? "north" : d.Y > Mathf.Abs(d.X) * 0.4f ? "south" : "";
        string ew = d.X < -Mathf.Abs(d.Y) * 0.4f ? "west" : d.X > Mathf.Abs(d.Y) * 0.4f ? "east" : "";
        return ns != "" && ew != "" ? $"{ns}-{ew}" : ns + ew;
    }
}
