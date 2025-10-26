using System;
using System.Collections.Generic;
using System.Linq;
using BBTimes.CompatibilityModule.EditorCompat;
using BBTimes.CustomComponents;
using BBTimes.Extensions;
using BBTimes.Manager;
using PixelInternalAPI.Classes;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.CustomContent.Builders;

public class Structure_OutsideBox : StructureBuilder, IBuilderPrefab
{
	public StructureWithParameters SetupBuilderPrefabs()
	{
		// Makes the LoaderStructureData for the spawn
		LevelLoaderPlugin.Instance.structureAliases.Add(EditorIntegration.TimesPrefix + "OutsideBox", new LoaderStructureData(this));

		return new() { prefab = this, parameters = new() { chance = [1f, 0.25f] } };
	}
	public void SetupPrefab() { }
	public void SetupPrefabPost() { }

	public string Name { get; set; }
	public string Category => "objects";
	// Public/assignable defaults
	public static WindowObject window;
	Color outsideLighting;
	static Material[] mats = [];
	System.Random activeRng;
	public static GameObject[] decorations;
	// Cached static fields
	private static readonly List<Direction> allDirections = Directions.All();
	private const float WallOffsetDistance = (LayerStorage.TileBaseOffset / 2f) - 0.001f;
	private const float FenceOffsetDistance = (LayerStorage.TileBaseOffset / 2f) - 0.01f;

	public override void OnGenerationFinished(LevelBuilder lb)
	{
		base.OnGenerationFinished(lb);
		if (lb is LevelGenerator)
		{
			TryToGenerate(lb.controlledRNG);
			Finished();
		}
	}

	// Level loader patch call as workaround
	public void OnLoadingFinished(LevelLoader loader) =>
		GenerateInPremadeMap(loader.controlledRNG);


	public override void GenerateInPremadeMap(System.Random rng)
	{
		base.GenerateInPremadeMap(rng);
		TryToGenerate(rng);
		Finished();
	}

	void TryToGenerate(System.Random rng)
	{
		activeRng = rng;
		try
		{
			if (BBTimesManager.plug.disableOutside.Value)
				return;

			outsideLighting = Singleton<BaseGameManager>.Instance.GetComponent<MainGameManagerExtraComponent>()?.outsideLighting ?? Color.white;
			Debug.Log("TIMES: Creating windows for outside...");
			var spawnedWindows = CreateWindows();
			if (spawnedWindows.Count == 0)
			{
				Debug.LogWarning("TIMES: No spots were found for windows to spawn!");
				return;
			}

			Debug.Log("TIMES: Creating outside...");
			var planeCover = CreatePlaneCoverObject();

			// Dictionary mapping position to the combined-mesh GameObject.
			var generatedOutsideObjects = CreateCombinedOutsideMeshes(planeCover);

			var visibleObjects = CalculateVisibleObjects(spawnedWindows, generatedOutsideObjects);
			ApplyVisibilityAndCull(visibleObjects, generatedOutsideObjects);

			Debug.Log("TIMES: Outside created successfully!");
		}
		catch (Exception e)
		{
			Debug.LogError("TIMES: Failed to create outside!");
			Debug.LogException(e);
			throw;
		}
	}


	private List<Window> CreateWindows()
	{
		var spawnedWindows = new List<Window>();
		var tiles = GetValidWindowTiles();
		float minimumFactor = parameters?.chance?[1] ?? 0.25f;
		int amountOfWindows = Mathf.FloorToInt(tiles.Count * Mathf.Clamp((float)activeRng.NextDouble(), minimumFactor, 0.5f));

		for (int i = 0; i < amountOfWindows; i++)
		{
			if (tiles.Count == 0) break;

			int idx = activeRng.Next(tiles.Count);
			var tile = tiles[idx];

			var dirArr = tile.Value;
			if (dirArr == null || dirArr.Length == 0) continue;

			var dir = dirArr[activeRng.Next(dirArr.Length)];
			var w = ec.ForceBuildWindow(tile.Key, dir, window);
			if (w)
			{
				w.aTile.AddRenderer(w.windows[0]);
				spawnedWindows.Add(w);
			}
			tiles.RemoveAt(idx);
		}
		return spawnedWindows;
	}

