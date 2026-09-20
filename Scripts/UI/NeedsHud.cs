using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// Bottom-left need bars for the player, plus the death screen.
public partial class NeedsHud : Control
{
    [Export] public NodePath PlayerPath;

    private Agent _player;
    private readonly ProgressBar[] _bars = new ProgressBar[4];
    private Label _shelter;
    private Label _death;

    private static readonly (string name, Color color)[] Rows =
    {
        ("Health", new Color(0.80f, 0.25f, 0.22f)),
        ("Warmth", new Color(0.95f, 0.62f, 0.25f)),
        ("Food",   new Color(0.62f, 0.72f, 0.30f)),
        ("Water",  new Color(0.35f, 0.62f, 0.85f)),
    };

    public override void _Ready()
    {
        _player = Sim.Player;
        Sim.LocalPlayerChanged += p => { _player = p; OnPlayerBound(); };
        if (_player != null) CallDeferred(nameof(OnPlayerBound));

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.Position = new Vector2(16, -16);
        box.SetAnchorsPreset(LayoutPreset.BottomLeft);
        box.GrowVertical = GrowDirection.Begin;
        box.AddThemeConstantOverride("separation", 4);
        AddChild(box);

        for (int i = 0; i < Rows.Length; i++)
        {
            var row = new HBoxContainer();
            var label = new Label { Text = Rows[i].name, CustomMinimumSize = new Vector2(64, 0) };
            label.AddThemeFontSizeOverride("font_size", 14);
            var bar = new ProgressBar
            {
                CustomMinimumSize = new Vector2(180, 14),
                MaxValue = 100, ShowPercentage = false,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            var fill = new StyleBoxFlat { BgColor = Rows[i].color };
            fill.SetCornerRadiusAll(2);
            var bg = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f) };
            bg.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("fill", fill);
            bar.AddThemeStyleboxOverride("background", bg);
            row.AddChild(label);
            row.AddChild(bar);
            box.AddChild(row);
            _bars[i] = bar;
        }

        _shelter = new Label();
        _shelter.AddThemeFontSizeOverride("font_size", 13);
        _shelter.Modulate = new Color(0.85f, 0.9f, 1f, 0.85f);
        box.AddChild(_shelter);

        _death = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
        };
        _death.SetAnchorsPreset(LayoutPreset.FullRect);
        _death.AddThemeFontSizeOverride("font_size", 28);
        _death.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        AddChild(_death);

    }

    private void OnPlayerBound()
    {
        if (_player == null) return;
        _player.Died += OnDied;
        if (_player is Player.PlayerController pc) pc.Revived += () => _death.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;
        var n = _player.Needs;
        _bars[0].Value = n.Health;
        _bars[1].Value = n.Warmth;
        _bars[2].Value = n.Hunger;
        _bars[3].Value = n.Thirst;
        string task = "";
        if (_player is Player.PlayerController pc)
        {
            string t = pc.IsServerCopy ? (pc.Task.Kind != Player.TaskKind.None ? pc.Task.Describe() + (pc.Task.IsComplete ? " - done" : "") : "") : pc.TaskText;
            string skip = pc.IsServerCopy ? (Net.Net.I?.SkipStatus() ?? "") : pc.SkipStatus;
            string company = pc.Faction == Agents.Faction.NWC ? "the North West Company" : "the Company";
            task = (t != "" ? $"\n{t}" : "") + $"\nStanding with {company}: {pc.Standing:0} ({pc.Rank})" + (skip != "" ? $"\n{skip}" : "");
        }
        string weapon = _player.Weapon != null ? Items.Items.Of(_player.Weapon.Value).Name : "bare hands";
        if (_player.IsDrunk) weapon += "  |  drunk";
        if (_player.IsSick) weapon += "  |  FEVER";
        if (_player.HasScurvy) weapon += "  |  SCURVY (eat fresh meat)";
        _shelter.Text = $"{(_player.IsSheltered ? "Indoors  " : "")}Load {_player.Inventory.TotalWeightLbs:0}/{_player.CarryCapacityLbs:0} lb{(_player.IsOverloaded ? " (overloaded)" : "")}  |  {weapon}" + task;
    }

    private void OnDied()
    {
        var cal = GameCalendar.Instance;
        _death.Text = $"You died.\n{cal?.DateString}\n\nPress R to begin again.";
        _death.Visible = true;
    }
}
