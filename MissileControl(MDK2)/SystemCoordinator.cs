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
            public static double GlobalTime { get; private set; }
            public static IMyShipController ReferenceController { get; private set; }
            public static Matrix ReferenceWorldMatrix => ReferenceController.WorldMatrix;
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

            private IMySoundBlock _soundBlock;
            private List<IMyFunctionalBlock> _functionalBlocks = new List<IMyFunctionalBlock>();
            public SystemCoordinator()
            {
                GetBlocks();
                Init();
            }

            private void Init()
            {
                Config = new MyIni();
                if (!Config.TryParse(MePB.CustomData))
                {
                    Config.Clear();
                }
                Config.Set("Config", "MissileID", SelfID);
                Config.Set("Config", "MissileAddress", IGCS.Me);

                Type = GetMissileType(Config.Get("Config", "Type").ToString(GetName(MissileType.Unknown)));
                Config.Set("Config", "Type", GetName(Type));

                GuidanceType = GetMissileGuidanceType(Config.Get("Config", "GuidanceType").ToString(GetName(MissileGuidanceType.Unknown)));
                Config.Set("Config", "GuidanceType", GetName(GuidanceType));

                Payload = GetMissilePayload(Config.Get("Config", "Payload").ToString(GetName(MissilePayload.Unknown)));
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

                MePB.CustomData = Config.ToString();
                MissileControl = new MissileControl(0, missileMass, maxSpeed, Type, GuidanceType, Payload, m, n, kp, ki, kd);
                CommunicationHandler = new CommunicationHandler(0, secureBroadcastPIN);

                CommandHandler = new CommandHandler();

                CommunicationHandler.RegisterTag("LauncherInfo", true);
                CommunicationHandler.RegisterTag("TargetInfo", true);
                CommunicationHandler.RegisterTag("Commands", true);
                CommandHandler.RegisterCommand("SYNC_CLOCK", (args) => SyncClock(args[0]));
                CommandHandler.RegisterCommand("ON", (args) => TurnOn());
                CommandHandler.RegisterCommand("OFF", (args) => TurnOff());
                CommandHandler.RegisterCommand("ACTIVATE", (args) => ActivateMissile(args[0], args[1]));
                CommandHandler.RegisterCommand("DEACTIVATE", (args) => DeactivateMissile());
                CommandHandler.RegisterCommand("LAUNCH", (args) => LaunchMissile());
                CommandHandler.RegisterCommand("ABORT", (args) => AbortMissile());
            }

            private void GetBlocks()
            {
                ReferenceController = AllGridBlocks.Find(b => b is IMyShipController && b.CustomName.Contains("Missile Controller")) as IMyShipController;
                if (ReferenceController == null)
                {
                    DebugWrite("Error: missile controller not found!\n", true);
                    throw new Exception("missile controller not found!\n");
                }

                _soundBlock = AllGridBlocks.Find(b => b is IMySoundBlock) as IMySoundBlock;

                _functionalBlocks = AllGridBlocks.Where(b => b is IMyFunctionalBlock).Cast<IMyFunctionalBlock>().ToList();
            }

            public void Run()
            {
                SystemTime += RuntimeInfo.TimeSinceLastRun.TotalSeconds;
                GlobalTime += RuntimeInfo.TimeSinceLastRun.TotalSeconds;
                DebugEcho($"System Time: {SystemTime:F2}s\n");
                DebugWrite($"System Time: {SystemTime:F2}s\n", false);
                DebugEcho($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n");
                DebugWrite($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n", true);
                CommunicationHandler.Recieve();

                while (CommunicationHandler.HasMessage("LauncherInfo", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("LauncherInfo", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        byte[] bytes = Convert.FromBase64String(msg.Data as string);
                        Launcher = EntityInfo.Deserialize(bytes, 0);
                    }
                }

                while (CommunicationHandler.HasMessage("TargetInfo", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("TargetInfo", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        byte[] bytes = Convert.FromBase64String(msg.Data as string);
                        Target = EntityInfo.Deserialize(bytes, 0);
                    }
                }

                while (CommunicationHandler.HasMessage("Commands", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler.TryRetrieveMessage("Commands", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        string command = msg.Data as string;
                        Command(command);
                    }
                }

                MissileControl.UpdateTarget(Target);
                MissileControl.UpdateLauncher(Launcher);
                MissileControl.Run(SystemTime);

                if (MissileControl.Stage > MissileStage.Launching)
                {
                    MissileInfo missileInfo = new MissileInfo(Launcher.EntityID, Target.EntityID, Stage, Type, GuidanceType, Payload);
                    MissileInfoLite missileInfoLite = new MissileInfoLite(Launcher.EntityID);
                    Self = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, SystemTime, missileInfo);
                    EntityInfo selfLite = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, SystemTime, missileInfoLite);

                    CommunicationHandler.SendUnicast(Self.Serialize(), LauncherAddress, "MyMissileInfo", true);
                    CommunicationHandler.SendBroadcast(selfLite.Serialize(), "AllMissileInfo", false);
                }
                
                if (MissileControl.Stage >= MissileStage.Flying && !CommunicationHandler.CanReach(LauncherAddress))
                {
                    AbortMissile();
                }
            }

            public void Command(string command)
            {
                CommandHandler.RunCommands(command);
            }

            private void SyncClock(string timeString)
            {
                double time;
                if (double.TryParse(timeString, out time))
                {
                    GlobalTime = time;
                }
            }

            private void TurnOn()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.Update1;
            }

            private void TurnOff()
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.None;
            }

            private void ActivateMissile(string launcherAddressString, string timeString)
            {
                _functionalBlocks.ForEach(b => b.Enabled = true);
                long launcherAddress;
                if (!long.TryParse(launcherAddressString, out launcherAddress)) return;
                LauncherAddress = launcherAddress;
                SyncClock(timeString);
                MissileControl.Activate();
            }

            private void DeactivateMissile()
            {
                _functionalBlocks.ForEach(b => { if (!ReferenceEquals(b, MePB)) b.Enabled = false; });
            }

            private void LaunchMissile()
            {
                if (_soundBlock != null)
                {
                    Random rand = new Random();

                    int num = rand.Next(0, 10);

                    switch (num)
                    {
                        case 0:
                            _soundBlock.SelectedSound = "Missile 0";
                            _soundBlock.Play();
                            break;
                        case 1:
                            _soundBlock.SelectedSound = "Missile 1";
                            _soundBlock.Play();
                            break;
                        default:
                            break;
                    }
                }
                MissileControl.Launch();
            }

            private void AbortMissile()
            {
                MissileControl.Abort();
            }
        }
    }
}
