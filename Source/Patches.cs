using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.CherryPickerUtility;

namespace CherryPicker
{
	//Makes the devmode spawner respect the psuedo-removed defs
	[HarmonyPatch(typeof(DebugThingPlaceHelper), nameof(DebugThingPlaceHelper.IsDebugSpawnable))]
	public class Patch_IsDebugSpawnable
	{
		static public bool Postfix(bool __result, ThingDef def)
		{	
			return processedDefs.Contains(def) ? false : __result;
		}
    }

	//Attempts to block mods that use c# to equip pawns when it generates them
	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
	[HarmonyPriority(Priority.First)]
	public class Patch_Wear
	{
		static public bool Prefix(Apparel newApparel)
		{	
			return processedDefs.Contains(newApparel?.def) ? false : true;
		}
    }

	[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.IsStuff), MethodType.Getter)]
	public class Patch_IsStuff
	{
		static public bool Postfix(bool __result, ThingDef __instance)
		{
			return Current.ProgramState == ProgramState.MapInitializing && processedDefs.Contains(__instance) ? false : __result;
		}
	}

	//We patch the next 2 methods because some mods add quests through c# and ignore cherry picker otherwise
	[HarmonyPatch(typeof(QuestUtility), nameof(QuestUtility.SendLetterQuestAvailable))]
	public class Patch_QuestUtility
	{
		static public bool Prefix(Quest quest)
		{	
			return processedDefs.Contains(quest?.root) ? false : true;
		}
    }

	[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Add))]
	public class Patch_QuestManager
	{
		static public bool Prefix(Quest quest)
		{	
			return processedDefs.Contains(quest?.root) ? false : true;
		}
    }

	//This is for ideology roles
	[HarmonyPatch(typeof(ApparelRequirement), nameof(ApparelRequirement.AllRequiredApparel))]
	public class Patch_AllRequiredApparel
	{
		static IEnumerable<ThingDef> Postfix(IEnumerable<ThingDef> values)
		{
			foreach (var thingDef in values) if (!processedDefs.Contains(thingDef)) yield return thingDef;
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
			return processedDefs.Contains(hediff.def) ? false : true;
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
