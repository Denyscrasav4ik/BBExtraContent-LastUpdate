using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat;

public class StructureOnWallPlacementTool : PlaceAndRotateTool
{
    public override string id => "structure_" + key;
    readonly public string key;
    readonly public float dirOffset, verticalOffset;
    readonly public bool useOppositeRotation;

    public StructureOnWallPlacementTool(string key, Sprite sprite, float dirOffset = 4.99f, float yOffset = 5f, bool useOppositeRotation = true)
    {
        this.key = key;
        this.sprite = sprite;
        this.dirOffset = dirOffset;
        verticalOffset = yOffset;
        this.useOppositeRotation = useOppositeRotation;
    }

    public override bool ValidLocation(IntVector2 position)
    {
        if (!base.ValidLocation(position)) return false;
        for (int i = 0; i < 4; i++)
        {
            if (EditorController.Instance.levelData.WallFree(position, (Direction)i, false)) return true;
        }
        return false;
    }

    protected override bool TryPlace(IntVector2 position, Direction dir)
    {
        if (EditorController.Instance.levelData.WallFree(position, dir, false))
        {
            EditorController.Instance.AddUndo();
            BasicObjectLocation obj = new()
            {
                prefab = key,
                position = position.ToWorld() + (dir.ToVector3() * dirOffset) + Vector3.up * verticalOffset,
                rotation = !useOppositeRotation ? dir.ToRotation() : dir.GetOpposite().ToRotation()
            };
            EditorController.Instance.levelData.objects.Add(obj);
            EditorController.Instance.AddVisual(obj);
            return true;
        }
        return false;
    }
}