using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace StellarCommand.SaveParser
{
    /// <summary>
    /// Reads a Stellaris .sav (ZIP with a PDX-script "gamestate") into a GameState.
    /// No UnityEngine dependency, so it is safe to run on a worker thread and to test outside Unity.
    ///
    /// The gamestate is tens of MB, so instead of parsing everything we scan top-level sections
    /// and only parse the blocks we need (player country, owned fleets, systems).
    /// Data locations differ between game versions, so every lookup tries a list of candidate
    /// paths, newest layout first. Missing data is reported through GameState.Warning.
    /// </summary>
    public static class StellarisSaveReader
    {
        // Candidate paths inside the player's country block (newest layout first).
        private static readonly string[][] ResourcePaths =
        {
            new[] { "modules", "standard_economy_module", "resources" }, // 3.x / 4.x
            new[] { "resources" },                                       // very old layout
        };

        private static readonly string[][] BalancePaths =
        {
            new[] { "budget", "current_month", "balance" },
            new[] { "budget", "last_month", "balance" },
        };

        public static GameState Read(string path)
        {
            string text = ExtractGamestate(path);
            return Build(text);
        }

        public static GameState Build(string text)
        {
            var state = new GameState
            {
                GameVersion = ReadTopLevelString(text, "version") ?? "",
                Date = ReadTopLevelString(text, "date") ?? "2200.01.01",
            };

            // Player country id. The player block holds an anonymous { name=... country=N } entry;
            // the parser flattens anonymous blocks so "country" is a direct child.
            int playerCountryId = 0;
            var playerNode = ParseSection(text, "player");
            if (playerNode != null)
                playerCountryId = playerNode.GetInt("country", 0);

            var countryNode = ParseEntry(text, "country", playerCountryId.ToString());
            if (countryNode == null)
            {
                state.Warning = $"Player country {playerCountryId} not found (game version {state.GameVersion}).";
            }
            else
            {
                state.PlayerEmpireName = ResolveEmpireName(text, countryNode);
                state.Resources = ParseResources(countryNode, state);
                state.ActiveAlerts = ParseAlerts(countryNode);
                state.Fleets = ParseOwnedFleets(text, countryNode);
            }

            state.Systems = ParseSystems(text);
            state.IsValid = true;
            return state;
        }

        // ---------------------------------------------------------------- archive

        private static string ExtractGamestate(string path)
        {
            // ReadWrite share so we can read while Stellaris is autosaving.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.GetEntry("gamestate");
            if (entry == null)
                throw new InvalidDataException("No 'gamestate' entry found in save file.");
            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // ---------------------------------------------------------------- data extraction

        private static EmpireResources ParseResources(PdxScriptParser.PdxNode country, GameState state)
        {
            var res = new EmpireResources();

            var stockpile = FindFirst(country, ResourcePaths);
            if (stockpile == null)
            {
                state.Warning = $"Resources not found in save (game version {state.GameVersion} may be unsupported).";
                return res;
            }

            res.Energy = stockpile.GetFloat("energy");
            res.Minerals = stockpile.GetFloat("minerals");
            res.Food = stockpile.GetFloat("food");
            res.Alloys = stockpile.GetFloat("alloys");
            res.ConsumerGoods = stockpile.GetFloat("consumer_goods");
            res.Unity = stockpile.GetFloat("unity");
            res.Influence = stockpile.GetFloat("influence");

            // Net monthly change = sum of every category in the budget balance.
            var balance = FindFirst(country, BalancePaths);
            if (balance != null)
            {
                foreach (var category in balance.Children)
                {
                    res.EnergyIncome += category.GetFloat("energy");
                    res.MineralsIncome += category.GetFloat("minerals");
                    res.FoodIncome += category.GetFloat("food");
                    res.AlloysIncome += category.GetFloat("alloys");
                }
            }

            return res;
        }

        private static List<string> ParseAlerts(PdxScriptParser.PdxNode country)
        {
            var alerts = new List<string>();
            var alertsNode = country.GetChild("alerts");
            if (alertsNode == null) return alerts;
            foreach (var alert in alertsNode.Children)
                if (!string.IsNullOrEmpty(alert.Value))
                    alerts.Add(alert.Value);
            return alerts;
        }

        private static List<Fleet> ParseOwnedFleets(string text, PdxScriptParser.PdxNode country)
        {
            var fleets = new List<Fleet>();

            var owned = new HashSet<string>();
            var ownedNode = country.GetChild("fleets_manager")?.GetChild("owned_fleets");
            if (ownedNode == null) return fleets;
            foreach (var entry in ownedNode.GetChildren("fleet"))
                owned.Add(entry.Value);
            if (owned.Count == 0) return fleets;

            ForEachEntry(text, "fleet", (id, start, end) =>
            {
                if (!owned.Contains(id)) return;
                var node = PdxScriptParser.Parse(text.Substring(start, end - start));

                // Starbases are stored as fleets too; skip them.
                if (node.GetChild("settings")?.GetValue("station") == "yes") return;

                int ships = node.GetChild("ships")?.Children.Count ?? 0;
                var coord = node.GetChild("combat")?.GetChild("coordinate");
                fleets.Add(new Fleet
                {
                    Name = ResolveName(node.GetChild("name"), "Fleet"),
                    ShipCount = ships,
                    MilitaryPower = node.GetFloat("military_power"),
                    PositionX = coord?.GetFloat("x") ?? 0f,
                    PositionY = coord?.GetFloat("y") ?? 0f,
                });
            });

            return fleets;
        }

        private static List<StarSystem> ParseSystems(string text)
        {
            var systems = new List<StarSystem>();
            ForEachEntry(text, "galactic_object", (id, start, end) =>
            {
                if (!int.TryParse(id, out int sysId)) return;
                var node = PdxScriptParser.Parse(text.Substring(start, end - start));
                var coord = node.GetChild("coordinate");

                var targets = new List<string>();
                var lanes = node.GetChild("hyperlane");
                if (lanes != null)
                    foreach (var to in lanes.GetChildren("to"))
                        targets.Add(to.Value);

                systems.Add(new StarSystem
                {
                    Id = sysId,
                    Name = ResolveName(node.GetChild("name"), "System"),
                    X = coord?.GetFloat("x") ?? 0f,
                    Y = coord?.GetFloat("y") ?? 0f,
                    HyperlaneTargets = string.Join(",", targets),
                });
            });
            return systems;
        }

        // ---------------------------------------------------------------- names

        private static string ResolveEmpireName(string text, PdxScriptParser.PdxNode country)
        {
            // Country names are localisation templates ("%ADJECTIVE%" + variables), so we cannot
            // resolve them without the game's loc files. The save's own top-level name is the
            // empire's name for the player's autosaves/ironman saves, so prefer it.
            string saveName = ReadTopLevelString(text, "name");
            if (!string.IsNullOrEmpty(saveName)) return saveName;
            return ResolveName(country.GetChild("name"), "Unknown Empire");
        }

        /// <summary>
        /// Names are either a plain string or a block { key="..." variables={...} }.
        /// Returns the innermost readable key, with localisation prefixes cleaned up.
        /// </summary>
        private static string ResolveName(PdxScriptParser.PdxNode nameNode, string fallback)
        {
            if (nameNode == null) return fallback;
            if (!nameNode.IsBlock) return string.IsNullOrEmpty(nameNode.Value) ? fallback : nameNode.Value;

            // Descend name → variables → value → variables → value ... keeping the deepest real key.
            // The parser flattens anonymous { } blocks, so "value" is a direct child of "variables".
            string best = null;
            var node = nameNode;
            while (node != null)
            {
                string key = node.GetValue("key");
                if (!string.IsNullOrEmpty(key) && !key.StartsWith("%", StringComparison.Ordinal))
                    best = key;
                node = node.GetChild("variables")?.GetChild("value");
            }

            return best == null ? fallback : CleanLocKey(best);
        }

        private static string CleanLocKey(string key)
        {
            foreach (var prefix in new[] { "SPEC_", "NAME_", "NAME" })
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    key = key.Substring(prefix.Length);
                    break;
                }
            }
            return key.Replace('_', ' ').Trim();
        }

        // ---------------------------------------------------------------- text scanning

        private static PdxScriptParser.PdxNode FindFirst(PdxScriptParser.PdxNode root, string[][] candidates)
        {
            foreach (var path in candidates)
            {
                var node = root;
                foreach (var key in path)
                {
                    node = node?.GetChild(key);
                    if (node == null) break;
                }
                if (node != null) return node;
            }
            return null;
        }

        private static int FindTopLevelKey(string text, string key)
        {
            string needle = key + "=";
            if (text.StartsWith(needle, StringComparison.Ordinal)) return 0;
            int i = text.IndexOf("\n" + needle, StringComparison.Ordinal);
            return i < 0 ? -1 : i + 1;
        }

        private static string ReadTopLevelString(string text, string key)
        {
            int i = FindTopLevelKey(text, key);
            if (i < 0) return null;
            i += key.Length + 1;
            if (i >= text.Length) return null;
            if (text[i] == '"')
            {
                int end = text.IndexOf('"', i + 1);
                return end < 0 ? null : text.Substring(i + 1, end - i - 1);
            }
            int stop = i;
            while (stop < text.Length && text[stop] != '\n' && text[stop] != '\r') stop++;
            return text.Substring(i, stop - i).Trim();
        }

        /// <summary>Returns the index of the '{' that opens a top-level section, or -1.</summary>
        private static int FindSectionOpen(string text, string key)
        {
            int i = FindTopLevelKey(text, key);
            if (i < 0) return -1;
            int p = i + key.Length + 1;
            while (p < text.Length && char.IsWhiteSpace(text[p])) p++;
            return p < text.Length && text[p] == '{' ? p : -1;
        }

        private static PdxScriptParser.PdxNode ParseSection(string text, string key)
        {
            int open = FindSectionOpen(text, key);
            if (open < 0) return null;
            int end = FindBlockEnd(text, open);
            return PdxScriptParser.Parse(text.Substring(open + 1, end - open - 2));
        }

        private static PdxScriptParser.PdxNode ParseEntry(string text, string section, string id)
        {
            PdxScriptParser.PdxNode result = null;
            ForEachEntry(text, section, (entryId, start, end) =>
            {
                if (result == null && entryId == id)
                    result = PdxScriptParser.Parse(text.Substring(start, end - start));
            }, stopAfterId: id);
            return result;
        }

        /// <summary>
        /// Walks the direct children of a top-level section ("id={ ... }"), skipping each block
        /// by brace matching. Calls back with the id and the inner range of each block.
        /// </summary>
        private static void ForEachEntry(string text, string section, Action<string, int, int> onEntry,
            string stopAfterId = null)
        {
            int open = FindSectionOpen(text, section);
            if (open < 0) return;

            int pos = open + 1;
            int len = text.Length;
            while (pos < len)
            {
                while (pos < len && char.IsWhiteSpace(text[pos])) pos++;
                if (pos >= len || text[pos] == '}') return;

                int keyStart = pos;
                while (pos < len && text[pos] != '=' && text[pos] != '{' && text[pos] != '}'
                       && !char.IsWhiteSpace(text[pos])) pos++;
                string key = text.Substring(keyStart, pos - keyStart);

                while (pos < len && char.IsWhiteSpace(text[pos])) pos++;
                if (pos >= len) return;
                if (text[pos] != '=') continue; // bare token, ignore

                pos++;
                while (pos < len && char.IsWhiteSpace(text[pos])) pos++;
                if (pos >= len) return;

                if (text[pos] == '{')
                {
                    int end = FindBlockEnd(text, pos);
                    onEntry(key, pos + 1, end - 1);
                    pos = end;
                    if (stopAfterId != null && key == stopAfterId) return;
                }
                else
                {
                    // scalar entry (e.g. "none"): skip the value token
                    while (pos < len && !char.IsWhiteSpace(text[pos])) pos++;
                }
            }
        }

        /// <summary>Given the index of '{', returns the index just past the matching '}'.</summary>
        private static int FindBlockEnd(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
            }
            return text.Length;
        }
    }
}
