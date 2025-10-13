using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;

namespace SilksongNeuralNetwork
{
    public class FunctionLogger
    {
        private readonly Harmony _harmony;
        private readonly string _logPath;
        private static ManualLogSource Logger;
        private static HashSet<string> _ignoredNamespaces = new HashSet<string>
        {
            "System",
            "Microsoft",
            "Unity",
            "TMPro",
            "Mono"
        };

        public FunctionLogger(string harmonyId, string logPath, ManualLogSource logger)
        {
            _harmony = new Harmony(harmonyId);
            _logPath = logPath;
            Logger = logger;
        }

        public void StartLogging()
        {
            // Only patch assemblies relevant to the game
            var gameAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name.StartsWith("TeamCherry."))
                .ToList();

            foreach (var assembly in gameAssemblies)
            {
                try
                {
                    PatchAssembly(assembly);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to patch assembly {assembly.FullName}: {ex.Message}");
                }

            }
        }

        private void PatchAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (ShouldIgnoreType(type)) continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                     BindingFlags.Instance | BindingFlags.Static))
                {
                    if (ShouldIgnoreMethod(method)) continue;

                    try
                    {
                        var prefix = new HarmonyMethod(typeof(FunctionLogger),
                                                     nameof(FunctionCallPrefix));
                        _harmony.Patch(method, prefix: prefix);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Failed to patch method {method.Name} in {type.FullName}: {ex.Message}");
                    }
                }
            }
        }

        private bool ShouldIgnoreType(Type type)
        {
            return type.IsInterface || type.IsAbstract ||
                   _ignoredNamespaces.Any(ns => type.FullName?.StartsWith(ns) ?? false);
        }

        private bool ShouldIgnoreMethod(MethodInfo method)
        {
            return method.IsAbstract || method.IsGenericMethod ||
                   method.GetCustomAttributes(typeof(HarmonyPatch), false).Any();
        }

        public static bool FunctionCallPrefix(MethodBase __originalMethod)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var className = __originalMethod.DeclaringType?.FullName ?? "Unknown";
            var methodName = __originalMethod.Name;

            Logger.LogInfo($"[{timestamp}] Called: {className}.{methodName}");
            return true; // true означає, що оригінальний метод буде виконано
        }

        public void StopLogging()
        {
            _harmony.UnpatchSelf();
        }
    }
}