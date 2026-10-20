using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Ex;
using HarmonyLib;
using UnityEngine.UI;

namespace FreemanFixes
{
    // Same-sex romance. The companion talk menu (HeroRecruitCtrl.ShowDilogByTalk) only offers the "marry me" line when
    // the companion's sex differs from yours (HeroRecruitCtrl.cs:1259); everything after it is already sex-neutral:
    // MarryAnswer sets HeroAgent.Partner, the 90/120/150 affection lines and divorce only check Partner, the save keeps
    // IsHavePartner, and the status reads Hero_partner ("In Relationship"). So a postfix adds the same line for a
    // same-sex companion under vanilla's other conditions (80+ affection, CanMarry, no partner, nobody else is your
    // partner, the line is translated), wired to the game's own MarryAnswer. Some English proposal/acceptance lines
    // are written for a man and a woman ("my woman", "she"), so for a same-sex couple a line with gendered words is
    // swapped for a neutral one; other languages keep the original, since we can't check their wording.
    internal static class SameSexRomance
    {
        private const int MarryAffection = 80; // as ShowDilogByTalk
        private const string NeutralQuestion = "I care about you more than I can say. Will you be my partner?";
        private const string NeutralAnswer = "Yes. I've been hoping you would ask.";

        private static readonly Regex Gendered = new Regex(
            @"\b(husband|wife|man|woman|girl|boy|lady|he|she|him|her|his|hers)\b", RegexOptions.IgnoreCase);

        private static readonly FieldInfo CallBackField = AccessTools.Field(typeof(DialogueInfo), "CallBack");
        private static readonly MethodInfo MarryAnswerMethod = AccessTools.Method(typeof(HeroRecruitCtrl), "MarryAnswer");

        internal static bool SameSex(HeroAgent hero)
        {
            return hero != null && hero.gen != null && MapAgent.code != null && MapAgent.code.gen != null
                && hero.gen.sex == MapAgent.code.gen.sex;
        }

        // The game's line, or the neutral one when the English text assumes a man and a woman.
        private static string Line(string key, string neutral)
        {
            string text = Localization.Format(key, new object[0]);
            return Gendered.IsMatch(text) ? neutral : text;
        }

        // ShowDilogByTalk postfix: vanilla's marry-me condition without the sex check.
        internal static void AddProposal(HeroAgent hero, List<DialogueInfo> list)
        {
            if (list == null || !SameSex(hero) || hero.Relationship < MarryAffection || !hero.CanMarry
                || hero.Partner != null || !Localization.Contains(hero.MarryMeQuestion) || AnyPartner()
                || (object)CallBackField == null || (object)MarryAnswerMethod == null)
            {
                return;
            }
            var di = new DialogueInfo();
            di.Info = Line(hero.MarryMeQuestion, NeutralQuestion);
            di.hero = hero;
            // The callback's type is the game's own delegate type; built by reflection so we never name it.
            CallBackField.SetValue(di, System.Delegate.CreateDelegate(CallBackField.FieldType, HeroRecruitCtrl.Instance,
                MarryAnswerMethod));
            list.Add(di); // where vanilla puts it: last, since there's no partner yet
        }

        private static bool AnyPartner()
        {
            foreach (HeroAgent h in HeroRecruitCtrl.Instance.heroAgents)
            {
                if (h != null && h.Partner != null)
                {
                    return true;
                }
            }
            return false;
        }

        // MarryAnswer postfix: the acceptance line, neutral for a same-sex couple when the English one is gendered.
        internal static void Answered(HeroAgent hero, Text text)
        {
            if (text != null && SameSex(hero))
            {
                string line = Line(hero.MarryMeAnswer, NeutralAnswer);
                if (line != text.text)
                {
                    text.SetText(line);
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class SameSexRomancePatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(HeroRecruitCtrl), nameof(HeroRecruitCtrl.ShowDilogByTalk))]
        private static void TalkListBuilt(HeroAgent hero, List<DialogueInfo> __result)
        {
            SameSexRomance.AddProposal(hero, __result);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HeroRecruitCtrl), "MarryAnswer")]
        private static void MarryAnswered(HeroAgent hero, Text text)
        {
            SameSexRomance.Answered(hero, text);
        }
    }
}