	private List<KeyValuePair<Cell, Direction[]>> GetValidWindowTiles()
	{
		var tiles = new List<KeyValuePair<Cell, Direction[]>>();
		var cells = new List<Cell>();

		if (!ec.rooms.Contains(ec.mainHall)) // I think Studio add the mainHall as a room in the list
			cells.AddRange(ec.mainHall.GetNewTileList());

		for (int i = 0; i < ec.rooms.Count; i++)
			cells.AddRange(ec.rooms[i].GetNewTileList()); // Includes hallways and rooms

		foreach (var t in cells)
		{
			if (t.Null || t.Hidden || t.offLimits || !t.HasAllFreeWall)
				continue;

			var dirs = Directions.All();
			dirs.RemoveAll(x => !ec.CellFromPosition(t.position + x.ToIntVector2()).Null || t.WallAnyCovered(x));

			if (dirs.Count != 0)
				tiles.Add(new(t, [.. dirs]));
		}
		return tiles;
	}

	private GameObject CreatePlaneCoverObject()
	{
		var planeCover = new GameObject("PlaneCover");
		planeCover.transform.SetParent(ec.transform, false);
		return planeCover;
	}

	private Dictionary<IntVector2, GameObject> CreateCombinedOutsideMeshes(GameObject parent)
	{
		InitializeMaterials();
		var generatedObjects = new Dictionary<IntVector2, GameObject>();

		// Create the wall material once here instead of for every cell
		var wallMat = new Material(ec.mainHall.wallMat.shader) { mainTexture = ec.mainHall.wallTex };

		bool isFirstFloor = parameters?.chance?[0] == 1f;
		bool lastFloor = Singleton<BaseGameManager>.Instance.levelObject.finalLevel;
		System.Random decorationRng = new(activeRng.Next());

		var outsideCells = ec.AllExistentCells().Where(c => c.Null && !c.Hidden && !c.offLimits).ToList();

		foreach (var cell in outsideCells)
		{
			var cellGO = BuildCombinedMeshForCell(cell, parent, wallMat, isFirstFloor, lastFloor);
			if (cellGO)
			{
				generatedObjects.Add(cell.position, cellGO);
				if (isFirstFloor && decorations.Length != 0 && decorationRng.NextDouble() > 0.75d)
				{
					int amount = decorationRng.Next(1, 4);
					for (int i = 0; i < amount; i++)
					{
						var d = Instantiate(decorations[decorationRng.Next(decorations.Length)], cellGO.transform);
						d.transform.localPosition = new Vector3(((float)decorationRng.NextDouble() * 8f) - 4, -5f, ((float)decorationRng.NextDouble() * 8f) - 4);
					}
				}
			}
		}
		return generatedObjects;
	}

