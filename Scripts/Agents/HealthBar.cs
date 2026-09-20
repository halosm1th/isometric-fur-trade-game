using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// A thin bar over someone's head for a few seconds after they're hurt.
public partial class HealthBar : Node2D
{
    private Agent _agent;
    private float _ttl;
    private float _max = 100f;

    public void Show(Agent a)
    {
        _agent = a;
        _max = a is Animal an ? an.Def.Health : 100f;
        _ttl = 4f;
        Visible = true;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_ttl <= 0f) { Visible = false; return; }
        _ttl -= (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_agent == null) return;
        float frac = Mathf.Clamp(_agent.Needs.Health / Mathf.Max(1f, _max), 0f, 1f);
        float alpha = Mathf.Clamp(_ttl, 0f, 1f);
        var rect = new Rect2(-22, 0, 44, 5);
        DrawRect(rect, new Color(0, 0, 0, 0.7f * alpha));
        DrawRect(new Rect2(-21, 1, 42 * frac, 3), new Color(frac > 0.5f ? 0.3f : 0.9f, frac > 0.5f ? 0.85f : 0.25f, 0.2f, alpha));
    }
}
