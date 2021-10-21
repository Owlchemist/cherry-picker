using Verse;
using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using static CherryPicker.ModSettings_CherryPicker;
 
namespace CherryPicker
{
	#if DEBUG
	[HotSwap.HotSwappable]
	#endif
    internal static class CherryPickerUtility
	{
		public static Def[] allDefs;
		public static List<Def> originalDefs = new List<Def>();
		public static List<string> workingList = new List<string>();
		public static bool filtered;
		public static List<string> report = new List<string>();
		public static int lineNumber = 0;

		public static void Setup()
		{
			var thingDefs = DefDatabase<ThingDef>.AllDefs.Where(x => !x.IsBlueprint && !x.IsFrame && !x.IsCorpse && !x.isUnfinishedThing &&
			(x.category == ThingCategory.Item || x.category == ThingCategory.Building || x.category == ThingCategory.Plant));
			var terrainDefs = DefDatabase<TerrainDef>.AllDefs;
			var recipeDefs = DefDatabase<RecipeDef>.AllDefs;

			var tmp = new List<Object>(thingDefs.Count() + terrainDefs.Count() + recipeDefs.Count());
			tmp.AddRange(thingDefs); tmp.AddRange(terrainDefs); tmp.AddRange(recipeDefs);
			allDefs = tmp.Cast<Def>().ToArray();

			thingDefs = null; terrainDefs = null; recipeDefs = null; tmp = null;

			MakeWorkingList();
			ProcessList();
		}

		//The worklist list differs from the removedDefs list in that it only contains defs for the current modlist.
		//This is to prevent defs on the player's list from being messed with if they have temporarily disabled a mod.
		public static void MakeWorkingList()
		{
			workingList.Clear();
			for (int i = 0; i < allDefs.Length; ++i)
			{
				var def = allDefs[i];
				if (removedDefs.Contains(def?.defName)) workingList.Add(def?.defName);
			}
		}

		public static int Search(Def def)
		{
			string input = (def.defName + def.label + def.modContentPack?.Name + def.GetType().Name).ToLower();
			//if (def.defName == "Concrete") Log.Message(((int)Math.Ceiling((float)(input.Length - input.Replace(filter.ToLower(), String.Empty).Length) / (float)filter.Length)).ToString());
			return (int)Math.Ceiling((float)(input.Length - input.Replace(filter.ToLower(), String.Empty).Length) / (float)filter.Length);
		}

		public static void DrawListItem(this Listing_Standard options, Def def)
		{
			if (def == null || (filtered && Search(def) == 0)) return;
			++lineNumber;

			bool flag = !workingList?.Contains(def.defName) ?? false;
			options.BetterCheckboxLabeled("[" + def.GetType().Name + " :: " + def.modContentPack?.Name + " :: " + def.defName + "] " + def.label, ref flag, null);
			
			//Add to list if missing
			if (!flag && !workingList.Contains(def.defName)) workingList.Add(def.defName);
			//Remove from list
			else if (flag && workingList.Contains(def.defName)) workingList.Remove(def.defName);
		}

		static void BetterCheckboxLabeled(this Listing_Standard option, string label, ref bool checkOn, string tooltip = null)
		{
			float lineHeight = Text.LineHeight;
			UnityEngine.Rect rect = option.GetRect(lineHeight);
			if (option.BoundingRectCached == null || rect.Overlaps(option.BoundingRectCached.Value))
			{
				if (!tooltip.NullOrEmpty())
				{
					if (Mouse.IsOver(rect))
					{
						Widgets.DrawHighlight(rect);
					}
					TooltipHandler.TipRegion(rect, tooltip);
				}
				Widgets.CheckboxLabeled(rect, label, ref checkOn, false, null, null, false);
			}
			option.Gap(option.verticalSpacing);
			if (lineNumber % 2 != 0) Widgets.DrawLightHighlight(rect);
		}

