﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityVersion = AssetRipper.VersionUtilities.UnityVersion;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MelonLoader.InternalUtils
{
    public static class UnityInformationHandler
    {
        private static readonly Regex UnityVersionRegex = new Regex(@"^[0-9]+\.[0-9]+\.[0-9]+[abcfx][0-9]+$");

        public static string GameName { get; private set; } = "UNKNOWN";
        public static string GameDeveloper { get; private set; } = "UNKNOWN";
        public static UnityVersion EngineVersion { get; private set; } = UnityVersion.MinVersion;
        public static string GameVersion { get; private set; } = "0";

        internal static void Setup()
        {
            string gameDataPath = MelonUtils.GetGameDataDirectory();

            if (!string.IsNullOrEmpty(MelonLaunchOptions.Core.UnityVersion))
            {
                try { EngineVersion = UnityVersion.Parse(MelonLaunchOptions.Core.UnityVersion); }
                catch (Exception ex)
                {
                    if (MelonDebug.IsEnabled())
                        MelonLogger.Error(ex);
                }
            }

            if (EngineVersion == UnityVersion.MinVersion)
            {
                AssetsManager assetsManager = new AssetsManager();
                ReadGameInfo(assetsManager, gameDataPath);
                assetsManager.UnloadAll();

                if (EngineVersion == UnityVersion.MinVersion)
                {
                    try { EngineVersion = ReadVersionFallback(gameDataPath); }
                    catch (Exception ex)
                    {
                        if (MelonDebug.IsEnabled())
                            MelonLogger.Error(ex);
                    }
                }
            }

            SetDefaultConsoleTitleWithGameName(GameName, GameVersion);

            MelonLogger.Msg("------------------------------");
            MelonLogger.Msg($"Game Name: {GameName}");
            MelonLogger.Msg($"Game Developer: {GameDeveloper}");
            MelonLogger.Msg($"Unity Version: {EngineVersion}");
            MelonLogger.Msg($"Game Version: {GameVersion}");
            MelonLogger.Msg("------------------------------");
        }

        private static void ReadGameInfo(AssetsManager assetsManager, string gameDataPath)
        {
            AssetsFileInstance instance = null;
            try
            {
                string bundlePath = Path.Combine(gameDataPath, "globalgamemanagers");
                if (!File.Exists(bundlePath))
                    bundlePath = Path.Combine(gameDataPath, "mainData");

                if (!File.Exists(bundlePath))
                {
                    bundlePath = Path.Combine(gameDataPath, "data.unity3d");
                    if (!File.Exists(bundlePath))
                        return;

                    BundleFileInstance bundleFile = assetsManager.LoadBundleFile(bundlePath);
                    instance = assetsManager.LoadAssetsFileFromBundle(bundleFile, "globalgamemanagers");
                }
                else
                    instance = assetsManager.LoadAssetsFile(bundlePath, true);
                if (instance == null)
                    return;

                assetsManager.LoadIncludedClassPackage();
                if (!instance.file.typeTree.hasTypeTree)
                    assetsManager.LoadClassDatabaseFromPackage(instance.file.typeTree.unityVersion);

                EngineVersion = UnityVersion.Parse(instance.file.typeTree.unityVersion);

                List<AssetFileInfoEx> assetFiles = instance.table.GetAssetsOfType(129);
                if (assetFiles.Count > 0)
                {
                    AssetFileInfoEx playerSettings = assetFiles.First();

                    AssetTypeInstance assetTypeInstance = null;
                    try
                    {
                        assetTypeInstance = assetsManager.GetTypeInstance(instance, playerSettings);
                    }
                    catch (Exception ex)
                    {
                        if (MelonDebug.IsEnabled())
                        {
                            MelonLogger.Error(ex);
                            MelonLogger.Warning("Attempting to use Large Class Package...");
                        }
                        assetsManager.LoadIncludedLargeClassPackage();
                        assetsManager.LoadClassDatabaseFromPackage(instance.file.typeTree.unityVersion);
                        assetTypeInstance = assetsManager.GetTypeInstance(instance, playerSettings);
                    }

                    if (assetTypeInstance != null)
                    {
                        AssetTypeValueField playerSettings_baseField = assetTypeInstance.GetBaseField();

                        AssetTypeValueField bundleVersion = playerSettings_baseField.Get("bundleVersion");
                        if (bundleVersion != null)
                            GameVersion = bundleVersion.GetValue().AsString();

                        AssetTypeValueField companyName = playerSettings_baseField.Get("companyName");
                        if (companyName != null)
                            GameDeveloper = companyName.GetValue().AsString();

                        AssetTypeValueField productName = playerSettings_baseField.Get("productName");
                        if (productName != null)
                            GameName = productName.GetValue().AsString();
                    }
                }
            }
            catch(Exception ex)
            {
                if (MelonDebug.IsEnabled())
                    MelonLogger.Error(ex);
                //MelonLogger.Error("Failed to Initialize Assets Manager!");
            }
            if (instance != null)
                instance.file.Close();
        }

        private static UnityVersion ReadVersionFallback(string gameDataPath)
        {
            string unityPlayerPath = Path.Combine(MelonUtils.GameDirectory, "UnityPlayer.dll");
            if (!File.Exists(unityPlayerPath))
                unityPlayerPath = MelonUtils.GetApplicationPath();

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var unityVer = FileVersionInfo.GetVersionInfo(unityPlayerPath);
                return new UnityVersion((ushort)unityVer.FileMajorPart, (ushort)unityVer.FileMinorPart, (ushort)unityVer.FileBuildPart);
            }

            var globalgamemanagersPath = Path.Combine(gameDataPath, "globalgamemanagers");
            if (File.Exists(globalgamemanagersPath))
                return GetVersionFromGlobalGameManagers(File.ReadAllBytes(globalgamemanagersPath));

            var dataPath = Path.Combine(gameDataPath, "data.unity3d");
            if (File.Exists(dataPath))
                return GetVersionFromDataUnity3D(File.OpenRead(dataPath));

            return default;
        }

        private static UnityVersion GetVersionFromGlobalGameManagers(byte[] ggmBytes)
        {
            var verString = new StringBuilder();
            var idx = 0x14;
            while (ggmBytes[idx] != 0)
            {
                verString.Append(Convert.ToChar(ggmBytes[idx]));
                idx++;
            }

            string unityVer = verString.ToString();
            if (!UnityVersionRegex.IsMatch(unityVer))
            {
                idx = 0x30;
                verString = new StringBuilder();
                while (ggmBytes[idx] != 0)
                {
                    verString.Append(Convert.ToChar(ggmBytes[idx]));
                    idx++;
                }

                unityVer = verString.ToString().Trim();
            }

            return UnityVersion.Parse(unityVer);
        }

        private static UnityVersion GetVersionFromDataUnity3D(Stream fileStream)
        {
            var verString = new StringBuilder();

            if (fileStream.CanSeek)
                fileStream.Seek(0x12, SeekOrigin.Begin);
            else
            {
                if (fileStream.Read(new byte[0x12], 0, 0x12) != 0x12)
                    throw new("Failed to seek to 0x12 in data.unity3d");
            }

            while (true)
            {
                var read = fileStream.ReadByte();
                if (read == 0)
                    break;
                verString.Append(Convert.ToChar(read));
            }

            return UnityVersion.Parse(verString.ToString().Trim());
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private extern static void SetDefaultConsoleTitleWithGameName([MarshalAs(UnmanagedType.LPStr)] string GameName, [MarshalAs(UnmanagedType.LPStr)] string GameVersion = null);
    }
}
