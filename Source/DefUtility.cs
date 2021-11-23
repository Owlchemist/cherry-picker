using Verse;
using System;
using static CherryPicker.CherryPickerUtility;
 
namespace CherryPicker
{
    internal static class DefUtility
	{
		public static System.Reflection.Assembly rootAssembly;

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
			return rootAssembly.GetType("Verse." + def.Split('/')[0]) ?? rootAssembly.GetType("RimWorld." + def.Split('/')[0]);
		}
		public static Def GetDef(string key)
		{
			return GetDef(GetDefName(key), GetDefType(key));
		}
		public static Def GetDef(string defName, Type type)
		{
			if (defName.NullOrEmpty() || type == null) return null;	
			
			Def def = (Def)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, nameof(DefDatabase<Def>.GetNamed), defName, false);
			if (def == null)
			{
				foreach (Def hardRemovedDef in hardRemovedDefs)
				{
					if (hardRemovedDef.defName == defName && hardRemovedDef.GetType().Name == type?.Name) return hardRemovedDef;
				}
			}
			
			return def;
		}
	}
}
