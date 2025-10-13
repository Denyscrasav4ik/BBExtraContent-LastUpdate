using System.Collections.Generic;
using System.IO;
using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Structures;

public class NotebookMachineTool(Sprite sprite) : ActivityTool("notebook", sprite, 5f)
{
    public override string id => "structure_times_notebookmachine";

    protected override bool TryPlace(IntVector2 position, Direction dir)
    {
        if (!base.TryPlace(position, dir)) return false;

        var room = EditorController.Instance.levelData.RoomFromPos(position, true);

        EditorController.Instance.AddUndo();
        var structure = (NotebookMachineStructureLocation)EditorController.Instance.AddOrGetStructureToData(EditorIntegration.TimesPrefix + "NotebookMachine", true);

        var machine = structure.CreateMachine();
        machine.position = room.activity.position + Vector3.down * 1.25f;
        machine.rotation = dir.ToRotation();

        if (!machine.ValidatePosition(EditorController.Instance.levelData))
        {
            EditorController.Instance.CancelHeldUndo();
            return false;
        }

        structure.machines.Add(machine);
        EditorController.Instance.AddVisual(machine);

        return true;
    }
}

public class NotebookMachineLocation : IEditorVisualizable, IEditorDeletable
{
    public NotebookMachineStructureLocation owner;
    public Vector3 position;
    public Quaternion rotation;

    public void CleanupVisual(GameObject visualObject) { }

    public bool OnDelete(EditorLevelData data)
    {
        owner.machines.Remove(this);
        EditorController.Instance.RemoveVisual(this);
        return true;
    }

    public GameObject GetVisualPrefab() => LevelStudioPlugin.Instance.genericStructureDisplays[EditorIntegration.TimesPrefix + "NotebookMachine"];

    public void InitializeVisual(GameObject visualObject)
    {
        visualObject.GetComponent<EditorDeletableObject>().toDelete = this;
        UpdateVisual(visualObject);
    }

    public void UpdateVisual(GameObject visualObject)
    {
        visualObject.transform.position = position;
        visualObject.transform.rotation = rotation;

        if (!ValidatePosition(EditorController.Instance.levelData))
            OnDelete(EditorController.Instance.levelData); // Workaround, since this machine depends in the notebook existence
    }

    public bool ValidatePosition(EditorLevelData data)
    {
        IntVector2 alignedPosition = position.ToCellVector();
        var room = data.RoomFromPos(alignedPosition, true);
        bool existStructures = data.structures.Exists( // Basically checks if there's no other notebook machine occupying the same spot. This is actually needed.
            str => str is NotebookMachineStructureLocation ntbLoc &&
                ntbLoc.machines.Exists(
                    machine => machine != this && machine.position.ToCellVector() == alignedPosition
                )
            );
        return room?.activity?.type == "notebook" && !existStructures;
    }
}

public class NotebookMachineStructureLocation : StructureLocation
{
    public List<NotebookMachineLocation> machines = [];
    public NotebookMachineLocation CreateMachine() => new() { owner = this };

    public override void AddStringsToCompressor(StringCompressor compressor) { }
    public override void CleanupVisual(GameObject visualObject) { }
    public override GameObject GetVisualPrefab() => null;
    public override void InitializeVisual(GameObject visualObject) => machines.ForEach(EditorController.Instance.AddVisual);

    public override StructureInfo Compile(EditorLevelData data, BaldiLevel level)
    {
        var info = new StructureInfo(type);
        foreach (var machine in machines)
            info.data.Add(new() { position = machine.position.ToCellVector().ToData() });
        return info;
    }

    public override void ReadInto(EditorLevelData data, BinaryReader reader, StringCompressor compressor)
    {
        _ = reader.ReadByte();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var machine = CreateMachine();
            machine.position = reader.ReadUnityVector3().ToUnity();
            machines.Add(machine);
        }
    }

    public override void ShiftBy(Vector3 worldOffset, IntVector2 cellOffset, IntVector2 sizeDifference)
    {
        foreach (var machine in machines)
            machine.position -= worldOffset;
    }

    public override void UpdateVisual(GameObject visualObject) => machines.ForEach(EditorController.Instance.UpdateVisual);

    public override bool ValidatePosition(EditorLevelData data)
    {
        for (int i = 0; i < machines.Count; i++)
        {
            if (!machines[i].ValidatePosition(data))
                machines[i].OnDelete(data);
        }

        return machines.Count != 0;
    }

    public override void Write(EditorLevelData data, BinaryWriter writer, StringCompressor compressor)
    {
        writer.Write((byte)0);
        writer.Write(machines.Count);
        foreach (var machine in machines)
        {
            writer.Write(machine.position.ToData());
        }
    }
}