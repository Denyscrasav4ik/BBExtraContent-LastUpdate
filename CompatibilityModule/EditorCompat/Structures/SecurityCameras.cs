using System.Collections.Generic;
using System.IO;
using BBTimes.CustomContent.Builders;
using BBTimes.Extensions;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.UI;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using TMPro;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Structures;

public class SecurityCameraTool : EditorTool
{
    public SecurityCameraTool(Sprite icon) =>
        sprite = icon;

    IntVector2? startingPos;
    bool placingCamera = false, successfullyPlaced = false;
    SecurityCameraLocation currentCamera;
    SecurityCameraStructureLocation currentStructure;

    public override string id => "structure_times_securitycameras";

    public override void Begin() { }

    public override bool Cancelled()
    {
        if (placingCamera)
        {
            placingCamera = false;
            EditorController.Instance.RemoveVisual(currentCamera);
            currentCamera = null;
            startingPos = null;

            if (currentStructure != null)
                EditorController.Instance.levelData.ValidatePlacements(true);
            currentStructure = null;

            EditorController.Instance.CancelHeldUndo();
            return false;
        }
        return true;
    }

    public override void Exit()
    {
        if (currentCamera != null && !successfullyPlaced)
        {
            EditorController.Instance.RemoveVisual(currentCamera);
        }
        successfullyPlaced = false;
        currentCamera = null;
        startingPos = null;
        placingCamera = false;
        EditorController.Instance.CancelHeldUndo();
    }

    public override bool MousePressed()
    {
        if (currentCamera == null)
        {
            EditorController.Instance.HoldUndo();
            startingPos = EditorController.Instance.mouseGridPosition;
            currentStructure = (SecurityCameraStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "SecurityCamera", true);
            currentCamera = currentStructure.CreateCamera();
            currentCamera.direction = Direction.North;
            currentCamera.distance = 1;
            currentCamera.position = startingPos.Value;
            EditorController.Instance.AddVisual(currentCamera);
            placingCamera = true;
            return false;
        }
        return false;
    }

    public override bool MouseReleased()
    {
        if (placingCamera)
        {
            if (!currentCamera.ValidatePosition(EditorController.Instance.levelData))
            {
                Cancelled();
                return false; // nope
            }
            currentStructure.cameraLocations.Add(currentCamera); // If the position is valid andthe mouse is done, just add the camera in the map
            placingCamera = false;
            successfullyPlaced = true;
            startingPos = null;
            return true;
        }
        return false;
    }

    public override void Update()
    {
        if (startingPos.HasValue)
        {
            EditorController.Instance.selector.SelectTile(startingPos.Value);
        }
        else
        {
            EditorController.Instance.selector.SelectTile(EditorController.Instance.mouseGridPosition);
        }
        if (placingCamera)
        {
            IntVector2 mousePos = EditorController.Instance.mouseGridPosition;
            Direction targetDirection = currentCamera.direction;
            if (mousePos != startingPos.Value)
            {
                targetDirection = Directions.DirFromVector3(new Vector3(mousePos.x - startingPos.Value.x, 0f, mousePos.z - startingPos.Value.z), 45f);
            }

            IntVector2 finalOff = mousePos.LockAxis(startingPos.Value, targetDirection) - startingPos.Value;
            currentCamera.direction = targetDirection;
            byte distance = (byte)(Mathf.Abs(finalOff.x + finalOff.z) + 1);
            currentCamera.distance = distance;
            EditorController.Instance.UpdateVisual(currentCamera);
        }
    }
}

public class SecurityCameraLocation : IEditorVisualizable, IEditorDeletable, IEditorSettingsable
{
    public IntVector2 position;
    public short distance = 1, turnCooldown = 10;
    public float detectCooldown = 2.5f;
    public Direction direction = Direction.Null;
    public SecurityCameraStructureLocation owner;

    public void CleanupVisual(GameObject visualObject) { }

    public bool ValidatePosition(EditorLevelData data)
    {
        IntVector2 dirVector = direction.ToIntVector2();
        PlusStudioLevelFormat.Cell startCell = data.GetCellSafe(position);
        if (startCell == null || startCell.roomId == 0)
            return false;
        for (int i = 0; i < distance; i++)
        {
            IntVector2 currentPosition = position + dirVector * i;
            PlusStudioLevelFormat.Cell cell = data.GetCellSafe(currentPosition);
            if (cell == null || cell.roomId != startCell.roomId) // If it identifies an invalid cell along the way, it is not a valid position
                return false;
        }
        return true;
    }

