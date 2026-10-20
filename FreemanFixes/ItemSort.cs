using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Sort by any item stat and filter by category, on both sides of the item exchange screen (UITrading): traders
    // and the dev store (trade mode, Merchant.Trade) and city storage, armory and loot (Storage.OpenStorage). Vanilla
    // only sorts by price (UITrading.SortPriceUp/Down, the ▲▼ buttons in the top bar), which Sort: Price replaces, so
    // those two are hidden while the screen is open. The top bar is full (Auto Sort toggle, discount text, speed), so
    // each side's controls sit in a row across the top of its own grid frame and the grid's Scroll View is inset by
    // the rows' height. Row 1 is ours: Category and direction buttons cloned from the Deal button, and the sort key as a
    // dropdown cloned from the settings screen's (UISettings.shadowQualityDropdown, or a companion row's AI dropdown,
    // HeroShow.dropdown, before the settings screen exists). A second row holds
    // the dev store's controls (DevStore.BuildControls) while it is the merchant, on both sides so the grids line up.
    // Sorting reorders Storage.items, as the game's own price sort does (the trade's tempItems are item ids, so a
    // pending trade is unaffected), keeping the bag's first Global.SpecialSoltNum special slots in place; it is
    // re-applied after every slot click (InventorySlot.Select), since the game compacts both sides as items move. The
    // category filter only hides slots in Inventory.Refresh; nothing is moved or removed. The dev store pages its
    // stock itself, so its side is sorted through DevStore.ApplySort (each whole category, not one page) and keeps
    // its own category pages instead of the filter. The dropdown only offers the stats some item it shows has (the
    // side's items in its category, or the dev store page's whole category; Default, Price and Name always), rebuilt
    // as items move; a sort that no longer applies falls back to Default. Settings last for the session, per side.
    internal static class ItemSort
    {
        internal const int Bag = 0;
        internal const int Other = 1;

        private const float RowGap = 4f;
        private const float CategoryFlex = 3f;
        private const float KeyFlex = 4f;
        private const float DirectionFlex = 2f;
        private const string RowName = "FreemanSlop_SortControls_";
        private const string ExtraRowName = "FreemanSlop_StoreControls_";

        internal static readonly int[] Key = { ItemStats.Default, ItemStats.Default };
        internal static readonly bool[] HighFirst = { true, true };
        internal static readonly int[] Filter = { ItemCategories.All, ItemCategories.All };

        private static readonly GameObject[] rows = new GameObject[2];
        private static readonly GameObject[] extraRows = new GameObject[2];
        private static readonly RectTransform[] scrolls = new RectTransform[2];
        private static readonly Vector2[] scrollOffsetMax = new Vector2[2]; // vanilla top edge, for the inset
        private static readonly Text[] categoryLabels = new Text[2];
        private static readonly GameObject[] categoryButtons = new GameObject[2]; // GetComponentInParent skips inactive
        private static readonly Dropdown[] keyDropdowns = new Dropdown[2];
        private static readonly List<int>[] keyOptions = { new List<int>(), new List<int>() }; // dropdown index -> key
        private static bool refreshing;
        private static bool syncing; // Unity 2018 has no Dropdown.SetValueWithoutNotify
        private static readonly Text[] directionLabels = new Text[2];
        private static readonly List<GameObject> hiddenPriceButtons = new List<GameObject>();
        private static float rowHeight;
        private static bool dumped;

        internal static bool IsOpen()
        {
            return UITrading.code != null && UITrading.code.gameObject.activeInHierarchy;
        }

        internal static Inventory Side(int side)
        {
            return side == Bag ? UITrading.code.playerInventory : UITrading.code.merchantInventory;
        }

        private static bool IsDevStoreSide(int side)
        {
            return side == Other && DevStore.IsMerchant(UITrading.code.merchantInventory.storage);
        }

        // The dev store's row on a side (built on first use).
        internal static Transform ExtraRow(UITrading trading, int side)
        {
            Ensure(trading);
            return extraRows[side] != null ? extraRows[side].transform : null;
        }

        // UITrading.OnEnable postfix (after DevMode's, which may already have built the rows for the dev store).
        internal static void Opened(UITrading trading)
        {
            Ensure(trading);
            bool devStore = DevStore.IsMerchant(trading.merchantInventory.storage);
            for (int side = 0; side < 2; side++)
            {
                if (rows[side] == null)
                {
                    continue;
                }
                extraRows[side].SetActive(devStore);
                Inset(side, devStore ? 2 : 1);
                UpdateLabels(side);
                Apply(side);
                RefreshOptions(side);
            }
            foreach (GameObject b in hiddenPriceButtons)
            {
                b.SetActive(false);
            }
        }

        private static void Ensure(UITrading trading)
        {
            if (trading.btnDeal == null)
            {
                return;
            }
            if (!dumped)
            {
                dumped = true;
                DepositPanelUi.DumpHierarchy(trading.transform, "Trade screen");
            }
            if (hiddenPriceButtons.Count == 0)
            {
                foreach (Button b in trading.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                    {
                        string m = b.onClick.GetPersistentMethodName(i);
                        if ((m == "SortPriceUp" || m == "SortPriceDown") && !hiddenPriceButtons.Contains(b.gameObject))
                        {
                            hiddenPriceButtons.Add(b.gameObject);
                        }
                    }
                }
            }
            rowHeight = ((RectTransform)trading.btnDeal.transform).rect.height;
            for (int side = 0; side < 2; side++)
            {
                if (rows[side] == null)
                {
                    Build(trading, side);
                }
            }
        }

        private static void Build(UITrading trading, int side)
        {
            Inventory inv = Side(side);
            ScrollRect scroll = inv != null ? inv.GetComponentInChildren<ScrollRect>(true) : null;
            if (scroll == null)
            {
                Plugin.Log.LogWarning("Item sort: no scroll area for side " + side + "; controls skipped");
                return;
            }
            scrolls[side] = (RectTransform)scroll.transform;
            scrollOffsetMax[side] = scrolls[side].offsetMax;
            Transform frame = scrolls[side].parent;
            rows[side] = Row(frame, RowName + side, 0);
            extraRows[side] = Row(frame, ExtraRowName + side, 1);
            Button deal = trading.btnDeal;
            Transform row = rows[side].transform;
            categoryLabels[side] = TradeButtons.Clone(deal, row, "", 0f, CategoryFlex);
            categoryButtons[side] = Handle(categoryLabels[side], side, SortButton.Category);
            keyDropdowns[side] = KeyDropdown(row, side);
            directionLabels[side] = TradeButtons.Clone(deal, row, "", 0f, DirectionFlex);
            Handle(directionLabels[side], side, SortButton.Direction);
        }

        private static Dropdown KeyDropdown(Transform row, int side)
        {
            Dropdown d = TradeButtons.CloneDropdown(row, "FreemanSlop_SortKey_" + side, KeyFlex);
            if (d == null)
            {
                return null;
            }
            SortDropdown handler = d.gameObject.AddComponent<SortDropdown>();
            handler.Side = side;
            d.onValueChanged.AddListener(new UnityAction<int>(handler.Changed));
            return d;
        }

        internal static void SetKey(int side, int key)
        {
            if (syncing)
            {
                return;
            }
            List<int> options = keyOptions[side];
            Key[side] = key >= 0 && key < options.Count ? options[key] : ItemStats.Default;
            if (Key[side] == ItemStats.Name)
            {
                HighFirst[side] = false; // A to Z
            }
            Press(side, 0);
        }

        // A full-width row across the top of the frame; index 0 is the first row, 1 the one under it.
        private static GameObject Row(Transform frame, string name, int index)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(frame, false); // appended last
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-2f * RowGap, rowHeight);
            rt.anchoredPosition = new Vector2(0f, -RowGap - index * (rowHeight + RowGap));
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return go;
        }

        private static GameObject Handle(Text label, int side, int kind)
        {
            GameObject button = label.GetComponentInParent<Button>().gameObject; // just built, so still active
            SortButton b = button.AddComponent<SortButton>();
            b.Side = side;
            b.Kind = kind;
            button.GetComponent<Button>().onClick.AddListener(new UnityAction(b.Click));
            return button;
        }

        // Move the grid's top edge down below the rows.
        private static void Inset(int side, int rowCount)
        {
            if (scrolls[side] != null)
            {
                float inset = RowGap + rowCount * (rowHeight + RowGap);
                scrolls[side].offsetMax = scrollOffsetMax[side] - new Vector2(0f, inset);
            }
        }

        internal static void Press(int side, int kind)
        {
            switch (kind)
            {
                case SortButton.Category:
                    Filter[side] = NextCategory(side);
                    break;
                case SortButton.Direction:
                    HighFirst[side] = !HighFirst[side];
                    break;
            }
            UpdateLabels(side);
            Apply(side);
            Inventory inv = Side(side);
            if (inv != null)
            {
                inv.Refresh(); // also applies the filter
            }
            RefreshOptions(side);
        }

        private static void UpdateLabels(int side)
        {
            if (rows[side] == null)
            {
                return;
            }
            categoryLabels[side].text = ItemCategories.Names[Filter[side]];
            if (Key[side] == ItemStats.Name)
            {
                directionLabels[side].text = HighFirst[side] ? "Z to A" : "A to Z";
            }
            else
            {
                directionLabels[side].text = HighFirst[side] ? "High first" : "Low first";
            }
        }

        // Sort one side by its key (Default leaves the game's order).
        internal static void Apply(int side)
        {
            if (!IsOpen())
            {
                return;
            }
            Inventory inv = Side(side);
            if (inv == null || inv.storage == null)
            {
                return;
            }
            if (IsDevStoreSide(side))
            {
                DevStore.ApplySort(Key[side], HighFirst[side]);
                return;
            }
            if (Key[side] == ItemStats.Default)
            {
                return;
            }
            List<int> items = inv.storage.items;
            int start = inv.storage.GetComponent<MapAgent>() != null ? Mathf.Min(Global.SpecialSoltNum, items.Count) : 0;
            var ids = new List<int>();
            for (int i = start; i < items.Count; i++)
            {
                if (items[i] != 0)
                {
                    ids.Add(items[i]);
                }
            }
            List<int> sorted = ItemStats.Sorted(ids, Key[side], HighFirst[side], inv.storage.GetComponent<Merchant>());
            for (int i = start; i < items.Count; i++)
            {
                int k = i - start;
                items[i] = k < sorted.Count ? sorted[k] : 0;
            }
        }

        // InventorySlot.Select postfix: the click moved an item and the game compacted both sides.
        internal static void AfterClick()
        {
            if (!IsOpen())
            {
                return;
            }
            for (int side = 0; side < 2; side++)
            {
                if (Key[side] != ItemStats.Default && !IsDevStoreSide(side))
                {
                    Apply(side);
                    Inventory inv = Side(side);
                    if (inv != null)
                    {
                        inv.Refresh();
                    }
                }
                RefreshOptions(side);
            }
        }

        // The items a side's dropdown offers stats for: what it shows (in its category), or the dev store's category.
        private static List<int> Shown(int side)
        {
            if (IsDevStoreSide(side))
            {
                return DevStore.CurrentCategoryItems();
            }
            var ids = new List<int>();
            Inventory inv = Side(side);
            if (inv == null || inv.storage == null)
            {
                return ids;
            }
            foreach (int id in inv.storage.items)
            {
                if (id != 0 && (Filter[side] == ItemCategories.All || ItemCategories.Of(id) == Filter[side]))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        // All, then each category this side has, then back to All.
        private static int NextCategory(int side)
        {
            Inventory inv = Side(side);
            List<int> present = inv != null && inv.storage != null
                ? ItemCategories.Present(inv.storage.items) : new List<int>();
            foreach (int c in present)
            {
                if (c > Filter[side])
                {
                    return c;
                }
            }
            return ItemCategories.All;
        }

        // The Category button only shows when the side has two or more categories; a filter whose items are all gone
        // goes back to All.
        private static void RefreshCategories(int side)
        {
            if (categoryButtons[side] == null)
            {
                return;
            }
            Inventory inv = Side(side);
            List<int> present = inv != null && inv.storage != null
                ? ItemCategories.Present(inv.storage.items) : new List<int>();
            if (Filter[side] != ItemCategories.All && !present.Contains(Filter[side]))
            {
                Filter[side] = ItemCategories.All;
                UpdateLabels(side);
                if (inv != null)
                {
                    inv.Refresh();
                }
            }
            categoryButtons[side].SetActive(!IsDevStoreSide(side) && (present.Count >= 2 || Filter[side] != ItemCategories.All));
        }

        // Rebuild the side's dropdown from the stats its items have; fall back to Default if the sort no longer applies.
        internal static void RefreshOptions(int side)
        {
            if (refreshing || !IsOpen())
            {
                return;
            }
            RefreshCategories(side);
            Dropdown d = keyDropdowns[side];
            if (d == null)
            {
                return;
            }
            refreshing = true;
            List<int> ids = Shown(side);
            var keys = new List<int>();
            for (int k = 0; k < ItemStats.Names.Length; k++)
            {
                if (k <= ItemStats.Name || ItemStats.AnyHas(ids, k))
                {
                    keys.Add(k);
                }
            }
            if (!keys.Contains(Key[side]))
            {
                Key[side] = ItemStats.Default;
                Apply(side); // the dev store re-pages; elsewhere Default keeps the order as it is
                UpdateLabels(side);
            }
            syncing = true;
            if (!SameKeys(keys, keyOptions[side]))
            {
                keyOptions[side].Clear();
                keyOptions[side].AddRange(keys);
                var names = new List<string>();
                foreach (int k in keys)
                {
                    names.Add("Sort: " + ItemStats.Names[k]);
                }
                d.ClearOptions();
                d.AddOptions(names);
            }
            int index = keys.IndexOf(Key[side]);
            if (d.value != index)
            {
                d.value = index;
            }
            d.RefreshShownValue();
            syncing = false;
            refreshing = false;
        }

        private static bool SameKeys(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        // Inventory.Refresh postfix: hide slots outside the side's category (never the bag's special slots).
        internal static void Filtered(Inventory inv)
        {
            if (UITrading.code == null)
            {
                return;
            }
            int side = inv == UITrading.code.playerInventory ? Bag : inv == UITrading.code.merchantInventory ? Other : -1;
            if (side < 0 || Filter[side] == ItemCategories.All || inv.storage == null || IsDevStoreSide(side))
            {
                return;
            }
            int keep = inv.isPlayerInventory ? Global.SpecialSoltNum : 0;
            List<int> items = inv.storage.items;
            for (int i = keep; i < inv.slots.Count && i < inv.storage.maxSlots; i++)
            {
                Transform slot = inv.slots[i];
                if (slot == null)
                {
                    continue;
                }
                int id = i < items.Count ? items[i] : 0;
                slot.gameObject.SetActive(id != 0 && ItemCategories.Of(id) == Filter[side]);
            }
        }

        // Hot reload: put the grids and the price buttons back, remove the rows and show every slot again.
        internal static void Teardown()
        {
            for (int side = 0; side < 2; side++)
            {
                if (scrolls[side] != null)
                {
                    scrolls[side].offsetMax = scrollOffsetMax[side];
                }
                scrolls[side] = null;
                if (rows[side] != null)
                {
                    Object.Destroy(rows[side]);
                }
                if (extraRows[side] != null)
                {
                    Object.Destroy(extraRows[side]);
                }
                rows[side] = null;
                extraRows[side] = null;
                keyDropdowns[side] = null;
                keyOptions[side].Clear();
                Filter[side] = ItemCategories.All;
            }
            foreach (GameObject b in hiddenPriceButtons)
            {
                if (b != null)
                {
                    b.SetActive(true);
                }
            }
            hiddenPriceButtons.Clear();
            if (IsOpen())
            {
                UITrading.code.playerInventory.Refresh();
                UITrading.code.merchantInventory.Refresh();
            }
        }
    }

    // Item categories, shared with the dev store's pages. Most items are the generic ItemType.ITEM, so the split
    // follows what the game uses them for: Supplies are the construction materials it counts (Global.ItemWood /
    // ItemIron / ItemTool, UICombat's construction costs), Useful are misc-slot gear (first aid kit, night vision,
    // binoculars), weapon attachments (sights, silencers) and health items, and everything else of no use goes to
    // General goods.
    internal static class ItemCategories
    {
        internal const int All = 0;
        internal const int Weapons = 1;
        internal const int Ammo = 2;
        internal const int Clothing = 3;
        internal const int Food = 4;
        internal const int Supplies = 5;
        internal const int Useful = 6;
        internal const int GeneralGoods = 7;

        internal static readonly string[] Names =
        {
            "All", "Weapons", "Ammo", "Clothing", "Food", "Supplies", "Useful", "General goods"
        };

        internal static int Of(int id)
        {
            if (RM.code.AllWeaponsItem.ContainsKey(id))
            {
                return Weapons;
            }
            if (RM.code.AllClothesItem.ContainsKey(id))
            {
                return Clothing;
            }
            Transform t;
            Item item = RM.code.AllItem.TryGetValue(id, out t) && t != null ? t.GetComponent<Item>() : null;
            if (item == null)
            {
                return GeneralGoods;
            }
            if (item.ItemType == ItemType.AMMO)
            {
                return Ammo;
            }
            if (item.ItemType == ItemType.FOOD)
            {
                return Food;
            }
            if (id == Global.ItemWood || id == Global.ItemIron || id == Global.ItemTool)
            {
                return Supplies;
            }
            if (item.ItemType == ItemType.HEALTH || item.ItemType == ItemType.ATTACHMENT
                || (item.SlotType >= SlotType.MISC && item.SlotType <= SlotType.MISC12))
            {
                return Useful;
            }
            return GeneralGoods;
        }

        // The categories present among ids, in Names order (All not included).
        internal static List<int> Present(List<int> ids)
        {
            var seen = new bool[Names.Length];
            foreach (int id in ids)
            {
                if (id != 0)
                {
                    seen[Of(id)] = true;
                }
            }
            var result = new List<int>();
            for (int c = 1; c < Names.Length; c++)
            {
                if (seen[c])
                {
                    result.Add(c);
                }
            }
            return result;
        }
    }

    // The stats the item info popup shows (UICombat.ShowInfo): WeaponRaycast for weapons, Appeal for clothing. The
    // popup's "Armor Proficiency Required" is Appeal.armor again (Appeal.requriedDefencePoints is never filled), so
    // Armor req. reads the same number.
    internal static class ItemStats
    {
        internal const int Default = 0;
        internal const int Price = 1;
        internal const int Name = 2;
        internal const int Damage = 3;
        internal const int Velocity = 4;
        internal const int FireRate = 5;
        internal const int Magazine = 6;
        internal const int Marksmanship = 7;
        internal const int Armor = 8;
        internal const int Camouflage = 9;
        internal const int ArmorRequirement = 10;
        internal const int FoodAmount = 11;

        internal static readonly string[] Names =
        {
            "Default", "Price", "Name", "Damage", "Velocity", "Fire rate", "Magazine", "Marksmanship req.", "Armor",
            "Camouflage", "Armor req.", "Food amount"
        };

        // The item's value for a key; false when it doesn't have that stat (those sort last).
        internal static bool TryGet(int id, int key, Merchant merchant, out float value)
        {
            value = 0f;
            Transform t;
            Item item = RM.code.AllItem.TryGetValue(id, out t) && t != null ? t.GetComponent<Item>() : null;
            if (item == null)
            {
                return false;
            }
            switch (key)
            {
                case Price:
                    value = item.GetBuyCost(merchant);
                    return true;
                case FoodAmount: // the popup's "Food Amount" line
                    value = item.amount;
                    return item.ItemType == ItemType.FOOD && item.amount > 0;
            }
            Transform w;
            WeaponRaycast gun = RM.code.AllWeaponsItem.TryGetValue(id, out w) && w != null ? w.GetComponent<WeaponRaycast>() : null;
            if (gun != null)
            {
                bool melee = item.SlotType == SlotType.MELEE;
                switch (key)
                {
                    case Damage: value = melee ? gun.Damage : gun.damage_Ori; return true;
                    case Velocity: value = gun.BulletSpeed; return !melee;
                    case FireRate: value = -gun.Interval; return !melee && gun.Interval > 0f; // shorter interval = faster
                    case Magazine: value = gun.MagSize; return !melee;
                    case Marksmanship: value = gun.MarksmanshipRequirement; return true;
                }
                return false;
            }
            Appeal clothes = item.GetComponent<Appeal>();
            if (clothes != null)
            {
                switch (key)
                {
                    case Armor: value = clothes.armor; return true;
                    case Camouflage: value = clothes.camouflage; return true;
                    case ArmorRequirement: value = clothes.armor; return clothes.armor != 0; // as UICombat.ShowInfo
                }
            }
            return false;
        }

        internal static bool AnyHas(List<int> ids, int key)
        {
            float v;
            foreach (int id in ids)
            {
                if (id != 0 && TryGet(id, key, null, out v))
                {
                    return true;
                }
            }
            return false;
        }

        // A sorted copy of ids; ties (and items without the stat, at the end) keep their order.
        internal static List<int> Sorted(List<int> ids, int key, bool highFirst, Merchant merchant)
        {
            var order = new ItemOrder();
            order.has = new bool[ids.Count];
            order.values = new float[ids.Count];
            order.names = key == Name ? new string[ids.Count] : null;
            order.highFirst = highFirst;
            var positions = new List<int>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                positions.Add(i);
                if (key == Name)
                {
                    Transform t;
                    Item item = RM.code.AllItem.TryGetValue(ids[i], out t) && t != null ? t.GetComponent<Item>() : null;
                    order.names[i] = item != null ? item.Name : null;
                    order.has[i] = !string.IsNullOrEmpty(order.names[i]);
                }
                else
                {
                    order.has[i] = TryGet(ids[i], key, merchant, out order.values[i]);
                }
            }
            positions.Sort(order);
            var result = new List<int>(ids.Count);
            foreach (int p in positions)
            {
                result.Add(ids[p]);
            }
            return result;
        }
    }

    // Compares positions in the list being sorted; the position breaks ties, which makes List.Sort stable.
    internal sealed class ItemOrder : IComparer<int>
    {
        internal bool[] has;
        internal float[] values;
        internal string[] names;
        internal bool highFirst;

        public int Compare(int a, int b)
        {
            if (has[a] != has[b])
            {
                return has[a] ? -1 : 1;
            }
            if (has[a])
            {
                int c = names != null
                    ? string.Compare(names[a], names[b], System.StringComparison.CurrentCultureIgnoreCase)
                    : values[a].CompareTo(values[b]);
                if (c != 0)
                {
                    return highFirst ? -c : c;
                }
            }
            return a.CompareTo(b);
        }
    }

    internal class SortButton : MonoBehaviour
    {
        internal const int Category = 1;
        internal const int Direction = 3;

        public int Side;
        public int Kind;

        public void Click()
        {
            ItemSort.Press(Side, Kind);
        }
    }

    internal class SortDropdown : MonoBehaviour
    {
        public int Side;

        public void Changed(int value)
        {
            ItemSort.SetKey(Side, value);
        }
    }

    // Clones of the trade screen's Deal button with a label of our own (shared with the dev store's controls).
    internal static class TradeButtons
    {
        internal static Text Clone(Button template, Transform parent, string label, float width, float flexible)
        {
            GameObject go = Object.Instantiate(template.gameObject, parent, false);
            go.SetActive(true);
            Button b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.interactable = true; // Deal is disabled while no trade is pending, and clones copy that state
            Text t = go.GetComponentInChildren<Text>(true);
            foreach (Component c in t.GetComponents<Component>())
            {
                // LocalizationText would overwrite our label with "Deal".
                if (c != null && c.GetType().Name == "LocalizationText")
                {
                    Object.DestroyImmediate(c);
                }
            }
            t.text = label;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = flexible;
            return t;
        }

        private static bool dropdownDumped;

        // A dropdown cloned from the settings screen's (UISettings.shadowQualityDropdown), or a companion row's AI
        // dropdown (HeroShow.dropdown) before the settings screen exists, with no options and no listeners.
        internal static Dropdown CloneDropdown(Transform parent, string name, float flexible)
        {
            Dropdown template = null;
            if (UISettings.code != null)
            {
                template = UISettings.code.shadowQualityDropdown;
            }
            if (template == null && HeroInfoShow.code != null && HeroInfoShow.code.HeroObj != null)
            {
                HeroShow show = HeroInfoShow.code.HeroObj.GetComponent<HeroShow>();
                template = show != null ? show.dropdown : null;
            }
            if (template == null)
            {
                Plugin.Log.LogWarning("Trade screen: no dropdown to clone; " + name + " skipped");
                return null;
            }
            GameObject go = Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.SetActive(true);
            go.transform.localScale = Vector3.one;
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                // LocalizationText would put the setting's own words back into the caption and items.
                if (c != null && c.GetType().Name == "LocalizationText")
                {
                    Object.DestroyImmediate(c);
                }
            }
            Dropdown d = go.GetComponent<Dropdown>();
            d.onValueChanged = new Dropdown.DropdownEvent();
            d.interactable = true;
            d.ClearOptions();
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) // not ??: Unity objects compare to null through their own operator
            {
                le = go.AddComponent<LayoutElement>();
            }
            le.preferredWidth = 0f;
            le.flexibleWidth = flexible;
            le.minWidth = -1f;
            if (!dropdownDumped)
            {
                dropdownDumped = true;
                DepositPanelUi.DumpHierarchy(go.transform, "Trade screen dropdown");
            }
            return d;
        }
    }

    [HarmonyPatch]
    internal static class ItemSortPatches
    {
        // After DevMode's OnEnable postfix (the dev store's controls and stock).
        [HarmonyPostfix, HarmonyPriority(Priority.Low), HarmonyPatch(typeof(UITrading), nameof(UITrading.OnEnable))]
        private static void TradeOpened(UITrading __instance)
        {
            ItemSort.Opened(__instance);
        }

        // After the dev store restocks its page.
        [HarmonyPostfix, HarmonyPriority(Priority.Low), HarmonyPatch(typeof(InventorySlot), nameof(InventorySlot.Select))]
        private static void SlotClicked()
        {
            ItemSort.AfterClick();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Inventory), nameof(Inventory.Refresh))]
        private static void Refreshed(Inventory __instance)
        {
            ItemSort.Filtered(__instance);
        }
    }
}
