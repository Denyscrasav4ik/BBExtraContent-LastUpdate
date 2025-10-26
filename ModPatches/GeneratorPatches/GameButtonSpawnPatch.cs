using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BBTimes.ModPatches
{

    [HarmonyPatch]
    internal static class GameButtonSpawnPatch
    {
        [HarmonyTargetMethods]
        static MethodBase[] GetGameButtonMethod() =>
        [
            AccessTools.Method(typeof(GameButton), nameof(GameButton.BuildInArea), [typeof(EnvironmentController), typeof(IntVector2), typeof(int), typeof(GameObject), typeof(GameButtonBase), typeof(System.Random), typeof(bool).MakeByRefType()]),
            AccessTools.Method(typeof(GameButton), nameof(GameButton.Build))
        ];

        [HarmonyPostfix]
        static void AddButtonMapIcon(EnvironmentController ec, GameButtonBase __result)
        {
            if (!__result) return;

            string name = Directions.DirFromVector3(__result.transform.forward, 45f).ToString();
            Debug.Log($"Added button with dir: {name}");
            ec.map.AddIcon(butIconPre.First(x => x.name.EndsWith(name)), __result.transform, Color.white);
        }

        internal static MapIcon[] butIconPre;

    }
}

