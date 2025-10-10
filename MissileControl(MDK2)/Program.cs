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
        private CommandHandler _commandHandler;
        private Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>>();
        private SystemCoordinator _systemCoordinator;
        public static IMyGridTerminalSystem GTS { get; private set; }
        public static IMyIntergridCommunicationSystem IGCS { get; private set; }
        public static IMyProgrammableBlock MePB { get; private set; }
        public static Action<string> DebugEcho { get; private set; }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;
            GTS = GridTerminalSystem;
            IGCS = IGC;
            MePB = Me;
            DebugEcho = Echo;


            List<IMyRemoteControl> controlBlocks = new List<IMyRemoteControl>();
            GTS.GetBlocksOfType(controlBlocks, b => b.IsSameConstructAs(Me) && b.CustomData.Contains("Missile Controller"));
            var controlBlock = controlBlocks.FirstOrDefault();

            _systemCoordinator = new SystemCoordinator(controlBlock);
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            
        }
    }
}
