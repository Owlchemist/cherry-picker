using Verse;
using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse.Sound;
using static CherryPicker.ModSettings_CherryPicker;
 
namespace CherryPicker
{
    internal static class CherryPickerUtility
	{
		public static Def[] allDefs; //All defs this mod supports editting
		public static HashSet<string> workingList = new HashSet<string>(); //A copy of the user's removed defs but only the defs loaded in this modlist
		public static List<string> report = new List<string>(); //Gives a console print of changes made
		public static Dictionary<string, string> labelCache = new Dictionary<string, string>();
		public static HashSet<Def> hardRemovedDefs = new HashSet<Def>(); //Used to keep track of direct DB removals
		public static bool filtered; //Tells the script the filter box is being used
		public static int lineNumber = 0; //Handles row highlighting and also dynamic window size for the scroll bar
		public static float cellPosition = 8f; //Tracks the vertical pacement in pixels
		public static System.Reflection.Assembly rootAssembly;
		public const float lineHeight = 22f; //Text.LineHeight + options.verticalSpacing;

		public static void Setup()
		{
			rootAssembly = typeof(ThingDef).Assembly;

			var timer = new System.Diagnostics.Stopwatch();
  			timer.Start();

			//Fetch all our def lists across multiple categories
			System.Object[] defLists = new System.Object[11];
			defLists[0] = DefDatabase<ThingDef>.AllDefsListForReading.Where(x => !x.IsBlueprint && !x.IsFrame && !x.IsCorpse && !x.isUnfinishedThing &&
							(x.category == ThingCategory.Item || x.category == ThingCategory.Building || x.category == ThingCategory.Plant || x.category == ThingCategory.Pawn));
			defLists[1] = DefDatabase<TerrainDef>.AllDefsListForReading;
			defLists[2] = DefDatabase<RecipeDef>.AllDefsListForReading;
			defLists[3] = DefDatabase<TraitDef>.AllDefsListForReading;
			defLists[4] = DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(x => DefDatabase<ResearchProjectDef>.AllDefsListForReading.
							Any(y => (!y.prerequisites?.Contains(x) ?? true) && (!y.hiddenPrerequisites?.Contains(x) ?? true)));
			defLists[5] = DefDatabase<DesignationCategoryDef>.AllDefsListForReading;
			defLists[6] = DefDatabase<ThingStyleDef>.AllDefsListForReading;
			defLists[7] = DefDatabase<QuestScriptDef>.AllDefsListForReading.Where(x => !DefDatabase<IncidentDef>.AllDefsListForReading.Any(y => y.questScriptDef == x));
			defLists[8] = DefDatabase<IncidentDef>.AllDefsListForReading;
			defLists[9] = DefDatabase<HediffDef>.AllDefsListForReading;
			defLists[10] = DefDatabase<ThoughtDef>.AllDefsListForReading;

			//Temp working collection to merge everything together
			var tmp = new List<System.Object>();
			foreach (IEnumerable<Def> list in defLists)
			{
				tmp.AddRange(list);
			}

			//Final collection
			allDefs = tmp.Cast<Def>().ToArray();

			//Free memory since this is a static member
			defLists = null; tmp = null;

			//Process lists
			if (legacyKeys?.Count > 0) ConvertLegacy(); //Convert to new version
			MakeLabelCache();
			MakeWorkingList();
			ProcessList();

			timer.Stop();
			TimeSpan timeTaken = timer.Elapsed;

			//Give report
			if (report.Count > 0) Log.Message("[Cherry Picker] The database was processed in " + timeTaken.ToString(@"ss\.fffff") + " seconds and the following defs were removed: " + string.Join(", ", report));
		}

		//Temporary code, will be deleted after a few weeks
		public static void ConvertLegacy()
		{
			var legacyKeysWorkingList = legacyKeys.ToList();
			foreach (string key in legacyKeysWorkingList)
			{
				string newKey = "";
				newKey = GetKey(GetDef(key, null));
				
				if (!newKey.NullOrEmpty())
				{
					removedDefs.Add(newKey);
					legacyKeys.Remove(key);
				}
			}
			LoadedModManager.GetMod<Mod_CherryPicker>().modSettings.Write();
		}

