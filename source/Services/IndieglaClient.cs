﻿using Playnite.SDK;
using Playnite.SDK.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using System;
using CommonPlayniteShared.Common;
using IndiegalaLibrary.Models;
using System.Collections.Generic;
using CommonPluginsShared;

namespace IndiegalaLibrary.Services
{
    public class IndieglaClient : LibraryClient
    {
        private static ILogger logger => LogManager.GetLogger();

        public override string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        public override bool IsInstalled => File.Exists(ClientExecPath);


        #region Client variables
        public static string AppData => Environment.GetEnvironmentVariable("appdata");
        public static string IGClient => Path.Combine(AppData, "IGClient");
        public static string IGStorage => Path.Combine(IGClient, "storage");
        public static string GameInstalledFile => Path.Combine(IGStorage, "installed.json");
        public static string ConfigFile => Path.Combine(IGClient, "config.json");
        #endregion


        #region Client data
        public static dynamic ConfigData
        {
            get
            {
                if (File.Exists(IndieglaClient.ConfigFile))
                {
                    return Serialization.FromJsonFile<dynamic>(IndieglaClient.ConfigFile);
                }
                else
                {
                    logger.Warn($"no 'config.json' in {IGClient}");
                }

                return null;
            }
        }

        public static ClientData ClientData
        {
            get
            {
                if (ConfigData != null)
                {
                    string jsonData = Serialization.ToJson(ConfigData?["gala_data"]);
                    return Serialization.FromJson<ClientData>(jsonData);
                }

                return null;
            }
        }


        public static List<ClientInstalled> GetClientGameInstalled()
        {
            if (File.Exists(IndieglaClient.ConfigFile))
            {
                return Serialization.FromJsonFile<List<ClientInstalled>>(IndieglaClient.GameInstalledFile);
            }
            else
            {
                logger.Warn($"no 'installed.json' in {IGStorage}");
            }

            return new List<ClientInstalled>();
        }

        public static ClientGameInfo GetClientGameInfo(string GameId)
        {
            try
            {
                string prod_slugged_name = IndiegalaAccountClient.GetProdSluggedName(GameId);
                if (prod_slugged_name != null && ConfigData?[prod_slugged_name] != null)
                {
                    string jsonData = Serialization.ToJson(ConfigData[prod_slugged_name]);
                    return Serialization.FromJson<ClientGameInfo>(jsonData);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }

            return null;
        }
        #endregion


        private static string _ClientExecPath = string.Empty;
        public static string ClientExecPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_ClientExecPath))
                {
                    return _ClientExecPath;
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\6f4f090a-db12-53b6-ac44-9ecdb7703b4a"))
                {
                    if (key != null)
                    {
                        foreach (string el in key?.GetValueNames())
                        {
                            if (el.Contains("InstallLocation"))
                            {
                                Common.LogDebug(true, $"Path-1 - {key.GetValue("InstallLocation").ToString()}");
                                Common.LogDebug(true, $"Path-2 - {key.GetValue("ShortcutName").ToString()}");
                                string path = Path.Combine(key.GetValue("InstallLocation").ToString(), key.GetValue("ShortcutName").ToString() + ".exe");
                                if (File.Exists(path))
                                {
                                    _ClientExecPath = path;
                                    return _ClientExecPath;
                                }
                            }
                        }
                    }
                }

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\6f4f090a-db12-53b6-ac44-9ecdb7703b4a"))
                {
                    if (key != null)
                    {
                        foreach (string el in key?.GetValueNames())
                        {
                            if (el.Contains("InstallLocation"))
                            {
                                Common.LogDebug(true, $"Path-3 - {key.GetValue("InstallLocation").ToString()}");
                                Common.LogDebug(true, $"Path-4 - {key.GetValue("ShortcutName").ToString()}");
                                string path = Path.Combine(key.GetValue("InstallLocation").ToString(), key.GetValue("ShortcutName").ToString() + ".exe");
                                if (File.Exists(path))
                                {
                                    _ClientExecPath = path;
                                    return _ClientExecPath;
                                }
                            }
                        }
                    }
                }

                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    using (RegistryKey key = hklm.OpenSubKey(@"SOFTWARE\6f4f090a-db12-53b6-ac44-9ecdb7703b4a"))
                    {
                        if (key != null)
                        {
                            foreach (string el in key?.GetValueNames())
                            {
                                if (el.Contains("InstallLocation"))
                                {
                                    Common.LogDebug(true, $"Path-5 - {key.GetValue("InstallLocation").ToString()}");
                                    Common.LogDebug(true, $"Path-6 - {key.GetValue("ShortcutName").ToString()}");
                                    string path = Path.Combine(key.GetValue("InstallLocation").ToString(), key.GetValue("ShortcutName").ToString() + ".exe");
                                    if (File.Exists(path))
                                    {
                                        _ClientExecPath = path;
                                        return _ClientExecPath;
                                    }
                                }
                            }
                        }
                    }
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store"))
                {
                    if (key != null)
                    {
                        foreach (string el in key?.GetValueNames())
                        {
                            if (el.Contains("IGClient") && !el.Contains("Setup"))
                            {
                                Common.LogDebug(true, $"Path-7 - {el.ToString()}");
                                string path = Path.Combine(el.ToString());
                                if (File.Exists(path))
                                {
                                    _ClientExecPath = el.ToString();
                                    return _ClientExecPath;
                                }
                            }
                        }
                    }
                }

                logger.Warn($"no installation find");

                return string.Empty;
            }
        }

        private static string _GameInstallPath = string.Empty;
        public static string GameInstallPath
        {
            get
            {
                string GameInstallPath = string.Empty;

                Common.LogDebug(true, $"Path-8 - {IGStorage}");
                if (File.Exists(Path.Combine(IGStorage, "install-path.json")))
                {
                    GameInstallPath = FileSystem.ReadFileAsStringSafe(Path.Combine(IGStorage, "install-path.json"));
                    if (GameInstallPath.Length > 7)
                    {
                        GameInstallPath = GameInstallPath.Replace("[\"", string.Empty).Replace("\"]", string.Empty).Replace("\\\\", "\\");
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(IGStorage, "default-install-path.json")))
                        {
                            GameInstallPath = FileSystem.ReadFileAsStringSafe(Path.Combine(IGStorage, "default-install-path.json"));
                            GameInstallPath = GameInstallPath.Replace("\"", string.Empty).Replace("/", "\\");
                        }
                    }
                }
                else
                {
                    logger.Warn($"no 'install-path.json' in {IGStorage}");
                }

                return GameInstallPath;
            }
        }


        #region Client actions
        public override void Open()
        {
            Process.Start(ClientExecPath);
        }

        public override void Shutdown()
        {
            Process mainProc = Process.GetProcessesByName("IGClient").FirstOrDefault();
            if (mainProc == null)
            {
                logger.Info("IndieGala client is no longer running, no need to shut it down.");
                return;
            }

            // TODO Check when command line
            Process[] workers = Process.GetProcessesByName("IGClient");
            foreach (Process worker in workers)
            {
                worker.Kill();
                worker.WaitForExit(2000);
                worker.Dispose();
            }
        }
        #endregion  
    }
}
