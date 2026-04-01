using System.Collections.Generic;
using System.Linq;
using BBTimes.Extensions;
using BBTimes.Extensions.ObjectCreationExtensions;
using BBTimes.Manager;
using BBTimes.ModPatches.GeneratorPatches;
using BBTimes.Plugin;
using NewPlusDecorations.Components;
using PixelInternalAPI.Classes;
using PixelInternalAPI.Extensions;
using UnityEngine;
using UnityEngine.AI;

namespace BBTimes.CustomContent.RoomFunctions
{
    internal class HighCeilingRoomFunction : RoomFunction
    {
        public override void Build(LevelBuilder builder, System.Random rng)
        {
            base.Build(builder, rng);
            if (changed || ceilingHeight < 1 || rng.NextDouble() > chanceToHappen)
                return;

            // In Build phase, we just flag it. The actual mesh generation happens after all rooms are set.
            changed = true;
        }

        public override void Initialize(RoomController room)
        {
            base.Initialize(room);
            proof = LevelBuilderInstanceGrabber.i;
            originalCeilTex = room.ceilTex;
            ogCellBins.Clear();
            room.cells.ForEach(c => ogCellBins.Add(c, c.ConstBin));

            // Set lighting
            if (customLight != null)
                room.lightPre = customLight;
            else
                room.lightPre = BBTimesManager.EmptyGameObject.transform;
        }

        public override void OnGenerationFinished()
        {
            base.OnGenerationFinished();

            // If it wasn't triggered in Build (e.g., Level Loader), check again
            if (!changed && (!proof || proof is LevelLoader))
                changed = true;

            if (changed)
            {
                room.ceilTex = ObjectCreationExtension.transparentTex;
                room.GenerateTextureAtlas();

                // Generate the single combined mesh for walls and ceiling
                GenerateCombinedMesh(proof is LevelLoader);

                // Handle object extensions
                ExtendRoomObjects();

                // Update base material for transparency if needed
                foreach (var c in room.cells)
                    c.SetBase(c.Tile.MeshRenderer.material.name.StartsWith(room.defaultPosterMat.name) ? room.posterMat : room.baseMat);
            }
        }

        void GenerateCombinedMesh(bool levelLoader = false)
        {
            var holder = new GameObject("HighCeiling_CombinedMesh");
            holder.transform.SetParent(room.transform, false);
            holder.transform.localPosition = Vector3.zero;

            // Pre-generate materials
            var wallMaterials = new List<Material>();
            var baseWallTex = usesSingleCustomWall ? customWallProximityToCeil[0] : room.wallTex;
            var baseWallMat = new Material(room.defaultAlphaMat) { mainTexture = TextureExtensions.GenerateTextureAtlas(ObjectCreationExtension.transparentTex, baseWallTex, ObjectCreationExtension.transparentTex) };
            baseWallMat.SetTexture(Storage.SPRITESTANDARD_LIGHTMAP, Singleton<CoreGameManager>.Instance.lightMapTexture);

            for (int i = 0; i < ceilingHeight; i++)
            {
                if (i >= ceilingHeight - customWallProximityToCeil.Length)
                {
                    int customIndex = i - (ceilingHeight - customWallProximityToCeil.Length);
                    var customTex = customWallProximityToCeil[customIndex];
                    var customMat = new Material(room.defaultAlphaMat) { mainTexture = TextureExtensions.GenerateTextureAtlas(ObjectCreationExtension.transparentTex, customTex, ObjectCreationExtension.transparentTex) };
                    customMat.SetTexture(Storage.SPRITESTANDARD_LIGHTMAP, Singleton<CoreGameManager>.Instance.lightMapTexture);
                    wallMaterials.Add(customMat);
                }
                else
                {
                    wallMaterials.Add(baseWallMat);
                }
            }

            // Prepare ceiling material (if needed) once
            Material ceilMat = null;
            if (hasCeiling)
            {
                var ceilTex = customCeiling ?? originalCeilTex;
                ceilMat = new Material(room.defaultAlphaMat) { mainTexture = TextureExtensions.GenerateTextureAtlas(ceilTex, ObjectCreationExtension.transparentTex, ObjectCreationExtension.transparentTex) };
                ceilMat.SetTexture(Storage.SPRITESTANDARD_LIGHTMAP, Singleton<CoreGameManager>.Instance.lightMapTexture);
            }

            // For each cell, create a separate mesh GameObject
            foreach (var c in room.cells)
            {
                // Per-cell geometry lists and per-material triangle lists
                var cellVertices = new List<Vector3>();
                var cellUvs = new List<Vector2>();
                var trianglesPerMaterial = new Dictionary<Material, List<int>>();

                // WALLS stacked
                for (int i = 1; i <= ceilingHeight; i++)
                {
                    var currentWallMat = wallMaterials[i - 1];
                    if (!trianglesPerMaterial.ContainsKey(currentWallMat))
                        trianglesPerMaterial.Add(currentWallMat, []);

                    int ogbin = ogCellBins.TryGetValue(c, out int val) ? val : 0;

                    if (levelLoader)
                    {
                        var dirs = Directions.All();
                        for (int d = 0; d < dirs.Count; d++)
                            if (!ogbin.IsBitSet(dirs[d].BitPosition()) && (c.doorDirs.Contains(dirs[d]) || !c.NavNavigable(dirs[d])))
                                ogbin = ogbin.ToggleBit(dirs[d].BitPosition());
                    }

                    if (ogbin == 0) continue;

                    Mesh sourceMesh = room.ec.TileMesh(ogbin);
                    Vector3 positionOffset = Vector3.up * (LayerStorage.TileBaseOffset * i); // vertical offset only; cell GameObject will be positioned at floor
                    AddMeshData(sourceMesh, cellVertices, cellUvs, trianglesPerMaterial[currentWallMat], positionOffset);
                }

                // CEILING for this cell
                if (hasCeiling && ceilMat != null)
                {
                    if (!trianglesPerMaterial.ContainsKey(ceilMat))
                        trianglesPerMaterial.Add(ceilMat, []);

                    Mesh sourceMesh = room.ec.TileMesh(0); // Open tile mesh for the ceiling plane
                    Vector3 positionOffset = Vector3.up * (ceilingHeight * LayerStorage.TileBaseOffset);
                    AddMeshData(sourceMesh, cellVertices, cellUvs, trianglesPerMaterial[ceilMat], positionOffset);
                }

                // If no geometry for this cell, skip creating a GameObject
                if (cellVertices.Count == 0)
                    continue;

                // Build the mesh for this cell
                var mesh = new Mesh
                {
                    vertices = [.. cellVertices],
                    uv = [.. cellUvs]
                };

                var materials = trianglesPerMaterial.Keys.ToList();
                mesh.subMeshCount = materials.Count;

                for (int i = 0; i < materials.Count; i++)
                {
                    mesh.SetTriangles(trianglesPerMaterial[materials[i]], i);
                }

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                // Create the cell GameObject
                var cellHolder = new GameObject($"HighCeiling_Cell");
                cellHolder.transform.SetParent(holder.transform, false);
                // Place at cell floor position (world position). holder is child of room, so set world position.
                cellHolder.transform.position = c.FloorWorldPosition;

                var mf = cellHolder.AddComponent<MeshFilter>();
                var mr = cellHolder.AddComponent<MeshRenderer>();
                mf.mesh = mesh;
                mr.materials = [.. materials];

                // Add the new renderer to this specific cell for culling/lighting
                c.AddRenderer(mr);
            }
        }

