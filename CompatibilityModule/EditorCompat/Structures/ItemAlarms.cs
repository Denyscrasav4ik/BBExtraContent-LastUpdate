using System.Collections.Generic;
using System.IO;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Structures;

public class ItemAlarmTool : EditorTool
{
    public override string id => "structure_times_itemalarm";

    public ItemAlarmTool(Sprite sprite) => this.sprite = sprite;

    ItemAlarmLocation currentAlarm = null;
    ItemAlarmStructureLocation currentStructure = null;
    bool successfullyPlaced = false;
    EditorDeletableObject lastItemSelected = null;

    public override void Begin() { }

    public override void Exit()
    {
        if (currentAlarm != null && !successfullyPlaced)
        {
            EditorController.Instance.RemoveVisual(currentAlarm);
        }
        lastItemSelected?.Highlight("none");
        successfullyPlaced = false;
        currentAlarm = null;
        currentStructure = null;
        EditorController.Instance.CancelHeldUndo();
    }

    public override bool Cancelled()
    {
        bool didCancel = false;
        if (currentAlarm != null)
        {
            EditorController.Instance.RemoveVisual(currentAlarm);
            didCancel = true;
        }
        lastItemSelected?.Highlight("none");
        currentAlarm = null;
        currentStructure = null;
        EditorController.Instance.CancelHeldUndo();
        return !didCancel;
    }

    public override bool MousePressed()
    {
        if (currentAlarm == null)
        {
            if (!Physics.Raycast(EditorController.Instance.mouseRay, out var hitInfo, 1000f, 8192) || !hitInfo.transform.TryGetComponent<EditorDeletableObject>(out var component))
                return false;

            if (component.toDelete is not ItemPlacement itemPlacement) return false; // It's supposed to be an ItemPlacement

            // Try to attach an alarm to the item under the cursor
            EditorController.Instance.HoldUndo();
            currentStructure = (ItemAlarmStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "ItemAlarm", true);

            currentAlarm = currentStructure.CreateAlarm();
            currentAlarm.position = new Vector3(itemPlacement.position.x, 5f, itemPlacement.position.y);

            if (!currentAlarm.ValidatePosition(EditorController.Instance.levelData))
            {
                Cancelled();
                return false;
            }

            EditorController.Instance.AddVisual(currentAlarm);
            currentStructure.alarms.Add(currentAlarm);
            successfullyPlaced = true;
            return true;
        }
        return false;
    }

    public override bool MouseReleased() => false;

    public override void Update()
    {
        EditorController.Instance.selector.SelectTile(EditorController.Instance.mouseGridPosition);

        if (!Physics.Raycast(EditorController.Instance.mouseRay, out var hitInfo, 1000f, 8192) || !hitInfo.transform.TryGetComponent<EditorDeletableObject>(out var component))
        {
            lastItemSelected?.Highlight("none");
            return;
        }

        if (component.toDelete is not ItemPlacement)
        {
            lastItemSelected?.Highlight("none");
            return; // It's supposed to be an ItemPlacement
        }

        if (lastItemSelected != component)
            lastItemSelected?.Highlight("none"); // Null check because duh

        lastItemSelected = component;
        component.Highlight("yellow");
    }
}

public class ItemAlarmLocation : IEditorVisualizable, IEditorDeletable
{
    public ItemAlarmStructureLocation owner;
    public Vector3 position;

    public void CleanupVisual(GameObject visualObject) { }

    public bool OnDelete(EditorLevelData data)
    {
        owner.alarms.Remove(this);
        EditorController.Instance.RemoveVisual(this);
        return true;
    }

    public GameObject GetVisualPrefab() => LevelStudioPlugin.Instance.genericStructureDisplays[EditorIntegration.TimesPrefix + "ItemAlarm"];

    public void InitializeVisual(GameObject visualObject)
    {
        visualObject.GetComponent<EditorDeletableObject>().toDelete = this;
        UpdateVisual(visualObject);
    }

    public void UpdateVisual(GameObject visualObject) => visualObject.transform.position = position;

    public bool ValidatePosition(EditorLevelData data) =>
        data.items.Exists(i => (new Vector3(i.position.x, 5f, i.position.y) - position).sqrMagnitude < 0.1f); // Check if there's at least one item nearby it
}

public class ItemAlarmStructureLocation : StructureLocation
{
    public List<ItemAlarmLocation> alarms = [];
    public ItemAlarmLocation CreateAlarm() => new() { owner = this };

    public override void AddStringsToCompressor(StringCompressor compressor) { }
    public override void CleanupVisual(GameObject visualObject) { }
    public override GameObject GetVisualPrefab() => null;
    public override void InitializeVisual(GameObject visualObject) => alarms.ForEach(EditorController.Instance.AddVisual);

    public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
    {
        var info = new StructureInfo(type);
        foreach (var alarm in alarms)
            info.data.Add(new() { position = alarm.position.ToCellVector().ToData() });
        return info;
    }

    public override void ReadInto(EditorLevelData data, BinaryReader reader, StringCompressor compressor)
    {
        _ = reader.ReadByte();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var alarm = CreateAlarm();
            alarm.position = reader.ReadUnityVector3().ToUnity();
            alarms.Add(alarm);
        }
    }

    public override void ShiftBy(Vector3 worldOffset, IntVector2 cellOffset, IntVector2 sizeDifference) =>
        alarms.ForEach(alarm => alarm.position -= worldOffset);

    public override void UpdateVisual(GameObject visualObject) => alarms.ForEach(EditorController.Instance.UpdateVisual);

    public override bool ValidatePosition(EditorLevelData data)
    {
        for (int i = 0; i < alarms.Count; i++)
        {
            if (!alarms[i].ValidatePosition(data))
                alarms[i].OnDelete(data); // delete themselves
        }
        return alarms.Count != 0;
    }

    public override void Write(EditorLevelData data, BinaryWriter writer, StringCompressor compressor)
    {
        writer.Write((byte)0);
        writer.Write(alarms.Count);
        foreach (var alarm in alarms)
            writer.Write(alarm.position.ToData());
    }
}