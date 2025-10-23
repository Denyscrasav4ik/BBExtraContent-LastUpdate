using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusLevelStudio.Editor.Tools;
using UnityEngine;

namespace BBTimes.CompatibilityModule.EditorCompat.Events;

public class SuperFanTool : PlaceAndRotateTool
{
    public override string id => "object_timessuperfansmarker";

    public SuperFanTool(Sprite sprite)
    {
        this.sprite = sprite;
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
                prefab = "timessuperfansmarker",
                position = position.ToWorld() + (dir.ToVector3() * 4.8f),
                rotation = dir.GetOpposite().ToRotation()
            };
            EditorController.Instance.levelData.objects.Add(obj);
            EditorController.Instance.AddVisual(obj);
            return true;
        }
        return false;
    }
}