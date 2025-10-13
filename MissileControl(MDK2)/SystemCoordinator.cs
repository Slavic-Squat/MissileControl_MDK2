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
            public static IMyShipController ReferenceController { get; private set; }
            public static Matrix ReferenceBasis => ReferenceController.WorldMatrix;
            public static Vector3 ReferencePosition => ReferenceController.GetPosition();
            public static Vector3 ReferenceVelocity => ReferenceController.GetShipVelocities().LinearVelocity;
            public static long SelfID => ReferenceController.CubeGrid.EntityId;

            public CommunicationHandler CommunicationHandler { get; private set; }
            public CommandHandler CommandHandler { get; private set; }
            public MissileControl MissileControl { get; private set; }

            public long LauncherAddress { get; private set; }
            public MissileStage Stage => MissileControl.Stage;
            public MissileType Type { get; private set; }
            public MissileGuidanceType GuidanceType { get; private set; }
            public MissilePayload Payload { get; private set; }
            public EntityInfo Self { get; private set; }
            public EntityInfo Target { get; private set; }
            public EntityInfo Launcher { get; private set; }

            private IMyTerminalBlock _storageBlock;

            private Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();
            public SystemCoordinator()
            {
                SystemTime = DateTime.Now;
                GetBlocks();
                Init();

                CommunicationHandler = new CommunicationHandler(0);
                CommandHandler = new CommandHandler(MePB, _commands);
                
                CommunicationHandler.RegisterTag("LauncherInfo");
                CommunicationHandler.RegisterTag("TargetInfo");
                CommunicationHandler.RegisterTag("Commands");
                _commands.Add("SYNC_CLOCK", (args) => SyncClock(args[0]));
                _commands.Add("ON", (args) => TurnOn());
                _commands.Add("OFF", (args) => TurnOff());
                _commands.Add("ACTIVATE", (args) => ActivateMissile(args[0], args[1]));
                _commands.Add("LAUNCH", (args) => LaunchMissile());
            }

            private void Init()
            {
                MyIni Config = new MyIni();
                Config.TryParse(_storageBlock.CustomData);
                Config.Set("Config", "MissileID", SelfID);
                Config.Set("Config", "MissileAddress", IGCS.Me);
                Type = GetMissileType(Config.Get("Config", "Type").ToString());
                Config.Set("Config", "Type", GetName(Type));
                GuidanceType = GetMissileGuidanceType(Config.Get("Config", "GuidanceType").ToString());
                Config.Set("Config", "GuidanceType", GetName(GuidanceType));
                Payload = GetMissilePayload(Config.Get("Config", "Payload").ToString());
                Config.Set("Config", "Payload", GetName(Payload));

                float missileMass = Config.Get("Config", "Mass").ToSingle(10000);
                Config.Set("Config", "Mass", missileMass.ToString());

                _storageBlock.CustomData = Config.ToString();
                MissileControl = new MissileControl(0, missileMass, Type, GuidanceType, Payload);
            }

            private void GetBlocks()
            {
                List<IMyTerminalBlock> tBlocks = new List<IMyTerminalBlock>();
                GTS.GetBlocksOfType(tBlocks, b => b.IsSameConstructAs(MePB) && b.CustomData.Contains("[Config]"));
                if (tBlocks.Count == 0) throw new Exception("No block with [Data] in CustomData found on this construct.");
                _storageBlock = tBlocks[0];

                List<IMyShipController> ctrlBlocks = new List<IMyShipController>();
                GTS.GetBlocksOfType(ctrlBlocks, b => b.IsSameConstructAs(MePB));
                if (ctrlBlocks.Count == 0) throw new Exception("No Control block found on this construct.");
                ReferenceController = ctrlBlocks[0];
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
                            Launcher = (EntityInfo)msgObject;
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
                    MissileControl.UpdateTarget(Target);
                    MissileControl.UpdateLauncher(Launcher);
                    MissileControl.Run(SystemTime);

                    MissileInfo missileInfo = new MissileInfo(Launcher.EntityID, Target.EntityID, Stage, Type, GuidanceType, Payload);
                    MissileInfoLite missileInfoLite = new MissileInfoLite(Launcher.EntityID);
                    Self = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, SystemTime, missileInfo);
                    EntityInfo selfLite = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, SystemTime, missileInfoLite);

                    CommunicationHandler.SendUnicast(Self.Serialize(), LauncherAddress, "MyMissiles");
                    CommunicationHandler.SendBroadcast(selfLite.Serialize(), "AllMissiles");
                }                
            }

            public void Command(string command)
            {
                CommandHandler.RunCommands(command);
            }

            public void SyncClock(string timeStringTicks)
            {
                long timeTicks;
                if (long.TryParse(timeStringTicks, out timeTicks))
                {
                    SystemTime = new DateTime(timeTicks);
                }
            }

            public void TurnOn()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.Update1;
            }

            public void TurnOff()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.None;
            }

            public void ActivateMissile(string launcherAddressString, string timeStringTicks)
            {
                long launcherAddress;
                if (!long.TryParse(launcherAddressString, out launcherAddress)) return;
                LauncherAddress = launcherAddress;
                SyncClock(timeStringTicks);
                MissileControl.Activate();
            }

            public void LaunchMissile()
            {
                MissileControl.Launch();
            }
        }
    }
}