	private GameObject BuildCombinedMeshForCell(Cell cell, GameObject parent, Material wallMaterial, bool isFirstFloor, bool lastFloor)
	{
		var vertices = new List<Vector3>();
		var uvs = new List<Vector2>();

		var wallTriangles = new List<int>();
		var grassTriangles = new List<int>();
		var fenceTriangles = new List<int>();
		System.Text.StringBuilder stringBuilder = new($"Cell_{cell.position.x}_{cell.position.z}_");
		bool hasQualityDefined = false; // Check vertical walls, ground and fence

		// VERTICAL WALLS
		foreach (var dir in allDirections)
		{
			var neighbor = ec.CellFromPosition(cell.position + dir.ToIntVector2());
			if (!neighbor.Null)
			{
				hasQualityDefined = true;
				int max = lastFloor ? 6 : 25;
				int start = isFirstFloor ? 0 : -25;
				for (int i = start; i <= max; i++)
				{
					Vector3 quadPosition = (dir.ToVector3() * WallOffsetDistance) + (Vector3.up * LayerStorage.TileBaseOffset * i);
					AddQuad(vertices, wallTriangles, uvs, dir.ToRotation(), quadPosition);
				}
			}
		}

		if (hasQualityDefined)
			stringBuilder.Append("VerticalWall_");
		hasQualityDefined = false;

		if (isFirstFloor)
		{
			// GROUND
			AddQuad(vertices, grassTriangles, uvs, Quaternion.Euler(90f, 0f, 0f), Vector3.down * (LayerStorage.TileBaseOffset / 2f));
			stringBuilder.Append("Outside_");

			// FENCE
			foreach (var dir in allDirections)
			{
				if (ec.ContainsCoordinates(cell.position + dir.ToIntVector2())) continue;
				hasQualityDefined = true;
				Vector3 quadPosition = dir.ToVector3() * FenceOffsetDistance;
				AddQuad(vertices, fenceTriangles, uvs, dir.ToRotation(), quadPosition);
			}

			if (hasQualityDefined)
				stringBuilder.Append("Fenced");
		}

		if (vertices.Count == 0) return null;

		var go = new GameObject(stringBuilder.ToString());
		go.transform.SetParent(parent.transform, false);
		go.transform.position = cell.CenterWorldPosition;

		var mf = go.AddComponent<MeshFilter>();
		var mr = go.AddComponent<MeshRenderer>();

		AddDebugPosText(cell);

		var mesh = new Mesh
		{
			vertices = [.. vertices],
			uv = [.. uvs]
		};

		var currentMaterials = new List<Material>();
		int submeshIndex = 0;

		// Sum the submeshes by knowing how many type of meshes there are to be included in a single object
		mesh.subMeshCount = (wallTriangles.Count != 0 ? 1 : 0) +
							(grassTriangles.Count != 0 ? 1 : 0) +
							(fenceTriangles.Count != 0 ? 1 : 0);

		if (wallTriangles.Count != 0)
		{
			mesh.SetTriangles(wallTriangles, submeshIndex++);
			currentMaterials.Add(wallMaterial);
		}
		if (grassTriangles.Count != 0)
		{
			mesh.SetTriangles(grassTriangles, submeshIndex++);
			currentMaterials.Add(mats[0]);
		}
		if (fenceTriangles.Count != 0)
		{
			mesh.SetTriangles(fenceTriangles, submeshIndex++);
			currentMaterials.Add(mats[1]);
		}

		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		mf.mesh = mesh;
		mr.materials = [.. currentMaterials];

		Singleton<CoreGameManager>.Instance.UpdateLighting(outsideLighting, cell.position);
		for (int i = 0; i < mr.materials.Length; i++)
		{
			var mat = mr.materials[i];
			if (!mat.GetTexture("_LightMap")) // Manually add the light map if it doesn't exist
				mat.SetTexture("_LightMap", Singleton<CoreGameManager>.Instance.lightMapTexture);
		}
		return go;
	}

