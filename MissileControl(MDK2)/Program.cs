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
        DateTime time;
        bool listeningForClock = false;
        string broadcastTag;
        IMyBroadcastListener broadcastListener;
        MissileControl missile;
        Dictionary<string, Action<string, string, string>> commands = new Dictionary<string, Action<string, string, string>>();
        MyCommandLine commandLine = new MyCommandLine();

        public Program()
        {
            missile = new MissileControl(this, 0, false);

            commands["InitMissile"] = (x, y, z) =>
            {
                missile.InitMissile(x, y);
                SyncClock(z);
            };
            commands["Launch"] = (x, y, z) => missile.Launch(x);
            commands["SyncClock"] = (x, y, z) => SyncClock(x);
            commands["RecieveClock"] = (x, y, z) => RecieveClock(x);
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
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

            if (commandLine.TryParse(argument))
            {
                string commandName = commandLine.Argument(0);
                string commandArgument0 = commandLine.Argument(1);
                string commandArgument1 = commandLine.Argument(2);
                string commandArgument2 = commandLine.Argument(3);
                Action<string, string, string> command;

                if (commands.TryGetValue(commandName, out command))
                {
                    try
                    {
                        command(commandArgument0, commandArgument1, commandArgument2);
                    }
                    catch (Exception ex)
                    {
                        Echo("Command had incorrect parameters");
                    }
                }
            }
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
    }
}
