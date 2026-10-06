using System.Collections.Generic;
using UnityEngine;
using StellarCommand.SaveParser;

namespace StellarCommand.UI
{
    /// <summary>
    /// Master controller for the VR command bridge scene.
    /// Wires the save loader to all holographic panels.
    /// </summary>
    public class CommandBridgeManager : MonoBehaviour
    {
        [Header("Save Loader")]
        public StellarisSaveLoader saveLoader;

        [Header("Panels")]
        public List<HolographicPanel> panels = new List<HolographicPanel>();

        private void OnEnable()
        {
            if (saveLoader != null)
                saveLoader.OnStateUpdated += RefreshAllPanels;
        }

        private void OnDisable()
        {
            if (saveLoader != null)
                saveLoader.OnStateUpdated -= RefreshAllPanels;
        }

        private void Start()
        {
            // Push current state immediately on start (handles loaded-before-subscribe case)
            if (saveLoader != null && saveLoader.CurrentState.IsValid)
                RefreshAllPanels(saveLoader.CurrentState);
        }

        private void RefreshAllPanels(GameState state)
        {
            foreach (var panel in panels)
            {
                if (panel != null) panel.Refresh(state);
            }
        }

        // Called from a UI button or voice command to force reload
        public void ForceReload()
        {
            if (saveLoader != null)
                saveLoader.LoadSave(StellarisSaveLoader.FindLatestSave());
        }
    }
}
