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
            public static double GlobalTime { get; private set; }
            public static IMyShipController ReferenceController { get; private set; }
            public static Matrix ReferenceWorldMatrix => ReferenceController.WorldMatrix;
            public static Vector3 ReferencePosition => ReferenceController.GetPosition();
            public static Vector3 ReferenceVelocity => ReferenceController.GetShipVelocities().LinearVelocity;
            public static long SelfID => ReferenceController.CubeGrid.EntityId;

            public MissileControl MissileControl { get; private set; }
            public long LauncherAddress { get; private set; }
            public long LauncherID { get; private set; }
            public MissileStage Stage => MissileControl.Stage;
            public MissileType Type { get; private set; }
            public MissileGuidanceType GuidanceType { get; private set; }
            public MissilePayload Payload { get; private set; }
            public EntityInfo Self { get; private set; }
            public EntityInfo Target { get; private set; }
            public double Time { get; private set; }

            private double _globalTimeOffset;
            private IMySoundBlock _soundBlock;
            private List<IMyFunctionalBlock> _functionalBlocks = new List<IMyFunctionalBlock>();
            public SystemCoordinator()
            {
                GetBlocks();
                Init();
            }

            private void Init()
            {
                Config.Set("Config", "MissileID", SelfID);
                Config.Set("Config", "MissileAddress", IGCS.Me);

                Type = MissileEnumHelper.GetMissileType(Config.Get("Config", "Type").ToString(MissileEnumHelper.GetDisplayString(MissileType.Unknown)));
                Config.Set("Config", "Type", MissileEnumHelper.GetDisplayString(Type));

                GuidanceType = MissileEnumHelper.GetMissileGuidanceType(Config.Get("Config", "GuidanceType").ToString(MissileEnumHelper.GetDisplayString(MissileGuidanceType.Unknown)));
                Config.Set("Config", "GuidanceType", MissileEnumHelper.GetDisplayString(GuidanceType));

                Payload = MissileEnumHelper.GetMissilePayload(Config.Get("Config", "Payload").ToString(MissileEnumHelper.GetDisplayString(MissilePayload.Unknown)));
                Config.Set("Config", "Payload", MissileEnumHelper.GetDisplayString(Payload));

                float missileMass = Config.Get("Config", "Mass").ToSingle(10000);
                Config.Set("Config", "Mass", missileMass);

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

                Direction launchDirection = MiscEnumHelper.GetDirection(Config.Get("Config", "LaunchDirection").ToString("FORWARD"));
                Config.Set("Config", "LaunchDirection", MiscEnumHelper.GetDirectionStr(launchDirection));

                MissileControl = new MissileControl(missileMass, maxSpeed, Type, GuidanceType, Payload, m, n, kp, ki, kd, launchDirection);

                CommunicationHandler0.RegisterTag("TargetInfo", true);
                CommunicationHandler0.RegisterTag("Commands", true);
                CommandHandler0.RegisterCommand("SYNC_CLOCK", (args) => SyncClock(args[0]));
                CommandHandler0.RegisterCommand("ON", (args) => TurnOn());
                CommandHandler0.RegisterCommand("OFF", (args) => TurnOff());
                CommandHandler0.RegisterCommand("ACTIVATE", (args) => ActivateMissile(args[0], args[1], args[2]));
                CommandHandler0.RegisterCommand("DEACTIVATE", (args) => DeactivateMissile());
                CommandHandler0.RegisterCommand("LAUNCH", (args) => LaunchMissile());
                CommandHandler0.RegisterCommand("ABORT", (args) => AbortMissile());
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

            public void Run(double time)
            {
                if (Time == 0)
                {
                    Time = time;
                    return;
                }

                GlobalTime = time + _globalTimeOffset;

                while (CommunicationHandler0.HasMessage("TargetInfo", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler0.TryRetrieveMessage("TargetInfo", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        byte[] bytes = Convert.FromBase64String(msg.Data as string);
                        Target = EntityInfo.Deserialize(bytes, 0);
                    }
                }

                while (CommunicationHandler0.HasMessage("Commands", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler0.TryRetrieveMessage("Commands", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        string command = msg.Data as string;
                        CommandHandler0.RunCommands(command);
                    }
                }

                MissileControl.UpdateTarget(Target);
                MissileControl.Run(time);

                if (MissileControl.Stage > MissileStage.Launching)
                {
                    MissileInfo missileInfo = new MissileInfo(LauncherID, Target.EntityID, Stage, Type, GuidanceType, Payload);
                    MissileInfoLite missileInfoLite = new MissileInfoLite(LauncherID);
                    Self = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missileInfo);
                    EntityInfo selfLite = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missileInfoLite);

                    CommunicationHandler0.SendUnicast(Self.Serialize(), LauncherAddress, "MyMissileInfo", true);
                    CommunicationHandler0.SendBroadcast(selfLite.Serialize(), "AllMissileInfo", false);
                }

                if (MissileControl.Stage >= MissileStage.Flying && (!CommunicationHandler0.CanReach(LauncherAddress) || !Target.IsValid))
                {
                    AbortMissile();
                }
            }

            private void SyncClock(string timeString)
            {
                double time;
                if (double.TryParse(timeString, out time))
                {
                    _globalTimeOffset = time - Time;
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

            private void ActivateMissile(string launcherAddressString, string launcherIDString, string timeString)
            {
                _functionalBlocks.ForEach(b => b.Enabled = true);
                long launcherAddress;
                if (!long.TryParse(launcherAddressString, out launcherAddress)) return;
                long launcherID;
                if (!long.TryParse(launcherIDString, out launcherID)) return;
                LauncherAddress = launcherAddress;
                LauncherID = launcherID;
                SyncClock(timeString);
                MissileControl.Activate();
            }

            private void DeactivateMissile()
            {
                _functionalBlocks.ForEach(b => { if (!ReferenceEquals(b, MePB) && !(b is IMyShipConnector)) b.Enabled = false; });
                MissileControl.Deactivate();
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
