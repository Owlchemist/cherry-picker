using Verse;
using System;
using RimWorld;
using static CherryPicker.CherryPickerUtility;
 
namespace CherryPicker
{
	[StaticConstructorOnStartup]
    internal static class DefUtility
	{
		public static System.Reflection.Assembly rootAssembly;
		public static System.Reflection.Assembly thisAssembly;

		static DefUtility()
		{
			rootAssembly = typeof(ThingDef).Assembly;
			thisAssembly = typeof(DefList).Assembly;
		}

		public static string ToKey(this Def def)
		{
			return def == null ? "" : (def.GetType().Name + "/" + def.defName);
		}
		public static string ToDefName(this string def)
		{
			return def.Split('/')[1];
		}
		public static Type ToType(this string def)
		{
			return rootAssembly.GetType("Verse." + def.Split('/')[0]) ?? rootAssembly.GetType("RimWorld." + def.Split('/')[0]) ?? thisAssembly.GetType("CherryPicker." + def.Split('/')[0]);
		}
		public static string ToTypeString(this string def)
		{
			return def.Split('/')[0];
		}
		public static Def ToDef(this string key)
		{
			return GetDef(key.ToDefName(), key.ToType());
		}
		public static Def GetDef(string defName, Type type)
		{
			if (defName.NullOrEmpty() || type == null || type == typeof(Backstory)) return null;

			Def def = (Def)GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), type, nameof(DefDatabase<Def>.GetNamed), defName, false);
			if (def == null)
			{
				foreach (Def hardRemovedDef in processedDefs)
				{
					if (hardRemovedDef?.defName == defName && hardRemovedDef.GetType() == type) return hardRemovedDef;
				}
			}
			
			return def;
		}
	}
}
