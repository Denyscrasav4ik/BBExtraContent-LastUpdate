using System.Collections.Generic;
using BBTimes.CompatibilityModule.EditorCompat;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.Objects;
using BBTimes.Extensions;
using MTM101BaldAPI;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Extensions;
using PlusStudioLevelLoader;
using TMPro;
using UnityEngine;


namespace BBTimes.CustomContent.Builders
{

	public class Structure_Trapdoor : StructureBuilder, IBuilderPrefab
	{
		public StructureWithParameters SetupBuilderPrefabs()
		{
			var trapdoorholder = new GameObject("TrapDoor").AddComponent<Trapdoor>();
			trapdoorholder.gameObject.ConvertToPrefab(true);

			var text = new GameObject("TrapdoorText").AddComponent<TextMeshPro>();
			text.gameObject.layer = LayerStorage.billboardLayer;
			text.transform.SetParent(trapdoorholder.transform);
			text.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			text.alignment = TextAlignmentOptions.Center;
			text.rectTransform.offsetMin = new(-4f, -3.99f);
			text.rectTransform.offsetMax = new(4f, 4.01f);
			text.transform.localPosition = Vector3.up * 0.05f;
			trapdoorholder.text = text;

			var collider = trapdoorholder.gameObject.AddComponent<BoxCollider>();
			collider.size = Vector3.one * 4.9f;
			collider.isTrigger = true;

			var builder = GetComponent<Structure_Trapdoor>();
			builder.trapDoorpre = trapdoorholder;

			var trapSprites = this.GetSpriteSheet(2, 2, 25f, "traps.png");

			builder.closedSprites = [trapSprites[0], trapSprites[1]];
			builder.openSprites = [trapSprites[2], trapSprites[3]];

			trapdoorholder.aud_shut = this.GetSound("trapDoor_shut.wav", "Sfx_Doors_StandardShut", SoundType.Effect, Color.white);
			trapdoorholder.aud_open = this.GetSound("trapDoor_open.wav", "Sfx_Doors_StandardOpen", SoundType.Effect, Color.white);

			var trapdoor = ObjectCreationExtensions.CreateSpriteBillboard(builder.closedSprites[0], false);
			trapdoor.transform.SetParent(trapdoorholder.transform); // prefab stuf
			trapdoor.transform.localScale = new(0.96f, 0.96f, 1f);

			trapdoor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			trapdoor.transform.localPosition = Vector3.up * 0.02f;
			trapdoor.name = "TrapdoorVisual";
			trapdoor.gameObject.layer = 0; // default layer
			Destroy(trapdoor.GetComponent<RendererContainer>());

			trapdoorholder.renderer = trapdoor;
			trapdoorholder.audMan = trapdoorholder.gameObject.CreatePropagatedAudioManager(35f, 70f);


			// Fake trapdoor
			var fake = trapdoor.SafeDuplicatePrefab(true);
			fake.name = "FakeTrapDoor";
			fake.gameObject.CreatePropagatedAudioManager(35f, 70f);
			trapdoorholder.fakeTrapdoorPre = fake.transform;

			// Makes the LoaderStructureData for the trapdoor spawn
			LevelLoaderPlugin.Instance.structureAliases.Add(EditorIntegration.TimesPrefix + "Trapdoor", new() { structure = this });

			return new() { prefab = this, parameters = new() { minMax = [new(3, 4)], chance = [0.55f] } };
		}

		public void SetupPrefabPost() { }
		public void SetupPrefab() { }

		public static string GetJSONUIPath() => System.IO.Path.Combine(BasePlugin.ModPath, "objects", "Trapdoor", "TrapdoorUI.json");

		public string Name { get; set; }
		public string Category => "objects";