		public static string GetKey(Def def)
		{
			return def == null ? "" : (def.GetType().Name + "/" + def.defName);
		}

		public static string GetDefName(string def)
		{
			return def.Split('/')[1];
		}

		public static Type GetDefType(string def)
		{
			return rootAssembly.GetType("Verse." + def.Split('/')[0]);
		}
		public static Def GetDef(string key)
		{
			return GetDef(GetDefName(key), GetDefType(key));
		}
		public static Def GetDef(string defName, Type type)
		{
			if (defName.NullOrEmpty()) return null;

			//ToDo: I'm sure there's a way to convert this to a one-liner using reflection
			
			if ((type == null || type == typeof(ThingDef)) && DefDatabase<ThingDef>.defsByName.TryGetValue(defName, out ThingDef thingDef)) return thingDef;
			
			if ((type == null || type == typeof(TerrainDef)) && DefDatabase<TerrainDef>.defsByName.TryGetValue(defName, out TerrainDef terrainDef)) return terrainDef;
			
			if ((type == null || type == typeof(RecipeDef)) && DefDatabase<RecipeDef>.defsByName.TryGetValue(defName, out RecipeDef recipeDef)) return recipeDef;
			
			if ((type == null || type == typeof(TraitDef)) && DefDatabase<TraitDef>.defsByName.TryGetValue(defName, out TraitDef traitDef)) return traitDef;
			
			if ((type == null || type == typeof(ResearchProjectDef)) && DefDatabase<ResearchProjectDef>.defsByName.TryGetValue(defName, out ResearchProjectDef researchProjectDef)) return researchProjectDef;
			
			if ((type == null || type == typeof(DesignationCategoryDef)) && DefDatabase<DesignationCategoryDef>.defsByName.TryGetValue(defName, out DesignationCategoryDef designationCategoryDef)) return designationCategoryDef;
			
			if ((type == null || type == typeof(ThingStyleDef)) && DefDatabase<ThingStyleDef>.defsByName.TryGetValue(defName, out ThingStyleDef thingStyleDef)) return thingStyleDef;

			if ((type == null || type == typeof(QuestScriptDef)) && DefDatabase<QuestScriptDef>.defsByName.TryGetValue(defName, out QuestScriptDef questScriptDef)) return questScriptDef;

			if ((type == null || type == typeof(IncidentDef)) && DefDatabase<IncidentDef>.defsByName.TryGetValue(defName, out IncidentDef incidentDef)) return incidentDef;

			if ((type == null || type == typeof(HediffDef)) && DefDatabase<HediffDef>.defsByName.TryGetValue(defName, out HediffDef hediffDef)) return hediffDef;

			if ((type == null || type == typeof(HediffDef)) && DefDatabase<ThoughtDef>.defsByName.TryGetValue(defName, out ThoughtDef thoughtDef)) return thoughtDef;

			foreach (Def hardRemovedDef in hardRemovedDefs)
			{
				if (hardRemovedDef.defName == defName && hardRemovedDef.GetType().Name == type?.Name) return hardRemovedDef;
			}
			
			return null;
		}

		public static void MakeLabelCache()
		{
			foreach (Def def in allDefs)
			{
				labelCache.Add(GetKey(def), def.GetType().Name + " :: " + def.modContentPack?.Name + " :: " + def.defName);
			}
		}

		//The worklist list differs from the removedDefs list in that it only contains defs for the current modlist.
		//This is to prevent defs on the player's list from being messed with if they have temporarily disabled a mod.
		public static void MakeWorkingList()
		{
			workingList.Clear();
			foreach (var key in removedDefs)
			{
				var def = GetDef(key);
				if (allDefs.Any(x => x == def)) workingList.Add(key);
			}
		}

