using Verse;
using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.DefUtility;
 
namespace CherryPicker
{
	[StaticConstructorOnStartup]
    internal static class CherryPickerUtility
	{
		public static Def[] allDefs; //All defs this mod supports editting
		public static Dictionary<Def, string> searchStringCache; //Sync'd index with allDefs
		public static HashSet<string> actualRemovedDefs = new HashSet<string>(); //This is like removedDefs except it only contains defs for this active modlist
		public static List<string> report = new List<string>(); //Gives a console print of changes made
		public static HashSet<Def> processedDefs = new HashSet<Def>(); //Used to keep track of direct DB removals
		public static HashSet<Backstory> removedBackstories = new HashSet<Backstory>(); //Used to keep track of direct DB removals
		public static ThingCategoryDef[] categoryCache; //Cache to avoid needing to recompile the categories each loop
		public static bool filtered; //Tells the script the filter box is being used
		public static bool reprocess; //Flags if the list needs to be processed again, in the event a DefList was appended
		public static HashSet<string> reprocessDefs = new HashSet<string>(); //These are defs that need to be added to the working list next reprocess
		public static Dialog_MessageBox reloadGameMessage = new Dialog_MessageBox("CherryPicker.ReloadRequired".Translate(), null, null, null, null, "CherryPicker.ReloadHeader".Translate(), true, null, null, WindowLayer.Dialog);

