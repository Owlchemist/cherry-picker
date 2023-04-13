using Verse;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using RimWorld;
using HarmonyLib;
using MonoMod.Utils;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.DefUtility;
namespace CherryPicker
{
	[StaticConstructorOnStartup]
    internal static class CherryPickerUtility
	{
		public static Def[] allDefs; //All defs this mod supports editting
		public static Dictionary<Def, string> searchStringCache; //Sync'd index with allDefs
		static List<string> report = new List<string>(); //Gives a console print of changes made
		public static bool
			filtered, //Tells the script the filter box is being used
			reprocess, //Flags if the list needs to be processed again, in the event a DefList was appended
			filteredStuff; //The IsStuff harmony patch will only be active if true
		static bool processDesignators, 
			processBodyTypes,
			processXenotypes,
			processRulePackDef,
			processTerrain,
			processPsycastPaths,
			processPsycasts;
		public static Dialog_MessageBox reloadGameMessage = new Dialog_MessageBox("CherryPicker.ReloadRequired".Translate(), null, null, null, null, "CherryPicker.ReloadHeader".Translate(), true, null, null, WindowLayer.Dialog);
		static SimpleCurve zeroCurve = new SimpleCurve() { { new CurvePoint(0, 0), true } };
		public static HashSet<Def> processedDefs = new HashSet<Def>(); //Used to keep track of direct DB removals
		public static HashSet<string>
			actualRemovedDefs = new HashSet<string>(), //This is like removedDefs except it only contains defs for this active modlist
			reprocessDefs = new HashSet<string>(); //These are defs that need to be added to the working list next reprocess
		static HashSet<Type> compWhitelist = new HashSet<Type>() //When comps are removed from items, exempt these
		{
			typeof(CompProperties_Drug), typeof(CompProperties_Styleable)
		};

		static CherryPickerUtility()
		{
			var timer = new System.Diagnostics.Stopwatch();
  			timer.Start();
			
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
			try
			{
				//Fetch all our def lists across multiple categories
				allDefs = new IEnumerable<Def>[]
				{
					processedDefs,
					DefDatabase<ThingDef>.AllDefs.Where(x => !x.IsBlueprint && !x.IsFrame && !x.isUnfinishedThing && 
						(x.category == ThingCategory.Item || x.category == ThingCategory.Building || x.category == ThingCategory.Plant || x.category == ThingCategory.Pawn)),
					DefDatabase<TerrainDef>.AllDefs,
					DefDatabase<RecipeDef>.AllDefs,
					DefDatabase<TraitDef>.AllDefs,
					DefDatabase<ResearchProjectDef>.AllDefs.Where(x => DefDatabase<ResearchProjectDef>.AllDefs.Any(y => (!y.prerequisites?.Contains(x) ?? true) && 
						(!y.hiddenPrerequisites?.Contains(x) ?? true))),
					DefDatabase<DesignationCategoryDef>.AllDefs,
					DefDatabase<ThingStyleDef>.AllDefs,
					DefDatabase<QuestScriptDef>.AllDefs.Where(x => !DefDatabase<IncidentDef>.AllDefs.Any(y => y.questScriptDef == x)),
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
					DefDatabase<PawnKindDef>.AllDefs.Where(x => x != PawnKindDefOf.Colonist),
					DefDatabase<GenStepDef>.AllDefs,
					DefDatabase<InspirationDef>.AllDefs,
					DefDatabase<StorytellerDef>.AllDefs,
					DefDatabase<ScenarioDef>.AllDefs,
					DefDatabase<DesignationDef>.AllDefs,
					DefDatabase<PawnsArrivalModeDef>.AllDefs,
					DefDatabase<GeneDef>.AllDefs,
					DefDatabase<XenotypeDef>.AllDefs,
					DefDatabase<BodyTypeDef>.AllDefs.Where(x => x != BodyTypeDefOf.Male && x != BodyTypeDefOf.Female),
					DefDatabase<FactionDef>.AllDefs.Where(x => x.maxConfigurableAtWorldCreation > 0),
					DefDatabase<BackstoryDef>.AllDefs,
					DefDatabase<WeatherDef>.AllDefs,
					DefDatabase<ScatterableDef>.AllDefs,
					DefDatabase<RaidAgeRestrictionDef>.AllDefs,
					DefDatabase<WeaponTraitDef>.AllDefs,
					DefDatabase<RulePackDef>.AllDefs,
					DefDatabase<InteractionDef>.AllDefs,
					DefDatabase<DefList>.AllDefs,
					GetDefFromMod(packageID: "vanillaexpanded.vfea", assemblyName: "VFEAncients", nameSpace: "VFEAncients", typeName: "PowerDef"),
					GetDefFromMod(packageID: "oskarpotocki.vanillafactionsexpanded.core", assemblyName:"VFECore", nameSpace:"VFECore.Abilities", typeName:"AbilityDef")
                       .Where(x => x.modExtensions.Any(e => e.GetType().Namespace == "VanillaPsycastsExpanded")),
                    GetDefFromMod(packageID: "vanillaexpanded.vpsycastse", assemblyName: "VanillaPsycastsExpanded", nameSpace: "VanillaPsycastsExpanded", typeName: "PsycasterPathDef")
				}.SelectMany(x => x).Distinct().ToArray();

				//Process lists
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
			catch (Exception ex)
			{                
				Log.Error("[Cherry Picker] Error constructing master def list...\n" + ex);
				return;
			}
		}
		static IEnumerable<Def> GetDefFromMod(string packageID, string assemblyName, string nameSpace, string typeName)
		{
			ModContentPack mod = LoadedModManager.RunningMods.FirstOrDefault(x => x.PackageId == packageID);
			if (mod != null) 
			{
				Type type = mod.assemblies.loadedAssemblies
                        .FirstOrDefault(a => a.GetName().Name == assemblyName)?.GetType(nameSpace + "." + typeName);

				if (type != null)
				{
					if (!typeCache.ContainsKey(typeName)) typeCache.Add(typeName, type);
					return (IEnumerable<Def>)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, "get_AllDefs");
				}
				else Log.Warning("[Cherry Picker] Could not find " + nameSpace+typeName);
			}
			return Enumerable.Empty<Def>();
		}

