using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using BBTimes.Plugin;
using BepInEx.Bootstrap;

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

            Transform templateTransform = rootTransform.Find("Reminder");
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
                rectTransform.anchoredPosition = new Vector2(90f, -90f);
                rectTransform.sizeDelta = new Vector2(300f, 50f);
            }

            TextMeshProUGUI textComponent = modInfo.GetComponent<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.alignment = TextAlignmentOptions.Right;
                textComponent.isRightToLeftText = false;

                TryApplyUkrainization(textComponent);
            }
        }

        private static void TryApplyUkrainization(TextMeshProUGUI textComponent)
        {
            bool isInstalled = Chainloader.PluginInfos.ContainsKey(Storage.guid_Ukrainization);
            if (!isInstalled) return;

            try
            {
                Type localizerType = Type.GetType("Ukrainization.TextLocalizer, Ukrainization");
                if (localizerType == null) return;

                var existing = textComponent.gameObject.GetComponent(localizerType);
                if (existing != null)
                {
                    UnityEngine.Object.Destroy(existing);
                }

                var localizer = textComponent.gameObject.AddComponent(localizerType);

                var keyField = localizerType.GetField("key");
                keyField?.SetValue(localizer, "BBTimes_ModInfo");

                var method = localizerType.GetMethod("GetLocalizedText");
                method?.Invoke(localizer, new object[] { "BBTimes_ModInfo" });
            }
            catch (Exception e) { }
        }
    }
}