    public GameObject GetVisualPrefab() =>
        LevelStudioPlugin.Instance.genericStructureDisplays[EditorIntegration.TimesPrefix + "SecurityCamera"];


    public void InitializeVisual(GameObject visualObject)
    {
        visualObject.GetComponent<EditorDeletableObject>().toDelete = this;
        visualObject.GetComponent<SettingsComponent>().activateSettingsOn = this;
        UpdateVisual(visualObject);
    }

    public void UpdateVisual(GameObject visualObject)
    {
        visualObject.transform.position = position.ToWorld();
        visualObject.transform.rotation = direction.ToRotation(); // yaw correction here to prevent the indicator's misaligment

        var manager = visualObject.GetComponent<EditorSecurityCameraVisualManager>();
        if (direction != Direction.Null && (manager.length != distance || manager.direction != direction))
            manager.InitializeVisuals(distance, direction);
    }

    public bool OnDelete(EditorLevelData data)
    {
        owner.DeleteCamera(this);
        return true;
    }

    public void SettingsClicked()
    {
        var ui = EditorController.Instance.CreateUI<SecurityCameraSettingsExchangeHandler>("SecurityCameraConfig", Structure_Camera.GetJSONUIPath());
        ui.myCamera = this;
        ui.Refresh();
    }
}

public class SecurityCameraStructureLocation : StructureLocation
{
    readonly public List<SecurityCameraLocation> cameraLocations = [];
    public override void AddStringsToCompressor(StringCompressor compressor) { }

    public override void CleanupVisual(GameObject visualObject) { }

    public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
    {
        StructureInfo info = new(type);
        if (cameraLocations.Count == 0)
        {
            Debug.LogWarning("Compiling Security Cameras without any one assigned");
            return info;
        }

        foreach (var camera in cameraLocations)
        {
            info.data.Add(new()
            {
                data = new Embedded2Shorts(camera.distance, camera.turnCooldown),
                direction = (PlusDirection)camera.direction,
                position = camera.position.ToData()
            });
            info.data.Add(new() { data = camera.detectCooldown.ConvertToIntNoRecast() });
        }
        return info;
    }

    public override GameObject GetVisualPrefab() => null;

    public override void InitializeVisual(GameObject visualObject) =>
        cameraLocations.ForEach(EditorController.Instance.AddVisual); // Add visual from this


    public override void ReadInto(EditorLevelData data, BinaryReader reader, StringCompressor compressor)
    {
        var version = reader.ReadByte(); // Version
        int camCount;
        // V0 Loading
        if (version == 0)
        {
            camCount = reader.ReadInt32();
            for (int i = 0; i < camCount; i++)
            {
                cameraLocations.Add(new()
                {
                    owner = this,
                    distance = reader.ReadByte(),
                    direction = (Direction)reader.ReadByte(),
                    position = PlusStudioLevelLoader.Extensions.ToInt(reader.ReadByteVector2())
                });
            }
            return;
        }

        // V1 Loading
        camCount = reader.ReadInt32();
        for (int i = 0; i < camCount; i += 2)
        {
            cameraLocations.Add(new()
            {
                owner = this,
                distance = reader.ReadInt16(),
                turnCooldown = reader.ReadInt16(),
                detectCooldown = reader.ReadSingle(),
                direction = (Direction)reader.ReadByte(),
                position = PlusStudioLevelLoader.Extensions.ToInt(reader.ReadByteVector2())
            });
        }
    }

    public override void ShiftBy(Vector3 worldOffset, IntVector2 cellOffset, IntVector2 sizeDifference) =>
        cameraLocations.ForEach(cam => cam.position -= cellOffset);

    public override void UpdateVisual(GameObject visualObject) =>
        cameraLocations.ForEach(EditorController.Instance.UpdateVisual);

    public override bool ValidatePosition(EditorLevelData data)
    {
        for (int i = 0; i < cameraLocations.Count; i++)
            if (!cameraLocations[i].ValidatePosition(data))
                DeleteCamera(cameraLocations[i--]);

        return cameraLocations.Count != 0;
    }

