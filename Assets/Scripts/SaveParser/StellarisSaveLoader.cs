using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace StellarCommand.SaveParser
{
    /// <summary>
    /// Finds the latest Stellaris save, parses it on a worker thread (the gamestate is tens of MB)
    /// and publishes the result on the main thread. See StellarisSaveReader for the parsing itself.
    /// Attach this to a GameObject in the scene.
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
        private bool _loading;
        private GameState _pending; // written by the worker thread, consumed in Update

        private void Start()
        {
            _resolvedPath = string.IsNullOrEmpty(savePath) ? FindLatestSave() : savePath;
            if (string.IsNullOrEmpty(_resolvedPath))
            {
                Debug.LogWarning("[StellarCommand] No Stellaris save found. Expected in Documents/Paradox Interactive/Stellaris/save games.");
                return;
            }
            LoadSave(_resolvedPath);
        }

        private void Update()
        {
            var finished = Interlocked.Exchange(ref _pending, null);
            if (finished != null)
            {
                _loading = false;
                CurrentState = finished;
                if (finished.ParseError != null)
                    Debug.LogError($"[StellarCommand] Failed to load save: {finished.ParseError}");
                else
                {
                    if (finished.Warning != null) Debug.LogWarning($"[StellarCommand] {finished.Warning}");
                    Debug.Log($"[StellarCommand] Loaded save: {finished.PlayerEmpireName} — {finished.Date} ({finished.GameVersion})");
                }
                OnStateUpdated?.Invoke(finished);
            }

            if (!autoPoll || string.IsNullOrEmpty(_resolvedPath) || _loading) return;
            _pollTimer += Time.deltaTime;
            if (_pollTimer < pollIntervalSeconds) return;
            _pollTimer = 0f;

            try
            {
                if (File.GetLastWriteTime(_resolvedPath) != _lastModified)
                    LoadSave(_resolvedPath);
            }
            catch { /* file may be locked during save */ }
        }

        /// <summary>Starts a background load. Ignored while another load is running.</summary>
        public void LoadSave(string path)
        {
            if (_loading || string.IsNullOrEmpty(path)) return;

            _loading = true;
            _resolvedPath = path;
            try { _lastModified = File.GetLastWriteTime(path); } catch { /* ignore */ }

            Task.Run(() =>
            {
                GameState result;
                try { result = StellarisSaveReader.Read(path); }
                catch (Exception e) { result = new GameState { ParseError = e.Message }; }
                Interlocked.Exchange(ref _pending, result);
            });
        }

        // Looks for the most recently modified .sav in the default Stellaris save location
        // (MyDocuments follows OneDrive redirection).
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
