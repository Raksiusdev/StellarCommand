using TMPro;
using UnityEngine;

namespace StellarCommand.UI
{
    /// <summary>
    /// Holographic panel showing empire resources: energy, minerals, food, alloys, etc.
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

        protected override void Awake()
        {
            panelTitle = "EMPIRE RESOURCES";
            base.Awake();
        }

        public override void Refresh(SaveParser.GameState state)
        {
            if (state == null || !state.IsValid) return;

            var r = state.Resources;
            SetLabel(empireNameLabel, state.PlayerEmpireName.ToUpper());
            SetLabel(empireDateLabel, state.Date);
            SetLabel(energyLabel, FormatResource("ENERGY", r.Energy, r.EnergyIncome));
            SetLabel(mineralsLabel, FormatResource("MINERALS", r.Minerals, r.MineralsIncome));
            SetLabel(foodLabel, FormatResource("FOOD", r.Food, r.FoodIncome));
            SetLabel(alloysLabel, FormatResource("ALLOYS", r.Alloys, r.AlloysIncome));
            SetLabel(consumerGoodsLabel, $"GOODS    {r.ConsumerGoods:N0}");
            SetLabel(unityLabel, $"UNITY    {r.Unity:N0}");
            SetLabel(influenceLabel, $"INFLUENCE {r.Influence:N0}");
        }

        private static string FormatResource(string name, float amount, float income)
        {
            string incomeStr = income >= 0 ? $"+{income:N1}" : $"{income:N1}";
            return $"{name,-10} {amount,8:N0}  ({incomeStr}/mo)";
        }

        private static void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label != null) label.text = text;
        }
    }
}
