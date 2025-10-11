using System.Collections.Generic;
using System.IO;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using TMPro;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Structures;

public class TrapdoorTool : EditorTool
{
    public TrapdoorTool(Sprite icon, bool linkedMode)
    {
        sprite = icon;
        this.linkedMode = linkedMode;
        type = !linkedMode ? "random" : "linked";
    }
    protected string type = "undefined";
    readonly bool linkedMode = false;
    TrapdoorStructureLocation currentStructure;
    TrapdoorLocation currentTrapdoor = null, linkedTrapdoor = null;
    bool successfullyPlaced = false, placingFirstLinkage = false;

    public override string id => $"structure_times_trapdoor_{type}";

    public override void Begin() { }

    public override bool Cancelled()
    {
        bool didCancel = false;
        if (currentTrapdoor != null)
        {
            EditorController.Instance.RemoveVisual(currentTrapdoor);
            didCancel = true;
        }
        if (linkedMode && linkedTrapdoor != null)
        {
            EditorController.Instance.RemoveVisual(linkedTrapdoor);
            didCancel = true;
        }
        currentTrapdoor = null;
        linkedTrapdoor = null;
        currentStructure = null;
        placingFirstLinkage = false;
        EditorController.Instance.CancelHeldUndo();
        return !didCancel;
    }

    public override void Exit()
    {
        if (!successfullyPlaced)
        {
            if (currentTrapdoor != null)
                EditorController.Instance.RemoveVisual(currentTrapdoor);
            if (linkedTrapdoor != null)
                EditorController.Instance.RemoveVisual(linkedTrapdoor);
        }
        successfullyPlaced = false;
        currentTrapdoor = null;
        linkedTrapdoor = null;
        placingFirstLinkage = false;
        EditorController.Instance.CancelHeldUndo();
    }

    public override bool MousePressed()
    {
        if (!linkedMode)
        {
            // Random trapdoor placement
            if (currentTrapdoor == null)
            {
                EditorController.Instance.HoldUndo();
                currentStructure = (TrapdoorStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "Trapdoor", true);
                currentTrapdoor = currentStructure.CreateTrapdoorLocation();
                currentTrapdoor.position = EditorController.Instance.mouseGridPosition;
                if (!currentTrapdoor.ValidatePosition(EditorController.Instance.levelData))
                {
                    Cancelled();
                    return false;
                }
                EditorController.Instance.AddVisual(currentTrapdoor);
                currentStructure.trapdoorLocations.Add(currentTrapdoor);
                successfullyPlaced = true;
                return true;
            }
        }
        else
        {
            // Linked trapdoor placement
            if (currentTrapdoor == null && !placingFirstLinkage)
            {
                // Place first trapdoor
                EditorController.Instance.HoldUndo();
                currentStructure = (TrapdoorStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "Trapdoor", true);
                currentTrapdoor = currentStructure.CreateTrapdoorLocation();
                currentTrapdoor.position = EditorController.Instance.mouseGridPosition;
                if (!currentTrapdoor.ValidatePosition(EditorController.Instance.levelData)) // Usual validation (id is assigned after to prevent the trapdoor thinking it
                                                                                            // is linked to something already)
                {
                    Cancelled();
                    return false;
                }

                // Assign a new unique ID for this pair
                int newId = 1;
                if (currentStructure.trapdoorLocations.Count != 0)
                {
                    foreach (var trap in currentStructure.trapdoorLocations) // Find the highest id inside the locations
                        if (trap.id >= newId) newId = trap.id + 1;
                }

                currentTrapdoor.id = newId;
                EditorController.Instance.AddVisual(currentTrapdoor);
                placingFirstLinkage = true;
                return false;
            }
            else if (placingFirstLinkage && linkedTrapdoor == null)
            {
                // Place second trapdoor (linked)
                linkedTrapdoor = currentStructure.CreateTrapdoorLocation();
                linkedTrapdoor.position = EditorController.Instance.mouseGridPosition;
                if (!linkedTrapdoor.ValidatePosition(EditorController.Instance.levelData)) // No ID set for the same reason, also assuming no anomaly makes both placed trapdoors have different IDs lol
                {
                    Cancelled();
                    return false;
                }
                // Add both to structure
                currentStructure.trapdoorLocations.Add(currentTrapdoor);
                currentStructure.trapdoorLocations.Add(linkedTrapdoor);
                linkedTrapdoor.id = currentTrapdoor.id; // Same ID as first
                EditorController.Instance.AddVisual(linkedTrapdoor); // Add and update visual for line
                EditorController.Instance.UpdateVisual(currentTrapdoor); // Update visual of this for the line renderer too
                successfullyPlaced = true;
                placingFirstLinkage = false;
                return true;
            }
        }
        return false;
    }

    public override bool MouseReleased() => false; // This is not needed actually

    public override void Update()
    {
        EditorController.Instance.selector.SelectTile(EditorController.Instance.mouseGridPosition);
    }
}

public class TrapdoorLocation : IEditorVisualizable, IEditorDeletable
{
    public IntVector2 position;
    public int id = -1;
    public int openCooldown = 15;
    public bool IsLinked => id != -1;
    public TrapdoorLocation Link => !IsLinked ? throw new System.NotSupportedException("TrapdoorLocation does not support linkage.") : owner.trapdoorLocations.Find(trap => trap != this && trap.id == id);
    public TrapdoorStructureLocation owner;

