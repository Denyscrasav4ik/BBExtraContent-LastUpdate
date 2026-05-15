using System.Collections.Generic;
using System.Reflection;
using BBTimes.CustomComponents;
using BBTimes.Manager;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace BBTimes.ModPatches
{
    [HarmonyPatch(typeof(StructureBuilder), "Finished")]
    internal class LogFinishedStructures
    {
        static void Prefix(StructureBuilder __instance) =>
            Debug.LogWarning($"The builder {__instance.GetType().Name} is finished.");
    }
}
