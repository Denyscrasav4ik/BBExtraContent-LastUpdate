using System.Collections.Generic;
using BBTimes.CompatibilityModule.EditorCompat;
using BBTimes.CustomComponents;
using BBTimes.CustomContent.Objects;
using BBTimes.Extensions;
using BBTimes.Extensions.ObjectCreationExtensions;
using BBTimes.ModPatches.EnvironmentPatches;
using MTM101BaldAPI;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Extensions;
using PlusStudioLevelLoader;
using UnityEngine;


namespace BBTimes.CustomContent.Builders
{
	public class Structure_Duct : StructureBuilder, IBuilderPrefab
	{
		public StructureWithParameters SetupBuilderPrefabs()
		{
			// Making of the main vent
			var vent = new GameObject("Duct", typeof(Duct)) { layer = LayerStorage.ignoreRaycast };
			vent.AddBoxCollider(Vector3.zero, new(9.99f, 10f, 9.99f), true);

			var blockObj = new GameObject("VentPrefab_RaycastBlock");
			blockObj.transform.SetParent(vent.transform);
			blockObj.transform.localPosition = Vector3.zero;
			blockObj.transform.localScale = new(1.2f, 10f, 1.2f);

			var box2 = blockObj.AddBoxCollider(Vector3.zero, Vector3.one * 10f, true);
			box2.enabled = false;

			blockObj.layer = LayerMask.NameToLayer("Block Raycast");

			Texture2D[] texs = [
				this.GetTexture("vent.png"),
				this.GetTexture("vent_1.png"),
				this.GetTexture("vent_2.png"),
				this.GetTexture("vent_3.png")
				];

			var visual = ObjectCreationExtension.CreateCube(texs[0]);
			Destroy(visual.GetComponent<BoxCollider>()); // Removes the collider

			vent.ConvertToPrefab(true);

			var v = vent.GetComponent<Duct>();
			v.renderer = visual.GetComponent<MeshRenderer>();
			v.ventTexs = texs;
			v.normalVentAudioMan = vent.CreatePropagatedAudioManager(2f, 25f); // Two propagated audio managers
			v.gasLeakVentAudioMan = vent.CreatePropagatedAudioManager(2f, 25f);
			v.ventAudios = [this.GetSoundNoSub("vent_normal.wav", SoundType.Effect),
				this.GetSound("vent_gasleak_start.wav", "Vfx_VentGasLeak", SoundType.Effect, Color.white),
				this.GetSound("vent_gasleak_loop.wav", "Vfx_VentGasLeak", SoundType.Effect, Color.white),
				this.GetSound("vent_gasleak_end.wav", "Vfx_VentGasLeak", SoundType.Effect, Color.white)];
			v.colliders = [box2];

			visual.transform.SetParent(vent.transform);
			visual.transform.localPosition = new Vector3(-4.95f, 9f, -4.95f);
			visual.transform.localScale = new Vector3(9.9f, 1f, 9.9f);

			ventPrefab = vent;

			// Making of particles

			var particle = GameExtensions.GetNewParticleSystem();
			particle.gameObject.name = "VentPrefab_ParticleEmitter";
			particle.transform.SetParent(vent.transform);
			particle.transform.localPosition = Vector3.up * 10f;
			particle.transform.rotation = Quaternion.Euler(0f, 0f, 0f);


			var m = particle.main;
			m.gravityModifier = -4f;
			m.cullingMode = ParticleSystemCullingMode.Automatic;
			m.startLifetimeMultiplier = 2.1f;
			m.startSize = 3f;


			var e = particle.emission;
			e.enabled = true;
			e.rateOverTime = 0f;

			var vp = particle.velocityOverLifetime;
			vp.enabled = true;
			vp.x = new(-5f, 5f);
			vp.z = new(-11f, 5f);
			vp.y = new(-24f, -15f);
			vp.radialMultiplier = 1.5f;
			vp.space = ParticleSystemSimulationSpace.World;

			var vs = particle.rotationBySpeed;
			vs.enabled = true;
			vs.z = 1.5f;
			vs.range = new(0f, 5f);

			v.particle = particle;

			particle.GetComponent<ParticleSystemRenderer>().material = ObjectCreationExtension.defaultDustMaterial;

			// Making of vent connections

			var connection = ObjectCreationExtension.CreateCube(this.GetTexture("ventConnection.png"), false);
			Destroy(connection.GetComponent<BoxCollider>());
			connection.name = "VentPrefab_Connection";
			connection.transform.localScale = new(connectionSize, 0.6f, connectionSize);


			ventConnectionPrefab = connection;



			var connection2 = Instantiate(connection);

			for (int i = 0; i < 4; i++)
			{
				var dir = Directions.All()[i];
				var con = Instantiate(connection2, connection.transform);
				con.transform.localPosition = dir.ToVector3() * 1.5f;
				con.transform.rotation = dir.ToRotation();
				con.transform.localScale = new Vector3(1f, 1f, 2.01f);
				con.name = "VentPrefab_Connection_" + dir;
				con.SetActive(false);
				Destroy(con.GetComponent<BoxCollider>());
			}

			Destroy(connection2);
			connection.ConvertToPrefab(true);


			// Makes the LoaderStructureData for the spawn
			LevelLoaderPlugin.Instance.structureAliases.Add(EditorIntegration.TimesPrefix + "Duct", new() { structure = this });

			return new() { prefab = this, parameters = new() { chance = [1 / 7f, 1 / 4f], minMax = [new(3, 8), new(1, 2)] } }; // Chance= [factorToReduceVentsOverLevelSize, factorToReduceWebSizeOverLevelSize], minMax=[AmountOfDucts, AmountOfWebs]
		}

