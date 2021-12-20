using System;
using System.Diagnostics;
using System.IO;
using InfectedRose.Lvl;
using NLUL.Core.Client.Patch;
using NLUL.Core.Client.Runtime;
using NLUL.Core.Util;

namespace NLUL.Core.Client
{
    public class ClientRunner
    {
        /// <summary>
        /// Information of the system.
        /// </summary>
        private readonly SystemInfo systemInfo;
        
        /// <summary>
        /// Patcher for the client runner.
        /// </summary>
        public ClientRuntime Runtime { get; }
        
        /// <summary>
        /// Patcher for the client runner.
        /// </summary>
        public ClientPatcher Patcher { get; private set; }

        /// <summary>
        /// Creates a Client instance.
        /// </summary>
        /// <param name="systemInfo">Information of the system.</param>
        public ClientRunner(SystemInfo systemInfo)
        {
            this.systemInfo = systemInfo;
            this.Patcher = new ClientPatcher(systemInfo);
            this.Runtime = new ClientRuntime(systemInfo);
        }

        /// <summary>
        /// Moves the client parent directory.
        /// </summary>
        /// <param name="destination">Destination directory of the clients.</param>
        public void MoveClientParentDirectory(string destination)
        {
            // Create the parent directory.
            var existingParentDirectory = this.systemInfo.SystemFileLocation;
            Directory.CreateDirectory(destination);
            foreach (var manifestEntry in this.Patcher.Manifest.Manifest)
            {
                manifestEntry.EntryDirectory = manifestEntry.EntryDirectory.Replace(existingParentDirectory.Replace("\\", "/"), destination.Replace("\\", "/"));
            }
            this.Patcher.Manifest.Save();

            // Set the parent directory.
            this.systemInfo.Settings.ClientParentLocation = destination;
            this.systemInfo.SaveSettings();

            // Move the clients.
            foreach (var clientDirectory in Directory.GetDirectories(existingParentDirectory))
            {
                var clientDirectoryName = new DirectoryInfo(clientDirectory).Name;
                if (File.Exists(Path.Combine(clientDirectory, "legouniverse.exe")))
                {
                    DirectoryExtensions.Move(clientDirectory, Path.Combine(destination, clientDirectoryName));
                }
            }

            // Reload the patches.
            this.Patcher = new ClientPatcher(systemInfo);
        }

        /// <summary>
        /// Launches the client.
        /// </summary>
        /// <param name="host">Host to launch.</param>
        /// <returns>Process that was started.</returns>
        public Process Launch(ServerEntry host)
        {
            // Set up the runtime if it isn't installed.
            if (!this.Runtime.IsInstalled)
            {
                if (this.Runtime.CanInstall)
                {
                    // Install the runtime.
                    this.Runtime.Install();
                }
                else
                {
                    // Stop the launch if a valid runtime isn't set up.
                    return null;
                }
            }
            
            // Modify the boot file.
            var bootConfigLocation = Path.Combine(this.systemInfo.ClientLocation, "boot.cfg");
            LegoDataDictionary bootConfig = null;
            try
            {
                bootConfig = LegoDataDictionary.FromString(File.ReadAllText(bootConfigLocation).Trim());
            }
            catch (FormatException)
            {
                bootConfig = LegoDataDictionary.FromString(File.ReadAllText(Path.Combine(this.systemInfo.ClientLocation, "boot_backup.cfg")).Trim());
            }
            bootConfig["SERVERNAME"] = host.ServerName;
            bootConfig["AUTHSERVERIP"] = host.ServerAddress;
            File.WriteAllText(bootConfigLocation,bootConfig.ToString("\n"));
            
            // Apply any pre-launch patches.
            foreach (var patch in Patcher.Patches)
            {
                if (patch is IPreLaunchPatch preLaunchPatch)
                {
                    if (!patch.Installed) continue;
                    preLaunchPatch.OnClientRequestLaunch();
                }
            }
            
            // Launch the client.
            var clientProcess = this.Runtime.RunApplication(Path.Combine(this.systemInfo.ClientLocation, "legouniverse.exe"), this.systemInfo.ClientLocation);
            clientProcess.Start();
            
            // Return the output.
            return clientProcess;
        }
    }
}