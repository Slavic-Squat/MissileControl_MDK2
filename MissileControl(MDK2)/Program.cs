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
    partial class Program : MyGridProgram
    {
        MyIni config = new MyIni();
        bool updatesPending = false;
        DateTime time;
        bool listeningForClock = false;
        string broadcastTag;
        IMyBroadcastListener broadcastListener;
        MissileControl missile;
        Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();
        MyCommandLine commandLine = new MyCommandLine();

        public Program()
        {
            if (!config.TryParse(Me.CustomData))
            {
                throw new Exception();
            }
            missile = new MissileControl(this, 0, false);

            commands["InitMissile"] = (x) =>
            {
                missile.InitMissile(x[0], x[1]);
                SyncClock(x[2]);
            };
            commands["Launch"] = (x) => missile.Launch(x[0]);
            commands["SyncClock"] = (x) => SyncClock(x[0]);
            commands["RecieveClock"] = (x) => RecieveClock(x[0]);
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != null)
            {
                TryRunCommand(argument);
            }
            if (updatesPending)
            {
                UpdateConfig();
                Echo(updatesPending.ToString());
            }
            TryRunQueuedCommands();

            time += Runtime.TimeSinceLastRun;
            if (listeningForClock && broadcastListener != null)
            {
                while (broadcastListener.HasPendingMessage)
                {
                    var message = broadcastListener.AcceptMessage();
                    if (message.Data is long)
                    {
                        time = new DateTime(message.As<long>());
                        listeningForClock = false;
                    }
                }
            }
            missile.Run(time);
        }

        public void SyncClock(string ticksString)
        {
            long ticks;
            long.TryParse(ticksString, out ticks);
            time = new DateTime(ticks);
        }

        public void RecieveClock(string channel)
        {
            listeningForClock = true;
            broadcastTag = channel;
            broadcastListener = IGC.RegisterBroadcastListener(channel);
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
                Me.CustomData = config.ToString();
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
                Me.CustomData = config.ToString();
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
            config.TryParse(Me.CustomData);
            updatesPending = false;
        }
    }
}