        private void AddMeshData(Mesh sourceMesh, List<Vector3> allVertices, List<Vector2> allUvs, List<int> allTriangles, Vector3 positionOffset)
        {
            int vertexOffset = allVertices.Count;

            // Add vertices and UVs, applying the position offset
            foreach (var vert in sourceMesh.vertices)
                allVertices.Add(vert + positionOffset);
            allUvs.AddRange(sourceMesh.uv);

            // Add triangles, applying the vertex offset
            foreach (var tri in sourceMesh.triangles)
                allTriangles.Add(tri + vertexOffset);
        }

        void ExtendRoomObjects()
        {
            var objects = room.objectObject;
            if (!string.IsNullOrEmpty(targetTransformNamePrefix) && targetTransformOffset > 0f)
                SearchChildsBasedOnCriteria(objects.transform, obj => obj.name.StartsWith(targetTransformNamePrefix), null, targetTransformOffset);

            // Search for columns
            SearchChildsBasedOnCriteria(objects.transform,
                obj => obj.GetComponent<Column>() != null,
                (clone, ceilIndex) =>
                {
                    Texture2D selectedTexture = room.wallTex;
                    // If custom array is missing or empty, fall back to the room wall texture
                    if (customWallProximityToCeil != null && customWallProximityToCeil.Length != 0)
                    {
                        // If flagged, use the first entry
                        if (usesSingleCustomWall)
                        {
                            selectedTexture = customWallProximityToCeil[0];
                        }
                        else
                        {
                            // Otherwise, do the basic proximity handling
                            int customCount = customWallProximityToCeil.Length;
                            int threshold = ceilingHeight - customCount;
                            if (ceilIndex > threshold)
                            {
                                int customIndex = ceilIndex - threshold - 1;
                                if (customIndex >= 0 && customIndex < customCount)
                                    selectedTexture = customWallProximityToCeil[customIndex];
                            }
                        }
                    }
                    // Update renderers (guard against missing material)
                    foreach (var renderer in clone.GetComponentsInChildren<Renderer>())
                        renderer.material.mainTexture = selectedTexture;
                },
                LayerStorage.TileBaseOffset);
        }

        void SearchChildsBasedOnCriteria(Transform objects, System.Predicate<Transform> predicate, System.Action<Transform, int> onInstantiate, float offset)
        {
            foreach (var obj in objects.AllChilds())
            {
                if (!predicate(obj))
                    continue;

                for (int i = 1; i <= ceilingHeight; i++)
                {
                    var clone = Instantiate(obj, objects);
                    clone.name = obj.name;
                    clone.position = obj.transform.position + (Vector3.up * (i * offset));
                    clone.rotation = obj.transform.rotation;
                    onInstantiate?.Invoke(clone, i);

                    var collider = clone.GetComponent<Collider>();
                    if (collider)
                        Destroy(collider);

                    var nav = clone.GetComponent<NavMeshObstacle>();
                    if (nav)
                        Destroy(nav);
                }
            }
        }


        LevelBuilder proof;
        Texture2D originalCeilTex;
        bool changed = false;
        public bool HasGeneratedCeiling => changed;

        [SerializeField]
        public string targetTransformNamePrefix = string.Empty;
        [SerializeField]
        public float targetTransformOffset = 1f;
        [SerializeField]
        public int ceilingHeight = 1;
        [SerializeField]
        public bool hasCeiling = true, usesSingleCustomWall = false;
        [SerializeField]
        public Transform customLight = null;
        [SerializeField]
        public float chanceToHappen = 1f;
        [SerializeField]
        public Texture2D customCeiling = null;
        [SerializeField]
        public Texture2D[] customWallProximityToCeil = [];

        readonly Dictionary<Cell, int> ogCellBins = [];
    }
}
