using BepInEx.Configuration;
using MTM101BaldAPI.OptionsAPI;
using MTM101BaldAPI.UI;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using MTM101BaldAPI;

namespace BBTimes.Manager
{
    public class BBTimesOptionsCategory : CustomOptionsCategory
    {
        private List<GameObject> categoryPages = new List<GameObject>();
        private int currentPageIndex = 0;
        private TextMeshProUGUI categoryTitle;

        private const float NAV_Y = 80f;
        private const float VIEWPORT_Y = -35f;
        private const float VIEWPORT_HEIGHT = 260f;
        private const float STEP_Y = 42f;
        private float _dummyY = 0f;

        public override void Build()
        {
            categoryTitle = CreateText("CategoryTitle", "Environment", new Vector3(0, NAV_Y, 0), BaldiFonts.ComicSans24, TextAlignmentOptions.Center, new Vector2(400, 30), Color.black, false);
            TextLocalizer titleLocalizer = categoryTitle.gameObject.AddComponent<TextLocalizer>();
            titleLocalizer.key = "BBTimes_Settings_Category_Environment";

            CreateButton(() => ChangePage(-1), BBTimesManager.man.Get<Sprite>("ArrowLeftHigh"), BBTimesManager.man.Get<Sprite>("ArrowLeftUnhigh"), "PrevPage", new Vector3(-165f, NAV_Y, 0f), null);
            CreateButton(() => ChangePage(1), BBTimesManager.man.Get<Sprite>("ArrowRightHigh"), BBTimesManager.man.Get<Sprite>("ArrowRightUnhigh"), "NextPage", new Vector3(170f, NAV_Y, 0f), null);

            var scrollText = CreateText("ScrollInstruction", "Use mouse wheel to scroll", new Vector3(0, -170f, 0), BaldiFonts.ComicSans12, TextAlignmentOptions.Center, new Vector2(400, 20), Color.red, false);
            TextLocalizer scrollLocalizer = scrollText.gameObject.AddComponent<TextLocalizer>();
            scrollLocalizer.key = "BBTimes_Settings_ScrollInstruction";

            BuildEnvironmentPage();
            BuildMiscPage();
            BuildHolidayPage();
            BuildNPCPage();
            BuildItemPage();
            BuildEventsPage();
            BuildStructuresPage();
            BuildNaturalObjectsPage();
            BuildSpecialRoomsPage();

            for (int i = 0; i < categoryPages.Count; i++)
            {
                categoryPages[i].SetActive(i == 0);
            }

            CreateApplyButton(() =>
            {
                BasePlugin.Instance.Config.Save();
            });
        }

        private void ChangePage(int direction)
        {
            categoryPages[currentPageIndex].SetActive(false);
            currentPageIndex = (currentPageIndex + direction + categoryPages.Count) % categoryPages.Count;
            categoryPages[currentPageIndex].SetActive(true);

            string pageName = categoryPages[currentPageIndex].name;
            categoryTitle.gameObject.GetComponent<TextLocalizer>().key = $"BBTimes_Settings_Category_{pageName}";

            categoryTitle.text = Singleton<LocalizationManager>.Instance.GetLocalizedText(categoryTitle.gameObject.GetComponent<TextLocalizer>().key);

            var scroll = categoryPages[currentPageIndex].GetComponentInChildren<BBTimesCategoryScroll>();
            if (scroll != null) scroll.ResetScroll();
        }

        private RectTransform CreateScrollablePage(string name)
        {
            GameObject pageRoot = new GameObject(name);
            pageRoot.transform.SetParent(this.transform, false);
            categoryPages.Add(pageRoot);

            GameObject vpObj = new GameObject("Viewport");
            RectTransform viewport = vpObj.AddComponent<RectTransform>();
            viewport.SetParent(pageRoot.transform, false);
            viewport.sizeDelta = new Vector2(480f, VIEWPORT_HEIGHT);
            viewport.anchoredPosition = new Vector2(0f, VIEWPORT_Y);
            vpObj.AddComponent<RectMask2D>();

            GameObject contentObj = new GameObject("Content");
            RectTransform content = contentObj.AddComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            contentObj.AddComponent<ForcedVerticalLayout>();

            var scroll = contentObj.AddComponent<BBTimesCategoryScroll>();
            scroll.target = content;

            return content;
        }

        private void FinalizeScroll(RectTransform content, float lastY)
        {
            float totalHeight = Mathf.Max(VIEWPORT_HEIGHT, lastY + 20f);
            content.sizeDelta = new Vector2(480f, totalHeight);
            content.anchoredPosition = Vector2.zero;
        }

        private void AddToggle(RectTransform parent, ConfigEntry<bool> entry, string uiLabelKey, string tooltipKey, ref float y)
        {
            string label = Singleton<LocalizationManager>.Instance.GetLocalizedText(uiLabelKey);
            var toggle = CreateToggle(label, label, entry.Value, Vector3.zero, 300f);
            StyleToggleCentered(toggle, 150f);

            toggle.transform.SetParent(parent, false);
            toggle.transform.localPosition = new Vector3(0f, -y, 0f);

            StandardMenuButton btn = toggle.transform.Find("HotSpot").GetComponent<StandardMenuButton>();
            btn.OnPress.AddListener(() =>
            {
                entry.Value = toggle.Value;
                BasePlugin.Instance.Config.Save();
            });

            AddTooltip(btn, Singleton<LocalizationManager>.Instance.GetLocalizedText(tooltipKey));

            y += STEP_Y;
        }

