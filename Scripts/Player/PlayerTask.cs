using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Player;

public enum TaskKind { Firewood, CarryCargo, DriveOffAnimals, DeliverLetter, Raid, None }

// An assignment the Factor hands the player (design doc 3.9, seen from the
// subordinate's side): intent and a count, not steps. Doing it earns
// standing with the Company; ignoring it costs a little.
public class PlayerTask
{
    public TaskKind Kind = TaskKind.None;
    public int Need;
    public int Done;
    public int DayAssigned;

    public bool IsComplete => Kind != TaskKind.None && Done >= Need;

    public string Describe() => Kind switch
    {
        TaskKind.Firewood => $"Mr. Auld: bring {Need} firewood to the woodpile ({Done}/{Need})",
        TaskKind.CarryCargo => $"Mr. Auld: carry {Need} lots from the ship to the warehouse ({Done}/{Need})",
        TaskKind.DriveOffAnimals => $"Mr. Auld: drive off the animals about the post ({Done}/{Need})",
        TaskKind.DeliverLetter => "Mr. Auld: carry his letter to the chief at the camp and bring back his answer",
        TaskKind.Raid => $"Go with the men and seize what you can from the other company's stores ({Done}/{Need} lots)",
        _ => "",
    };

    public void Progress(TaskKind kind, int n)
    {
        if (Kind != kind || IsComplete) return;
        Done += n;
        if (IsComplete) Sim.Notice("Task done. The Factor will hear of it.");
    }
}