		static CherryPickerUtility()
		{
			var timer = new System.Diagnostics.Stopwatch();
  			timer.Start();

			//Check for new-users
			if (allRemovedDefs == null) allRemovedDefs = new HashSet<string>();
			
			Setup();

			timer.Stop();
			TimeSpan timeTaken = timer.Elapsed;

			//Give report
			if (report.Count > 0)
			{
				Log.Message("[Cherry Picker] The database was processed in " + timeTaken.ToString(@"ss\.fffff") + " seconds and the following defs were removed" + 
				(report.Any(x => x.Contains("FAILED:")) ? " <color=red>with " + report.Count(x => x.Contains("FAILED:")).ToString() + " errors</color>" : "") + ": " + 
				string.Join(", ", report));
			}
		}
		public static void Setup(bool secondPass = false)
		{
			//Fetch all our def lists across multiple categories
			allDefs = new IEnumerable<Def>[]
			{
				processedDefs,
				DefDatabase<ThingDef>.AllDefsListForReading.Where
					(x => !x.IsBlueprint && !x.IsFrame && !x.IsCorpse && !x.isUnfinishedThing &&
					(x.category == ThingCategory.Item || x.category == ThingCategory.Building || x.category == ThingCategory.Plant || x.category == ThingCategory.Pawn)),
				DefDatabase<TerrainDef>.AllDefs,
				DefDatabase<RecipeDef>.AllDefs,
				DefDatabase<TraitDef>.AllDefs,
				DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(x => DefDatabase<ResearchProjectDef>.AllDefsListForReading.
								Any(y => (!y.prerequisites?.Contains(x) ?? true) && (!y.hiddenPrerequisites?.Contains(x) ?? true))),
				DefDatabase<DesignationCategoryDef>.AllDefs,
				DefDatabase<ThingStyleDef>.AllDefs,
				DefDatabase<QuestScriptDef>.AllDefsListForReading.Where(x => !DefDatabase<IncidentDef>.AllDefsListForReading.Any(y => y.questScriptDef == x)),
				DefDatabase<IncidentDef>.AllDefs,
				DefDatabase<HediffDef>.AllDefs,
				DefDatabase<ThoughtDef>.AllDefs,
				DefDatabase<TraderKindDef>.AllDefs,
				DefDatabase<GatheringDef>.AllDefs,
				DefDatabase<WorkTypeDef>.AllDefs,
				DefDatabase<MemeDef>.AllDefs,
				DefDatabase<PreceptDef>.AllDefs,
				DefDatabase<RitualPatternDef>.AllDefs,
				DefDatabase<HairDef>.AllDefs,
				DefDatabase<BeardDef>.AllDefs,
				DefDatabase<RaidStrategyDef>.AllDefs,
				DefDatabase<MainButtonDef>.AllDefs,
				DefDatabase<AbilityDef>.AllDefs,
				DefDatabase<BiomeDef>.AllDefs,
				DefDatabase<MentalBreakDef>.AllDefs,
				DefDatabase<SpecialThingFilterDef>.AllDefs,
				DefDatabase<DefList>.AllDefs
			}.SelectMany(x => x).Distinct().ToArray();

			//Avoid having to recompile this list for each thingdef
			categoryCache = DefDatabase<ThingCategoryDef>.AllDefsListForReading.SelectMany(x => x.ThisAndChildCategoryDefs ?? Enumerable.Empty<ThingCategoryDef>()).ToArray();

			//Process lists
			MakeLabelCache();
			MakeWorkingList();
			ProcessList();

			if (secondPass)
			{
				//Give report
				if (report.Count > 0)
				{
					Log.Message("[Cherry Picker] These dynamically generated defs were also processed" + 
					(report.Any(x => x.Contains("FAILED:")) ? " <color=red>with " + report.Count(x => x.Contains("FAILED:")).ToString() + " errors</color>" : "") + ": " + 
					string.Join(", ", report));
				}
			}
		}
		public static void MakeLabelCache()
		{
			searchStringCache = new Dictionary<Def, string>();
			foreach (var def in allDefs)
			{
				searchStringCache.Add(def, (def?.defName + def.label + def.modContentPack?.Name + def.GetType().Name).ToLower());
			}
		}
		public static void MakeWorkingList()
		{
			actualRemovedDefs.Clear();

			//Add any waiting defs first...
			allRemovedDefs.AddRange(reprocessDefs);
			reprocessDefs.Clear();

			foreach (string key in allRemovedDefs)
			{
				Type type = key.ToType();
				string defName = key.ToDefName();
				if (type == typeof(Backstory))
				{
					if (BackstoryDatabase.allBackstories.ContainsKey(defName) || removedBackstories.Any(x => x.identifier == defName)) actualRemovedDefs.Add("Backstory/" + defName);
					continue;
				} 

				Def def = GetDef(defName, type);
				if (allDefs.Contains(def)) actualRemovedDefs.Add(key);
			}
		}
		public static void ProcessList()
		{
			report.Clear();
			
			//Handle new removals
			foreach (string key in actualRemovedDefs)
			{
				allRemovedDefs.Add(key);
				Type type = key.ToType();
				string defName = key.ToDefName();

				//Special handling for backstories
				if (type == typeof(Backstory) && !removedBackstories.Any(x => x.identifier == defName))
				{
					if (BackstoryDatabase.allBackstories.TryGetValue(defName, out Backstory backstory)) removedBackstories.Add(backstory);
					report.Add(BackstoryDatabase.allBackstories.Remove(defName) ? "\n - " + key : ("\n - FAILED: " + key));
					continue;
				}

				Def def = GetDef(defName, type);
				//Because it's a hashlist it'll only return true if this def has not been processed already
				if (def != null && processedDefs.Add(def))
				{
					report.Add(RemoveDef(def) ? "\n - " + key : ("\n - FAILED: " + key));
				}
			}

			//Handle def restorations or prompt restart
			bool restart = false;
			foreach (string key in allRemovedDefs.ToList())
			{
				Type type = key.ToType();
				string defName = key.ToDefName();
				if (type == typeof(Backstory))
				{
					var backstory = removedBackstories.FirstOrDefault(x => x.identifier == defName);
					if (!actualRemovedDefs.Contains(key) && backstory != null)
					{
						BackstoryDatabase.allBackstories.Add(defName, backstory);
					 	removedBackstories.Remove(backstory);
						allRemovedDefs.Remove(key);
					}
					continue;
				}

				Def def = GetDef(defName, type);
				restart =
				(
					!actualRemovedDefs.Contains(key) && //Is this def in the working list?
					allDefs.Contains(def) && //Is this def part of the current modlist?
					allRemovedDefs.Remove(key) && //Could we remove it?
					processedDefs.Remove(def) && //Mark as no longer processed
					!TryRestoreDef(def) && //Was the def restorable or do we need to restart?
					!restart //Make bool a one-way flip
				);
			}
			if (restart) Find.WindowStack.Add(new Dialog_MessageBox("CherryPicker.RestartRequired".Translate(), null, null, null, null, "CherryPicker.RestartHeader".Translate(), true, null, null, WindowLayer.Dialog));

			//Reorder
			allDefs = allDefs.OrderBy(x => !actualRemovedDefs.Contains(x.ToKey())).ToArray();

			if (reprocess)
			{
				reprocess = false;
				MakeWorkingList();
				ProcessList();
			}
		}
		public static bool RemoveDef(Def def)
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
						foreach (ThingCategoryDef thingCategoryDef in categoryCache)
						{
							thingCategoryDef.allChildThingDefsCached?.Remove(thingDef);
							thingCategoryDef.sortedChildThingDefsCached?.Remove(thingDef);
							thingCategoryDef.childThingDefs?.Remove(thingDef);
						}

