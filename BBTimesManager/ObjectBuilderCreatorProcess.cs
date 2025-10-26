using System.IO;
using BBTimes.CustomContent.Builders;
using BBTimes.Helpers;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetTools;
using UnityEngine;

namespace BBTimes.Manager
{
	internal static partial class BBTimesManager
	{
		static void CreateObjBuilders()
		{

			// Duct Builder
			StructureWithParameters vent = CreatorExtensions.CreateObjectBuilder<Structure_Duct>("Structure_Duct", out _, "Duct");
			floorDatas[F3].WeightedObjectBuilders.Add(new(vent, 45, LevelType.Schoolhouse));

			// Wall Bell Builder
			vent = CreatorExtensions.CreateObjectBuilder<RandomForcedPostersBuilder>("ForcedPosterBuilder", out var forcedPosterBuilder);
			forcedPosterBuilder.allowedShape = TileShapeMask.Single | TileShapeMask.Corner;
			vent.parameters.chance[0] = 0.35f;
			forcedPosterBuilder.posters = [
				new WeightedPosterObject() {selection = ObjectCreators.CreatePosterObject([AssetLoader.TextureFromFile(Path.Combine(MiscPath, TextureFolder, GetAssetName("wallbell.png")))]), weight = 100}
				];
			foreach (var fld in floorDatas)
				fld.Value.ForcedObjectBuilders.Add(new(vent));

			// Trapdoor Builder
			vent = CreatorExtensions.CreateObjectBuilder<Structure_Trapdoor>("Structure_Trapdoor", out _, "Trapdoor");

			floorDatas[F2].WeightedObjectBuilders.Add(new(vent, 50, LevelType.Schoolhouse, LevelType.Maintenance));

			vent = CloneParameter(vent);
			vent.parameters.minMax[0] = new(4, 5);
			vent.parameters.chance[0] = 0.35f;

			floorDatas[F3].WeightedObjectBuilders.Add(new(vent, 25, LevelType.Schoolhouse, LevelType.Maintenance));


			// Camera Builder
			vent = CreatorExtensions.CreateObjectBuilder<Structure_Camera>("Structure_Camera", out _, "SecurityCamera");
			vent.parameters.minMax[0] = new(3, 5);

			//floorDatas[F1].ForcedObjectBuilders.Add(vent);

			floorDatas[F2].WeightedObjectBuilders.Add(new(vent, 65));
			floorDatas[END].WeightedObjectBuilders.Add(new(vent, 35));
			vent = CloneParameter(vent);
			vent.parameters.minMax[0] = new(5, 7);
			vent.parameters.minMax[1] = new(12, 15);

			floorDatas[F4].ForcedObjectBuilders.Add(new(vent, LevelType.Laboratory));
			floorDatas[F5].ForcedObjectBuilders.Add(new(vent, LevelType.Laboratory));


			// Squisher builder
			vent = CreatorExtensions.CreateObjectBuilder<Structure_Squisher>("Structure_Squisher", out _, "Squisher");

			vent.parameters.minMax[0].z = 6;
			vent.parameters.chance[0] = 0.15f;
			floorDatas[F3].WeightedObjectBuilders.Add(new(vent, 95, LevelType.Schoolhouse, LevelType.Factory));
			floorDatas[F4].ForcedObjectBuilders.Add(new(vent, LevelType.Factory));
			floorDatas[F5].ForcedObjectBuilders.Add(new(vent, LevelType.Factory));


			// Small Door builder
			vent = CreatorExtensions.CreateObjectBuilder<Structure_SmallDoor>("Structure_SmallDoor", out _, "SmallDoor");
			foreach (var fld in floorDatas)
				fld.Value.ForcedObjectBuilders.Add(new(vent, LevelType.Schoolhouse, LevelType.Maintenance));

			// ItemAlarm Builder
			vent = CreatorExtensions.CreateObjectBuilder<Structure_ItemAlarm>("Structure_ItemAlarm", out _, "ItemAlarm");

			floorDatas[F3].ForcedObjectBuilders.Add(new(vent, LevelType.Laboratory));
			floorDatas[F4].ForcedObjectBuilders.Add(new(vent, LevelType.Laboratory));

			vent = CloneParameter(vent);
			vent.parameters.minMax[0] = new(6, 9);
			floorDatas[F5].ForcedObjectBuilders.Add(new(vent, LevelType.Laboratory));

			// Notebook Machine Builder
			vent = CreatorExtensions.CreateObjectBuilder<Structure_NotebookMachine>("Structure_NotebookMachine", out _, "NotebookMachine");

			foreach (var floor in floorDatas)
				floor.Value.ForcedObjectBuilders.Add(new(vent)); // Every floor must have this for rooms

			// Outside Box
			// *for F1
			vent = CreatorExtensions.CreateObjectBuilder<Structure_OutsideBox>("Structure_OutsideBox", out _, "OutsideBox");
			Structure_OutsideBox.decorations = new GameObject[8];
			for (int i = 0; i < Structure_OutsideBox.decorations.Length; i++)
				Structure_OutsideBox.decorations[i] = man.Get<GameObject>($"editorPrefab_TimesGenericOutsideFlower_{i + 1}");

			floorDatas[F1].ForcedObjectBuilders.Add(new(vent));
			floorDatas[END].ForcedObjectBuilders.Add(new(vent));

			// *for the rest of the levels
			vent = CloneParameter(vent);
			vent.parameters.chance[0] = 0f;

			foreach (var floor in floorDatas)
				if (floor.Key != F1 && floor.Key != END) // Except F1, that one is special
					floor.Value.ForcedObjectBuilders.Add(new(vent)); // Every floor must have this for rooms

			static StructureWithParameters CloneParameter(StructureWithParameters bld) =>
				new() { prefab = bld.prefab, parameters = new() { chance = bld.parameters.chance.CopyArray(), minMax = bld.parameters.minMax.CopyArray(), prefab = bld.parameters.prefab.CopyObjArray() } };

		}

		static T[] CopyArray<T>(this T[] ogAr)
		{
			if (ogAr == null)
				return null;

			T[] newAr = new T[ogAr.Length];
			for (int i = 0; i < ogAr.Length; i++) // Useful for arrays that contains structs
				newAr[i] = ogAr[i];
			return newAr;
		}

		static WeightedGameObject[] CopyObjArray(this WeightedGameObject[] ogAr)
		{
			if (ogAr == null)
				return null;

			WeightedGameObject[] newAr = new WeightedGameObject[ogAr.Length];
			for (int i = 0; i < ogAr.Length; i++) // Useful for arrays that contains structs
				newAr[i] = new() { selection = ogAr[i].selection, weight = ogAr[i].weight };
			return newAr;
		}

	}
}
