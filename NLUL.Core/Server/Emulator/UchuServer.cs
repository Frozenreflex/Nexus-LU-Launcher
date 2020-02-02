/*
 * TheNexusAvenger
 *
 * Sets up and runs an Uchu instance.
 * https://github.com/yuwui/Uchu
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Xml.Serialization;
using NLUL.Core.Server.Prerequisite;
using NLUL.Core.Server.Util;

namespace NLUL.Core.Server.Emulator
{
    /*
     * State of the Uchu server.
     */
    public class UchuState
    {
        public string CurrentVersion;
        public int ProcessId = 0;
    }
    
    /*
     * Database section of the Uchu config.
     */
    public class UchuDatabase
    {
        public string Provider = "postgres";
        public string Database = "uchu";
        public string Host = "localhost";
        public string Username = "postgres";
        public string Password = "postgres";
    }
    
    /*
     * ConsoleLogging section of the Uchu config.
     */
    public class UchuConsoleLogging
    {
        public string Level = "Debug";
    }
    
    /*
     * FileLogging section of the Uchu config.
     */
    public class UchuFileLogging
    {
        public string Level = "None";
        public string Logfile = "uchu.log";
    }
    
    /*
     * DllSource section of the Uchu config.
     */
    public class UchuDllSource
    {
        public string ServerDllSourcePath = "../../../../";
        public string DotNetPath = "dotnet";
        public string ScriptDllSource = "Uchu.StandardScripts";
    }
    
    /*
     * ManagedScriptSources section of the Uchu config.
     */
    public class UchuManagedScriptSources
    {
        
    }
    
    /*
     * ResourcesConfiguration section of the Uchu config.
     */
    public class UchuResourcesConfiguration
    {
        public string GameResourceFolder = "/res";
    }
    
    /*
     * UchuNetworking section of the Uchu config.
     */
    public class UchuNetworking
    {
        public string Certificate;
        public string Hostname;
        public string CharacterPort = "2002";
    }
    
    /*
     * Uchu XML config structure.
     */
    [XmlRoot(ElementName = "Uchu")]
    public class UchuConfig
    {
        public UchuDatabase Database = new UchuDatabase();
        public UchuConsoleLogging ConsoleLogging = new UchuConsoleLogging();
        public UchuFileLogging FileLogging = new UchuFileLogging();
        public UchuDllSource DllSource = new UchuDllSource();
        public UchuManagedScriptSources ManagedScriptSources = new UchuManagedScriptSources();
        public UchuResourcesConfiguration ResourcesConfiguration = new UchuResourcesConfiguration();
        public UchuNetworking Networking = new UchuNetworking();
    }
    
    public class UchuServer : IEmulator
    {
        private const string BUILD_MODE = "Debug";
        private const string DOTNET_APP_VERSION = "netcoreapp3.1";
        
        private ServerInfo ServerInfo;
        private UchuState State;
        
        /*
         * Returns the current GitHub commit.
         */
        private static string GetCurrentGitHubCommit()
        {
            return GitHub.GetLastCommit("yuwui","Uchu");
        }
        
        /*
         * Creates an Uchu Server object.
         */
        public UchuServer(ServerInfo info)
        {
            this.ServerInfo = info;
            this.ReadState();
        }
        
        /*
         * Reads the current state.
         */
        private void ReadState()
        {
            var stateLocation = Path.Combine(this.ServerInfo.ServerFileLocation,"state.xml");
            if (File.Exists(stateLocation))
            {
                var stateSerializer = new XmlSerializer(typeof(UchuState));  
                var stateReader = new FileStream(stateLocation,FileMode.Open);  
                this.State = (UchuState) stateSerializer.Deserialize(stateReader);  
                stateReader.Close();  
            }
            else
            {
                this.State = new UchuState();
            }
        }
        
        /*
         * Saves the current state.
         */
        private void WriteState()
        {
            // Serialize the state.
            var stateLocation = Path.Combine(this.ServerInfo.ServerFileLocation,"state.xml");
            var stateSerializer = new XmlSerializer(typeof(UchuState));  
            var stateWriter = new StreamWriter(stateLocation);  
            stateSerializer.Serialize(stateWriter,this.State);  
            stateWriter.Close();
        }
        
        /*
         * Cleans up files from installs.
         */
        private void CleanupFiles()
        {
            // Clean up the zip files.
            File.Delete(Path.Combine(this.ServerInfo.ServerFileLocation,"InfectedRose.zip"));
            File.Delete(Path.Combine(this.ServerInfo.ServerFileLocation,"RakDotNet.zip"));
            File.Delete(Path.Combine(this.ServerInfo.ServerFileLocation,"Server.zip"));
            
            // Clean up the extracted directories.
            var infectedRoseDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"InfectedRose");
            if (Directory.Exists(infectedRoseDirectory))
            {
                Directory.Delete(infectedRoseDirectory,true);
            }
            var rakDotNetDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"RakDotNet");
            if (Directory.Exists(rakDotNetDirectory))
            {
                Directory.Delete(rakDotNetDirectory, true);
            }
        }
        
        /*
         * Creates the initial server configuration.
         */
        private void CreateConfig()
        {
            // Create the base config.
            var config = new UchuConfig();
            
            // Set the values that are specific to the install.
            config.DllSource.DotNetPath = Path.GetFullPath(Path.Combine(this.ServerInfo.ServerFileLocation,"Tools","dotnet3.1","dotnet"));
            config.ResourcesConfiguration.GameResourceFolder = Path.GetFullPath(Path.Combine(this.ServerInfo.ClientLocation,"res"));
            
            // Write the config.
            var configLocation = Path.Combine(this.ServerInfo.ServerFileLocation,"Server","Uchu-master","Uchu.Master","bin",BUILD_MODE,DOTNET_APP_VERSION,"config.xml");
            var configSerializer = new XmlSerializer(typeof(UchuConfig));  
            var configWriter = new StreamWriter(configLocation);  
            configSerializer.Serialize(configWriter,config);  
            configWriter.Close();  
        }
        
        /*
         * Returns the prerequisites for the server.
         */
        public List<IPrerequisite> GetPrerequisites()
        {
            return new List<IPrerequisite>()
            {
                new DotNetCore31(Path.Combine(this.ServerInfo.ServerFileLocation,"Tools"))
            };
            
            // TODO: Detect PostgresSQL install
            // TODO: Redis support?
        }
        
        /*
         * Returns if the server is running.
         */
        public bool IsRunning()
        {
            // Return true if the process id isn't zero and the process exists.
            try
            {
                if (this.State.ProcessId != 0)
                {
                    Process.GetProcessById(this.State.ProcessId);
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Update that the server is not running.
                this.State.ProcessId = 0;
                this.WriteState();
            }

            // Return false (not running).
            return false;
        }
        
        /*
         * Returns if an update is available.
         */
        public bool IsUpdateAvailable()
        {
            return this.State.CurrentVersion == GetCurrentGitHubCommit();
        }
        
        /*
         * Installs the server. Used for both initializing
         * the first time and updating.
         */
        public void Install()
        {
            // Get the tool locations.
            var toolsLocation = Path.Combine(this.ServerInfo.ServerFileLocation,"Tools");
            var dotNetDirectoryLocation = Path.Combine(toolsLocation,"dotnet3.1");
            var dotNetExecutableLocation = Path.Combine(dotNetDirectoryLocation, "dotnet");
            
            // TODO: Verify client is unpacked.
            
            // Clear previous files.
            Console.WriteLine("Clearing old files");
            this.CleanupFiles();
            
            // Remove the previous server.
            var serverOldDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"Server");
            if (Directory.Exists(serverOldDirectory))
            {
                Directory.Delete(serverOldDirectory,true);
            }
            
            // Install additional tools.
            Console.WriteLine("Installing .NET Entity Framework Command Line Interface");
            var entityFrameworkInstallProcess = new Process();
            entityFrameworkInstallProcess.StartInfo.WorkingDirectory = dotNetDirectoryLocation;
            entityFrameworkInstallProcess.StartInfo.FileName = dotNetExecutableLocation;
            entityFrameworkInstallProcess.StartInfo.CreateNoWindow = true;
            entityFrameworkInstallProcess.StartInfo.Arguments = "tool install --global dotnet-ef";
            entityFrameworkInstallProcess.Start();
            entityFrameworkInstallProcess.WaitForExit();
            entityFrameworkInstallProcess.Close();
            
            // Download and extract the server files from master.
            var client = new WebClient();
            Console.WriteLine("Downloading the latest server from GitHub/yuwui/Uchu");
            var targetServerZip = Path.Combine(this.ServerInfo.ServerFileLocation,"Server.zip");
            var targetServerDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"Server");
            client.DownloadFile("https://github.com/yuwui/Uchu/archive/master.zip",targetServerZip);
            ZipFile.ExtractToDirectory(targetServerZip,targetServerDirectory);
            Directory.Delete(Path.Combine(targetServerDirectory,"Uchu-master","InfectedRose"),true);
            Directory.Delete(Path.Combine(targetServerDirectory,"Uchu-master","RakDotNet"),true);
            
            // Download and extract InfectedRose.
            Console.WriteLine("Downloading the latest InfectedRose library from GitHub/Wincent01/InfectedRose");
            var targetInfectedRoseDownloadZip = Path.Combine(this.ServerInfo.ServerFileLocation,"InfectedRose.zip");
            var targetInfectedRoseDownloadDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"InfectedRose");
            client.DownloadFile("https://github.com/Wincent01/InfectedRose/archive/master.zip",targetInfectedRoseDownloadZip);
            ZipFile.ExtractToDirectory(targetInfectedRoseDownloadZip,targetInfectedRoseDownloadDirectory);
            Directory.Move(Path.Combine(targetInfectedRoseDownloadDirectory,"InfectedRose-master"),Path.Combine(targetServerDirectory,"Uchu-master","InfectedRose"));
            
            // Download and extract InfectedRose.
            Console.WriteLine("Downloading the latest InfectedRose library from GitHub/yuwui/RakDotNet");
            var targetRakDotNetDownloadZip = Path.Combine(this.ServerInfo.ServerFileLocation,"RakDotNet.zip");
            var targetRakDotNetDownloadDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"RakDotNet");
            client.DownloadFile("https://github.com/yuwui/RakDotNet/archive/3.25/tcpudp.zip",targetRakDotNetDownloadZip);
            ZipFile.ExtractToDirectory(targetRakDotNetDownloadZip,targetRakDotNetDownloadDirectory);
            Directory.Move(Path.Combine(targetRakDotNetDownloadDirectory,"RakDotNet-3.25-tcpudp"),Path.Combine(targetServerDirectory,"Uchu-master","RakDotNet"));

            // Compile the server.
            Console.WriteLine("Building the server.");
            var buildDirectory = Path.Combine(targetServerDirectory,"Uchu-master");
            var buildProcess = new Process();
            buildProcess.StartInfo.WorkingDirectory = buildDirectory;
            buildProcess.StartInfo.FileName = dotNetExecutableLocation;
            buildProcess.StartInfo.CreateNoWindow = true;
            buildProcess.StartInfo.Arguments = "build -c " + BUILD_MODE;
            buildProcess.Start();
            buildProcess.WaitForExit();
            buildProcess.Close();

            // Clean up the download files.
            Console.WriteLine("Cleaning up files.");
            // this.CleanupFiles();
            
            // Create the default configuration.
            Console.WriteLine("Creating the default configuration.");
            this.CreateConfig();
            this.State.CurrentVersion = GetCurrentGitHubCommit();
            this.WriteState();
        }

        /*
         * Starts the server.
         */
        public void Start()
        {
            // Return if the server is already running.
            if (this.IsRunning())
            {
                Console.WriteLine("Server is already running.");
                return;
            }
            
            // Determine the file locations.
            var masterServerDirectory = Path.Combine(this.ServerInfo.ServerFileLocation,"Server","Uchu-master","Uchu.Master","bin",BUILD_MODE,DOTNET_APP_VERSION);
            var masterServerExecutable = Path.Combine(masterServerDirectory,"Uchu.Master");
            
            // Create and start the process.
            var uchuProcess = new Process();
            uchuProcess.StartInfo.FileName = masterServerExecutable;
            uchuProcess.StartInfo.WorkingDirectory = masterServerDirectory;
            uchuProcess.StartInfo.CreateNoWindow = true;
            uchuProcess.Start();
            Console.WriteLine("Started server.");
            
            // Start and store the process id.
            this.State.ProcessId = uchuProcess.Id;
            this.WriteState();
        }
        
        /*
         * Stops the server.
         */
        public void Stop()
        {
            // Stop the process.
            if (this.IsRunning())
            {
                var uchuProcess = Process.GetProcessById(this.State.ProcessId);
                uchuProcess.Kill(true);
                Console.WriteLine("Stopped server.");
            }
            else
            {
                Console.WriteLine("Server not running.");
            }

            // Store 0 as the process id.
            this.State.ProcessId = 0;
            this.WriteState();
        }
    }
}