		public static void ProcessList()
		{
			report.Clear();
			//Add to list if missing
			for (int i = 0; i < workingList.Count; ++i)
			{
				var defName = workingList[i];
				if (!removedDefs.Contains(defName)) removedDefs.Add(defName);
			}
			//Remove any list entries as needed
			bool restartNeeded = false;
			for (int i = 0; i < removedDefs.Count; ++i)
			{
				var defName = removedDefs[i];
				if (allDefs.Any(x => x.defName == defName))
				{
					if (!workingList.Contains(defName)) 
					{
						removedDefs.RemoveAt(i);
						--i;
						restartNeeded = true;
					}
					else
					{
						report.Add(defName);
						PsuedoRemoveDef(defName);
					}
				}
			}
			//Give report
			if (report.Count > 0) Log.Message("[Cherry Picker] The following defs have been psuedo removed: " + string.Join(", ", report));

			//Refresh the working list
			MakeWorkingList();

			if (restartNeeded)
			{
				Find.WindowStack.Add(new Dialog_MessageBox("CherryPicker.RestartRequired".Translate(), null, null, null, null, "CherryPicker.RestartHeader".Translate(), true, null, null, WindowLayer.Dialog));
			}
		}

		static void PsuedoRemoveDef(string defName)
		{
			try
			{
				if (DefDatabase<ThingDef>.GetNamed(defName, false) != null)
				{
					ThingDef thingDef = DefDatabase<ThingDef>.GetNamed(defName, false) as ThingDef;

					thingDef.BaseMarketValue = 0f; //Market value considered for some spawning
					thingDef.tradeability = Tradeability.None; //Won't be sold or come from drop pods
					thingDef.thingCategories?.Clear(); //Filters
					thingDef.thingSetMakerTags?.Clear(); //Quest rewards
					DefDatabase<ScenarioDef>.AllDefs.Select(x => x.scenario.parts?.RemoveAll
						(y => y.GetType() == typeof(ScenPart_StartingThing_Defined) && y.ChangeType<ScenPart_StartingThing_Defined>().thingDef == thingDef)); //Scenario starting items
					
					if (thingDef.category == ThingCategory.Building)
					{
						var originalDesignationCategory = thingDef.designationCategory;
						thingDef.designationCategory = null; //Hide from architect menus
						if (originalDesignationCategory != null) originalDesignationCategory.ResolveReferences();
						thingDef.minifiedDef = null; //Removes from storage filters
						thingDef.researchPrerequisites = null; //Removes from research UI
					}
					else if (thingDef.category == ThingCategory.Item)
					{
						thingDef.recipeMaker?.recipeUsers.Clear();
						//Is this item equipment?
						if (thingDef.thingClass == typeof(Apparel) || thingDef.equipmentType == EquipmentType.Primary)
						{
							thingDef.weaponTags?.Clear();
							DefDatabase<PawnKindDef>.AllDefs.Where(x => x.apparelRequired?.Remove(thingDef) ?? false);
						}
					}
				}
				else if (DefDatabase<TerrainDef>.GetNamed(defName, false) != null)
				{
					TerrainDef terrainDef = DefDatabase<TerrainDef>.GetNamed(defName, false) as TerrainDef;

					var originalDesignationCategory = terrainDef.designationCategory;
					terrainDef.designationCategory = null; //Hide from architect menus
					if (originalDesignationCategory != null) originalDesignationCategory.ResolveReferences();

				}
				else if (DefDatabase<RecipeDef>.GetNamed(defName, false) != null)
				{
					RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamed(defName, false) as RecipeDef;

					foreach (var thingDef in recipeDef.recipeUsers ?? Enumerable.Empty<ThingDef>())
					{
						thingDef.recipes?.Remove(recipeDef);
						thingDef.allRecipesCached?.Remove(recipeDef);
					}
					recipeDef.recipeUsers?.Clear();
				}
			}
			//In the event there's a bug, this will at least allow the user to not be stuck in the options menu
			catch (System.NullReferenceException)
			{
				Log.Warning("[Cherry Picker] unable to process "  + defName);
				return;
			}
		}
	}
}
