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
        public static List<IMyTerminalBlock> AllGridBlocks = new List<IMyTerminalBlock>();
        public static IMyIntergridCommunicationSystem IGCS { get; private set; }
        public static IMyGridProgramRuntimeInfo RuntimeInfo { get; private set; }

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

            _systemCoordinator = new SystemCoordinator();
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument != null)
            {
                _systemCoordinator.Command(argument);
            }
            _systemCoordinator.Run();
        }
    }
}