		public static int Search(Def def)
		{
			string input = (def.defName + def.label + def.modContentPack?.Name + def.GetType().Name).ToLower();
			return (int)Math.Ceiling((float)(input.Length - input.Replace(filter.ToLower(), String.Empty).Length) / (float)filter.Length);
		}

		public static void DrawListItem(Listing_Standard options, Def def)
		{
			//Prepare key
			string key = GetKey(def);

			//Determine checkbox status...
			bool checkOn = !workingList?.Contains(key) ?? false;
			//Draw...
			Rect rect = options.GetRect(lineHeight);
			rect.y = cellPosition;

			//Actually draw the line item
			if (options.BoundingRectCached == null || rect.Overlaps(options.BoundingRectCached.Value))
			{
				CheckboxLabeled(rect, labelCache[key], def.label, ref checkOn, def);
			}

			//Handle row coloring and spacing
			options.Gap(options.verticalSpacing);
			if (lineNumber % 2 != 0) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);

			//Tooltip
			TooltipHandler.TipRegion(rect, labelCache[key] + "\n\n" + def.description);
			
			//Add to working list if missing
			if (!checkOn && !workingList.Contains(key)) workingList.Add(key);
			//Remove from working list
			else if (checkOn && workingList.Contains(key)) workingList.Remove(key);
		}

		public static void CheckboxLabeled(Rect rect, string data, string label, ref bool checkOn, Def def)
		{
			Rect leftHalf = rect.LeftHalf();
			
			//Is there an icon?
			Rect iconRect = new Rect(leftHalf.x, leftHalf.y, 32f, leftHalf.height);
			Texture2D icon = null;
			if (def is BuildableDef) icon = ((BuildableDef)def).uiIcon;
			else if (def is RecipeDef) icon = ((RecipeDef)def).ProducedThingDef?.uiIcon;
			if (icon != null) GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true, 1f, Color.white, 0f, 0f);

			//If there is a label, split the cell in half, otherwise use the full cell for data
			if (!label.NullOrEmpty())
			{
				Rect dataRect = new Rect(iconRect.xMax, iconRect.y, leftHalf.width - 32f, leftHalf.height);

				Widgets.Label(dataRect, data?.Truncate(dataRect.width - 12f, InspectPaneUtility.truncatedLabelsCached));
				Rect rightHalf = rect.RightHalf();
				Widgets.Label(rightHalf, label.Truncate(rightHalf.width - 12f, InspectPaneUtility.truncatedLabelsCached));
			}
			else
			{
				Rect dataRect = new Rect(iconRect.xMax, iconRect.y, rect.width - 32f, leftHalf.height);
				Widgets.Label(dataRect, data?.Truncate(dataRect.width - 12f, InspectPaneUtility.truncatedLabelsCached));
			}

