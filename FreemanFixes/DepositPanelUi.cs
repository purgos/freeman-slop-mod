using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Builds native uGUI controls into the game's personal <-> faction transfer panel (UIData.depositPanel):
    // the amount number becomes a typeable InputField and a row of percentage buttons (clones of the panel's own
    // CANCEL button) sits underneath on a copy of the panel's background, so everything matches the game's style.
    //
    // No lambdas / static method groups on purpose: the game's old Mono chokes on newer compiler-generated caches,
    // so every listener is an instance method on one of these components.
    internal class DepositPanelUi : MonoBehaviour
    {
        private const string RootName = "FreemanFixes_Controls";
        private static readonly int[] Percents = { 0, 10, 25, 50, 75, 100 };

        internal static DepositPanelUi Current;

        private UIData ui;
        private InputField input;
        private bool syncing;

        // What Build changed, so Teardown can put the game's panel back exactly (ScriptEngine hot reload).
        private GameObject inputGo;
        private GameObject rowGo;
        private RectTransform amountRt;
        private Transform amountParent;
        private int amountSibling;
        private Vector2 amountAnchorMin, amountAnchorMax, amountPivot, amountPos, amountSize;
        private bool amountRichText;

        internal static void Ensure(UIData ui)
        {
            if (ui == null || ui.depositPanel == null)
            {
                return;
            }
            DepositPanelUi existing = ui.depositPanel.GetComponent<DepositPanelUi>();
            if (existing != null)
            {
                Current = existing;
                return;
            }
            DumpHierarchy(ui.depositPanel.transform);
            Current = ui.depositPanel.AddComponent<DepositPanelUi>();
            Current.Build(ui);
        }

        private void Build(UIData data)
        {
            ui = data;
            Button cancel = FindButton("ButtonDeposit", false);
            BuildInput();
            if (cancel != null)
            {
                BuildPercentRow(cancel);
            }
            else
            {
                Plugin.Log.LogWarning("Transfer panel: no button found to clone, percentage row skipped");
            }
        }

        // OK is wired (in the scene) to UIData.ButtonDeposit; the other button is CANCEL.
        private Button FindButton(string okMethod, bool wantOk)
        {
            foreach (Button b in ui.depositPanel.GetComponentsInChildren<Button>(true))
            {
                bool isOk = false;
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == okMethod)
                    {
                        isOk = true;
                    }
                }
                Text label = b.GetComponentInChildren<Text>(true);
                if (!isOk && label != null && label.text.Trim().ToUpper() == "OK")
                {
                    isOk = true;
                }
                if (isOk == wantOk)
                {
                    return b;
                }
            }
            return null;
        }

        // Wrap the panel's amount Text in an InputField (the Text stays where it is and keeps its font/size).
        private void BuildInput()
        {
            Text amountText = ui.depositText;
            if (amountText == null)
            {
                return;
            }
            var go = new GameObject("FreemanFixes_AmountInput", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            var textRt = (RectTransform)amountText.transform;
            inputGo = go;
            amountRt = textRt;
            amountParent = textRt.parent;
            amountSibling = textRt.GetSiblingIndex();
            amountAnchorMin = textRt.anchorMin;
            amountAnchorMax = textRt.anchorMax;
            amountPivot = textRt.pivot;
            amountPos = textRt.anchoredPosition;
            amountSize = textRt.sizeDelta;
            amountRichText = amountText.supportRichText;
            rt.SetParent(textRt.parent, false);
            rt.SetSiblingIndex(textRt.GetSiblingIndex());
            CopyRect(textRt, rt);

            // Subtle field background so it reads as editable, in the panel's own dark tone.
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.35f);

            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            amountText.supportRichText = false;

            input = go.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = amountText;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 10;
            input.caretColor = amountText.color;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.93f, 0.55f, 0.13f, 0.45f); // game's orange accent
            input.text = ui.amount.ToString();
            input.onValueChanged.AddListener(new UnityAction<string>(OnTyped));
            input.onEndEdit.AddListener(new UnityAction<string>(OnEndEdit));
        }

        private void BuildPercentRow(Button template)
        {
            var panelRt = (RectTransform)ui.depositPanel.transform;
            var templateRt = (RectTransform)template.transform;
            float buttonH = Mathf.Max(templateRt.rect.height, 24f);
            float pad = 8f;

            var row = new GameObject(RootName, typeof(RectTransform));
            rowGo = row;
            var rowRt = (RectTransform)row.transform;
            rowRt.SetParent(panelRt, false);
            // Full panel width, hanging directly below it.
            rowRt.anchorMin = new Vector2(0f, 0f);
            rowRt.anchorMax = new Vector2(1f, 0f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -4f);
            rowRt.sizeDelta = new Vector2(0f, buttonH + pad * 2f);

            Image panelBg = ui.depositPanel.GetComponent<Image>();
            Image rowBg = row.AddComponent<Image>();
            if (panelBg != null)
            {
                rowBg.sprite = panelBg.sprite;
                rowBg.type = panelBg.type;
                rowBg.color = panelBg.color;
                rowBg.material = panelBg.material;
            }
            else
            {
                rowBg.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            }

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)pad * 2, (int)pad * 2, (int)pad, (int)pad);
            layout.spacing = pad;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (int pct in Percents)
            {
                GameObject clone = Instantiate(template.gameObject, rowRt, false);
                clone.name = "FreemanFixes_Pct" + pct;
                clone.SetActive(true);
                Button b = clone.GetComponent<Button>();
                b.onClick = new Button.ButtonClickedEvent(); // drop CANCEL's scene wiring
                Text label = clone.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = pct == 0 ? "0" : (pct == 100 ? "MAX" : pct + "%");
                }
                var pctBtn = clone.AddComponent<PercentButton>();
                pctBtn.Percent = pct;
                b.onClick.AddListener(new UnityAction(pctBtn.Click));
            }
        }

        // Undo everything Build did: move the game's amount Text back where it was and remove our objects.
        internal static void TeardownCurrent()
        {
            if (Current != null)
            {
                Current.Teardown();
                Current = null;
            }
        }

        private void Teardown()
        {
            if (amountRt != null && amountParent != null)
            {
                amountRt.SetParent(amountParent, false);
                amountRt.SetSiblingIndex(amountSibling);
                amountRt.anchorMin = amountAnchorMin;
                amountRt.anchorMax = amountAnchorMax;
                amountRt.pivot = amountPivot;
                amountRt.anchoredPosition = amountPos;
                amountRt.sizeDelta = amountSize;
                Text amountText = amountRt.GetComponent<Text>();
                if (amountText != null)
                {
                    amountText.supportRichText = amountRichText;
                }
            }
            if (inputGo != null)
            {
                Destroy(inputGo);
            }
            if (rowGo != null)
            {
                Destroy(rowGo);
            }
            Destroy(this);
        }

        private static void CopyRect(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localScale = from.localScale;
        }

        internal void ApplyPercent(int pct)
        {
            TransferPatches.SetAmount(ui, (int)((long)TransferPatches.Available(ui) * pct / 100));
        }

        // Keep the field showing the amount whenever it changes from elsewhere (slider, percent buttons, reopen).
        internal void ShowAmount(int amount)
        {
            if (input == null || syncing)
            {
                return;
            }
            syncing = true;
            input.text = amount.ToString();
            syncing = false;
        }

        private void OnTyped(string value)
        {
            if (syncing)
            {
                return;
            }
            int parsed;
            if (!int.TryParse(value, out parsed))
            {
                parsed = 0;
            }
            syncing = true; // let the slider follow without rewriting what's being typed
            TransferPatches.SetAmount(ui, parsed);
            syncing = false;
        }

        private void OnEndEdit(string value)
        {
            ShowAmount(ui.amount); // normalise: clamped value, no leading zeros / empty field
        }

        internal static void DumpHierarchy(Transform root, string label = "Transfer panel")
        {
            var sb = new StringBuilder(label + " hierarchy:\n");
            Dump(root, 0, sb);
            Plugin.Log.LogInfo(sb.ToString());
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (inactive)");
            var rt = t as RectTransform;
            if (rt != null)
            {
                sb.Append(" rect=").Append(rt.rect.width).Append('x').Append(rt.rect.height)
                  .Append(" pos=").Append(rt.anchoredPosition);
            }
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c != null && !(c is Transform))
                {
                    sb.Append(" [").Append(c.GetType().Name).Append(']');
                }
            }
            Text text = t.GetComponent<Text>();
            if (text != null)
            {
                sb.Append(" \"").Append(text.text.Length > 40 ? text.text.Substring(0, 40) + "…" : text.text).Append('"')
                  .Append(" font=").Append(text.fontSize).Append(" wrap=").Append(text.horizontalOverflow);
            }
            sb.Append('\n');
            foreach (Transform child in t)
            {
                Dump(child, depth + 1, sb);
            }
        }
    }

    internal class PercentButton : MonoBehaviour
    {
        public int Percent;

        public void Click()
        {
            if (DepositPanelUi.Current != null)
            {
                DepositPanelUi.Current.ApplyPercent(Percent);
            }
        }
    }
}
