using Verse;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System;
using RimWorld;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.CherryPickerUtility;
using static CherryPicker.DrawUtility;
 
namespace CherryPicker
{
	public class DefList : Def { public List<string> defs; }
    public class Mod_CherryPicker : Mod
	{
		private QuickSearchWidget quickSearchWidget = new QuickSearchWidget();

		public Mod_CherryPicker(ModContentPack content) : base(content)
		{
			new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
			base.GetSettings<ModSettings_CherryPicker>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard options = new Listing_Standard();
			
			//Prepare scrollable view area rect
			Rect scrollViewRect = inRect;
			scrollViewRect.y += 30f;
			scrollViewRect.yMax -= 30f;
			
			//Prepare line height cache
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;

			//Calculate size of rect based on content
			Rect listRect = new Rect(0f, 0f, inRect.width - 30f, (lineNumber + 2) * lineHeight);

			//=========Def filter Button===========
			Rect defFilterRect = inRect;
			defFilterRect.height = 30f;
			defFilterRect.width *= .33f;
			options.Begin(defFilterRect);
				string defTypeButton = filteredType?.Name;
				TooltipHandler.TipRegion(new Rect(options.curX, options.curY, options.ColumnWidth, 30), ("CherryPicker." + (defTypeButton ?? "AllDefs") + ".Desc").Translate() );
				if (defTypeButton == null)
				{
					defTypeButton = "CherryPicker.AllDefTypes".Translate();
				}
				if (options.ButtonText(defTypeButton))
				{
					try
					{
						List<FloatMenuOption> buttonMenu = new List<FloatMenuOption>(MenuOfDefs());
						if (buttonMenu.Count != 0)
						{
							Find.WindowStack.Add(new FloatMenu(buttonMenu));
						}
					}
					catch (System.Exception ex) { Log.Message("[Cherry Picker] Error creating def type drop-down menu.\n" + ex); }
				}
			options.End();

			//=========Mod filter Button===========
			Rect modFilterRect = defFilterRect;
			modFilterRect.x += defFilterRect.width + 5f;
			options.Begin(modFilterRect);
			string packFilterButton = filteredMod.NullOrEmpty() ? "CherryPicker.AllPacks".Translate() : filteredMod;
			if (options.ButtonText(packFilterButton))
			{
				try
				{
					List<FloatMenuOption> buttonMenu = new List<FloatMenuOption>(MenuOfPacks());
					if (buttonMenu.Count != 0)
					{
						Find.WindowStack.Add(new FloatMenu(buttonMenu));
					}
				}
				catch (System.Exception ex) { Log.Message("[Cherry Picker] Error creating content pack drop-down menu.\n" + ex); }
			}
			options.End();

			//=========Search field===========
			Rect textFilterRect = modFilterRect;
			textFilterRect.x += modFilterRect.width + 5f;
			quickSearchWidget.OnGUI(textFilterRect);
			filter = quickSearchWidget.filter.Text.ToLower();
			filtered = !String.IsNullOrEmpty(filter);

			//=========Body===========
			Widgets.BeginScrollView(scrollViewRect, ref scrollPos, listRect, true);
				options.Begin(listRect);
				DrawList(inRect, options);
				Text.Anchor = anchor;
				options.End();
			Widgets.EndScrollView();
		}

		public override string SettingsCategory()
		{
			return "Cherry Picker";
		}

		public override void WriteSettings()
		{
			base.WriteSettings();
			cachedDefMenu = null; //Cleanup static to free memory
		}
	}

	public class ModSettings_CherryPicker : ModSettings
	{
		public override void ExposeData()
		{
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				try
				{
					ProcessList();
				}
				catch (Exception ex)
				{                
					Log.Error("[Cherry Picker] Error processing list. Skipping to preserve data...\n" + ex);
				}
			}
			
			Scribe_Collections.Look(ref allRemovedDefs, "keys", LookMode.Value);

			base.ExposeData();
		}

		public static HashSet<string> allRemovedDefs = new HashSet<string>();
		public static Vector2 scrollPos;
		public static string filter;
	}
}
