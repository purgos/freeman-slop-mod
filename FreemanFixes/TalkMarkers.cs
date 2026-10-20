using System.Collections.Generic;
using Ex;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FreemanFixes
{
    // The companions panel (HeroInfoShow) has a Talk button per companion (HeroShow.TalkBtn, plus HeroInfoShow.Talkbtn
    // for the selected one), but nothing says when a companion has something new to say. In the talk window
    // (HeroRecruitCtrl.ShowDilogByTalk) a line is unread while its DialogueInfo.IsTalk is true, and UIHeroWindow
    // greys it out once read: "Tell me about yourself" until HeroAgent.IsTalkPast, and each Q_A question once
    // affection reaches i * 10 and the one before it was answered, until its own IsTalk clears. We mirror those rules
    // and add a bold red "(!)" to the Talk button's label while any such line is unread. Rows refresh in HeroShow.Show /
    // ReafashHeriInfo, and every visible row again when the talk window closes (lines read there clear the marker).
    internal static class TalkMarkers
    {
        private const string Suffix = " <b><color=#FF0000>(!)</color></b>";

        private static readonly HashSet<Text> marked = new HashSet<Text>();
        private static bool logged;

        internal static bool HasUnread(HeroAgent hero)
        {
            if (hero == null)
            {
                return false;
            }
            if (!hero.IsTalkPast && Translated(hero.Past))
            {
                return true;
            }
            for (int i = 0; i < hero.Q_A.Count; i++)
            {
                QuestionAndAnswer qa = hero.Q_A[i];
                if (hero.Relationship < i * 10 || (i > 0 && hero.Q_A[i - 1].IsTalk))
                {
                    continue; // not offered yet
                }
                if (qa.IsTalk && Translated(qa.Question) && Translated(qa.Answer))
                {
                    return true;
                }
            }
            return false;
        }

        // ShowDilogByTalk only offers lines whose key has a translation.
        private static bool Translated(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            string text = Localization.Format(key, new object[0]);
            return !string.IsNullOrEmpty(text) && text != key;
        }

        internal static void Mark(Button button, NPCGen gen)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null || gen == null)
            {
                return;
            }
            if (!logged)
            {
                logged = true;
                Plugin.Log.LogInfo("Talk marker: label \"" + Plain(label.text) + "\", rich text " + label.supportRichText);
            }
            string plain = Plain(label.text);
            if (HasUnread(gen.GetComponent<HeroAgent>()))
            {
                label.supportRichText = true;
                label.text = plain + Suffix;
                marked.Add(label);
            }
            else
            {
                label.text = plain;
                marked.Remove(label);
            }
        }

        internal static void RefreshVisible()
        {
            foreach (HeroShow row in Object.FindObjectsOfType<HeroShow>())
            {
                if (row._NpcGen != null)
                {
                    Mark(row.TalkBtn, row._NpcGen);
                }
            }
            HeroInfoShow info = HeroInfoShow.code;
            if (info != null && info.CurNpc != null)
            {
                Mark(info.Talkbtn, info.CurNpc);
            }
        }

        // Hot reload: put the game's labels back.
        internal static void Teardown()
        {
            foreach (Text label in marked)
            {
                if (label != null)
                {
                    label.text = Plain(label.text);
                }
            }
            marked.Clear();
        }

        private static string Plain(string text)
        {
            return text != null && text.EndsWith(Suffix) ? text.Substring(0, text.Length - Suffix.Length) : text;
        }
    }

    [HarmonyPatch]
    internal static class TalkMarkersPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(HeroShow), nameof(HeroShow.Show))]
        private static void RowShown(HeroShow __instance, NPCGen nPC)
        {
            TalkMarkers.Mark(__instance.TalkBtn, nPC);
        }

        // Also runs from ClickThis, which makes this row's companion the selected one (HeroInfoShow.CurNpc).
        [HarmonyPostfix, HarmonyPatch(typeof(HeroShow), nameof(HeroShow.ReafashHeriInfo))]
        private static void RowRefreshed(HeroShow __instance, NPCGen gen)
        {
            TalkMarkers.Mark(__instance.TalkBtn, gen);
            if (__instance.show != null && __instance.show.CurNpc == gen)
            {
                TalkMarkers.Mark(__instance.show.Talkbtn, gen);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIHeroWindow), nameof(UIHeroWindow.Close))]
        private static void TalkClosed()
        {
            TalkMarkers.RefreshVisible();
        }
    }
}
