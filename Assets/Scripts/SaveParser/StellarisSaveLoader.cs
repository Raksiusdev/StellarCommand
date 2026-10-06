using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace StellarCommand.SaveParser
{
    /// <summary>
    /// Loads and parses Stellaris .sav files (ZIP archives containing a PDX-script "gamestate" file).
    /// Attach this to a GameObject in the scene and call Load() or enable auto-polling.
    /// </summary>
    public class StellarisSaveLoader : MonoBehaviour
    {
        [Header("Save File")]
        [Tooltip("Full path to the Stellaris .sav file. Leave empty to auto-detect latest save.")]
        public string savePath = "";

        [Header("Auto-Poll")]
        [Tooltip("Automatically reload when the save file changes.")]
        public bool autoPoll = true;
        public float pollIntervalSeconds = 5f;

        public GameState CurrentState { get; private set; } = new GameState();
        public event Action<GameState> OnStateUpdated;

        private float _pollTimer;
        private DateTime _lastModified;
        private string _resolvedPath;

        private void Start()
        {
            _resolvedPath = string.IsNullOrEmpty(savePath) ? FindLatestSave() : savePath;
            if (!string.IsNullOrEmpty(_resolvedPath))
                LoadSave(_resolvedPath);
        }

        private void Update()
        {
            if (!autoPoll || string.IsNullOrEmpty(_resolvedPath)) return;
            _pollTimer += Time.deltaTime;
            if (_pollTimer < pollIntervalSeconds) return;
            _pollTimer = 0f;

            try
            {
                var modified = File.GetLastWriteTime(_resolvedPath);
                if (modified != _lastModified)
                    LoadSave(_resolvedPath);
            }
            catch { /* file may be locked during save */ }
        }

        public void LoadSave(string path)
        {
            try
            {
                _resolvedPath = path;
                _lastModified = File.GetLastWriteTime(path);
                string gamestate = ExtractGamestate(path);
                var root = PdxScriptParser.Parse(gamestate);
                CurrentState = BuildGameState(root);
                CurrentState.IsValid = true;
                OnStateUpdated?.Invoke(CurrentState);
                Debug.Log($"[StellarCommand] Loaded save: {CurrentState.PlayerEmpireName} — {CurrentState.Date}");
            }
            catch (Exception e)
            {
                CurrentState = new GameState { ParseError = e.Message };
                Debug.LogError($"[StellarCommand] Failed to load save: {e.Message}");
            }
        }

        private static string ExtractGamestate(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("gamestate");
            if (entry == null)
                throw new InvalidDataException("No 'gamestate' entry found in save file.");
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static GameState BuildGameState(PdxScriptParser.PdxNode root)
        {
            var state = new GameState();

            state.Date = root.GetValue("date", "2200.01.01");

            // Find player country index
            int playerCountryId = 0;
            var playerNode = root.GetChild("player");
            if (playerNode != null)
            {
                var firstPlayer = playerNode.GetChild("0");
                if (firstPlayer != null)
                    playerCountryId = firstPlayer.GetInt("country", 0);
            }

            // Parse countries to find player empire
            var countriesNode = root.GetChild("country");
            if (countriesNode != null)
            {
                var countryNode = countriesNode.GetChild(playerCountryId.ToString());
                if (countryNode != null)
                {
                    state.PlayerEmpireName = countryNode.GetValue("name", "Unknown Empire");
                    state.Resources = ParseResources(countryNode);
                    state.ActiveAlerts = ParseAlerts(countryNode);
                }
            }

            // Parse fleets
            var fleetsNode = root.GetChild("fleet");
            if (fleetsNode != null)
            {
                foreach (var fleetEntry in fleetsNode.Children)
                {
                    if (!int.TryParse(fleetEntry.Key, out _)) continue;
                    var fleet = new Fleet
                    {
                        Name = fleetEntry.GetValue("name", "Fleet"),
                        ShipCount = fleetEntry.GetInt("num_ships", 0),
                        MilitaryPower = fleetEntry.GetFloat("military_power", 0f),
                    };
                    state.Fleets.Add(fleet);
                }
            }

            // Parse galaxy systems
            var galaxyNode = root.GetChild("galactic_object");
            if (galaxyNode != null)
            {
                foreach (var sysEntry in galaxyNode.Children)
                {
                    if (!int.TryParse(sysEntry.Key, out int sysId)) continue;
                    var coordNode = sysEntry.GetChild("coordinate");
                    var system = new StarSystem
                    {
                        Id = sysId,
                        Name = sysEntry.GetValue("name", "System"),
                        X = coordNode?.GetFloat("x") ?? 0f,
                        Y = coordNode?.GetFloat("y") ?? 0f,
                    };
                    state.Systems.Add(system);
                }
            }

            return state;
        }

        private static EmpireResources ParseResources(PdxScriptParser.PdxNode countryNode)
        {
            var res = new EmpireResources();
            var stockpile = countryNode.GetChild("resources");
            if (stockpile == null) return res;

            res.Energy = stockpile.GetFloat("energy");
            res.Minerals = stockpile.GetFloat("minerals");
            res.Food = stockpile.GetFloat("food");
            res.Alloys = stockpile.GetFloat("alloys");
            res.ConsumerGoods = stockpile.GetFloat("consumer_goods");
            res.Unity = stockpile.GetFloat("unity");
            res.Influence = stockpile.GetFloat("influence");

            var income = countryNode.GetChild("income");
            if (income != null)
            {
                res.EnergyIncome = income.GetFloat("energy");
                res.MineralsIncome = income.GetFloat("minerals");
                res.FoodIncome = income.GetFloat("food");
                res.AlloysIncome = income.GetFloat("alloys");
            }

            return res;
        }

        private static System.Collections.Generic.List<string> ParseAlerts(PdxScriptParser.PdxNode countryNode)
        {
            var alerts = new System.Collections.Generic.List<string>();
            var alertsNode = countryNode.GetChild("alerts");
            if (alertsNode == null) return alerts;
            foreach (var alert in alertsNode.Children)
                if (!string.IsNullOrEmpty(alert.Value))
                    alerts.Add(alert.Value);
            return alerts;
        }

        // Looks for the most recently modified .sav in default Stellaris save location
        public static string FindLatestSave()
        {
            string savesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Paradox Interactive", "Stellaris", "save games");

            if (!Directory.Exists(savesRoot)) return null;

            string newest = null;
            DateTime newestTime = DateTime.MinValue;

            foreach (var file in Directory.EnumerateFiles(savesRoot, "*.sav", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTime(file);
                if (t > newestTime) { newestTime = t; newest = file; }
            }

            return newest;
        }
    }
}