		static void MakeWorkingList()
		{
			actualRemovedDefs.Clear();

			//Add any waiting defs first...
			allRemovedDefs.AddRange(reprocessDefs);
			reprocessDefs.Clear();

			foreach (string key in allRemovedDefs)
			{
				Type type = key.ToType();
				string defName = key.ToDefName();

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

				Def def = GetDef(defName, type);
				//Because it's a hashlist it'll only return true if this def has not been processed already
				if (def != null && processedDefs.Add(def))
				{
					report.Add(RemoveDef(def) ? "\n - " + key : ("\n - FAILED: " + key));
				}
			}

			//Consolidated associated loops
			PostProcess();

			//Handle def restorations or prompt restart
			bool restart = false;
			foreach (string key in allRemovedDefs.ToList())
			{
				Type type = key.ToType();
				string defName = key.ToDefName();

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

			//Active harmony patches where needed
			if (filteredStuff)
			{
				var method = AccessTools.Property(typeof(ThingDef), nameof(ThingDef.IsStuff)).GetGetMethod();
				if (Mod_CherryPicker.patchLedger.Add(nameof(method)))
				{
					Mod_CherryPicker._harmony.Patch(method, transpiler: new HarmonyMethod(typeof(DynamicPatches), nameof(DynamicPatches.Transpiler_IsStuff)));
				}
			}
		}
		static bool RemoveDef(Def def)
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

						//If mineable...
						thingDef.deepCommonality = thingDef.deepCountPerCell = thingDef.deepCountPerPortion = 0;
						thingDef.deepLumpSizeRange = IntRange.zero;

						//0'ing out the nutrition removes food from filters
						if (thingDef.ingestible != null) 
						{
							thingDef.SetStatBaseValue(StatDefOf.Nutrition, 0);
							thingDef.ingestible.preferability = FoodPreferability.NeverForNutrition;
						}

						if (thingDef.IsStuff) filteredStuff = true;
						
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

							if (thingDef.building != null)
							{
								thingDef.building.claimable = false; //Possibly only needed to work better with Minify Everything
								thingDef.building.buildingTags?.Clear();

								//If mineable
								thingDef.building.mineableScatterCommonality = 0;
								thingDef.building.mineableScatterLumpSizeRange = IntRange.zero;
								thingDef.building.isNaturalRock = false;
							}
						}
						
						//Items
						else if (thingDef.category == ThingCategory.Item)
						{
							//Remove from recipe ingredients
							thingDef.recipeMaker?.recipeUsers?.Clear();

							//If medicine...
							thingDef.statBases?.RemoveAll(x => x.stat == StatDefOf.MedicalPotency);

							//Makes this stuff material not show up in generated items
							if (thingDef.stuffProps != null) thingDef.stuffProps.commonality = 0;
							
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
								thingDef.comps.RemoveAll(x => !compWhitelist.Contains(x.GetType()));
							}

							//If weapon
							else if (thingDef.equipmentType == EquipmentType.Primary)
							{
								thingDef.weaponTags?.Clear();
								thingDef.weaponClasses?.Clear();
							}

							//If implant
							thingDef.techHediffsTags?.Clear();

							//TODO: Some mods filter onto their defs using an extension. For now, their removal will operate through a whitelist until a better solution is written
							//Seed = SeedsPlease: Lite
							thingDef.modExtensions?.RemoveAll(x => x.GetType().Name == "Seed");

							//If drug
							if (thingDef.IsDrug)
							{
								thingDef.ingestible.drugCategory = DrugCategory.None;
								thingDef.techLevel = TechLevel.Archotech; //Filtered this way in RandomDrugs()
								var list = DefDatabase<DrugPolicyDef>.AllDefsListForReading;
								for (int i = list.Count; i-- > 0;)
								{
									list[i].entries?.RemoveAll(x => x.drug == thingDef);
								}
							}
						}
						
