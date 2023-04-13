using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System;
using System.Reflection.Emit;
using static CherryPicker.CherryPickerUtility;

namespace CherryPicker
{
	public static class DynamicPatches
	{
		//[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.IsStuff), MethodType.Getter)]
		public static IEnumerable<CodeInstruction> Transpiler_IsStuff(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
			var label = generator.DefineLabel();
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Ldarg_0)
				{
					instruction.labels.Add(label);
					break;
				}
			}
			
			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DynamicPatches), nameof(FilterStuff)));
			yield return new CodeInstruction(OpCodes.Brfalse, label); //If not filtered then just transfer control to normal vanilla handling
			yield return new CodeInstruction(OpCodes.Ldc_I4_0); //Otherwise, push false to the return
			yield return new CodeInstruction(OpCodes.Ret);

			foreach (var instruction in instructions)
			{
				yield return instruction;
			}
		}
		public static bool FilterStuff(ThingDef __instance)
		{
			return Current.ProgramState == ProgramState.MapInitializing && processedDefs.Contains(__instance);
		}
	}

	//Makes the devmode spawner respect the psuedo-removed defs
	[HarmonyPatch(typeof(DebugThingPlaceHelper), nameof(DebugThingPlaceHelper.IsDebugSpawnable))]
	class Patch_IsDebugSpawnable
	{
		static bool Postfix(bool __result, ThingDef def)
		{	
			return processedDefs.Contains(def) ? false : __result;
		}
    }

	//Attempts to block mods that use c# to equip pawns when it generates them
	[HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
	[HarmonyPriority(Priority.First)]
	class Patch_Wear
	{
		static bool Prefix(Apparel newApparel)
		{	
			return processedDefs.Contains(newApparel?.def) ? false : true;
		}
    }

	//We patch the next 2 methods because some mods add quests through c# and ignore cherry picker otherwise
	[HarmonyPatch(typeof(QuestUtility), nameof(QuestUtility.SendLetterQuestAvailable))]
	class Patch_QuestUtility
	{
		static bool Prefix(Quest quest)
		{	
			return processedDefs.Contains(quest?.root) ? false : true;
		}
    }

	[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Add))]
	class Patch_QuestManager
	{
		static bool Prefix(Quest quest)
		{	
			return processedDefs.Contains(quest?.root) ? false : true;
		}
    }

	//This is for ideology roles
	[HarmonyPatch(typeof(ApparelRequirement), nameof(ApparelRequirement.AllRequiredApparel))]
	class Patch_AllRequiredApparel
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
	class Patch_AddHediff
	{
		static bool Prefix(Hediff hediff)
		{
			return processedDefs.Contains(hediff.def) ? false : true;
		}
    }

	//Run a second time to catch defs that are generated on runtime
	[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
	class Patch_MainMenuDrawer_MainMenuOnGUI
	{
		static bool hasRan;
		static void Postfix()
		{
			if (!hasRan) 
			{
				CherryPickerUtility.Setup(true);
				hasRan = true;
			}
		}
    }
}