		// setup prefab ^^
		public override void PostOpenCalcGenerate(LevelGenerator lg, System.Random rng)
		{

			base.PostOpenCalcGenerate(lg, rng);

			map = new(ec, PathType.Const, int.MaxValue, []);

			var ecData = ec.GetComponent<EnvironmentControllerData>();

			var tiles = ec.mainHall.GetTilesOfShape(TileShapeMask.Corner | TileShapeMask.End | TileShapeMask.Single, CellCoverage.Center | CellCoverage.Down, false);
			//Debug.Log("BBTimes: available tiles: " + tiles.Count);

			if (tiles.Count == 0)
			{
				Debug.LogWarning("Structure_Trapdoor failed to find any good spots for trapdoors.");
				Finished();
				return;
			}

			List<WeightedSelection<Cell>> weightedCells = tiles.ConvertAll(x => new WeightedSelection<Cell>() { selection = x, weight = 100 });
			List<IntVector2> positionsToAvoid = [];

			int amount = rng.Next(parameters.minMax[0].x, parameters.minMax[0].z + 1);
			for (int i = 0; i < amount; i++)
			{
				if (weightedCells.Count == 0)
					break;


				var trap = CreateTrapDoor(GetCell(), ec);

				if (weightedCells.Count >= 1 && rng.NextDouble() <= parameters.chance[0]) // Checks 1 in the count because every GetCell() removes an item from this list
				{
					var strap = CreateTrapDoor(GetCell(), ec);

					trap.SetLinkedTrapDoor(strap);
					strap.SetLinkedTrapDoor(trap);

					trap.renderer.sprite = openSprites[1];
					strap.renderer.sprite = openSprites[1];

					trap.sprites = [closedSprites[1], openSprites[1]];
					strap.sprites = [closedSprites[1], openSprites[1]];
				}
				else
				{
					trap.renderer.sprite = openSprites[0]; // Random trapdoor
					trap.sprites = [closedSprites[0], openSprites[0]];
				}
			}

			Finished();

			Cell GetCell()
			{
				int index = WeightedSelection<Cell>.ControlledRandomIndexList(weightedCells, rng);
				var cell = weightedCells[index].selection;

				positionsToAvoid.Add(cell.position);

				weightedCells.RemoveAt(index);

				if (weightedCells.Count != 0)
				{
					map.Calculate([.. positionsToAvoid]); // Update weights on the positions and distances
					for (int x = 0; x < weightedCells.Count; x++)
					{
						int val = Mathf.Min(maximumDistanceFromATrapdoor, map.Value(weightedCells[x].selection.position));
						if (val < minimumDistanceFromATrapDoor)
							weightedCells.RemoveAt(x--);
						else
							weightedCells[x].weight = val;
					}
				}

				return cell;
			}

		}

		public override void Load(List<StructureData> datas)
		{
			base.Load(datas);

			for (int i = 0; i < datas.Count; i++)
			{
				var cell = ec.CellFromPosition(datas[i].position);
				Embedded2Shorts embeddedData = datas[i].data;

				var trap = CreateTrapDoor(cell, ec, embeddedData.B); // B here for cooldown
				if (datas[i].data <= 0) // If below or equal to 0, there's no link explicitely told
				{
					RandomTrapdoor();
					continue;
				}

				// Algorithm to find a potential trapdoor to link
				short id = embeddedData.A;
				bool success = false;

				for (int z = i + 1; z < datas.Count; z++)
				{
					Embedded2Shorts nextEmbeddedData = datas[z].data;
					short nextId = nextEmbeddedData.A;

					if (nextId == id)
					{
						cell = ec.CellFromPosition(datas[z].position); // Updates cell for the next position

						// Linked Trapdoor Setup
						var strap = CreateTrapDoor(cell, ec, nextEmbeddedData.B); // nextEmbeddedData.B because it has the cooldown there
						trap.SetLinkedTrapDoor(strap);
						strap.SetLinkedTrapDoor(trap);

						trap.renderer.sprite = openSprites[1];
						strap.renderer.sprite = openSprites[1];
						trap.sprites = [closedSprites[1], openSprites[1]];
						strap.sprites = [closedSprites[1], openSprites[1]];

						datas.RemoveAt(z);
						datas.RemoveAt(i--);
						success = true;

						break;
					}
				}

				if (!success) // If no success on finding a linked trapdoor, just assign it as a random one
				{
					Debug.LogWarning($"Trapdoor contains ID ({id}) but no correspondent trapdoor to link. Setting it to random...");
					RandomTrapdoor();
				}


				void RandomTrapdoor()
				{
					trap.renderer.sprite = openSprites[0]; // Random trapdoor
					trap.sprites = [closedSprites[0], openSprites[0]];
					datas.RemoveAt(i--);
				}
			}
			Finished();
		}

		private Trapdoor CreateTrapDoor(Cell pos, EnvironmentController ec, short cooldown = -1)
		{
			var trapdoor = Instantiate(trapDoorpre);
			trapdoor.transform.SetParent(pos.ObjectBase);
			trapdoor.transform.position = pos.FloorWorldPosition;
			trapdoor.gameObject.SetActive(true);
			trapdoor.SetEC(ec);
			if (cooldown != -1) trapdoor.SetOpenCooldown(cooldown); // If new cooldown exists, set the trapdoor to it

			pos.HardCover(CellCoverage.Down | CellCoverage.Center);
			pos.AddRenderer(trapdoor.renderer);
			pos.AddRenderer(trapdoor.text.GetComponent<MeshRenderer>());
			ec.map.AddIcon(icon, trapdoor.transform, Color.white);

			return trapdoor;
		}

		[SerializeField]
		public Trapdoor trapDoorpre;

		[SerializeField]
		public int minimumDistanceFromATrapDoor = 10, maximumDistanceFromATrapdoor = 500;

		[SerializeField]
		public Sprite[] closedSprites; // 0 - random, 1 - linked

		[SerializeField]
		public Sprite[] openSprites;

		internal static MapIcon icon;
		DijkstraMap map;
	}
}
