using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityScanner.Utilities.Addressables
{
    public static class USAddressablesReflection
    {
        private static readonly Dictionary<string, bool> AddressablesByGuidCache = new Dictionary<string, bool>();
        private static bool _reflectionInitialized;
        private static bool _reflectionAvailable;
        private static bool _warningLogged;
        private static PropertyInfo _settingsProperty;
        private static MethodInfo _findAssetEntryMethod;
        private static int _findAssetEntryParamCount;
        private static readonly object[] SingleArg = new object[1];
        private static readonly object[] DoubleArg = new object[2];

        public static void ClearCache()
        {
            AddressablesByGuidCache.Clear();
        }

        public static bool IsAssetAddressable(string assetPath, bool useReflection = true)
        {
            if (!useReflection || string.IsNullOrEmpty(assetPath))
                return false;

            try
            {
                var guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                    return false;

                if (AddressablesByGuidCache.TryGetValue(guid, out var cached))
                    return cached;

                var result = IsGuidAddressable(guid);
                AddressablesByGuidCache[guid] = result;
                return result;
            }
            catch (Exception e)
            {
                LogReflectionWarning($"checking asset {assetPath}", e);
                return false;
            }
        }

        private static bool IsGuidAddressable(string guid)
        {
            EnsureReflectionInitialized();
            if (!_reflectionAvailable)
                return false;

            try
            {
                var settings = _settingsProperty.GetValue(null, null);
                if (settings == null)
                    return false;

                object entry;
                if (_findAssetEntryParamCount == 1)
                {
                    SingleArg[0] = guid;
                    entry = _findAssetEntryMethod.Invoke(settings, SingleArg);
                    SingleArg[0] = null;
                }
                else
                {
                    DoubleArg[0] = guid;
                    DoubleArg[1] = true;
                    entry = _findAssetEntryMethod.Invoke(settings, DoubleArg);
                    DoubleArg[0] = null;
                    DoubleArg[1] = null;
                }

                return entry != null;
            }
            catch (Exception e)
            {
                _reflectionAvailable = false;
                LogReflectionWarning($"checking guid {guid}", e);
                return false;
            }
        }

        private static void EnsureReflectionInitialized()
        {
            if (_reflectionInitialized)
                return;

            _reflectionInitialized = true;

            try
            {
                Type defaultObjectType = null;
                Type settingsType = null;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                foreach (var assembly in assemblies)
                {
                    defaultObjectType ??= assembly.GetType(
                        "UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject",
                        false);
                    defaultObjectType ??= assembly.GetType(
                        "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject",
                        false);

                    settingsType ??= assembly.GetType(
                        "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings",
                        false);
                    settingsType ??= assembly.GetType(
                        "UnityEditor.AddressableAssets.AddressableAssetSettings",
                        false);

                    if (defaultObjectType != null && settingsType != null)
                        break;
                }

                if (defaultObjectType == null || settingsType == null)
                    return;

                _settingsProperty = defaultObjectType.GetProperty(
                    "Settings",
                    BindingFlags.Public | BindingFlags.Static);
                _findAssetEntryMethod =
                    settingsType.GetMethod("FindAssetEntry", new[] { typeof(string) }) ??
                    settingsType.GetMethod("FindAssetEntry", new[] { typeof(string), typeof(bool) });
                _findAssetEntryParamCount = _findAssetEntryMethod?.GetParameters().Length ?? 0;

                _reflectionAvailable =
                    _settingsProperty != null && _findAssetEntryMethod != null;
            }
            catch (Exception e)
            {
                _reflectionAvailable = false;
                LogReflectionWarning("initializing Addressables reflection", e);
            }
        }

        private static void LogReflectionWarning(string context, Exception exception)
        {
            if (_warningLogged)
                return;

            _warningLogged = true;
            Debug.LogWarning($"Failed to detect Addressables via reflection while {context}: {exception}");
        }
    }
}
