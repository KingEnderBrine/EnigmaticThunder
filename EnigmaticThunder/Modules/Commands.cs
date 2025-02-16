﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using RoR2;
using RoR2.ConVar;

namespace EnigmaticThunder.Modules
{
    /// <summary>
    /// A module for scanning static methods of a given assembly
    /// so that they are registered as console commands for the in-game console.
    /// </summary>
    public class CommandHelper : Util.Module
    {
        private static readonly Queue<Assembly> Assemblies = new Queue<Assembly>();
        private static RoR2.Console _console;

        /// <summary>
        /// Scans the calling assembly for ConCommand attributes and Convar fields and adds these to the console.
        /// This method may be called at any time.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public static void AddToConsoleWhenReady()
        {

            var assembly = Assembly.GetCallingAssembly();
            Assemblies.Enqueue(assembly);
            HandleCommandsConvars();
        }

        internal override void Load()
        {
            On.RoR2.Console.InitConVars += ConsoleReady;
        }


        private static void ConsoleReady(On.RoR2.Console.orig_InitConVars orig, RoR2.Console self)
        {
            orig(self);

            _console = self;
            HandleCommandsConvars();
        }

        private static void HandleCommandsConvars()
        {
            if (_console == null)
            {
                return;
            }

            while (Assemblies.Count > 0)
            {
                var assembly = Assemblies.Dequeue();
                RegisterCommands(assembly);
                RegisterConVars(assembly);
            }
        }

        private static void RegisterCommands(Assembly assembly)
        {
            var types = assembly?.GetTypes();
            if (types == null)
            {
                return;
            }

            try
            {
                var catalog = _console.concommandCatalog;
                const BindingFlags consoleCommandsMethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var methods = types.SelectMany(t =>
                    t.GetMethods(consoleCommandsMethodFlags).Where(m => m.GetCustomAttribute<ConCommandAttribute>() != null));

                foreach (var methodInfo in methods)
                {
                    if (!methodInfo.IsStatic)
                    {
                        LogCore.LogE($"ConCommand defined as {methodInfo.Name} in {assembly.FullName} could not be registered. " +
                                              "ConCommands must be static methods.");
                        continue;
                    }

                    var attributes = methodInfo.GetCustomAttributes<ConCommandAttribute>();
                    foreach (var attribute in attributes)
                    {
                        var conCommand = new RoR2.Console.ConCommand
                        {
                            flags = attribute.flags,
                            helpText = attribute.helpText,
                            action = (RoR2.Console.ConCommandDelegate)Delegate.CreateDelegate(
                                typeof(RoR2.Console.ConCommandDelegate), methodInfo)
                        };

                        catalog[attribute.commandName.ToLower()] = conCommand;
                    }
                }
            }
            catch (Exception e)
            {
                LogCore.LogE($"{nameof(CommandHelper)} failed to scan the assembly called {assembly.FullName}. Exception : {e}");
            }
        }

        private static void RegisterConVars(Assembly assembly)
        {
            try
            {
                var customVars = new List<BaseConVar>();
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (field.FieldType.IsSubclassOf(typeof(BaseConVar)))
                        {
                            if (field.IsStatic)
                            {
                                _console.RegisterConVarInternal((BaseConVar)field.GetValue(null));
                                customVars.Add((BaseConVar)field.GetValue(null));
                            }
                            else if (type.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
                            {
                                LogCore.LogE(
                                    $"ConVar defined as {type.Name} in {assembly.FullName}. {field.Name} could not be registered. ConVars must be static fields.");
                            }
                        }
                    }
                }
                foreach (var baseConVar in customVars)
                {
                    if ((baseConVar.flags & ConVarFlags.Engine) != ConVarFlags.None)
                    {
                        baseConVar.defaultValue = baseConVar.GetString();
                    }
                    else if (baseConVar.defaultValue != null)
                    {
                        baseConVar.SetString(baseConVar.defaultValue);
                    }
                }
            }
            catch (Exception e)
            {
                LogCore.LogE($"{nameof(CommandHelper)} failed to scan the assembly called {assembly.FullName}. Exception : {e}");
            }
        }
    }
}
