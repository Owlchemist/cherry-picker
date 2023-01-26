using Verse;
using RimWorld;
using UnityEngine;
using Verse.Sound;
using System.Collections.Generic;
using System;
using System.Linq;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.CherryPickerUtility;
 
namespace CherryPicker
{
    internal static class DrawUtility
	{
		public static int lineNumber; //Handles row highlighting and also dynamic window size for the scroll bar
		static int cellPosition; //Tracks the vertical placement in pixels
		public const int lineHeight = 22; //Text.LineHeight + options.verticalSpacing;
		public static List<FloatMenuOption> cachedMenu; //Stores the filter drop-down menu, clears when you leave the mod options
		public static Type filteredType;
		
		public static void DrawList(Rect container, Listing_Standard options)
		{
			lineNumber = cellPosition = 0; //Reset
			//List out all the unremoved defs from the compiled database
			for (int i = 0; i < allDefs.Length; i++)
			{
				Def def = allDefs[i];
				if (def != null && 
				(!filtered || (searchStringCache.TryGetValue(def, out string label) && label.Contains(filter) )) &&
				(filteredType == null || filteredType == def.GetType()) )
				{
					cellPosition += lineHeight;
					++lineNumber;
					
					if (cellPosition > scrollPos.y - container.height && cellPosition < scrollPos.y + container.height) DrawListItem(options, def);
				}
				
			}
		}
		public static void DrawListItem(Listing_Standard options, Def def)
		{
			//Prepare key
			string key = def.ToKey();
			string type = key.ToTypeString();

			//Determine checkbox status...
			bool checkOn = !actualRemovedDefs?.Contains(key) ?? false;
			
			//Fetch bounding rect
			Rect rect = options.GetRect(lineHeight);
			rect.y = cellPosition;

			//Label
			string dataString = type + " :: " + def.modContentPack?.Name + " :: " + def.defName;

			//Actually draw the line item
			if (options.BoundingRectCached == null || rect.Overlaps(options.BoundingRectCached.Value))
			{
				CheckboxLabeled(rect, dataString, def.label, ref checkOn, def);
			}

			//Handle row coloring and spacing
			options.Gap(options.verticalSpacing);
			if (lineNumber % 2 != 0) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);

			//Tooltip
			//TooltipHandler.TipRegion(rect, dataString + "\n\n" + (type == nameof(DefList) ? string.Join(def.description + "\n", ((DefList)def).defs) : def.description));
			
			//Add to working list if missing
			bool flag = false;
			if (!checkOn && (flag = !actualRemovedDefs.Contains(key))) actualRemovedDefs.Add(key);
			//Remove from working list
			else if (checkOn && (flag = actualRemovedDefs.Contains(key))) actualRemovedDefs.Remove(key);
			//Immediately process the list if this is a deflist
			if (flag && type == nameof(DefList)) LoadedModManager.GetMod<Mod_CherryPicker>().WriteSettings();
		}
		static void CheckboxLabeled(Rect rect, string data, string label, ref bool checkOn, Def def)
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
		public static List<FloatMenuOption> FloatMenu()
		{
			if (cachedMenu != null) return cachedMenu;
			cachedMenu = new List<FloatMenuOption>();

			HashSet<string> isDistinct = new HashSet<string>();
			for (int i = 0; i < allDefs.Length; i++)
			{
				var type = allDefs[i].GetType();
				var nameSpace = type.Namespace;
				var label = type.Name;
				if (nameSpace != "RimWorld" && nameSpace != "Verse" && !DefUtility.typeCache.ContainsKey(label)) continue;
				if (isDistinct.Add(label))
				{
					cachedMenu.Add(new FloatMenuOption(label, delegate()
					{
						ApplyCategoryFilter(label);
					}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
			}
			cachedMenu.SortBy(x => x.labelInt);
			cachedMenu.Insert(0, new FloatMenuOption("CherryPicker.AllDefTypes".Translate(), delegate()
				{
					ApplyCategoryFilter("AllDefTypes");
				}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));

			return cachedMenu;
		}
		static void ApplyCategoryFilter(string test)
		{
			if (test == "AllDefTypes") 
			{
				filteredType = null;
				return;
			}
			filteredType = DefUtility.ToType(test, true);
		}
	}
}