        private void AddDynamicToggle(RectTransform parent, string section, string configKey, string uiLabel, string tooltipKey, ref float y)
        {
            var config = BasePlugin.Instance.Config.Bind(section, configKey, true, $"Enable {uiLabel}");
            var toggle = CreateToggle(uiLabel, uiLabel, config.Value, Vector3.zero, 300f);
            StyleToggleCentered(toggle, 150f);

            toggle.transform.SetParent(parent, false);
            toggle.transform.localPosition = new Vector3(0f, -y, 0f);

            StandardMenuButton btn = toggle.transform.Find("HotSpot").GetComponent<StandardMenuButton>();
            btn.OnPress.AddListener(() =>
            {
                config.Value = toggle.Value;
                BasePlugin.Instance.Config.Save();
            });

            string translatedTooltip = string.Format(Singleton<LocalizationManager>.Instance.GetLocalizedText(tooltipKey), uiLabel);
            AddTooltip(btn, translatedTooltip);

            y += STEP_Y;
        }

        private void StyleToggleCentered(MenuToggle toggle, float checkboxOffset)
        {
            if (toggle == null) return;

            Transform textObj = toggle.transform.Find("ToggleText");
            if (textObj != null)
            {
                TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.anchoredPosition = Vector2.zero;
                tmp.rectTransform.sizeDelta = new Vector2(300f, 32f);
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 10f;
                tmp.fontSizeMax = 24f;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
            }

            Transform boxObj = toggle.transform.Find("Box");
            if (boxObj != null)
            {
                boxObj.localPosition = new Vector3(checkboxOffset, 0f, 0f);
            }

            Transform hotSpot = toggle.transform.Find("HotSpot");
            if (hotSpot != null)
            {
                hotSpot.localPosition = Vector3.zero;
            }
        }

        private void BuildEnvironmentPage()
        {
            var container = CreateScrollablePage("Environment");
            float y = 20f;
            AddToggle(container, BasePlugin.Instance.disableOutside, "BBTimes_Settings_Env_Outside_Name", "BBTimes_Settings_Env_Outside_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.enableBigRooms, "BBTimes_Settings_Env_BigRooms_Name", "BBTimes_Settings_Env_BigRooms_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.disableSchoolhouseEscape, "BBTimes_Settings_Env_Escape_Name", "BBTimes_Settings_Env_Escape_Desc", ref y);
            FinalizeScroll(container, y);
        }

