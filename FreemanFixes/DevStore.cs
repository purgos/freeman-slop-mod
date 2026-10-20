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
    // pages of up to 200 items, leaving room to sell. Page and Free controls (clones of the screen's Deal button) sit in
    // the second row ItemSort puts over the store's grid, and Clear Bag in the one over your bag. Free zeroes
    // Item.GetBuyCost. The trade screen's sort (ItemSort) re-pages
    // each whole category in the chosen order (ApplySort), so page 1 holds the best of the category, not of a slice.
    internal class DevStore : MonoBehaviour
    {
        private const int ItemsPerPage = 200; // of the 250 merchant slots; the rest takes items you sell
        private const int Slots = 250;
        private const int StoreCredits = 99999999;

        internal static DevStore Instance;

        private Storage storage;
        private Merchant merchant;
        private readonly List<string> pageNames = new List<string>();
        private readonly List<List<int>> pages = new List<List<int>>();
        private readonly List<List<int>> categories = new List<List<int>>(); // by ItemCategories id, unsorted
        private int page;
        private int sortKey = ItemStats.Default;
        private bool sortHighFirst = true;

        private static bool controlsBuilt;
        private static Dropdown pageDropdown;
        private static bool syncingPage; // Unity 2018 has no Dropdown.SetValueWithoutNotify
        private static Text freeLabel;
        private static Text clearLabel;
        private const string ClearText = "Clear Bag";
        private const float ClearConfirmSeconds = 3f;
        private float clearArmedUntil;

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
            var go = new GameObject("Dev Store"); // the trade screen shows the merchant object's name as its title
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
            DevMenu.ShowStatus(message, true);
        }

        internal static void Teardown()
        {
            if (Instance != null && IsOpen())
            {
                UITrading.code.Quit();
            }
            controlsBuilt = false; // the buttons go with ItemSort's rows
            pageDropdown = null;
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

        internal static bool HasPendingTrade()
        {
            return IsOpen() && (UITrading.code.playerInventory.storage.tempItems.Count > 0
                || UITrading.code.merchantInventory.storage.tempItems.Count > 0);
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

        // Called from UITrading.OnEnable; ItemSort shows the rows holding our controls only while the dev store is the
        // merchant.
        internal static void SyncControls(UITrading trading)
        {
            bool ours = Instance != null && trading.isTrading && trading.merchantInventory.storage == Instance.storage;
            if (ours && !controlsBuilt)
            {
                BuildControls(trading);
            }
            if (ours)
            {
                Instance.UpdateLabels();
            }
        }

        private void BuildPages()
        {
            categories.Clear();
            for (int c = 0; c < ItemCategories.Names.Length; c++)
            {
                categories.Add(new List<int>());
            }
            foreach (KeyValuePair<int, Transform> kv in RM.code.AllItem)
            {
                Item item = kv.Value != null ? kv.Value.GetComponent<Item>() : null;
                if (item == null || kv.Key == 0)
                {
                    continue;
                }
                categories[ItemCategories.Of(kv.Key)].Add(kv.Key);
            }
            foreach (List<int> ids in categories)
            {
                ids.Sort();
            }
            Paginate();
            Plugin.Log.LogInfo("Dev store: " + pages.Count + " pages from " + RM.code.AllItem.Count + " items");
        }

        private void Paginate()
        {
            pages.Clear();
            pageNames.Clear();
            for (int c = 1; c < categories.Count; c++) // 0 is "All"
            {
                List<int> ids = sortKey == ItemStats.Default ? categories[c]
                    : ItemStats.Sorted(categories[c], sortKey, sortHighFirst, merchant);
                AddCategory(ItemCategories.Names[c], ids);
            }
        }

        internal static bool IsMerchant(Storage s)
        {
            return Instance != null && s != null && s == Instance.storage;
        }

        // ItemSort: re-page every category in this order, staying on the current category's first page.
        internal static void ApplySort(int key, bool highFirst)
        {
            DevStore store = Instance;
            if (store == null || (store.sortKey == key && store.sortHighFirst == highFirst) || store.pages.Count == 0)
            {
                return;
            }
            string current = Category(store.pageNames[store.page]);
            store.sortKey = key;
            store.sortHighFirst = highFirst;
            store.Paginate();
            int target = 0;
            for (int i = 0; i < store.pageNames.Count; i++)
            {
                if (Category(store.pageNames[i]) == current)
                {
                    target = i;
                    break;
                }
            }
            store.GoTo(target);
        }

        private void AddCategory(string name, List<int> ids)
        {
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

        internal static void PickPage(int value)
        {
            if (syncingPage || Instance == null || value < 0 || value >= Instance.pages.Count || value == Instance.page)
            {
                return;
            }
            Instance.GoTo(value);
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
            ItemSort.RefreshOptions(ItemSort.Other); // the sort stats on offer follow the page's category
        }

        // Every item of the current page's category (what the sort dropdown offers stats for).
        internal static List<int> CurrentCategoryItems()
        {
            DevStore store = Instance;
            if (store == null || store.pages.Count == 0)
            {
                return new List<int>();
            }
            string current = Category(store.pageNames[store.page]);
            for (int c = 1; c < ItemCategories.Names.Length; c++)
            {
                if (ItemCategories.Names[c] == current)
                {
                    return store.categories[c];
                }
            }
            return store.pages[store.page];
        }

        // Two clicks within a few seconds: empties your bag (not what you're wearing). Cancels any pending trade first
        // so the game's temp-item bookkeeping doesn't hand anything back afterwards.
        internal void ClearBag()
        {
            if (Time.unscaledTime > clearArmedUntil)
            {
                clearArmedUntil = Time.unscaledTime + ClearConfirmSeconds;
                if (clearLabel != null)
                {
                    clearLabel.text = "Click again";
                }
                return;
            }
            clearArmedUntil = 0f;
            if (clearLabel != null)
            {
                clearLabel.text = ClearText;
            }
            if (IsOpen())
            {
                UITrading.code.Revert();
            }
            Storage bag = MapAgent.code.GetComponent<Storage>();
            int removed = 0;
            for (int i = 0; i < bag.items.Count; i++)
            {
                if (bag.items[i] != 0)
                {
                    bag.items[i] = 0;
                    removed++;
                }
            }
            bag.tempItems.Clear();
            if (bag.inventory != null)
            {
                bag.inventory.Refresh();
            }
            if (UIInventory.code != null && UIInventory.code.playerInventory != null)
            {
                UIInventory.code.playerInventory.Refresh();
            }
            Fill();
            MapAgent.code._Army.GetMapSpeed(isRefresh: true);
            DevActions.Report("Cleared " + removed + " items from your bag");
        }

        private void Update()
        {
            // Disarm the Clear Bag confirmation if the second click doesn't come.
            if (clearArmedUntil > 0f && Time.unscaledTime > clearArmedUntil)
            {
                clearArmedUntil = 0f;
                if (clearLabel != null)
                {
                    clearLabel.text = ClearText;
                }
            }
        }

        internal void ToggleFree()
        {
            DevMode.StoreFree.Value = !DevMode.StoreFree.Value;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            if (pageDropdown != null && page < pageNames.Count)
            {
                syncingPage = true;
                if (pageDropdown.options.Count != pageNames.Count)
                {
                    pageDropdown.ClearOptions();
                    pageDropdown.AddOptions(pageNames); // the page count per category changes with the sort
                    TradeButtons.FitList(pageDropdown);
                }
                else
                {
                    for (int i = 0; i < pageNames.Count; i++)
                    {
                        pageDropdown.options[i].text = pageNames[i];
                    }
                }
                pageDropdown.value = page;
                pageDropdown.RefreshShownValue();
                syncingPage = false;
            }
            if (freeLabel != null)
            {
                freeLabel.text = DevMode.StoreFree.Value ? "Free: ON" : "Free: OFF";
            }
        }

        private static void BuildControls(UITrading trading)
        {
            Transform storeRow = ItemSort.ExtraRow(trading, ItemSort.Other);
            Transform bagRow = ItemSort.ExtraRow(trading, ItemSort.Bag);
            if (storeRow == null || bagRow == null)
            {
                Plugin.Log.LogWarning("Dev store: no control rows; store controls skipped");
                return;
            }
            controlsBuilt = true;
            Button deal = trading.btnDeal;
            pageDropdown = TradeButtons.CloneDropdown(storeRow, "FreemanSlop_Store_Page", 3f);
            if (pageDropdown != null)
            {
                pageDropdown.AddOptions(Instance.pageNames);
                TradeButtons.FitList(pageDropdown);
                PageDropdown handler = pageDropdown.gameObject.AddComponent<PageDropdown>();
                pageDropdown.onValueChanged.AddListener(new UnityAction<int>(handler.Changed));
            }
            freeLabel = AddControl(deal, storeRow, "", StoreButton.Free, 0f, 2f);
            clearLabel = AddControl(deal, bagRow, ClearText, StoreButton.ClearBag, 150f, 0f);
        }

        private static Text AddControl(Button template, Transform row, string label, int kind, float width, float flexible)
        {
            Text t = TradeButtons.Clone(template, row, label, width, flexible);
            GameObject go = TradeButtons.ButtonOf(t).gameObject;
            go.name = "FreemanSlop_Store_" + kind;
            var sb = go.AddComponent<StoreButton>();
            sb.Kind = kind;
            go.GetComponent<Button>().onClick.AddListener(new UnityAction(sb.Click));
            return t;
        }
    }

    internal class PageDropdown : MonoBehaviour
    {
        public void Changed(int value)
        {
            DevStore.PickPage(value);
        }
    }

    internal class StoreButton : MonoBehaviour
    {
        internal const int Free = 4;
        internal const int ClearBag = 5;

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
                case Free:
                    store.ToggleFree();
                    break;
                case ClearBag:
                    store.ClearBag();
                    break;
            }
        }
    }
}
