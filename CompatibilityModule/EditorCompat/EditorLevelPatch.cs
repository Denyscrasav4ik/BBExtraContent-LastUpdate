using System.Collections.Generic;
using System.IO;
using System.Linq;
using BBTimes.Extensions;
using BBTimes.Manager;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using MTM101BaldAPI.Registers;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using PlusStudioLevelFormat;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat
{
	internal static class EditorIntegration
	{
		private static AssetManager _editorAssetMan;

		internal static void Initialize(AssetManager man)
		{
			LoadEditorAssets();
			InitializeVisuals(man);
			EditorLevelData.AddDefaultTextureAction(InitializeDefaultTextures);
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
				if (!name.StartsWith("Ignore_"))
				{
					_editorAssetMan.Add("UI/" + name, AssetLoader.SpriteFromTexture2D(AssetLoader.TextureFromFile(file), 40f));
				}
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
		}

		private static void InitializeVisuals(AssetManager man)
		{
			// Objects
			EditorInterface.AddObjectVisual("bathStall", man.Get<GameObject>("editorPrefab_bathStall"), true);
			EditorInterface.AddObjectVisual("bathDoor", man.Get<GameObject>("editorPrefab_bathDoor"), true);
			EditorInterface.AddObjectVisual("sink", man.Get<GameObject>("editorPrefab_sink"), true);
			EditorInterface.AddObjectVisual("Toilet", man.Get<GameObject>("editorPrefab_Toilet"), true);
			EditorInterface.AddObjectVisual("BasketHoop", man.Get<GameObject>("editorPrefab_BasketHoop"), true);
			EditorInterface.AddObjectVisualWithCustomSphereCollider("BasketballPile", man.Get<GameObject>("editorPrefab_BasketballPile"), 2f, Vector3.zero);
			EditorInterface.AddObjectVisual("GrandStand", man.Get<GameObject>("editorPrefab_GrandStand"), true);
			EditorInterface.AddObjectVisual("BasketMachine", man.Get<GameObject>("editorPrefab_BasketMachine"), true);
			EditorInterface.AddObjectVisualWithCustomSphereCollider("BasketBallBigLine", man.Get<GameObject>("editorPrefab_BasketBallBigLine"), 2.5f, Vector3.zero);
			EditorInterface.AddObjectVisual("FancyComputerTable", man.Get<GameObject>("editorPrefab_FancyComputerTable"), true);
			EditorInterface.AddObjectVisualWithCustomSphereCollider("ComputerBillboard", man.Get<GameObject>("editorPrefab_ComputerBillboard"), 1f, Vector3.zero);
			EditorInterface.AddObjectVisualWithCustomBoxCollider("StraightRunLine", man.Get<GameObject>("editorPrefab_StraightRunLine"), new(4.9f, 1f, 4.9f), Vector3.zero);
			EditorInterface.AddObjectVisualWithCustomBoxCollider("CurvedRunLine", man.Get<GameObject>("editorPrefab_CurvedRunLine"), new(4.9f, 1f, 4.9f), Vector3.zero);
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
			for (int i = 1; i <= 6; i++)
				EditorInterface.AddObjectVisual("TimesGenericCornerLamp_" + i, man.Get<GameObject>("editorPrefab_TimesGenericCornerLamp_" + i), true);

			// NPCs
			var allNpcs = new Dictionary<string, string>
			{
				{"0thprize", "ZeroPrize"}, {"adverto", "Adverto"}, {"bubbly", "Bubbly"}, {"Camerastand", "Camerastand"},
				{"cheeseMan", "CheeseMan"}, {"coolMop", "CoolMop"}, {"detentionBot", "DetentionBot"}, {"dribble", "Dribble"},
				{"everettTreewood", "EverettTreewood"}, {"faker", "Faker"}, {"gluebotrony", "Glubotrony"},
				{"happyholidays", "HappyHolidays"}, {"inkArtist", "InkArtist"}, {"JerryTheAC", "JerryTheAC"},
				{"leapy", "Leapy"}, {"MGS", "Magicalstudent"}, {"mopliss", "Mopliss"}, {"mimiCry", "Mimicry"},
				{"mrKreye", "MrKreye"}, {"Mugh", "Mugh"}, {"noseMan", "NoseMan"}, {"officeChair", "OfficeChair"},
				{"pencilBoy", "PencilBoy"}, {"phawillow", "Phawillow"}, {"penny", "Penny"}, {"pran", "Pran"},
				{"pix", "Pix"}, {"quiker", "Quiker"}, {"rollBot", "Rollingbot"}, {"serOran", "SerOran"},
				{"scienceTeacher", "ScienceTeacher"}, {"snowfolke", "Snowfolke"}, {"stunly", "Stunly"},
				{"superintendent", "Superintendent"}, {"spj", "Superintendentjr"}, {"tickTock", "TickTock"},
				{"watcher", "Watcher"}, {"VacuumCleaner", "VacuumCleaner"}, {"winTerry", "Winterry"}, {"ZapZap", "ZapZap"}
			};

			foreach (var pair in allNpcs)
			{
				var en = EnumExtensions.GetFromExtendedName<Character>(pair.Value);
				var meta = NPCMetaStorage.Instance.Find(x => x.character == en && BBTimesManager.plug.Info == x.info);
				if (meta != null)
				{
					EditorInterface.AddNPCVisual("times_" + pair.Value, meta.value);
				}
			}
			// Special case for oldsweep
			var sweepMeta = NPCMetaStorage.Instance.All().FirstOrDefault(m => m.prefabs.Any(p => p.Value.GetComponent<CustomContent.NPCs.ClassicGottaSweep>()));
			if (sweepMeta != null)
			{
				EditorInterface.AddNPCVisual("times_oldsweep", sweepMeta.value);
			}
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
				string key = "times_" + itmEnum;
				Sprite icon = _editorAssetMan.Get<Sprite>("UI/ITM_" + itmEnum);
				EditorInterfaceModes.AddToolToCategory(mode, "items", new ItemTool(key, icon));
			}

			// Add NPC tools
			string[] npcToolData =
			[
				"ZeroPrize", "Adverto", "Bubbly", "Camerastand", "CheeseMan", "CoolMop", "oldsweep", "DetentionBot", "Dribble",
				"EverettTreewood", "Faker", "Glubotrony", "HappyHolidays", "InkArtist", "JerryTheAC", "Leapy", "Magicalstudent",
				"Mopliss", "Mimicry", "MrKreye", "Mugh", "NoseMan", "OfficeChair", "PencilBoy", "Phawillow", "Penny",
				"Pran", "Pix", "Quiker", "Rollingbot", "SerOran", "ScienceTeacher", "Snowfolke", "Stunly",
				"Superintendent", "Superintendentjr", "TickTock", "Watcher", "VacuumCleaner", "Winterry", "ZapZap"
			];
			foreach (string npcName in npcToolData)
			{
				string key = "times_" + npcName;
				Sprite icon = GetSprite($"UI/Npc_{npcName}", $"UI/npc_{npcName}");
				EditorInterfaceModes.AddToolToCategory(mode, "characters", new NPCTool(key, icon));
			}

			// Add Room tools
			string[] roomNames =
			[
				"Bathroom", "AbandonedRoom", "BasketballArea", "ComputerRoom", "DribbleRoom", "Forest", "Kitchen",
				"FocusRoom", "SuperMystery", "ExibitionRoom", "SnowyPlayground", "IceRink"
			];
			foreach (string roomName in roomNames)
			{
				Sprite icon = GetSprite($"UI/Floor_{roomName}", $"UI/floor_{roomName}");
				EditorInterfaceModes.AddToolToCategory(mode, "rooms", new RoomTool(roomName, icon));
			}

			// Add Object tools
			AddObjectTools(mode);
		}
		/// <summary>
		/// Add object tools.
		/// </summary>
		private static void AddObjectTools(EditorMode mode)
		{
			// Key: object ID, Value: isRotatable
			var objectTools = new List<ObjectData>
			{
				new("bathStall", true, 5f), new("bathDoor", true), new("sink", false), new("Toilet", false),
				new("BasketHoop", true), new("BasketballPile", false, 2f), new("GrandStand", true, 4f), new("BasketMachine", true),
				new("BasketBallBigLine", true), new("FancyComputerTable", true), new("ComputerBillboard", false, 5f),
				new("StraightRunLine", true), new("CurvedRunLine", true), new("Foresttree", false), new("Campfire", false),
				new("Beartrap", false), new("KitchenCabinet", false), new("JoeChef", true, 5f), new("FocusedStudent", false, 5f),
				new("ComputerTeleporter", false), new("DustShroom", false), new("SensitiveVase", false, 4.2f),
				new("TimesItemDescriptor", false, 5f), new("SnowyPlaygroundTree", false), new("SnowPile", false),
				new("Shovel_ForSnowPile", false, 0.1f), new("MysteryTresentMaker", false), new("MetalFence", true),
				new("SecretBread", false), new("TimesKitchenSteak", false), new("JoeSign", false)
			};
			for (int i = 1; i <= 8; i++) objectTools.Add(new("TimesGenericOutsideFlower_" + i, false));
			for (int i = 1; i <= 6; i++) objectTools.Add(new("TimesGenericCornerLamp_" + i, false));

			foreach (var pair in objectTools)
			{
				Sprite icon = GetSprite($"UI/Obj_{pair.prefab}", $"UI/obj_{pair.prefab}");
				if (pair.rotatable) // isRotatable
					EditorInterfaceModes.AddToolToCategory(mode, "objects", new ObjectTool(pair.prefab, icon, pair.offset));
				else
					EditorInterfaceModes.AddToolToCategory(mode, "objects", new ObjectToolNoRotation(pair.prefab, icon, pair.offset));
			}

			// Special Bulk Object Tool
			Sprite multiStallSprite = GetSprite("UI/Obj_fullStall", "UI/obj_fullStall");
			EditorInterfaceModes.AddToolToCategory(mode, "objects", new BulkObjectTool("fullStall", multiStallSprite,
				[
					new("bathStall", new Vector3(-5f, 0f, 0f), new(0f, 90f)),
					new("bathDoor", new Vector3(0f, 0f, 4f), Vector3.zero),
					new("bathStall", new Vector3(5f, 0f, 0f), new(0f, 90f))
				]
			));
		}

		private static Sprite GetSprite(string key1, string key2) =>
			_editorAssetMan.ContainsKey(key1) ? _editorAssetMan.Get<Sprite>(key1) : _editorAssetMan.Get<Sprite>(key2);

		private readonly struct ObjectData(string prefab, bool rotatable)
		{
			public ObjectData(string prefab, bool rotatable, float offset) : this(prefab, rotatable) =>
				this.offset = offset;

			readonly public string prefab = prefab;
			readonly public bool rotatable = rotatable;
			readonly public float offset = 0f;
		}
	}
}