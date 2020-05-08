using System;
using System.Linq.Expressions;
using System.Reflection;



namespace ScienceChecklist
{
	/// <summary>
	/// Class to access the DMagic API via reflection so we don't have to recompile
	/// when the DMagic mod updates. If the DMagic API changes, we will need to modify this code.
	/// </summary>
	public static class DMagic
	{
		public static bool Installed { get; private set; }

		public static Type ScienceAnimateType { get; private set; }
		public static Type ScienceAnimateGenericType { get; private set; }

		public static API ScienceAnimate { get; private set; }
		public static API ScienceAnimateGeneric { get; private set; }

		public static void Init()
		{
			var logger = new Logger("DMagic");

			foreach (var asm in AssemblyLoader.loadedAssemblies)
			{
				switch (asm.name)
				{
				// DMagic's Orbital Science (Magnetometer etc.)
				case "DMagic":
					try
					{
						ScienceAnimateType =
							(ScienceAnimate =
							new API(asm.typesDictionary
							[typeof(PartModule)]
							["DMModuleScienceAnimate"])
							).type;
						Installed = true;
					}
					catch (Exception ex)
					{
						logger.LogError("Could not reflect DMagic's DMModuleScienceAnimate: " + ex.ToString());
					}
					break;
				// DMagic's generic module used e.g. by ReStock+ (if both installed)
				case "DMModuleScienceAnimateGeneric":
					try
					{
						ScienceAnimateGenericType = 
							(ScienceAnimateGeneric =
							new API(asm.typesDictionary
							[typeof(PartModule)]
							["DMModuleScienceAnimateGeneric"])
							).type;
						Installed = true;
					}
					catch (Exception ex)
					{
						logger.LogError("Could not reflect DMagic's DMModuleScienceAnimateGeneric: " + ex.ToString());
					}
					break;
				}
			}
		}

		public static bool IsScienceAnimate(ModuleScienceExperiment module)
			=> ScienceAnimateType?.IsAssignableFrom(module.GetType()) ?? false;
		public static bool IsScienceAnimateGeneric(ModuleScienceExperiment module)
			=> ScienceAnimateGenericType?.IsAssignableFrom(module.GetType()) ?? false;

		public class API
		{
			readonly Logger _logger;

			public readonly Type type;
			readonly Func<ModuleScienceExperiment, bool> _canConduct;
			readonly Action<ModuleScienceExperiment, bool> _gatherScienceData;
			readonly Func<ModuleScienceExperiment, ExperimentSituations> _getSituation;
			readonly Func<ModuleScienceExperiment, ExperimentSituations, string> _getBiome;

			internal API(Type type)
			{
				_logger = new Logger(this);
				this.type = type;

				const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
				var module = Expression.Parameter(
					typeof(ModuleScienceExperiment), "module");
				try
				{
					_canConduct = Expression.Lambda<Func<ModuleScienceExperiment, bool>>(
						Expression.Call(Expression.Convert(module, type),
						type.GetMethod("canConduct", flags, null, new Type[0], null)),
						module).Compile();
				}
				catch
				{
					_logger.Error("DMModuleScienceAnimateGeneric.canConduct method signature has changed. [x] Science will not work for DMModuleScienceAnimateGeneric experiments");
					throw;
				}

				try
				{
					var silent = Expression.Parameter(
					typeof(bool), "silent");
					_gatherScienceData = Expression.Lambda<Action<ModuleScienceExperiment, bool>>(
						Expression.Call(Expression.Convert(module, type),
						type.GetMethod("gatherScienceData", flags, null, new Type[] { typeof(bool) }, null),
						silent), module, silent).Compile();
				}
				catch
				{
					_logger.Error("DMModuleScienceAnimateGeneric.gatherScienceData method signature has changed. [x] Science will not work for DMModuleScienceAnimateGeneric experiments");
					throw;
				}

				try
				{
					_getSituation = Expression.Lambda<Func<ModuleScienceExperiment, ExperimentSituations>>(
						Expression.Call(Expression.Convert(module, type),
						type.GetMethod("getSituation", flags, null, new Type[0], null)),
						module).Compile();
				}
				catch
				{
					_logger.Error("DMModuleScienceAnimateGeneric.getSituation method signature has changed. [x] Science will not work for DMModuleScienceAnimateGeneric experiments");
					throw;
				}

				try
				{
					var situation = Expression.Parameter(
					typeof(ExperimentSituations), "situation");
					_getBiome = Expression.Lambda<Func<ModuleScienceExperiment, ExperimentSituations, string>>(
						Expression.Call(Expression.Convert(module, type),
						type.GetMethod("getBiome", flags, null, new Type[] { typeof(ExperimentSituations) }, null),
						situation), module, situation).Compile();
				}
				catch
				{
					_logger.Error("DMModuleScienceAnimateGeneric.getSituation method signature has changed. [x] Science may not work for DMModuleScienceAnimateGeneric experiments");
					throw;
				}
			}

			public bool canConduct(ModuleScienceExperiment mse, bool runSingleUse = true)
				=> _canConduct(mse) && (runSingleUse || mse.rerunnable
					|| (int)mse.Fields.GetValue("experimentsLimit") > 1);

			public void gatherScienceData(ModuleScienceExperiment mse, bool HideResultsWindow)
				=> _gatherScienceData(mse, HideResultsWindow);

			public ExperimentSituations getSituation(ModuleScienceExperiment mse)
				=> _getSituation(mse);

			public string getBiome(ModuleScienceExperiment mse, ExperimentSituations sit)
				=> _getBiome(mse, sit);
		}
	}
}