						//Plants
						else if (thingDef.category == ThingCategory.Plant && thingDef.plant != null)
						{
							thingDef.plant.sowTags?.Clear(); //Farming UI
							thingDef.plant.cavePlant = false; //Mushroom filters
							thingDef.plant.wildBiomes?.Clear();
							//thingDef.plant.purpose = PlantPurpose.Misc; //Remove from some filters
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
							//Spawn chance
							foreach (var animalBiomeRecord in thingDef.race.wildBiomes ?? Enumerable.Empty<AnimalBiomeRecord>())
							{
								animalBiomeRecord.commonality = 0f;
							}
						}
						break;
					}
				
					case nameof(TerrainDef):
					{
						TerrainDef terrainDef = def as TerrainDef;

						//Hide from architect menus
						DesignationCategoryDef originalDesignationCategory = terrainDef.designationCategory;
						terrainDef.designationCategory = null;
						originalDesignationCategory?.ResolveReferences();

						terrainDef.costList?.Clear(); //Prevent from map spawning
						break;
					}
				
					case nameof(RecipeDef):
					{
						RecipeDef recipeDef = def as RecipeDef;
					
						foreach (ThingDef x in recipeDef.recipeUsers ?? Enumerable.Empty<ThingDef>())
						{
							x.recipes?.Remove(recipeDef);
							x.allRecipesCached?.Remove(recipeDef);
						}
						
						recipeDef.requiredGiverWorkType = null;
						recipeDef.researchPrerequisite = null;
						recipeDef.researchPrerequisites?.Clear();
						//Remove from database too. Even though it's removed, its reference may still persist in other cached collections
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
							//Subsitute prereqs. Take the removed def's prereqs and apply it to any descendant defs
							if (entry.prerequisites?.Contains(researchProjectDef) ?? false)
							{
								entry.prerequisites.AddRange(researchProjectDef.prerequisites?.Where(x => !entry.prerequisites.Contains(x)) ?? Enumerable.Empty<ResearchProjectDef>());
							}
							//Do the same for hidden prereqs
							if (entry.hiddenPrerequisites?.Contains(researchProjectDef) ?? false)
							{
								entry.hiddenPrerequisites.AddRange(researchProjectDef.hiddenPrerequisites?.Where(x => !entry.prerequisites.Contains(x)) ?? Enumerable.Empty<ResearchProjectDef>());
							}
							entry.prerequisites?.RemoveAll(x => x == researchProjectDef);
							entry.hiddenPrerequisites?.RemoveAll(x => x == researchProjectDef);
						}
						DefDatabase<ResearchProjectDef>.Remove(researchProjectDef);
						processTerrain = true; //Need to remove research requirements from terrain
						break;
					}
					
					case nameof(DesignationCategoryDef):
					{
						DefDatabase<DesignationCategoryDef>.Remove(def as DesignationCategoryDef);
						if (Current.ProgramState == ProgramState.Playing) reloadRequired = true;
						break;
					}
					
					case nameof(ThingStyleDef):
					{
						ThingStyleDef thingStyleDef = def as ThingStyleDef;
						foreach (var styleCategoryDef in DefDatabase<StyleCategoryDef>.AllDefsListForReading)
						{
							if (!styleCategoryDef.thingDefStyles.Any(y => y.styleDef == thingStyleDef)) continue;

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

					case nameof(InspirationDef):
					{
						((InspirationDef)def).baseCommonality = 0f;
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
						preceptDef.enabledForNPCFactions = false;
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
						raidStrategyDef.pointsFactorCurve = zeroCurve;
						raidStrategyDef.selectionWeightPerPointsCurve = zeroCurve;
						break;
					}

					case nameof(MainButtonDef):
					{
						((MainButtonDef)def).buttonVisible = false;
						break;
					}

					case nameof(AbilityDef) when def is AbilityDef abilityDef:
					{
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

						break;
					}

					case nameof(DefList):
					{
						reprocess = true;
						reprocessDefs.AddRange(((DefList)def).defs);
						break;
					}

					case nameof(PawnKindDef):
					{
						PawnKindDef pawnKindDef = def as PawnKindDef;
						pawnKindDef.combatPower = float.MaxValue; //Should make it fail selective filters
						pawnKindDef.canArriveManhunter = false;
						pawnKindDef.canBeSapper = false;
						pawnKindDef.allowInMechClusters = false;
						pawnKindDef.minGenerationAge = 0;
						break;
					}

					case nameof(GenStepDef):
					{
						foreach (MapGeneratorDef mapGeneratorDef in DefDatabase<MapGeneratorDef>.AllDefsListForReading)
						{
							mapGeneratorDef.genSteps?.Remove(def as GenStepDef);
						}
						break;
					}
					
					case nameof(HediffDef):
					{
						break;
					}

					case nameof(StorytellerDef):
					{
						((StorytellerDef)def).listVisible = false;
						break;
					}

					case nameof(ScenarioDef):
					{
						((ScenarioDef)def).scenario.showInUI = false;
						break;
					}

					case nameof(DesignationDef):
					{
						processDesignators = true;
						break;
					}

					case nameof(FactionDef):
					{
						FactionDef factiondef = def as FactionDef;
						factiondef.maxConfigurableAtWorldCreation = -1; //Makes the UI skip it
						factiondef.startingCountAtWorldCreation = 0;
						factiondef.requiredCountAtGameStart = 0;
						break;
					}

					case nameof(BodyTypeDef):
					{
						processBodyTypes = true;
						break;
					}

					case nameof(BackstoryDef):
					{
						DefDatabase<BackstoryDef>.Remove(def as BackstoryDef);
						break;
					}

					case nameof(WeatherDef):
					{
						WeatherDef weatherDef = def as WeatherDef;
						weatherDef.temperatureRange = new FloatRange(-999f, -998f); //Hack to invalidate for all biomes
						weatherDef.isBad = false; //Excludes from forced weather
						break;
					}
					
					case nameof(PawnsArrivalModeDef):
					{
						PawnsArrivalModeDef pawnsArrivalModeDef = def as PawnsArrivalModeDef;
						pawnsArrivalModeDef.minTechLevel = TechLevel.Archotech;
						pawnsArrivalModeDef.pointsFactorCurve = zeroCurve;
						pawnsArrivalModeDef.selectionWeightCurve = zeroCurve;
						break;
					}
					
					case nameof(GeneDef):
					{
						if (!ModLister.BiotechInstalled) break;
						DefDatabase<GeneDef>.Remove(def as GeneDef);
						break;
					}

					case nameof(XenotypeDef):
					{
						if (!ModLister.BiotechInstalled) break;
						DefDatabase<XenotypeDef>.Remove(def as XenotypeDef);
						processXenotypes = true;
						break;
					}

					case nameof(ScatterableDef):
					{
						ScatterableDef scatterableDef = def as ScatterableDef;
						scatterableDef.scatterType = "null";
						break;
					}

					case nameof(RaidAgeRestrictionDef):
					{
						((RaidAgeRestrictionDef)def).chance = 0f;
						break;
					}

					case nameof(WeaponTraitDef):
					{
						((WeaponTraitDef)def).commonality = 0f;
						break;
					}

					case nameof(RulePackDef):
					{
						processRulePackDef = true;
						break;
					}

					case nameof(InteractionDef):
					{
						InteractionDef interactionDef = def as InteractionDef;
						interactionDef.workerInt = (InteractionWorker)Activator.CreateInstance(typeof(InteractionWorker_Dummy));
						interactionDef.workerInt.interaction = interactionDef;
						break;
					}
					
					//Mod: Vanilla Factions Expanded: Ancients
					case "PowerDef":
					{
						if (typeCache.TryGetValue("PowerDef", out Type type))
						{
							GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, "Remove", def);
						}
						break;
					}

					//Mod: Vanilla Psycasts Expanded
                    case "PsycasterPathDef":
                    {
                        if (typeCache.TryGetValue("PsycasterPathDef", out Type type))
                        {
                            GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, "Remove", def);
                        }

                        processPsycastPaths = true; //Need to remove paths from the cached list

                        break;
                    }
					
					//Mod: Vanilla Psycasts Expanded
					//Need to check to see if its a psycast, or a different sort of ability
                    case "AbilityDef" when def.modExtensions.Any(e => e.GetType().Namespace == "VanillaPsycastsExpanded"):
                    {
                        processPsycasts = true; //Need to remove psycasts from paths and handle prereqs
                        break;
                    }

					default:
					{
						Log.Error("[Cherry Picker] " + (def?.defName ?? "<unknown>") + " is an unknown type.");
						return false;
					}
				}
			}
			//In the event there's a bug, this will prevent the exposure from hanging and causing data loss
			catch (Exception ex)
			{                
				Log.Error("[Cherry Picker] Error processing " + (def?.defName ?? "<unknown>") + "...\n" + ex);
				return false;
			}
			if (reloadRequired && Current.ProgramState == ProgramState.Playing)
			{
				if (!Find.WindowStack.IsOpen(reloadGameMessage)) Find.WindowStack.Add(reloadGameMessage);
			}
			return true;
		}
		static bool TryRestoreDef(Def def)
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

					case nameof(BackstoryDef):
					{
						DefDatabase<BackstoryDef>.Add(def as BackstoryDef);
						break;
					}

					case nameof(HediffDef):
					{
						break;
					}
					
					case nameof(StorytellerDef):
					{
						((StorytellerDef)def).listVisible = true;
						break;
					}

					case nameof(ScenarioDef):
					{
						((ScenarioDef)def).scenario.showInUI = true;
						break;
					}

					default:
					{
						return false;
					}
				}
			}
			catch (Exception ex)
			{                
				Log.Error("[Cherry Picker] Error restoring " + (def?.defName ?? "<unknown>") + "...\n" + ex);
				return false;
			}
			return true;
		}
		static void PostProcess()
		{
			//Update categories
			var compiledCategories = DefDatabase<ThingCategoryDef>.AllDefs.SelectMany(x => x.ThisAndChildCategoryDefs ?? Enumerable.Empty<ThingCategoryDef>()).ToArray();
			for (int i = 0; i < compiledCategories.Length; ++i)
			{
				ThingCategoryDef thingCategoryDef = compiledCategories[i];
				thingCategoryDef.allChildThingDefsCached?.RemoveWhere(x => processedDefs.Contains(x));
				thingCategoryDef.sortedChildThingDefsCached?.RemoveAll(x => processedDefs.Contains(x));
				thingCategoryDef.childThingDefs?.RemoveAll(x => processedDefs.Contains(x));
				thingCategoryDef.childSpecialFilters.RemoveAll(x => processedDefs.Contains(x));
			}

			//Processes scenario starting items
			int length = DefDatabase<ScenarioDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				ScenarioDef scenarioDef = DefDatabase<ScenarioDef>.defsList[i];
				scenarioDef.scenario.parts?.RemoveAll(y => y.GetType() == typeof(ScenPart_StartingThing_Defined) && 
					processedDefs.Contains(((ScenPart_StartingThing_Defined)y).thingDef));

				if(processXenotypes)
				{
					foreach (var scenPart in scenarioDef.scenario?.parts ?? Enumerable.Empty<ScenPart>())
					{
						ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes xenoScenPart = scenPart as ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes;
						if (xenoScenPart == null) continue;
						xenoScenPart.overrideKinds?.RemoveAll(x => processedDefs.Contains(x.xenotype));
						xenoScenPart.xenotypeCounts?.RemoveAll(x => processedDefs.Contains(x.xenotype));
					}
				}
			}

			//Processes recipes using removed items
			length = DefDatabase<RecipeDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				RecipeDef recipeDef = DefDatabase<RecipeDef>.defsList[i];
				foreach (IngredientCount ingredientCount in recipeDef.ingredients ?? Enumerable.Empty<IngredientCount>())
				{
					ingredientCount.filter?.thingDefs?.RemoveAll(x => processedDefs.Contains(x));
					ingredientCount.filter?.allowedDefs?.RemoveWhere(x => processedDefs.Contains(x));
				}

				recipeDef.fixedIngredientFilter?.thingDefs?.RemoveAll(x => processedDefs.Contains(x));
				recipeDef.fixedIngredientFilter?.allowedDefs?.RemoveWhere(x => processedDefs.Contains(x));
				recipeDef.defaultIngredientFilter?.thingDefs?.RemoveAll(x => processedDefs.Contains(x));
				recipeDef.defaultIngredientFilter?.allowedDefs?.RemoveWhere(x => processedDefs.Contains(x));
			}

			//Process vaious references within thingDefs
			length = DefDatabase<ThingDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				ThingDef thingDef = DefDatabase<ThingDef>.defsList[i];
				//Costlists
				thingDef.costList?.RemoveAll(x => processedDefs.Contains(x?.thingDef));
				thingDef.costListForDifficulty?.costList?.RemoveAll(x => processedDefs.Contains(x?.thingDef));
				//Butchery
				thingDef.butcherProducts?.RemoveAll(x => processedDefs.Contains(x?.thingDef));
				//Designations
				if (processedDefs.Contains(thingDef.designationCategory)) thingDef.designationCategory = null;
				//Recipes
				thingDef.allRecipesCached?.RemoveAll(x => processedDefs.Contains(x));
				thingDef.recipes?.RemoveAll(x => processedDefs.Contains(x));
				//Research
				if (processedDefs.Contains(thingDef.recipeMaker?.researchPrerequisite)) thingDef.recipeMaker.researchPrerequisite = null;
				else thingDef.recipeMaker?.researchPrerequisites?.RemoveAll(x => processedDefs.Contains(x));
				thingDef.researchPrerequisites?.RemoveAll(x => processedDefs.Contains(x));
				//Kill leavings
				thingDef.killedLeavings?.RemoveAll(x => processedDefs.Contains(x?.thingDef));
				//Process out spawner comps
				foreach (CompProperties compProperties in thingDef.comps ?? Enumerable.Empty<CompProperties>())
				{
					if (compProperties.compClass == typeof(CompSpawner))
					{
						CompProperties_Spawner compProperties_Spawner = compProperties as CompProperties_Spawner;
						if (compProperties_Spawner != null && processedDefs.Contains(compProperties_Spawner.thingToSpawn)) compProperties_Spawner.spawnCount = 0;
					}
					else if (compProperties.compClass == typeof(CompSpawnerPawn))
					{
						CompProperties_SpawnerPawn compProperties_SpawnerPawn = compProperties as CompProperties_SpawnerPawn;
						compProperties_SpawnerPawn.spawnablePawnKinds?.RemoveAll(x => processedDefs.Contains(x));
						//Seems the game already has handling if the list becomes 0 counted.
					}
				}
			}
			
			//Process TraderKindDef for removed items
			length = DefDatabase<TraderKindDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				TraderKindDef traderKindDef = DefDatabase<TraderKindDef>.defsList[i];
				
				var tmpList = traderKindDef.stockGenerators?.ToList();
				foreach (var stockGenerator in tmpList ?? Enumerable.Empty<StockGenerator>())
				{
					StockGenerator_MultiDef stockGenerator_MultiDef = stockGenerator as StockGenerator_MultiDef;
					if (stockGenerator_MultiDef != null)
					{
						stockGenerator_MultiDef.thingDefs?.RemoveAll(x => processedDefs.Contains(x));

						//If defs were removed and this list is now empty, remove the whole generator
						if (stockGenerator_MultiDef.thingDefs.Count == 0) traderKindDef.stockGenerators.Remove(stockGenerator);
						continue;
					}

					StockGenerator_SingleDef stockGenerator_SingleDef = stockGenerator as StockGenerator_SingleDef;
					if (stockGenerator_SingleDef != null && processedDefs.Contains(stockGenerator_SingleDef.thingDef))
					{
						traderKindDef.stockGenerators.Remove(stockGenerator);
					}
				}
			}

            Type extensionType = null;
            AccessTools.FieldRef<DefModExtension, object> getUnlockData = null;
            AccessTools.FieldRef<object, Def> getPath = null;
			//If we changed paths, we need to ensure they are properly removed from spawning
            if (processPsycastPaths)
            {
                if (!typeCache.TryGetValue("PawnKindAbilityExtension_Psycasts", out extensionType))
                {
                    extensionType = AccessTools.TypeByName("VanillaPsycastsExpanded.PawnKindAbilityExtension_Psycasts");
                    typeCache.Add("PawnKindAbilityExtension_Psycasts", extensionType);
                }

                if (!typeCache.TryGetValue("PathUnlockData", out var pathUnlockDataType))
                {
                    pathUnlockDataType = AccessTools.TypeByName("VanillaPsycastsExpanded.PathUnlockData");
                    typeCache.Add("PathUnlockData", pathUnlockDataType);
                }
				
                getUnlockData = AccessTools.FieldRefAccess<object>(extensionType, "unlockedPaths");
                getPath = AccessTools.FieldRefAccess<Def>(pathUnlockDataType, "path");
            }

            //Process pawnkinds that reference this item
			length = DefDatabase<PawnKindDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				PawnKindDef pawnKindDef = DefDatabase<PawnKindDef>.defsList[i];
				pawnKindDef.apparelRequired?.RemoveAll(x => processedDefs.Contains(x));
				pawnKindDef.techHediffsRequired?.RemoveAll(x => processedDefs.Contains(x));
				//Omits from manhunter event
				if (processedDefs.Contains(pawnKindDef.race))
				{
					pawnKindDef.canArriveManhunter = false;
					pawnKindDef.combatPower = float.MaxValue; //Makes too expensive to ever buy with points
					pawnKindDef.isGoodBreacher = false; //Special checks
				}
				if (processXenotypes) pawnKindDef.xenotypeSet?.xenotypeChances?.RemoveAll(x => processedDefs.Contains(x.xenotype));

                if (extensionType != null && getUnlockData != null && getPath != null && pawnKindDef.modExtensions != null)
                {
                    DefModExtension extension = pawnKindDef.modExtensions.FirstOrDefault(extensionType.IsInstanceOfType);
                    if (extension != null)
                    {
                        if (getUnlockData(extension) is IList unlockData)
                        {
                            for (int j = unlockData.Count; j-- > 0;)
                            {
                                if (processedDefs.Contains(getPath(unlockData[j]))) unlockData.RemoveAt(j); 
                            }
                        }
                    }
                }
			}

			//Processes biomes
			length = DefDatabase<BiomeDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				BiomeDef biomeDef = DefDatabase<BiomeDef>.defsList[i];
				biomeDef.wildPlants?.RemoveAll(x => processedDefs.Contains(x.plant));
				biomeDef.cachedWildPlants?.RemoveAll(x => processedDefs.Contains(x));
				biomeDef.cachedPlantCommonalities?.RemoveAll(x => processedDefs.Contains(x.Key));
				biomeDef.cachedAnimalCommonalities?.RemoveAll(x => processedDefs.Contains(x.Key));
				biomeDef.cachedPollutionAnimalCommonalities?.RemoveAll(x => processedDefs.Contains(x.Key));
				//Prevent animal spawning
				foreach (BiomeAnimalRecord biomeAnimalRecord in biomeDef.wildAnimals ?? Enumerable.Empty<BiomeAnimalRecord>())
				{
					if (biomeAnimalRecord.animal == null) continue;
					if (processedDefs.Contains(biomeAnimalRecord.animal.race) || processedDefs.Contains(biomeAnimalRecord.animal))
					{
						biomeAnimalRecord.commonality = 0;
					}
				}
				//Handle pollution cache list
				if (!ModLister.BiotechInstalled) continue;
				foreach (BiomeAnimalRecord biomeAnimalRecord in biomeDef.pollutionWildAnimals ?? Enumerable.Empty<BiomeAnimalRecord>())
				{
					if (biomeAnimalRecord.animal == null) continue;
					if (processedDefs.Contains(biomeAnimalRecord.animal.race) || processedDefs.Contains(biomeAnimalRecord.animal))
					{
						biomeAnimalRecord.commonality = 0;
					}
				}
			}

			//Processes gensteps
			length = DefDatabase<GenStepDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				GenStepDef genStepDef = DefDatabase<GenStepDef>.defsList[i];
				GenStep_ScatterGroup genStep_ScatterGroup = genStepDef.genStep as GenStep_ScatterGroup;
				if (genStep_ScatterGroup != null)
				{
					foreach (GenStep_ScatterGroup.ScatterGroup scatterGroup in genStep_ScatterGroup.groups)
					{
						scatterGroup.things.RemoveAll(x => processedDefs.Contains(x.thing));
					}
				}
				GenStep_ScatterThings genStep_ScatterThings = genStepDef.genStep as GenStep_ScatterThings;
				if (genStep_ScatterThings != null && processedDefs.Contains(genStep_ScatterThings.thingDef))
				{
					genStep_ScatterThings.clusterSize = 0;
				}
			}

			//Remove removed genes from xenotypes
			if (ModLister.BiotechInstalled)
			{
				length = DefDatabase<XenotypeDef>.DefCount;
				for (int i = 0; i < length; ++i)
				{
					XenotypeDef xenotypeDef = DefDatabase<XenotypeDef>.defsList[i];
					xenotypeDef.genes?.RemoveAll(x => processedDefs.Contains(x));
				}

				if (processXenotypes)
				{
					length = DefDatabase<FactionDef>.DefCount;
					for (int i = 0; i < length; ++i)
					{
						FactionDef factionDef = DefDatabase<FactionDef>.defsList[i];
						factionDef.xenotypeSet?.xenotypeChances?.RemoveAll(x => processedDefs.Contains(x.xenotype));
					}
					if (ModLister.ideologyInstalled)
					{
						length = DefDatabase<MemeDef>.DefCount;
						for (int i = 0; i < length; ++i)
						{
							MemeDef memeDef = DefDatabase<MemeDef>.defsList[i];
							memeDef.xenotypeSet?.xenotypeChances?.RemoveAll(x => processedDefs.Contains(x.xenotype));
						}
					}
				}

				//Process boss groups
				length = DefDatabase<BossgroupDef>.DefCount;
				for (int i = 0; i < length; ++i)
				{
					BossgroupDef bossgroupDef = DefDatabase<BossgroupDef>.defsList[i];
					foreach (BossGroupWave bossGroupWave in bossgroupDef.waves ?? Enumerable.Empty<BossGroupWave>())
					{
						bossGroupWave.escorts?.RemoveAll(x => processedDefs.Contains(x.kindDef));
					}
				}
			}

			//Processes styles (Ideology)
			if (ModLister.IdeologyInstalled)
			{
				var styleCategoryDefs2 = DefDatabase<StyleCategoryDef>.AllDefs.Select(x => x.thingDefStyles);
				//List of lists
				foreach (List<ThingDefStyle> styleCategoryDef in styleCategoryDefs2)
				{
					List<ThingDefStyle> styleDefWorkingList = styleCategoryDef.ToList();
					//Go through this list
					foreach (ThingDefStyle thingDefStyles in styleCategoryDef)
					{
						if (processedDefs.Contains(thingDefStyles.thingDef))
						{
							var styleDef = thingDefStyles.styleDef;
							DefDatabase<ThingStyleDef>.AllDefsListForReading.Remove(styleDef);
							styleDefWorkingList.Remove(thingDefStyles);
						}
					}
				}

				//Is this building used for a ideology precept?
				length = DefDatabase<PreceptDef>.DefCount;
				for (int i = 0; i < length; ++i)
				{
					PreceptDef preceptDef = DefDatabase<PreceptDef>.defsList[i];
					preceptDef.buildingDefChances?.RemoveAll(x => processedDefs.Contains(x.def));
				}
			}

			//Process designation removals
			if (processDesignators)
			{
				for (int i = DefDatabase<DesignationCategoryDef>.defsList.Count; i-- > 0;)
				{
					var designationCategoryDef = DefDatabase<DesignationCategoryDef>.defsList[i];
					if (designationCategoryDef.specialDesignatorClasses == null) continue;

					for (int j = designationCategoryDef.specialDesignatorClasses.Count; j-- > 0;)
					{
						var type = designationCategoryDef.specialDesignatorClasses[j];
						if (type == null) continue;
						Designator designator = (Designator)Activator.CreateInstance(type);
						if (processedDefs.Contains(designator.Designation))
						{
							designationCategoryDef.specialDesignatorClasses?.Remove(type);
							designationCategoryDef.resolvedDesignators?.Remove(designator);
						}
					}
				}
			}

			//Process body types from the backstory db
			if (processBodyTypes)
			{
				foreach (BackstoryDef backstory in DefDatabase<BackstoryDef>.AllDefsListForReading)
				{
					if (processedDefs.Contains(backstory.bodyTypeMale)) backstory.bodyTypeMale = BodyTypeDefOf.Male;
					if (processedDefs.Contains(backstory.bodyTypeFemale)) backstory.bodyTypeFemale = BodyTypeDefOf.Female;
					if (processedDefs.Contains(backstory.bodyTypeGlobal)) backstory.bodyTypeGlobal = null;
				}
			}

			//Process terrain
			if (processTerrain)
			{
				length = DefDatabase<TerrainDef>.DefCount;
				for (int i = 0; i < length; ++i)
				{
					TerrainDef terrainDef = DefDatabase<TerrainDef>.defsList[i];
					terrainDef.researchPrerequisites?.RemoveAll(x => processedDefs.Contains(x));
				}
			}
			
			//Process factions
			length = DefDatabase<FactionDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				FactionDef factionDef = DefDatabase<FactionDef>.defsList[i];
				//If a pawnkind was removed that a faction defaulted to, change the default to Colonist
				if (processedDefs.Contains(factionDef.basicMemberKind)) factionDef.basicMemberKind = PawnKindDefOf.Colonist;

				//Process memes from factions
				factionDef.requiredMemes?.RemoveAll(x => processedDefs.Contains(x));
				if (factionDef.requiredMemes?.Count == 0) factionDef.requiredMemes = null;

				//VE Classical mod support
				if (factionDef.modExtensions != null)
				{
					DefModExtension ext = factionDef.modExtensions.FirstOrDefault(x => x.GetType().Name == "FactionExtension_SenatorInfo");
					if (ext == null) continue;
		
					var field = ext.GetType().GetField("senatorResearch", AccessTools.all);
					if (field == null) continue;

					List<ResearchProjectDef> senatorResearch = (List<ResearchProjectDef>)field.GetValue(ext);
					senatorResearch?.RemoveAll(x => processedDefs.Contains(x));
				}
			}

			//Process cultures
			if (processRulePackDef)
			{
				length = DefDatabase<CultureDef>.DefCount;
				for (int i = 0; i < length; ++i)
				{
					CultureDef cultureDef = DefDatabase<CultureDef>.defsList[i];
					if (processedDefs.Contains(cultureDef.pawnNameMaker)) cultureDef.pawnNameMaker = null;
					if (processedDefs.Contains(cultureDef.pawnNameMakerFemale)) cultureDef.pawnNameMakerFemale = null;
					if (processedDefs.Contains(cultureDef.leaderTitleMaker)) cultureDef.leaderTitleMaker = null;
				}
			}	
			
			//ThingSetMakerDef
			length = DefDatabase<ThingSetMakerDef>.DefCount;
			for (int i = 0; i < length; ++i)
			{
				ThingSetMakerDef thingSetMakerDef = DefDatabase<ThingSetMakerDef>.defsList[i];

				ThingSetMaker_Sum thingSetMaker_Sum = thingSetMakerDef.root as ThingSetMaker_Sum;
				if (thingSetMaker_Sum == null) continue;
				foreach (var option in thingSetMaker_Sum.options ?? Enumerable.Empty<ThingSetMaker_Sum.Option>())
				{
					ThingSetMaker_RandomOption thingSetMaker_RandomOption = option?.thingSetMaker as ThingSetMaker_RandomOption;
					if (thingSetMaker_RandomOption == null) continue;
					foreach (var item in thingSetMaker_RandomOption.options ?? Enumerable.Empty<ThingSetMaker_RandomOption.Option>())
					{
						//Remove rewards based on a specific item, if that item was removed
						if (item.weightIfPlayerHasNoItemItem != null && processedDefs.Contains(item.weightIfPlayerHasNoItemItem))
						{
							item.weight = 0f;
							if (item.weightIfPlayerHasNoItem != null) item.weightIfPlayerHasNoItem = 0f;
						}
						//Remove rewards based on a specific xenotype, if that item was xenotype
						if (processXenotypes && item.weightIfPlayerHasXenotypeXenotype != null && processedDefs.Contains(item.weightIfPlayerHasXenotypeXenotype))
						{
							item.weightIfPlayerHasXenotypeXenotype = null;
							if (item.weightIfPlayerHasNoItem != null) item.weightIfPlayerHasXenotype = 0f;
						}

						//Remove rewards that come from a specific faction, if faction was removed
						ThingSetMaker_Conditional_MakingFaction thingSetMaker_Conditional_MakingFaction = item.thingSetMaker as ThingSetMaker_Conditional_MakingFaction;
						if (thingSetMaker_Conditional_MakingFaction == null) continue;
						if (processedDefs.Contains(thingSetMaker_Conditional_MakingFaction.makingFaction)) 
						{
							item.weight = 0f;
						}
					}
				}
			}

			//Need to remove psycasts from the cache in the ITab
            if (processPsycastPaths)
            {
                if (!typeCache.TryGetValue("ITab_Pawn_Psycasts", out var iTabType))
                {
                    iTabType = AccessTools.TypeByName("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts");
                    typeCache.Add("ITab_Pawn_Psycasts", iTabType);
                }

                if (InspectTabManager.sharedInstances.TryGetValue(iTabType, out var iTab))
                {
                    IDictionary paths = AccessTools.Field(iTabType, "pathsByTab").GetValue(iTab) as IDictionary;
                    foreach (var obj in paths.Values)
                    {
                        var list = obj as IList;
                        length = list.Count;
                        for (int i = length; i -- > 0;)
                        {
                            if (processedDefs.Contains(list[i])) list.RemoveAt(i);
                        }
                    }
                }
            }

            if (processPsycasts && typeCache.TryGetValue("PsycasterPathDef", out var pathDefType))
            {
                Def blank = (Def) AccessTools.Field(pathDefType, "Blank").GetValue(null);
                AccessTools.FieldRef<Def, Def[][]> psycasts = AccessTools.FieldRefAccess<Def, Def[][]>(AccessTools.Field(pathDefType, "abilityLevelsInOrder"));
                AccessTools.FieldRef<Def, IList> abilities = AccessTools.FieldRefAccess<Def, IList>(AccessTools.Field(pathDefType, "abilities"));
                if (!typeCache.TryGetValue("AbilityExtension_Psycast", out var psycastExtension))
                {
                    psycastExtension = AccessTools.TypeByName("VanillaPsycastsExpanded.AbilityExtension_Psycast");
                    typeCache.Add("AbilityExtension_Psycast", psycastExtension);
                }

                AccessTools.FieldRef<object, object> prereqsGetter = AccessTools.FieldRefAccess<object>(psycastExtension, "prerequisites");
                foreach (var path in (IEnumerable<Def>)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), pathDefType, "get_AllDefs"))
                {
                    Def[][] psycastLevels = psycasts(path);
                    for (int i = 0; i < psycastLevels.Length; i++)
                    {
                        Def[] psycastLevel = psycastLevels[i];
                        for (int j = 0; j < psycastLevel.Length; j++)
                        {
                            Def psycast = psycastLevel[j];
							if (psycast == blank) continue;
                            if (processedDefs.Contains(psycast))
                            {
                                //Replace the psycast with a blank as to not break the display
                                psycastLevels[i][j] = blank;
                            } 
                            else
                            {
                                DefModExtension extension = psycast.modExtensions.FirstOrDefault(psycastExtension.IsInstanceOfType);
                                if (prereqsGetter(extension) is IList prereqs)
                                {
                                    for (int k = prereqs.Count; k-- > 0;)
                                    {
                                        Def toCheck = prereqs[k] as Def;
                                        //Need to rewire any psycasts that depend on this to depend on the ones this depends on
                                        if (processedDefs.Contains(toCheck))
                                        {
                                            prereqs.RemoveAt(k);
                                            DefModExtension innerExtension = toCheck.modExtensions.FirstOrDefault(psycastExtension.IsInstanceOfType);
                                            if (prereqsGetter(innerExtension) is IList newPrereqs)
                                            {
                                                foreach (var newPrereq in newPrereqs)
                                                {
                                                    prereqs.Add(newPrereq);
                                                }
                                            }

                                        }
                                    }
									
                                    prereqsGetter(extension) = prereqs;
                                }
                            }
                        }
                    }

                    psycasts(path) = psycastLevels;

					IList abilitiesList = abilities(path);
                    for (int i = abilitiesList.Count; i-- > 0;)
                    {
                        if (abilitiesList[i] is Def toCheck && processedDefs.Contains(toCheck))
                        {
							abilitiesList.RemoveAt(i);
                        }
                    }

					abilities(path) = abilitiesList;
                }
            }
		}
	}
}