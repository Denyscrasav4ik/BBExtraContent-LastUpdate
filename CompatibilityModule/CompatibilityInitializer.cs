using BBTimes.CompatibilityModule.EditorCompat;
using BBTimes.CompatibilityModule.GrapplingHookTweaksCompats;
using BBTimes.Plugin;
using BepInEx.Bootstrap;
using MTM101BaldAPI.AssetTools;

namespace BBTimes.CompatibilityModule
{
	internal static class CompatibilityInitializer
	{
		internal static void InitializeOnLoadMods()
		{
			if (Chainloader.PluginInfos.ContainsKey(Storage.guid_HookTweaks))
				GrapplingHookTweaksCompat.Loadup();
		}
		internal static void InitializePostOnLoadMods()
		{
			// if (BBTimesManager.plug.HasInfiniteFloors && !BBTimesManager.plug.disableArcadeRennovationsSupport.Value)
			// 	ArcadeRenovationsCompat.Loadup();
		}
		internal static void InitializePostSetup(AssetManager man)
		{
			if (Chainloader.PluginInfos.ContainsKey(Storage.guid_LevelStudio))
				EditorIntegration.Initialize(man);
		}
		internal static void InitializeOnAwake()
		{
			if (Chainloader.PluginInfos.ContainsKey(Storage.guid_CustomMusics))
				CustomMusicsCompat.Loadup();
			if (Chainloader.PluginInfos.ContainsKey(Storage.guid_CustomVendingMachines))
				CustomVendingMachinesCompat.Loadup();
			if (Chainloader.PluginInfos.ContainsKey(Storage.guid_CustomPosters))
				CustomPostersCompat.Loadup();
			if (Chainloader.PluginInfos.ContainsKey(Storage.guid_Advanced))
				AdvancedEditionCompat.Loadup();
		}
	}
}
