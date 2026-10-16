using Ex;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Settings > General: a "Dev Mode" toggle (clone of the game's Friendly Fire toggle) between the Back and Default
    // buttons. Shown while [Dev Mode] ShowSettingsToggle is true; switches [Dev Mode] Enabled.
    internal class DevModeToggle : MonoBehaviour
    {
        private const string Name = "FreemanSlop_DevModeToggle";
        // Toggle-grid coordinates (general panel): Friendly Fire is at (365, -27) and Reverse Trigger at (365, -56);
        // this lands the checkbox in the gap between Back and Default, on their row.
        private static readonly Vector2 Position = new Vector2(275f, -107f);

        private static GameObject toggleObject;
        private static Toggle toggle;
        private static bool syncing;

        internal static void Sync(UISettings settings)
        {
            bool show = DevMode.ShowSettingsToggle != null && DevMode.ShowSettingsToggle.Value;
            if (show && toggleObject == null && settings != null)
            {
                Build(settings);
            }
            if (toggleObject != null)
            {
                toggleObject.SetActive(show);
            }
            Refresh();
        }

        internal static void Refresh()
        {
            if (toggle == null)
            {
                return;
            }
            syncing = true;
            toggle.isOn = DevMode.On;
            syncing = false;
        }

        internal static void Teardown()
        {
            if (toggleObject != null)
            {
                Destroy(toggleObject);
            }
            toggleObject = null;
            toggle = null;
        }

        private static void Build(UISettings settings)
        {
            Transform template = settings.transform.Find("GameObject/bg/general panel/toggle FriendlyFire");
            if (template == null)
            {
                Plugin.Log.LogWarning("Dev mode: Settings toggle template not found");
                return;
            }
            toggleObject = Instantiate(template.gameObject, template.parent, false);
            toggleObject.name = Name;
            ((RectTransform)toggleObject.transform).anchoredPosition = Position;
            toggle = toggleObject.GetComponent<Toggle>();
            toggle.onValueChanged = new Toggle.ToggleEvent(); // drop Friendly Fire's wiring
            toggle.interactable = true;
            Transform labelT = toggleObject.transform.Find("Label");
            Text label = labelT != null ? labelT.GetComponent<Text>() : null;
            if (label != null)
            {
                LocalizationText loc = label.GetComponent<LocalizationText>();
                if (loc != null)
                {
                    DestroyImmediate(loc);
                }
                label.text = "Dev Mode";
            }
            var me = toggleObject.AddComponent<DevModeToggle>();
            toggle.onValueChanged.AddListener(new UnityAction<bool>(me.Changed));
        }

        public void Changed(bool on)
        {
            if (!syncing && DevMode.Enabled.Value != on)
            {
                DevMode.Enabled.Value = on;
            }
        }
    }

    // Lives on the cheat panel: with dev mode off the whole End-key cheat panel stays closed (vanilla cheats too).
    internal class CheatGate : MonoBehaviour
    {
        private static CheatGate current;

        internal static void Attach(UICheat cheat)
        {
            if (cheat != null && cheat.GetComponent<CheatGate>() == null)
            {
                current = cheat.gameObject.AddComponent<CheatGate>();
            }
        }

        internal static void Detach()
        {
            if (current != null)
            {
                Destroy(current);
            }
            current = null;
        }

        private void Update()
        {
            if (!DevMode.On)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
