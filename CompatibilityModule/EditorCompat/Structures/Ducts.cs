using System.Collections.Generic;
using System.IO;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Structures
{
    public class DuctPlaceTool : PointTool
    {
        public override string id => "structure_times_duct_place";

        public DuctPlaceTool(Sprite icon) { sprite = icon; }

        protected override bool TryPlace(IntVector2 position)
        {
            var structure = (DuctStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "Duct", true); // Super important to be true, to have a good connection handling
            var duct = structure.CreateDuctLocation();
            duct.position = position;

            if (!duct.ValidatePosition(EditorController.Instance.levelData))
            {
                structure.DeleteDuct(duct);
                if (structure.ducts.Count == 0)
                    EditorController.Instance.levelData.structures.Remove(structure);
                return false;
            }

            EditorController.Instance.AddUndo();
            structure.ducts.Add(duct);
            EditorController.Instance.AddVisual(duct);
            return true;
        }
    }

    public class DuctConnectTool : EditorTool
    {
        public override string id => "structure_times_duct_connect";

        private DuctLocation _firstDuct = null;
        private DuctLocation _highlightedDuct = null;
        public DuctConnectTool(Sprite icon) { sprite = icon; }

        public override void Begin()
        {
            DuctEditorVisualManager.ShowConnections = true;
            UpdateAllDuctVisuals();
        }

        public override void Exit()
        {
            DuctEditorVisualManager.ShowConnections = false;
            UpdateAllDuctVisuals();
            UnhighlightDucts(forReal: true);
            _firstDuct = null;
            _highlightedDuct = null;
        }

        public override bool Cancelled()
        {
            UnhighlightDucts(forReal: true);
            if (_firstDuct != null)
            {
                _firstDuct = null;
                return false;
            }
            return true;
        }

        public override bool MousePressed()
        {
            var duct = GetDuctUnderCursor();
            if (duct == null)
                return false;

            if (_firstDuct == null)
            {
                _firstDuct = duct;
                HighlightDuct(_firstDuct, "yellow");
            }
            else
            {
                if (_firstDuct == duct) // Deselect if clicking the same duct
                {
                    _firstDuct = null;
                    UnhighlightDucts();
                    return false;
                }

                var structure = _firstDuct.owner;
                if (structure.connections.Exists(c => (c.ductA == _firstDuct && c.ductB == duct) || (c.ductA == duct && c.ductB == _firstDuct)))
                    EditorController.Instance.AddUndo();
                structure.connections.Add(new(_firstDuct, duct));
                UpdateAllDuctVisuals();
                UnhighlightDucts(forReal: true);
                _firstDuct = null;
                _highlightedDuct = null;
            }
            return false;
        }

        public override bool MouseReleased() => false;

        public override void Update()
        {
            var duct = GetDuctUnderCursor();

            if (_highlightedDuct != duct)
            {
                UnhighlightDucts();
                _highlightedDuct = duct;
                if (_highlightedDuct != null)
                    HighlightDuct(_highlightedDuct, "green");

            }
        }

        private DuctLocation GetDuctUnderCursor()
        {
            if (Physics.Raycast(EditorController.Instance.mouseRay, out var hit, 1000f, LevelStudioPlugin.editorInteractableLayerMask))
                if (hit.transform.TryGetComponent<DuctEditorVisualManager>(out var manager))
                    return manager.myLocation;

            return null;
        }

        private void HighlightDuct(DuctLocation duct, string color)
        {
            if (duct == null) return;
            EditorController.Instance.GetVisual(duct)?.GetComponent<EditorRendererContainer>().Highlight(color);
        }
        private void UnhighlightDucts(bool forReal = false)
        {
            if (_highlightedDuct != null)
                HighlightDuct(_highlightedDuct, "none");
            if (_firstDuct != null)
                HighlightDuct(_firstDuct, forReal ? "none" : "yellow");

        }

        private void UpdateAllDuctVisuals()
        {
            var structure = (DuctStructureLocation)EditorController.Instance.GetStructureData(EditorIntegration.TimesPrefix + "Duct");
            if (structure == null) return;

            foreach (var duct in structure.ducts)
            {
                var visual = EditorController.Instance.GetVisual(duct);
                visual?.GetComponent<DuctEditorVisualManager>().UpdateConnections();
            }
        }

    }
    public class DuctStructureLocation : StructureLocation
    {
        public readonly List<DuctLocation> ducts = [];

        // The index order of ducts is used when serializing/deserializing connections
        public readonly List<DuctConnection> connections = [];

        public override void AddStringsToCompressor(StringCompressor compressor) { }
        public override void CleanupVisual(GameObject visualObject)
        {
            for (int i = 0; i < ducts.Count; i++)
                EditorController.Instance.RemoveVisual(ducts[i]);

        }

        public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
        {
            StructureInfo info = new(type);
            if (ducts.Count == 0)
            {
                Debug.LogWarning("Compiling Ducts with no ducts assigned.");
                return info;
            }

            HashSet<DuctLocation> visited = [];
            int currentWebId = 0;

            foreach (var startDuct in ducts)
            {
                // If this duct is already assigned to a web, skip it
                if (visited.Contains(startDuct))
                    continue;

                // BFS queue seeded with the starting duct for this component
                Queue<DuctLocation> queue = new();
                queue.Enqueue(startDuct);
                visited.Add(startDuct);

                // Process all reachable ducts from startDuct and tag them with currentWebId
                while (queue.Count > 0)
                {
                    var currentDuct = queue.Dequeue();

                    // data compile here
                    info.data.Add(new()
                    {
                        data = currentWebId,
                        position = currentDuct.position.ToData()
                    });

                    // Explore neighboring ducts connected by an edge.
                    foreach (var connection in connections)
                    {
                        if (!connection.Connects(currentDuct)) continue;

                        var neighbor = connection.GetOther(currentDuct);
                        // Neighbor might be null or already visited; skip in both cases
                        if (neighbor != null && !visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                currentWebId++;
            }

            return info;
        }

        public override GameObject GetVisualPrefab() => null;

        public override void InitializeVisual(GameObject visualObject)
        {
            foreach (var duct in ducts)
                EditorController.Instance.AddVisual(duct);

        }

        public override void ReadInto(EditorLevelData data, BinaryReader reader, StringCompressor compressor)
        {
            _ = reader.ReadByte(); // Version
                                   // Read duct points: first the count, then each position encoded as a bytevector2
            int ductCount = reader.ReadInt32();
            for (int i = 0; i < ductCount; i++)
                ducts.Add(new() { owner = this, position = PlusStudioLevelLoader.Extensions.ToInt(reader.ReadByteVector2()) });

            // Read connections: stored as pairs of indices into the ducts list
            int connectionCount = reader.ReadInt32();
            for (int i = 0; i < connectionCount; i++)
            {
                int idx1 = Mathf.Max(0, Mathf.Min(ducts.Count - 1, reader.ReadInt32()));
                int idx2 = Mathf.Max(0, Mathf.Min(ducts.Count - 1, reader.ReadInt32()));
                if (ducts.Count != 0) // To not worry about index out of range
                    connections.Add(new(ducts[idx1], ducts[idx2]));
            }
        }

        public override void ShiftBy(Vector3 worldOffset, IntVector2 cellOffset, IntVector2 sizeDifference)
        {
            foreach (var duct in ducts)
                duct.position -= cellOffset;

        }

        public override void UpdateVisual(GameObject visualObject)
        {
            foreach (var duct in ducts)
                EditorController.Instance.UpdateVisual(duct);

        }

        public override bool ValidatePosition(EditorLevelData data)
        {
            for (int i = ducts.Count - 1; i >= 0; i--)
            {
                if (!ducts[i].ValidatePosition(data))
                    DeleteDuct(ducts[i]);
            }
            return ducts.Count != 0;
        }

        public override void Write(EditorLevelData data, BinaryWriter writer, StringCompressor compressor)
        {
            writer.Write((byte)0);
            // Serialize ducts as a count followed by each duct's position encoded to a bytevector
            writer.Write(ducts.Count);
            foreach (var duct in ducts)
                writer.Write(PlusStudioLevelLoader.Extensions.ToByte(duct.position));

            // Serialize connections
            writer.Write(connections.Count);
            foreach (var connection in connections)
            {
                writer.Write(Mathf.Max(0, ducts.IndexOf(connection.ductA)));
                writer.Write(Mathf.Max(0, ducts.IndexOf(connection.ductB)));
            }
        }

        public void DeleteDuct(DuctLocation duct)
        {
            EditorController.Instance.RemoveVisual(duct);
            connections.RemoveAll(c => c.Connects(duct));
            ducts.Remove(duct);
            foreach (var d in ducts)
                EditorController.Instance.UpdateVisual(d);

        }

        public DuctLocation CreateDuctLocation() => new() { owner = this };
    }

    public class DuctLocation : IEditorVisualizable, IEditorDeletable
    {
        public IntVector2 position;
        public DuctStructureLocation owner;

        public void CleanupVisual(GameObject visualObject) { }

        public bool ValidatePosition(EditorLevelData data)
        {
            PlusStudioLevelFormat.Cell cell = data.GetCellSafe(position);
            return cell != null && cell.roomId != 0;
        }

        public GameObject GetVisualPrefab() => LevelStudioPlugin.Instance.genericStructureDisplays[EditorIntegration.TimesPrefix + "Duct"];

        public void InitializeVisual(GameObject visualObject)
        {
            var manager = visualObject.GetComponent<DuctEditorVisualManager>();
            manager.myLocation = this;
            visualObject.GetComponent<EditorDeletableObject>().toDelete = this;
            UpdateVisual(visualObject);
        }

        public void UpdateVisual(GameObject visualObject)
        {
            visualObject.transform.position = position.ToWorld();
            visualObject.GetComponent<DuctEditorVisualManager>().UpdateConnections();
        }

        public bool OnDelete(EditorLevelData data)
        {
            owner.DeleteDuct(this);
            return true;
        }
    }

    public class DuctConnection(DuctLocation a, DuctLocation b)
    {
        public DuctLocation ductA = a;
        public DuctLocation ductB = b;

        public bool Connects(DuctLocation duct) => duct == ductA || duct == ductB;

        public DuctLocation GetOther(DuctLocation duct)
        {
            if (duct == ductA) return ductB;
            if (duct == ductB) return ductA;
            return null;
        }
    }

    public class DuctEditorVisualManager : MonoBehaviour
    {
        public static bool ShowConnections = false;
        public DuctLocation myLocation;
        public EditorRendererContainer container;
        public LineRenderer lineRendererPrefab;
        private readonly List<LineRenderer> _lineRenderers = [];

        private void Update()
        {
            if (ShowConnections)
                UpdateConnections();
            else
                ClearLines();

        }

        public void UpdateConnections()
        {
            ClearLines();
            if (myLocation == null || myLocation.owner == null) return;

            // For each connection involving this location, draw (or reuse) a line renderer
            // between this duct and the other connected duct.
            foreach (var connection in myLocation.owner.connections)
            {
                if (!connection.Connects(myLocation)) continue;

                var other = connection.GetOther(myLocation);
                if (other == null) continue; // skip malformed connection

                var line = GetOrCreateLineRenderer();
                line.SetPosition(0, myLocation.position.ToWorld() + Vector3.up * 9.5f);
                line.SetPosition(1, other.position.ToWorld() + Vector3.up * 9.5f);
                line.gameObject.SetActive(true);
            }
        }

        private void ClearLines()
        {
            foreach (var line in _lineRenderers)
                line.gameObject.SetActive(false);

        }

        private LineRenderer GetOrCreateLineRenderer()
        {
            var line = _lineRenderers.Find(l => !l.gameObject.activeSelf);
            if (!line)
            {
                line = Instantiate(lineRendererPrefab, transform);
                _lineRenderers.Add(line);
            }
            return line;
        }
    }
}