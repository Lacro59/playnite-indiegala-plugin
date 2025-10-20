using Playnite.SDK;
using Playnite.SDK.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using System;
using CommonPlayniteShared.Common;
using System.Collections.Generic;
using CommonPluginsShared;
using Playnite.SDK.Models;
using CommonPluginsShared.Extensions;
using IndiegalaLibrary.Models.GalaClient;

namespace IndiegalaLibrary.Services
{
    public class IndiegalaClient : LibraryClient
    {
        private static ILogger Logger => LogManager.GetLogger();

        public override string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        public override bool IsInstalled => File.Exists(ClientExecPath);


        #region Client variables

        private static string AppData => Environment.GetEnvironmentVariable("appdata");
        private static string IGClient => Path.Combine(AppData, "IGClient");
        private static string IGStorage => Path.Combine(IGClient, "storage");
        private static string GameInstalledFile => Path.Combine(IGStorage, "installed.json");
        private static string ConfigFile => Path.Combine(IGClient, "config.json");

        #endregion


        #region Client data

        public static dynamic ConfigData
        {
            get
            {
                if (File.Exists(ConfigFile))
                {
                    return Serialization.FromJsonFile<dynamic>(ConfigFile);
                }
                else
                {
                    Logger.Warn($"no 'config.json' in {IGClient}");
                }
                return null;
            }
        }

        public static GalaData ClientData
        {
            get
            {
                if (ConfigData != null)
                {
                    string jsonData = Serialization.ToJson(ConfigData?["gala_data"]);
                    return Serialization.FromJson<GalaData>(jsonData);
                }
                return null;
            }
        }


        public static List<GalaInstalled> GetClientGameInstalled()
        {
            _ = Serialization.TryFromJsonFile(GameInstalledFile, out List<GalaInstalled> clientInstalled);
            return clientInstalled;
        }

        public static GameAction GameIsInstalled(string gameId)
        {
            GalaInstalled gameInstalled = GetClientGameInstalled()?.FirstOrDefault(x => x.Target.ItemData.IdKeyName.IsEqual(gameId));
            return gameInstalled != null
                ? new GameAction
                {
                    Type = GameActionType.File,
                    IsPlayAction = true,
                    Name = "IGClient",
                    WorkingDir = Path.Combine(Serialization.TryFromJson(Serialization.ToJson(gameInstalled.Path), out string[] paths) ? paths[0] : gameInstalled.Path.ToString(), gameInstalled.Target.ItemData.SluggedName),
                    Path = gameInstalled.Target.GameData.ExePath
                }
                : null;
        }

        #endregion


        private static string _clientExecPath;
        public static string ClientExecPath
        {
            get
            {
                if (!_clientExecPath.IsNullOrEmpty())
                {
                    return _clientExecPath;
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\6f4f090a-db12-53b6-ac44-9ecdb7703b4a"))
                {
                    if (key != null)
                    {
                        foreach (string el in key?.GetValueNames())
                        {
                            if (el.Contains("InstallLocation"))
                            {
                                Common.LogDebug(true, $"Path-1 - {key.GetValue("InstallLocation")}");
                                Common.LogDebug(true, $"Path-2 - {key.GetValue("ShortcutName")}");
                                string path = Path.Combine(key.GetValue("InstallLocation").ToString(), key.GetValue("ShortcutName").ToString() + ".exe");
                                if (File.Exists(path))
                                {
                                    _clientExecPath = path;
                                    return _clientExecPath;
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
                                Common.LogDebug(true, $"Path-3 - {key.GetValue("InstallLocation")}");
                                Common.LogDebug(true, $"Path-4 - {key.GetValue("ShortcutName")}");
                                string path = Path.Combine(key.GetValue("InstallLocation").ToString(), key.GetValue("ShortcutName").ToString() + ".exe");
                                if (File.Exists(path))
                                {
                                    _clientExecPath = path;
                                    return _clientExecPath;
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
                                    Common.LogDebug(true, $"Path-5 - {key.GetValue("InstallLocation")}");
                                    Common.LogDebug(true, $"Path-6 - {key.GetValue("ShortcutName")}");
                                    string path = Path.Combine(key.GetValue("InstallLocation").ToString(), key.GetValue("ShortcutName").ToString() + ".exe");
                                    if (File.Exists(path))
                                    {
                                        _clientExecPath = path;
                                        return _clientExecPath;
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
                                Common.LogDebug(true, $"Path-7 - {el}");
                                string path = Path.Combine(el.ToString());
                                if (File.Exists(path))
                                {
                                    _clientExecPath = el.ToString();
                                    return _clientExecPath;
                                }
                            }
                        }
                    }
                }

                Logger.Warn($"no installation find");
                return string.Empty;
            }
        }

        private static string _gameInstallPath = string.Empty;
        public static string GameInstallPath
        {
            get
            {
                if (!_gameInstallPath.IsNullOrEmpty())
                {
                    return _gameInstallPath;
                }

                Common.LogDebug(true, $"Path-8 - {IGStorage}");
                if (File.Exists(Path.Combine(IGStorage, "install-path.json")))
                {
                    _gameInstallPath = FileSystem.ReadFileAsStringSafe(Path.Combine(IGStorage, "install-path.json"));
                    if (_gameInstallPath.Length > 7)
                    {
                        _gameInstallPath = _gameInstallPath.Replace("[\"", string.Empty).Replace("\"]", string.Empty).Replace("\\\\", "\\");
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(IGStorage, "default-install-path.json")))
                        {
                            _gameInstallPath = FileSystem.ReadFileAsStringSafe(Path.Combine(IGStorage, "default-install-path.json"));
                            _gameInstallPath = _gameInstallPath.Replace("\"", string.Empty).Replace("/", "\\");
                        }
                    }
                }
                else
                {
                    Logger.Warn($"no 'install-path.json' in {IGStorage}");
                }

                return _gameInstallPath;
            }
        }


        #region Client actions

        public override void Open()
        {
            _ = Process.Start(ClientExecPath);
        }

        public override void Shutdown()
        {
            Process mainProc = Process.GetProcessesByName("IGClient").FirstOrDefault();
            if (mainProc == null)
            {
                Logger.Info("IndieGala client is no longer running, no need to shut it down.");
                return;
            }

            // TODO Check when command line
            Process[] workers = Process.GetProcessesByName("IGClient");
            foreach (Process worker in workers)
            {
                worker.Kill();
                _ = worker.WaitForExit(2000);
                worker.Dispose();
            }
        }

        #endregion  
    }
}