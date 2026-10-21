using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Player;

public enum TaskKind { Firewood, CarryCargo, DriveOffAnimals, DeliverLetter, Raid, Report, Courier, Geese, Brigade, None }

// An assignment the Factor hands the player (design doc 3.9, seen from the
// subordinate's side): intent and a count, not steps. Doing it earns
// standing with the Company; ignoring it costs a little.
public class PlayerTask
{
    public TaskKind Kind = TaskKind.None;
    public int Need;
    public int Done;
    public int DayAssigned;
    public string Intent = "";      // why, in the leader's words (design doc 3.5: intent, not reasoning)
    public int Deadline;            // day it's wanted by
    public bool Accepted;           // you said you would
    public bool OwnWay;             // he does not mind how (design doc 3.9)
    public string Convoy = "", Dest = "";   // TaskKind.Brigade: which brigade, and where it is bound

    public bool IsComplete => Kind != TaskKind.None && Done >= Need;

    // Who gave it: the master of your post as you are called to him ("Mr. Auld"
    // at York, "Mr. Cameron" across the river), set when it is handed out.
    public string Boss = "";
    private string B => Boss != "" ? Boss : "The master";

    public string Describe() => Kind switch
    {
        TaskKind.Firewood => $"{B}: bring {Need} firewood to the woodpile ({Done}/{Need})",
        TaskKind.CarryCargo => $"{B}: carry {Need} lots from the ship to the warehouse ({Done}/{Need})",
        TaskKind.DriveOffAnimals => $"{B}: drive off the animals about the post ({Done}/{Need})",
        TaskKind.DeliverLetter => Done == 0 ? $"{B}: carry his letter to the chief at the camp" : $"{B}: bring the chief's answer back to him",
        TaskKind.Raid => $"Go with the men and seize what you can from the other company's stores ({Done}/{Need} lots)",
        TaskKind.Report => $"Report to {B}: find him and speak to him",
        TaskKind.Courier => $"Carry the outfit up the river road and stow it at Oxford House ({Done}/{Need} lots)",
        TaskKind.Brigade => IsComplete ? $"Went with {Convoy} to {Dest}" : $"Go with {Convoy} to {Dest}: catch up with the boats, ask the guide for a place, and ride in with them",
        TaskKind.Geese => $"To the marsh while the flight lasts: bring {Need} geese to the stores ({Done}/{Need})",
        _ => "",
    } + (Intent != "" ? $" - {Intent}" : "") + (OwnWay ? " [your own way]" : "");

    // Told of every piece of work of a kind, assigned or not: a quest that
    // asks for six lots carried counts them whatever the Factor said today.
    public System.Action<TaskKind, int> Counted;

    public void Progress(TaskKind kind, int n)
    {
        Counted?.Invoke(kind, n);
        if (Kind != kind || IsComplete) return;
        Done += n;
        if (IsComplete) Sim.Notice("Task done. The Factor will hear of it.");
    }
}
