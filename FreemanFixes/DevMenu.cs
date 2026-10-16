using System.Collections.Generic;
using Ex;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Full-screen dev menu, opened from the cheat panel's "Dev Menu" button (vanilla "Add All Items", see DevMode).
    // Styled from the game's own pieces: the trade screen's backdrop, the character screen's panel background,
    // orange section headers, row text and RENAME-style buttons, so it reads as one of the game's screens.
    // Tabs: General = Player (money, reputation, morale, XP), Tools (item store + free toggle), Troops (add any
    // soldier type); Factions = relation + money per faction; Companions = see DevMenuCompanions.cs.
    internal partial class DevMenu : MonoBehaviour
    {
        private const string RootName = "FreemanSlop_DevMenu";
        private const int SortingOrder = 120;
        private const float RowHeight = 34f;
        private const float HeaderHeight = 28f;
        private const float TextSize = 21f;
        private const float PlayerColumnWidth = 560f;
        private const float FactionColumnWidth = 1000f;
        private const float TroopColumnWidth = 640f;

        private const int ActMoney = 1;
        private const int ActReputation = 2;
        private const int ActMorale = 3;
        private const int ActXp = 4;
        private const int ActRelation = 5;
        private const int ActFactionMoney = 6;
        private const int ActAddTroops = 7;
        private const int ActStore = 8;
        private const int ActStoreFree = 9;
        private const int ActClose = 10;
        private const int ActAllNeutral = 17;
        private const int ActAllHostile = 19;
        private const int ActAllAllies = 20;
        private const int ActMoralePreset = 22; // arg = morale value

        private static readonly Color HostileColor = new Color(0.95f, 0.2f, 0.15f);
        private static readonly Color UnfriendlyColor = new Color(0.93f, 0.55f, 0.13f);
        private static readonly Color FriendlyColor = new Color(0.35f, 0.85f, 0.35f);

        internal static DevMenu Instance;

        // Templates (the game's own UI objects) that every element copies its look from.
        private RawImage tBackdrop;
        private RawImage tPanel;
        private RawImage tHeaderBar;
        private Text tHeaderText;
        private Text tRowText;
        private Text tTitle;
        private GameObject tButton;

        private readonly Dictionary<int, InputField> inputs = new Dictionary<int, InputField>();
        private readonly Dictionary<int, Text> values = new Dictionary<int, Text>();
        private readonly List<Faction> factions = new List<Faction>();
        private readonly List<Text> factionNames = new List<Text>();
        private readonly List<Transform> soldiers = new List<Transform>();
        private Text storeFreeLabel;

        internal static void Toggle()
        {
            if (Instance != null && Instance.gameObject.activeSelf)
            {
                Instance.Close();
            }
            else
            {
                Open();
            }
        }

        internal static void Open()
        {
            if (MapAgent.code == null)
            {
                DevStore.Prompt("Load a game first");
                return;
            }
            if (Instance == null && !Build())
            {
                return;
            }
            if (UICheat.code != null)
            {
                UICheat.code.gameObject.SetActive(false);
            }
            Instance.gameObject.SetActive(true);
            Instance.Refresh();
        }

        internal static void Teardown()
        {
            if (Instance != null)
            {
                Destroy(Instance.gameObject);
            }
            Instance = null;
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        // ---------------------------------------------------------------- building

        private static bool Build()
        {
            var go = new GameObject(RootName, typeof(RectTransform));
            DontDestroyOnLoad(go);
            var menu = go.AddComponent<DevMenu>();
            if (!menu.FindTemplates())
            {
                Destroy(go);
                DevStore.Prompt("Dev menu: game UI not found (open your inventory once, then try again)");
                return false;
            }
            Instance = menu;
            menu.BuildCanvas();
            menu.BuildContent();
            return true;
        }

        private bool FindTemplates()
        {
            if (UIInventory.code == null || UITrading.code == null)
            {
                return false;
            }
            Transform inv = UIInventory.code.transform;
            tPanel = Find<RawImage>(inv, "LeftPanel/BG");
            tHeaderBar = Find<RawImage>(inv, "LeftPanel/BG/attributesPanel");
            tHeaderText = Find<Text>(inv, "LeftPanel/BG/attributesPanel/points");
            tRowText = Find<Text>(inv, "LeftPanel/BG/attributesPanel/attributes/marksmanship/Text");
            tTitle = Find<Text>(inv, "LeftPanel/BG/Player Details");
            Transform rename = inv.Find("LeftPanel/BG/PlayerName/ButtonChangeName");
            tButton = rename != null ? rename.gameObject : null;
            tBackdrop = Find<RawImage>(UITrading.code.transform, "GameObject/bg");
            bool ok = tPanel && tHeaderBar && tHeaderText && tRowText && tTitle && tButton && tBackdrop;
            if (!ok)
            {
                Plugin.Log.LogWarning("Dev menu: a template is missing (panel/header/row/title/button/backdrop)");
            }
            return ok;
        }

        private static T Find<T>(Transform root, string path) where T : Component
        {
            Transform t = root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        private void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            CanvasScaler game = UIInventory.code.GetComponentInParent<CanvasScaler>();
            if (game == null && UICheat.code != null)
            {
                game = UICheat.code.GetComponentInParent<CanvasScaler>();
            }
            if (game != null)
            {
                scaler.uiScaleMode = game.uiScaleMode;
                scaler.referenceResolution = game.referenceResolution;
                scaler.screenMatchMode = game.screenMatchMode;
                scaler.matchWidthOrHeight = game.matchWidthOrHeight;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            gameObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildContent()
        {
            RectTransform root = (RectTransform)transform;
            Stretch(CopyRawImage(tBackdrop, root, "Backdrop").rectTransform, 0f);

            Text title = CopyText(tTitle, root, "Title", "DEV MODE", 40f, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(600f, 60f));

            Button close = MakeButton(root, "Close", ActClose, 0);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-110f, -45f), new Vector2(140f, 36f));

            BuildTabs(root);
            generalPage = Page(root, "GeneralPage");
            var colRt = (RectTransform)generalPage.transform;
            BuildPlayerColumn(Column(colRt, PlayerColumnWidth));
            BuildTroopColumn(Column(colRt, TroopColumnWidth));
            factionsPage = Page(root, "FactionsPage");
            BuildFactionColumn(Column((RectTransform)factionsPage.transform, FactionColumnWidth));
            factionsPage.SetActive(false);
            BuildCompanionsPage(root);
        }

        private void BuildPlayerColumn(Transform col)
        {
            Header(col, "Player");
            StatRow(col, "Money", ActMoney);
            StatRow(col, "Reputation", ActReputation);
            StatRow(col, "Morale (all troops, 0-100)", ActMorale);
            Transform moraleRow = Row(col);
            MakeButton(moraleRow, "Morale 0", ActMoralePreset, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(moraleRow, "Morale 50", ActMoralePreset, 50).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(moraleRow, "Morale 100", ActMoralePreset, 100).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            StatRow(col, "XP", ActXp);
            Spacer(col, 16f);
            Header(col, "Tools");
            Transform storeRow = Row(col);
            Button store = MakeButton(storeRow, "Item Store (endless, one of each)", ActStore, 0);
            store.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Transform freeRow = Row(col);
            Button free = MakeButton(freeRow, "", ActStoreFree, 0);
            free.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            storeFreeLabel = free.GetComponentInChildren<Text>(true);
        }

        private void BuildFactionColumn(Transform col)
        {
            Header(col, "Factions  (relation -100..100, money)");
            Transform allRow = Row(col);
            MakeButton(allRow, "Make All Hostile", ActAllHostile, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(allRow, "Make All Neutral", ActAllNeutral, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(allRow, "Make All Allies", ActAllAllies, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (Global.code == null || Global.code.factions == null)
            {
                return;
            }
            foreach (Transform t in Global.code.factions.items)
            {
                Faction f = t != null ? t.GetComponent<Faction>() : null;
                if (f == null)
                {
                    continue;
                }
                if (f.name == "Player")
                {
                    factions.Insert(0, f);
                }
                else
                {
                    factions.Add(f);
                }
            }
            for (int i = 0; i < factions.Count; i++)
            {
                Transform row = Row(col);
                Text name = Label(row, "", 0f, 1f);
                factionNames.Add(name);
                if (factions[i].name == "Player")
                {
                    Label(row, "", 150f, 0f); // no relation with yourself
                    Label(row, "", 70f, 0f);
                }
                else
                {
                    values[Key(ActRelation, i)] = Label(row, "", 70f, 0f);
                    inputs[Key(ActRelation, i)] = MakeInput(row, 80f);
                    MakeButton(row, "Set", ActRelation, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;
                }
                values[Key(ActFactionMoney, i)] = Label(row, "", 150f, 0f);
                inputs[Key(ActFactionMoney, i)] = MakeInput(row, 120f);
                MakeButton(row, "Set", ActFactionMoney, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;
            }
        }

        private void BuildTroopColumn(Transform col)
        {
            Header(col, "Troops  (count, then Add)");
            if (RM.code != null)
            {
                foreach (KeyValuePair<int, Transform> kv in RM.code.AllSoldier)
                {
                    NPCGen gen = kv.Value != null ? kv.Value.GetComponent<NPCGen>() : null;
                    if (gen != null && !MyEvent.ISHeroOfSoldier(gen))
                    {
                        soldiers.Add(kv.Value);
                    }
                }
            }
            Transform content = ScrollArea(col);
            for (int i = 0; i < soldiers.Count; i++)
            {
                Transform row = Row(content);
                Label(row, soldiers[i].GetComponent<NPCGen>().Name, 0f, 1f);
                InputField count = MakeInput(row, 70f);
                count.text = "1";
                inputs[Key(ActAddTroops, i)] = count;
                MakeButton(row, "Add", ActAddTroops, i).gameObject.AddComponent<LayoutElement>().preferredWidth = 70f;
            }
        }

        private void StatRow(Transform col, string label, int action)
        {
            Transform row = Row(col);
            Label(row, label, 0f, 1f);
            values[Key(action, 0)] = Label(row, "", 110f, 0f);
            inputs[Key(action, 0)] = MakeInput(row, 100f);
            MakeButton(row, "Set", action, 0).gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
        }

        // ---------------------------------------------------------------- element factories (copy game styles)

        private Transform Column(RectTransform parent, float width)
        {
            RawImage bg = CopyRawImage(tPanel, parent, "Column");
            bg.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            var v = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return bg.transform;
        }

        private void Header(Transform parent, string text)
        {
            RawImage bar = CopyRawImage(tHeaderBar, parent, "Header");
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = HeaderHeight;
            Text t = CopyText(tHeaderText, bar.transform, "Text", text, TextSize - 2f, TextAnchor.MiddleLeft);
            Stretch(t.rectTransform, 6f);
        }

        private static void Spacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static Transform Row(Transform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = RowHeight;
            le.flexibleHeight = 0f; // else the row inherits the cloned button's flexible height and stretches
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return go.transform;
        }

        private Text Label(Transform row, string text, float width, float flexible)
        {
            Text t = CopyText(tRowText, row, "Label", text, TextSize, TextAnchor.MiddleLeft);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexible;
            return t;
        }

        private InputField MakeInput(Transform row, float width)
        {
            var go = new GameObject("Input", typeof(RectTransform));
            go.transform.SetParent(row, false);
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            Text text = CopyText(tRowText, go.transform, "Text", "", TextSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 4f);
            text.supportRichText = false;
            Text ph = CopyText(tRowText, go.transform, "Placeholder", "...", TextSize, TextAnchor.MiddleCenter);
            Stretch(ph.rectTransform, 4f);
            Color c = text.color;
            ph.color = new Color(c.r, c.g, c.b, 0.35f);
            var field = go.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = ph;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = 10;
            field.customCaretColor = true;
            field.caretColor = c;
            field.selectionColor = new Color(0.93f, 0.55f, 0.13f, 0.45f); // game's orange accent
            return field;
        }

        private Button MakeButton(Transform parent, string label, int action, int arg)
        {
            GameObject go = Instantiate(tButton, parent, false);
            go.name = "Button " + label;
            go.SetActive(true);
            Button b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent(); // drop RENAME's wiring
            Text t = go.GetComponentInChildren<Text>(true);
            if (t != null)
            {
                StripLocalization(t.gameObject);
                t.text = label;
                t.fontSize = (int)(TextSize - 2f);
                t.resizeTextForBestFit = false;
            }
            var mb = go.AddComponent<DevMenuButton>();
            mb.Action = action;
            mb.Arg = arg;
            b.onClick.AddListener(new UnityAction(mb.Click));
            return b;
        }

        private Transform ScrollArea(Transform col)
        {
            var view = new GameObject("Scroll", typeof(RectTransform));
            view.transform.SetParent(col, false);
            view.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var mask = view.AddComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.01f); // Mask needs a graphic; nearly invisible
            view.AddComponent<Mask>().showMaskGraphic = false;
            var scroll = view.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var content = new GameObject("Content", typeof(RectTransform));
            var crt = (RectTransform)content.transform;
            crt.SetParent(view.transform, false);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var v = content.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)view.transform;
            scroll.content = crt;
            return content.transform;
        }

        private static RawImage CopyRawImage(RawImage from, Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<RawImage>();
            img.texture = from.texture;
            img.color = from.color;
            img.uvRect = from.uvRect;
            img.material = from.material;
            return img;
        }

        private static Text CopyText(Text from, Transform parent, string name, string text, float size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = from.font;
            t.fontStyle = from.fontStyle;
            t.color = from.color;
            t.material = from.material;
            t.lineSpacing = from.lineSpacing;
            t.fontSize = (int)size;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            Shadow s = from.GetComponent<Shadow>();
            if (s != null)
            {
                Shadow copy = go.AddComponent<Shadow>();
                copy.effectColor = s.effectColor;
                copy.effectDistance = s.effectDistance;
            }
            return t;
        }

        private static void StripLocalization(GameObject go)
        {
            LocalizationText loc = go.GetComponent<LocalizationText>();
            if (loc != null)
            {
                DestroyImmediate(loc);
            }
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, 0f);
            rt.offsetMax = new Vector2(-inset, 0f);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static int Key(int action, int arg)
        {
            return action * 10000 + arg;
        }

        // ---------------------------------------------------------------- values + actions

        private void Refresh()
        {
            if (MapAgent.code == null)
            {
                return;
            }
            SetValue(ActMoney, 0, MapAgent.code.GetCoin().ToString("N0"));
            SetValue(ActReputation, 0, MapAgent.code.reputation.ToString());
            SetValue(ActMorale, 0, Mathf.RoundToInt(MapAgent.code._Army.GetAverageMorale()).ToString());
            SetValue(ActXp, 0, MapAgent.code.gen.curExp + " (Lv " + MapAgent.code.gen.Level + ")");
            for (int i = 0; i < factions.Count; i++)
            {
                Faction f = factions[i];
                if (f == null)
                {
                    continue;
                }
                factionNames[i].text = f.Name;
                factionNames[i].color = FactionColor(f);
                SetValue(ActRelation, i, f.relationship.ToString());
                SetValue(ActFactionMoney, i, "$ " + f.Money.ToString("N0"));
            }
            if (storeFreeLabel != null)
            {
                storeFreeLabel.text = DevMode.StoreFree.Value ? "Store prices: FREE" : "Store prices: NORMAL";
            }
            RefreshCompanions();
        }

        private Color FactionColor(Faction f)
        {
            if (f.name == "Player")
            {
                return tRowText.color;
            }
            switch (f.relationValue)
            {
                case RelationValue.Hostile:
                    return HostileColor;
                case RelationValue.Unfriendly:
                    return UnfriendlyColor;
                case RelationValue.Friendly:
                case RelationValue.Ally:
                    return FriendlyColor;
                default:
                    return tRowText.color;
            }
        }

        private void SetValue(int action, int arg, string text)
        {
            Text t;
            if (values.TryGetValue(Key(action, arg), out t))
            {
                t.text = text;
            }
        }

        internal void Do(int action, int arg)
        {
            if (DoCompanions(action, arg))
            {
                return;
            }
            switch (action)
            {
                case ActClose:
                    Close();
                    return;
                case ActStore:
                    Close();
                    DevStore.Open();
                    return;
                case ActAllNeutral:
                    DevActions.AllNeutral(factions);
                    Refresh();
                    return;
                case ActMoralePreset:
                    DevActions.SetMorale(arg);
                    Refresh();
                    return;
                case ActAllHostile:
                    DevActions.AllHostile(factions);
                    Refresh();
                    return;
                case ActAllAllies:
                    DevActions.AllAllies(factions);
                    Refresh();
                    return;
                case ActStoreFree:
                    DevMode.StoreFree.Value = !DevMode.StoreFree.Value;
                    Refresh();
                    return;
            }
            InputField field;
            int value;
            if (!inputs.TryGetValue(Key(action, arg), out field) || !int.TryParse(field.text, out value))
            {
                DevStore.Prompt("Type a number first");
                return;
            }
            switch (action)
            {
                case ActMoney:
                    DevActions.SetMoney(value);
                    break;
                case ActReputation:
                    DevActions.SetReputation(value);
                    break;
                case ActMorale:
                    DevActions.SetMorale(value);
                    break;
                case ActXp:
                    DevActions.SetXp(value);
                    break;
                case ActRelation:
                    DevActions.SetRelation(factions[arg], value);
                    break;
                case ActFactionMoney:
                    DevActions.SetFactionMoney(factions[arg], value);
                    break;
                case ActAddTroops:
                    DevActions.AddTroops(soldiers[arg], value);
                    break;
            }
            if (action != ActAddTroops)
            {
                field.text = string.Empty;
            }
            Refresh();
        }
    }

    internal class DevMenuButton : MonoBehaviour
    {
        public int Action;
        public int Arg;

        public void Click()
        {
            if (DevMenu.Instance != null)
            {
                DevMenu.Instance.Do(Action, Arg);
            }
        }
    }
}
