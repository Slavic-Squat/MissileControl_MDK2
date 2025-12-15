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
        private SystemCoordinator _systemCoordinator;
        public static Action<string> DebugEcho { get; private set; }
        public static Action<string, bool> DebugWrite { get; private set; }
        public static IMyProgrammableBlock MePB { get; private set; }
        public static IMyGridTerminalSystem GTS { get; private set; }
        public static List<IMyTerminalBlock> AllGridBlocks { get; private set; } = new List<IMyTerminalBlock>();
        public static IMyIntergridCommunicationSystem IGCS { get; private set; }
        public static IMyGridProgramRuntimeInfo RuntimeInfo { get; private set; }
        public static double SystemTime { get; private set; }
        public static MyIni Config { get; private set; }
        public static CommunicationHandler CommunicationHandler0 { get; private set; }
        public static CommandHandler CommandHandler0 { get; private set; }

        public static int DebugCounter { get; set; } = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            GTS = GridTerminalSystem;
            IGCS = IGC;
            MePB = Me;
            DebugEcho = Echo;
            DebugWrite = (s, b) => Me.GetSurface(0).WriteText(s, b);
            RuntimeInfo = Runtime;

            GridTerminalSystem.GetBlocksOfType(AllGridBlocks, b => b.IsSameConstructAs(Me));

            Config = new MyIni();
            if (!Config.TryParse(MePB.CustomData))
            {
                Config.Clear();
            }

            long secureBroadcastPIN = Config.Get("Config", "SecureBroadcastPIN").ToInt64(123456);
            Config.Set("Config", "SecureBroadcastPIN", secureBroadcastPIN);
            CommunicationHandler0 = new CommunicationHandler(0, secureBroadcastPIN);
            CommandHandler0 = new CommandHandler();
            _systemCoordinator = new SystemCoordinator();
            MePB.CustomData = Config.ToString();
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            SystemTime += RuntimeInfo.TimeSinceLastRun.TotalSeconds;
            DebugEcho($"System Time: {SystemTime:F2}s\n");
            DebugWrite($"System Time: {SystemTime:F2}s\n", false);
            DebugEcho($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n");
            DebugWrite($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n", true);

            if (argument != null)
            {
                _systemCoordinator.Command(argument);
            }
            CommunicationHandler0.Receive();
            _systemCoordinator.Run(SystemTime);
        }
    }
}