    public void CleanupVisual(GameObject visualObject) { }
    public bool ValidatePosition(EditorLevelData data)
    {
        PlusStudioLevelFormat.Cell startCell = data.GetCellSafe(position);
        // If cell valid, room is valid and the id is -1 or corresponds to an existent one, it should be added
        return startCell != null && startCell.roomId != 0 && (id == -1 || owner.trapdoorLocations.Exists(trap => trap != this && trap.id == id));
    }

    public GameObject GetVisualPrefab() =>
        LevelStudioPlugin.Instance.genericStructureDisplays[EditorIntegration.TimesPrefix + "Trapdoor" + (id == -1 ? "Random" : "Linked")];


    public void InitializeVisual(GameObject visualObject)
    {
        visualObject.GetComponent<EditorDeletableObject>().toDelete = this;
        UpdateVisual(visualObject);
    }

    public void UpdateVisual(GameObject visualObject)
    {
        visualObject.transform.position = position.ToWorld();
        visualObject.transform.rotation = Direction.North.ToRotation(); // Faces north by default

        var textMesh = visualObject.GetComponentInChildren<TextMeshPro>();
        textMesh.text = openCooldown.ToString();
        textMesh.gameObject.SetActive(true);

        if (IsLinked)
        {
            var link = Link;
            if (link != null)
                visualObject.GetComponent<TrapdoorEditorVisualManager>().UpdateLinePositions(position.ToWorld() + Vector3.up, link.position.ToWorld() + Vector3.up);
        }
    }

    public bool OnDelete(EditorLevelData data)
    {
        if (IsLinked) // Delete its linkage too
        {
            var trap = Link;
            if (trap != null)
                owner.DeleteTrapdoor(trap);
        }
        owner.DeleteTrapdoor(this);
        return true;
    }
}

public class TrapdoorStructureLocation : StructureLocation
{
    public List<TrapdoorLocation> trapdoorLocations = [];
    public void DeleteTrapdoor(TrapdoorLocation trap)
    {
        EditorController.Instance.RemoveVisual(trap);
        trapdoorLocations.Remove(trap);
    }
    public TrapdoorLocation CreateTrapdoorLocation() =>
        new() { owner = this };

    public override void AddStringsToCompressor(StringCompressor compressor) { }

    public override void CleanupVisual(GameObject visualObject) { }

    public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
    {
        StructureInfo info = new(type);
        if (trapdoorLocations.Count == 0)
        {
            Debug.LogWarning("Compiling Trapdoors without any one assigned");
            return info;
        }

        foreach (var trapdoor in trapdoorLocations)
        {
            info.data.Add(new()
            {
                data = trapdoor.id,
                position = trapdoor.position.ToData()
            });
        }
        return info;
    }

    public override GameObject GetVisualPrefab() => null;

    public override void InitializeVisual(GameObject visualObject) =>
        trapdoorLocations.ForEach(EditorController.Instance.AddVisual); // Add visual from this

    public override void ReadInto(EditorLevelData data, BinaryReader reader, StringCompressor compressor)
    {
        _ = reader.ReadByte(); // Version

        int trapdoorCount = reader.ReadInt32();
        for (int i = 0; i < trapdoorCount; i++)
        {
            trapdoorLocations.Add(new()
            {
                owner = this,
                id = reader.ReadInt32(),
                position = PlusStudioLevelLoader.Extensions.ToInt(reader.ReadByteVector2())
            });
        }
    }

    public override void ShiftBy(Vector3 worldOffset, IntVector2 cellOffset, IntVector2 sizeDifference) =>
        trapdoorLocations.ForEach(trap => trap.position -= cellOffset);

    public override void UpdateVisual(GameObject visualObject) =>
        trapdoorLocations.ForEach(EditorController.Instance.UpdateVisual);

    public override bool ValidatePosition(EditorLevelData data)
    {
        for (int i = 0; i < trapdoorLocations.Count; i++)
            if (!trapdoorLocations[i].ValidatePosition(data))
                DeleteTrapdoor(trapdoorLocations[i--]);

        return trapdoorLocations.Count != 0;
    }

    public override void Write(EditorLevelData data, BinaryWriter writer, StringCompressor compressor)
    {
        writer.Write((byte)0);
        writer.Write(trapdoorLocations.Count);
        for (int i = 0; i < trapdoorLocations.Count; i++)
        {
            writer.Write(trapdoorLocations[i].id);
            writer.Write(PlusStudioLevelLoader.Extensions.ToByte(trapdoorLocations[i].position));
        }
    }
}

public class TrapdoorEditorVisualManager : MonoBehaviour, IEditorInteractable, IEditorMovable
{
    public LineRenderer lineRenderer;
    public EditorRendererContainer container;
    public bool OnClicked() // To trigger movable selection
    {
        EditorController.Instance.selector.SelectObject(this, MoveAxis.None, RotateAxis.None);
        return false;
    }

    public bool OnHeld() => throw new System.NotImplementedException("Not used!");

    public void OnReleased() => throw new System.NotImplementedException("Not used!");

    public bool InteractableByTool(EditorTool tool) => false;

    public void UpdateLinePositions(Vector3 one, Vector3 two)
    {
        lineRenderer.SetPosition(0, one);
        lineRenderer.SetPosition(1, two);
    }

    public void Selected() // Movable for selected
    {
        lineRenderer.gameObject.SetActive(true);
        container.Highlight("yellow"); // Has to manually highlight apparently
    }

    public void Unselected()
    {
        lineRenderer.gameObject.SetActive(false);
        container.Highlight("none");
    }

    public void MoveUpdate(Vector3? position, Quaternion? rotation) { }

    public Transform GetTransform() => transform;
}