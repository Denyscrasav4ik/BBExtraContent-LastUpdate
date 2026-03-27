using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BBTimes.ModPatches
{
    [HarmonyPatch(typeof(MainMenu))]
    internal class MainMenuPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void Postfix(MainMenu __instance)
        {
            CreateModInfoText(__instance.transform);
        }

        private static void CreateModInfoText(Transform rootTransform)
        {
            if (rootTransform == null) return;

            Transform? templateTransform = rootTransform.Find("Reminder");
            if (templateTransform == null) return;

            if (rootTransform.Find("ModInfoExtra") != null) return;

            GameObject modInfo = GameObject.Instantiate(templateTransform.gameObject, rootTransform);
            modInfo.name = "ModInfoExtra";
            modInfo.transform.SetSiblingIndex(15);

            RectTransform rectTransform = modInfo.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(90f, 150f);
                rectTransform.sizeDelta = new Vector2(300f, 50f);
            }

            TextMeshProUGUI textComponent = modInfo.GetComponent<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.alignment = TextAlignmentOptions.Right;
                textComponent.isRightToLeftText = false;

                TextLocalizer localizer = textComponent.gameObject.GetComponent<TextLocalizer>();
                if (localizer == null)
                {
                    localizer = textComponent.gameObject.AddComponent<TextLocalizer>();
                }

                localizer.key = "BBTimes_ModInfo";
                localizer.GetLocalizedText(localizer.key);
            }
        }
    }
}
