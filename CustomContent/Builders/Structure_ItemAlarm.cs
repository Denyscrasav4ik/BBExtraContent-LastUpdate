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
	public class Structure_ItemAlarm : StructureBuilder, IBuilderPrefab
	{

		public StructureWithParameters SetupBuilderPrefabs()
		{
			var alarmObj = ObjectCreationExtensions.CreateSpriteBillboard(this.GetSprite(25f, "itemDetectorAlarm.png"))
				.AddSpriteHolder(out var renderer, 0.7f);
			alarmObj.name = "ItemAlarm";
			renderer.name = "ItemAlarm_Renderer";
			alarmObj.gameObject.ConvertToPrefab(true);
			alarmPre = alarmObj.gameObject.AddComponent<ItemAlarm>();
			alarmPre.audMan = alarmObj.gameObject.CreatePropagatedAudioManager(125f, 185f);
			alarmPre.audAlarm = this.GetSound("itemDetectorAlarmNoise.wav", "Vfx_ItemAlarm_Alarm", SoundType.Effect, Color.white);
			alarmPre.renderer = renderer.transform;

			// Makes the LoaderStructureData for the spawn
			LevelLoaderPlugin.Instance.structureAliases.Add(EditorIntegration.TimesPrefix + "ItemAlarm", new LoaderStructureData(this));

			return new() { prefab = this, parameters = new() { minMax = [new(2, 5)] } }; // minMax amount
		}
		public void SetupPrefab() { }
		public void SetupPrefabPost() { }

		public string Name { get; set; }
		public string Category => "objects";


		// Prefab stuff above ^^

		public override void Load(List<StructureData> data)
		{
			base.Load(data);
			loadedInAsLevelLoader = true;
			if (data.Count == 0) return;

			var potentialPickups = new List<Pickup>(ec.items);
			potentialPickups.RemoveAll(pic => !pic.free || pic.showDescription || ec.CellFromPosition(pic.transform.position).room.type == RoomType.Hall);

			if (potentialPickups.Count == 0) return;

			var holder = CreateAlarmHolder();

			foreach (var dat in data)
			{
				Vector3 dataPosition = dat.position.ToVector3();
				int index = -1;
				float smallestDistance = 0f;
				for (int i = 0; i < potentialPickups.Count; i++)
				{
					// -1 check to optimize this by a tiny bit
					float distance = Vector3.Distance(potentialPickups[i].transform.position.ZeroOutY(), dataPosition);
					if (index == -1 || distance < smallestDistance)
					{
						index = i;
						smallestDistance = distance;
					}
				}

				if (index != -1)
				{
					CreateItemAlarm(potentialPickups[index], holder);
				}
			}
		}

		public override void OnGenerationFinished(LevelBuilder lg)
		{
			base.OnGenerationFinished(lg);
			if (loadedInAsLevelLoader) return;

			var potentialPickups = new List<Pickup>(ec.items);
			potentialPickups.RemoveAll(pic => !pic.free || pic.showDescription || ec.CellFromPosition(pic.transform.position).room.type == RoomType.Hall); // Only store items show description... Hopefully that stays like that

			if (potentialPickups.Count == 0)
			{
				Debug.LogWarning("Structure_ItemAlarm failed to find any good spots for alarms");
				Finished();
				return;
			}

			var holder = CreateAlarmHolder();

			int amount = lg.controlledRNG.Next(parameters.minMax[0].x, parameters.minMax[0].z + 1);

			for (int i = 0; i < amount; i++)
			{
				if (potentialPickups.Count == 0)
					break;

				int idx = lg.controlledRNG.Next(potentialPickups.Count);
				CreateItemAlarm(potentialPickups[i], holder);
				potentialPickups.RemoveAt(idx);
			}

			Finished();
		}

		void CreateItemAlarm(Pickup pickup, Transform holder)
		{
			var alarm = Instantiate(alarmPre, holder);

			alarm.AttachToPickup(pickup);
			alarm.Ec = ec;
		}

		Transform CreateAlarmHolder()
		{
			var gb = new GameObject("ItemAlarm_Holder");
			gb.transform.SetParent(ec.transform);
			gb.transform.localPosition = Vector3.zero;
			return gb.transform;
		}

		[SerializeField]
		internal ItemAlarm alarmPre;

		bool loadedInAsLevelLoader = false;
	}
}
