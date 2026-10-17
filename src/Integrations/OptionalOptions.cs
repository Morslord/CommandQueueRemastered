using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;

namespace CommandQueueRemastered.Integrations
{
    internal static class OptionalOptions
    {
        public static void Register(RemasteredPlugin plugin)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate => candidate.GetType("RiskOfOptions.ModSettingsManager") != null);
            if (assembly == null) { plugin.LogInfo("RiskOfOptions not installed; using BepInEx configuration."); return; }
            foreach (var entry in plugin.GroupOptions.Values)
                RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", entry);
            RegisterEntry(plugin, assembly, "CheckBoxOption", "CheckBoxConfig", plugin.RotateRepeatedItems);
            RegisterEntry(plugin, assembly, "SliderOption", "SliderConfig", plugin.MenuScale);
            RegisterEntry(plugin, assembly, "SliderOption", "SliderConfig", plugin.ItemScale);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.TemporaryItemBehavior);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.EtherealItemBehavior);
            RegisterEntry(plugin, assembly, "ChoiceOption", "ChoiceConfig", plugin.ExclusiveItemSelection);
            RegisterIcon(plugin, assembly);
        }
        private static void RegisterEntry(RemasteredPlugin plugin, Assembly assembly, string optionName, string configName, ConfigEntryBase entry)
        {
            try
            {
                Type configurationType = assembly.GetType("RiskOfOptions.OptionConfigs." + configName, true);
                object configuration = Activator.CreateInstance(configurationType);
                Set(configuration, "name", entry.Definition.Key);
                Set(configuration, "category", entry.Definition.Section);
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

