/*
 * TheNexusAvenger
 *
 * Starts a server.
 */

using System;
using System.Collections.Generic;
using NLUL.Core;
using NLUL.Core.Server;

namespace NLUL.CLI.Action.Server
{
    public class Start : IAction
    {
        /*
         * Returns the arguments for the action.
         */
        public string GetArguments()
        {
            return "<serverName>";
        }
        
        /*
         * Returns a description of what the action does.
         */
        public string GetDescription()
        {
            return "Starts a server that currently isn't running.";
        }
        
        /*
         * Performs the action.
         */
        public void Run(List<string> arguments,SystemInfo systemInfo)
        {
            // Get the name.
            if (arguments.Count < 3)
            {
                Console.WriteLine("serverName not specified.");
                Actions.PrintUsage("server","start");
                return;
            }
            var name = arguments[2];
            
            // Get the server.
            var serverCreator = new ServerCreator(systemInfo);
            var server = serverCreator.GetServer(name);
            
            // Start the server.
            if (server == null)
            {
                Console.WriteLine("Server \"" + name + "\" does not exist.");
            }
            else if (server.IsRunning())
            {
                Console.WriteLine("Server \"" + name + "\" is already running. Use \'server stop \"" + name + "\"' to stop the server.");
            }
            else
            {
                Console.WriteLine("Starting server \"" + name + "\".");
                server.Start();
                Console.WriteLine("Started server \"" + name + "\".");
            }
        }
    }
}