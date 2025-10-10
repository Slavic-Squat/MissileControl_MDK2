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
        public class SystemCoordinator
        {
            public CommunicationHandler CommunicationHandler { get; private set; }
            public CommandHandler CommandHandler { get; private set; }
            public MissileControl MissileControl { get; private set; }
            public MyIni Config { get; private set; }
            public DateTime SystemTime { get; private set; }

            public long LauncherID { get; private set; }
            public long SelfAddress => CommunicationHandler.SelfAddress;
            public long LauncherAddress { get; private set; }
            public MissileStage Stage => MissileControl.Stage;
            public MissileType Type { get; private set; }
            public MissileGuidanceType GuidanceType { get; private set; }
            public MissilePayload Payload { get; private set; }
            public long TargetID { get; private set; }
            public EntityInfo Self => MissileControl.Self;
            public EntityInfo Target { get; private set; }
            public EntityInfo Launcher { get; private set; }

            private IMyTerminalBlock _storageBlock;
            public SystemCoordinator()
            {
                SystemTime = DateTime.Now;
                CommunicationHandler = new CommunicationHandler(0);
                //MissileControl = new MissileControl(0);
                Config = new MyIni();
            }

            public void Init()
            {
                TryGetBlocks();

                Config.TryParse(_storageBlock.CustomData);
                LauncherID = Config.Get("Data", "LauncherID").ToInt64(0);
                LauncherAddress = Config.Get("Data", "LauncherAddress").ToInt64(0);
                MissileType type;
                Enum.TryParse(Config.Get("Data", "Type").ToString(), out type);
                Type = type;
                MissileGuidanceType guidanceType;
                Enum.TryParse(Config.Get("Data", "GuidanceType").ToString(), out guidanceType);
                GuidanceType = guidanceType;
                MissilePayload payload;
                Enum.TryParse(Config.Get("Data", "Payload").ToString(), out payload);
                Payload = payload;

            }

            public bool TryGetBlocks()
            {
                try
                {
                    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                    GTS.GetBlocksOfType(blocks, b => b.IsSameConstructAs(MePB) && b.CustomData.Contains("[Data]"));
                    if (blocks.Count == 0) throw new Exception("No block with [Data] in CustomData found on this construct.");
                    return true;
                }
                catch (Exception e)
                {
                    DebugEcho(e.Message);
                    throw;
                }
            }

            public void Run()
            {
                SystemTime += RuntimeInfo.TimeSinceLastRun;
                DebugEcho(SystemTime.ToString());
                //MissileControl.Target = Target;
                //MissileControl.Launcher = Launcher;
                CommunicationHandler.Recieve();
                //MissileControl.Run(time);
            }

            public void Command(string argument)
            {

            }

            public void SyncClock(string timeString)
            {
                DateTime time;
                if (DateTime.TryParse(timeString, out time))
                {
                    SystemTime = time;
                }
            }
        }
    }
}
