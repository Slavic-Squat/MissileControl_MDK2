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
        public class MissileControl
        {
            #region General Info
            private Program program;
            private int ID;
            private string missileTag;
            private bool clusterMissile;
            #endregion

            #region Broadcast Info
            private string launcherTag;
            private long selectedTargetID;
            private IMyBroadcastListener targetsInfoListener;
            private IMyBroadcastListener launcherInfoListener;
            private ImmutableDictionary<long, MyTuple<Vector3, Vector3, long>> targetsInfo;
            private MyTuple<string, Vector3, Vector3, long> launcherInfo;
            #endregion

            #region Parts
            private List<IMyGyro> gyros = new List<IMyGyro>();
            private IMyRemoteControl remoteControl;
            private IMyShipMergeBlock mergeBlock;
            private List<IMyWarhead> payload = new List<IMyWarhead>();
            private List<IMyMotorStator> attachmentRotors = new List<IMyMotorStator>();
            private List<IMyThrust> thrusters = new List<IMyThrust>();
            #endregion

            #region Controllers
            private MissileGuidance missileGuidance;
            private PIDControl pitchController;
            private PIDControl yawController;
            #endregion

            #region Properties
            private float maxForwardThrust;
            private float maxBackwardThrust;
            private float maxLeftwardThrust;
            private float maxRightwardThrust;
            private float maxUpwardThrust;
            private float maxDownwardThrust;

            private float missileMass;

            private float maxForwardAccel;
            private float maxRadialAccel;

            private Dictionary<IMyThrust, MyTuple<Vector3, Direction>> thrusterInfo = new Dictionary<IMyThrust, MyTuple<Vector3, Direction>>();
            #endregion

            #region State Info
            private DateTime time;
            private bool active;
            public Stage stage = Stage.Idle;

            private Vector3 missilePosition;
            private Vector3 missileVelocity;

            private Vector3 targetPosition;
            private Vector3 targetVelocity;
            private DateTime targetInfoTime;

            private Vector3 launcherPosition;
            private Vector3 launcherVelocity;
            private DateTime launcherInfoTime;

            private Vector3 relativeTargetPosition;
            private Vector3 closingVelocity;
            private float distanceToTarget;
            private float closingSpeed;
            private float timeToTarget;

            private Vector3 localTotalAcceleration;
            private Vector3 localVectorToAlign;
            #endregion

            private enum Direction
            {
                Forward, Backward, Leftward, Rightward, Upward, Downward
            }

            public enum Stage
            {
                Idle, Launching, Flying, Interception, Detonation
            }

            public MissileControl(Program program, int ID, bool clusterMissile)
            {
                this.program = program;
                this.ID = ID;
                this.clusterMissile = clusterMissile;

                program.GridTerminalSystem.GetBlockGroupWithName($"Missile Thrusters [{ID}]").GetBlocksOfType<IMyThrust>(thrusters);
                program.GridTerminalSystem.GetBlockGroupWithName($"Missile Gyros [{ID}]").GetBlocksOfType<IMyGyro>(gyros);
                remoteControl = (IMyRemoteControl)program.GridTerminalSystem.GetBlockWithName($"Missile Controller [{ID}]");
                mergeBlock = (IMyShipMergeBlock)program.GridTerminalSystem.GetBlockWithName($"Missile Merge Block [{ID}]");
                program.GridTerminalSystem.GetBlockGroupWithName($"Payload [{ID}]").GetBlocksOfType(payload);

                if (clusterMissile == true)
                {
                    program.GridTerminalSystem.GetBlockGroupWithName($"Attachment Rotors [{ID}]").GetBlocksOfType(attachmentRotors);
                }

                Init();

                pitchController = new PIDControl(1.0f, 0, 0.2f);
                yawController = new PIDControl(1.0f, 0, 0.2f);

                missileGuidance = new MissileGuidance(maxForwardAccel, maxRadialAccel, 3.5f, 10);
            }

            public void Init()
            {
                foreach (IMyThrust thruster in thrusters)
                {
                    Vector3D localThrusterDirection = Vector3.Round(Vector3.TransformNormal(thruster.WorldMatrix.Backward, Matrix.Transpose(remoteControl.WorldMatrix)), 1);

                    if (localThrusterDirection.Z == -1)
                    {
                        maxForwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Forward));
                    }
                    else if (localThrusterDirection.Z == 1)
                    {
                        maxBackwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Backward));
                    }
                    else if (localThrusterDirection.X == -1)
                    {
                        maxLeftwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Leftward));
                    }
                    else if (localThrusterDirection.X == 1)
                    {
                        maxRightwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Rightward));
                    }
                    else if (localThrusterDirection.Y == 1)
                    {
                        maxUpwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Upward));
                    }
                    else if (localThrusterDirection.Y == -1)
                    {
                        maxDownwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Downward));
                    }
                }

                missileMass = remoteControl.CalculateShipMass().PhysicalMass;
                maxForwardAccel = maxForwardThrust / missileMass;
                maxRadialAccel = maxRightwardThrust / missileMass;
            }

            public void Run(DateTime time)
            {
                if (active)
                {
                    float timeDelta = (float)program.Runtime.TimeSinceLastRun.TotalSeconds;

                    missileMass = remoteControl.CalculateShipMass().PhysicalMass;
                    missilePosition = remoteControl.CubeGrid.GetPosition();
                    missileVelocity = remoteControl.GetShipVelocities().LinearVelocity;

                    while (targetsInfoListener.HasPendingMessage)
                    {
                        var messageIn = targetsInfoListener.AcceptMessage();
                        if (messageIn.Data is ImmutableDictionary<long, MyTuple<Vector3, Vector3, long>>)
                        {
                            targetsInfo = messageIn.As<ImmutableDictionary<long, MyTuple<Vector3, Vector3, long>>>();

                            targetPosition = targetsInfo[selectedTargetID].Item1;
                            targetVelocity = targetsInfo[selectedTargetID].Item2;
                            targetInfoTime = new DateTime(targetsInfo[selectedTargetID].Item3);
                        }
                    }
                    while (launcherInfoListener.HasPendingMessage)
                    {
                        var messageIn = launcherInfoListener.AcceptMessage();
                        if (messageIn.Data is MyTuple<string, Vector3, Vector3, long>)
                        {
                            launcherInfo = messageIn.As<MyTuple<string, Vector3, Vector3, long>>();

                            launcherPosition = launcherInfo.Item2;
                            launcherVelocity = launcherInfo.Item3;
                            launcherInfoTime = new DateTime(launcherInfo.Item4);
                        }
                    }
                    Vector3 estimatedTargetPos = targetPosition;
                    Vector3 estimatedLauncherPos = launcherPosition;
                    if (targetInfoTime < time)
                    {
                        float secSinceLastUpdate = (float)(time - targetInfoTime).TotalSeconds;
                        estimatedTargetPos = targetPosition + targetVelocity * secSinceLastUpdate;
                    }
                    if (launcherInfoTime < time)
                    {
                        float secSinceLastUpdate = (float)(time - launcherInfoTime).TotalSeconds;
                        estimatedLauncherPos = launcherPosition + launcherVelocity * secSinceLastUpdate;
                    }
                    relativeTargetPosition = estimatedTargetPos - missilePosition;
                    distanceToTarget = relativeTargetPosition.Length();
                    closingVelocity = targetVelocity - missileVelocity;
                    closingSpeed = Vector3.Dot(Vector3.Normalize(relativeTargetPosition), Vector3.Normalize(closingVelocity)) * closingVelocity.Length();
                    timeToTarget = distanceToTarget / -closingSpeed;

                    switch (stage)
                    {
                        case Stage.Idle:

                            localVectorToAlign = -Vector3.UnitZ;
                            localTotalAcceleration = Vector3.Zero;

                            break;

                        case Stage.Launching:

                            mergeBlock.Enabled = false;
                            localVectorToAlign = -Vector3.UnitZ;
                            localTotalAcceleration = maxForwardAccel * localVectorToAlign;

                            if ((missilePosition - estimatedLauncherPos).Length() > 100)
                            {
                                stage = Stage.Flying;
                            }

                            break;

                        case Stage.Flying:

                            missileGuidance.Run(missileVelocity, missilePosition, targetVelocity, estimatedTargetPos);
                            localTotalAcceleration = Vector3.TransformNormal(missileGuidance.totalAcceleration, Matrix.Transpose(remoteControl.WorldMatrix));
                            localVectorToAlign = Vector3.TransformNormal(missileGuidance.vectorToAlign, Matrix.Transpose(remoteControl.WorldMatrix));

                            if (timeToTarget > 0 && timeToTarget < 7)
                            {
                                stage = Stage.Interception;
                            }

                            break;

                        case Stage.Interception:

                            missileGuidance.Run(missileVelocity, missilePosition, targetVelocity, estimatedTargetPos);
                            localTotalAcceleration = Vector3.TransformNormal(missileGuidance.totalAcceleration, Matrix.Transpose(remoteControl.WorldMatrix));
                            localVectorToAlign = Vector3.TransformNormal(missileGuidance.vectorToAlign, Matrix.Transpose(remoteControl.WorldMatrix));

                            if (clusterMissile == true)
                            {
                                foreach (IMyGyro gyro in gyros)
                                {
                                    gyro.Roll = (float)(2 * Math.PI);
                                }

                                if (timeToTarget > 0 && timeToTarget < 5)
                                {
                                    stage = Stage.Detonation;
                                    foreach (IMyWarhead warhead in payload)
                                    {
                                        warhead.IsArmed = true;
                                        warhead.DetonationTime = timeToTarget - 0.1f;
                                        warhead.StartCountdown();
                                    }
                                    foreach (IMyMotorStator attachmentRotor in attachmentRotors)
                                    {
                                        attachmentRotor.Detach();
                                    }
                                }
                            }
                            else
                            {
                                if (timeToTarget <= 0.5f)
                                {
                                    stage = Stage.Detonation;
                                    foreach (IMyWarhead warhead in payload)
                                    {
                                        warhead.IsArmed = true;
                                        warhead.Detonate();
                                    }
                                }
                            }
                            break;
                    }

                    float rotation = (float)Math.Acos(Vector3.Dot(Vector3.Forward, localVectorToAlign));
                    Vector3 rotationVector = Vector3.Cross(Vector3.Forward, localVectorToAlign);
                    rotationVector.Normalize();
                    Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, rotation);
                    Matrix alignedMatrix = Matrix.Transform(Matrix.Identity, quaternion);

                    float yawError = (float)Math.Atan2(-alignedMatrix.M13, alignedMatrix.M11);
                    program.Echo(yawError.ToString());
                    float pitchError = (float)Math.Atan2(-alignedMatrix.M32, alignedMatrix.M22);
                    program.Echo(pitchError.ToString());
                    float yawCorrection = yawController.Run(yawError, timeDelta);
                    float pitchCorrection = pitchController.Run(pitchError, timeDelta);

                    foreach (IMyGyro gyro in gyros)
                    {
                        gyro.Pitch = -pitchCorrection;
                        gyro.Yaw = -yawCorrection;
                    }


                    foreach (KeyValuePair<IMyThrust, MyTuple<Vector3, Direction>> thruster in thrusterInfo)
                    {
                        Vector3 localTargetThrustVector = localTotalAcceleration * missileMass;
                        float maxThrust = 0;
                        switch (thruster.Value.Item2)
                        {
                            case Direction.Forward:
                                maxThrust = maxForwardThrust;
                                break;

                            case Direction.Backward:
                                maxThrust = maxBackwardThrust;
                                break;

                            case Direction.Leftward:
                                maxThrust = maxLeftwardThrust;
                                break;

                            case Direction.Rightward:
                                maxThrust = maxRightwardThrust;
                                break;

                            case Direction.Upward:
                                maxThrust = maxUpwardThrust;
                                break;

                            case Direction.Downward:
                                maxThrust = maxDownwardThrust;
                                break;
                        }

                        if (Vector3.Dot(localVectorToAlign, Vector3.Forward) > 0.95f && maxThrust != 0)
                        {
                            thruster.Key.ThrustOverridePercentage = Vector3.Dot(localTargetThrustVector, thruster.Value.Item1) / maxThrust;
                        }
                        else
                        {
                            thruster.Key.ThrustOverridePercentage = 0;
                        }
                    }

                    var messageOut = new MyTuple<string, string, long, Vector3, Vector3>(missileTag, stage.ToString(), selectedTargetID, missilePosition, missileVelocity);
                    program.IGC.SendBroadcastMessage($"[{launcherTag}]_MissileInfo", messageOut);
                }
            }

            public void InitMissile(string launcherTag, string missileTag)
            {
                targetsInfoListener = program.IGC.RegisterBroadcastListener($"[{launcherTag}]_TargetInfo");
                launcherInfoListener = program.IGC.RegisterBroadcastListener($"[{launcherTag}]_LauncherInfo");
                this.missileTag = missileTag;
                this.launcherTag = launcherTag;
                active = true;
            }

            public void Launch(string targetIDString)
            {
                long.TryParse(targetIDString, out selectedTargetID);

                stage = Stage.Launching;

                if (program.Runtime.UpdateFrequency != UpdateFrequency.Update1)
                {
                    program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                }
            }

            public void ResetMissile()
            {

            }
        }
    }
}
