using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// Animation names for a body's sprite, without the garbage. Every frame every
// animal and every man asked its sprite what it was playing (a new StringName
// each time, read back from the engine), built its next animation's name, and
// turned each name into a StringName again to ask whether the sprite had it
// and to play it. That was most of what the collector stopped the game to
// sweep. Here the names are turned into StringNames once and kept, and what is
// playing is noted when the sprite says it changed, not read every frame.
public sealed class SpriteAnims
{
    private static readonly Dictionary<string, StringName> _names = new();

    // The one StringName for this name.
    public static StringName Name(string name)
    {
        if (!_names.TryGetValue(name, out var sn)) _names[name] = sn = new StringName(name);
        return sn;
    }

    private readonly AnimatedSprite2D _sprite;

    // What the sprite is playing (or last played), as a plain string.
    public string Current { get; private set; }

    public SpriteAnims(AnimatedSprite2D sprite)
    {
        _sprite = sprite;
        Current = sprite.Animation;
        sprite.AnimationChanged += Refresh;
        sprite.SpriteFramesChanged += Refresh;
    }

    private void Refresh() => Current = _sprite.Animation;

    public bool Has(string name) => _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(Name(name));

    // Play it unless it is already the animation (the old `Animation != name` test).
    public void Play(string name)
    {
        if (Current != name) _sprite.Play(Name(name));
    }

    // Play it from the start whatever is playing (the old bare Play).
    public void Restart(string name) => _sprite.Play(Name(name));
}