        private void BuildMiscPage()
        {
            var container = CreateScrollablePage("Miscellaneous");
            float y = 20f;
            AddToggle(container, BasePlugin.Instance.enableYoutuberMode, "BBTimes_Settings_Misc_Youtuber_Name", "BBTimes_Settings_Misc_Youtuber_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.enableReplacementNPCsAsNormalOnes, "BBTimes_Settings_Misc_Replacements_Name", "BBTimes_Settings_Misc_Replacements_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.disableArcadeRennovationsSupport, "BBTimes_Settings_Misc_Arcade_Name", "BBTimes_Settings_Misc_Arcade_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.disableRedEndingCutscene, "BBTimes_Settings_Misc_Cutscene_Name", "BBTimes_Settings_Misc_Cutscene_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.enableUnbalancedLegacyMode, "BBTimes_Settings_Misc_Legacy_Name", "BBTimes_Settings_Misc_Legacy_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.forceEnableSecretObjects, "BBTimes_Settings_Misc_Secrets_Name", "BBTimes_Settings_Misc_Secrets_Desc", ref y);
            FinalizeScroll(container, y);
        }

        private void BuildHolidayPage()
        {
            var container = CreateScrollablePage("Holidays");
            float y = 20f;
            AddToggle(container, BasePlugin.Instance.forceChristmasMode, "BBTimes_Settings_Holidays_Christmas_Name", "BBTimes_Settings_Holidays_Christmas_Desc", ref y);
            AddToggle(container, BasePlugin.Instance.forceBaldiMarch31Day, "BBTimes_Settings_Holidays_March31_Name", "BBTimes_Settings_Holidays_March31_Desc", ref y);
            FinalizeScroll(container, y);
        }

        private void BuildNPCPage()
        {
            var container = CreateScrollablePage("NPC Settings");
            HashSet<string> addedNames = new HashSet<string>();
            var uniqueNPCs = BBTimesManager.floorDatas.Values.SelectMany(f => f.NPCs).Select(n => n.selection).Where(n => n != null).ToList();

            foreach (var npc in uniqueNPCs)
                if (addedNames.Add(npc.name))
                    AddDynamicToggle(container, "NPC Settings", "Enable " + npc.name, npc.name, "BBTimes_Settings_Generic_NPC_Desc", ref _dummyY);

            FinalizeScroll(container, addedNames.Count * STEP_Y + 40f);
        }

        private void BuildItemPage()
        {
            var container = CreateScrollablePage("Item Settings");
            HashSet<string> addedItems = new HashSet<string>();
            var allItems = BBTimesManager.floorDatas.Values.SelectMany(f => f.Items.Select(i => i.selection).Concat(f.ShopItems.Select(si => si.selection)).Concat(f.ForcedItems.Select(fi => fi.selection))).Where(i => i != null).ToList();

            foreach (var item in allItems)
            {
                string itemName = item.itemType == Items.Points ? item.nameKey : EnumExtensions.GetExtendedName<Items>((int)item.itemType);
                if (addedItems.Add(itemName))
                    AddDynamicToggle(container, "Item Settings", "Enable " + itemName, itemName, "BBTimes_Settings_Generic_Item_Desc", ref _dummyY);
            }
            FinalizeScroll(container, addedItems.Count * STEP_Y + 40f);
        }

        private void BuildEventsPage()
        {
            var container = CreateScrollablePage("Random Event Settings");
            float y = 20f;

            HashSet<string> addedEvents = new HashSet<string>();
            var uniqueEvents = BBTimesManager.floorDatas.Values
                .SelectMany(f => f.Events)
                .Select(e => e.selection)
                .Where(e => e != null)
                .ToList();

            foreach (var ev in uniqueEvents)
                if (addedEvents.Add(ev.name))
                    AddDynamicToggle(container, "Random Event Settings", "Enable " + ev.name, ev.name, "BBTimes_Settings_Generic_Event_Desc", ref y);

            FinalizeScroll(container, y);
        }

        private void BuildStructuresPage()
        {
            var container = CreateScrollablePage("Structure Settings");
            float y = 20f;

            HashSet<string> addedStructures = new HashSet<string>();
            var uniqueStructures = BBTimesManager.floorDatas.Values
                .SelectMany(f => f.WeightedObjectBuilders.Select(w => w.selection.prefab.name)
                .Concat(f.ForcedObjectBuilders.Select(fb => fb.GetWeightedSelection().prefab.name)))
                .Distinct()
                .ToList();

            foreach (var strName in uniqueStructures)
                if (addedStructures.Add(strName))
                    AddDynamicToggle(container, "Structure Settings", "Enable " + strName, strName, "BBTimes_Settings_Generic_Structure_Desc", ref y);

            FinalizeScroll(container, y);
        }

        private void BuildNaturalObjectsPage()
        {
            var container = CreateScrollablePage("Naturally Spawning Objects Settings");
            float y = 20f;

            HashSet<string> addedNat = new HashSet<string>();
            var uniqueNat = BBTimesManager.floorDatas.Values
                .SelectMany(f => f.WeightedNaturalObjects)
                .Select(n => n.selection.name)
                .Distinct()
                .ToList();

            foreach (var objName in uniqueNat)
                if (addedNat.Add(objName))
                    AddDynamicToggle(container, "Naturally Spawning Objects Settings", "Enable " + objName, objName, "BBTimes_Settings_Generic_Object_Desc", ref y);

            FinalizeScroll(container, y);
        }

        private void BuildSpecialRoomsPage()
        {
            var container = CreateScrollablePage("Special Room Settings");
            float y = 20f;

            HashSet<string> addedRooms = new HashSet<string>();
            var uniqueRooms = BBTimesManager.floorDatas.Values
                .SelectMany(f => f.SpecialRooms)
                .Where(r => r.HasRoomName)
                .Select(r => r.RoomName)
                .Distinct()
                .ToList();

            foreach (var roomName in uniqueRooms)
                if (addedRooms.Add(roomName))
                    AddDynamicToggle(container, "Special Room Settings", "Enable " + roomName, roomName, "BBTimes_Settings_Generic_Room_Desc", ref y);

            FinalizeScroll(container, y);
        }
    }

    public class BBTimesCategoryScroll : MonoBehaviour
    {
        public RectTransform target;
        private float scrollY = 0f;
        private float targetScrollY = 0f;
        private const float sensitivity = 30f;
        private const float lerpSpeed = 12f;

        void Update()
        {
            if (!transform.parent.parent.gameObject.activeInHierarchy) return;

            float delta = Input.mouseScrollDelta.y;
            if (delta != 0) targetScrollY -= delta * sensitivity;

            scrollY = Mathf.Lerp(scrollY, targetScrollY, Time.unscaledDeltaTime * lerpSpeed);

            if (target != null)
                target.anchoredPosition = new Vector2(0f, -scrollY);
        }

        public void ResetScroll()
        {
            scrollY = 0f;
            targetScrollY = 0f;
        }
    }

    //Hacky thing to fix a a positioning bug
    public class ForcedVerticalLayout : MonoBehaviour
    {
        public float startY = 50f;
        public float spacing = 42f;

        void LateUpdate()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                float y = startY + (i * spacing);
                child.localPosition = new Vector3(0f, -y, 0f);
            }
        }
    }
}
