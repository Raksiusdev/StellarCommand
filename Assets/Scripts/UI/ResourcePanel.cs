using TMPro;
using UnityEngine;

namespace StellarCommand.UI
{
    /// <summary>
    /// Holographic panel showing empire resources: energy, minerals, food, alloys, etc.
    /// Each row is a single TMP label laid out with rich-text tab stops (name | stockpile | monthly net).
    /// </summary>
    public class ResourcePanel : HolographicPanel
    {
        [Header("Resource Labels")]
        public TextMeshProUGUI energyLabel;
        public TextMeshProUGUI mineralsLabel;
        public TextMeshProUGUI foodLabel;
        public TextMeshProUGUI alloysLabel;
        public TextMeshProUGUI consumerGoodsLabel;
        public TextMeshProUGUI unityLabel;
        public TextMeshProUGUI influenceLabel;
        public TextMeshProUGUI empireDateLabel;
        public TextMeshProUGUI empireNameLabel;

        // Stellaris-style resource colours
        private const string EnergyColor = "#FFD54A";
        private const string MineralsColor = "#FF7A7A";
        private const string FoodColor = "#7DFF9B";
        private const string AlloysColor = "#C99BFF";
        private const string GoodsColor = "#FFA85A";
        private const string UnityColor = "#B88CFF";
        private const string InfluenceColor = "#FFD27A";

        private const string ValueColor = "#F2FCFF";
        private const string GainColor = "#5CFF9D";
        private const string LossColor = "#FF6B6B";

        protected override void Awake()
        {
            panelTitle = "EMPIRE RESOURCES";
            base.Awake();
        }

        public override void Refresh(SaveParser.GameState state)
        {
            if (state == null) return;

            if (!state.IsValid)
            {
                SetLabel(empireNameLabel, "SAVE ERROR");
                SetLabel(empireDateLabel, state.ParseError ?? "");
                return;
            }

            var r = state.Resources;
            SetLabel(empireNameLabel, state.PlayerEmpireName.ToUpper());
            SetLabel(empireDateLabel, string.IsNullOrEmpty(state.Warning) ? state.Date : "UNSUPPORTED SAVE VERSION");

            SetLabel(energyLabel, Row("ENERGY", EnergyColor, r.Energy, r.EnergyIncome));
            SetLabel(mineralsLabel, Row("MINERALS", MineralsColor, r.Minerals, r.MineralsIncome));
            SetLabel(foodLabel, Row("FOOD", FoodColor, r.Food, r.FoodIncome));
            SetLabel(alloysLabel, Row("ALLOYS", AlloysColor, r.Alloys, r.AlloysIncome));
            SetLabel(consumerGoodsLabel, Row("GOODS", GoodsColor, r.ConsumerGoods, null));
            SetLabel(unityLabel, Cell("UNITY", UnityColor, r.Unity));
            SetLabel(influenceLabel, Cell("INFLUENCE", InfluenceColor, r.Influence));
        }

        // name at the left, stockpile at 38%, net monthly change at 70%
        private static string Row(string name, string nameColor, float amount, float? income)
        {
            string net = "";
            if (income.HasValue)
            {
                string color = income.Value >= 0f ? GainColor : LossColor;
                string sign = income.Value >= 0f ? "+" : "";
                net = $"<pos=70%><size=80%><color={color}>{sign}{income.Value:N1}</color></size>";
            }
            return $"<color={nameColor}>{name}</color><pos=38%><color={ValueColor}>{Compact(amount)}</color>{net}";
        }

        private static string Cell(string name, string nameColor, float amount)
        {
            return $"<color={nameColor}>{name}</color><pos=55%><color={ValueColor}>{Compact(amount)}</color>";
        }

        private static string Compact(float v)
        {
            float abs = Mathf.Abs(v);
            if (abs >= 1_000_000f) return $"{v / 1_000_000f:0.##}M";
            if (abs >= 100_000f) return $"{v / 1_000f:0.#}K";
            return v.ToString("N0");
        }

        private static void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
