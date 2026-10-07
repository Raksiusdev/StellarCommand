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

        /// <summary>Game version string from the save, e.g. "Pegasus v4.4.6".</summary>
        public string GameVersion { get; set; } = "";

        /// <summary>Country id of the player's empire.</summary>
        public int PlayerCountryId { get; set; }

        /// <summary>Non-fatal problem, e.g. data not found because the save format is unsupported.</summary>
        public string Warning { get; set; } = null;
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
        /// <summary>Vertical offset in galaxy units (Stellaris stores a small height per system).</summary>
        public float Height { get; set; }
        /// <summary>Star class key such as "sc_f".</summary>
        public string StarClass { get; set; } = "";
        /// <summary>Country id that owns the sector this system belongs to, or -1 when unowned.</summary>
        public int OwnerId { get; set; } = -1;
        public bool IsPlayerOwned { get; set; }
        public bool IsColonized { get; set; }
        public string HyperlaneTargets { get; set; }
    }
}
