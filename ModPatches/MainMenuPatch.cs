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

            var button = modInfo.GetComponent<StandardMenuButton>();
            if (button != null)
            {
                UnityEngine.Object.Destroy(button);
            }

            RectTransform rectTransform = modInfo.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(165f, -90f);
                rectTransform.sizeDelta = new Vector2(150f, 50f);
            }

            TextMeshProUGUI textComponent = modInfo.GetComponent<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.alignment = TextAlignmentOptions.Right;
                textComponent.isRightToLeftText = false;

                TryApplyLocalization(textComponent);
            }
        }

        private static void TryApplyLocalization(TextMeshProUGUI textComponent)
        {
            GameObject go = textComponent.gameObject;

            bool hasUkrainization = Chainloader.PluginInfos.ContainsKey(Storage.guid_Ukrainization);

            if (hasUkrainization)
            {
                try
                {
                    Type ukrType = Type.GetType("Ukrainization.TextLocalizer, Ukrainization");
                    if (ukrType != null)
                    {
                        var existing = go.GetComponent(ukrType);
                        if (existing != null)
                            UnityEngine.Object.Destroy(existing);

                        var localizer = go.AddComponent(ukrType);

                        var keyField = ukrType.GetField("key");
                        keyField?.SetValue(localizer, "BBTimes_ModInfo");

                        var apply = ukrType.GetMethod("ApplyLocalization");
                        apply?.Invoke(localizer, null);

                        return;
                    }
                }
                catch { }
            }

            try
            {
                Type baseType = typeof(TextLocalizer);

                var existing = go.GetComponent(baseType);
                if (existing != null)
                    UnityEngine.Object.Destroy(existing);

                var localizer = go.AddComponent<TextLocalizer>();

                localizer.key = "BBTimes_ModInfo";
                localizer.encrypted = false;
                localizer.onlySetIfBlank = false;

                localizer.GetLocalizedText(localizer.key);
            }
            catch { }
        }
    }
}