		public void SetupPrefab() { }
		public void SetupPrefabPost() { }

		const float connectionSize = 2f;

		public string Name { get; set; }
		public string Category => "objects";




		// ^^
		public override void PostOpenCalcGenerate(LevelGenerator lg, System.Random rng)
		{
			base.PostOpenCalcGenerate(lg, rng);

			var room = lg.Ec.mainHall; // hallway

			// Find suitable corner/single tiles for duct placement
			List<Cell> candidateCells = room.GetTilesOfShape(TileShapeMask.Corner | TileShapeMask.Single, false);
			if (candidateCells.Count == 0)
			{
				Debug.LogWarning("Structure_Duct failed to find any good spots to spawn them.");
				Finished();
				return;
			}
			int webAmount = rng.Next(parameters.minMax[1].x, parameters.minMax[1].z);
			List<Region> webRegions = [];
			// Offset
			IntVector2 offset = new(Mathf.FloorToInt(lg.levelSize.x * parameters.chance[1]), Mathf.FloorToInt(lg.levelSize.z * parameters.chance[1]));

			for (int webCounter = 0; webCounter < webAmount; webCounter++)
			{
				// Calculate number of vents based on level size
				int ventAmount = Mathf.FloorToInt(Mathf.Clamp(lg.levelSize.x * lg.levelSize.z * parameters.chance[0], parameters.minMax[0].x, parameters.minMax[0].z));

				for (int i = 0; i < webRegions.Count; i++)
				{
					candidateCells.RemoveAll(cell =>
						webRegions[i].InsideRegion(cell.position) || // If there's a cell inside any of the regions, delete them
						new Region(cell.position - offset, cell.position + offset).Intersects(webRegions[i]) // If the region from the cell intersects the other, it should be removed too
						);
				}

				if (candidateCells.Count == 0)
					break;

				// Select a central point for the duct web
				Cell webCenter = candidateCells[rng.Next(candidateCells.Count)];

				// Define a localized region around the center
				Region webRegion = new(
					webCenter.position - offset,
					webCenter.position + offset
				);

				// Clamp region to the level size
				webRegion.min = IntVector2.CombineGreatest(webRegion.min, new IntVector2(0, 0)); // Max function but with other name
				webRegion.max = IntVector2.CombineLowest(webRegion.max, lg.levelSize - new IntVector2(1, 1)); // Min function

				webRegions.Add(webRegion);

				// Find cells within the localized region
				List<Cell> webCells = [];
				for (int x = webRegion.min.x; x <= webRegion.max.x; x++)
				{
					for (int z = webRegion.min.z; z <= webRegion.max.z; z++)
					{
						Cell cell = ec.cells[x, z];
						if (cell.TileMatches(room) && !cell.HasAnyHardCoverage && !cell.open && // Open and with 0 hard coverage
							!cell.doorHere && (cell.shape.HasFlag(TileShapeMask.Corner) || cell.shape.HasFlag(TileShapeMask.Single)) && // should have the same shape
							!lg.Ec.TrapCheck(cell)) // And shall not be a trap
						{
							webCells.Add(cell);
						}

						candidateCells.Remove(cell); // Where the web reaches won't be suitable places for the candidateCells
					}
				}

				if (webCells.Count == 0)
				{
					Debug.LogWarning("Structure_Duct failed to find any good spots in the localized region.");
					continue;
				}

				// Randomly select cells for vents from the localized region
				List<Cell> selectedCells = [];
				while (selectedCells.Count < ventAmount && webCells.Count != 0)
				{
					int randomIndex = rng.Next(webCells.Count);
					selectedCells.Add(webCells[randomIndex]);
					webCells.RemoveAt(randomIndex);
				}

				// Create vents and group them
				List<Duct> webVents = CreateVentsInCells(selectedCells);

				if (webVents.Count == 0)
				{
					Debug.LogWarning("Structure_Duct failed to create any vents.");
					continue;
				}

				// Connect vents within the same web
				ConnectVentsInWeb(webVents);

				// Block the first vent to prevent NPCs from using it immediately
				webVents[0].BlockMe();
			}

			Finished();
		}

