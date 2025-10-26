using System.IO;
using BBTimes.CustomComponents;
using MTM101BaldAPI.AssetTools;
using PixelInternalAPI.Extensions;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.Manager
{
	internal static partial class BBTimesManager
	{
		static void CreateCubeMaps()
		{
			var F3Map = AssetLoader.CubemapFromFile(Path.Combine(MiscPath, TextureFolder, GetAssetName("cubemap_night.png")));
			LevelLoaderPlugin.Instance.skyboxAliases.Add("TimesNightSky", F3Map);

			var twilight = GenericExtensions.FindResourceObjectByName<Cubemap>("Cubemap_Twilight");

			// Add lightings outside for GameManagers
			foreach (var man in GenericExtensions.FindResourceObjects<SceneObject>())
			{
				var comp = man.manager.GetComponent<MainGameManagerExtraComponent>();
				if (comp == null) continue;
				//if (man.levelTitle == "F1") By default, it's the *default* cube map
				//{
				//	comp.mapForToday = ObjectCreationExtension.defaultCubemap;
				//	continue;
				//}
				if (man.levelTitle == F2 || man.levelTitle == F5)
				{
					comp.outsideLighting = new Color32(255, 204, 131, 255);
					man.skybox = twilight;
					continue;
				}
				if (man.levelTitle == F3 || man.levelTitle == F4)
				{
					man.skybox = F3Map;
					comp.outsideLighting = new Color32(160, 153, 255, 255);
					continue;
				}
			}
		}
	}
}
