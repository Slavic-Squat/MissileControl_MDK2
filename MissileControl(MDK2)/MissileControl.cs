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
            public int ID { get; private set; }
            public MissileStage Stage { get; private set; }
            public MissileType Type { get; set; }
            public MissileGuidanceType GuidanceType { get; set; }
            public MissilePayload Payload { get; set; }
            public EntityInfo Target { get; set; }
            public EntityInfo Launcher { get; set; }
            public DateTime LasRunTime { get; private set; }


            private List<IMyGyro> _gyros = new List<IMyGyro>();
            private IMyShipConnector _connector;
            private List<IMyWarhead> _payload = new List<IMyWarhead>();
            private List<IMyThrust> _thrusters = new List<IMyThrust>();

            private MissileGuidance _missileGuidance;
            private PIDControl _pitchController;
            private PIDControl _yawController;

            private float _maxForwardAccel;
            private float _maxRadialAccel;
            private float _maxAccel;

            private List<ThrusterInfo> _thrusterInfos = new List<ThrusterInfo>();
            private Dictionary<Direction, float> _maxThrust = new Dictionary<Direction, float>();

            public MissileControl(int ID)
            {
                this.ID = ID;
                Init();

                Stage = MissileStage.Idle;
                _pitchController = new PIDControl(1.0f, 0, 0.2f);
                _yawController = new PIDControl(1.0f, 0, 0.2f);

                _missileGuidance = new MissileGuidance(_maxAccel, 0.75f, 3.5f, maxSpeed: 100);
            }

            public void GetBlocks()
            {
                GTS.GetBlocksOfType(_thrusters, b => b.IsSameConstructAs(MePB) && b.CustomName.Contains("Thruster"));
                if (_thrusters.Count == 0)
                {
                    throw new Exception("No thrusters found on this construct.");
                }
                GTS.GetBlocksOfType(_gyros, b => b.IsSameConstructAs(MePB) && b.CustomName.Contains("Gyro"));
                if (_gyros.Count == 0)
                {
                    throw new Exception("No gyros found on this construct.");
                }

                List<IMyShipConnector> connectors = new List<IMyShipConnector>();
                GTS.GetBlocksOfType(connectors, b => b.IsSameConstructAs(MePB) && b.CustomName.Contains("Connector"));
                if (connectors.Count == 0)
                {
                    throw new Exception("No connector found on this construct.");
                }
                _connector = connectors[0];

                GTS.GetBlocksOfType(_payload, b => b.IsSameConstructAs(MePB) && b.CustomName.Contains("Warhead"));
                if (_payload.Count == 0)
                {
                    throw new Exception("No warheads found on this construct.");
                }
            }

            public void Init()
            {
                LasRunTime = DateTime.MinValue;
                GetBlocks();
                _thrusterInfos.Clear();
                _maxThrust.Clear();

                foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                {
                    _maxThrust[dir] = 0;
                }

                foreach (IMyThrust thruster in _thrusters)
                {
                    Vector3D localThrusterDirection = Vector3.TransformNormal(thruster.WorldMatrix.Backward, Matrix.Transpose(SystemCoordinator.ReferenceBasis));

                    float epsilon = 0.01f;
                    if (localThrusterDirection.Z <= -1 + epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Forward);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Forward] += thruster.MaxEffectiveThrust;
                    }
                    else if (localThrusterDirection.Z >= 1 - epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Backward);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Backward] += thruster.MaxEffectiveThrust;
                    }
                    else if (localThrusterDirection.X <= -1 + epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Left);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Left] += thruster.MaxThrust;
                    }
                    else if (localThrusterDirection.X >= 1 - epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Right);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Right] += thruster.MaxThrust;
                    }
                    else if (localThrusterDirection.Y >= 1 - epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Up);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Up] += thruster.MaxThrust;
                    }
                    else if (localThrusterDirection.Y <= -1 + epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Down);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Down] += thruster.MaxThrust;
                    }
                }

                float missileMass = SystemCoordinator.ReferenceController.CalculateShipMass().PhysicalMass;
                _maxForwardAccel = _maxThrust[Direction.Forward] / missileMass;
                _maxRadialAccel = _maxThrust[Direction.Right] / missileMass;
                _maxAccel = (float)Math.Sqrt(_maxForwardAccel * _maxForwardAccel + _maxRadialAccel * _maxRadialAccel);
            }

            public void Run(DateTime time)
            {
                if (LasRunTime == DateTime.MinValue)
                {
                    LasRunTime = time;
                    return;
                }

                if (Stage > MissileStage.Idle)
                {
                    float timeDelta = (float)(time - LasRunTime).TotalSeconds;

                    float missileMass = SystemCoordinator.ReferenceController.CalculateShipMass().PhysicalMass;
                    Vector3 missilePos = SystemCoordinator.ReferencePosition;
                    Vector3 missileVel = SystemCoordinator.ReferenceVelocity;

                    Vector3 estimatedTargetPos = Target.Position;
                    Vector3 estimatedLauncherPos = Launcher.Position;
                    if (Target.TimeRecorded < time)
                    {
                        float secSinceLastUpdate = (float)(time - Target.TimeRecorded).TotalSeconds;
                        estimatedTargetPos = Target.Position + Target.Velocity * secSinceLastUpdate;
                    }
                    if (Launcher.TimeRecorded < time)
                    {
                        float secSinceLastUpdate = (float)(time - Launcher.TimeRecorded).TotalSeconds;
                        estimatedLauncherPos = Launcher.Position + Launcher.Velocity * secSinceLastUpdate;
                    }
                    Vector3 relTargetPos = estimatedTargetPos - missilePos;
                    float distToTarget = relTargetPos.Length();
                    Vector3 relTargetDir = Vector3.Normalize(relTargetPos);
                    Vector3 relVel = Target.Velocity - missileVel;
                    float closingSpeed = -Vector3.Dot(Vector3.Normalize(relTargetPos), relVel);
                    float timeToTarget = distToTarget / closingSpeed;


                    Vector3 vectorToAlign = Vector3.Zero;
                    Vector3 accelVector = Vector3.Zero;
                    Vector3 forwardVector = SystemCoordinator.ReferenceBasis.Forward;
                    switch (Stage)
                    {
                        case MissileStage.Launching:

                            _connector.Disconnect();
                            vectorToAlign = forwardVector;
                            accelVector = forwardVector * _maxForwardAccel;

                            if ((missilePos - estimatedLauncherPos).Length() > 100)
                            {
                                Stage = MissileStage.Flying;
                            }

                            break;

                        case MissileStage.Flying:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, Target.Velocity, missilePos, missileVel);
                            vectorToAlign = relTargetDir;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            if (timeToTarget > 0 && timeToTarget < 7)
                            {
                                Stage = MissileStage.Interception;
                            }

                            break;

                        case MissileStage.Interception:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, Target.Velocity, missilePos, missileVel);
                            vectorToAlign = relTargetDir;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);
                            if (timeToTarget <= 0.5f)
                            {
                                foreach (IMyWarhead warhead in _payload)
                                {
                                    warhead.IsArmed = true;
                                    warhead.Detonate();
                                }
                            }
                            break;

                        default:
                            vectorToAlign = forwardVector;
                            break;
                    }
                    Vector3 forwardVectorLocal = Vector3.TransformNormal(forwardVector, Matrix.Transpose(SystemCoordinator.ReferenceBasis));
                    Vector3 vectorToAlignLocal = Vector3.TransformNormal(vectorToAlign, Matrix.Transpose(SystemCoordinator.ReferenceBasis));
                    Quaternion quaternion = Quaternion.CreateFromTwoVectors(forwardVectorLocal, vectorToAlignLocal);
                    Matrix alignedMatrixLocal = Matrix.Transform(Matrix.Identity, quaternion);

                    float yawError = (float)Math.Atan2(-alignedMatrixLocal.M13, alignedMatrixLocal.M11);
                    float pitchError = (float)Math.Atan2(-alignedMatrixLocal.M32, alignedMatrixLocal.M22);
                    float yawCorrection = _yawController.Run(yawError, timeDelta);
                    float pitchCorrection = _pitchController.Run(pitchError, timeDelta);

                    foreach (IMyGyro gyro in _gyros)
                    {
                        gyro.Pitch = -pitchCorrection;
                        gyro.Yaw = -yawCorrection;
                    }


                    foreach (var thrusterInfo in _thrusterInfos)
                    {
                        Vector3 desiredThrustVector = accelVector * missileMass;
                        float maxThrust = _maxThrust[thrusterInfo.Direction];

                        if (Vector3.Dot(vectorToAlign, forwardVector) > 0.9f && maxThrust != 0)
                        {
                            thrusterInfo.Thruster.ThrustOverridePercentage = Vector3.Dot(desiredThrustVector, thrusterInfo.Vector) / maxThrust;
                        }
                        else
                        {
                            thrusterInfo.Thruster.ThrustOverridePercentage = 0;
                        }
                    }
                }
            }

            private void ClampAndAlign(Vector3 currentVectorToAlign, ref Vector3 accelVector, out Vector3 newVectorToAlign)
            {

                if (accelVector.Length() > _maxAccel)
                {
                    accelVector = Vector3.Normalize(accelVector) * _maxAccel;
                }

                float accelMag = accelVector.Length();

                float minForwardAccel;
                float maxForwardAccel;

                if (accelMag <= _maxRadialAccel)
                {
                    minForwardAccel = 0;
                    maxForwardAccel = _maxForwardAccel;
                }
                else
                {
                    minForwardAccel = (float)Math.Sqrt(accelMag * accelMag - _maxRadialAccel * _maxRadialAccel);
                    maxForwardAccel = _maxForwardAccel;
                }

                float currentForwardAccel = Vector3.Dot(accelVector, currentVectorToAlign);

                if (currentForwardAccel >= minForwardAccel && currentForwardAccel <= maxForwardAccel)
                {
                    newVectorToAlign = currentVectorToAlign;
                    return;
                }

                Vector3 rotationVector = Vector3.Cross(currentVectorToAlign, accelVector);

                if (rotationVector.Length() <= 1e-6f)
                {
                    rotationVector = Vector3.CalculatePerpendicularVector(currentVectorToAlign);
                }

                rotationVector = Vector3.Normalize(rotationVector);

                float targetForwardAccel = currentForwardAccel < minForwardAccel ? minForwardAccel : maxForwardAccel;

                float currentAccelAngle = (float)Math.Acos(currentForwardAccel / accelMag);
                float targetAccelAngle = (float)Math.Acos(targetForwardAccel / accelMag);
                float rotationAngle = targetAccelAngle - currentAccelAngle;

                Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, rotationAngle);
                newVectorToAlign = Vector3.Transform(currentVectorToAlign, quaternion);
            }

            public void Activate()
            {
                if (Stage != MissileStage.Idle)
                {
                    return;
                }
                Stage = MissileStage.Active;
            }

            public void Launch()
            {
                if (Stage != MissileStage.Active)
                {
                    return;
                }
                Stage = MissileStage.Launching;
            }
        }
    }
}
