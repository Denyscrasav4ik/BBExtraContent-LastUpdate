using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBTimes.CompatibilityModule.EditorCompat.Structures;
using BBTimes.CustomContent.Builders;
using BBTimes.CustomContent.Events;
using BBTimes.CustomContent.Objects;
using BBTimes.Extensions;
using BBTimes.Manager;
using BBTimes.Plugin;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Registers;
using PixelInternalAPI.Extensions;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using PlusStudioLevelFormat;
using TMPro;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat
{
    internal static class EditorIntegration
    {
        private static AssetManager _editorAssetMan;

        // CONSTANT FIELDS for registering content
        public const string TimesPrefix = "times_";
        // NPCs
        static readonly string[] allNpcs = [
            "ZeroPrize", "Adverto", "Bubbly",
                "CheeseMan", "CoolMop", "DetentionBot",
                "Faker", "Glubotrony",
                "HappyHolidays", "InkArtist", "PuddingFan",
                "Leapy", "Magicalstudent",
                "MrKreye", "Cactungus", "NoseMan", "OfficeChair",
                "PencilBoy", "Phawillow", "Pran",
                "Pix", "Quiker", "Rollingbot", "SerOran",
                "ScienceTeacher", "Snowfolke", "Stunly",
                "Superintendent",
                "VacuumCleaner", "Winterry", "ZapZap"
            ];

        // Random Events
        static readonly string[] allEvents = [
                "Principalout", "FrozenEvent", "CurtainsClosed", "HologramPast", "SkateboardDay", "Earthquake", "SuperFans", "LightningEvent", "SuperMysteryRoom", "NatureEvent"
            ];
        // Skyboxes
        static readonly string[] allSkyboxes = [
            "TimesNightSky"
        ];

        internal static void Initialize(AssetManager man)
        {
            LoadEditorAssets();
            InitializeVisuals(man);
            InitializeOtherTextures();
            InitializeDefaultTextures(LevelStudioPlugin.Instance.defaultRoomTextures);
            EditorInterfaceModes.AddModeCallback(InitializeTools);
        }

        /// <summary>
        /// Add the physical objects, items, npcs, stuff to the editor - aside from the tools.
        /// </summary>
        private static void LoadEditorAssets()
        {
            _editorAssetMan = new AssetManager();
            string editorUIPath = Path.Combine(BasePlugin.ModPath, "EditorUI");

            // Load all general UI sprites
            string[] files = Directory.GetFiles(editorUIPath);
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                // Debug.Log($"Adding icon \'UI/{name}\'");
                _editorAssetMan.Add("UI/" + name, AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(file), 40f));
            }

            // Load and process item sprites
            foreach (var meta in ItemMetaStorage.Instance.All())
            {
                if (meta.info != BBTimesManager.plug.Info) continue;

                ItemObject itm = meta.value;
                string itmEnum = itm.itemType == Items.Points ? itm.name : itm.itemType.ToStringExtended();

                // Skip if we've already processed this item (handles shared ItemObject instances)
                if (_editorAssetMan.ContainsKey("UI/ITM_" + itmEnum)) continue;

                Sprite icon = itm.itemSpriteSmall;
                var tex = icon.texture;

                if (tex.width != 32 || tex.height != 32)
                {
                    tex = tex.ActualResize(32, 32);
                }
                icon = AssetLoader.SpriteFromTexture2D(tex, 40f);
                _editorAssetMan.Add("UI/ITM_" + itmEnum, icon);
            }

            // *** Skyboxes ***
            foreach (var skybox in allSkyboxes)
            {
                LevelStudioPlugin.Instance.skyboxSprites.Add(skybox, GetSprite($"UI/Skybox_{skybox}", $"UI/skybox_{skybox}"));
                LevelStudioPlugin.Instance.selectableSkyboxes.Add(skybox);
            }
        }

        private static void InitializeVisuals(AssetManager man)
        {
            static UntouchableEditorBasicObject ReplaceEditorBasicObject(string key, EditorBasicObject basicObj)
            {
                var newComp = basicObj.gameObject.SwapComponent<EditorBasicObject, UntouchableEditorBasicObject>(false);
                LevelStudioPlugin.Instance.basicObjectDisplays[key] = newComp; // Update the component to not display a Null object
                return newComp;
            }
            // Objects
            EditorInterface.AddObjectVisual("bathStall", man.Get<GameObject>("editorPrefab_bathStall"), true);
            EditorInterface.AddObjectVisual("bathDoor", man.Get<GameObject>("editorPrefab_bathDoor"), true);
            EditorInterface.AddObjectVisual("sink", man.Get<GameObject>("editorPrefab_sink"), true);
            EditorInterface.AddObjectVisual("FancyComputerTable", man.Get<GameObject>("editorPrefab_FancyComputerTable"), true);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("ComputerBillboard", man.Get<GameObject>("editorPrefab_ComputerBillboard"), 1f, Vector3.zero);
            EditorInterface.AddObjectVisual("Foresttree", man.Get<GameObject>("editorPrefab_Foresttree"), true);
            EditorInterface.AddObjectVisual("Campfire", man.Get<GameObject>("editorPrefab_Campfire"), true);
            EditorInterface.AddObjectVisual("Beartrap", man.Get<GameObject>("editorPrefab_Beartrap"), true);
            EditorInterface.AddObjectVisual("KitchenCabinet", man.Get<GameObject>("editorPrefab_KitchenCabinet"), true);
            EditorInterface.AddObjectVisual("JoeChef", man.Get<GameObject>("editorPrefab_JoeChef"), true);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("FocusedStudent", man.Get<GameObject>("editorPrefab_FocusedStudent"), 2f, Vector3.zero);
            EditorInterface.AddObjectVisual("ComputerTeleporter", man.Get<GameObject>("editorPrefab_ComputerTeleporter"), true);
            EditorInterface.AddObjectVisual("DustShroom", man.Get<GameObject>("editorPrefab_DustShroom"), true);
            EditorInterface.AddObjectVisual("SensitiveVase", man.Get<GameObject>("editorPrefab_SensitiveVase"), true);
            EditorInterface.AddObjectVisual("TimesItemDescriptor", man.Get<GameObject>("editorPrefab_TimesItemDescriptor"), true);
            EditorInterface.AddObjectVisual("SnowyPlaygroundTree", man.Get<GameObject>("editorPrefab_SnowyPlaygroundTree"), true);
            EditorInterface.AddObjectVisual("SnowPile", man.Get<GameObject>("editorPrefab_SnowPile"), true);
            EditorInterface.AddObjectVisual("Shovel_ForSnowPile", man.Get<GameObject>("editorPrefab_Shovel_ForSnowPile"), true);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("MysteryTresentMaker", man.Get<GameObject>("editorPrefab_MysteryTresentMaker"), 2f, Vector3.zero);
            EditorInterface.AddObjectVisual("MetalFence", man.Get<GameObject>("editorPrefab_MetalFence"), true);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("SecretBread", man.Get<GameObject>("editorPrefab_SecretBread"), 1f, Vector3.zero);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("TimesKitchenSteak", man.Get<GameObject>("editorPrefab_TimesKitchenSteak"), 1f, Vector3.zero);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("JoeSign", man.Get<GameObject>("editorPrefab_JoeSign"), 2f, Vector3.zero);
            for (int i = 1; i <= 8; i++)
                EditorInterface.AddObjectVisualWithCustomSphereCollider("TimesGenericOutsideFlower_" + i, man.Get<GameObject>("editorPrefab_TimesGenericOutsideFlower_" + i), 1.5f, Vector3.zero);
            for (int i = 1; i <= 4; i++)
                EditorInterface.AddObjectVisual("TimesGenericCornerLamp_" + i, man.Get<GameObject>("editorPrefab_TimesGenericCornerLamp_" + i), true);

            // SECRET ENDING OBJECTS
            EditorInterface.AddObjectVisualWithCustomSphereCollider("Times_SecretBaldi", man.Get<GameObject>("editorPrefab_Times_SecretBaldi"), 2f, Vector3.zero);
            EditorInterface.AddObjectVisual("Times_InvisibleWall", man.Get<GameObject>("editorPrefab_Times_InvisibleWall"), true);
            EditorInterface.AddObjectVisual("Times_CanBeDisabledInvisibleWall", man.Get<GameObject>("editorPrefab_Times_CanBeDisabledInvisibleWall"), true);
            EditorInterface.AddObjectVisual("Times_ScrewingInvisibleWall", man.Get<GameObject>("editorPrefab_Times_ScrewingInvisibleWall"), true);
            EditorInterface.AddObjectVisual("Times_KeyLockedInvisibleWall", man.Get<GameObject>("editorPrefab_Times_KeyLockedInvisibleWall"), true);
            EditorInterface.AddObjectVisual("Times_SecretGenerator", man.Get<GameObject>("editorPrefab_Times_SecretGenerator"), true);
            EditorInterface.AddObjectVisual("Times_GeneratorCylinder", man.Get<GameObject>("editorPrefab_Times_GeneratorCylinder"), true);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("Times_theYAYComputer", man.Get<GameObject>("editorPrefab_Times_theYAYComputer"), 1f, Vector3.zero);
            EditorInterface.AddObjectVisualWithCustomSphereCollider("Times_TrueLorePaper", man.Get<GameObject>("editorPrefab_Times_TrueLorePaper"), 1f, Vector3.zero);
            EditorInterface.AddObjectVisual("Times_GeneratorLever", man.Get<GameObject>("editorPrefab_Times_GeneratorLever"), true);
            for (int i = 1; i <= 6; i++)
                EditorInterface.AddObjectVisual($"Times_ContainedBaldi_F{i}", man.Get<GameObject>($"editorPrefab_Times_ContainedBaldi_F{i}"), true);
            ReplaceEditorBasicObject(TimesPrefix + "SecretButton", EditorInterface.AddObjectVisual(TimesPrefix + "SecretButton", man.Get<GameObject>("editorPrefab_SecretButton"), true));

            // NPCs
            foreach (var npcName in allNpcs)
            {
                var en = EnumExtensions.GetFromExtendedName<Character>(npcName);
                var meta = NPCMetaStorage.Instance.Find(x => x.character == en && BBTimesManager.plug.Info == x.info);
                if (meta != null)
                {
                    EditorInterface.AddNPCVisual(TimesPrefix + npcName, meta.value);
                }
            }

            // Special case for oldsweep
            EditorInterface.AddNPCVisual("times_oldsweep", BBTimesManager.man.Get<NPC>("NPC_oldsweep"));

            // Rooms
            EditorInterface.AddRoomVisualManager<OutsideRoomVisualManager>("SnowyPlayground");
            EditorInterface.AddRoomVisualManager<OutsideRoomVisualManager>("IceRink");

            // Random Events
            foreach (var evName in allEvents)
                LevelStudioPlugin.Instance.eventSprites.Add(TimesPrefix + evName, GetSprite($"UI/event_{evName}", $"UI/Event_{evName}"));

            // ** Door/Windows
            // Small Doors
            var smallDoorBuilder = BBTimesManager.man.Get<Structure_SmallDoor>("Builder_Structure_SmallDoor");
            EditorInterface.AddDoor<DoorDisplay>(TimesPrefix + "SmallDoor", DoorIngameStatus.AlwaysObject, smallDoorBuilder.doorPre.mask[0], smallDoorBuilder.doorPre.overlayShut);

            // Windows
            EditorInterface.AddWindow(TimesPrefix + "MetalWindow", BBTimesManager.man.Get<WindowObject>("Window_MetalWindow"));
            EditorInterface.AddWindow(TimesPrefix + "ClassicWindow", BBTimesManager.man.Get<WindowObject>("Window_ClassicWindow"));
            EditorInterface.AddWindow(TimesPrefix + "RoundWindow", BBTimesManager.man.Get<WindowObject>("Window_RoundWindow"));

            // ** Structures **
            // Readonly variables here
            var referenceLineRenderer = Resources.FindObjectsOfTypeAll<ITM_GrapplingHook>()[0].lineRenderer;

            // Squishers
            var squisherBuilder = BBTimesManager.man.Get<Structure_Squisher>("Builder_Structure_Squisher");
            var squisherVisual = EditorInterface.AddStructureGenericVisual(TimesPrefix + "Squisher", squisherBuilder.squisherPre.gameObject);
            var squisherCollider = squisherVisual.GetComponent<BoxCollider>();
            squisherCollider.size = new(4.5f, 21f, 4.5f);
            squisherCollider.center = new(0f, 10.5f, 0f);
            squisherVisual.AddComponent<SettingsComponent>().offset = Vector3.up * 25f;
            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "Squisher", typeof(SquisherStructureLocation));

            // Security Camera
            var securityCameraBuilder = (Structure_Camera)BBTimesManager.man.Get<StructureBuilder>("Builder_Structure_Camera");
            var securityCamera = securityCameraBuilder.camPre.GetComponent<SecurityCamera>(); // Gets camera here
            var securityCameraObj = EditorInterface.AddStructureGenericVisual(TimesPrefix + "SecurityCamera", securityCameraBuilder.camPre.gameObject); // Add as a generic visual with no components
            securityCameraObj.AddComponent<SettingsComponent>().offset = Vector3.up * 15f;

            // Visual manager setup for Security Camera
            var secCamVisualManager = securityCameraObj.AddComponent<EditorSecurityCameraVisualManager>();
            secCamVisualManager.cameraPlaneRendererPre = securityCamera.visionIndicatorPre; // Indicator
            secCamVisualManager.collider = securityCameraObj.AddComponent<BoxCollider>(); // Adds collider
            secCamVisualManager.collider.isTrigger = true;
            secCamVisualManager.renderContainer = securityCameraObj.GetComponent<EditorRendererContainer>();

            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "SecurityCamera", typeof(SecurityCameraStructureLocation));
            // Trapdoors
            var trapdoorBuilder = (Structure_Trapdoor)BBTimesManager.man.Get<StructureBuilder>("Builder_Structure_Trapdoor");
            var trapdoor = trapdoorBuilder.trapDoorpre;
            var trapdoorRandomObj = AddStructureGenericVisual(TimesPrefix + "TrapdoorRandom", trapdoor.gameObject, typeof(TextMeshPro));
            trapdoorRandomObj.GetComponent<BoxCollider>().size = new(9.8f, 1f, 9.8f);
            trapdoorRandomObj.GetComponentInChildren<SpriteRenderer>().sprite = trapdoorBuilder.closedSprites[0];
            trapdoorRandomObj.AddComponent<SettingsComponent>();
            var trapdoorLinkedObj = AddStructureGenericVisual(TimesPrefix + "TrapdoorLinked", trapdoor.gameObject, typeof(TextMeshPro));
            trapdoorLinkedObj.GetComponent<BoxCollider>().size = new(9.8f, 1f, 9.8f);
            trapdoorLinkedObj.GetComponentInChildren<SpriteRenderer>().sprite = trapdoorBuilder.closedSprites[1]; // Linked is index 1
                                                                                                                  // Add line renderer to indicate linkage for linked trapdoor

            var trapdoorLinkedObj_lineRenderer = referenceLineRenderer.SafeInstantiate();
            trapdoorLinkedObj_lineRenderer.transform.SetParent(trapdoorLinkedObj.transform);
            trapdoorLinkedObj_lineRenderer.transform.localPosition = Vector3.zero;
            trapdoorLinkedObj_lineRenderer.gameObject.SetActive(false);
            trapdoorLinkedObj_lineRenderer.material.SetColor("_TextureColor", Color.blue);
            trapdoorLinkedObj_lineRenderer.widthMultiplier = 0.95f;
            trapdoorLinkedObj_lineRenderer.gameObject.layer = LayerMask.NameToLayer("Overlay");
            trapdoorLinkedObj_lineRenderer.positionCount = 2;
            trapdoorLinkedObj_lineRenderer.material.SetTexture(Storage.SPRITESTANDARD_LIGHTMAP, null); // No light map effect

            var trapdoorLinkedObj_visualManager = trapdoorLinkedObj.AddComponent<TrapdoorEditorVisualManager>();
            trapdoorLinkedObj_visualManager.container = trapdoorLinkedObj.GetComponent<EditorRendererContainer>();
            trapdoorLinkedObj_visualManager.lineRenderer = trapdoorLinkedObj_lineRenderer;

            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "Trapdoor", typeof(TrapdoorStructureLocation));

            // Notebook Machine
            var ntbMachineBuilder = (Structure_NotebookMachine)BBTimesManager.man.Get<StructureBuilder>("Builder_Structure_NotebookMachine");
            var ntbMachineObj = EditorInterface.AddStructureGenericVisual(TimesPrefix + "NotebookMachine", ntbMachineBuilder.ntbMachinePre.gameObject);
            Object.Destroy(ntbMachineObj.GetComponent<BoxCollider>()); // No Box collider

            var ntbMachineObj_collider = ntbMachineObj.AddComponent<SphereCollider>();
            ntbMachineObj_collider.isTrigger = true;
            ntbMachineObj_collider.center = Vector3.up * 1.25f;
            ntbMachineObj_collider.radius = 3f; // Slightly bigger than the notebook for a reason
            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "NotebookMachine", typeof(NotebookMachineStructureLocation));

            // Item Alarm
            var itemAlarmBuilder = (Structure_ItemAlarm)BBTimesManager.man.Get<StructureBuilder>("Builder_Structure_ItemAlarm");
            var itemAlarmObj = EditorInterface.AddStructureGenericVisual(TimesPrefix + "ItemAlarm", itemAlarmBuilder.alarmPre.gameObject);
            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "ItemAlarm", typeof(ItemAlarmStructureLocation));

            // Duct
            var ductBuilder = (Structure_Duct)BBTimesManager.man.Get<StructureBuilder>("Builder_Structure_Duct");
            var ductPrefab = ductBuilder.ventPrefab;

            // Visual for individual Duct
            var ductVisual = EditorInterface.AddStructureGenericVisual(TimesPrefix + "Duct", ductPrefab);
            var ductVisualManager = ductVisual.AddComponent<DuctEditorVisualManager>();
            ductVisualManager.container = ductVisual.GetComponent<EditorRendererContainer>();
            ductVisualManager.container.AddRendererRange(ductVisual.GetComponentsInChildren<Renderer>(), "none");

            var ductHitbox = ductVisual.GetComponent<BoxCollider>();
            ductHitbox.center = Vector3.up * 9.5f;
            ductHitbox.size = new(9.99f, 1f, 9.99f);

            // Create LineRenderer prefab for connections
            var duct_lineRenderer = referenceLineRenderer.SafeInstantiate();
            duct_lineRenderer.gameObject.SetActive(false);
            duct_lineRenderer.transform.SetParent(ductVisual.transform, false);
            duct_lineRenderer.material.SetColor("_TextureColor", Color.cyan);
            duct_lineRenderer.widthMultiplier = 0.75f;
            duct_lineRenderer.positionCount = 2;
            duct_lineRenderer.gameObject.layer = LayerMask.NameToLayer("Overlay");
            duct_lineRenderer.material.SetTexture(Storage.SPRITESTANDARD_LIGHTMAP, null);
            ductVisualManager.lineRendererPrefab = duct_lineRenderer;

            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "Duct", typeof(DuctStructureLocation));

            // ** Room Structures **
            // Event Machine
            var eventMachine = BBTimesManager.man.Get<GameObject>("editorPrefab_EventMachine");
            ReplaceEditorBasicObject(TimesPrefix + "EventMachine", EditorInterface.AddObjectVisualWithCustomSphereCollider(TimesPrefix + "EventMachine", eventMachine, 3f, Vector3.zero));

            // ** Structure Events **

            // SuperFans
            var superFan = ((SuperFans)BBTimesManager.man.Get<RandomEvent>("Event_SuperFans")).superFanPre;
            var superFanDisplay = ObjectCreationExtensions.CreateSpriteBillboard(superFan.renderer.sprite, false).AddSpriteHolder(out var superFanRenderer, 0f);
            superFanDisplay.gameObject.ConvertToPrefab(true);
            superFanDisplay.name = "SuperFans_visual";

            ReplaceEditorBasicObject(TimesPrefix + "SuperFan", EditorInterface.AddObjectVisualWithCustomSphereCollider(TimesPrefix + "SuperFan", superFanDisplay.gameObject, 3f, Vector3.zero));

            // ** Global Structures **
            LevelStudioPlugin.Instance.structureTypes.Add(TimesPrefix + "OutsideBox", typeof(OutsideBoxLocation)); // It does nothing, so it's a good stub
        }

        /// <summary>
        /// Adds custom room default textures to the editor's dictionary.
        /// </summary>
        private static void InitializeDefaultTextures(Dictionary<string, TextureContainer> containers)
        {
            containers.Add("Bathroom", new TextureContainer("bathFloor", "bathWall", "bathCeil"));
            containers.Add("AbandonedRoom", new TextureContainer("BlueCarpet", "moldWall", "Ceiling"));
            containers.Add("BasketballArea", new TextureContainer("dirtyGrayFloor", "SaloonWall", "Ceiling"));
            containers.Add("ComputerRoom", new TextureContainer("computerRoomFloor", "computerRoomWall", "computerRoomCeiling"));
            containers.Add("DribbleRoom", new TextureContainer("dribbleRoomFloor", "SaloonWall", "Ceiling"));
            containers.Add("Forest", new TextureContainer("Grass", "forestWall", "None"));
            containers.Add("Kitchen", new TextureContainer("kitchenFloor", "Wall", "Ceiling"));
            containers.Add("FocusRoom", new TextureContainer("BlueCarpet", "Wall", "Ceiling"));
            containers.Add("SuperMystery", new TextureContainer("redCeil", "redWall", "redFloor"));
            containers.Add("ExibitionRoom", new TextureContainer("BlueCarpet", "Wall", "Ceiling"));
            containers.Add("SnowyPlayground", new TextureContainer("snowyPlaygroundFloor", "Fence", "None"));
            containers.Add("IceRink", new TextureContainer("IceRinkFloor", "IceRinkWall", "None"));
        }

        static void InitializeOtherTextures() =>
            LevelStudioPlugin.Instance.selectableTextures.AddRange(BBTimesManager.man.Get<List<string>>("TimesSchoolTextures"));


        /// <summary>
        /// Creates and adds all the placeable tools to the editor's toolbox.
        /// </summary>
        private static void InitializeTools(EditorMode mode, bool isVanillaCompliant)
        {
            // Add Item tools
            var itemsToAdd = ItemMetaStorage.Instance.GetAllFromMod(BBTimesManager.plug.Info)
                .Select(meta => meta.value)
                .Distinct() // No repeated items
                .ToArray();

            foreach (var itm in itemsToAdd)
            {
                string itmEnum = itm.itemType == Items.Points ? itm.name : itm.itemType.ToStringExtended();
                string key = TimesPrefix + itmEnum;
                Sprite icon = _editorAssetMan.Get<Sprite>("UI/ITM_" + itmEnum);
                EditorInterfaceModes.AddToolToCategory(mode, "items", new ItemTool(key, icon));
                // Debug.Log("{\"key\": \"Ed_Tool_item_" + key + "_Desc\", \"value\":\"[DESCRIPTION]\"},");
            }

            // Add NPC tools
            foreach (string npcName in allNpcs)
            {
                string key = TimesPrefix + npcName;
                EditorInterfaceModes.AddToolToCategory(mode, "npcs", new NPCTool(key, GetSprite($"UI/Npc_{npcName}", $"UI/npc_{npcName}")));

                // Adding PRI posters
                EditorInterfaceModes.AddToolToCategory(mode, "posters", new PosterTool(key + "_PRIPoster"));
            }

            // Add Room tools
            string[] roomNames =
            [
                "Bathroom", "AbandonedRoom", "ComputerRoom", "Forest", "Kitchen",
                "FocusRoom", "SuperMystery", "ExibitionRoom", "SnowyPlayground", "IceRink"
            ];
            foreach (var room in roomNames)
                EditorInterfaceModes.AddToolToCategory(mode, "rooms", new RoomTool(room, GetSprite($"UI/Floor_{room}", $"UI/floor_{room}")));

            // Add Object tools
            AddObjectTools(mode);

            // Add light tools
            EditorInterfaceModes.AddToolToCategory(mode, "lights", new LightTool("Times_HangingLongLight", GetSprite($"UI/Light_HangingLongLight", $"UI/light_HangingLongLight")));

            if (mode.id == "rooms") return; // Below here, there are things that are unnecessary for the rooms editor

            // Random Events
            foreach (var evName in allEvents)
                mode.availableRandomEvents.Add(TimesPrefix + evName);

            // Structures
            EditorInterfaceModes.AddToolToCategory(mode, "activities", new NotebookMachineTool(GetSprite("UI/Structure_NotebookMachine", "UI/structure_NotebookMachine")));

            EditorInterfaceModes.AddToolToCategory(mode, "structures", new StructureOnWallPlacementTool(TimesPrefix + "SuperFan", GetSprite($"UI/Structure_SuperFans", $"UI/structure_SuperFans")));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new StructureOnWallPlacementTool(TimesPrefix + "EventMachine", GetSprite($"UI/Structure_EventMachine", $"UI/structure_EventMachine"), useOppositeRotation: false));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new SecurityCameraTool(GetSprite($"UI/Structure_SecurityCamera", $"UI/structure_SecurityCamera")));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new TrapdoorTool(GetSprite($"UI/Structure_TrapdoorRng", $"UI/structure_TrapdoorRng"), false));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new TrapdoorTool(GetSprite($"UI/Structure_TrapdoorLink", $"UI/structure_TrapdoorLink"), true));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new ItemAlarmTool(GetSprite("UI/Structure_ItemAlarm", "UI/structure_ItemAlarm")));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new DuctPlaceTool(GetSprite("UI/Structure_Duct", "UI/structure_Duct")));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new DuctConnectTool(GetSprite("UI/Structure_DuctConnect", "UI/structure_DuctConnect")));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new SquisherTool(GetSprite("UI/Structure_Squisher", "UI/structure_Squisher")));
            EditorInterfaceModes.AddToolToCategory(mode, "structures", new SquisherWithButtonTool(GetSprite("UI/Structure_SquisherWithButton", "UI/structure_SquisherWithButton")));

            // Window tools
            EditorInterfaceModes.AddToolToCategory(mode, "doors", new WindowTool(TimesPrefix + "MetalWindow", GetSprite("UI/Window_MetalWindow", "UI/window_MetalWindow")));
            EditorInterfaceModes.AddToolToCategory(mode, "doors", new WindowTool(TimesPrefix + "ClassicWindow", GetSprite("UI/Window_ClassicWindow", "UI/window_ClassicWindow")));
            EditorInterfaceModes.AddToolToCategory(mode, "doors", new WindowTool(TimesPrefix + "RoundWindow", GetSprite("UI/Window_RoundWindow", "UI/window_RoundWindow")));

            // Door tools
            EditorInterfaceModes.AddToolToCategory(mode, "doors", new DoorTool(TimesPrefix + "SmallDoor", GetSprite("UI/Door_SmallDoor", "UI/door_SmallDoor")));

            // Outside Tool
            mode.globalRandomStructures.Add(new()
            {
                nameKey = $"Ed_GlobalStructure_{TimesPrefix}OutsideBox_Title",
                descKey = $"Ed_GlobalStructure_{TimesPrefix}OutsideBox_Desc",
                structureToSpawn = TimesPrefix + "OutsideBox",
                settingsPageType = typeof(OutsideBoxUIHandler),
                settingsPagePath = Structure_OutsideBox.GetJSONUIPath()
            });
        }
        /// <summary>
        /// Add object tools.
        /// </summary>
        private static void AddObjectTools(EditorMode mode)
        {
            // Key: object ID, Value: isRotatable
            var objectTools = new List<ObjectData>
            {
                new("bathStall", true, 5f), new("bathDoor", true, "doors"), new("sink", false), new("Toilet", false), new("FancyComputerTable", true), new("ComputerBillboard", false, 5f),
                new("Foresttree", false), new("Campfire", false),
                new("Beartrap", false), new("KitchenCabinet", false), new("JoeChef", true, 5f), new("FocusedStudent", false, 5f),
                new("ComputerTeleporter", false), new("DustShroom", false), new("SensitiveVase", false, 4.2f),
                new("TimesItemDescriptor", false, 5f), new("SnowyPlaygroundTree", false), new("SnowPile", false),
                new("Shovel_ForSnowPile", false, 0.1f), new("MysteryTresentMaker", false), new("MetalFence", true),
                new("SecretBread", false), new("TimesKitchenSteak", false), new("JoeSign", false)
            };
            for (int i = 1; i <= 8; i++) objectTools.Add(new("TimesGenericOutsideFlower_" + i, false));
            for (int i = 1; i <= 4; i++) objectTools.Add(new("TimesGenericCornerLamp_" + i, false));

            // SECRET ENDING OBJECTS
            // objectTools.Add(new("Times_SecretBaldi", true, 5f));
            // objectTools.Add(new("Times_InvisibleWall", true, 5f));
            // objectTools.Add(new("Times_CanBeDisabledInvisibleWall", true, 5f));
            // objectTools.Add(new("Times_ScrewingInvisibleWall", true, 5f));
            // objectTools.Add(new("Times_KeyLockedInvisibleWall", true, 5f));
            // objectTools.Add(new("Times_SecretGenerator", true, 5f));
            // objectTools.Add(new("Times_GeneratorCylinder", true, 5f));
            // objectTools.Add(new("Times_theYAYComputer", true, 5f));
            // objectTools.Add(new("Times_TrueLorePaper", true, 5f));
            // objectTools.Add(new("Times_GeneratorLever", true, 5f));
            // for (int i = 1; i <= 6; i++)
            // 	objectTools.Add(new($"Times_ContainedBaldi_F{i}", true, 5f));
            // EditorInterfaceModes.AddToolToCategory(mode, "objects", new StructureOnWallPlacementTool(TimesPrefix + "SecretButton", null));

            foreach (var pair in objectTools)
            {
                Sprite icon = GetSprite($"UI/Object_{pair.prefab}", $"UI/object_{pair.prefab}");
                if (pair.rotatable) // isRotatable
                    EditorInterfaceModes.AddToolToCategory(mode, pair.tool, new ObjectTool(pair.prefab, icon, pair.offset));
                else
                    EditorInterfaceModes.AddToolToCategory(mode, pair.tool, new ObjectToolNoRotation(pair.prefab, icon, pair.offset));
                // Debug.Log("{\"key\": \"Ed_Tool_object_" + pair.prefab + "_Name\", \"value\":\"" + pair.prefab.ToFriendlyName() + "\"},");
                // Debug.Log("{\"key\": \"Ed_Tool_object_" + pair.prefab + "_Desc\", \"value\":\"[DESCRIPTION]\"},");
            }

            // Special Bulk Object Tool
            // Uses "Ed_Tool_bulkobject_" prefix for these things
            EditorInterfaceModes.AddToolToCategory(mode, "objects", new BulkObjectTool("fullStall", GetSprite("UI/Helper_fullStall", "UI/helper_fullStall"), [
                    new("bathStall", new Vector3(-5f, 5f, 0f), new(0f, 90f)),
                    new("bathDoor", new Vector3(0f, 0f, 4f)),
                    new("bathStall", new Vector3(5f, 5f, 0f), new(0f, 90f))
                ]
            ));

        }

        private static Sprite GetSprite(string key1, string key2)
        {
            var spr = _editorAssetMan.ContainsKey(key1) ? _editorAssetMan.Get<Sprite>(key1) : _editorAssetMan.Get<Sprite>(key2);
            // Debug.Log($"Getting sprite: {(_editorAssetMan.ContainsKey(key1) ? key1 : key2)}");
            return spr;
        }


        private readonly struct ObjectData(string prefab, bool rotatable, string category = "objects")
        {
            public ObjectData(string prefab, bool rotatable, float offset) : this(prefab, rotatable) =>
                this.offset = offset;

            readonly public string prefab = prefab;
            readonly public bool rotatable = rotatable;
            readonly public float offset = 0f;
            readonly public string tool = category;
        }

        static GameObject AddStructureGenericVisual(string key, GameObject obj, params System.Type[] exceptions)
        {
            GameObject gameObject = EditorInterface.CloneToPrefabStripMonoBehaviors(obj, exceptions);
            gameObject.name = gameObject.name.Replace("_Stripped", "_GenericStructureVisual");
            EditorRendererContainer editorRendererContainer = gameObject.gameObject.AddComponent<EditorRendererContainer>();
            editorRendererContainer.AddRendererRange(gameObject.GetComponentsInChildren<Renderer>(), "none");
            gameObject.gameObject.AddComponent<EditorDeletableObject>().renderContainer = editorRendererContainer;
            gameObject.layer = 13;
            LevelStudioPlugin.Instance.genericStructureDisplays.Add(key, gameObject);
            return gameObject;
        }
    }

    [HarmonyPatch]
    [ConditionalPatchMod(Storage.guid_LevelStudio)]
    internal static class EditorLevelPatches
    {
        [HarmonyPatch(typeof(EditorLevelData), nameof(EditorLevelData.FinalizeCompile))]
        [HarmonyPostfix]
        static void TimesFinalizeCompile(BaldiLevel toFinalize)
        {
            for (int i = 0; i < toFinalize.levelSize.x; i++)
            {
                for (int j = 0; j < toFinalize.levelSize.y; j++)
                {
                    if (toFinalize.cells[i, j].roomId != 0 && toFinalize.rooms[toFinalize.cells[i, j].roomId - 1].type == "SuperMystery")
                    {
                        toFinalize.secretCells[i, j] = true;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(EditorLevelData), nameof(EditorLevelData.Compile))]
        [HarmonyPostfix]
        static void TimesCompileMysteryDoors(EditorLevelData __instance, ref BaldiLevel __result) // Copypaste from Compile to work with SuperMysteryRoom (seriously, make an event/callback or something for sanitizing these doors)
        {
            for (int m = 0; m < __instance.doors.Count; m++)
            {
                string prefab = __instance.doors[m].type;
                bool smartDoorPosition = __instance.GetSmartDoorPosition(__instance.doors[m].position, __instance.doors[m].direction, out var outPos, out var outDir);
                bool foundMysteryDoor = false;

                if (__instance.doors[m].type == "standard")
                {
                    if (__instance.RoomFromPos(outPos, forEditor: false).roomType == "SuperMystery")
                    {
                        foundMysteryDoor = true;
                        prefab = "mysterydoor";
                    }
                    else if (__instance.RoomFromPos(__instance.doors[m].position + __instance.doors[m].direction.ToIntVector2(), forEditor: false).roomType == "SuperMystery")
                    {
                        foundMysteryDoor = true;
                        outPos = __instance.doors[m].position + __instance.doors[m].direction.ToIntVector2();
                        outDir = __instance.doors[m].direction.GetOpposite();
                        prefab = "mysterydoor";
                    }
                }

                if (foundMysteryDoor)
                {
                    ByteVector2 outBytePos = outPos.ToByte();
                    __result.doors.RemoveAll(door => door.position == outBytePos && door.direction == (PlusDirection)outDir);
                    __result.doors.Add(new DoorInfo
                    {
                        prefab = prefab,
                        position = outBytePos,
                        direction = (PlusDirection)outDir,
                        roomId = __instance.GetCellSafe(outPos.x, outPos.z).roomId
                    });
                }
            }
        }
    }
}