    public override void Write(EditorLevelData data, BinaryWriter writer, StringCompressor compressor)
    {
        writer.Write((byte)1);
        writer.Write(cameraLocations.Count);
        for (int i = 0; i < cameraLocations.Count; i++)
        {
            writer.Write(cameraLocations[i].distance);
            writer.Write(cameraLocations[i].turnCooldown);
            writer.Write(cameraLocations[i].detectCooldown);
            writer.Write((byte)cameraLocations[i].direction);
            writer.Write(PlusStudioLevelLoader.Extensions.ToByte(cameraLocations[i].position));
        }
    }

    public void DeleteCamera(SecurityCameraLocation cam)
    {
        EditorController.Instance.RemoveVisual(cam);
        cameraLocations.Remove(cam);
    }

    public SecurityCameraLocation CreateCamera() =>
        new() { owner = this };

}

public class EditorSecurityCameraVisualManager : MonoBehaviour
{
    public List<SpriteRenderer> cameraRenderers = [];
    public EditorRendererContainer renderContainer;
    public SpriteRenderer cameraPlaneRendererPre;
    public BoxCollider collider;
    public TextureSlider slider;
    public int length;
    public Direction direction;

    public void InitializeVisuals(int length, Direction direction)
    {
        cameraRenderers.ForEach(cam =>
        {
            var rendererIndex = renderContainer.myRenderers.IndexOf(cam);
            if (rendererIndex != -1) // Remove the old renderers before clearing out
            {
                renderContainer.myRenderers.RemoveAt(rendererIndex);
                renderContainer.defaultHighlights.RemoveAt(rendererIndex);
            }
            Destroy(cam.gameObject); // Destroy the renderer itself
        });
        cameraRenderers.Clear();
        this.length = length;
        this.direction = direction;
        var rotQuart = direction.ToRotation() * Quaternion.Euler(90f, 0f, 0f); // x = 90f
        renderContainer.myRenderers.Clear();
        for (int i = 0; i < length; i++)
        {
            var clone = Instantiate(cameraPlaneRendererPre, transform);
            clone.transform.localPosition = (Vector3.forward * i * 10f) + (Vector3.up * 0.1f);
            clone.transform.rotation = rotQuart;

            renderContainer.AddRenderer(clone, "none");
            cameraRenderers.Add(clone);
        }
        collider.size = new Vector3(10f, 0.01f, length * 10f);
        collider.center = Vector3.forward * (length - 1) * 5f;
    }
}

public class SecurityCameraSettingsExchangeHandler : EditorOverlayUIExchangeHandler
{
    public SecurityCameraLocation myCamera;
    TextMeshProUGUI detectCooldownText, turnCooldownText;
    bool somethingChanged = false;

    public override void OnElementsCreated()
    {
        base.OnElementsCreated();
        detectCooldownText = transform?.Find("DetectCooldownBox").GetComponent<TextMeshProUGUI>();
        turnCooldownText = transform?.Find("TurnCooldownBox").GetComponent<TextMeshProUGUI>();
        EditorController.Instance.HoldUndo();
    }

    public void Refresh()
    {
        detectCooldownText.text = myCamera.detectCooldown.ToString();
        turnCooldownText.text = myCamera.turnCooldown.ToString();
    }

    public override bool OnExit()
    {
        if (somethingChanged)
            EditorController.Instance.AddHeldUndo();
        else
            EditorController.Instance.CancelHeldUndo();
        return base.OnExit();
    }

    public override void SendInteractionMessage(string message, object data)
    {
        switch (message)
        {
            case "setDetectCooldown":
                if (float.TryParse((string)data, out var cooldown))
                {
                    myCamera.detectCooldown = Mathf.Max(cooldown, 0.25f);
                    somethingChanged = true;
                }
                Refresh();
                break;
            case "setTurnCooldown":
                if (short.TryParse((string)data, out var turnCooldown))
                {
                    myCamera.turnCooldown = (short)Mathf.Max(turnCooldown, 1);
                    somethingChanged = true;
                }
                Refresh();
                break;
        }
        base.SendInteractionMessage(message, data);
    }
}