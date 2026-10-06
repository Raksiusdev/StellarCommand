using System.Collections.Generic;

namespace StellarCommand.SaveParser
{
    public class GameState
    {
        public string Date { get; set; } = "2200.01.01";
        public string PlayerEmpireName { get; set; } = "Unknown Empire";
        public EmpireResources Resources { get; set; } = new EmpireResources();
        public List<Fleet> Fleets { get; set; } = new List<Fleet>();
        public List<StarSystem> Systems { get; set; } = new List<StarSystem>();
        public List<string> ActiveAlerts { get; set; } = new List<string>();
        public bool IsValid { get; set; } = false;
        public string ParseError { get; set; } = null;
    }

    public class EmpireResources
    {
        public float Energy { get; set; }
        public float Minerals { get; set; }
        public float Food { get; set; }
        public float Alloys { get; set; }
        public float ConsumerGoods { get; set; }
        public float Unity { get; set; }
        public float Influence { get; set; }

        // Monthly income (deltas)
        public float EnergyIncome { get; set; }
        public float MineralsIncome { get; set; }
        public float FoodIncome { get; set; }
        public float AlloysIncome { get; set; }
    }

    public class Fleet
    {
        public string Name { get; set; } = "Unknown Fleet";
        public int ShipCount { get; set; }
        public float MilitaryPower { get; set; }
        public string Status { get; set; } = "Idle";
        public float PositionX { get; set; }
        public float PositionY { get; set; }
    }

    public class StarSystem
    {
        public string Name { get; set; } = "Unknown System";
        public int Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public bool IsPlayerOwned { get; set; }
        public bool IsColonized { get; set; }
        public string HyperlaneTargets { get; set; }
    }
}
