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
				allDefs = processedDefs
					.Concat(DefDatabase<ThingDef>.AllDefs
						.Where(x => !x.IsBlueprint && !x.IsFrame && !x.isUnfinishedThing && (x.category == ThingCategory.Item || x.category == ThingCategory.Building || x.category == ThingCategory.Plant || x.category == ThingCategory.Pawn)))
					.Concat(DefDatabase<ResearchProjectDef>.AllDefs
						.Where(x => DefDatabase<ResearchProjectDef>.AllDefs.Any(y => (!y.prerequisites?.Contains(x) ?? true) && (!y.hiddenPrerequisites?.Contains(x) ?? true))))
					.Concat(DefDatabase<BodyTypeDef>.AllDefs
						.Where(x => x != BodyTypeDefOf.Male && x != BodyTypeDefOf.Female))
					.Concat(DefDatabase<FactionDef>.AllDefs
						.Where(x => x.maxConfigurableAtWorldCreation > 0))
					.Concat(DefDatabase<PawnKindDef>.AllDefs
						.Where(x => x != PawnKindDefOf.Colonist))
					.Concat(DefDatabase<QuestScriptDef>.AllDefs
						.Where(x => !DefDatabase<IncidentDef>.AllDefs.Any(y => y.questScriptDef == x)))
					.Concat(DefDatabase<TerrainDef>.AllDefs)
					.Concat(DefDatabase<RecipeDef>.AllDefs)
					.Concat(DefDatabase<TraitDef>.AllDefs)
					.Concat(DefDatabase<DesignationCategoryDef>.AllDefs)
					.Concat(DefDatabase<ThingStyleDef>.AllDefs)
					.Concat(DefDatabase<IncidentDef>.AllDefs)
					.Concat(DefDatabase<HediffDef>.AllDefs)
					.Concat(DefDatabase<ThoughtDef>.AllDefs)
					.Concat(DefDatabase<TraderKindDef>.AllDefs)
					.Concat(DefDatabase<GatheringDef>.AllDefs)
					.Concat(DefDatabase<WorkTypeDef>.AllDefs)
					.Concat(DefDatabase<MemeDef>.AllDefs)
					.Concat(DefDatabase<PreceptDef>.AllDefs)
					.Concat(DefDatabase<RitualPatternDef>.AllDefs)
					.Concat(DefDatabase<HairDef>.AllDefs)
					.Concat(DefDatabase<TattooDef>.AllDefs)
					.Concat(DefDatabase<BeardDef>.AllDefs)
					.Concat(DefDatabase<RaidStrategyDef>.AllDefs)
					.Concat(DefDatabase<MainButtonDef>.AllDefs)
					.Concat(DefDatabase<AbilityDef>.AllDefs)
					.Concat(DefDatabase<BiomeDef>.AllDefs)
					.Concat(DefDatabase<MentalBreakDef>.AllDefs)
					.Concat(DefDatabase<SpecialThingFilterDef>.AllDefs)
					.Concat(DefDatabase<GenStepDef>.AllDefs)
					.Concat(DefDatabase<InspirationDef>.AllDefs)
					.Concat(DefDatabase<StorytellerDef>.AllDefs)
					.Concat(DefDatabase<ScenarioDef>.AllDefs)
					.Concat(DefDatabase<DesignationDef>.AllDefs)
					.Concat(DefDatabase<PawnsArrivalModeDef>.AllDefs)
					.Concat(DefDatabase<GeneDef>.AllDefs)
					.Concat(DefDatabase<XenotypeDef>.AllDefs)
					.Concat(DefDatabase<BackstoryDef>.AllDefs)
					.Concat(DefDatabase<WeatherDef>.AllDefs)
					.Concat(DefDatabase<ScatterableDef>.AllDefs)
					.Concat(DefDatabase<RaidAgeRestrictionDef>.AllDefs)
					.Concat(DefDatabase<WeaponTraitDef>.AllDefs)
					.Concat(DefDatabase<RulePackDef>.AllDefs)
					.Concat(DefDatabase<InteractionDef>.AllDefs)
					.Concat(DefDatabase<DefList>.AllDefs)
					.Concat(GetDefFromMod(packageID: "vanillaexpanded.vfea", assemblyName: "VFEAncients", nameSpace: "VFEAncients", typeName: "PowerDef"))
					.Concat(GetDefFromMod(packageID: "oskarpotocki.vanillafactionsexpanded.core", assemblyName:"VFECore", nameSpace:"VFECore.Abilities", typeName:"AbilityDef")
                       .Where(x => x.modExtensions.Any(e => e.GetType().Namespace == "VanillaPsycastsExpanded")))
                    .Concat(GetDefFromMod(packageID: "vanillaexpanded.vpsycastse", assemblyName: "VanillaPsycastsExpanded", nameSpace: "VanillaPsycastsExpanded", typeName: "PsycasterPathDef"))
					.Distinct() //Some dynamically generated defs can seemingly caused dupes
					.ToArray();
			}
			catch (Exception ex)
			{                
				Log.Error("[Cherry Picker] Error constructing master def list...\n" + ex);
				return;
			}
			try
			{
				//Process lists
				MakeWorkingList();
				ProcessList();
			}
			catch (Exception ex)
			{                
				Log.Error("[Cherry Picker] Error processing master def list...\n" + ex);
				return;
			}

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
		static IEnumerable<Def> GetDefFromMod(string packageID, string assemblyName, string nameSpace, string typeName)
		{
			if (LoadedModManager.RunningMods.FirstOrDefault(x => x.PackageId == packageID) is ModContentPack mod) 
			{
				if (mod.assemblies.loadedAssemblies.FirstOrDefault(a => a.GetName().Name == assemblyName)?.GetType(nameSpace + "." + typeName) is Type type)
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
				var method = AccessTools.PropertyGetter(typeof(ThingDef), nameof(ThingDef.IsStuff));
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
				switch(def)
				{
					case ThingDef thingDef:
					{
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
							if (thingDef.apparel is ApparelProperties apparelProperties)
							{
								reloadRequired = true;

								apparelProperties.tags?.Clear();
								apparelProperties.defaultOutfitTags?.Clear();
								apparelProperties.canBeDesiredForIdeo = false;
								apparelProperties.ideoDesireAllowedFactionCategoryTags?.Clear();
								apparelProperties.ideoDesireDisallowedFactionCategoryTags?.Clear();

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
							if (thingDef.race.wildBiomes != null)
							{
								foreach (var animalBiomeRecord in thingDef.race.wildBiomes)
								{
									animalBiomeRecord.commonality = 0f;
								}
							}
						}
						break;
					}
				
					case TerrainDef terrainDef:
					{
						//Hide from architect menus
						DesignationCategoryDef originalDesignationCategory = terrainDef.designationCategory;
						terrainDef.designationCategory = null;
						originalDesignationCategory?.ResolveReferences();

						terrainDef.costList?.Clear(); //Prevent from map spawning
						break;
					}
				
					case RecipeDef recipeDef:
					{
						if (recipeDef.recipeUsers != null)
						{
							foreach (ThingDef x in recipeDef.recipeUsers)
							{
								x.recipes?.Remove(recipeDef);
								x.allRecipesCached?.Remove(recipeDef);
							}
						}
						
						recipeDef.requiredGiverWorkType = null;
						recipeDef.researchPrerequisite = null;
						recipeDef.researchPrerequisites?.Clear();
						//Remove from database too. Even though it's removed, its reference may still persist in other cached collections
						DefDatabase<RecipeDef>.Remove(recipeDef);
						break;
					}

					case TraitDef traitDef:
					{
						DefDatabase<TraitDef>.Remove(traitDef);
						break;
					}
					
					case ResearchProjectDef researchProjectDef:
					{
						//Do any other research project use as a prereq?
						var researchProjectDefList = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
						for (int i = researchProjectDefList.Count; i-- > 0;)
						{
							ResearchProjectDef entry = researchProjectDefList[i];
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
					
					case DesignationCategoryDef designationCategoryDef:
					{
						DefDatabase<DesignationCategoryDef>.Remove(designationCategoryDef);
						if (Current.ProgramState == ProgramState.Playing) reloadRequired = true;
						break;
					}
					
					case ThingStyleDef thingStyleDef:
					{
						var styleCategoryDefList = DefDatabase<StyleCategoryDef>.AllDefsListForReading;
						for (int i = styleCategoryDefList.Count; i-- > 0;)
						{
							var styleCategoryDef  = styleCategoryDefList[i];
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
					
					case QuestScriptDef questScriptDef:
					{	
						questScriptDef.rootSelectionWeight = 0; //Makes the IsRootRandomSelected getter return false, which excludes from ChooseNaturalRandomQuest
						questScriptDef.decreeSelectionWeight = 0; //Excludes decrees
						break;
					}
					
					case IncidentDef incidentDef:
					{	
						incidentDef.baseChance = 0;
						incidentDef.baseChanceWithRoyalty = 0;
						incidentDef.earliestDay = int.MaxValue;
						incidentDef.minThreatPoints = float.MaxValue;
						incidentDef.minPopulation = int.MaxValue;
						break;
					}
					
					case ThoughtDef thoughtDef:
					{
						thoughtDef.durationDays = 0f;
						thoughtDef.isMemoryCached = BoolUnknown.Unknown;
						thoughtDef.minExpectation = new ExpectationDef() { order = int.MaxValue };
						break;
					}

					case TraderKindDef traderKindDef:
					{
						traderKindDef.commonality = 0f;
						break;
					}

					case GatheringDef gatheringDef:
					{
						gatheringDef.randomSelectionWeight = 0f;
						break;
					}

					case InspirationDef inspirationDef:
					{
						inspirationDef.baseCommonality = 0f;
						break;
					}

					case WorkTypeDef workTypeDef:
					{
						workTypeDef.visible = false;
						
						var pawnColumnDefList = DefDatabase<PawnColumnDef>.AllDefsListForReading;
						for (int i = pawnColumnDefList.Count; i-- > 0;)
						{
							var pawnColumnDef = pawnColumnDefList[i];
							if (pawnColumnDef.workType == workTypeDef) pawnColumnDefList.Remove(pawnColumnDef);
						}

						var pawnTableDefList = DefDatabase<PawnTableDef>.AllDefsListForReading;
						for (int i = pawnTableDefList.Count; i-- > 0;)
						{
							pawnTableDefList[i].columns.RemoveAll(y => y.workType == workTypeDef);
						}
						break;
					}

					case MemeDef memeDef:
					{
						DefDatabase<MemeDef>.Remove(memeDef);
						break;
					}

					case PreceptDef preceptDef:
					{
						preceptDef.enabledForNPCFactions = false;
						preceptDef.visible = false;
						preceptDef.selectionWeight = 0;
						DefDatabase<PreceptDef>.Remove(preceptDef);
						break;
					}

					case RitualPatternDef ritualPatternDef:
					{
						DefDatabase<RitualPatternDef>.Remove(ritualPatternDef);
						break;
					}

					case HairDef hairDef:
					{
						DefDatabase<HairDef>.Remove(hairDef);
						break;
					}

					case BeardDef beardDef:
					{
						DefDatabase<BeardDef>.Remove(beardDef);
						break;
					}

					case TattooDef tattooDef:
					{
						DefDatabase<TattooDef>.Remove(tattooDef);
						break;
					}

					case RaidStrategyDef raidStrategyDef:
					{
						raidStrategyDef.pointsFactorCurve = zeroCurve;
						raidStrategyDef.selectionWeightPerPointsCurve = zeroCurve;
						break;
					}

					case MainButtonDef mainButtonDef:
					{
						mainButtonDef.buttonVisible = false;
						break;
					}

					case AbilityDef abilityDef:
					{
						abilityDef.level = int.MaxValue; //Won't make it past the random select filters
						DefDatabase<PreceptDef>.AllDefsListForReading.ForEach(x => x.grantedAbilities?.Remove(abilityDef));
						DefDatabase<RoyalTitleDef>.AllDefsListForReading.ForEach(x => x.grantedAbilities?.Remove(abilityDef));
						break;
					}

					case BiomeDef biomeDef:
					{
						biomeDef.implemented = false;
						break;
					}

					case MentalBreakDef mentalBreakDef:
					{
						mentalBreakDef.baseCommonality = 0;
						break;
					}

					case SpecialThingFilterDef specialThingFilterDef:
					{
						var recipeDefList = DefDatabase<RecipeDef>.AllDefsListForReading;
						for (int i = recipeDefList.Count; i-- > 0;)
						{
							RecipeDef recipeDef = recipeDefList[i];
							recipeDef.fixedIngredientFilter?.specialFiltersToAllow?.Remove(specialThingFilterDef.defName);
							recipeDef.fixedIngredientFilter?.specialFiltersToDisallow?.Remove(specialThingFilterDef.defName);
							recipeDef.defaultIngredientFilter?.specialFiltersToAllow?.Remove(specialThingFilterDef.defName);
							recipeDef.defaultIngredientFilter?.specialFiltersToDisallow?.Remove(specialThingFilterDef.defName);
							recipeDef.forceHiddenSpecialFilters?.Remove(specialThingFilterDef);
						}

						break;
					}

					case DefList defList:
					{
						reprocess = true;
						reprocessDefs.AddRange(defList.defs);
						break;
					}

					case PawnKindDef pawnKindDef:
					{
						pawnKindDef.combatPower = float.MaxValue; //Should make it fail selective filters
						pawnKindDef.canArriveManhunter = false;
						pawnKindDef.canBeSapper = false;
						pawnKindDef.allowInMechClusters = false;
						pawnKindDef.minGenerationAge = 0;
						break;
					}

					case GenStepDef genStepDef:
					{
						var mapGeneratorDefList = DefDatabase<MapGeneratorDef>.AllDefsListForReading;
						for (int i = mapGeneratorDefList.Count; i-- > 0;)
						{
							mapGeneratorDefList[i].genSteps?.Remove(genStepDef);
						}
						break;
					}
					
					case HediffDef hediffDef:
					{
						break; //Do nothing, this can only be handled via harmony interception
					}

					case StorytellerDef storytellerDef:
					{
						storytellerDef.listVisible = false;
						break;
					}

					case ScenarioDef scenarioDef:
					{
						scenarioDef.scenario.showInUI = false;
						break;
					}

					case DesignationDef designationDef:
					{
						processDesignators = true;
						break;
					}

					case FactionDef factiondef:
					{
						factiondef.maxConfigurableAtWorldCreation = -1; //Makes the UI skip it
						factiondef.startingCountAtWorldCreation = 0;
						factiondef.requiredCountAtGameStart = 0;
						break;
					}

					case BodyTypeDef bodyTypeDef:
					{
						processBodyTypes = true;
						break;
					}

					case BackstoryDef backstoryDef:
					{
						DefDatabase<BackstoryDef>.Remove(backstoryDef);
						break;
					}

					case WeatherDef weatherDef:
					{
						weatherDef.temperatureRange = new FloatRange(-999f, -998f); //Hack to invalidate for all biomes
						weatherDef.isBad = false; //Excludes from forced weather
						break;
					}
					
					case PawnsArrivalModeDef pawnsArrivalModeDef:
					{
						pawnsArrivalModeDef.minTechLevel = TechLevel.Archotech;
						pawnsArrivalModeDef.pointsFactorCurve = zeroCurve;
						pawnsArrivalModeDef.selectionWeightCurve = zeroCurve;
						break;
					}
					
					case GeneDef geneDef:
					{
						if (!ModLister.BiotechInstalled) break;
						DefDatabase<GeneDef>.Remove(geneDef);
						break;
					}

					case XenotypeDef xenotypeDef:
					{
						if (!ModLister.BiotechInstalled) break;
						DefDatabase<XenotypeDef>.Remove(xenotypeDef);
						processXenotypes = true;
						break;
					}

					case ScatterableDef scatterableDef:
					{
						scatterableDef.scatterType = "null";
						break;
					}

					case RaidAgeRestrictionDef raidAgeRestrictionDef:
					{
						raidAgeRestrictionDef.chance = 0f;
						break;
					}

					case WeaponTraitDef weaponTraitDef:
					{
						weaponTraitDef.commonality = 0f;
						break;
					}

					case RulePackDef rulePackDef:
					{
						processRulePackDef = true;
						break;
					}

					case InteractionDef interactionDef:
					{
						interactionDef.workerInt = (InteractionWorker)Activator.CreateInstance(typeof(InteractionWorker_Dummy));
						interactionDef.workerInt.interaction = interactionDef;
						break;
					}
					
					//Mod: Vanilla Factions Expanded: Ancients
					case Def poweDef when poweDef.GetType().Name == "PowerDef":
					{
						if (typeCache.TryGetValue("PowerDef", out Type type))
						{
							GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, "Remove", def);
						}
						break;
					}

					//Mod: Vanilla Psycasts Expanded
					case Def psycasterPathDef when psycasterPathDef.GetType().Name == "PsycasterPathDef":
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
					case Def abilityDef when abilityDef.GetType().Name == "AbilityDef" && (def.modExtensions.Any(e => e.GetType().Namespace == "VanillaPsycastsExpanded") ):
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

					case nameof(TattooDef):
					{
						DefDatabase<TattooDef>.Add(def as TattooDef);
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
			var scenarioDefList = DefDatabase<ScenarioDef>.defsList;
			for (int i = scenarioDefList.Count; i-- > 0;)
			{
				ScenarioDef scenarioDef = scenarioDefList[i];
				scenarioDef.scenario.parts?.RemoveAll(y => y is ScenPart_StartingThing_Defined scenPar && 
					processedDefs.Contains(scenPar.thingDef));

				if(processXenotypes)
				{
					if (scenarioDef.scenario?.parts is not List<ScenPart> parts) continue;
					foreach (var scenPart in parts)
					{
						if (scenPart is not ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes xenoScenPart) continue;
						xenoScenPart.overrideKinds?.RemoveAll(x => processedDefs.Contains(x.xenotype));
						xenoScenPart.xenotypeCounts?.RemoveAll(x => processedDefs.Contains(x.xenotype));
					}
				}
			}

			//Processes recipes using removed items
			var recipeDefList = DefDatabase<RecipeDef>.defsList;
			for (int i = recipeDefList.Count; i-- > 0;)
			{
				RecipeDef recipeDef = recipeDefList[i];

				if (recipeDef.ingredients is not List<IngredientCount> ingredients) continue;
				bool somethingRemoved = false;
				foreach (IngredientCount ingredientCount in ingredients)
				{
					ingredientCount.filter?.thingDefs?.RemoveAll(x => processedDefs.Contains(x));
					somethingRemoved = ingredientCount.filter?.allowedDefs?.RemoveWhere(x => processedDefs.Contains(x)) > 0 || somethingRemoved;
				}

				if (somethingRemoved && ingredients.All(x => x.filter.allowedDefs.Count == 0))
				{
					DefDatabase<RecipeDef>.Remove(recipeDef); //This is mainly to handle dynamically generated receipes such as Administer_<drugDef>
				}

				recipeDef.fixedIngredientFilter?.thingDefs?.RemoveAll(x => processedDefs.Contains(x));
				recipeDef.fixedIngredientFilter?.allowedDefs?.RemoveWhere(x => processedDefs.Contains(x));
				recipeDef.defaultIngredientFilter?.thingDefs?.RemoveAll(x => processedDefs.Contains(x));
				recipeDef.defaultIngredientFilter?.allowedDefs?.RemoveWhere(x => processedDefs.Contains(x));
			}

			//Process various references within thingDefs
			var thingDefList = DefDatabase<ThingDef>.defsList;
			for (int i = thingDefList.Count; i-- > 0;)
			{
				ThingDef thingDef = thingDefList[i];
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
				if (thingDef.comps == null) continue;
				foreach (CompProperties compProperties in thingDef.comps)
				{
					if (compProperties is CompProperties_Spawner compProperties_Spawner)
					{
						if (processedDefs.Contains(compProperties_Spawner.thingToSpawn)) compProperties_Spawner.spawnCount = 0;
					}
					else if (compProperties is CompProperties_SpawnerPawn compProperties_SpawnerPawn)
					{
						compProperties_SpawnerPawn.spawnablePawnKinds?.RemoveAll(x => processedDefs.Contains(x));
						//Seems the game already has handling if the list becomes 0 counted.
					}
				}
			}
			
			//Process TraderKindDef for removed items
			var traderKindDefList = DefDatabase<TraderKindDef>.defsList;
			for (int i = traderKindDefList.Count; i-- > 0;)
			{
				TraderKindDef traderKindDef = traderKindDefList[i];
				
				var stockGeneratorsList = traderKindDef.stockGenerators;
				for (int j = stockGeneratorsList?.Count ?? 0; j-- > 0;)
				{
					var stockGenerator = stockGeneratorsList[j];
					if (stockGenerator is StockGenerator_MultiDef stockGenerator_MultiDef)
					{
						stockGenerator_MultiDef.thingDefs?.RemoveAll(x => processedDefs.Contains(x));

						//If defs were removed and this list is now empty, remove the whole generator
						if (stockGenerator_MultiDef.thingDefs.Count == 0) traderKindDef.stockGenerators.Remove(stockGenerator);
					}
					else if (stockGenerator is StockGenerator_SingleDef stockGenerator_SingleDef && processedDefs.Contains(stockGenerator_SingleDef.thingDef))
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
			var pawnKindDefList = DefDatabase<PawnKindDef>.defsList;
			for (int i = pawnKindDefList.Count; i-- > 0;)
			{
				PawnKindDef pawnKindDef = pawnKindDefList[i];
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
			var biomeDefList = DefDatabase<BiomeDef>.defsList;
			for (int i = biomeDefList.Count; i-- > 0;)
			{
				BiomeDef biomeDef = biomeDefList[i];
				biomeDef.wildPlants?.RemoveAll(x => processedDefs.Contains(x.plant));
				biomeDef.cachedWildPlants?.RemoveAll(x => processedDefs.Contains(x));
				biomeDef.cachedPlantCommonalities?.RemoveAll(x => processedDefs.Contains(x.Key));
				biomeDef.cachedAnimalCommonalities?.RemoveAll(x => processedDefs.Contains(x.Key));
				biomeDef.cachedPollutionAnimalCommonalities?.RemoveAll(x => processedDefs.Contains(x.Key));
				//Prevent animal spawning
				if (biomeDef.wildAnimals != null)
				{
					foreach (BiomeAnimalRecord biomeAnimalRecord in biomeDef.wildAnimals)
					{
						if (biomeAnimalRecord.animal == null) continue;
						if (processedDefs.Contains(biomeAnimalRecord.animal.race) || processedDefs.Contains(biomeAnimalRecord.animal))
						{
							biomeAnimalRecord.commonality = 0;
						}
					}
				}
				//Handle pollution cache list
				if (!ModLister.BiotechInstalled || biomeDef.pollutionWildAnimals == null) continue;
				foreach (BiomeAnimalRecord biomeAnimalRecord in biomeDef.pollutionWildAnimals)
				{
					if (biomeAnimalRecord.animal == null) continue;
					if (processedDefs.Contains(biomeAnimalRecord.animal.race) || processedDefs.Contains(biomeAnimalRecord.animal))
					{
						biomeAnimalRecord.commonality = 0;
					}
				}
			}

			//Processes gensteps
			var genStepDefList = DefDatabase<GenStepDef>.defsList;
			for (int i = genStepDefList.Count; i-- > 0;)
			{
				var genStep = genStepDefList[i].genStep;
				if (genStep is GenStep_ScatterGroup genStep_ScatterGroup)
				{
					foreach (GenStep_ScatterGroup.ScatterGroup scatterGroup in genStep_ScatterGroup.groups)
					{
						scatterGroup.things.RemoveAll(x => processedDefs.Contains(x.thing));
					}
				}
				if (genStep is GenStep_ScatterThings genStep_ScatterThings && processedDefs.Contains(genStep_ScatterThings.thingDef))
				{
					genStep_ScatterThings.clusterSize = 0;
				}
			}

			//Process factions
			var factionDefList = DefDatabase<FactionDef>.defsList;
			for (int i = factionDefList.Count; i-- > 0;)
			{
				FactionDef factionDef = factionDefList[i];
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
			
			//Remove removed genes from xenotypes
			if (ModLister.BiotechInstalled)
			{
				var xenotypeDefList = DefDatabase<XenotypeDef>.defsList;
				for (int i = xenotypeDefList.Count; i-- > 0;)
				{
					xenotypeDefList[i].genes?.RemoveAll(x => processedDefs.Contains(x));
				}

				if (processXenotypes)
				{
					for (int i = factionDefList.Count; i-- > 0;)
					{
						factionDefList[i].xenotypeSet?.xenotypeChances?.RemoveAll(x => processedDefs.Contains(x.xenotype));
					}
					if (ModLister.ideologyInstalled)
					{
						var memeDefList = DefDatabase<MemeDef>.defsList;
						for (int i = memeDefList.Count; i-- > 0;)
						{
							memeDefList[i].xenotypeSet?.xenotypeChances?.RemoveAll(x => processedDefs.Contains(x.xenotype));
						}
					}
				}

				//Process boss groups
				var bossgroupDefList = DefDatabase<BossgroupDef>.defsList;
				for (int i = bossgroupDefList.Count; i-- > 0;)
				{
					BossgroupDef bossgroupDef = bossgroupDefList[i];
					if (bossgroupDef.waves == null) continue;
					foreach (BossGroupWave bossGroupWave in bossgroupDef.waves)
					{
						bossGroupWave.escorts?.RemoveAll(x => processedDefs.Contains(x.kindDef));
					}
				}
			}

			//Processes styles and Precepts (Ideology)
			if (ModLister.IdeologyInstalled)
			{
				//List of lists
				foreach (List<ThingDefStyle> styleCategoryDefList in DefDatabase<StyleCategoryDef>.AllDefs.Select(x => x.thingDefStyles))
				{
					//Go through this list
					for (int i = styleCategoryDefList.Count; i-- > 0;)
					{
						ThingDefStyle thingDefStyles = styleCategoryDefList[i];
						if (processedDefs.Contains(thingDefStyles.thingDef))
						{
							DefDatabase<ThingStyleDef>.Remove(thingDefStyles.styleDef);
							styleCategoryDefList.Remove(thingDefStyles);
						}
					}
				}

				//Is this building used for a ideology precept?
				var preceptDefList = DefDatabase<PreceptDef>.defsList;
				for (int i = preceptDefList.Count; i-- > 0;)
				{
					PreceptDef preceptDef = preceptDefList[i];
					preceptDef.buildingDefChances?.RemoveAll(x => processedDefs.Contains(x.def));
					if (preceptDef.roleApparelRequirements == null) continue;

					var roleApparelRequirementsList = preceptDef.roleApparelRequirements;
					for (int j = roleApparelRequirementsList.Count; j-- > 0;)
					{
						var roleApparelRequirements = roleApparelRequirementsList[j];
						if (roleApparelRequirements.requirement?.requiredDefs?.RemoveAll(x => processedDefs.Contains(x)) > 0 && roleApparelRequirements.requirement.requiredDefs.Count == 0)
						{
							roleApparelRequirementsList.Remove(roleApparelRequirements);
						}
					}
				}
			}

			//Process designation removals
			if (processDesignators)
			{
				var designationCategoryDefList = DefDatabase<DesignationCategoryDef>.defsList;
				for (int i = designationCategoryDefList.Count; i-- > 0;)
				{
					var designationCategoryDef = designationCategoryDefList[i];
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
				var backstoryDefList = DefDatabase<BackstoryDef>.defsList;
				for (int i = backstoryDefList.Count; i-- > 0;)
				{
					BackstoryDef backstory = backstoryDefList[i];
					if (processedDefs.Contains(backstory.bodyTypeMale)) backstory.bodyTypeMale = BodyTypeDefOf.Male;
					if (processedDefs.Contains(backstory.bodyTypeFemale)) backstory.bodyTypeFemale = BodyTypeDefOf.Female;
					if (processedDefs.Contains(backstory.bodyTypeGlobal)) backstory.bodyTypeGlobal = null;
				}
			}

			//Process terrain
			if (processTerrain)
			{
				var terrainDefList = DefDatabase<TerrainDef>.defsList;
				for (int i = terrainDefList.Count; i-- > 0;)
				{
					terrainDefList[i].researchPrerequisites?.RemoveAll(x => processedDefs.Contains(x));
				}
			}
			
			//Process cultures
			if (processRulePackDef)
			{
				var cultureDefList = DefDatabase<CultureDef>.defsList;
				for (int i = cultureDefList.Count; i-- > 0;)
				{
					CultureDef cultureDef = cultureDefList[i];
					if (processedDefs.Contains(cultureDef.pawnNameMaker)) cultureDef.pawnNameMaker = null;
					if (processedDefs.Contains(cultureDef.pawnNameMakerFemale)) cultureDef.pawnNameMakerFemale = null;
					if (processedDefs.Contains(cultureDef.leaderTitleMaker)) cultureDef.leaderTitleMaker = null;
				}
			}	
			
			//ThingSetMakerDef
			var thingSetMakerDefList = DefDatabase<ThingSetMakerDef>.defsList;
			for (int i = thingSetMakerDefList.Count; i-- > 0;)
			{
				ThingSetMaker thingSetMaker = thingSetMakerDefList[i].root;

				if (thingSetMaker is not ThingSetMaker_Sum thingSetMaker_Sum || thingSetMaker_Sum.options.NullOrEmpty()) continue;
				foreach (var option in thingSetMaker_Sum.options)
				{
					if (option.thingSetMaker is not ThingSetMaker_RandomOption thingSetMaker_RandomOption || thingSetMaker_RandomOption.options.NullOrEmpty()) continue;
					foreach (var item in thingSetMaker_RandomOption.options)
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
						if (item.thingSetMaker is not ThingSetMaker_Conditional_MakingFaction thingSetMaker_Conditional_MakingFaction) continue;
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
                        for (int i = list.Count; i-- > 0;)
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