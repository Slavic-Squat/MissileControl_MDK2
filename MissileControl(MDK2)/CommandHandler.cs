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
        public class CommandHandler
        {
            private MyCommandLine _commandLine = new MyCommandLine();
            private Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();

            private IMyTerminalBlock _storageBlock;
            private string _commandsHeader = "[COMMANDS]\n";

            public CommandHandler(IMyTerminalBlock storageBlock, Dictionary<string, Action<string[]>> commands)
            {
                _storageBlock = storageBlock;
                _commands = commands;
                _storageBlock.CustomData = _commandsHeader;
            }

            public bool RunCustomDataCommands()
            {
                string commands = null;
                if (!_storageBlock.CustomData.StartsWith(_commandsHeader))
                {
                    _storageBlock.CustomData = _commandsHeader;
                    return false;
                }
                else
                {
                    commands = _storageBlock.CustomData.Substring(_commandsHeader.Length);
                }
                if (commands != null && commands != "")
                {
                    bool success = RunCommands(commands);
                    _storageBlock.CustomData = _commandsHeader;
                    return success;
                }
                return false;
            }

            public bool RunCommands(string commandsString)
            {
                if (commandsString == null || commandsString == "")
                {
                    return false;
                }
                string[] separatedCommandStrings = commandsString.Split('|', '\n');
                foreach (string commandString in separatedCommandStrings)
                {
                    commandsString = commandString.Trim();
                    if (_commandLine.TryParse(commandString))
                    {
                        string commandName = _commandLine.Argument(0);
                        string[] commandArguments = new string[_commandLine.ArgumentCount - 1];
                        for (int i = 0; i < commandArguments.Length; i++)
                        {
                            commandArguments[i] = _commandLine.Argument(i + 1);
                        }
                        Action<string[]> command;

                        if (commandName != null)
                        {
                            if (_commands.TryGetValue(commandName, out command))
                            {
                                command(commandArguments);
                            }
                        }
                    }
                }
                return true;
            }
        }
    }
}
