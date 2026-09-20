using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

public partial class CalendarHud : Label
{
    private string _lastEvent = "";
    private float _eventTimer;

    public override void _Ready()
    {
        var cal = GameCalendar.Instance;
        if (cal != null)
        {
            cal.CalendarEvent += name =>
            {
                _lastEvent = name switch
                {
                    "ShipArrives" => "The ship is in from England",
                    "Brigade" => "The Nor'Westers' brigade is in from Fort William",
                    "ShipDeparts" => "The ship has sailed",
                    "FreezeUp" => "The bay has frozen",
                    "Breakup" => "The ice is going out",
                    "SummerBisonHunt" => "The summer hunt is out on the plains",
                    "FallBisonHunt" => "The fall hunt is out on the plains",
                    _ => name,
                };
                _eventTimer = 8f;
            };
        }
    }

    public override void _Process(double delta)
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;

        string fast = Input.IsActionPressed("time_fast") ? "  >>" : "";
        Text = $"{cal.DateString}   {cal.TimeString}{fast}\n{cal.SeasonString} - {cal.Weather.Describe()}";

        if (_eventTimer > 0f)
        {
            _eventTimer -= (float)delta;
            Text += $"\n\n{_lastEvent}";
        }
    }
}
