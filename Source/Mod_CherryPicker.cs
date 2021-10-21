using Verse;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using RimWorld;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.CherryPickerUtility;
 
namespace CherryPicker
{
	#if DEBUG
	[HotSwap.HotSwappable]
	#endif
    public class Mod_CherryPicker : Mod
	{
		public Mod_CherryPicker(ModContentPack content) : base(content)
		{
			new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
			base.GetSettings<ModSettings_CherryPicker>();
			LongEventHandler.QueueLongEvent(() => Setup(), "CherryPicker.Setup", false, null);
		}

		const float lineSpacing = 20.5f;
		static int lastNumOfLines = 1;
		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard options = new Listing_Standard();
			Rect filterRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, 100f);
			Rect scrollViewRect = inRect;
			scrollViewRect.y += 30f;
			scrollViewRect.yMax -= 30f;
			Rect listRect = new Rect(0f, 0f, inRect.width - 30f, (lastNumOfLines + 1) * lineSpacing);

			options.Begin(inRect);
				filter = options.TextEntryLabeled("Filter: ", filter);
				filtered = !String.IsNullOrEmpty(filter);
			options.End();
			Widgets.BeginScrollView(scrollViewRect, ref scrollPos, listRect, true);
				options.Begin(listRect);
				Text.Font = GameFont.Tiny;
				for (int i = 0; i < workingList.Count; ++i)
				{
					Def def = allDefs.FirstOrDefault(x => x.defName == workingList[i]);
					options.DrawListItem(def);
				}
				options.GapLine();
				for (int i = 0; i < allDefs.Length; ++i)
				{
					Def def = allDefs[i];
					if (!workingList.Contains(def.defName)) options.DrawListItem(def);
				}
				lastNumOfLines = lineNumber;
				lineNumber = 0;
				Text.Font = GameFont.Small;
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
			ProcessList();
			base.WriteSettings();
		}
	}

	public class ModSettings_CherryPicker : ModSettings
	{
		public override void ExposeData()
		{
			Scribe_Collections.Look(ref removedDefs, "removedDefs", LookMode.Value, LookMode.Value);
			if (removedDefs == null) removedDefs = new List<string>();
			base.ExposeData();
		}

		public static List<string> removedDefs = new List<string>();
		public static Vector2 scrollPos = Vector2.zero;
		public static string filter;
	}
}
