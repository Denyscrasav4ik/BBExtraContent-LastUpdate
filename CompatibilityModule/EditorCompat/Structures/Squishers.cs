using System.Collections.Generic;
using System.IO;
using BBTimes.CustomContent.Builders;
using BBTimes.Extensions;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using PlusLevelStudio.UI;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using TMPro;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Structures;

public class SquisherTool : PointTool
{
    public override string id => "structure_" + EditorIntegration.TimesPrefix + "squisher";
    public SquisherTool(Sprite icon) { sprite = icon; }

    protected override bool TryPlace(IntVector2 position)
    {
        EditorController.Instance.AddUndo();
        var structure = (SquisherStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "Squisher", true);
        var squisher = new SquisherLocation()
        {
            prefab = EditorIntegration.TimesPrefix + "Squisher",
            deleteAction = (d, l) =>
            {
                var squLoc = (SquisherLocation)l;
                structure.squishers.Remove(squLoc);
                EditorController.Instance.RemoveVisual(l);
                structure.DeleteIfInvalid();
                // squLoc.button?.OnDelete(d); This squisher never has a button anyways
                return true;
            },
            position = position
        };
        structure.squishers.Add(squisher);
        EditorController.Instance.AddVisual(squisher);
        return true;
    }
}

public class SquisherWithButtonTool : EditorTool
{
    public override string id => "structure_" + EditorIntegration.TimesPrefix + "squisher_button";
    public SquisherWithButtonTool(Sprite icon) { sprite = icon; }

    IntVector2? squisherPos;
    IntVector2? buttonPos;
    bool firstPlaced = false;
    SquisherLocation squisher;

    public override void Begin() { }

    public override bool Cancelled()
    {
        if (buttonPos.HasValue)
        {
            buttonPos = null;
            return false;
        }
        if (firstPlaced)
        {
            EditorController.Instance.RemoveVisual(squisher);
            squisher = null;
            firstPlaced = false;
            squisherPos = null;
            return false;
        }
        if (squisherPos.HasValue)
        {
            squisherPos = null;
            return false;
        }
        return true;
    }

    public override void Exit()
    {
        squisherPos = null;
        buttonPos = null;
        if (squisher != null)
        {
            EditorController.Instance.RemoveVisual(squisher);
            squisher = null;
        }
        firstPlaced = false;
    }

    public override bool MousePressed()
    {
        if (firstPlaced)
        {
            if (EditorController.Instance.levelData.RoomIdFromPos(EditorController.Instance.mouseGridPosition, true) != 0)
            {
                buttonPos = EditorController.Instance.mouseGridPosition;
                EditorController.Instance.selector.SelectRotation(buttonPos.Value, PlaceButton);
            }
            return false;
        }
        if (squisherPos.HasValue) return false;
        if (EditorController.Instance.levelData.RoomIdFromPos(EditorController.Instance.mouseGridPosition, true) != 0)
        {
            squisherPos = EditorController.Instance.mouseGridPosition;
            PlaceSquisher();
        }
        return false;
    }

    void PlaceSquisher()
    {
        EditorController.Instance.AddUndo();
        var structure = (SquisherStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "Squisher", true);
        squisher = new SquisherLocation()
        {
            prefab = EditorIntegration.TimesPrefix + "Squisher",
            deleteAction = (d, l) =>
            {
                var squLoc = (SquisherLocation)l;
                structure.squishers.Remove(squLoc);
                EditorController.Instance.RemoveVisual(l);
                structure.DeleteIfInvalid();
                squLoc.button?.OnDelete(d);
                return true;
            },
            position = squisherPos.Value
        };
        EditorController.Instance.AddVisual(squisher);
        firstPlaced = true;
        EditorController.Instance.selector.DisableSelection();
    }

    void PlaceButton(Direction dir)
    {
        var button = new SimpleButtonLocation()
        {
            prefab = "button",
            position = buttonPos.Value,
            direction = dir
        };
        if (!button.ValidatePosition(EditorController.Instance.levelData, false))
        {
            return;
        }
        squisher.button = button;
        button.deleteAction = (d, l) => { squisher.button = null; EditorController.Instance.RemoveVisual(l); return true; };

        var structure = (SquisherStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "Squisher", true);
        structure.squishers.Add(squisher);
        EditorController.Instance.AddVisual(button);
        EditorController.Instance.UpdateVisual(structure);
        squisher = null;
        EditorController.Instance.SwitchToTool(null);
    }

    public override bool MouseReleased() => false;

    public override void Update()
    {
        if (firstPlaced)
        {
            if (buttonPos == null)
                EditorController.Instance.selector.SelectTile(EditorController.Instance.mouseGridPosition);
            return;
        }
        if (squisherPos == null)
            EditorController.Instance.selector.SelectTile(EditorController.Instance.mouseGridPosition);
    }
}

public class SquisherLocation : PointLocation, IEditorSettingsable
{
    public short speed = 7, cooldown = 5;
    public SimpleButtonLocation button;

    public void SettingsClicked()
    {
        var ui = EditorController.Instance.CreateUI<SquisherSettingsExchangeHandler>("SquisherConfig", Structure_Squisher.GetJSONUIPath());
        ui.mySquisher = this;
        ui.Refresh();
    }