	private void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Quaternion rotation, Vector3 position)
	{
		int vCount = vertices.Count;
		float size = LayerStorage.TileBaseOffset / 2f;

		vertices.Add(rotation * new Vector3(-size, -size, 0) + position);
		vertices.Add(rotation * new Vector3(size, -size, 0) + position);
		vertices.Add(rotation * new Vector3(-size, size, 0) + position);
		vertices.Add(rotation * new Vector3(size, size, 0) + position);

		triangles.Add(vCount);
		triangles.Add(vCount + 2);
		triangles.Add(vCount + 1);
		triangles.Add(vCount + 2);
		triangles.Add(vCount + 3);
		triangles.Add(vCount + 1);

		uvs.Add(new Vector2(0, 0));
		uvs.Add(new Vector2(1, 0));
		uvs.Add(new Vector2(0, 1));
		uvs.Add(new Vector2(1, 1));
	}

	private void InitializeMaterials()
	{
		if (mats.Length != 0) return;
		var baseMat = BBTimesManager.man.Get<GameObject>("TransparentPlaneTemplate").GetComponent<MeshRenderer>().material;
		var grassMat = new Material(baseMat) { mainTexture = BBTimesManager.man.Get<Texture2D>("Tex_Grass") };
		var fenceMat = new Material(baseMat) { mainTexture = BBTimesManager.man.Get<Texture2D>("Tex_Fence") };
		mats = [grassMat, fenceMat];
	}

	private Dictionary<GameObject, List<Cell>> CalculateVisibleObjects(List<Window> spawnedWindows, Dictionary<IntVector2, GameObject> allGeneratedObjects)
	{
		var visibleObjectsMap = new Dictionary<GameObject, List<Cell>>();

		Debug.Log($"TIMES: Starting calculation for {spawnedWindows.Count} windows and {allGeneratedObjects.Count} outside objects.");

		// 1. Pre-calculate the FOV origins for all windows to avoid redundant math
		var windowFovPoints = new List<WindowFovData>(spawnedWindows.Count);
		foreach (var window in spawnedWindows)
		{
			// Ensure window has valid cells before processing
			if (!(window.aTile.Null ? window.bTile : window.aTile).Null)
			{
				AddDebugPosText(window.aTile);
				windowFovPoints.Add(new WindowFovData(window));
			}
		}

		// 2. Iterate through every single generated outside object
		foreach (var kvp in allGeneratedObjects)
		{
			IntVector2 objectPosition = kvp.Key;
			GameObject targetObject = kvp.Value;
			Vector3 targetCenter = ec.CellFromPosition(objectPosition).CenterWorldPosition;

			// 3. For each object, check if ANY window can see it
			bool objectIsVisible = false;
			foreach (var windowData in windowFovPoints)
			{
				// Debug.Log($"[Visibility] Checking if object at ({objectPosition.ToString()}) can be seen from window at ({windowData.CullingCell.position.ToString()})");
				// Perform the 3-ray FOV check from this window to the target object's center
				bool canSee = Raycast(windowData.Center, targetCenter) ||
							  Raycast(windowData.Corner1, targetCenter - windowData.cornerOffset) ||
							  Raycast(windowData.Corner2, targetCenter + windowData.cornerOffset);

				if (canSee)
				{
					// Debug.Log($"[Visibility] SUCCESS: Object at ({objectPosition.ToString()}) IS VISIBLE from window ({windowData.CullingCell.position.ToString()}).");
					// As soon as one window sees it, mark it as visible for that window
					if (visibleObjectsMap.TryGetValue(targetObject, out var cells))
						cells.Add(windowData.CullingCell);
					else
						visibleObjectsMap[targetObject] = [windowData.CullingCell]; // make sure the list is initialized

					objectIsVisible = true;
				}
			}
			if (!objectIsVisible)
			{
				// Debug.Log($"[Visibility] FAILURE: Object at ({objectPosition.ToString()}) was not visible from any window.");
			}
		}
		// Debug.Log($"[Visibility] Calculation complete. Found {visibleObjectsMap.Count} visible objects.");

		return visibleObjectsMap;
	}

	private void ApplyVisibilityAndCull(Dictionary<GameObject, List<Cell>> visibleObjectsMap, Dictionary<IntVector2, GameObject> allGeneratedObjects)
	{
		var nullCull = ec.CullingManager.GetComponent<NullCullingManager>();

		foreach (var kvp in allGeneratedObjects)
		{
			GameObject go = kvp.Value;
			if (visibleObjectsMap.TryGetValue(go, out var cullingCells))
			{
				// This object is visible. Add all its renderers to the culling cell
				var renderers = go.GetComponentsInChildren<Renderer>();
				foreach (var r in renderers)
					cullingCells.ForEach(cell => nullCull.AddRendererToCell(cell, r));
			}
			else
			{
				// This object is not visible from any window, destroy it.
				Destroy(go);
			}
		}
	}

	private bool Raycast(Vector3 start, Vector3 end)
	{
		Cell startCell = ec.CellFromPosition(start);
		Cell endCell = ec.CellFromPosition(end);

		if (startCell == endCell)
			return true;



		Vector3 direction = (end - start).normalized;

		IntVector2 currentMapPos = startCell.position;

		// distance ray has to travel to cross one cell boundary
		Vector2 deltaDist = new(
			Mathf.Abs(LayerStorage.TileBaseOffset / (direction.x == 0 ? float.Epsilon : direction.x)),
			Mathf.Abs(LayerStorage.TileBaseOffset / (direction.z == 0 ? float.Epsilon : direction.z))
		);

		// which direction to step in (+1 or -1)
		int stepX, stepZ;

		// length of ray from start to first intersection with a grid line
		float sideDistX, sideDistZ;

		// Calculate step and initial sideDist for X
		if (direction.x < 0)
		{
			stepX = -1;
			sideDistX = (start.x - (currentMapPos.x * LayerStorage.TileBaseOffset)) * deltaDist.x / LayerStorage.TileBaseOffset;
		}
		else
		{
			stepX = 1;
			sideDistX = ((currentMapPos.x + 1) * LayerStorage.TileBaseOffset - start.x) * deltaDist.x / LayerStorage.TileBaseOffset;
		}

		// Calculate step and initial sideDist for Z
		if (direction.z < 0)
		{
			stepZ = -1;
			sideDistZ = (start.z - (currentMapPos.z * LayerStorage.TileBaseOffset)) * deltaDist.y / LayerStorage.TileBaseOffset;
		}
		else
		{
			stepZ = 1;
			sideDistZ = ((currentMapPos.z + 1) * LayerStorage.TileBaseOffset - start.z) * deltaDist.y / LayerStorage.TileBaseOffset;
		}

		while (true)
		{
			// Advance to the next grid cell
			if (sideDistX < sideDistZ)
			{
				sideDistX += deltaDist.x;
				currentMapPos.x += stepX;
			}
			else
			{
				sideDistZ += deltaDist.y;
				currentMapPos.z += stepZ;
			}

			if (!ec.ContainsCoordinates(currentMapPos)) return false; // This should be the safety limit

			Cell currentCell = ec.CellFromPosition(currentMapPos);

			if (currentCell == endCell)
				return true;


			if (!currentCell.Null)
				return false;

		}
	}

	private readonly struct WindowFovData
	{
		public readonly Cell CullingCell;
		public readonly Vector3 Center;
		public readonly Vector3 Corner1;
		public readonly Vector3 Corner2;
		public readonly Vector3 cornerOffset;

		public WindowFovData(Window window)
		{
			CullingCell = window.aTile.Null ? window.bTile : window.aTile;
			Center = (window.aTile.CenterWorldPosition + window.bTile.CenterWorldPosition) * 0.5f;
			var perpendicularDir = Quaternion.Euler(0, 90, 0) * window.direction.ToVector3();
			cornerOffset = perpendicularDir * LayerStorage.TileBaseOffset * 0.5f; // Full tile half opening
			Corner1 = Center - cornerOffset;
			Corner2 = Center + cornerOffset;
		}
	}

	void AddDebugPosText(Cell t)
	{
		// Only uncomment this if utterly necessary
		// var text = new GameObject("DebugPositionText_(" + t.position.ToString() + ')').AddComponent<TMPro.TextMeshPro>();
		// text.gameObject.layer = LayerStorage.billboardLayer;

		// text.alignment = TMPro.TextAlignmentOptions.Center;
		// text.rectTransform.offsetMin = new(-4f, -3.99f);
		// text.rectTransform.offsetMax = new(4f, 4.01f);
		// text.transform.position = t.CenterWorldPosition;
		// text.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		// text.text = t.position.ToString();
	}
}
