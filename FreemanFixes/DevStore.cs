using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Dev item store: a standalone trader (Storage + Merchant, opened via the game's own Merchant.Trade) with one of
    // every item that never runs out: after every click in the trade screen the page is restocked (taking an item
    // from a trader removes it and compacts the list, InventorySlot.Select), keeping anything you sold to it so
    // Revert still works. The merchant grid is a fixed 250 slots (Inventory.Initiate), so stock is split into category
    // pages of up to 200 items, leaving room to sell. Page and Free controls are clones of the screen's Deal button in
    // the free right half of its top bar. Free zeroes Item.GetBuyCost.
    internal class DevStore : MonoBehaviour
    {
        private const int ItemsPerPage = 200; // of the 250 merchant slots; the rest takes items you sell
        private const int Slots = 250;
        private const int StoreCredits = 99999999;
        private const string RowName = "FreemanSlop_StoreControls";

        internal static DevStore Instance;

        private Storage storage;
        private Merchant merchant;
        private readonly List<string> pageNames = new List<string>();
        private readonly List<List<int>> pages = new List<List<int>>();
        private int page;

        private static GameObject controlsRow;
        private static Text pageLabel;
        private static Text freeLabel;

        internal static void Open()
        {
            if (TerrainControl.code == null || !TerrainControl.code.isWorld || MapAgent.code == null)
            {
                Prompt("Open the store from the world map");
                return;
            }
            Ensure();
            if (Instance.pages.Count == 0)
            {
                Prompt("No items found");
                return;
            }
            Instance.Fill();
            Instance.merchant.Trade();
        }

        private static void Ensure()
        {
            if (Instance != null)
            {
                return;
            }
            var go = new GameObject("FreemanSlop_DevStore");
            DontDestroyOnLoad(go);
            Storage s = go.AddComponent<Storage>();
            s.items = new List<int>();
            s.tempItems = new List<int>();
            s.helmets = new List<int>();
            s.armors = new List<int>();
            s.masks = new List<int>();
            s.rifles = new List<int>();
            s.pistols = new List<int>();
            s.shirts = new List<int>();
            s.pants = new List<int>();
            s.maxSlots = Slots;
            s.credits = StoreCredits;
            Instance = go.AddComponent<DevStore>();
            Instance.storage = s;
            Instance.merchant = go.AddComponent<Merchant>();
            Instance.BuildPages();
        }

        internal static void Prompt(string message)
        {
            if (UICombat.code != null)
            {
                UICombat.code.AddPrompt(message);
            }
        }

        internal static void Teardown()
        {
            if (Instance != null && IsOpen())
            {
                UITrading.code.Quit();
            }
            if (controlsRow != null)
            {
                Destroy(controlsRow);
            }
            controlsRow = null;
            if (Instance != null)
            {
                Destroy(Instance.gameObject);
            }
            Instance = null;
        }

        private static bool IsOpen()
        {
            return Instance != null && UITrading.code != null && UITrading.code.gameObject.activeInHierarchy
                && UITrading.code.isTrading && UITrading.code.merchantInventory.storage == Instance.storage;
        }

        internal static bool IsFreeNow()
        {
            return DevMode.StoreFree != null && DevMode.StoreFree.Value && IsOpen();
        }

        internal static void RestockIfOpen()
        {
            if (IsOpen())
            {
                Instance.Fill();
            }
        }

        // Called from UITrading.OnEnable: show our controls only while the dev store is the merchant.
        internal static void SyncControls(UITrading trading)
        {
            bool ours = Instance != null && trading.isTrading && trading.merchantInventory.storage == Instance.storage;
            if (ours && controlsRow == null)
            {
                BuildControls(trading);
            }
            if (controlsRow != null)
            {
                controlsRow.SetActive(ours);
            }
            if (ours)
            {
                Instance.UpdateLabels();
            }
        }

        private void BuildPages()
        {
            var weapons = new List<int>();
            var clothes = new List<int>();
            var ammo = new List<int>();
            var medical = new List<int>();
            var other = new List<int>();
            foreach (KeyValuePair<int, Transform> kv in RM.code.AllItem)
            {
                Item item = kv.Value != null ? kv.Value.GetComponent<Item>() : null;
                if (item == null || kv.Key == 0)
                {
                    continue;
                }
                if (RM.code.AllWeaponsItem.ContainsKey(kv.Key))
                {
                    weapons.Add(kv.Key);
                }
                else if (RM.code.AllClothesItem.ContainsKey(kv.Key))
                {
                    clothes.Add(kv.Key);
                }
                else if (item.ItemType == ItemType.AMMO)
                {
                    ammo.Add(kv.Key);
                }
                else if (item.ItemType == ItemType.FOOD || item.ItemType == ItemType.HEALTH)
                {
                    medical.Add(kv.Key);
                }
                else
                {
                    other.Add(kv.Key);
                }
            }
            AddCategory("Weapons", weapons);
            AddCategory("Clothing", clothes);
            AddCategory("Ammo", ammo);
            AddCategory("Food & Medical", medical);
            AddCategory("Other", other);
            Plugin.Log.LogInfo("Dev store: " + pages.Count + " pages from " + RM.code.AllItem.Count + " items");
        }

        private void AddCategory(string name, List<int> ids)
        {
            ids.Sort();
            int pageCount = (ids.Count + ItemsPerPage - 1) / ItemsPerPage;
            for (int p = 0; p < pageCount; p++)
            {
                int start = p * ItemsPerPage;
                pages.Add(ids.GetRange(start, Mathf.Min(ItemsPerPage, ids.Count - start)));
                pageNames.Add(pageCount > 1 ? name + " " + (p + 1) + "/" + pageCount : name);
            }
        }

        // Full reset to one of each page item (opening, page change, after a deal).
        private void Fill()
        {
            storage.items = PageStock(null);
            storage.maxSlots = Slots;
            storage.tempItems.Clear();
            storage.credits = StoreCredits;
            if (storage.inventory != null)
            {
                storage.inventory.Refresh();
            }
            UpdateLabels();
        }

        // After a click: one of each page item again, in order, followed by whatever else is in the store (items you
        // sold, still pending in the trade). Leaves the game's temp-item bookkeeping alone so Revert/Deal still work.
        private void Restock()
        {
            storage.items = PageStock(storage.items);
            if (storage.inventory != null)
            {
                storage.inventory.Refresh();
            }
        }

        private List<int> PageStock(List<int> current)
        {
            List<int> pageIds = pages[page];
            var stock = new List<int>(Slots);
            stock.AddRange(pageIds);
            if (current != null)
            {
                // Keep extras: anything beyond one copy of each page item.
                var counts = new Dictionary<int, int>();
                foreach (int id in pageIds)
                {
                    counts[id] = 1;
                }
                foreach (int id in current)
                {
                    if (id == 0)
                    {
                        continue;
                    }
                    int left;
                    if (counts.TryGetValue(id, out left) && left > 0)
                    {
                        counts[id] = left - 1; // this copy is the one already in the page stock
                    }
                    else if (stock.Count < Slots)
                    {
                        stock.Add(id);
                    }
                }
            }
            while (stock.Count < Slots)
            {
                stock.Add(0);
            }
            return stock;
        }

        internal static void RestockAfterClick()
        {
            if (IsOpen())
            {
                Instance.Restock();
            }
        }

        internal void Turn(int delta)
        {
            if (pages.Count == 0)
            {
                return;
            }
            GoTo((page + delta + pages.Count) % pages.Count);
        }

        // Label click: jump to the first page of the next category.
        internal void NextCategory()
        {
            string current = Category(pageNames[page]);
            for (int i = 1; i <= pages.Count; i++)
            {
                int candidate = (page + i) % pages.Count;
                if (Category(pageNames[candidate]) != current)
                {
                    GoTo(candidate);
                    return;
                }
            }
        }

        private static string Category(string pageName)
        {
            int cut = pageName.LastIndexOf(' ');
            return cut > 0 && char.IsDigit(pageName[cut + 1]) ? pageName.Substring(0, cut) : pageName;
        }

        private void GoTo(int newPage)
        {
            if (IsOpen())
            {
                UITrading.code.Revert(); // hand back anything mid-trade before swapping stock
            }
            page = newPage;
            Fill();
        }

        internal void ToggleFree()
        {
            DevMode.StoreFree.Value = !DevMode.StoreFree.Value;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            if (pageLabel != null && page < pageNames.Count)
            {
                pageLabel.text = pageNames[page];
            }
            if (freeLabel != null)
            {
                freeLabel.text = DevMode.StoreFree.Value ? "Free: ON" : "Free: OFF";
            }
        }

        private static void BuildControls(UITrading trading)
        {
            Button deal = null;
            foreach (Button b in trading.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                {
                    if (b.onClick.GetPersistentMethodName(i) == "Deal")
                    {
                        deal = b;
                    }
                }
            }
            if (deal == null || trading.isAutoSort == null)
            {
                Plugin.Log.LogWarning("Dev store: Deal button or top bar not found; store controls skipped");
                return;
            }
            // Top bar (holds the auto-sort toggle and sort buttons on its left half; right half is empty while trading).
            Transform bar = trading.isAutoSort.transform.parent;
            controlsRow = new GameObject(RowName, typeof(RectTransform));
            var rt = (RectTransform)controlsRow.transform;
            rt.SetParent(bar, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(320f, -4f);
            var dealRt = (RectTransform)deal.transform;
            rt.sizeDelta = new Vector2(600f, dealRt.rect.height);
            var row = controlsRow.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 6f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            AddControl(deal, "<", StoreButton.Prev, 50f);
            pageLabel = AddControl(deal, "", StoreButton.Category, 250f);
            AddControl(deal, ">", StoreButton.Next, 50f);
            freeLabel = AddControl(deal, "", StoreButton.Free, 150f);
        }

        private static Text AddControl(Button template, string label, int kind, float width)
        {
            GameObject go = Instantiate(template.gameObject, controlsRow.transform, false);
            go.name = "FreemanSlop_Store_" + kind;
            go.SetActive(true);
            Button b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            Text t = go.GetComponentInChildren<Text>(true);
            foreach (Component c in t.GetComponents<Component>())
            {
                // LocalizationText would overwrite our label with "Deal".
                if (c != null && c.GetType().Name == "LocalizationText")
                {
                    DestroyImmediate(c);
                }
            }
            t.text = label;
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var sb = go.AddComponent<StoreButton>();
            sb.Kind = kind;
            b.onClick.AddListener(new UnityAction(sb.Click));
            return t;
        }
    }

    internal class StoreButton : MonoBehaviour
    {
        internal const int Prev = 1;
        internal const int Next = 2;
        internal const int Category = 3;
        internal const int Free = 4;

        public int Kind;

        public void Click()
        {
            DevStore store = DevStore.Instance;
            if (store == null)
            {
                return;
            }
            switch (Kind)
            {
                case Prev:
                    store.Turn(-1);
                    break;
                case Next:
                    store.Turn(1);
                    break;
                case Category:
                    store.NextCategory();
                    break;
                case Free:
                    store.ToggleFree();
                    break;
            }
        }
    }
}