    public override void InitializeVisual(GameObject visualObject)
    {
        base.InitializeVisual(visualObject);
        visualObject.GetComponent<SettingsComponent>().activateSettingsOn = this;
    }
}

public class SquisherStructureLocation : StructureLocation
{
    public List<SquisherLocation> squishers = [];

    public override void AddStringsToCompressor(StringCompressor compressor) { }
    public override void CleanupVisual(GameObject visualObject)
    {
        foreach (var squisher in squishers)
            EditorController.Instance.RemoveVisual(squisher);
    }

    public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
    {
        var info = new StructureInfo(type);
        foreach (var squisher in squishers)
        {
            info.data.Add(new StructureDataInfo()
            {
                position = squisher.position.ToData(),
                data = new Embedded2Shorts(squisher.speed, squisher.cooldown)
            });
            if (squisher.button != null)
            {
                info.data.Add(new StructureDataInfo()
                {
                    position = squisher.button.position.ToData(),
                    direction = (PlusDirection)squisher.button.direction,
                    data = -1
                });
            }
        }
        return info;
    }

    public override GameObject GetVisualPrefab() => null;

    public override void InitializeVisual(GameObject visualObject)
    {
        foreach (var squisher in squishers)
        {
            EditorController.Instance.AddVisual(squisher);
            if (squisher.button != null)
                EditorController.Instance.AddVisual(squisher.button);
        }
    }

    public override void ReadInto(EditorLevelData data, BinaryReader reader, StringCompressor compressor)
    {
        _ = reader.ReadByte(); // version
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var squisher = new SquisherLocation()
            {
                prefab = EditorIntegration.TimesPrefix + "Squisher",
                deleteAction = OnDeleteSquisher,
                position = PlusStudioLevelLoader.Extensions.ToInt(reader.ReadByteVector2()),
                speed = reader.ReadInt16(), // short
                cooldown = reader.ReadInt16()
            };
            if (reader.ReadBoolean())
            {
                squisher.button = new SimpleButtonLocation()
                {
                    prefab = "button",
                    deleteAction = (d, l) => squisher.button != null,
                    position = PlusStudioLevelLoader.Extensions.ToInt(reader.ReadByteVector2()),
                    direction = (Direction)reader.ReadByte()
                };
            }
            squishers.Add(squisher);
        }
    }

    public override void ShiftBy(Vector3 worldOffset, IntVector2 cellOffset, IntVector2 sizeDifference)
    {
        foreach (var squisher in squishers)
        {
            squisher.position -= cellOffset;
            if (squisher.button != null)
                squisher.button.position -= cellOffset;
        }
    }

    public override void UpdateVisual(GameObject visualObject)
    {
        foreach (var squisher in squishers)
        {
            EditorController.Instance.UpdateVisual(squisher);
            if (squisher.button != null)
                EditorController.Instance.UpdateVisual(squisher.button);
        }
    }

    public override bool ValidatePosition(EditorLevelData data)
    {
        for (int i = squishers.Count - 1; i >= 0; i--)
        {
            if (!squishers[i].ValidatePosition(data, true))
                OnDeleteSquisher(data, squishers[i]);
        }
        return squishers.Count > 0;
    }

    public override void Write(EditorLevelData data, BinaryWriter writer, StringCompressor compressor)
    {
        writer.Write((byte)0);
        writer.Write(squishers.Count);
        foreach (var squisher in squishers)
        {
            writer.Write(PlusStudioLevelLoader.Extensions.ToByte(squisher.position));
            writer.Write(squisher.speed);
            writer.Write(squisher.cooldown);
            writer.Write(squisher.button != null);
            if (squisher.button != null)
            {
                writer.Write(PlusStudioLevelLoader.Extensions.ToByte(squisher.button.position));
                writer.Write((byte)squisher.button.direction);
            }
        }
    }

    bool OnDeleteSquisher(EditorLevelData data, PointLocation loc)
    {
        squishers.Remove((SquisherLocation)loc);
        EditorController.Instance.RemoveVisual(loc);
        DeleteIfInvalid();
        return true;
    }
}

public class SquisherSettingsExchangeHandler : EditorOverlayUIExchangeHandler
{
    public SquisherLocation mySquisher;
    TextMeshProUGUI speedText, cooldownText;
    bool somethingChanged = false;

    public override void OnElementsCreated()
    {
        base.OnElementsCreated();
        speedText = transform?.Find("SpeedBox").GetComponent<TextMeshProUGUI>();
        cooldownText = transform?.Find("CooldownBox").GetComponent<TextMeshProUGUI>();
        EditorController.Instance.HoldUndo();
    }

    public void Refresh()
    {
        speedText.text = mySquisher.speed.ToString();
        cooldownText.text = mySquisher.cooldown.ToString();
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
            case "setSpeed":
                if (short.TryParse((string)data, out var speed))
                {
                    mySquisher.speed = (short)Mathf.Clamp(speed, 1, 25);
                    somethingChanged = true;
                }
                Refresh();
                break;

            case "setCooldown":
                if (short.TryParse((string)data, out var cooldown))
                {
                    mySquisher.cooldown = (short)Mathf.Max(cooldown, 0);
                    somethingChanged = true;
                }
                Refresh();
                break;
        }
        base.SendInteractionMessage(message, data);
    }
}
