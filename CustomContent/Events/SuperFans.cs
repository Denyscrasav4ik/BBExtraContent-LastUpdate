using System.Collections;
using System.Collections.Generic;
using BBTimes.CompatibilityModule.EditorCompat;
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

			LevelLoaderPlugin.Instance.basicObjects.Add(EditorIntegration.TimesPrefix + "SuperFan", superFan.gameObject);
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
				var chosenDirection = Directions.DirFromVector3(su.transform.forward, 45f);
				if (chosenDirection == Direction.Null)
				{
					Debug.LogWarning("A Super Fan was located on a spot with no available wall to be chosen! Destroying it instead.", this);
					Destroy(su.gameObject);
					continue;
				}

				// If the direction it faces is a wall, set it to face the opposite
				if (ec.CellFromPosition(su.transform.position).HasWallInDirection(chosenDirection)) // The chosenDirection should always be facing the where the fan will blow, not the wall it is attached to
					chosenDirection = chosenDirection.GetOpposite();

				su.Initialize(ec, IntVector2.GetGridPosition(su.transform.position), chosenDirection, out _);
				superFans.Add(su);
			}
		}

		public override void AfterUpdateSetup(System.Random rng)
		{
			base.AfterUpdateSetup(rng);
			List<Cell> cells = ec.AllCells();
			List<Direction> candidateDirections = [];

			// Filter cells: must have a hard free wall, not open, not main hall, and not more than 2 wall directions
			for (int i = 0; i < cells.Count; i++)
			{
				if (!cells[i].TileMatches(ec.mainHall) || !cells[i].HasHardFreeWall || cells[i].open || cells[i].AllWallDirections.Count > 2)
					cells.RemoveAt(i--);
			}

			int fans = rng.Next(minFans, maxFans + 1);
			for (int i = 0; i < fans; i++)
			{
				if (cells.Count == 0) return;

				int idx = rng.Next(cells.Count);
				Cell cell = cells[idx];
				var expectedRoom = cell.room;
				candidateDirections.Clear();

				// Find a wall direction whose opposite side faces an open direction and a long hallway
				foreach (var wallDir in cell.AllWallDirections)
				{
					Direction oppositeDir = wallDir.GetOpposite();
					Cell nextCell = ec.CellFromPosition(cell.position + oppositeDir.ToIntVector2());
					bool cancelHallLengthCheck = false;
					if (!nextCell.Null && nextCell.TileMatches(expectedRoom))
					{
						// Check hallway length in the open direction
						int length = 0;
						Cell hallwayCell = nextCell;
						while (!hallwayCell.Null && length < minHallwayLength)
						{
							IntVector2 nextVector = hallwayCell.position + oppositeDir.ToIntVector2();
							if (!ec.ContainsCoordinates(nextVector)) // Coordinate check
							{
								cancelHallLengthCheck = true;
								break;
							}
							hallwayCell = ec.CellFromPosition(nextVector);

							// Misc check for stuff like room equivalency
							if (!hallwayCell.TileMatches(expectedRoom))
							{
								cancelHallLengthCheck = true;
								break;
							}
							length++;
						}
						if (cancelHallLengthCheck) continue; // Skips to the next direction in the list

						if (length >= minHallwayLength)
							candidateDirections.Add(wallDir);
					}
				}

				if (candidateDirections.Count == 0)
				{
					cells.RemoveAt(idx);
					i--; // -1 to not consider the super fan was added
					continue;
				}

				Direction chosenWallDir = candidateDirections[rng.Next(candidateDirections.Count)];
				var superFan = Instantiate(superFanPre, cell.TileTransform);
				superFan.Initialize(ec, cell.position, chosenWallDir.GetOpposite(), out var l);

				cell.HardCover(chosenWallDir.ToCoverage());
				superFans.Add(superFan);
				cells.RemoveAt(idx);
				for (int z = 0; z < l.Count; z++)
					cells.Remove(l[z]);
			}
		}

		public override void Begin()
		{
			base.Begin();
			superFans.ForEach(x => StartCoroutine(SlowlyTurnFans(x, true)));
		}

		public override void End()
		{
			base.End();
			superFans.ForEach(x => StartCoroutine(SlowlyTurnFans(x, false)));
		}

		IEnumerator SlowlyTurnFans(SuperFan fan, bool on)
		{
			yield return new WaitForSecondsEnvironmentTimescale(ec, Random.Range(minTurnCooldown, maxTurnCooldown));
			fan.TurnMe(on);
		}

		[SerializeField]
		internal int minFans = 13, maxFans = 21, minHallwayLength = 7;

		[SerializeField]
		internal float minTurnCooldown = 2f, maxTurnCooldown = 5f;

		[SerializeField]
		internal SuperFan superFanPre;

		readonly List<SuperFan> superFans = [];
	}
}
