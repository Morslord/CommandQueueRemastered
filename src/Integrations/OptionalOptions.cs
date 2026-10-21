using System;
using System.Linq;
using CommandQueueRemastered.Game;
using HarmonyLib;
using System.Reflection;
using BepInEx.Configuration;
using CommandQueueRemastered.Core;
using UnityEngine;

namespace CommandQueueRemastered.Integrations
{
    internal static class OptionalOptions
    {
        public static void Register(RemasteredPlugin plugin, Harmony harmony)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate => candidate.GetType("RiskOfOptions.ModSettingsManager") != null);
            if (assembly == null) { plugin.LogInfo("RiskOfOptions not installed; using BepInEx configuration."); return; }
            foreach (var entry in plugin.GroupOptions.Values)
                RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", entry);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.RotateRepeatedItems);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.LimiterCountMode);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.BlockGameplayInput);
            RegisterEntry(plugin, assembly, "SliderOption", "SliderConfig", plugin.MenuScale);
            RegisterEntry(plugin, assembly, "SliderOption", "SliderConfig", plugin.ItemScale);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.TemporaryItemBehavior);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.EtherealItemBehavior);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.IgnoreTemporaryItems);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.IgnoreEtherealItems);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.ExclusiveItemSelection);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.AutoCollectQueuedItems);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.AutoPickupCommandEssence);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.ExcludePausedPlayers);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.DistributionEnabled);
            RegisterStageSelector(plugin, assembly);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.DistributeChests);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.DistributeExchangePools);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.DistributeTeleporterRewards);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.DrifterSalvage);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.HexGroupsOfSeven);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.FewestItemsCount);
            foreach (var entry in plugin.EssenceDropModes.OrderBy(pair => Array.IndexOf(ItemGroups.Names, ItemGroups.Group(pair.Key))).Select(pair => pair.Value))
                RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", entry);
            RegisterIcon(plugin, assembly);
            OptionalCategoryPaging.Install(harmony, assembly, plugin);
        }
        private static void RegisterStageSelector(RemasteredPlugin plugin, Assembly assembly)
        {
            try
            {
                var option = Activator.CreateInstance(assembly.GetType("RiskOfOptions.Options.GenericButtonOption", true),
                    "ExcludedStages", "Item Distribution Rules", StageExclusionDialog.Description(), "Select stages…",
                    new UnityEngine.Events.UnityAction(() => StageExclusionDialog.Open(plugin)));
                var add = assembly.GetType("RiskOfOptions.ModSettingsManager", true).GetMethod("AddOption",
                    new[] { assembly.GetType("RiskOfOptions.Options.BaseOption", true), typeof(string), typeof(string) });
                add.Invoke(null, new[] { option, RemasteredPlugin.Guid, RemasteredPlugin.Name });
            }
            catch (Exception exception)
            {
                plugin.LogError("Stage selection button unavailable; retaining text input", exception);
                RegisterEntry(plugin, assembly, "StringInputFieldOption", "InputFieldConfig", plugin.ExcludedDistributionStages);
            }
        }
        private static void RegisterEntry(RemasteredPlugin plugin, Assembly assembly, string optionName, string configName, ConfigEntryBase entry)
        {
            try
            {
                Type configurationType = assembly.GetType("RiskOfOptions.OptionConfigs." + configName, true);
                object configuration = Activator.CreateInstance(configurationType);
                string displayName = OptionsPresentation.Name(entry.Definition.Section, entry.Definition.Key);
                Set(configuration, "name", displayName);
                Set(configuration, "category", OptionsPresentation.Category(entry.Definition.Section));
                Set(configuration, "description", entry.Description.Description);
                Set(configuration, "restartRequired", false);
                if (entry == plugin.ItemScale) { Set(configuration, "min", 0.5f); Set(configuration, "max", 2f); Set(configuration, "formatString", "{0:0.00}x"); }
                if (entry == plugin.MenuScale) { Set(configuration, "min", 0.5f); Set(configuration, "max", 2f); Set(configuration, "formatString", "{0:0.00}x"); }
                var option = Activator.CreateInstance(assembly.GetType("RiskOfOptions.Options." + optionName, true), entry, configuration);
                var manager = assembly.GetType("RiskOfOptions.ModSettingsManager", true);
                var add = manager.GetMethod("AddOption", new[] { assembly.GetType("RiskOfOptions.Options.BaseOption", true), typeof(string), typeof(string) });
                if (add == null) throw new MissingMethodException(manager.FullName, "AddOption");
                add.Invoke(null, new[] { option, RemasteredPlugin.Guid, RemasteredPlugin.Name });
                plugin.LogInfo("RiskOfOptions registered " + entry.Definition.Key + ".");
            }
            catch (Exception exception) { plugin.LogError("RiskOfOptions registration failed for " + entry.Definition.Key, exception); }
        }
        private static void Set(object configuration, string name, object value)
        {
            var field = configuration.GetType().GetField(name);
            if (field == null) throw new MissingFieldException(configuration.GetType().FullName, name);
            field.SetValue(configuration, value);
        }
        private static void RegisterIcon(RemasteredPlugin plugin, Assembly assembly)
        {
            try
            {
                using (var resource = typeof(RemasteredPlugin).Assembly.GetManifestResourceStream("CommandQueueRemastered.icon.png"))
                {
                    if (resource == null) { plugin.LogWarning("Embedded mod icon is missing."); return; }
                    using (var buffer = new System.IO.MemoryStream())
                    {
                        resource.CopyTo(buffer);
                        plugin.ModIconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(plugin.ModIconTexture, buffer.ToArray())) throw new System.IO.InvalidDataException("Invalid mod icon PNG.");
                        plugin.ModIcon = Sprite.Create(plugin.ModIconTexture, new Rect(0, 0, plugin.ModIconTexture.width, plugin.ModIconTexture.height), new Vector2(0.5f, 0.5f));
                        var manager = assembly.GetType("RiskOfOptions.ModSettingsManager", true);
                        var method = manager.GetMethod("SetModIcon", new[] { typeof(Sprite), typeof(string), typeof(string) });
                        if (method == null) throw new MissingMethodException(manager.FullName, "SetModIcon");
                        method.Invoke(null, new object[] { plugin.ModIcon, RemasteredPlugin.Guid, RemasteredPlugin.Name });
                        plugin.LogInfo("RiskOfOptions mod icon registered.");
                    }
                }
            }
            catch (Exception exception) { plugin.LogError("RiskOfOptions mod icon registration failed", exception); }
        }
    }
}