		public override void Load(List<StructureData> data)
		{
			base.Load(data);

			if (data.Count == 0)
			{
				Finished();
				return;
			}

			// Group ducts by their data field (web ID)
			Dictionary<int, List<StructureData>> webGroups = [];
			foreach (StructureData structureData in data)
			{
				if (!webGroups.ContainsKey(structureData.data))
				{
					webGroups[structureData.data] = [];
					// Debug.Log($"Structure_Duct: Unique web ID found ({structureData.data})");
				}

				webGroups[structureData.data].Add(structureData);
			}

			// Process each web group separately
			foreach (var webGroup in webGroups)
			{
				List<StructureData> webData = webGroup.Value;

				// Create vents for this web group
				List<Duct> webVents = [];
				foreach (StructureData structureData in webData)
				{
					Cell cell = ec.CellFromPosition(structureData.position);
					if (cell == null) continue;

					GameObject vent = Instantiate(ventPrefab, cell.ObjectBase);
					vent.transform.position = cell.FloorWorldPosition;
					vent.SetActive(true);

					Duct duct = vent.GetComponent<Duct>();
					duct.ec = ec;
					cell.HardCoverEntirely();
					cell.AddRenderer(duct.renderer);
					cell.AddRenderer(duct.particle.GetComponentInChildren<ParticleSystemRenderer>());
					duct.Initialize();

					webVents.Add(duct);
				}

				if (webVents.Count == 0) continue;

				// Connect vents within this web group
				ConnectVentsInWeb(webVents);

				// Block the first vent in this web
				webVents[0].BlockMe();
			}

			Finished();
		}

		// Creates duct vents in the specified cells and assigns them to a web group
		private List<Duct> CreateVentsInCells(List<Cell> cells)
		{
			List<Duct> vents = [];

			foreach (Cell cell in cells)
			{
				GameObject vent = Instantiate(ventPrefab, cell.ObjectBase);
				vent.transform.position = cell.FloorWorldPosition;
				vent.SetActive(true);

				Duct duct = vent.GetComponent<Duct>();
				duct.ec = ec;
				cell.HardCoverEntirely();
				cell.AddRenderer(duct.renderer);
				cell.AddRenderer(duct.particle.GetComponent<ParticleSystemRenderer>());
				duct.Initialize();

				vents.Add(duct);
			}

			return vents;
		}

