
using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using static CherryPicker.ModSettings_CherryPicker;

namespace CherryPicker
{
	[HarmonyPatch(typeof(DebugThingPlaceHelper), nameof(DebugThingPlaceHelper.IsDebugSpawnable))]
	public class Patch_IsDebugSpawnable
	{
		//Makes the devmode spawner respect the psuedo-removed defs
		static public bool Prefix(ThingDef def, bool __result)
		{	
			if (removedDefs.Contains(def.defName)) {
				__result = false;
				return false;
				}
			return true;
		}
    }

	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
	public class Patch_Wear
	{
		//Attempts to block mods that use c# to equip pawns when it generates them
		static public bool Prefix(Apparel newApparel)
		{	
			return !removedDefs.Contains(newApparel?.def.defName);
		}
    }

	[HarmonyPatch(typeof(ApparelRequirement), nameof(ApparelRequirement.AllRequiredApparel))]
	public class Patch_AllRequiredApparel
	{
		static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> values)
		{
			foreach (var thingDef in values)
			{
				if (!removedDefs.Contains(thingDef?.defName)) yield return thingDef;
			}
		}
    }
}
