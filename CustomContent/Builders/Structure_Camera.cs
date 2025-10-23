using System.Collections.Generic;
using BBTimes.CompatibilityModule.EditorCompat;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.Objects;
using BBTimes.Extensions;
using MTM101BaldAPI;
using PixelInternalAPI.Extensions;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.CustomContent.Builders
{
	public class Structure_Camera : StructureBuilder, IBuilderPrefab
	{

		public StructureWithParameters SetupBuilderPrefabs()
		{
			var cam = this.GetModel("SecurityCamera", true, false, Vector3.one * 0.02f, out var renderer);
			renderer.transform.localPosition = Vector3.up * 9.15f;
			cam.gameObject.ConvertToPrefab(true);

			var camComp = cam.gameObject.AddComponent<SecurityCamera>();
			camComp.collider = cam.gameObject.AddBoxCollider(new(0f, 9f, 0f), Vector3.one * 2f, true);

			var visionIndicator = ObjectCreationExtensions.CreateSpriteBillboard(this.GetSprite(15f, "tiledGrid.png"), false);
			visionIndicator.gameObject.layer = 0;
			visionIndicator.material.SetTexture("_LightMap", null); // No light affected, it's always bright

			visionIndicator.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			visionIndicator.transform.localScale = new(1f, 1.172f, 1f);
			visionIndicator.name = "CameraVisionIndicator";
			visionIndicator.gameObject.ConvertToPrefab(true);

			camComp.visionIndicatorPre = visionIndicator;

			camComp.audMan = cam.gameObject.CreatePropagatedAudioManager(25f, 110f);
			camComp.audAlarm = this.GetSound("alarm.wav", "Vfx_Camera_Alarm", SoundType.Effect, Color.white);
			camComp.audTurn = this.GetSound("camSwitch.wav", "Vfx_Camera_Switch", SoundType.Effect, Color.white);
			camComp.audDetect = this.GetSound("spot.wav", "Vfx_Camera_Spot", SoundType.Effect, Color.white);

			camPre = cam.transform;

			// Makes the LoaderStructureData for the camera spawn
			LevelLoaderPlugin.Instance.structureAliases.Add(EditorIntegration.TimesPrefix + "SecurityCamera", new() { structure = this });

			return new() { prefab = this, parameters = new() { minMax = [new(1, 1), new(5, 10)] } }; // 0 = Amount of cameras, 1 = minMax distance for them
		}
		public void SetupPrefab() { }
		public void SetupPrefabPost() { }

		public string Name { get; set; }
		public string Category => "objects";

		public static string GetJSONUIPath() => System.IO.Path.Combine(BasePlugin.ModPath, "objects", "SecurityCamera", "CameraUI.json");


		// Prefab stuff above ^^
		public override void PostOpenCalcGenerate(LevelGenerator lg, System.Random rng)
		{
			base.PostOpenCalcGenerate(lg, rng);

			var room = lg.Ec.mainHall;
			var spots = room.GetTilesOfShape(TileShapeMask.Corner | TileShapeMask.Single, false);
			for (int i = 0; i < spots.Count; i++)
				if (!spots[i].HardCoverageFits(CellCoverage.Up))
					spots.RemoveAt(i--);

			if (spots.Count == 0)
			{
				Finished();
				Debug.LogWarning("CameraBuilder has failed to find a good spot for the Security Camera.");
				return;
			}

			int amount = rng.Next(parameters.minMax[0].x, parameters.minMax[0].z + 1);

			for (int i = 0; i < amount; i++)
			{
				if (spots.Count == 0)
					break;

				int s = rng.Next(spots.Count);
				var cam = Instantiate(camPre, spots[s].ObjectBase).GetComponentInChildren<SecurityCamera>();
				cam.Ec = ec;
				cam.Setup(spots[s].AllOpenNavDirections, rng.Next(parameters.minMax[1].x, parameters.minMax[1].z + 1));

				spots[s].HardCover(CellCoverage.Up);
				spots.RemoveAt(s);
			}

			Finished();
		}
		public override void Load(List<StructureData> data)
		{
			base.Load(data);
			for (int i = 0; i < data.Count; i += 2)
			{
				var spot = ec.CellFromPosition(data[i].position);
				var cam = Instantiate(camPre, spot.ObjectBase).GetComponentInChildren<SecurityCamera>();
				cam.Ec = ec;

				Embedded2Shorts embedded = data[i].data;
				float turnCooldown = data[i + 1].data.ReinterpretAsFloat();
				cam.Setup(spot.AllOpenNavDirections, embedded.A, embedded.B, turnCooldown);

				spot.HardCover(CellCoverage.Up);
			}

			Finished();
		}

		[SerializeField]
		internal Transform camPre;
	}
}