						//If mineable...
						thingDef.deepCommonality = thingDef.deepCountPerCell = thingDef.deepCountPerPortion = 0;
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
								List<ThingDefStyle> styleDefWorkingList = styleCategoryDef.ToList();
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
								thingDef.building.isNaturalRock = false;
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

							//Is this building used for a ideology precept?
							if (ModLister.IdeologyInstalled)
							{
								DefDatabase<PreceptDef>.AllDefsListForReading.ForEach
								(x =>
									{
										x.buildingDefChances?.RemoveAll(x => x.def == thingDef);
									}
								);
							}
						}
						//Items
						else if (thingDef.category == ThingCategory.Item)
						{
							//Scenario starting items
							foreach (ScenarioDef scenarioDef in DefDatabase<ScenarioDef>.AllDefsListForReading)
							{
								scenarioDef.scenario.parts?.RemoveAll(y => y.GetType() == typeof(ScenPart_StartingThing_Defined) && 
									y.ChangeType<ScenPart_StartingThing_Defined>().thingDef == thingDef);
							}

							//Remove from recipe ingredients
							thingDef.recipeMaker?.recipeUsers.Clear();
							int length = DefDatabase<RecipeDef>.DefCount;
							for (int i = 0; i < length; ++i)
							{
								RecipeDef recipeDef = DefDatabase<RecipeDef>.defsList[i];
								foreach (IngredientCount ingredientCount in recipeDef.ingredients ?? Enumerable.Empty<IngredientCount>())
								{
									ingredientCount.filter?.thingDefs?.Remove(thingDef);
									ingredientCount.filter?.allowedDefs?.Remove(thingDef);
								}

								recipeDef.fixedIngredientFilter?.thingDefs?.Remove(thingDef);
								recipeDef.fixedIngredientFilter?.allowedDefs?.Remove(thingDef);
								recipeDef.defaultIngredientFilter?.thingDefs?.Remove(thingDef);
								recipeDef.defaultIngredientFilter?.allowedDefs?.Remove(thingDef);
							}

							//Butchery and Costlists
							length = DefDatabase<ThingDef>.DefCount;
							for (int i = 0; i < length; ++i)
							{
								ThingDef x = DefDatabase<ThingDef>.defsList[i];
								x.costList?.RemoveAll(y => y.thingDef == thingDef);
								x.costListForDifficulty?.costList?.RemoveAll(y => y.thingDef == thingDef);
								x.butcherProducts?.RemoveAll(z => z.thingDef == thingDef);
							}

							//If medicine...
							thingDef.statBases?.RemoveAll(x => x.stat == StatDefOf.MedicalPotency);

							//Makes this stuff material not show up in generated items
							if (thingDef.stuffProps != null) thingDef.stuffProps.commonality = 0;

							//Process pawnkinds that reference this item
							foreach (PawnKindDef pawnKindDef in DefDatabase<PawnKindDef>.AllDefsListForReading)
							{
								pawnKindDef.apparelRequired?.Remove(thingDef);
								pawnKindDef.techHediffsRequired?.Remove(thingDef);
							}
							
							//If apparel
							if (thingDef.thingClass == typeof(Apparel) && thingDef.apparel != null)
							{
								reloadRequired = true;

								thingDef.apparel.tags?.Clear();
								thingDef.apparel.defaultOutfitTags?.Clear();
								thingDef.apparel.canBeDesiredForIdeo = false;
								thingDef.apparel.ideoDesireAllowedFactionCategoryTags?.Clear();
								thingDef.apparel.ideoDesireDisallowedFactionCategoryTags?.Clear();

								//Some apparrel have special comps which filters target to locate. Remove except for some whitelisted comps which break things if removed
								thingDef.comps.RemoveAll(x => x.GetType() != typeof(CompProperties_Drug));
							}

							//If weapon
							else if (thingDef.equipmentType == EquipmentType.Primary)
							{
								thingDef.weaponTags?.Clear();
								thingDef.weaponClasses?.Clear();
							}

							//If implant
							thingDef.techHediffsTags?.Clear();
						}
						//Plants
						else if (thingDef.category == ThingCategory.Plant)
						{
							if (thingDef.plant != null)
							{
								thingDef.plant.sowTags.Clear(); //Farming UI
								thingDef.plant.cavePlant = false; //Mushroom filters
								thingDef.plant.wildBiomes?.Clear();
							}
							foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefsListForReading)
							{
								biomeDef.wildPlants?.RemoveAll(y => y.plant == thingDef);
								biomeDef.cachedWildPlants?.Remove(thingDef);
								biomeDef.cachedPlantCommonalities?.Remove(thingDef);
							}
						}
						//Pawns and animals
						else if (thingDef.category == ThingCategory.Pawn)
						{
							//Omits from farm animal related events
							thingDef.tradeTags?.Clear();
							//Omits from migration event
							thingDef.race.herdMigrationAllowed = false;
							//For farm animal joins event
							thingDef.race.wildness = 1f;

							//Omits from manhunter event
							foreach (var pawnKindDef in DefDatabase<PawnKindDef>.AllDefsListForReading)
							{
								if (pawnKindDef.race.defName == def.defName)
								{
									pawnKindDef.canArriveManhunter = false;
									pawnKindDef.combatPower = float.MaxValue; //Makes too expensive to ever buy with points
									pawnKindDef.isGoodBreacher = false; //Special checks
								}
							}
							

							/*
							if (thingDef.race.animalType == AnimalType.Dryad)
							{
								
							}
							*/
							
							//Prevent biome spawning
							DefDatabase<BiomeDef>.AllDefsListForReading.ForEach
							(x =>
								{
									x.wildAnimals?.ForEach(x => {if (x.animal?.race == thingDef) x.commonality = 0 ;} );
								}
							);
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
					
						recipeDef.recipeUsers?.ForEach
						(x => 
							{
								x.recipes?.Remove(recipeDef);
								x.allRecipesCached?.Remove(recipeDef);
							}
						);
						int length = DefDatabase<ThingDef>.DefCount;
						for (int i = 0; i < length; ++i)
						{
							ThingDef x = DefDatabase<ThingDef>.defsList[i];
							x.allRecipesCached?.Remove(recipeDef);
							x.recipes?.Remove(recipeDef);
						}
						recipeDef.requiredGiverWorkType = null;
						recipeDef.researchPrerequisite = null;
						recipeDef.researchPrerequisites?.Clear();
						DefDatabase<RecipeDef>.Remove(recipeDef);
						break;
					}

					case nameof(TraitDef):
					{
						DefDatabase<TraitDef>.Remove(def as TraitDef);
						break;
					}
					
					case nameof(ResearchProjectDef):
					{
						ResearchProjectDef researchProjectDef = def as ResearchProjectDef;

						//Do any other research project use as a prereq?
						foreach (ResearchProjectDef entry in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
						{
							if (entry.prerequisites?.Contains(researchProjectDef) ?? false)
							{
								entry.prerequisites.AddRange(researchProjectDef.prerequisites?.Where(x => !entry.prerequisites.Contains(x)) ?? Enumerable.Empty<ResearchProjectDef>());
							}
							if (entry.hiddenPrerequisites?.Contains(researchProjectDef) ?? false)
							{
								entry.hiddenPrerequisites.AddRange(researchProjectDef.hiddenPrerequisites?.Where(x => !entry.prerequisites.Contains(x)) ?? Enumerable.Empty<ResearchProjectDef>());
							}
							entry.prerequisites?.RemoveAll(x => x == researchProjectDef);
							entry.hiddenPrerequisites?.RemoveAll(x => x == researchProjectDef);
						}
						DefDatabase<ResearchProjectDef>.Remove(researchProjectDef);
						break;
					}
					
					case nameof(DesignationCategoryDef):
					{
						DefDatabase<DesignationCategoryDef>.Remove(def as DesignationCategoryDef);
						
						int length = DefDatabase<ThingDef>.DefCount;
						for (int i = 0; i < length; ++i)
						{
							ThingDef x = DefDatabase<ThingDef>.defsList[i];
							if (x.designationCategory == def as DesignationCategoryDef) x.designationCategory = null;
						}

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

					case nameof(TraderKindDef):
					{
						((TraderKindDef)def).commonality = 0f;
						break;
					}

					case nameof(GatheringDef):
					{
						((GatheringDef)def).randomSelectionWeight = 0f;
						break;
					}

					case nameof(WorkTypeDef):
					{
						WorkTypeDef workTypeDef = def as WorkTypeDef;
						workTypeDef.visible = false;
						DefDatabase<PawnColumnDef>.AllDefsListForReading.RemoveAll(x => x.workType == workTypeDef);
						DefDatabase<PawnTableDef>.AllDefsListForReading.ForEach(x => x.columns.RemoveAll(y => y.workType == workTypeDef));
						break;
					}

					case nameof(MemeDef):
					{
						DefDatabase<MemeDef>.Remove(def as MemeDef);
						break;
					}

					case nameof(PreceptDef):
					{
						PreceptDef preceptDef = def as PreceptDef;
						preceptDef.allowedForNPCFactions = false;
						preceptDef.visible = false;
						preceptDef.selectionWeight = 0;
						DefDatabase<PreceptDef>.Remove(preceptDef);
						break;
					}

					case nameof(RitualPatternDef):
					{
						DefDatabase<RitualPatternDef>.Remove(def as RitualPatternDef);
						break;
					}

					case nameof(HairDef):
					{
						DefDatabase<HairDef>.Remove(def as HairDef);
						break;
					}

					case nameof(BeardDef):
					{
						DefDatabase<BeardDef>.Remove(def as BeardDef);
						break;
					}

					case nameof(RaidStrategyDef):
					{
						RaidStrategyDef raidStrategyDef = def as RaidStrategyDef;
						raidStrategyDef.pointsFactorCurve = new SimpleCurve() { { new CurvePoint(0, 0), true } };
						raidStrategyDef.selectionWeightPerPointsCurve = new SimpleCurve() { { new CurvePoint(0, 0), true } };
						break;
					}

					case nameof(MainButtonDef):
					{
						((MainButtonDef)def).buttonVisible = false;
						break;
					}

					case nameof(AbilityDef):
					{
						AbilityDef abilityDef = def as AbilityDef;
						abilityDef.level = int.MaxValue; //Won't make it past the random select filters
						DefDatabase<PreceptDef>.AllDefsListForReading.ForEach(x => x.grantedAbilities?.Remove(abilityDef));
						DefDatabase<RoyalTitleDef>.AllDefsListForReading.ForEach(x => x.grantedAbilities?.Remove(abilityDef));
						break;
					}

					case nameof(BiomeDef):
					{
						((BiomeDef)def).implemented = false;
						break;
					}

					case nameof(MentalBreakDef):
					{
						((MentalBreakDef)def).baseCommonality = 0;
						break;
					}

					case nameof(SpecialThingFilterDef):
					{
						SpecialThingFilterDef specialThingFilterDef = def as SpecialThingFilterDef;
						int length = DefDatabase<RecipeDef>.DefCount;
						for (int i = 0; i < length; ++i)
						{
							RecipeDef recipeDef = DefDatabase<RecipeDef>.defsList[i];
							recipeDef.fixedIngredientFilter?.specialFiltersToAllow?.Remove(specialThingFilterDef.defName);
							recipeDef.fixedIngredientFilter?.specialFiltersToDisallow?.Remove(specialThingFilterDef.defName);
							recipeDef.defaultIngredientFilter?.specialFiltersToAllow?.Remove(specialThingFilterDef.defName);
							recipeDef.defaultIngredientFilter?.specialFiltersToDisallow?.Remove(specialThingFilterDef.defName);
							recipeDef.forceHiddenSpecialFilters?.Remove(specialThingFilterDef);
						}
						//Update categories
						foreach (ThingCategoryDef thingCategoryDef in categoryCache)
						{
							thingCategoryDef.childSpecialFilters.Remove(specialThingFilterDef);
						}

						break;
					}

					case nameof(DefList):
					{
						reprocess = true;
						reprocessDefs.AddRange(((DefList)def).defs);
						break;
					}

					default: return false;
				}
			}
			//In the event there's a bug, this will prevent the exposure from hanging and causing data loss
			catch (Exception)            
			{                
				return false;
			}
			if (reloadRequired && Current.ProgramState == ProgramState.Playing)
			{
				if (!Find.WindowStack.IsOpen(reloadGameMessage)) Find.WindowStack.Add(reloadGameMessage);
			}
			return true;
		}
		public static bool TryRestoreDef(Def def)
		{
			try
			{
				switch(def.GetType().Name)
				{
					case nameof(TraitDef):
					{
						DefDatabase<TraitDef>.Add(def as TraitDef);
						break;
					}

					case nameof(MemeDef):
					{
						DefDatabase<MemeDef>.Add(def as MemeDef);
						break;
					}

					case nameof(RitualPatternDef):
					{
						DefDatabase<RitualPatternDef>.Add(def as RitualPatternDef);
						break;
					}

					case nameof(HairDef):
					{
						DefDatabase<HairDef>.Add(def as HairDef);
						break;
					}

					case nameof(BeardDef):
					{
						DefDatabase<BeardDef>.Add(def as BeardDef);
						break;
					}

					case nameof(MainButtonDef):
					{
						((MainButtonDef)def).buttonVisible = true;
						break;
					}

					case nameof(BiomeDef):
					{
						((BiomeDef)def).implemented = true;
						break;
					}

					case nameof(DefList):
					{
						reprocess = true;
						allRemovedDefs.RemoveWhere(x => (((DefList)def).defs.Contains(x)));
						return false;
					}

					default: return false;
				}
			}
			catch (Exception)            
			{                
				return false;
			}
			return true;
		}
	}
}
