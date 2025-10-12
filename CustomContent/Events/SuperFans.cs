using System.Collections.Generic;
using BBTimes.CustomComponents;
using BBTimes.CustomComponents.EventSpecificComponents;
using BBTimes.Extensions;
using MTM101BaldAPI;
using MTM101BaldAPI.Registers;
using PixelInternalAPI.Extensions;
using PlusStudioLevelLoader;
using UnityEngine;


namespace BBTimes.CustomContent.Events
{
	public class SuperFans : RandomEvent, IObjectPrefab
	{
		public void SetupPrefab()
		{
			eventIntro = this.GetSound("Bal_SuperFans.wav", "Event_SuperFans1", SoundType.Voice, Color.green);
			eventIntro.additionalKeys = [
				new() { time = 1.356f, key = "Event_SuperFans2" },
				new() { time = 3.659f, key = "Event_SuperFans3" }
			];

			Cumulo cloud = (Cumulo)NPCMetaStorage.Instance.Get(Character.Cumulo).value;

			var storedSprites = this.GetSpriteSheet(5, 1, 35f, "fan.png");

			var superFanRend = ObjectCreationExtensions.CreateSpriteBillboard(storedSprites[0], false);
			superFanRend.gameObject.ConvertToPrefab(true);
			superFanRend.name = "Superfan";
			var superFan = superFanRend.gameObject.AddComponent<SuperFan>();

			superFan.audBlow = cloud.audBlowing;

			superFan.renderer = superFanRend;
			superFan.sprites = storedSprites;
			superFan.windManager = Instantiate(cloud.windManager);
			superFan.windManager.transform.SetParent(superFan.transform);
			superFan.windGraphicsParent = superFan.windManager.transform.Find("WindGraphicsParent");
			superFan.audMan = superFan.windManager.GetComponentInChildren<AudioManager>();
			superFan.windGraphics = superFan.windGraphicsParent.GetComponentsInChildren<MeshRenderer>();

			superFanPre = superFan;

			LevelLoaderPlugin.Instance.basicObjects.Add("timessuperfansmarker", superFan.gameObject);
		}
		public void SetupPrefabPost() { }
		public string Name { get; set; }
		public string Category => "events";

		// ---------------------------------------------------

		public override void PremadeSetup()
		{
			base.PremadeSetup();
			foreach (var su in FindObjectsOfType<SuperFan>())
			{
				var chosenDirection = ec.CellFromPosition(su.transform.position).RandomUncoveredDirection(crng);
				if (chosenDirection == Direction.Null)
				{
					Debug.LogWarning("A Super Fan was located on a spot with no available wall to be chosen! Destroying it instead.", this);
					Destroy(su.gameObject);
					continue;
				}
				su.Initialize(ec, IntVector2.GetGridPosition(su.transform.position), chosenDirection.GetOpposite(), out _);
				superFans.Add(su);
			}
		}

		public override void AfterUpdateSetup(System.Random rng)
		{
			base.AfterUpdateSetup(rng); // Copy paste from LockdownDoorEvent
			List<Cell> list = ec.AllCells();

			for (int i = 0; i < list.Count; i++)
				if (!list[i].TileMatches(ec.mainHall) || !list[i].HasHardFreeWall || list[i].open || list[i].AllWallDirections.Count > 2) // If wall directions is above 2, this is not a corner or single cell
					list.RemoveAt(i--);

			int fans = rng.Next(minFans, maxFans + 1);
			for (int i = 0; i < fans; i++)
			{
				if (list.Count == 0) return;

				int num3 = rng.Next(list.Count);
				var superFan = Instantiate(superFanPre, list[num3].TileTransform);
				var wallDir = list[num3].RandomConstDirection(rng);

				superFan.Initialize(ec, list[num3].position, wallDir.GetOpposite(), out var l);

				list[num3].HardCover(wallDir.ToCoverage());
				superFans.Add(superFan);
				list.RemoveAt(num3);
				for (int z = 0; z < l.Count; z++)
					list.Remove(l[z]);
			}
		}

		public override void Begin()
		{
			base.Begin();
			superFans.ForEach(x => x.TurnMe(true));
		}

		public override void End()
		{
			base.End();
			superFans.ForEach(x => x.TurnMe(false));
		}

		[SerializeField]
		internal int minFans = 13, maxFans = 21;

		[SerializeField]
		internal SuperFan superFanPre;

		readonly List<SuperFan> superFans = [];
	}
}
