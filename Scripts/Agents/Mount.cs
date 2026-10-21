using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// The horse under a rider (design doc 12.3), or one standing picketed or
// grazing with no rider at all. Only a picture: the riding itself is the
// agent's (Agent.Mounted), the horse's worth and its winter are the
// household's (Economy.Herd). Draws from the horse's own sheet when there is
// one, and until then from a plain stand-in, in the horse's coat.
public partial class Mount : Node2D
{
    public const string HorseFrames = "res://Scenes/Animals/HorseFrames.tres";
    // A plain pony drawn by tools/sprites/make_horse_standin.py, in pale greys to take the coat.
    private const string Stand = "res://Scenes/Animals/HorseStandinFrames.tres";
    // How far the rider sits above the ground.
    public const float RiderLift = 30f;
    private const float StandScale = 0.62f;

    public int Coat;
    // A rider's horse follows its rider; a loose one grazes where it stands.
    public bool Ridden = true;
    private AnimatedSprite2D _horse;
    private SpriteAnims _anims;
    private AnimatedSprite2D _rider;
    private Vector2 _last;
    private Vector2 _dir = new(1, 1);
    private float _speed;
    private bool _ownSheet;

    public override void _Ready()
    {
        _ownSheet = ResourceLoader.Exists(HorseFrames);
        _horse = new AnimatedSprite2D { SpriteFrames = GD.Load<SpriteFrames>(_ownSheet ? HorseFrames : Stand), Centered = true };
        // The stand-in's coat is painted over it; a real sheet has its own.
        if (!_ownSheet) _horse.Modulate = Economy.Horses.Coats[Coat % Economy.Horses.Coats.Length];
        // The stand-in is drawn big; a plains pony is not much longer than a man is tall.
        if (!_ownSheet) _horse.Scale = Vector2.One * StandScale;
        var first = _horse.SpriteFrames.HasAnimation("idle_se") ? _horse.SpriteFrames.GetFrameTexture("idle_se", 0) : null;
        _horse.Offset = first != null ? new Vector2(0, -first.GetHeight() / 2f + 4f) : new Vector2(0, -16);
        AddChild(_horse);
        _anims = new SpriteAnims(_horse);
        if (Ridden && GetParent() is Node2D body)
        {
            // The horse goes under the rider: first among the body's pictures.
            body.MoveChild(this, 0);
            _rider = body.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
            if (_rider != null) _rider.Position = new Vector2(_rider.Position.X, -RiderLift);
        }
        _last = GlobalPosition;
        Play();
    }

    public override void _ExitTree()
    {
        if (_rider != null && IsInstanceValid(_rider)) _rider.Position = new Vector2(_rider.Position.X, 0f);
    }

    public override void _Process(double delta)
    {
        float dt = Mathf.Max((float)delta, 1e-4f);
        var moved = GlobalPosition - _last;
        _last = GlobalPosition;
        float v = moved.Length() / dt;
        _speed = Mathf.Lerp(_speed, v, 0.25f);
        if (moved.LengthSquared() > 0.5f) _dir = moved;
        Play();
    }

    private static readonly string[,] Gaits =
    {
        { "run_ne", "run_nw", "run_se", "run_sw" },
        { "walk_ne", "walk_nw", "walk_se", "walk_sw" },
        { "idle_ne", "idle_nw", "idle_se", "idle_sw" },
        { "graze_ne", "graze_nw", "graze_se", "graze_sw" },
    };

    private void Play()
    {
        if (_horse?.SpriteFrames == null || _anims == null) return;
        int gait = _speed > 330f ? 0 : _speed > 8f ? 1 : 2;
        // Grazing: a loose horse stands with its head down.
        if (!Ridden && gait == 2 && _anims.Has("graze_se")) gait = 3;
        int facing = (_dir.Y < 0 ? 0 : 2) + (_dir.X < 0 ? 1 : 0);
        // The names are made once (Gaits), not every frame.
        string anim = Gaits[gait, facing];
        if (!_anims.Has(anim)) anim = Gaits[2, facing];
        if (!_anims.Has(anim)) return;
        _anims.Play(anim);
    }
}
