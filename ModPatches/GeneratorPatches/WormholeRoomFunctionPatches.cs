using System.Collections.Generic;
using HarmonyLib;

namespace BBTimes.ModPatches.GeneratorPatches;

[HarmonyPatch(typeof(WormholeRoomFunction))]
internal static class WormholeRoomFunctionPatches
{
    [HarmonyPatch("Build"), HarmonyPostfix]
    static void AddCustomClassrooms(WormholeRoomFunction __instance, LevelBuilder builder)
    {
        foreach (RoomController roomController in builder.Ec.rooms)
        {
            if (allowedClassRooms.Contains(roomController.category))
            {
                __instance.classrooms[builder.GetRegionIdFromPosition(roomController.position)].Add(roomController);
            }
        }
    }

    public static HashSet<RoomCategory> allowedClassRooms = [];
}