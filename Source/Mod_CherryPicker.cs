using Verse;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.CherryPickerUtility;
using static CherryPicker.DrawUtility;
 
namespace CherryPicker
{
    public class Mod_CherryPicker : Mod
	{
		public Mod_CherryPicker(ModContentPack content) : base(content)
		{
			new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
			base.GetSettings<ModSettings_CherryPicker>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard options = new Listing_Standard();
			//Make stationary rect for the filter box
			Rect filterRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, 100f);
			//Prepare scrollable view area rect
			Rect scrollViewRect = inRect;
			scrollViewRect.y += 30f;
			scrollViewRect.yMax -= 30f;
			
			//Prepare line height cache
			TextAnchor anchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;

			//Calculate size of rect based on content
			Rect listRect = new Rect(0f, 0f, inRect.width - 30f, (lineNumber + 2) * lineHeight);

			options.Begin(inRect);
				filterCache = options.TextEntryLabeled("Filter: ", filterCache);
				filter = filterCache.ToLower();
				filtered = !String.IsNullOrEmpty(filter);
			options.End();
			Widgets.BeginScrollView(scrollViewRect, ref scrollPos, listRect, true);
				options.Begin(listRect);
				DrawList(inRect, options);
				Text.Anchor = anchor;
				options.End();
			Widgets.EndScrollView();
			
			base.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "Cherry Picker";
		}

		public override void WriteSettings()
		{
			base.WriteSettings();
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

		public static HashSet<string> allRemovedDefs;
		public static Vector2 scrollPos;
		public static string filterCache;
		public static string filter;
	}
}
