
using HarmonyLib;
using Verse;
using static CherryPicker.ModSettings_CherryPicker;

namespace CherryPicker
{
	[HarmonyPatch(typeof(DebugThingPlaceHelper), nameof(DebugThingPlaceHelper.IsDebugSpawnable))]
	public class Patch_IsDebugSpawnable
	{
		//Makes the devmode spawner respect the psueod-removed defs
		static public bool Prefix(ThingDef def, bool __result)
		{	
			if (removedDefs.Contains(def.defName)) {
				__result = false;
				return false;
				}
			return true;
		}
    }
}
