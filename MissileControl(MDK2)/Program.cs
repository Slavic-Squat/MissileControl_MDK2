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
        #region Command Control
        private CommandHandler commandHandler;
        private Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();
        #endregion

        #region Broadcast Info
        private IMyBroadcastListener broadcastListener;
        private string broadcastTag;
        #endregion

        #region State Info
        private DateTime time;
        private bool listeningForClock = false;
        #endregion

        private MissileControl missile;

        public Program()
        {
            missile = new MissileControl(this, 0, false);

            commands["InitMissile"] = (args) =>
            {
                missile.InitMissile(args[0], args[1]);
                SyncClock(args[2]);
            };
            commands["Launch"] = (args) => missile.Launch(args[0]);
            commands["SyncClock"] = (args) => SyncClock(args[0]);
            commands["RecieveClock"] = (args) => RecieveClock(args[0]);

            commandHandler = new CommandHandler(Me, commands);
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != null)
            {
                commandHandler.TryRunCommand(argument);
            }
            commandHandler.Run();

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
    }
}
