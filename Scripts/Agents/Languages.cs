namespace IsometricFurTradeGame.Scripts.Agents;

// What a people speak, and how far one tongue carries into another (design
// doc 10.6). The trade ran on Cree in the north and on Nakoda and signs on
// the plains, but a man with good Cree is no better off in a Blackfoot lodge
// than he would be in a Spanish one: the languages are not kin enough to
// hear. Before this every band person in the country was labelled "Cree",
// which made a Gros Ventre camp sound like a Cree one.
public static class Languages
{
    public static string Name(Faction f) => f switch
    {
        Faction.Cree => "Cree",
        Faction.Ojibwe => "Ojibwe",
        Faction.Assiniboine => "Assiniboine",
        Faction.Sioux => "Dakota",
        Faction.Blackfoot => "Blackfoot",
        Faction.Dene => "Chipewyan",
        Faction.Plateau => "the Plateau tongue",
        Faction.Coast => "the Coast tongue",
        Faction.Missouri => "Mandan",
        Faction.Metis => "Michif",
        _ => "English",
    };

    // How much of `theirs` a speaker of `mine` makes out, 0..1. One is the
    // same tongue; the middling numbers are the real kinships - Michif is
    // half Cree, Dakota and Nakoda are two Siouan dialects, Ojibwe and Cree
    // are close enough to trade in. Everything else is nought: Chipewyan is
    // Athabaskan, the Plateau and Coast tongues are neither, and Blackfoot,
    // for all that it is Algonquian, cannot be heard by a Cree speaker.
    public static float Kin(Faction mine, Faction theirs)
    {
        if (mine == theirs) return 1f;
        return (mine, theirs) switch
        {
            (Faction.Cree, Faction.Metis) or (Faction.Metis, Faction.Cree) => 0.8f,
            (Faction.Cree, Faction.Ojibwe) or (Faction.Ojibwe, Faction.Cree) => 0.6f,
            (Faction.Ojibwe, Faction.Metis) or (Faction.Metis, Faction.Ojibwe) => 0.5f,
            (Faction.Assiniboine, Faction.Sioux) or (Faction.Sioux, Faction.Assiniboine) => 0.8f,
            (Faction.Assiniboine, Faction.Missouri) or (Faction.Missouri, Faction.Assiniboine) => 0.3f,
            (Faction.Sioux, Faction.Missouri) or (Faction.Missouri, Faction.Sioux) => 0.3f,
            (Faction.Cree, Faction.Blackfoot) or (Faction.Blackfoot, Faction.Cree) => 0.1f,
            _ => 0f,
        };
    }

    // The peoples of the country, as against the companies and the ship.
    public static bool IsNation(Faction f) => f is not (Faction.HBC or Faction.NWC or Faction.American or Faction.ShipsCrew or Faction.Wild);
}
