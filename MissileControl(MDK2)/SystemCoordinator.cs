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
            public static double SystemTime { get; private set; }
            public static IMyShipController ReferenceController { get; private set; }
            public static Matrix ReferenceBasis => ReferenceController.WorldMatrix;
            public static Vector3 ReferencePosition => ReferenceController.GetPosition();
            public static Vector3 ReferenceVelocity => ReferenceController.GetShipVelocities().LinearVelocity;
            public static long SelfID => ReferenceController.CubeGrid.EntityId;

            public MyIni Config = new MyIni();
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
            private List<IMyFunctionalBlock> _allBlocks = new List<IMyFunctionalBlock>();

            private Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();
            public SystemCoordinator()
            {
                GetBlocks();
                Init();
            }

            private void Init()
            {
                Config = new MyIni();
                if (!Config.TryParse(_storageBlock.CustomData))
                {
                    Config.Clear();
                    Config.Set("Config", "Type", GetName(MissileType.Unknown));
                    Config.Set("Config", "GuidanceType", GetName(MissileGuidanceType.Unknown));
                    Config.Set("Config", "Payload", GetName(MissilePayload.Unknown));
                    Config.Set("Config", "Mass", "0");
                    Config.Set("Config", "SecureBroadcastPIN", "123456");
                }
                Config.Set("Config", "MissileID", SelfID);
                Config.Set("Config", "MissileAddress", IGCS.Me);

                Type = GetMissileType(Config.Get("Config", "Type").ToString());
                Config.Set("Config", "Type", GetName(Type));

                GuidanceType = GetMissileGuidanceType(Config.Get("Config", "GuidanceType").ToString());
                Config.Set("Config", "GuidanceType", GetName(GuidanceType));

                Payload = GetMissilePayload(Config.Get("Config", "Payload").ToString());
                Config.Set("Config", "Payload", GetName(Payload));

                float missileMass = Config.Get("Config", "Mass").ToSingle(10000);
                Config.Set("Config", "Mass", missileMass);

                long secureBroadcastPIN = Config.Get("Config", "SecureBroadcastPIN").ToInt64(123456);
                Config.Set("Config", "SecureBroadcastPIN", secureBroadcastPIN);

                float maxSpeed = Config.Get("Config", "MaxSpeed").ToSingle(100);
                Config.Set("Config", "MaxSpeed", maxSpeed);

                float m = Config.Get("Config", "M").ToSingle(0.35f);
                Config.Set("Config", "M", m);

                float n = Config.Get("Config", "N").ToSingle(5f);
                Config.Set("Config", "N", n);

                float kp = Config.Get("Config", "Kp").ToSingle(2.5f);
                Config.Set("Config", "Kp", kp);

                float ki = Config.Get("Config", "Ki").ToSingle(0f);
                Config.Set("Config", "Ki", ki);

                float kd = Config.Get("Config", "Kd").ToSingle(0f);
                Config.Set("Config", "Kd", kd);

                _storageBlock.CustomData = Config.ToString();
                MissileControl = new MissileControl(0, missileMass, maxSpeed, Type, GuidanceType, Payload, m, n, kp, ki, kd);
                CommunicationHandler = new CommunicationHandler(0, secureBroadcastPIN);

                CommandHandler = new CommandHandler(MePB, _commands);

                CommunicationHandler.RegisterTag("MyMissileLauncherInfo", true);
                CommunicationHandler.RegisterTag("MyMissileTargetInfo", true);
                CommunicationHandler.RegisterTag("MyMissileCommands", true);
                _commands["SYNC_CLOCK"] = (args) => SyncClock(args[0]);
                _commands["ON"] = (args) => TurnOn();
                _commands["OFF"] = (args) => TurnOff();
                _commands["ACTIVATE"] = (args) => ActivateMissile(args[0], args[1]);
                _commands["LAUNCH"] = (args) => LaunchMissile();
                _commands["ABORT"] = (args) => AbortMissile();
            }

            private void GetBlocks()
            {
                GTS.GetBlocksOfType(_allBlocks, b => b.IsSameConstructAs(MePB));
                
                _storageBlock = _allBlocks.Find(b => b.CustomData.Contains("[Config]"));
                if (_storageBlock == null) throw new Exception("No block with [Config] in CustomData found on this construct.");

                ReferenceController = _allBlocks.Find(b => b is IMyShipController) as IMyShipController;
                if (ReferenceController == null) throw new Exception("No Control block found on this construct.");
            }

            public void Run()
            {
                SystemTime += RuntimeInfo.TimeSinceLastRun.TotalSeconds;
                DebugEcho(SystemTime.ToString());
                CommunicationHandler.Recieve();
                CommandHandler.RunCustomDataCommands();

                while (CommunicationHandler.HasMessage("MyMissileLauncherInfo", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("MyMissileLauncherInfo", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        object msgObject = Deserializer.Deserialize(msg.Data as string);
                        if (msgObject is EntityInfo)
                        {
                            Launcher = (EntityInfo)msgObject;
                        }
                    }
                }

                while (CommunicationHandler.HasMessage("MyMissileTargetInfo", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("MyMissileTargetInfo", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        object msgObject = Deserializer.Deserialize(msg.Data as string);
                        if (msgObject is EntityInfo)
                        {
                            Target = (EntityInfo)msgObject;
                        }
                    }
                }

                while (CommunicationHandler.HasMessage("MyMissileCommands", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("MyMissileCommands", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        object msgObject = Deserializer.Deserialize(msg.Data as string);
                        if (msgObject is string)
                        {
                            Command((string)msgObject);
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

                    CommunicationHandler.SendUnicast(Self.Serialize(), LauncherAddress, "MyMissiles", true);
                    CommunicationHandler.SendBroadcast(selfLite.Serialize(), "AllMissiles", false);
                }
                
                if (MissileControl.Stage >= MissileStage.Flying && !CommunicationHandler.CanReach(LauncherAddress))
                {
                    AbortMissile();
                }
            }

            public bool Command(string command)
            {
                return CommandHandler.RunCommands(command);
            }

            private bool SyncClock(string timeString)
            {
                double time;
                if (double.TryParse(timeString, out time))
                {
                    SystemTime = time;
                    return true;
                }
                return false;
            }

            private bool TurnOn()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.Update1;
                return true;
            }

            private bool TurnOff()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.None;
                _allBlocks.ForEach(b => { if (!ReferenceEquals(b, MePB)) b.Enabled = false; });
                return true;
            }

            private bool ActivateMissile(string launcherAddressString, string timeString)
            {
                _allBlocks.ForEach(b => b.Enabled = true);
                long launcherAddress;
                if (!long.TryParse(launcherAddressString, out launcherAddress)) return false;
                LauncherAddress = launcherAddress;
                SyncClock(timeString);
                return MissileControl.Activate();
            }

            private bool LaunchMissile()
            {
                return MissileControl.Launch();
            }

            private bool AbortMissile()
            {
                return MissileControl.Abort();
            }
        }
    }
}