			//Checkbox
			if (Widgets.ButtonInvisible(rect, true))
			{
				checkOn = !checkOn;
				if (checkOn) SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
				else SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
			}
			Widgets.CheckboxDraw(rect.xMax - 24f, rect.y, checkOn, false, 24f, null, null);
		}

		public static void ProcessList()
		{
			report.Clear();
			//If a def was just added to the remove list and it's not already on our saved list...
			workingList.ToList().ForEach(x => removedDefs.Add(x));
			
			//Now that the removed list has been updated above, process it, adding and removing as needed
			bool restartNeeded = false;
			var removedDefsWorkingList = removedDefs.ToArray();
			foreach (string key in removedDefsWorkingList)
			{
				Def def = GetDef(key);
				//Does this removed def exist in this current modlist?
				if (allDefs.Any(x => x == def))
				{
					//A def was removed from the removedDefs list?
					if (!workingList.Contains(key)) 
					{
						removedDefs.Remove(key);
						restartNeeded = true;
					}
					else report.Add(PsuedoRemoveDef(def) ? "\n - " + key : ("\n<color=red> - Failed: " + key + "</color>"));
				}
			}

			if (restartNeeded)
			{
				Find.WindowStack.Add(new Dialog_MessageBox("CherryPicker.RestartRequired".Translate(), null, null, null, null, "CherryPicker.RestartHeader".Translate(), true, null, null, WindowLayer.Dialog));
			}
		}

		static bool PsuedoRemoveDef(Def def)
		{
			bool reloadRequired = false;

			try
			{
				switch(def.GetType().Name)
				{
					case nameof(Verse.ThingDef):
					{
						ThingDef thingDef = def as ThingDef;
						thingDef.BaseMarketValue = 0f; //Market value considered for some spawning
						thingDef.tradeability = Tradeability.None; //Won't be sold or come from drop pods
						thingDef.thingCategories?.Clear(); //Filters
						thingDef.thingSetMakerTags?.Clear(); //Quest rewards

						//Update categories
						thingDef.thingCategories?.ForEach(x => x.ThisAndChildCategoryDefs?.ToList().ForEach
						(y => 
							{
								y.allChildThingDefsCached?.Remove(thingDef);
								y.sortedChildThingDefsCached?.Remove(thingDef);
								y.childThingDefs?.Remove(thingDef);
							}
						));

						//If mineable...
						thingDef.deepCommonality = 0;
						thingDef.deepCountPerCell = 0;
						thingDef.deepCountPerPortion = 0;
						thingDef.deepLumpSizeRange = IntRange.zero;

						//0'ing out the nutrition removes food from filters
						if (thingDef.ingestible != null) thingDef.SetStatBaseValue(StatDefOf.Nutrition,0);
						
						//Remove styles (Ideology)
						if (ModLister.IdeologyInstalled)
						{
							var styleCategoryDefs2 = DefDatabase<StyleCategoryDef>.AllDefsListForReading.Select(x => x.thingDefStyles);
							//List of lists
							foreach (List<ThingDefStyle> styleCategoryDef in styleCategoryDefs2)
							{
								var styleDefWorkingList = styleCategoryDef.ToList();
								//Go through this list
								foreach (ThingDefStyle thingDefStyles in styleCategoryDef)
								{
									if (thingDefStyles.thingDef == thingDef)
									{
										var styleDef = thingDefStyles.styleDef;
										DefDatabase<ThingStyleDef>.AllDefsListForReading.Remove(styleDef);
										styleDefWorkingList.Remove(thingDefStyles);
									}
								}
							}
						}
						
						//Buildables
						if (thingDef.category == ThingCategory.Building)
						{
							//Remove gizmo
							var originalDesignationCategory2 = thingDef.designationCategory;
							thingDef.designationCategory = null; //Hide from architect menus
							originalDesignationCategory2?.ResolveReferences();

							thingDef.minifiedDef = null; //Removes from storage filters
							thingDef.researchPrerequisites = null; //Removes from research UI

							//If mineable
							if (thingDef.building != null)
							{
								thingDef.building.mineableScatterCommonality = 0;
								thingDef.building.mineableScatterLumpSizeRange = IntRange.zero;
							}

							//Check if used for runes/junk on map gen
							DefDatabase<GenStepDef>.AllDefsListForReading.ForEach
							(x =>
								{
									if (x.genStep.GetType() == typeof(GenStep_ScatterGroup))
									{
										x.genStep.ChangeType<GenStep_ScatterGroup>().groups.ForEach(y => y.things.RemoveAll(z => z.thing == thingDef));
									}
									else if (x.genStep.GetType() == typeof(GenStep_ScatterThings) && x.genStep.ChangeType<GenStep_ScatterThings>().thingDef == thingDef)
									{
										x.genStep.ChangeType<GenStep_ScatterThings>().clusterSize = 0;
									}
								}
							);
						}
						//Items
						else if (thingDef.category == ThingCategory.Item)
						{
							//Scenario starting items
							DefDatabase<ScenarioDef>.AllDefsListForReading.ForEach
							(x => 
								x.scenario.parts?.RemoveAll(y => y.GetType() == typeof(ScenPart_StartingThing_Defined) && y.ChangeType<ScenPart_StartingThing_Defined>().thingDef == thingDef)
							);

							//Remove from recipe ingredients
							DefDatabase<RecipeDef>.AllDefsListForReading.ForEach
							(x =>
								{
									x.ingredients?.ForEach
									(y =>
										{
										y.filter?.thingDefs?.Remove(thingDef);
										y.filter?.allowedDefs?.Remove(thingDef);
										}
									);
									x.fixedIngredientFilter?.thingDefs?.Remove(thingDef);
									x.fixedIngredientFilter?.allowedDefs?.Remove(thingDef);
									x.defaultIngredientFilter?.thingDefs?.Remove(thingDef);
									x.defaultIngredientFilter?.allowedDefs?.Remove(thingDef);
								}
							);

							//Butchery and Costlists
							DefDatabase<ThingDef>.AllDefsListForReading.ForEach(x =>
							{
								x.costList?.RemoveAll(y => y.thingDef == thingDef);
								x.costListForDifficulty?.costList?.RemoveAll(y => y.thingDef == thingDef);
								x.butcherProducts?.RemoveAll(z => z.thingDef == thingDef);
							});

							//Makes this stuff material not show up in generated items
							if (thingDef.stuffProps != null) thingDef.stuffProps.commonality = 0;

							thingDef.recipeMaker?.recipeUsers.Clear();
							//Is this item equipment?
							if (thingDef.thingClass == typeof(Apparel) || thingDef.equipmentType == EquipmentType.Primary)
							{
								//Apparel?
								if (thingDef.apparel != null)
								{
									reloadRequired = true;

									thingDef.apparel.tags?.Clear();
									thingDef.apparel.defaultOutfitTags?.Clear();
									thingDef.apparel.canBeDesiredForIdeo = false;
									thingDef.apparel.ideoDesireAllowedFactionCategoryTags?.Clear();
									thingDef.apparel.ideoDesireDisallowedFactionCategoryTags?.Clear();
								}
								thingDef.weaponTags?.Clear();
								DefDatabase<PawnKindDef>.AllDefsListForReading.ForEach(x => x.apparelRequired?.Remove(thingDef));
								thingDef.techHediffsTags?.Clear();
								DefDatabase<PawnKindDef>.AllDefsListForReading.ForEach(x => x.techHediffsRequired?.Remove(thingDef));
								thingDef.comps?.Clear(); //Some scripts filter-select defs by their components. This should help exclude them
							}
						}
						//Plants
						else if (thingDef.category == ThingCategory.Plant)
						{
							foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefsListForReading)
							{
								biomeDef.wildPlants?.RemoveAll(x => x.plant == thingDef);
								biomeDef.cachedWildPlants?.RemoveAll(x => x == thingDef);
								biomeDef.cachedPlantCommonalities?.RemoveAll(x => x.Key == thingDef);
							}
						}
						//Pawns and animals
						else if (thingDef.category == ThingCategory.Pawn)
						{
							//Omits from farm animal related events
							thingDef.tradeTags?.Clear();
							//Omits from migration event
							thingDef.race.herdMigrationAllowed = false;
							//Omits from manhunter event
							DefDatabase<PawnKindDef>.AllDefsListForReading.ForEach(x => { if (x.defName == def.defName) x.canArriveManhunter = false ;} );

							/*
							if (thingDef.race.animalType == AnimalType.Dryad)
							{
								
							}
							*/
							
							var biomeDefs = DefDatabase<BiomeDef>.AllDefsListForReading;
							foreach (var biomeDef in biomeDefs)
							{
								//Prevent biome spawning
								biomeDef.wildAnimals?.ForEach(x => {if (x.animal?.race == thingDef) x.commonality = 0 ;} );
							}
						}
						break;
					}
				
					case nameof(TerrainDef):
					{
						var originalDesignationCategory = ((TerrainDef)def).designationCategory;
						((TerrainDef)def).designationCategory = null; //Hide from architect menus
						originalDesignationCategory?.ResolveReferences();
						break;
					}
				
					case nameof(RecipeDef):
					{
						RecipeDef recipeDef = def as RecipeDef;
					
						recipeDef.recipeUsers?.ForEach(x => {x.recipes?.Remove(recipeDef); x.allRecipesCached?.Remove(recipeDef); });
						DefDatabase<ThingDef>.AllDefsListForReading.ForEach(x => {x.allRecipesCached?.Remove(recipeDef); x.recipes?.Remove(recipeDef);} );
						recipeDef.requiredGiverWorkType = null;
						break;
					}

					case nameof(TraitDef):
					{
						hardRemovedDefs.Add(def);
						DefDatabase<TraitDef>.Remove(def as TraitDef);
						break;
					}
					
					case nameof(ResearchProjectDef):
					{
						hardRemovedDefs.Add(def);
						DefDatabase<ResearchProjectDef>.Remove(def as ResearchProjectDef);
						break;
					}
					
					case nameof(DesignationCategoryDef):
					{
						hardRemovedDefs.Add(def);
						DefDatabase<DesignationCategoryDef>.Remove(def as DesignationCategoryDef);
						DefDatabase<ThingDef>.AllDefsListForReading.ForEach(x => {if (x.designationCategory == def) x.designationCategory = null; } );

						if (Current.ProgramState == ProgramState.Playing) reloadRequired = true;
						break;
					}
					
					case nameof(ThingStyleDef):
					{
						ThingStyleDef thingStyleDef = def as ThingStyleDef;
						var styleCategoryDefs = DefDatabase<StyleCategoryDef>.AllDefsListForReading.Where(x => x.thingDefStyles.Any(y => y.styleDef == thingStyleDef));
						foreach (var styleCategoryDef in styleCategoryDefs)
						{
							//Find in list
							ThingDefStyle thingDefStyle = styleCategoryDef.thingDefStyles.FirstOrDefault(x => x.styleDef == thingStyleDef);
							if (thingDefStyle == null) continue;
							//Remove from cache
							styleCategoryDef.addDesignators?.Remove(thingDefStyle.thingDef as BuildableDef);
							styleCategoryDef.cachedAllDesignatorBuildables?.Remove(thingDefStyle.thingDef as BuildableDef);
							//Remove from list
							styleCategoryDef.thingDefStyles.Remove(thingDefStyle);
						}
						break;
					}
					
					case nameof(QuestScriptDef):
					{
						QuestScriptDef questScriptDef = def as QuestScriptDef;
						
						questScriptDef.rootSelectionWeight = 0; //Makes the IsRootRandomSelected getter return false, which excludes from ChooseNaturalRandomQuest
						questScriptDef.decreeSelectionWeight = 0; //Excludes decrees
						break;
					}
					
					case nameof(IncidentDef):
					{
						IncidentDef incidentDef = def as IncidentDef;
						
						incidentDef.baseChance = 0;
						incidentDef.baseChanceWithRoyalty = 0;
						incidentDef.earliestDay = int.MaxValue;
						incidentDef.minThreatPoints = float.MaxValue;
						incidentDef.minPopulation = int.MaxValue;
						break;
					}
					
					case nameof(ThoughtDef):
					{
						ThoughtDef thoughtDef = def as ThoughtDef;

						thoughtDef.durationDays = 0f;
						thoughtDef.isMemoryCached = BoolUnknown.Unknown;
						thoughtDef.minExpectation = new ExpectationDef() { order = int.MaxValue };
						break;
					}
					
					default: return false;
				}
			}
			//In the event there's a bug, this will at least allow the user to not be stuck in the options menu
			catch (System.NullReferenceException)
			{
				return false;
			}
			if (reloadRequired && Current.ProgramState == ProgramState.Playing)
			{
				//Find.WindowStack.Add(new Dialog_MessageBox("CherryPicker.ReloadRequired".Translate(), null, null, null, null, "CherryPicker.ReloadHeader".Translate(), true, null, null, WindowLayer.Dialog));
			}
			return true;
		}
	}
}
