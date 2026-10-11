using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// A board on a post outside a gate with the place's name painted on it, so
// you know whose fort you have walked up to.
[Tool]
public partial class PostSign : Node2D
{
    public string Text = "";
    public string Sub = "";
    private const int Size = 16, SubSize = 11;

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var wood = new Color(0.42f, 0.29f, 0.17f);
        var dark = new Color(0.22f, 0.14f, 0.08f);
        var paint = new Color(0.93f, 0.88f, 0.74f);
        float w = Mathf.Max(font.GetStringSize(Text, HorizontalAlignment.Left, -1, Size).X, Sub == "" ? 0f : font.GetStringSize(Sub, HorizontalAlignment.Left, -1, SubSize).X) + 26f;
        float h = Sub == "" ? 30f : 44f;
        // Two posts sunk in the ground, the board across them.
        DrawRect(new Rect2(-w / 2f + 8f, -h - 34f, 6f, h + 34f), dark);
        DrawRect(new Rect2(w / 2f - 14f, -h - 34f, 6f, h + 34f), dark);
        var board = new Rect2(-w / 2f, -h - 40f, w, h);
        DrawRect(board, wood);
        DrawRect(board, dark, false, 2f);
        DrawString(font, new Vector2(-w / 2f, board.Position.Y + 21f), Text, HorizontalAlignment.Center, w, Size, paint);
        if (Sub != "") DrawString(font, new Vector2(-w / 2f, board.Position.Y + 37f), Sub, HorizontalAlignment.Center, w, SubSize, paint.Darkened(0.15f));
    }
}
