using PlusLevelStudio;
using PlusLevelStudio.Editor;
using PlusStudioLevelFormat;
using PlusStudioLevelLoader;

namespace BBTimes.CompatibilityModule.EditorCompat.Events;

public class SuperFanMarker : CellMarker
{
    public override void Compile(EditorLevelData data, BaldiLevel compiled)
    {
        ushort num = data.RoomIdFromPos(position, true);
        if (num == 0)
        {
            throw new System.Exception("Uh oh, no room found for super fan.");
        }

        compiled.rooms[num - 1].basicObjects.Add(new BasicObjectInfo
        {
            position = position.ToWorld().ToData(),
            prefab = type,
            rotation = default
        });
    }

    public override bool ValidatePosition(EditorLevelData data)
    {
        EditorRoom editorRoom = data.RoomFromPos(position, true);

        return
        editorRoom != null &&
        editorRoom.roomType == "hall" &&
        data.cells[position.x, position.z].walls != 0; // It can't be a place with no walls
    }
}