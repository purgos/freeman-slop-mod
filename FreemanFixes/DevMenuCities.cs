using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Dev menu "Cities" tab, replacing the cheat panel's AddAllCity / AddRecentlyCity (UICheat, hidden in dev mode):
    // Take All Cities, plus one row per city with its owner and an Owner button that lists the factions in a side
    // column (like the Factions tab's Wars column) to hand the city to any of them. DevActions does the change.
    // A change that leaves a faction with nothing (so the game destroys it) takes a second click within a few seconds.
    internal partial class DevMenu
    {
        private const int ActTabCities = 40;
        private const int ActCityOwnerFor = 41;
        private const int ActSetCityOwner = 42;
        private const int ActTakeAllCities = 43;
        private const float CitiesWidth = 1220f;
        private const float OwnersWidth = 570f;
        private const float DestroyConfirmSeconds = 5f;

        private GameObject citiesPage;
        private readonly List<City> cities = new List<City>();
        private readonly List<Text> cityOwnerLabels = new List<Text>();
        private Transform ownerList;
        private Text ownersHeader;
        private int ownerFor = -1;
        private int armedCityAction = -1; // Key(action, arg) waiting for its confirming click, with the city it was for
        private int armedCityFor = -1;
        private float armedCityUntil;

        private void BuildCitiesPage(RectTransform root)
        {
            citiesPage = Page(root, "CitiesPage");
            var page = (RectTransform)citiesPage.transform;
            Transform col = Column(page, CitiesWidth);
            Header(col, "Cities  -  owner of each city (a faction left with none is destroyed by the game)");
            Transform allRow = Row(col);
            MakeButton(allRow, "Take All Cities", ActTakeAllCities, 0).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Transform list = ScrollArea(col);
            if (Global.code != null && Global.code.locations != null)
            {
                foreach (Transform t in Global.code.locations.items)
                {
                    City city = t != null ? t.GetComponent<City>() : null;
                    if (city != null && city._Army != null)
                    {
                        cities.Add(city);
                    }
                }
            }
            for (int i = 0; i < cities.Count; i++)
            {
                Transform row = Row(list);
                Label(row, cities[i].Name, 0f, 1f);
                cityOwnerLabels.Add(Label(row, "", 360f, 0f));
                Width(MakeButton(row, "Owner", ActCityOwnerFor, i), 120f);
            }

            Transform owners = Column(page, OwnersWidth);
            ownersHeader = HeaderText(owners, "Owner  -  press Owner on a city");
            ownerList = ScrollArea(owners);
            citiesPage.SetActive(false);
        }

        private void RefreshCities()
        {
            for (int i = 0; i < cities.Count; i++)
            {
                Faction owner = cities[i] != null ? cities[i]._Army.faction : null;
                cityOwnerLabels[i].text = owner != null ? owner.Name : "-";
                cityOwnerLabels[i].color = owner != null ? FactionColor(owner) : tRowText.color;
            }
            RebuildOwnerList();
        }

        private void RebuildOwnerList()
        {
            if (ownerList == null)
            {
                return;
            }
            for (int i = ownerList.childCount - 1; i >= 0; i--)
            {
                Destroy(ownerList.GetChild(i).gameObject);
            }
            if (ownerFor < 0 || ownerFor >= cities.Count || cities[ownerFor] == null)
            {
                return;
            }
            City city = cities[ownerFor];
            ownersHeader.text = "Owner  -  " + city.Name;
            for (int j = 0; j < factions.Count; j++)
            {
                Faction f = factions[j];
                if (f == null || (f.isDestroyed && f.name != "Player"))
                {
                    continue;
                }
                bool owns = city._Army.faction == f;
                Transform row = Row(ownerList);
                Text name = Label(row, f.Name, 0f, 1f);
                name.color = FactionColor(f);
                Width(MakeButton(row, owns ? "Owner" : "Give", ActSetCityOwner, j), 120f);
            }
        }

        // True when nobody gets destroyed, or this is the second click on the same button in time; otherwise arms it
        // and says who would go.
        private bool ConfirmDestroy(int action, int arg, List<Faction> doomed, string what, string button)
        {
            if (doomed.Count == 0)
            {
                return true;
            }
            int key = Key(action, arg);
            if (armedCityAction == key && armedCityFor == ownerFor && Time.unscaledTime <= armedCityUntil)
            {
                armedCityAction = -1;
                return true;
            }
            armedCityAction = key;
            armedCityFor = ownerFor;
            armedCityUntil = Time.unscaledTime + DestroyConfirmSeconds;
            var names = new System.Text.StringBuilder();
            foreach (Faction f in doomed)
            {
                if (names.Length > 0)
                {
                    names.Append(", ");
                }
                names.Append(f.Name);
            }
            ShowStatus(what + " leaves " + names + " with nothing and the game will destroy " +
                (doomed.Count == 1 ? "it" : "them") + " - click " + button + " again within 5 seconds to confirm", true);
            return false;
        }

        // Returns true if the action belonged to the Cities tab.
        private bool DoCities(int action, int arg)
        {
            switch (action)
            {
                case ActTabCities:
                    ShowTab(citiesPage);
                    return true;
                case ActCityOwnerFor:
                    ownerFor = arg;
                    break;
                case ActSetCityOwner:
                    if (ownerFor >= 0 && ownerFor < cities.Count && cities[ownerFor] != null)
                    {
                        City city = cities[ownerFor];
                        if (!ConfirmDestroy(action, arg, DevActions.DestroyedBy(city, factions[arg]),
                            "Giving " + city.Name + " to " + factions[arg].Name, "Give"))
                        {
                            return true;
                        }
                        DevActions.SetCityOwner(city, factions[arg]);
                    }
                    break;
                case ActTakeAllCities:
                    if (!ConfirmDestroy(action, arg, DevActions.DestroyedByTakeAll(), "Taking every city", "Take All Cities"))
                    {
                        return true;
                    }
                    DevActions.TakeAllCities();
                    break;
                default:
                    return false;
            }
            Refresh();
            return true;
        }
    }
}
