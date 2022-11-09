using Verse;
using System;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using static CherryPicker.CherryPickerUtility;
 
namespace CherryPicker
{
	[StaticConstructorOnStartup]
    internal static class DefUtility
	{
		public static Assembly rootAssembly;
		public static Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

		static DefUtility()
		{
			rootAssembly = typeof(ThingDef).Assembly;
			typeCache.Add("DefList", typeof(DefList));
		}

		public static string ToKey(this Def def)
		{
			return def == null ? "" : (def.GetType().Name + "/" + def.defName);
		}
		public static string ToDefName(this string def)
		{
			return def.Split('/')[1];
		}
		public static Type ToType(this string key)
		{
			string typeName = key.Split('/')[0];

			//Fast handling for vanilla types
			Type type;
			type = rootAssembly.GetType("Verse." + typeName) ?? rootAssembly.GetType("RimWorld." + typeName);
			if (type != null) return type;
			
			//Check the cache for modded types
			if (typeCache.TryGetValue(typeName, out type))
			{
				return type;
			}
			return null;
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
			if (defName.NullOrEmpty() || type == null) return null;

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
