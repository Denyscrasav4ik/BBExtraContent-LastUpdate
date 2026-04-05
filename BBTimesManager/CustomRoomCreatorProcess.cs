using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBTimes.CompatibilityModule.EditorCompat;
using BBTimes.CustomComponents;
using BBTimes.CustomComponents.EventSpecificComponents.FrozenEvent;
using BBTimes.CustomContent.CustomItems;
using BBTimes.CustomContent.Events;
using BBTimes.CustomContent.Misc;
using BBTimes.CustomContent.NPCs;
using BBTimes.CustomContent.Objects;
using BBTimes.CustomContent.RoomFunctions;
using BBTimes.Extensions;
using BBTimes.Extensions.ObjectCreationExtensions;
using BBTimes.Manager.InternalClasses.LevelTypeWeights;
using BBTimes.ModPatches.GeneratorPatches;
using BBTimes.ModPatches.NpcPatches;
using BBTimes.Plugin;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.OBJImporter;
using MTM101BaldAPI.Registers;
using NewPlusDecorations;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Components;
using PixelInternalAPI.Extensions;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace BBTimes.Manager
{
    internal static partial class BBTimesManager
    {
        static void CreateCustomRooms()
        {
            // ==========================================================================================
            // ===================== 1. CREATE STRUCTURES FOR CUSTOM ROOMS ==============================
            // ==========================================================================================
            // This section is for creating all the GameObjects, prefabs, and assets that will be used in custom rooms.
            // This part remains largely the same, as it deals with asset creation, not loading infrastructure.

            // --- Variable Declarations (for reuse throughout the method) ---
            RoomGroupWithLevelType levelTypeGroup = null; // Used for registering room groups with level types
            RoomGroup group = null; // Used for registering room groups
            RoomSettings sets = null; // Used for registering room settings
            List<WeightedRoomAsset> room = null; // Used for holding room asset lists
            BasicObjectSwapData swap = null; // Used for object swaps in rooms
            Texture2D floorTex = null; // Used for floor textures
            int commonRoomWeight = 0; // Used for snowy playground/ice rink weights
            WeightedRoomAsset classWeightPre = null; // Used for classroom/faculty/office base
            int christmasIncreaseFactor = Storage.IsChristmas ? 100 : 0;
            List<WeightedRoomAssetWithLevelType> typedRoom = null; // Used for holding room assets converted to level types

            // --- Common references and textures ---
            var lightPre = man.Get<WeightedTransform>("WeightedLightTransform");
            var carpet = new WeightedTexture2D() { selection = GenericExtensions.FindResourceObjectByName<Texture2D>("Carpet"), weight = 100 };
            var ceiling = new WeightedTexture2D() { selection = GenericExtensions.FindResourceObjectByName<Texture2D>("CeilingNoLight"), weight = 100 };
            var saloonWall = new WeightedTexture2D() { selection = GenericExtensions.FindResourceObjectByName<Texture2D>("SaloonWall"), weight = 100 };
            var normWall = new WeightedTexture2D() { selection = GenericExtensions.FindResourceObjectByName<Texture2D>("Wall"), weight = 100 };
            var blackTexture = TextureExtensions.CreateSolidTexture(1, 1, Color.black); // It'll be stretched anyways lol
            var grass = man.Get<Texture2D>("Tex_Grass");
            var chalkBoard = GenericExtensions.FindResourceObjectByName<Texture2D>("chk_blank");
            var chalkboardFont = GenericExtensions.FindResourceObjectByName<TMP_FontAsset>("COMIC_24_Smooth_Pro");

            // --- Misc References that involves Categories ---
            var powerLeverRoomRef = GenericExtensions.FindResourceObject<Structure_PowerLever>();
            System.Action<RoomCategory>
            addAsPowerReceiver = powerLeverRoomRef.poweredRoomCategories.Add,
            addAsBreakerRoom = powerLeverRoomRef.breakerRoomCategories.Add,
            addAsLeverRoom = powerLeverRoomRef.leverRoomCategories.Add;

            // ------------------------------------------------------------------------------------------
            // -------------------------- BATHROOM STRUCTURES -------------------------------------------
            // ------------------------------------------------------------------------------------------

            // Bath Stall
            var bathStall = ObjectCreationExtension.CreateCube(AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "bathToiletWalls.png")), false);
            bathStall.gameObject.AddNavObstacle(new(1f, 10f, 1f));
            bathStall.name = "bathStall";
            bathStall.AddContainer(bathStall.GetComponent<MeshRenderer>());

            bathStall.transform.localScale = new(9.9f, 10f, 1f);
            bathStall.AddObjectToEditor();

            var bathSprites = TextureExtensions.LoadSpriteSheet(2, 1, 25f, GetRoomAsset("Bathroom", "BathDoor.png"));
            var bathDoor = new GameObject("bathDoor");
            bathDoor.gameObject.AddBoxCollider(Vector3.up * 5f, new(9.9f, 10f, 1f), true);
            bathDoor.layer = LayerStorage.iClickableLayer;

            var bathDoorRenderer = ObjectCreationExtensions.CreateSpriteBillboard(bathSprites[0], false);
            bathDoorRenderer.transform.SetParent(bathDoor.transform);
            bathDoorRenderer.transform.localPosition = Vector3.up * 5f;
            bathDoorRenderer.name = "BathDoorVisual";
            bathDoorRenderer.transform.localScale = new(0.976f, 1f, 1f);
            bathDoor.AddContainer(bathDoorRenderer);
            Object.Destroy(bathDoorRenderer.GetComponent<RendererContainer>());

            var bathDoorCollider = new GameObject("BathdoorCollider");
            bathDoorCollider.transform.SetParent(bathDoor.transform);
            bathDoorCollider.transform.localPosition = Vector3.zero;

            var collider = bathDoorCollider.gameObject.AddBoxCollider(Vector3.zero, new(9.9f, 10f, 0.7f), false);
            bathDoor.gameObject.AddObjectToEditor();

            var genDor = bathDoor.gameObject.AddComponent<GenericDoor>();
            genDor.audMan = bathDoor.gameObject.CreatePropagatedAudioManager(135f, 195f);
            genDor.audOpen = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Bathroom", "bathDoorOpen.wav")), "Sfx_Doors_StandardOpen", SoundType.Voice, Color.white);
            genDor.audClose = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Bathroom", "bathDoorShut.wav")), "Sfx_Doors_StandardShut", SoundType.Voice, Color.white);
            genDor.colliders = [collider];
            genDor.closed = bathSprites[0];
            genDor.open = bathSprites[1];
            genDor.renderer = bathDoorRenderer;
            genDor.noiseChance = 0.5f;

            // Bath Sink
            bathSprites = TextureExtensions.LoadSpriteSheet(2, 1, 41.5f, GetRoomAsset("Bathroom", "sink.png"));
            var bathSink = ObjectCreationExtensions.CreateSpriteBillboard(bathSprites[0])
                .AddSpriteHolder(out var bathSinkRenderer, 2f, LayerStorage.iClickableLayer);
            bathSinkRenderer.name = "sink";
            bathSink.name = "sink";
            bathSink.gameObject.AddObjectToEditor();

            var bathSinkFunction = bathSink.gameObject.AddComponent<TimedFountain>();
            bathSinkFunction.renderer = bathSinkRenderer;
            bathSinkFunction.sprEnabled = bathSprites[0];
            bathSinkFunction.sprDisabled = bathSprites[1];
            bathSinkFunction.refillValue = 15f;

            bathSinkFunction.gameObject.layer = LayerStorage.iClickableLayer;
            bathSinkFunction.gameObject.AddBoxCollider(Vector3.zero, new(0.8f, 10f, 0.8f), true);
            bathSinkFunction.audMan = bathSinkFunction.gameObject.CreatePropagatedAudioManager(15f, 35f);
            bathSinkFunction.audSip = man.Get<SoundObject>("audSlurp");

            collider = new GameObject("sinkCollider").AddBoxCollider(Vector3.zero, new(0.6f, 10f, 0.6f), false);
            collider.gameObject.AddNavObstacle(new(1f, 10f, 1f));
            collider.gameObject.layer = LayerStorage.ignoreRaycast;
            collider.transform.SetParent(bathSink.transform);
            collider.transform.localPosition = Vector3.zero;


            // Toilet
            var toilet = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "toilet.png")), 35f)).AddSpriteHolder(out var toiletRenderer, 2f, LayerMask.NameToLayer("ClickableCollideable"));
            toiletRenderer.name = "toiletRenderer";
            toilet.name = "Toilet";
            toilet.gameObject.AddObjectToEditor();
            toilet.gameObject.AddBoxCollider(Vector3.up * 5f, new(0.8f, 5f, 0.8f), false);
            toilet.gameObject.AddNavObstacle(new(1.2f, 10f, 1.2f));
            var toiletComp = toilet.gameObject.AddComponent<Toilet>();
            toiletComp.audMan = toilet.gameObject.CreatePropagatedAudioManager(55f, 75f);
            toiletComp.audFlush = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Bathroom", "toilet.wav")), "Vfx_Toilet_Flush", SoundType.Effect, Color.white);


            // Light for Bathroom
            var bathLightPre = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "long_hanginglamp.png")), 50f))
                .AddSpriteHolder(out _, 8.98f).transform;
            bathLightPre.name = "hangingLongLight";
            bathLightPre.gameObject.ConvertToPrefab(true);
            LevelLoaderPlugin.Instance.lightTransforms.Add("Times_HangingLongLight", bathLightPre);

            // ------------------------------------------------------------------------------------------
            // -------------------------- COMPUTER ROOM STRUCTURES --------------------------------------
            // ------------------------------------------------------------------------------------------
            //Table
            var table = new GameObject("FancyComputerTable")
            {
                layer = LayerStorage.ignoreRaycast
            };

            table.AddBoxCollider(Vector3.zero, Vector3.one * 10f, false);
            table.AddNavObstacle(Vector3.one * 10f);

            var tableHead = ObjectCreationExtension.CreateCube(AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", "computerTableTex.png")), false);
            Object.Destroy(tableHead.GetComponent<Collider>());
            tableHead.transform.SetParent(table.transform);
            tableHead.transform.localPosition = Vector3.up * 3f;
            tableHead.transform.localScale = new(9.9f, 1f, 9.9f);

            var renderer = table.AddContainer(tableHead.GetComponent<MeshRenderer>());

            TableLegCreator(new(-4f, 1.25f, 4f));
            TableLegCreator(new(4f, 1.25f, -4f));
            TableLegCreator(new(4f, 1.25f, 4f));
            TableLegCreator(new(-4f, 1.25f, -4f));

            void TableLegCreator(Vector3 pos)
            {
                var machineWheel = ObjectCreationExtension.CreateCube(blackTexture, false);
                machineWheel.transform.SetParent(table.transform);
                machineWheel.transform.localPosition = pos;
                machineWheel.transform.localScale = new(1f, 2.5f, 1f);
                Object.Destroy(machineWheel.GetComponent<Collider>());
                renderer.renderers = renderer.renderers.AddToArray(machineWheel.GetComponent<MeshRenderer>());
            }

            table.AddObjectToEditor();

            // Computer

            var computer = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", GetAssetName("computer.png"))), 25f));
            computer.name = "ComputerBillboard";
            computer.gameObject.AddObjectToEditor();

            // Event machine
            Sprite[] sprs = TextureExtensions.LoadSpriteSheet(3, 1, 26f, GetRoomAsset("ComputerRoom", GetAssetName("eventMachine.png")));
            var machine = ObjectCreationExtensions.CreateSpriteBillboard(sprs[1], false);
            machine.gameObject.layer = 0;
            machine.name = "EventMachine";
            machine.gameObject.AddObjectToEditor(EditorIntegration.TimesPrefix + machine.name);
            var evMac = machine.gameObject.AddComponent<EventMachine>();
            evMac.spriteToChange = machine;
            evMac.sprNoEvents = machine.sprite;
            evMac.sprWorking = sprs[2];
            evMac.sprDead = sprs[0];
            // Audio Setup from event machine
            evMac.audBalAngry = [
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("ComputerRoom", "Bal_NoEvent1.wav")), "Vfx_BAL_NoEvent0_0", SoundType.Voice, Color.green),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("ComputerRoom", "Bal_NoEvent2.wav")), "Vfx_BAL_NoEvent1_0", SoundType.Voice, Color.green),
                ];

            evMac.audBalAngry[0].additionalKeys = [new() { key = "Vfx_BAL_NoEvent0_1", time = 2.556f }];
            evMac.audBalAngry[1].additionalKeys = [new() { key = "Vfx_BAL_NoEvent1_1", time = 1.841f }];

            machine.gameObject.AddBoxCollider(Vector3.forward * -1.05f, new(6f, 10f, 1f), true);
            man.Add($"editorPrefab_{machine.name}", machine.gameObject);

            // ComputerTeleporter
            sprs = TextureExtensions.LoadSpriteSheet(3, 2, 12.5f, GetRoomAsset("ComputerRoom", GetAssetName("ComputerTeleporter.png")));
            var machineComputer = ObjectCreationExtensions.CreateSpriteBillboard(sprs[5]).AddSpriteHolder(out machine, 5f, LayerStorage.ignoreRaycast);
            machine.name = "Sprite";

            var teleporter = machineComputer.gameObject.AddComponent<ComputerTeleporter>();
            teleporter.name = "ComputerTeleporter";

            teleporter.gameObject.AddObjectToEditor();
            teleporter.gameObject.AddBoxCollider(Vector3.up * 5f, new(8.25f, 10f, 8.25f), true);

            teleporter.animComp = teleporter.gameObject.AddComponent<AnimationComponent>();
            teleporter.animComp.renderers = [machine];
            teleporter.animComp.animation = [.. sprs.Take(5)];
            teleporter.animComp.speed = 11.5f;

            teleporter.sprEnabled = teleporter.animComp.animation;
            teleporter.sprDisabled = [machine.sprite];

            teleporter.audMan = teleporter.gameObject.CreatePropagatedAudioManager(15f, 50f);
            teleporter.loopingAudMan = teleporter.gameObject.CreatePropagatedAudioManager(15f, 50f);
            teleporter.audTeleport = man.Get<SoundObject>("teleportAud");
            teleporter.audLoop = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("ComputerRoom", GetAssetName("teleportationNoises.wav"))), "Vfx_CompTel_Working", SoundType.Voice, Color.white);

            // Item Descriptor
            var descriptorObj = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", GetAssetName("item_descriptor.png"))), 13f))
                .AddSpriteHolder(out var descriptorRenderer, -0.15f, LayerStorage.iClickableLayer);
            descriptorObj.name = "TimesItemDescriptor";
            descriptorRenderer.name = "TimesItemDescriptor_Renderer";

            descriptorObj.gameObject.AddObjectToEditor();

            descriptorObj.gameObject.AddBoxCollider(Vector3.zero, new(2.5f, 5f, 2.5f), true);
            var descriptor = descriptorObj.gameObject.AddComponent<ItemDescriptor>();
            descriptor.audMan = descriptorObj.gameObject.CreatePropagatedAudioManager(45f, 65f);
            descriptor.audTap = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("ComputerRoom", GetAssetName("descriptor_beep.wav"))), string.Empty, SoundType.Voice, Color.white);
            descriptor.audTap.subtitle = false;

            descriptor.text = new GameObject("DescriptorText", typeof(BillboardRotator)).AddComponent<TextMeshPro>();
            descriptor.text.transform.SetParent(descriptorObj.transform);
            descriptor.text.transform.localPosition = Vector3.up * 2f;
            descriptor.text.fontSize = 5.5f;
            descriptor.text.richText = true; // Allow html stuff... I hope this is the right setting
            descriptor.text.color = new(0.16796875f, 0.16796875f, 0.31640625f);
            descriptor.text.alignment = TextAlignmentOptions.Center;
            descriptor.text.wordSpacing = 5;
            descriptor.text.rectTransform.sizeDelta = new(8.15f, 5f);
            descriptor.text.text = Singleton<LocalizationManager>.Instance.GetLocalizedText("PST_ItemDescriptor_NoDescription");

            // ------------------------------------------------------------------------------------------
            // -------------------------- KITCHEN STRUCTURES --------------------------------------------
            // ------------------------------------------------------------------------------------------

            // Kitchen "table"
            var shelf = new GameObject("KitchenCabinet");
            shelf.gameObject.AddBoxCollider(Vector3.zero, new(10f, 10f, 10f), false);
            shelf.layer = LayerStorage.ignoreRaycast;
            var renderers = new Renderer[2];

            var shelfBody = ObjectCreationExtension.CreateCube(Object.Instantiate(man.Get<Texture2D>("plasticTexture")).ApplyLightLevel(-45f), false);
            shelfBody.transform.SetParent(shelf.transform);
            shelfBody.transform.localPosition = Vector3.up * 2.5f;
            shelfBody.transform.localScale = new(9.9f, 1f, 9.9f);
            Object.Destroy(shelfBody.GetComponent<Collider>());
            renderers[0] = shelfBody.GetComponent<MeshRenderer>();


            shelfBody = ObjectCreationExtension.CreateCube(man.Get<Texture2D>("plasticTexture"), false);
            shelfBody.transform.SetParent(shelf.transform);
            shelfBody.transform.localPosition = Vector3.up * 0.7f;
            shelfBody.transform.localScale = new(7.5f, 4f, 7.5f);
            Object.Destroy(shelfBody.GetComponent<Collider>());
            renderers[1] = shelfBody.GetComponent<MeshRenderer>();

            shelf.AddContainer(renderers);

            shelf.gameObject.AddObjectToEditor();

            // Joe

            var joe = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Kitchen", "JoeChef.png")), 29f)).AddSpriteHolder(out _, 0f, LayerStorage.iClickableLayer);
            joe.name = "JoeChef";
            joe.gameObject.AddBoxCollider(Vector3.zero, Vector3.one * 10f, true);
            joe.gameObject.AddObjectToEditor();

            var joeChef = joe.gameObject.AddComponent<JoeChef>();
            joeChef.audMan = joe.gameObject.CreatePropagatedAudioManager(175f, 356f);
            joeChef.kitchenAudMan = joe.gameObject.CreatePropagatedAudioManager(76f, 155f);
            joeChef.audWelcome = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Kitchen", "Joe_Welcome.wav")), "Vfx_Joe_Welcome", SoundType.Voice, Color.white);
            joeChef.audScream = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Kitchen", "Joe_Scream.wav")), "Vfx_Joe_Scream", SoundType.Voice, Color.white);
            joeChef.audKitchen = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Kitchen", "KitchenThing.wav")), string.Empty, SoundType.Voice, Color.white);
            joeChef.audKitchen.subtitle = false;

            // Joe's Sign
            var joeSign = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Kitchen", "JoeChefSign.png")), 15f)).AddSpriteHolder(out _, 0f, LayerStorage.ignoreRaycast);
            joeSign.name = "JoeSign";
            joeSign.gameObject.AddBoxCollider(Vector3.zero, Vector3.one, true);
            joeSign.gameObject.AddObjectToEditor();

            // ------------------------------------------------------------------------------------------
            // -------------------------- FOREST STRUCTURES ---------------------------------------------
            // ------------------------------------------------------------------------------------------

            // Tree
            var tree = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Forest", "forestTree.png")), 8f)).AddSpriteHolder(out _, 10.76f, 0);
            tree.gameObject.AddBoxCollider(Vector3.zero, new(0.8f, 10f, 0.8f), false);
            tree.gameObject.AddNavObstacle(new(1.9f, 10f, 1.9f));

            var treeRaycastBlock = new GameObject("TreeRaycastHitbox");
            treeRaycastBlock.AddBoxCollider(Vector3.zero, new(4f, 10f, 4f), false);
            treeRaycastBlock.layer = LayerStorage.blockRaycast;
            treeRaycastBlock.transform.SetParent(tree.transform);
            treeRaycastBlock.transform.localPosition = Vector3.zero;

            tree.name = "Foresttree";
            tree.gameObject.AddObjectToEditor();

            var treeEasterEgg = tree.DuplicatePrefab();
            ((SpriteRenderer)treeEasterEgg.GetComponent<RendererContainer>().renderers[0]).sprite = AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Forest", "forestTreeEasterEgg.png")), 8f);

            // Campfire
            var campFire = tree.DuplicatePrefab();
            ((SpriteRenderer)campFire.GetComponent<RendererContainer>().renderers[0]).sprite = AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("Forest", "FireStatic.png")), 27f);
            Object.Destroy(campFire.transform.Find("TreeRaycastHitbox").gameObject); // Not needed for the campfire

            campFire.transform.GetChild(0).localPosition = Vector3.up * 1.2f;
            campFire.name = "Campfire";

            var lgtSrc = campFire.gameObject.AddComponent<LightSourceObject>();
            lgtSrc.colorStrength = 5;
            lgtSrc.colorToLight = new(1f, 0.65f, 0f);

            var campFireAud = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Forest", "fire.wav")), string.Empty, SoundType.Voice, Color.white);
            campFireAud.subtitle = false;
            campFire.gameObject.AddObjectToEditor();
            var audSource = campFire.gameObject.CreatePropagatedAudioManager(40f, 80f)
                .AddStartingAudiosToAudioManager(true, campFireAud);

            // BearTrap
            var trap = ObjectCreationExtensions.CreateSpriteBillboard(man.Get<Sprite[]>("Beartrap")[1]).AddSpriteHolder(out var trapRender, 1f, 0).gameObject.AddComponent<PersistentBearTrap>();
            trap.name = "Beartrap";

            trap.gameObject.AddObjectToEditor();
            trap.gameObject.AddBoxCollider(Vector3.zero, new(1.9f, 10f, 1.9f), true);
            trap.gameObject.AddNavObstacle(Vector3.zero, new(2.6f, 10f, 2.6f));

            trap.audMan = trap.gameObject.CreatePropagatedAudioManager(75f, 105f);
            trap.audCatch = man.Get<SoundObject>("BeartrapCatch");
            trap.sprClosed = man.Get<Sprite[]>("Beartrap")[0];
            trap.sprOpen = trapRender.sprite;
            trap.renderer = trapRender;
            trap.gaugeSprite = (ItemMetaStorage.Instance.FindByEnumFromMod(EnumExtensions.GetFromExtendedName<Items>("Beartrap"), plug.Info).value.item as ITM_Beartrap).gaugeSprite;

            // ------------------------------------------------------------------------------------------
            // -------------------------- SNOWY PLAYGROUND STRUCTURES -----------------------------------
            // ------------------------------------------------------------------------------------------

            // Snowy Tree
            var snowyTree = Object.Instantiate(GenericExtensions.FindResourceObjectByName<RendererContainer>("TreeCG"));
            snowyTree.name = "SnowyPlaygroundTree";
            snowyTree.gameObject.AddObjectToEditor();
            var snowyTreeRenderer = (MeshRenderer)snowyTree.renderers[0];
            snowyTreeRenderer.material.SetTexture("_MainTex", AssetLoader.TextureFromFile(GetRoomAsset("SnowyPlayground", "snowyTreeCG.png")));

            // Snow Pile
            var snowPile = new OBJLoader().Load(
                GetRoomAsset("SnowyPlayground", "SnowPile.obj"),
                GetRoomAsset("SnowyPlayground", "SnowPile.mtl"),
                ObjectCreationExtension.defaultMaterial);
            snowPile.name = "SnowPile";
            SetupObjCollisionAndScale(snowPile, new(9.7f, 10f, 9.7f), 0.2f, addMeshCollider: false);

            snowPile.AddObjectToEditor();

            var snowPileComp = snowPile.AddComponent<SnowPile>();
            snowPileComp.collider = snowPile.AddBoxCollider(Vector3.up * 7.5f, new(7.5f, 10f, 7.5f), false);
            snowPileComp.mainRenderer = snowPile.transform.GetChild(0).gameObject; // The renderer

            snowPileComp.audMan = snowPile.CreatePropagatedAudioManager(55f, 75f);
            snowPileComp.audPop = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("SnowyPlayground", "lowPop.wav")), "Sfx_Effects_Pop", SoundType.Effect, Color.white);

            var system = GameExtensions.GetNewParticleSystem();
            system.gameObject.name = "snowPileParticles";
            system.transform.SetParent(snowPileComp.transform);
            system.transform.localPosition = Vector3.up * 1.25f;
            system.GetComponent<ParticleSystemRenderer>().material = new Material(ObjectCreationExtension.defaultDustMaterial) { mainTexture = AssetLoader.TextureFromFile(GetRoomAsset("SnowyPlayground", "blueSnowBall.png")) };

            var main = system.main;
            main.gravityModifierMultiplier = 0.95f;
            main.startLifetimeMultiplier = 9f;
            main.startSpeedMultiplier = 4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = new(0.5f, 2f);

            var emission = system.emission;
            emission.enabled = false;

            var vel = system.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new(-15f, 15f);
            vel.y = new(25f, 35f);
            vel.z = new(-15f, 15f);

            snowPileComp.snowPopParts = system;

            // ********** Shovel Code ************

            var shovel = new OBJLoader().Load(
                GetRoomAsset("SnowyPlayground", "shovel.obj"),
                GetRoomAsset("SnowyPlayground", "shovel.mtl"),
                ObjectCreationExtension.defaultMaterial
                );
            shovel.name = "Shovel_ForSnowPile";
            SetupObjCollisionAndScale(shovel, default, 0.2f, false, false);

            shovel.AddObjectToEditor();

            var snowShovel = shovel.AddComponent<SnowShovel>();
            snowShovel.normalRender = snowShovel.transform.GetChild(0).gameObject;
            snowShovel.clickableCollision = shovel.AddBoxCollider(Vector3.up * 5f, new(5f, 10f, 2.5f), true);

            snowShovel.holdRender = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromFile(GetRoomAsset("SnowyPlayground", "shovelRender.png"), Vector2.one * 0.5f, 23f)).gameObject;
            snowShovel.holdRender.transform.SetParent(snowShovel.transform);
            snowShovel.holdRender.transform.localPosition = Vector3.zero;
            snowShovel.holdRender.name = "SnowShovel2DRender";
            snowShovel.holdRender.gameObject.SetActive(false);

            var snowShovel_timerOffsetObj = new GameObject("Timer_OffsetHolder");
            snowShovel_timerOffsetObj.transform.SetParent(snowShovel.transform);
            snowShovel_timerOffsetObj.transform.localPosition = Vector3.up * 5f;

            snowShovel.timer = Object.Instantiate(man.Get<TextMeshPro>("genericTextMesh"));
            snowShovel.timer.name = "Timer";
            snowShovel.timer.transform.SetParent(snowShovel_timerOffsetObj.transform);
            snowShovel.timer.color = Color.white;
            snowShovel.timer.gameObject.SetActive(false);

            // ******** MTM101 machine ********

            var mtm101 = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromFile(GetRoomAsset("SnowyPlayground", "mtm101.png"), Vector2.one * 0.5f, 23f)).AddSpriteHolder(out var mtm101renderer, 5.5f, LayerStorage.iClickableLayer);
            mtm101.name = "MysteryTresentMaker";
            mtm101renderer.name = "MysteryTresentMakerRenderer";
            mtm101.gameObject.AddBoxCollider(Vector3.up * 5f, new(3.5f, 10f, 3.5f), true);
            mtm101.gameObject.AddObjectToEditor();

            var mtm = mtm101.gameObject.AddComponent<MysteryTresentMaker>();
            mtm.audMan = mtm101.gameObject.CreatePropagatedAudioManager(66f, 100f);
            mtm.audInsert = GenericExtensions.FindResourceObject<MathMachine>().audWin;

            var treSprites = TextureExtensions.LoadSpriteSheet(4, 3, 24f, GetRoomAsset("SnowyPlayground", "tresentSheet.png"));

            mtm.tresentPre = ObjectCreationExtensions.CreateSpriteBillboard(treSprites[0]).AddSpriteHolder(out var tresentRender, 1.5f, LayerStorage.standardEntities)
                .gameObject.SetAsPrefab(true).AddComponent<Tresent>();
            mtm.tresentPre.name = "Tresent";
            tresentRender.name = "TresentRenderer";
            var tresentRenderbase = tresentRender.AddSpriteHolder(out _, 2.7f).transform;
            tresentRenderbase.SetParent(mtm.tresentPre.transform);
            tresentRenderbase.localPosition = Vector3.down * 5f;

            mtm.tresentPre.renderer = tresentRender;
            mtm.tresentPre.sprsPrepareExplosion = [.. treSprites.Take(9)];
            mtm.tresentPre.sprsExploding = [.. treSprites.Skip(9).Take(3)];

            mtm.tresentPre.bonkerPre = (ITM_Hammer)man.Get<ItemObject>("Item_Hammer").item;

            mtm.tresentPre.audMan = mtm.tresentPre.gameObject.CreatePropagatedAudioManager(35f, 65f);
            mtm.tresentPre.audThrow = man.Get<SoundObject>("audGenericThrow");
            mtm.tresentPre.audExplode = man.Get<SoundObject>("audPop");

            mtm.tresentPre.audPrepExplode = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("SnowyPlayground", "tresent_prepBreak.wav")), string.Empty, SoundType.Effect, Color.white);
            mtm.tresentPre.audPrepExplode.subtitle = false;

            mtm.tresentPre.text = Object.Instantiate(man.Get<TextMeshPro>("genericTextMesh"), mtm.tresentPre.transform);
            mtm.tresentPre.text.name = "TresentText";

            mtm.tresentPre.text.color = Color.white;
            mtm.tresentPre.text.fontSize = 7f;
            mtm.tresentPre.text.gameObject.SetActive(false);

            mtm.tresentPre.entity = mtm.tresentPre.gameObject.CreateEntity(1f, rendererBase: tresentRenderbase);

            // Tresent particles
            mtm.tresentPre.confettiParts = GameExtensions.GetNewParticleSystem();
            mtm.tresentPre.confettiParts.name = "TresentConfetti";
            mtm.tresentPre.confettiParts.transform.SetParent(tresentRenderbase);
            mtm.tresentPre.confettiParts.transform.localPosition = Vector3.zero;
            mtm.tresentPre.confettiParts.GetComponent<ParticleSystemRenderer>().material = new Material(ObjectCreationExtension.defaultDustMaterial) { mainTexture = AssetLoader.TextureFromFile(GetRoomAsset("SnowyPlayground", "numberFettis.png")) };

            main = mtm.tresentPre.confettiParts.main;
            main.gravityModifierMultiplier = 0.35f;
            main.startLifetimeMultiplier = 12f;
            main.startSpeedMultiplier = 2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = new(0.8f, 1.5f);

            emission = mtm.tresentPre.confettiParts.emission;
            emission.enabled = false;

            vel = mtm.tresentPre.confettiParts.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new(-12f, 12f);
            vel.y = new(6f, 14f);
            vel.z = new(-12f, 12f);

            var an = mtm.tresentPre.confettiParts.textureSheetAnimation;
            an.enabled = true;
            an.numTilesX = 4;
            an.numTilesY = 4;
            an.animation = ParticleSystemAnimationType.WholeSheet;
            an.fps = 0f;
            an.timeMode = ParticleSystemAnimationTimeMode.FPS;
            an.cycleCount = 1;
            an.startFrame = new(0, 15); // 2x2

            var col = mtm.tresentPre.confettiParts.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.enableDynamicColliders = false;

            // Tresent addition to the floors
            floorDatas[F2].WeightedNaturalObjects.Add(new(mtm101.gameObject, 25 + christmasIncreaseFactor));
            floorDatas[F3].WeightedNaturalObjects.Add(new(mtm101.gameObject, 30 + christmasIncreaseFactor));
            floorDatas[F4].WeightedNaturalObjects.Add(new(mtm101.gameObject, 45 + christmasIncreaseFactor));
            floorDatas[F5].WeightedNaturalObjects.Add(new(mtm101.gameObject, 60 + christmasIncreaseFactor));
            floorDatas[END].WeightedNaturalObjects.Add(new(mtm101.gameObject, 15 + christmasIncreaseFactor));

            // ------------------------------------------------------------------------------------------
            // -------------------------- ICE RINK ROOM STRUCTURES --------------------------------------
            // ------------------------------------------------------------------------------------------

            // ** Metal Fence
            var metalFence = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("IceRink", "metalFence.png")), 25f), false)
                .AddSpriteHolder(out var metalFenceRenderer, Vector3.forward * 5f + Vector3.up * 5f, LayerStorage.ignoreRaycast);
            metalFence.name = "MetalFence";
            metalFence.gameObject.AddBoxCollider(metalFenceRenderer.transform.localPosition, new(10f, 5f, 0.95f), false);
            metalFence.gameObject.AddNavObstacle(metalFenceRenderer.transform.localPosition + (Vector3.down * 3f), new(12f, 10f, 1.75f));
            metalFenceRenderer.name = "Sprite";
            metalFence.gameObject.AddObjectToEditor();
            metalFence.transform.localScale = new(0.977f, 1f, 1f);

            // ** Ice Water
            var iceWater = ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("IceRink", "IceRinkWater.png")), 20f), false)
                .AddSpriteHolder(out var iceWaterRenderer, Vector3.up * 0.1f, LayerStorage.ignoreRaycast);
            iceWater.name = "iceWater";
            iceWater.gameObject.AddBoxCollider(Vector3.up * 5f, new(3.85f, 5f, 3.85f), true);
            var iceWaterComp = iceWater.gameObject.AddComponent<IceRinkWater>();

            iceWaterRenderer.name = "Sprite";
            iceWaterRenderer.gameObject.layer = 0;
            iceWaterRenderer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            iceWaterRenderer.transform.localScale = Vector3.one * 1.56f;
            iceWater.gameObject.ConvertToPrefab(true);

            // ------------------------------------------------------------------------------------------
            // -------------------------- FOCUS ROOM STRUCTURES -----------------------------------------
            // ------------------------------------------------------------------------------------------

            // Get the voicelines for Female and Male

            SoundObject[] maleVoices = [
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_M_Please.wav")), "Vfx_FocusStd_Disturbed1", SoundType.Voice, Color.white),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_M_Please2.wav")), "Vfx_FocusStd_Disturbed2", SoundType.Voice, Color.white),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_M_Scream.wav")), "Vfx_FocusStd_Scream1", SoundType.Voice, Color.white)
            ];

            SoundObject[] femaleVoices = [
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_F_Please.wav")), "Vfx_FocusStd_Disturbed1", SoundType.Voice, Color.white),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_F_Please2.wav")), "Vfx_FocusStd_Disturbed2", SoundType.Voice, Color.white),
                ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_F_Scream.wav")), "Vfx_FocusStd_Scream1", SoundType.Voice, Color.white)
            ];

            var student = ObjectCreationExtensions.CreateSpriteBillboard(null);
            student.name = "FocusedStudent";
            student.gameObject.AddObjectToEditor();

            var focusedStudent = student.gameObject.AddComponent<FocusedStudent>();

            focusedStudent.audMan = student.gameObject.CreatePropagatedAudioManager(35f, 100f);
            focusedStudent.audMan.overrideSubtitleColor = true;
            focusedStudent.renderer = student;
            focusedStudent.audBookNoise = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_PageTurn.wav")), "Vfx_PageTurn", SoundType.Voice, Color.white);

            Sprite focusedStudent_smallBubble = GenericExtensions.FindResourceObjectByName<Sprite>("Cloud_Sprite_Small"),
                focusedStudent_bigBubble = AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(GetRoomAsset("FocusRoom", "FocusedStudent_ThoughtCloud.png")), 15f);

            var focusedStudent_bubbleHolder = new GameObject("FocusedStudent_BubbleHolder").AddComponent<BillboardUpdater>();
            focusedStudent_bubbleHolder.transform.SetParent(focusedStudent.transform, false);
            focusedStudent_bubbleHolder.transform.localPosition = Vector3.zero;

            focusedStudent.readingIndicatorRenderers = [
                FocusedStudent_AddThoughtBubble(true, new(1.5f, 1.25f)),
                FocusedStudent_AddThoughtBubble(false, new(3.07f, 3.15f)),
            ];


            FocusedStudent.appearanceSet = [
                FocusedStudent_GetNewAppearance(1, true, new(0f, 0.65f, 0f)), // The OG
				FocusedStudent_GetNewAppearance(2, true, new Color32(198, 196, 9, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(3, true, new Color32(189, 16, 16, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(4, true, new Color32(16, 142, 164, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(5, true, new Color32(179, 46, 173, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(6, true, new Color32(53, 91, 215, byte.MaxValue)),

                FocusedStudent_GetNewAppearance(1, false, new(0f, 0.65f, 0f)), // The Female OG lol
				FocusedStudent_GetNewAppearance(2, false, new Color32(198, 196, 9, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(3, false, new Color32(189, 16, 16, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(4, false, new Color32(16, 142, 164, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(5, false, new Color32(179, 46, 173, byte.MaxValue)),
                FocusedStudent_GetNewAppearance(6, false, new Color32(53, 91, 215, byte.MaxValue)),
            ];

            student.sprite = FocusedStudent.appearanceSet[0].Reading;

            FocusedStudent.Appearances FocusedStudent_GetNewAppearance(int variant, bool isMale, Color subtitleColor)
            {
                char genderLetter = isMale ? 'M' : 'F';
                var sprites = TextureExtensions.LoadSpriteSheet(3, 1, 25f, GetRoomAsset("FocusRoom", $"FocusStd_{genderLetter}{variant}.png"));
                SoundObject[] soundSet = isMale ? maleVoices : femaleVoices;

                var appearance = new FocusedStudent.Appearances
                {
                    Reading = sprites[0],
                    Speaking = sprites[1],
                    Screaming = sprites[2],
                    audAskSilence = soundSet[0],
                    audAskSilence2 = soundSet[1],
                    audDisturbed = soundSet[2],
                    subtitleColor = subtitleColor
                };
                return appearance;
            }

            SpriteRenderer FocusedStudent_AddThoughtBubble(bool smallBubble, Vector2 offset)
            {
                var newBubble = ObjectCreationExtensions.CreateSpriteBillboard(smallBubble ? focusedStudent_smallBubble : focusedStudent_bigBubble);
                newBubble.enabled = false;
                newBubble.transform.SetParent(focusedStudent_bubbleHolder.transform);
                newBubble.transform.localPosition = offset;

                return newBubble;
            }

            // ------------------------------------------------------------------------------------------
            // -------------------------- ART ROOM STRUCTURES -------------------------------------------
            // ------------------------------------------------------------------------------------------
            var vaseSprs = TextureExtensions.LoadSpriteSheet(2, 1, 17f, GetRoomAsset("ExibitionRoom", "Vase.png"));
            var vase = ObjectCreationExtensions.CreateSpriteBillboard(vaseSprs[0]).AddSpriteHolder(out var vaseRenderer, 0f, LayerStorage.ignoreRaycast);
            vase.gameObject.AddBoxCollider(Vector3.zero, new(4.5f, 5f, 4.5f), true);
            vase.gameObject.AddNavObstacle(new(6.5f, 5f, 6.5f));
            vase.name = "SensitiveVase";
            vaseRenderer.name = "VaseSprite";
            vase.gameObject.AddObjectToEditor();

            var vaseObj = vase.gameObject.AddComponent<Vase>();
            vaseObj.audBreak = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("ExibitionRoom", "break.wav")), "Vfx_Vase_Break", SoundType.Effect, Color.white);
            vaseObj.audMan = vase.gameObject.CreatePropagatedAudioManager(50f, 85f);
            vaseObj.renderer = vaseRenderer;
            vaseObj.sprBroken = vaseSprs[1];

            // ------------------------------------------------------------------------------------------
            // -------------------------- MISCELLANEOUS STRUCTURES --------------------------------------
            // ------------------------------------------------------------------------------------------
            // Corner Lamps
            List<WeightedTransform> transformsList = [
                new() { selection =  ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(Path.Combine(MiscPath, TextureFolder, GetAssetName("lamp.png"))), 25f))
                .AddSpriteHolder(out _, 2.9f, LayerStorage.ignoreRaycast)
                .gameObject.SetAsPrefab(true)
                .AddBoxCollider(Vector3.zero, new Vector3(0.8f, 10f, 0.8f), false).transform, weight = 75 },

                new() { selection =  ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(Path.Combine(MiscPath, TextureFolder, GetAssetName("lightBulb.png"))), 65f))
                .AddSpriteHolder(out _, 5.1f, LayerStorage.ignoreRaycast)
                .gameObject.SetAsPrefab(true)
                .AddBoxCollider(Vector3.zero, new Vector3(0.8f, 10f, 0.8f), false).transform, weight = 35 },

                new() { selection =  ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(Path.Combine(MiscPath, TextureFolder, GetAssetName("lampShaped.png"))), 25f))
                .AddSpriteHolder(out _, 3.1f, LayerStorage.ignoreRaycast)
                .gameObject.SetAsPrefab(true)
                .AddBoxCollider(Vector3.zero, new Vector3(0.8f, 10f, 0.8f), false).transform, weight = 55 },

                new() { selection =  ObjectCreationExtensions.CreateSpriteBillboard(AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(Path.Combine(MiscPath, TextureFolder, GetAssetName("neatlySideLamp.png"))), 35f))
                .AddSpriteHolder(out _, 3.6f, LayerStorage.ignoreRaycast)
                .gameObject.SetAsPrefab(true)
                .AddBoxCollider(Vector3.zero, new Vector3(0.8f, 10f, 0.8f), false).transform, weight = 45 },
                ];

            for (int i = 0; i < transformsList.Count; i++)
            {
                transformsList[i].selection.name = "TimesGenericCornerLamp_" + (i + 1);
                transformsList[i].selection.gameObject.AddObjectToEditor();
            }

            man.Add("prefabs_cornerLamps", transformsList);

            // ==========================================================================================
            // ===================== 2. REGISTER ROOMS INTO THE GAME ===================================
            // ==========================================================================================
            // This section is for registering the created structures as rooms and adding them to the floor data.

            // ------------------------------------------------------------------------------------------
            // -------------------------- BATHROOM ROOM REGISTRATION ------------------------------------
            // ------------------------------------------------------------------------------------------
            // Bathrooms
            sets = RegisterRoom("Bathroom", new(0.85f, 0.85f, 0.85f, 1f),
                ObjectCreators.CreateDoorDataObject("BathDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "bathDoorOpened.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "bathDoorClosed.png"))));

            Superintendent.AddAllowedRoom(sets.category);

            room = GetAllAssets(GetRoomAsset("Bathroom"), 45, 25, mapBg: AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "MapBG_Bathroom.png")));
            var mirrorPoster = ObjectCreators.CreatePosterObject([AssetLoader.TextureFromFile(GetRoomAsset("Bathroom", "mirror.png"))]);
            mirrorPoster.name = "PST_Mirror";
            mirrorPoster.AddPosterToEditor();
            var fun = room[0].selection.AddRoomFunctionToContainer<PosterAsideFromObject>();
            fun.targetPrefabName = "sink";
            fun.posterPre = mirrorPoster;

            var cover = room[0].selection.AddRoomFunctionToContainer<CoverRoomFunction>();
            cover.hardCover = true;
            cover.coverage = CellCoverage.North | CellCoverage.East | CellCoverage.West | CellCoverage.South;

            room.ForEach(x =>
            {
                x.selection.posterChance = 0f;
                x.selection.lightPre = bathLightPre;
            });
            sets.container = room[0].selection.roomFunctionContainer;

            group = new RoomGroup()
            {
                stickToHallChance = 1f,
                minRooms = 0,
                maxRooms = 1,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(F1)],
                name = "Bathroom",
                light = [new() { selection = bathLightPre }],
            };

            floorDatas[F1].RoomAssets.Add(new(group));

            group = new RoomGroup()
            {
                stickToHallChance = 0.7f,
                minRooms = 1,
                maxRooms = 1,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(F2)],
                name = "Bathroom",
                light = [new() { selection = bathLightPre }]
            };

            floorDatas[F2].RoomAssets.Add(new(group));

            group = new RoomGroup()
            {
                stickToHallChance = 0.7f,
                minRooms = 1,
                maxRooms = 2,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(F3)],
                name = "Bathroom",
                light = [new() { selection = bathLightPre }]
            };

            floorDatas[F3].RoomAssets.Add(new(group));
            levelTypeGroup = new RoomGroupWithLevelType(group, LevelType.Maintenance, LevelType.Laboratory);
            floorDatas[F4].RoomAssets.Add(levelTypeGroup);
            floorDatas[F5].RoomAssets.Add(levelTypeGroup);
            group = new RoomGroup()
            {
                stickToHallChance = 0.45f,
                minRooms = 1,
                maxRooms = 3,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(END)],
                name = "Bathroom",
                light = [new() { selection = bathLightPre }]
            };
            floorDatas[END].RoomAssets.Add(new(group));

            // ------------------------------------------------------------------------------------------
            // -------------------------- ABANDONED ROOM REGISTRATION -----------------------------------
            // ------------------------------------------------------------------------------------------
            // Abandoned Room
            sets = RegisterRoom("AbandonedRoom", new(0.59765625f, 0.19921875f, 0f),
                ObjectCreators.CreateDoorDataObject("OldDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("AbandonedRoom", "oldDoorOpen.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("AbandonedRoom", "oldDoorClosed.png"))));

            room = GetAllAssets(GetRoomAsset("AbandonedRoom"), 250, 45, mapBg: AssetLoader.TextureFromFile(GetRoomAsset("AbandonedRoom", "MapBG_AbandonedRoom.png")));
            room[0].selection.AddRoomFunctionToContainer<ShowItemsInTheEnd>();
            var abandonedPoster = ObjectCreators.CreatePosterObject([AssetLoader.TextureFromFile(GetRoomAsset("AbandonedRoom", "abandonedRoomTut.png"))]);
            abandonedPoster.name = "PST_AbandonedRoomTut";
            abandonedPoster.AddPosterToEditor();
            room[0].selection.AddRoomFunctionToContainer<RandomPosterFunction>().posters = [abandonedPoster];
            room.ForEach(x => x.selection.posterChance = 0f);
            sets.container = room[0].selection.roomFunctionContainer;

            group = new RoomGroup()
            {
                stickToHallChance = 0.45f,
                minRooms = 1,
                maxRooms = 1,
                potentialRooms = [.. room],
                name = "AbandonedRoom",
                light = [lightPre],
                ceilingTexture = [ceiling],
                floorTexture = [carpet]
            };
            floorDatas[F3].RoomAssets.Add(new(group));

            AddCategoryForNPCToSpawn(sets.category, typeof(Phawillow));
            addAsBreakerRoom(sets.category);

            // ------------------------------------------------------------------------------------------
            // -------------------------- COMPUTER ROOM REGISTRATION ------------------------------------
            // ------------------------------------------------------------------------------------------
            // Computer Room

            sets = RegisterRoom("ComputerRoom", new(0f, 0f, 0.35f),
                ObjectCreators.CreateDoorDataObject("ComputerDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", "computerDoorOpened.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", "computerDoorClosed.png"))));


            room = GetAllAssets(GetRoomAsset("ComputerRoom"), 75, 50, mapBg: AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", "MapBG_Computer.png")));
            var computerPoster = ObjectCreators.CreatePosterObject([AssetLoader.TextureFromFile(GetRoomAsset("ComputerRoom", "ComputerPoster.png"))]);
            computerPoster.name = "PST_ComputerRoom";
            computerPoster.AddPosterToEditor();
            room[0].selection.AddRoomFunctionToContainer<RandomPosterFunction>().posters = [computerPoster];
            room[0].selection.AddRoomFunctionToContainer<EventMachineSpawner>().machinePre = evMac;

            sets.container = room[0].selection.roomFunctionContainer;

            group = new RoomGroup()
            {
                stickToHallChance = 1f,
                minRooms = 1,
                maxRooms = 2,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(F2)],
                name = "ComputerRoom",
                light = [lightPre],
            };

            floorDatas[F2].RoomAssets.Add(new(group));
            group = new RoomGroup()
            {
                stickToHallChance = 1f,
                minRooms = 2,
                maxRooms = 2,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(END)],
                name = "ComputerRoom",
                light = [lightPre]
            };

            floorDatas[END].RoomAssets.Add(new(group));

            group = new RoomGroup()
            {
                stickToHallChance = 1f,
                minRooms = 2,
                maxRooms = 4,
                potentialRooms = [.. room.FilterRoomAssetsByFloor(F3)],
                name = "ComputerRoom",
                light = [lightPre]
            };

            floorDatas[F3].RoomAssets.Add(new(group));
            levelTypeGroup = new(group, LevelType.Laboratory);
            floorDatas[F4].RoomAssets.Add(levelTypeGroup);
            floorDatas[F5].RoomAssets.Add(levelTypeGroup);

            addAsBreakerRoom(sets.category);
            addAsLeverRoom(sets.category);

            // ------------------------------------------------------------------------------------------
            // -------------------------- SWEEP'S CLOSET REGISTRATION -----------------------------------
            // ------------------------------------------------------------------------------------------
            // Sweep's Closet
            var sweepCloset = GenericExtensions.FindResourceObject<GottaSweep>().potentialRoomAssets[0].selection;

            room = GetAllAssets(GetRoomAsset("Closet"), sweepCloset.maxItemValue, 100, sweepCloset.roomFunctionContainer, autoSizeLimitControl: 5.75f, throwIfNoRoomFound: false);

            if (room.Count != 0)
            {
                room[0].selection.AddRoomFunctionToContainer<HighCeilingRoomFunction>().ceilingHeight = 1;
                var sweepPoster = ObjectCreators.CreatePosterObject([AssetLoader.TextureFromFile(GetRoomAsset("Closet", "sweepSad.png"))]);
                sweepPoster.name = "PST_SweepPoster";
                sweepPoster.AddPosterToEditor();
                room[0].selection.AddRoomFunctionToContainer<RandomPosterFunction>().posters = [sweepPoster];
            }

            room.ForEach(x =>
            {
                x.selection.posters = sweepCloset.posters;
                x.selection.posterChance = sweepCloset.posterChance;
                x.selection.windowChance = sweepCloset.windowChance;
                x.selection.windowObject = sweepCloset.windowObject;
                x.selection.ceilTex = sweepCloset.ceilTex;
                x.selection.wallTex = sweepCloset.wallTex;
                x.selection.florTex = sweepCloset.florTex;
                x.selection.lightPre = sweepCloset.lightPre;
            });

            AddAssetsToNpc<GottaSweep>(room);
            AddAssetsToNpc<ZeroPrize>([new() { selection = sweepCloset, weight = 100 }, .. room]);
            AddAssetsToNpc<CoolMop>([new() { selection = sweepCloset, weight = 100 }, .. room]);
            AddAssetsToNpc<VacuumCleaner>([new() { selection = sweepCloset, weight = 100 }, .. room]);

            // ------------------------------------------------------------------------------------------
            // -------------------------- DR REFLEX OFFICE REGISTRATION ---------------------------------
            // ------------------------------------------------------------------------------------------
            // Dr Reflex Office
            sweepCloset = GenericExtensions.FindResourceObject<DrReflex>().potentialRoomAssets[0].selection;
            room = GetAllAssets(GetRoomAsset("Reflex"), sweepCloset.maxItemValue, 100, sweepCloset.roomFunctionContainer, autoSizeLimitControl: 6.15f);

            room.ForEach(x =>
            {
                x.selection.posters = sweepCloset.posters;
                x.selection.posterChance = sweepCloset.posterChance;
                x.selection.windowChance = sweepCloset.windowChance;
                x.selection.windowObject = sweepCloset.windowObject;
                x.selection.ceilTex = sweepCloset.ceilTex;
                x.selection.wallTex = sweepCloset.wallTex;
                x.selection.florTex = sweepCloset.florTex;
                x.selection.lightPre = sweepCloset.lightPre;
            });

            AddAssetsToNpc<DrReflex>(room);

            // ------------------------------------------------------------------------------------------
            // -------------------------- KITCHEN ROOM REGISTRATION -------------------------------------
            // ------------------------------------------------------------------------------------------
            // Kitchen
            sets = RegisterRoom("Kitchen", new(0.59765625f, 0.796875f, 0.99609375f, 1f),
                ObjectCreators.CreateDoorDataObject("KitchenDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("Kitchen", "kitchenDoorOpened.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("Kitchen", "kitchenDoorClosed.png"))));


            room = GetAllAssets(GetRoomAsset("Kitchen"), 75, 35, mapBg: AssetLoader.TextureFromFile(GetRoomAsset("Kitchen", "MapBG_Kitchen.png")), autoSizeLimitControl: 7f);
            if (!plug.enableBigRooms.Value)
                RemoveBigRooms(room, 7f);

            var lunchObj = GenericExtensions.FindResourceObjectByName<RendererContainer>("Decor_Lunch").transform;
            BasicObjectSwapData[] swaps = [
                new()
                {
                    chance = 0.75f,
                    potentialReplacements = [new() { selection = MakeOffsettedCopy(DecorsPlugin.Get<GameObject>("editorPrefab_SaltAndHot").transform, Vector3.up * 1.25f), weight = 100 }],
                    prefabToSwap = lunchObj
                },
                new()
                {
                    chance = 0.1f,
                    potentialReplacements = [new() { selection = man.Get<GameObject>("editorPrefab_SecretBread").transform, weight = 25 }, new() { selection = man.Get<GameObject>("editorPrefab_TimesKitchenSteak").transform, weight = 100 }],
                    prefabToSwap = lunchObj
                }
                ];

            room.ForEach(x =>
            {
                x.selection.basicSwaps.AddRange(swaps);
                x.selection.lightPre = EmptyGameObject.transform;
            });

            var joeKitchenMusic = ObjectCreators.CreateSoundObject(AssetLoader.AudioClipFromFile(GetRoomAsset("Kitchen", "Mus_Joe.wav")), string.Empty, SoundType.Music, Color.white);
            joeKitchenMusic.subtitle = false;

            var roomBaseFunction = room[0].selection.AddRoomFunctionToContainer<RoomBaseFunction>();
            roomBaseFunction.roomBase = room[0].selection.roomFunctionContainer.transform;

            room[0].selection.roomFunctionContainer
            .gameObject
            .CreatePropagatedAudioManager(35f, 85f).AddStartingAudiosToAudioManager(true, joeKitchenMusic);

            group = new RoomGroup()
            {
                stickToHallChance = 1f,
                minRooms = 0,
                maxRooms = 1,
                potentialRooms = [.. room],
                name = "Kitchen",
                light = [new() { selection = EmptyGameObject.transform, weight = 100 }],
                ceilingTexture = [ceiling],
                wallTexture = [normWall],
            };

            AddGroupToAllFloors(group);

            // ------------------------------------------------------------------------------------------
            // -------------------------- SUPER MYSTERY ROOM REGISTRATION -------------------------------
            // ------------------------------------------------------------------------------------------
            // Super mystery room
            sets = RegisterRoom("SuperMystery", new(1f, 0.439f, 0f),
                ObjectCreators.CreateDoorDataObject("SuperMysteryDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("SuperMystery", "FakeMysteryRoom_Open.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("SuperMystery", "FakeMysteryRoom.png"))));

            Superintendent.AddAllowedRoom(sets.category);

            room = GetAllAssets(GetRoomAsset("SuperMystery"), 75, 50, secretRoom: true);
            room[0].selection.AddRoomFunctionToContainer<RuleFreeZone>();

            var exclamations = TextureExtensions.LoadSpriteSheet(4, 2, 25f, GetRoomAsset("SuperMystery", "exclamations.png"));
            var transforms = new Transform[exclamations.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                var exclBillboard = ObjectCreationExtensions.CreateSpriteBillboard(exclamations[i]).transform;
                exclBillboard.name = "TimesExclamation_" + (i + 1);
                exclBillboard.gameObject.AddObjectToEditor();
                transforms[i] = exclBillboard;
            }

            room[0].selection.AddRoomFunctionToContainer<EnvironmentObjectSpawner>().randomTransforms = transforms;

            sets.container = room[0].selection.roomFunctionContainer;

            room.ForEach(x => { x.selection.maxItemValue = 999; x.selection.posterChance = 0; });

            AddAssetsToEvent<SuperMysteryRoom>(room);

            // ************************* SPECIAL ROOMS BELOW ****************************

            // ------------------------------------------------------------------------------------------
            // -------------------------- FOREST SPECIAL ROOM REGISTRATION ------------------------------
            // ------------------------------------------------------------------------------------------
            // Forest Area
            sets = RegisterSpecialRoom("Forest", Color.cyan);

            room = GetAllAssets(GetRoomAsset("Forest"), 75, 1, mapBg: Storage.HasCrispyPlus ? AssetLoader.TextureFromFile(GetRoomAsset("Forest", "mapIcon_trees.png")) : null, squaredShape: true, keepTextures: true, autoSizeLimitControl: -1);
            //Swap for 99 trees
            swap = new BasicObjectSwapData() { chance = 0.01f, potentialReplacements = [new() { selection = treeEasterEgg.transform, weight = 100 }], prefabToSwap = tree.transform };
            floorTex = AssetLoader.TextureFromFile(GetRoomAsset("Forest", "treeWall.png"));
            AddTextureToEditor("forestWall", floorTex);

            room.ForEach(x =>
            {
                x.selection.basicSwaps.Add(swap);
                x.selection.keepTextures = true;
                x.selection.florTex = grass;
                x.selection.wallTex = floorTex;
                x.selection.ceilTex = ObjectCreationExtension.transparentTex;
            });


            room[0].selection.AddRoomFunctionToContainer<SpecialRoomSwingingDoorsBuilder>().swingDoorPre = man.Get<SwingDoor>("swingDoorPre");
            room[0].selection.AddRoomFunctionToContainer<RuleFreeZone>();
            room[0].selection.AddRoomFunctionToContainer<CoverRoomFunction>().coverage = (CellCoverage)~0; // Reverse of 0

            var highCeil = room[0].selection.AddRoomFunctionToContainer<HighCeilingRoomFunction>();
            highCeil.ceilingHeight = 2;
            highCeil.usesSingleCustomWall = true;
            var tex = TextureExtensions.CreateSolidTexture(1, 1, new(0.12156f, 0.12156f, 0.478431f));
            highCeil.customWallProximityToCeil = [tex];
            highCeil.customCeiling = tex;

            var ambience = room[0].selection.AddRoomFunctionToContainer<AmbienceRoomFunction>();
            ambience.source = room[0].selection.roomFunctionContainer.gameObject.CreateAudioSource(35, 125);
            ambience.source.volume = 0;
            ambience.source.clip = AssetLoader.AudioClipFromFile(GetRoomAsset("Forest", "Crickets.wav"));
            ambience.source.loop = true;

            var vignetteCanvas = ObjectCreationExtensions.CreateCanvas();
            vignetteCanvas.gameObject.SetActive(false);
            vignetteCanvas.transform.SetParent(room[0].selection.roomFunctionContainer.transform);
            var vignette = ObjectCreationExtensions.CreateImage(vignetteCanvas, AssetLoader.TextureFromFile(GetRoomAsset("Forest", "darkOverlay.png")));

            var vignetteFunc = room[0].selection.AddRoomFunctionToContainer<VignetteRoomFunction>();
            vignetteFunc.vignette = vignetteCanvas;
            vignetteFunc.fovModifier = -25f;

            sets.container = room[0].selection.roomFunctionContainer;

            typedRoom = ConvertRoomsToLevelTypeOnes(room, "Forest", LevelType.Schoolhouse);

            floorDatas[F3].SpecialRooms.AddRange(typedRoom.ConvertAssetWeights(60));

            // ------------------------------------------------------------------------------------------
            // -------------------------- SNOWY PLAYGROUND SPECIAL ROOM REGISTRATION --------------------
            // ------------------------------------------------------------------------------------------
            // SNOWY PLAYGROUND

            var playgroundRoomRef = GenericExtensions.FindResourceObjects<RoomAsset>().First(x => x.name.StartsWith("Playground"));
            var playgroundClonedRoomContainer = Object.Instantiate(playgroundRoomRef.roomFunctionContainer);
            playgroundClonedRoomContainer.name = "SnowPlayground_Container";
            playgroundClonedRoomContainer.gameObject.ConvertToPrefab(true);

            var snowFunc = playgroundClonedRoomContainer.gameObject.AddComponent<FallingParticlesFunction>();

            snowFunc.particleTexture = AssetLoader.TextureFromFile(GetRoomAsset("SnowyPlayground", "snowFlake.png"));

            playgroundClonedRoomContainer.AddFunction(snowFunc);

            var objCornerSpawn = playgroundClonedRoomContainer.gameObject.AddComponent<CornerObjectSpawner>();
            objCornerSpawn.randomObjs = [new() { selection = mtm.transform, weight = 100 }];

            playgroundClonedRoomContainer.AddFunction(objCornerSpawn);

            var slipFunc = playgroundClonedRoomContainer.gameObject.AddComponent<SlipperyMaterialFunction>();
            slipFunc.slipMatPre = man.Get<SlippingMaterial>("SlipperyMatPrefab").SafeDuplicatePrefab(true);
            ((SpriteRenderer)slipFunc.slipMatPre.GetComponent<RendererContainer>().renderers[0]).sprite = AssetLoader.SpriteFromFile(GetRoomAsset("SnowyPlayground", "icePatch.png"), Vector2.one * 0.5f, 22f);
            slipFunc.slipMatPre.force = 65f;
            slipFunc.slipMatPre.antiForceReduceFactor = 0.75f;
            slipFunc.slipMatPre.name = "SnowyIcePatch";

            slipFunc.minMax = new(3, 5);

            playgroundClonedRoomContainer.AddFunction(slipFunc);

            sets = RegisterSpecialRoom("SnowyPlayground", Color.cyan);

            commonRoomWeight = christmasIncreaseFactor + 75;

            room = GetAllAssets(GetRoomAsset("SnowyPlayground"), commonRoomWeight, 1, cont: playgroundClonedRoomContainer, mapBg: Storage.HasCrispyPlus ? AssetLoader.TextureFromFile(GetRoomAsset("SnowyPlayground", "mapIcon_snow.png")) : null, squaredShape: true, keepTextures: true, autoSizeLimitControl: -1);
            floorTex = man.Get<Texture2D>("Texture_SnowyGrass");
            AddTextureToEditor("snowyPlaygroundFloor", floorTex);

            room.ForEach(x =>
            {
                x.selection.keepTextures = true;
                x.selection.florTex = floorTex;
                x.selection.wallTex = playgroundRoomRef.wallTex;
                x.selection.ceilTex = playgroundRoomRef.ceilTex;
            });

            sets.container = playgroundClonedRoomContainer;

            typedRoom = ConvertRoomsToLevelTypeOnes(room, "Snowy Playground", LevelType.Schoolhouse);

            floorDatas[F1].SpecialRooms.AddRange(typedRoom);
            floorDatas[F2].SpecialRooms.AddRange(typedRoom.ConvertAssetWeights(Mathf.FloorToInt(commonRoomWeight * 0.85f)));
            floorDatas[END].SpecialRooms.AddRange(typedRoom);
            floorDatas[F3].SpecialRooms.AddRange(typedRoom.ConvertAssetWeights(Mathf.FloorToInt(commonRoomWeight * 0.65f)));

            // ------------------------------------------------------------------------------------------
            // -------------------------- ICE RINK SPECIAL ROOM REGISTRATION ----------------------------
            // ------------------------------------------------------------------------------------------
            // Ice Rink Room

            playgroundClonedRoomContainer = Object.Instantiate(playgroundRoomRef.roomFunctionContainer);
            playgroundClonedRoomContainer.name = "IceRinkRoom_Container";
            playgroundClonedRoomContainer.gameObject.ConvertToPrefab(true);

            snowFunc = playgroundClonedRoomContainer.gameObject.AddComponent<FallingParticlesFunction>();
            snowFunc.bounderiesOnly = true;

            playgroundClonedRoomContainer.AddFunction(snowFunc);
            playgroundClonedRoomContainer.AddFunction(playgroundClonedRoomContainer.gameObject.AddComponent<IceSlippingFunction>());

            var iceRinkFunc = playgroundClonedRoomContainer.gameObject.AddComponent<IceWaterFunction>();
            iceRinkFunc.waterPre = iceWaterComp;
            playgroundClonedRoomContainer.AddFunction(iceRinkFunc);

            sets = RegisterSpecialRoom("IceRink", Color.cyan);

            commonRoomWeight = christmasIncreaseFactor + 85;

            room = GetAllAssets(GetRoomAsset("IceRink"), commonRoomWeight, 1, cont: playgroundClonedRoomContainer, mapBg: Storage.HasCrispyPlus ? AssetLoader.TextureFromFile(GetRoomAsset("IceRink", "mapIcon_iceRink.png")) : null, squaredShape: true, keepTextures: true, autoSizeLimitControl: -1);
            floorTex = AssetLoader.TextureFromFile(GetRoomAsset("IceRink", "IceRinkFloor.png"));
            AddTextureToEditor("IceRinkFloor", floorTex);

            var iceRinkWall = AssetLoader.TextureFromFile(GetRoomAsset("IceRink", "IceRinkWall.png"));
            AddTextureToEditor("IceRinkWall", iceRinkWall);

            room.ForEach(x =>
            {
                x.selection.keepTextures = true;
                x.selection.florTex = floorTex;
                x.selection.wallTex = iceRinkWall;
                x.selection.ceilTex = playgroundRoomRef.ceilTex;
            });

            sets.container = playgroundClonedRoomContainer;

            typedRoom = ConvertRoomsToLevelTypeOnes(room, "Ice Rink Room", LevelType.Schoolhouse);

            floorDatas[F1].SpecialRooms.AddRange(typedRoom);
            floorDatas[F2].SpecialRooms.AddRange(typedRoom.ConvertAssetWeights(Mathf.FloorToInt(commonRoomWeight * 0.85f)));
            floorDatas[END].SpecialRooms.AddRange(typedRoom);
            floorDatas[F3].SpecialRooms.AddRange(typedRoom.ConvertAssetWeights(Mathf.FloorToInt(commonRoomWeight * 0.65f)));


            // ================================================ Base Game Room Variants ====================================================

            //Classrooms

            classWeightPre = FindRoomGroupOfNameWithNoActivity("Class"); // Notebooks-only

            room = GetAllAssets(GetRoomAsset("Class"), classWeightPre.selection.maxItemValue, classWeightPre.weight, classWeightPre.selection.offLimits, classWeightPre.selection.roomFunctionContainer, autoSizeLimitControl: 6.5f);
            if (!plug.enableBigRooms.Value)
                RemoveBigRooms(room, 6.56f);
            var fullClassCopy = new List<WeightedRoomAsset>(room);
            var assignClassWeightPreForClassrooms = new System.Action(() =>
                room.ForEach(x =>
                {
                    x.selection.posters = classWeightPre.selection.posters;
                    x.selection.posterChance = classWeightPre.selection.posterChance;
                    x.selection.windowChance = classWeightPre.selection.windowChance;
                    x.selection.windowObject = classWeightPre.selection.windowObject;
                    x.selection.lightPre = classWeightPre.selection.lightPre;
                    x.selection.keepTextures = false;
                    x.selection.basicSwaps = classWeightPre.selection.basicSwaps;
                    x.selection.roomFunctionContainer = classWeightPre.selection.roomFunctionContainer;
                }
            ));

            // F1 (Notebook) classrooms
            room.RemoveAll(room => room.selection.activity.prefab is not NoActivity); // No activity allowed!
            assignClassWeightPreForClassrooms();

            floorDatas[F1].Classrooms.AddRange(room.FilterRoomAssetsByFloor());
            room.Clear();

            // F2+ (Math Machines) classrooms
            classWeightPre = FindRoomGroupOfNameWithActivity<MathMachine>("Class"); // Math machine
            room.AddRange(fullClassCopy);
            room.RemoveAll(room => room.selection.activity.prefab is not MathMachine); // Only math machines allowed!
            assignClassWeightPreForClassrooms();

            floorDatas[END].Classrooms.AddRange(room);
            floorDatas[F2].Classrooms.AddRange(room);
            floorDatas[F3].Classrooms.AddRange(room);
            floorDatas[F4].Classrooms.AddRange(room);
            floorDatas[F5].Classrooms.AddRange(room);

            room.Clear();
            // F3+ (Match and other activities) classrooms
            // Don't exist layouts for these yet, but I'm adding the code here
            classWeightPre = FindRoomGroupOfNameWithActivity<MatchActivity>("Class"); // Match
            room.AddRange(fullClassCopy);
            room.RemoveAll(room => room.selection.activity.prefab is not MatchActivity); // Only matches allowed

            floorDatas[F3].Classrooms.AddRange(room);
            floorDatas[F4].Classrooms.AddRange(room);
            floorDatas[F5].Classrooms.AddRange(room);

            // Balloon buster thing
            room.Clear();

            classWeightPre = FindRoomGroupOfNameWithActivity<BalloonBuster>("Class"); // Balloon Buster
            room.AddRange(fullClassCopy);
            room.RemoveAll(room => room.selection.activity.prefab is not BalloonBuster); // Only busters allowed

            floorDatas[F3].Classrooms.AddRange(room);
            floorDatas[F4].Classrooms.AddRange(room);
            floorDatas[F5].Classrooms.AddRange(room);


            // ****** Focus Room (A classroom variant, but with a new npc) ******
            PosterObject[] wallClock = [man.Get<PosterObject>("WallClock")]; // Class variants don't actually

            classWeightPre = FindRoomGroupOfNameWithNoActivity("Class"); // Notebooks-only

            sets = RegisterRoom("FocusRoom", new(0f, 1f, 0.5f),
                ObjectCreators.CreateDoorDataObject("FocusRoomDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("FocusRoom", "Focus_Room_Door_Opened.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("FocusRoom", "Focus_Room_Door_Closed.png"))));

            Superintendent.AddAllowedRoom(sets.category);
            //CameraStand.allowedRoomsToSpawn.Add(sets.category);
            ChalkfacePatch.allowedClassroomCategories.Add(sets.category);
            WormholeRoomFunctionPatches.allowedClassRooms.Add(sets.category);

            room = GetAllAssets(GetRoomAsset("FocusRoom"), classWeightPre.selection.maxItemValue, classWeightPre.weight / 3, classWeightPre.selection.offLimits, keepTextures: false);
            GenerateActivityExplainingPoster(room[0].selection, "PST_CHK_FocusedStudentTitle", "PST_CHK_FocusedStudentDesc", "FocusedStudent");

            var redCouchprefab = Resources.FindObjectsOfTypeAll<RendererContainer>().First(x => x.name == "RedCouch");
            room.ForEach(foc => foc.selection.basicObjects.ForEach(basO => { if (basO.prefab.name == "Couch") basO.prefab = redCouchprefab.transform; }));

            RegisterFalseClass();
            addAsPowerReceiver(sets.category);

            // ****** Art Room *******
            sets = RegisterRoom("ExibitionRoom", new(0.54296875f, 0.18359375f, 0.95703125f),
                ObjectCreators.CreateDoorDataObject("ExibitionRoomDoor",
                AssetLoader.TextureFromFile(GetRoomAsset("ExibitionRoom", "ExibitClassStandard_Open.png")),
                AssetLoader.TextureFromFile(GetRoomAsset("ExibitionRoom", "ExibitClassStandard_Closed.png"))));

            Superintendent.AddAllowedRoom(sets.category);
            //CameraStand.allowedRoomsToSpawn.Add(sets.category);
            ChalkfacePatch.allowedClassroomCategories.Add(sets.category);
            WormholeRoomFunctionPatches.allowedClassRooms.Add(sets.category);

            room = GetAllAssets(GetRoomAsset("ExibitionRoom"), classWeightPre.selection.maxItemValue, classWeightPre.weight / 3, classWeightPre.selection.offLimits, keepTextures: false);
            GenerateActivityExplainingPoster(room[0].selection, "PST_CHK_VaseTitle", "PST_CHK_VaseDesc", "SensibleVases");

            RegisterFalseClass();
            addAsPowerReceiver(sets.category);

            void RegisterFalseClass()
            {
                if (!plug.enableBigRooms.Value)
                    RemoveBigRooms(room, 7.85f);

                if (room.Count != 0)
                    room[0].selection.AddRoomFunctionToContainer<RandomPosterFunction>().posters = wallClock;


                room.ForEach(x =>
                {
                    x.selection.name.Replace("Class", sets.category.ToString());
                    x.selection.doorMats = sets.doorMat;
                    x.selection.category = sets.category;
                    x.selection.color = sets.color;
                    x.selection.wallTex = classWeightPre.selection.wallTex;
                    x.selection.florTex = classWeightPre.selection.florTex;
                    x.selection.ceilTex = classWeightPre.selection.ceilTex;
                    x.selection.posters = classWeightPre.selection.posters;
                    x.selection.posterChance = classWeightPre.selection.posterChance;
                    x.selection.windowChance = classWeightPre.selection.windowChance;
                    x.selection.windowObject = classWeightPre.selection.windowObject;
                    x.selection.mapMaterial = classWeightPre.selection.mapMaterial;
                    x.selection.lightPre = classWeightPre.selection.lightPre;
                    x.selection.basicSwaps = classWeightPre.selection.basicSwaps;
                });

                floorDatas[END].Classrooms.AddRange(room);
                floorDatas[F2].Classrooms.AddRange(room);
                floorDatas[F3].Classrooms.AddRange(room);
                floorDatas[F4].Classrooms.AddRange(room);
                floorDatas[F5].Classrooms.AddRange(room);
            }

            static void GenerateActivityExplainingPoster(RoomAsset asset, string nameKey, string descKey, string activityName)
            {
                var posterClone = Object.Instantiate(GenericExtensions.FindResourceObjectByName<PosterObject>("Chk_Act_MathMachine"));
                posterClone.textData[0].textKey = nameKey;
                posterClone.textData[1].textKey = descKey;
                posterClone.name = $"Chk_Act_{activityName}";

                posterClone.AddPosterToEditor();

                asset.AddRoomFunctionToContainer<ChalkboardBuilderFunction>().chalkBoards = [new() { selection = posterClone, weight = 100 }];
            }

            //Faculties

            classWeightPre = FindRoomGroupOfName("Faculty");
            room = GetAllAssets(GetRoomAsset("Faculty"), classWeightPre.selection.maxItemValue, classWeightPre.weight, classWeightPre.selection.offLimits, classWeightPre.selection.roomFunctionContainer, keepTextures: false, autoSizeLimitControl: 7f);

            if (!plug.enableBigRooms.Value)
                RemoveBigRooms(room, 8f);

            room.ForEach(x =>
            {
                x.selection.posters = classWeightPre.selection.posters;
                x.selection.posterChance = classWeightPre.selection.posterChance;
                x.selection.windowChance = classWeightPre.selection.windowChance;
                x.selection.windowObject = classWeightPre.selection.windowObject;
                x.selection.lightPre = classWeightPre.selection.lightPre;
                x.selection.basicSwaps = classWeightPre.selection.basicSwaps;
            });

            foreach (var floorData in floorDatas)
                floorData.Value.Faculties.AddRange(room.FilterRoomAssetsByFloor(floorData.Key));

            //Offices
            classWeightPre = FindRoomGroupOfName("Office");
            room = GetAllAssets(GetRoomAsset("Office"), classWeightPre.selection.maxItemValue, classWeightPre.weight, classWeightPre.selection.offLimits, classWeightPre.selection.roomFunctionContainer, keepTextures: false, autoSizeLimitControl: 7.5f);
            if (!plug.enableBigRooms.Value)
                RemoveBigRooms(room, 7.65f);

            var globePrefab = GenericExtensions.FindResourceObjectByName<RendererContainer>("Decor_Globe").transform;

            classWeightPre.selection.basicSwaps.Add(new()
            {
                chance = 0.75f,
                potentialReplacements = [
                            new() { selection = MakeOffsettedCopy(DecorsPlugin.Get<GameObject>("editorPrefab_SmallPottedPlant").transform, Vector3.up * 1.01f) },
                            new() { selection = MakeOffsettedCopy(DecorsPlugin.Get<GameObject>("editorPrefab_TableLightLamp").transform, Vector3.up * 2.16f) },
                            new() { selection = MakeOffsettedCopy(DecorsPlugin.Get<GameObject>("editorPrefab_FancyOfficeLamp").transform, Vector3.up * 1.26f) },
                            new() { selection = MakeOffsettedCopy(DecorsPlugin.Get<GameObject>("editorPrefab_TheRulesBook").transform, Vector3.up * 1.56f) }
                            ],
                prefabToSwap = globePrefab
            });

            room.ForEach(x =>
            {
                x.selection.posters = classWeightPre.selection.posters;
                x.selection.posterChance = classWeightPre.selection.posterChance;
                x.selection.windowChance = classWeightPre.selection.windowChance;
                x.selection.windowObject = classWeightPre.selection.windowObject;
                x.selection.lightPre = classWeightPre.selection.lightPre;
                x.selection.basicSwaps = classWeightPre.selection.basicSwaps;
            });

            foreach (var floorData in floorDatas)
                floorData.Value.Offices.AddRange(room.FilterRoomAssetsByFloor(floorData.Key));

            classWeightPre = Resources.FindObjectsOfTypeAll<LevelObject>().First(x => x.potentialPostPlotSpecialHalls.Length != 0).potentialPostPlotSpecialHalls[0];

            room = GetAllAssets(GetRoomAsset("AfterHalls"), classWeightPre.selection.maxItemValue, classWeightPre.weight, classWeightPre.selection.offLimits, classWeightPre.selection.roomFunctionContainer, keepTextures: false, isAHallway: true);

            room.ForEach(x =>
            {
                x.selection.posters = classWeightPre.selection.posters;
                x.selection.posterChance = classWeightPre.selection.posterChance;
                x.selection.windowChance = classWeightPre.selection.windowChance;
                x.selection.windowObject = classWeightPre.selection.windowObject;
                x.selection.lightPre = classWeightPre.selection.lightPre;
            });

            foreach (var floorData in floorDatas)
                floorData.Value.Halls.AddRange(room.FilterRoomAssetsByFloor(floorData.Key).ConvertAll<KeyValuePair<WeightedRoomAsset, bool>>(x => new(x, true)));

            //================================ Special Rooms ========================================

            AddSpecialRoomCollectionWithName("Cafeteria");
            AddSpecialRoomCollectionWithName("Playground");
            AddSpecialRoomCollectionWithName("Library");

            static void AddSpecialRoomCollectionWithName(string name)
            {
                WeightedRoomAsset classWeightPre = null;
                foreach (var ld in Resources.FindObjectsOfTypeAll<LevelObject>())
                {
                    var r = ld.potentialSpecialRooms.FirstOrDefault(x => x.selection.roomFunctionContainer.name.StartsWith(name));
                    if (r != null)
                    {
                        classWeightPre = r;
                        break;
                    }
                }
                if (classWeightPre == null)
                    throw new System.ArgumentException("Could not find a special room instance with " + name);

                var room = GetAllAssets(GetRoomAsset(name), classWeightPre.selection.maxItemValue, classWeightPre.weight, classWeightPre.selection.offLimits, classWeightPre.selection.roomFunctionContainer, keepTextures: classWeightPre.selection.keepTextures, squaredShape: true, autoSizeLimitControl: -1f);

                room.ForEach(x =>
                {
                    x.selection.posters = classWeightPre.selection.posters;
                    x.selection.posterChance = classWeightPre.selection.posterChance;
                    x.selection.windowChance = classWeightPre.selection.windowChance;
                    x.selection.windowObject = classWeightPre.selection.windowObject;
                    x.selection.lightPre = classWeightPre.selection.lightPre;
                    x.selection.basicSwaps = classWeightPre.selection.basicSwaps;
                });
                foreach (var floorData in floorDatas)
                    floorData.Value.SpecialRooms.AddRange(ConvertRoomsToLevelTypeOnes(room.FilterRoomAssetsByFloor(floorData.Key)));


            }

            static void AddAssetsToNpc<N>(List<WeightedRoomAsset> assets, System.Action<N> customAction = null) where N : NPC
            {
                NPCMetaStorage.Instance.All().Do(x =>
                {
                    if (x.value is not N)
                        return;

                    foreach (var npc in x.prefabs)
                    {
                        npc.Value.potentialRoomAssets = npc.Value.potentialRoomAssets.AddRangeToArray([.. assets]);
                        customAction?.Invoke((N)npc.Value);
                    }
                });

            }

            static List<E> AddAssetsToEvent<E>(List<WeightedRoomAsset> assets) where E : RandomEvent
            {
                var l = new List<E>();
                foreach (var x in RandomEventMetaStorage.Instance.All())
                {
                    if (x.value is E e)
                    {
                        l.Add(e);
                        e.potentialRoomAssets = e.potentialRoomAssets.AddRangeToArray([.. assets]);
                    }
                }
                return l;
            }

            static void AddCategoryForNPCToSpawn(RoomCategory cat, params System.Type[] npcs)
            {
                NPCMetaStorage.Instance.All().Do(x =>
                {
                    if (!npcs.Contains(x.value.GetType()))
                        return;

                    foreach (var npc in x.prefabs)
                        npc.Value.spawnableRooms.Add(cat);
                });
            }

            static WeightedRoomAsset FindRoomGroupOfName(string name)
            {
                foreach (var sco in Resources.FindObjectsOfTypeAll<SceneObject>())
                {
                    foreach (var lvl in sco.GetCustomLevelObjects())
                    {
                        var lvlGroup = lvl.roomGroup.FirstOrDefault(x => x.name == name);
                        if (lvlGroup != null)
                            return lvlGroup.potentialRooms[0];
                    }
                }
                return null;
            }

            static WeightedRoomAsset FindRoomGroupOfNameWithActivity<T>(string name) where T : Activity
            {
                foreach (var lvl in Resources.FindObjectsOfTypeAll<LevelObject>())
                {
                    var lvlGroup = lvl.roomGroup.FirstOrDefault(x => x.name == name);
                    if (lvlGroup != null)
                    {
                        var potRooms = lvlGroup.potentialRooms;
                        for (int i = 0; i < potRooms.Length; i++)
                        {
                            if (potRooms[i].selection.hasActivity || potRooms[i].selection.activity.prefab is T)
                                return potRooms[i];
                        }
                    }
                }
                return null;
            }

            static WeightedRoomAsset FindRoomGroupOfNameWithNoActivity(string name)
            {
                foreach (var lvl in Resources.FindObjectsOfTypeAll<LevelObject>())
                {
                    var lvlGroup = lvl.roomGroup.FirstOrDefault(x => x.name == name);
                    if (lvlGroup != null)
                    {
                        var potRooms = lvlGroup.potentialRooms;
                        for (int i = 0; i < potRooms.Length; i++)
                        {
                            if (potRooms[i].selection.hasActivity && potRooms[i].selection.activity.prefab is NoActivity)
                                return potRooms[i];
                        }
                    }
                }
                return null;
            }

            static Transform MakeOffsettedCopy(Transform og, Vector3 offset)
            {
                var newObj = new GameObject($"{og.name}_Holder");
                newObj.ConvertToPrefab(true);
                og.SetParent(newObj.transform, false);
                og.localPosition = offset;
                return newObj.transform;
            }

            GameObject SetupObjCollisionAndScale(GameObject obj, Vector3 navMeshSize, float newScale, bool automaticallyContainer = true, bool addMeshCollider = true)
            {
                obj.transform.localScale = Vector3.one;
                if (navMeshSize != default)
                    obj.gameObject.AddNavObstacle(navMeshSize);

                var childRef = new GameObject(obj.name + "_Renderer");
                childRef.transform.SetParent(obj.transform);
                childRef.transform.localPosition = Vector3.zero;

                var childs = obj.transform.AllChilds();
                childs.ForEach(c =>
                {
                    if (c == childRef.transform)
                        return;
                    c.SetParent(childRef.transform);
                    c.transform.localPosition = Vector3.zero;
                    c.transform.localScale = Vector3.one * newScale;
                    if (addMeshCollider)
                        c.gameObject.AddComponent<MeshCollider>();
                });

                if (automaticallyContainer)
                    obj.AddContainer(obj.GetComponentsInChildren<MeshRenderer>());


                return obj;
            }

            void AddGroupToAllFloors(RoomGroup roomGroup)
            {
                RoomGroupWithLevelType rGroupType = new(roomGroup);
                foreach (var data in floorDatas)
                    data.Value.RoomAssets.Add(rGroupType);
            }

        }

        static void RemoveBigRooms(List<WeightedRoomAsset> assets, float averageGiven)
        {
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i].selection.GetRoomSize().Magnitude() >= averageGiven)
                {
                    Object.Destroy(assets[i].selection);
                    assets.RemoveAt(i--);
                }
            }
        }

        static string GetRoomAsset(string roomName, string asset = "") => Path.Combine(BasePlugin.ModPath, "rooms", roomName, asset);

        static void AddTextureToEditor(string name, Texture2D tex)
        {
            LevelLoaderPlugin.Instance.roomTextureAliases.Add(name, tex);
            man.Add(name, tex);
        }

        static PosterObject AddPosterToEditor(this PosterObject obj)
        {
            LevelLoaderPlugin.Instance.posterAliases.Add(obj.name, obj);

            EditorInterfaceModes.AddModeCallback((EditorMode mode, bool vanillaCompliant) =>
            {
                EditorInterfaceModes.AddToolToCategory(mode, "posters", new PosterTool(obj.name));
            });

            return obj;
        }

        static void AddObjectToEditor(this GameObject obj, string key = null)
        {
            LevelLoaderPlugin.Instance.basicObjects.Add(string.IsNullOrEmpty(key) ? obj.name : key, obj);
            man.Add($"editorPrefab_{obj.name}", obj);
            obj.ConvertToPrefab(true);
        }

        static RoomSettings RegisterRoom(string roomName, Color color, StandardDoorMats mat)
        {
            var settings = new RoomSettings(EnumExtensions.ExtendEnum<RoomCategory>(roomName), RoomType.Room, color, mat);
            LevelLoaderPlugin.Instance.roomSettings.Add(roomName, settings);
            // Debug.Log("{\"key\": \"Ed_Tool_room_" + roomName + "_Title\", \"value\":\"" + roomName.ToFriendlyName() + " \"},");
            // Debug.Log("{\"key\": \"Ed_Tool_room_" + roomName + "_Desc\", \"value\":\"[DESCRIPTION]\"},");
            return settings;
        }

        static List<WeightedRoomAssetWithLevelType> ConvertRoomsToLevelTypeOnes(List<WeightedRoomAsset> assets, params LevelType[] acceptedTypes) =>
            ConvertRoomsToLevelTypeOnes(assets, null, acceptedTypes);

        static List<WeightedRoomAssetWithLevelType> ConvertRoomsToLevelTypeOnes(List<WeightedRoomAsset> assets, string roomName, params LevelType[] acceptedTypes)
        {
            bool useRoomName = !string.IsNullOrEmpty(roomName);
            List<WeightedRoomAssetWithLevelType> newAssets = [];

            for (int i = 0; i < assets.Count; i++)
            {
                newAssets.Add(useRoomName ?
                new(roomName, assets[i].selection, assets[i].weight, acceptedTypes) :
                new(assets[i].selection, assets[i].weight, acceptedTypes)
                );
            }
            return newAssets;
        }

        static RoomSettings RegisterSpecialRoom(string roomName, Color color)
        {
            var settings = new RoomSettings(RoomCategory.Special, RoomType.Room, color, man.Get<StandardDoorMats>("ClassDoorSet"));
            LevelLoaderPlugin.Instance.roomSettings.Add(roomName, settings);
            return settings;
        }

        static List<WeightedRoomAsset> GetAllAssets(string path, int maxValue, int assetWeight, bool isOffLimits = false, RoomFunctionContainer cont = null, bool isAHallway = false, bool secretRoom = false, Texture2D mapBg = null, bool keepTextures = false, bool squaredShape = false, float autoSizeLimitControl = 12f, bool throwIfNoRoomFound = true)
        {
            List<WeightedRoomAsset> assets = [];
            RoomFunctionContainer container = cont;
            BaldiRoomAsset roomData = null;

            string roomName = Path.GetDirectoryName(path);

            // Search for the new .rbpl file format instead of the obsolete .cbld
            foreach (var file in Directory.GetFiles(path, "*.rbpl", SearchOption.AllDirectories))
            {
                if (new FileInfo(file).Length == 0) continue; // Skip empty files
                try
                {
                    // Load RBPL file
                    using BinaryReader reader = new(File.OpenRead(file));

                    roomData = BaldiRoomAsset.Read(reader);
                    // Create an ExtendedRoomAsset from the loaded data
                    var asset = LevelImporter.CreateRoomAsset(roomData, squaredShape);

                    asset.name = Path.GetFileNameWithoutExtension(file);
                    asset.maxItemValue = maxValue;
                    asset.offLimits = isOffLimits;
                    // asset.hallway lol = isAHallway; // Doesn't actually exist, but it's good to keep here as a reminder that this boolean exists
                    // Copy paste from EditorCustomRooms lol
                    if (mapBg != null)
                    {
                        asset.mapMaterial = new(asset.mapMaterial);
                        asset.mapMaterial.SetTexture("_MapBackground", mapBg);
                        asset.mapMaterial.SetShaderKeywords(["_KEYMAPSHOWBACKGROUND_ON"]);
                        asset.mapMaterial.name = asset.name;
                    }

                    if (secretRoom)
                    {
                        List<IntVector2> cellPositions = [.. asset.cells.Select(c => c.pos)];
                        asset.secretCells.AddRange(cellPositions);
                        asset.blockedWallCells.AddRange(cellPositions);
                        asset.eventSafeCells.Clear();
                        asset.entitySafeCells.Clear();
                    }

                    if (isAHallway)
                    {
                        asset.mapMaterial = null; // hallways have no material
                    }
                    else if (container == null) // No reason why would a hallway need a container anyways
                    {
                        // If no RoomFunctionContainer is provided, create one for the first loaded room in the set.
                        container = new GameObject(asset.name + "_Container").AddComponent<RoomFunctionContainer>();
                        container.functions = []; // Initializes the functions bruh
                        container.gameObject.ConvertToPrefab(true);

                        if (
                            !LevelLoaderPlugin.Instance.roomSettings.TryGetValue(roomData.type, out var settings) &&
                            !LevelLoaderPlugin.Instance.roomSettings.TryGetValue(roomName, out settings) // As last resort
                            )
                        {
                            throw new System.Exception($"This room (\'{roomData.type}\') has no roomSettings available! Skipping!");
                        }

                        settings.container = container; // Sets the container for this category
                    }
                    asset.keepTextures = keepTextures;


                    asset.roomFunctionContainer = container;

                    assets.Add(new WeightedRoomAsset() { selection = asset, weight = assetWeight });
                    _moddedAssets.Add(asset);

                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"------------- Warning: an exception was found during .rbpl room loading of file: {file} --------------");
                    Debug.LogException(e);
                    erroredOutRooms.Add(roomData);
                    if (!LevelLoaderPlugin.Instance.roomSettings.ContainsKey(roomData.type))
                        Debug.Log($"\'{roomData.type}\' RoomCategory does not exist in loader.");
                    foreach (var obj in roomData.basicObjects)
                    {
                        if (!LevelLoaderPlugin.Instance.basicObjects.ContainsKey(obj.prefab))
                            Debug.Log($"\'{obj.prefab}\' Object does not exist in loader.");
                    }
                    foreach (var obj in roomData.items)
                    {
                        if (!LevelLoaderPlugin.Instance.itemObjects.ContainsKey(obj.item))
                            Debug.Log($"\'{obj.item}\' Item does not exist in loader.");
                    }
                }

            }

            if (autoSizeLimitControl > 0f && !plug.enableBigRooms.Value)
                RemoveBigRooms(assets, autoSizeLimitControl);

            if (throwIfNoRoomFound && assets.Count == 0)
                throw new System.ArgumentOutOfRangeException($"RoomAssets loaded from path: {path} resulted in 0 rooms loaded in.");
            return assets;
        }
        static List<BaldiRoomAsset> erroredOutRooms = []; // For debugging in UE
        static List<WeightedRoomAsset> FilterRoomAssetsByFloor(this List<WeightedRoomAsset> assets) =>
            assets.FilterRoomAssetsByFloor(string.Empty);
        static List<WeightedRoomAsset> FilterRoomAssetsByFloor(this List<WeightedRoomAsset> assets, string floor)
        {
            var newAss = new List<WeightedRoomAsset>(assets);
            System.Exception e = null;
            try
            {
                for (int i = 0; i < newAss.Count; i++)
                {
                    string[] rawData = newAss[i].selection.name.Split('!');

                    if (rawData.Length > 2)
                    {
                        if (int.TryParse(rawData[2], out int val))
                        {
                            newAss[i].selection.maxItemValue = val;
                            if (val >= 200)
                                newAss[i].weight = 10;
                        }
                        if (rawData.Length > 3)
                        {
                            if (int.TryParse(rawData[3], out val))
                                newAss[i].weight = val;
                        }
                    }

                    if (!string.IsNullOrEmpty(floor))
                    {
                        string[] sdata = rawData[1].Split(',');

                        if (!sdata.Contains(floor))
                        {
                            newAss.RemoveAt(i--);
                            if (newAss.Count == 0)
                                break;
                        }
                    }
                }
            }
            catch (System.IndexOutOfRangeException ex)
            {
                Debug.LogError("Failed to get data from the room collection due to invalid format in the data!");
                e = ex;
            }
            catch (System.ArgumentOutOfRangeException ex)
            {
                Debug.LogError("Failed to get data from the room collection due to invalid format in the data!");
                e = ex;
            }
            finally
            {
                if (e != null)
                    Debug.LogException(e);
            }

            return newAss;
        }

        static List<WeightedRoomAsset> ConvertAssetWeights(this List<WeightedRoomAsset> assets, int newWeight)
        {
            for (int i = 0; i < assets.Count; i++)
                assets[i] = new() { selection = assets[i].selection, weight = newWeight };
            return assets;
        }
        static List<WeightedRoomAssetWithLevelType> ConvertAssetWeights(this List<WeightedRoomAssetWithLevelType> assets, int newWeight)
        {
            for (int i = 0; i < assets.Count; i++)
                assets[i] = new(assets[i].HasRoomName ? assets[i].RoomName : null, assets[i].selection, newWeight);
            return assets;
        }

        readonly static List<RoomAsset> _moddedAssets = [];
    }
}
