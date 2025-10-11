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
            public static DateTime SystemTime { get; private set; }
            public static Matrix ReferenceBasis => _referenceBlock.WorldMatrix;
            public static long SelfID => _referenceBlock.CubeGrid.EntityId;
            public static long SelfAddress => IGCS.Me;

            public CommunicationHandler CommunicationHandler { get; private set; }
            public CommandHandler CommandHandler { get; private set; }
            public MissileControl MissileControl { get; private set; }
            public MyIni Config { get; private set; }

            public long LauncherID { get; private set; }
            public long LauncherAddress { get; private set; }
            public MissileStage Stage => MissileControl.Stage;
            public MissileType Type { get; private set; }
            public MissileGuidanceType GuidanceType { get; private set; }
            public MissilePayload Payload { get; private set; }
            public EntityInfo Self { get; private set; }
            public EntityInfo Target { get; private set; }
            public EntityInfo Launcher { get; private set; }

            private IMyTerminalBlock _storageBlock;
            private static IMyCubeBlock _referenceBlock;

            private Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();
            public SystemCoordinator()
            {
                SystemTime = DateTime.Now;
                TryGetBlocks();

                CommunicationHandler = new CommunicationHandler(0);
                CommandHandler = new CommandHandler(MePB, _commands);
                MissileControl = new MissileControl(0);
                Config = new MyIni();

                Init();
                CommunicationHandler.RegisterTag("LauncherInfo");
                CommunicationHandler.RegisterTag("TargetInfo");
                CommunicationHandler.RegisterTag("Commands");
                _commands.Add("SYNC_CLOCK", (args) => SyncClock(args[0]));
            }

            public void Init()
            {
                Config.TryParse(_storageBlock.CustomData);
                Config.Set("Data", "MissileID", SelfID);
                Config.Set("Data", "MissileAddress", SelfAddress);
                MissileType type;
                Enum.TryParse(Config.Get("Data", "Type").ToString(), out type);
                Type = type;
                MissileGuidanceType guidanceType;
                Enum.TryParse(Config.Get("Data", "GuidanceType").ToString(), out guidanceType);
                GuidanceType = guidanceType;
                MissilePayload payload;
                Enum.TryParse(Config.Get("Data", "Payload").ToString(), out payload);
                Payload = payload;

                MissileControl.Type = Type;
                MissileControl.GuidanceType = GuidanceType;
                MissileControl.Payload = Payload;
            }

            public bool TryGetBlocks()
            {
                try
                {
                    List<IMyTerminalBlock> tBlocks = new List<IMyTerminalBlock>();
                    GTS.GetBlocksOfType(tBlocks, b => b.IsSameConstructAs(MePB) && b.CustomData.Contains("[Data]"));
                    if (tBlocks.Count == 0) throw new Exception("No block with [Data] in CustomData found on this construct.");

                    List<IMyRemoteControl> rcBlocks = new List<IMyRemoteControl>();
                    GTS.GetBlocksOfType(rcBlocks, b => b.IsSameConstructAs(MePB) && b.IsMainCockpit);
                    if (rcBlocks.Count == 0) throw new Exception("No Remote Control block found on this construct.");
                    _referenceBlock = rcBlocks[0];
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
                CommunicationHandler.Recieve();
                CommandHandler.RunCustomDataCommands();

                while (CommunicationHandler.HasMessage("LauncherInfo"))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("LauncherInfo", out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        object msgObject = Deserializer.Deserialize(msg.Data as string);
                        if (msgObject is EntityInfo)
                        {
                            var entity = (EntityInfo)msgObject;
                            if (entity.EntityID == LauncherID)
                            {
                                Launcher = entity;
                            }
                        }
                    }
                }

                while (CommunicationHandler.HasMessage("TargetInfo"))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("TargetInfo", out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        object msgObject = Deserializer.Deserialize(msg.Data as string);
                        if (msgObject is EntityInfo)
                        {
                            Target = (EntityInfo)msgObject;
                        }
                    }
                }

                if (MissileControl.Stage > MissileStage.Idle)
                {
                    MissileControl.Target = Target;
                    MissileControl.Launcher = Launcher;
                    MissileControl.Run(SystemTime);

                    MissileInfo missileInfo = new MissileInfo(LauncherID, Target.EntityID, Stage, Type, GuidanceType, Payload);
                    Self = new EntityInfo(SelfID, MissileControl.MissilePos, MissileControl.MissileVel, MissileControl.LasRunTime, missileInfo);

                    CommunicationHandler.SendUnicast(Self.Serialize(), LauncherAddress, "MyMissiles");
                }                
            }

            public void Command(string command)
            {
                CommandHandler.TryRunCommands(command);
            }

            public void SyncClock(string timeString)
            {
                DateTime time;
                if (DateTime.TryParse(timeString, out time))
                {
                    SystemTime = time;
                }
            }

            public void ActivateMissile()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.Update1;
                MissileControl.Activate();

                Config.TryParse(_storageBlock.CustomData);
                LauncherID = Config.Get("Data", "LauncherID").ToInt64(0);
                LauncherAddress = Config.Get("Data", "LauncherAddress").ToInt64(0);
            }
        }
    }
}
