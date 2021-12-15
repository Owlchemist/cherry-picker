
using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System;
using static CherryPicker.ModSettings_CherryPicker;

namespace CherryPicker
{
	//Makes the devmode spawner respect the psuedo-removed defs
	[HarmonyPatch(typeof(DebugThingPlaceHelper), nameof(DebugThingPlaceHelper.IsDebugSpawnable))]
	public class Patch_IsDebugSpawnable
	{
		static public bool Prefix(ThingDef def, bool __result)
		{	
			if (allRemovedDefs.Contains(def.ToKey()))
			{
				__result = false;
				return false;
			}
			return true;
		}
    }

	//Attempts to block mods that use c# to equip pawns when it generates them
	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
	public class Patch_Wear
	{
		static public bool Prefix(Apparel newApparel)
		{	
			return !allRemovedDefs.Contains(newApparel?.def.ToKey());
		}
    }

	//This is for ideology roles
	[HarmonyPatch(typeof(ApparelRequirement), nameof(ApparelRequirement.AllRequiredApparel))]
	public class Patch_AllRequiredApparel
	{
		static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> values)
		{
			foreach (var thingDef in values)
			{
				if (!allRemovedDefs.Contains(thingDef.ToKey())) yield return thingDef;
			}
		}
    }

	[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new Type[]
	{
		typeof(Hediff),
		typeof(BodyPartRecord),
		typeof(DamageInfo?),
		typeof(DamageWorker.DamageResult)
	})]
	public class Patch_AddHediff
	{
		static bool Prefix(Hediff hediff)
		{
			return !allRemovedDefs.Contains(hediff.def.ToKey());
		}
    }

	//Run a second time to catch defs that are generated on runtime
	[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
	public class Patch_MainMenuDrawer_MainMenuOnGUI
	{
		static bool hasRan;
		static void Postfix()
		{
			if (!hasRan) CherryPickerUtility.Setup(true);
			hasRan = true;
		}
    }
}
