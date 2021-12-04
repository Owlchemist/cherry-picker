using Verse;
using RimWorld;
using UnityEngine;
using Verse.Sound;
using System.Linq;
using static CherryPicker.ModSettings_CherryPicker;
using static CherryPicker.CherryPickerUtility;
 
namespace CherryPicker
{
    internal static class DrawUtility
	{
		public static int lineNumber; //Handles row highlighting and also dynamic window size for the scroll bar
		static int cellPosition; //Tracks the vertical pacement in pixels
		public const int lineHeight = 22; //Text.LineHeight + options.verticalSpacing;
		
		public static void DrawList(Rect container, Listing_Standard options)
		{
			lineNumber = cellPosition = 0; //Reset
			//List out all the unremoved defs from the compiled database
			foreach (Def def in allDefs)
			{
				if (def != null && (!filtered || (searchStringCache.TryGetValue(def)?.Contains(filter) ?? false)))
				{
					cellPosition += lineHeight;
					++lineNumber;
					
					if (cellPosition > scrollPos.y - container.height && cellPosition < scrollPos.y + container.height) DrawListItem(options, def);
				}
				
			}
			foreach (var backstory in removedBackstories.Concat(BackstoryDatabase.allBackstories.Values))
			{
				if (!filtered || ("backstory" + backstory.title).Contains(filter))
				{
					cellPosition += lineHeight;
					++lineNumber;
					
					if (cellPosition > scrollPos.y - container.height && cellPosition < scrollPos.y + container.height) DrawBSListItem(options, backstory);
				}
			}
		}

		public static void DrawListItem(Listing_Standard options, Def def)
		{
			//Prepare key
			string key = def.ToKey();

			//Determine checkbox status...
			bool checkOn = !workingList?.Contains(key) ?? false;
			//Draw...
			Rect rect = options.GetRect(lineHeight);
			rect.y = cellPosition;

			//Label
			string dataString = def.GetType().Name + " :: " + def.modContentPack?.Name + " :: " + def.defName;

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
			TooltipHandler.TipRegion(rect, dataString + "\n\n" + def.description);
			
			//Add to working list if missing
			if (!checkOn && !workingList.Contains(key)) workingList.Add(key);
			//Remove from working list
			else if (checkOn && workingList.Contains(key)) workingList.Remove(key);
		}

		//Todo: refactor DrawListItem and DrawBSListItem to combine more elegantly
		public static void DrawBSListItem(Listing_Standard options, Backstory def)
		{
			//Prepare key
			string key = "Backstory/" + def.identifier;

			//Determine checkbox status...
			bool checkOn = !workingList?.Contains(key) ?? false;
			//Draw...
			Rect rect = options.GetRect(lineHeight);
			rect.y = cellPosition;

			//Label
			string dataString = "Backstory :: " + def.identifier;

			//Actually draw the line item
			if (options.BoundingRectCached == null || rect.Overlaps(options.BoundingRectCached.Value))
			{
				CheckboxLabeled(rect, dataString, def.title, ref checkOn, null);
			}

			//Handle row coloring and spacing
			options.Gap(options.verticalSpacing);
			if (lineNumber % 2 != 0) Widgets.DrawLightHighlight(rect);
			Widgets.DrawHighlightIfMouseover(rect);

			//Tooltip
			TooltipHandler.TipRegion(rect, dataString + "\n\n" + def.descTranslated);
			
			//Add to working list if missing
			if (!checkOn && !workingList.Contains(key)) workingList.Add(key);
			//Remove from working list
			else if (checkOn && workingList.Contains(key)) workingList.Remove(key);
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
	}
}
