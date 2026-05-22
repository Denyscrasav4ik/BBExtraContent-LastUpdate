using System.Collections.Generic;
using BaldisBasicsPlusAdvanced.API;
using BaldisBasicsPlusAdvanced.Game.Activities;
using BBTimes.Manager;
using BBTimes.Plugin;
using HarmonyLib;
using MTM101BaldAPI;

namespace BBTimes.CompatibilityModule
{
	internal class AdvancedEditionCompat
	{
		internal static void Loadup()
		{
			List<string> strs = [];
			for (int i = 1; i <= elvTips; i++)
				strs.Add($"times_elv_tip{i}");
			ApiManager.AddNewTips(BBTimesManager.plug.Info, [.. strs]);

			// Very important for christmas mode
			if (Storage.IsChristmas)
				PitOverrides.DisableEnglishClass();
		}
		const int elvTips = 9;
	}
}