		// Connects all vents within a web group using pathfinding and connection pieces
		private void ConnectVentsInWeb(List<Duct> webVents)
		{
			// Get all the possible room types and categories these vents can have based on their locations
			RoomCategory[] allowedRoomCategories = [.. webVents.ConvertAll(vent => ec.CellFromPosition(vent.transform.position).room.category)];
			RoomType[] allowedRoomTypes = [.. webVents.ConvertAll(vent => ec.CellFromPosition(vent.transform.position).room.type)];
			bool[] successConnection = new bool[webVents.Count];

			// Connect each vent to every other vent in the same web
			for (int i = 0; i < webVents.Count; i++)
			{
				if (successConnection[i]) continue; // A connection should not be done twice with another vent

				var vent = webVents[i];
				Cell centerCell = ec.CellFromPosition(vent.transform.position);

				for (int j = 0; j < webVents.Count; j++)
				{
					var targetVent = webVents[j];
					// If the vent already had a successfull connection
					if (targetVent == vent || successConnection[j]) continue; // Skip self-connection

					Cell targetCell = ec.CellFromPosition(targetVent.transform.position);

					// Use pathfinding limited to hallways
					EnvironmentControllerPatch.SetNewData(
						null,
						allowedRoomCategories,
						allowedRoomTypes,
						true
					);

					ec.FindPath(centerCell, targetCell, PathType.Const, out List<Cell> path, out bool success);
					EnvironmentControllerPatch.ResetData();

					if (!success) // No managed success, we skip here
						continue;

					successConnection[i] = true; // Two connections are established
					successConnection[j] = true; // then both are satisfied

					vent.nextVents.Add(targetVent); // Make literal connection by nextVents too
					targetVent.nextVents.Add(vent);

					// Place connection pieces along the path
					foreach (Cell pathCell in path)
					{
						if (allConnectionPositions.ContainsKey(pathCell.position)) continue;

						GameObject connection = Instantiate(ventConnectionPrefab, pathCell.ObjectBase);
						pathCell.AddRenderer(connection.GetComponent<MeshRenderer>());
						connection.transform.localPosition = Vector3.up * 9.5f;
						connection.SetActive(true);
						pathCell.HardCover(CellCoverage.Up);

						allConnectionPositions.Add(pathCell.position, connection);

						// Connect to adjacent connection pieces
						ConnectToAdjacentPieces(pathCell, connection);

						// Add renderers for all child connection pieces
						foreach (Transform child in connection.transform.AllChilds())
						{
							MeshRenderer childRenderer = child.GetComponent<MeshRenderer>();
							if (childRenderer != null)
								pathCell.AddRenderer(childRenderer);
						}
					}
				}
			}
		}

		// Connects a connection piece to adjacent pieces in the network
		private void ConnectToAdjacentPieces(Cell cell, GameObject connection)
		{
			List<Cell> neighbors = [];
			ec.GetNavNeighbors(cell, neighbors, PathType.Const);

			foreach (Cell neighbor in neighbors)
			{
				if (allConnectionPositions.TryGetValue(neighbor.position, out GameObject adjacentConnection))
				{
					// Calculate direction and activate appropriate connection arms
					Direction direction = Directions.DirFromVector3(
						adjacentConnection.transform.position - connection.transform.position,
						45f
					);

					Direction oppositeDirection = direction.GetOpposite();

					// Activate connection arm on current piece
					Transform connectionArm = connection.transform.Find("VentPrefab_Connection_" + direction);
					connectionArm?.gameObject.SetActive(true);

					// Activate corresponding arm on adjacent piece
					Transform adjacentArm = adjacentConnection.transform.Find("VentPrefab_Connection_" + oppositeDirection);
					adjacentArm?.gameObject.SetActive(true);
				}
			}
		}

		readonly Dictionary<IntVector2, GameObject> allConnectionPositions = [];

		[SerializeField]
		public GameObject ventPrefab;

		[SerializeField]
		public GameObject ventConnectionPrefab;
	}
}
