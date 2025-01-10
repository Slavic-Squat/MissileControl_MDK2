using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class ConfigCommandHelper
        {
            private MyIni config = new MyIni();
            private bool updatesPending = false;
            private MyCommandLine commandLine = new MyCommandLine();
            private Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();

            IMyTerminalBlock storageBlock;

            public ConfigCommandHelper(IMyTerminalBlock storageBlock, Dictionary<string, Action<string[]>> commands)
            {
                if (!config.TryParse(storageBlock.CustomData))
                {
                    throw new Exception();
                }

                this.storageBlock = storageBlock;
                this.commands = commands;
            }

            public void Run()
            {
                if (updatesPending)
                {
                    UpdateConfig();
                }
                TryRunQueuedCommands();
            }

            public bool TryRunCommand(string commandString)
            {
                try
                {
                    if (commandLine.TryParse(commandString))
                    {
                        if (commandLine.Switch("ConfigUpdated"))
                        {
                            updatesPending = true;
                        }
                        string commandName = commandLine.Argument(0);
                        string[] commandArguments = new string[commandLine.ArgumentCount - 1];
                        for (int i = 0; i < commandArguments.Length; i++)
                        {
                            commandArguments[i] = commandLine.Argument(i + 1);
                        }
                        Action<string[]> command;

                        if (commandName != null)
                        {
                            if (commands.TryGetValue(commandName, out command))
                            {
                                command(commandArguments);
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                        return true;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            public bool TryQueueUserCommand(string userCommandName)
            {
                try
                {
                    string userCommandString = config.Get("User Commands", userCommandName).ToString();
                    int queuedCommandsCounter = config.Get("Script Info", "Queued Commands Counter").ToInt32();
                    config.Set("Queued Commands", $"{queuedCommandsCounter}", userCommandString);
                    queuedCommandsCounter++;
                    config.Set("Script Info", "Queued Commands Counter", $"{queuedCommandsCounter}");
                    storageBlock.CustomData = config.ToString();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            public bool TryDequeueUserCommand(string userCommandName)
            {
                try
                {
                    config.Delete("Queued Commands", userCommandName);
                    storageBlock.CustomData = config.ToString();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            public bool TryRunQueuedCommands()
            {
                try
                {
                    List<MyIniKey> queuedCommandKeys = new List<MyIniKey>();
                    config.GetKeys("Queued Commands", queuedCommandKeys);
                    queuedCommandKeys.Sort();

                    foreach (var queueCommandKey in queuedCommandKeys)
                    {
                        TryRunCommand(config.Get(queueCommandKey).ToString());
                        TryDequeueUserCommand(queueCommandKey.Name);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

            public void UpdateConfig()
            {
                config.TryParse(storageBlock.CustomData);
                updatesPending = false;
            }
        }
    }
}
