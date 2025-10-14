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
            public MissileStage Stage { get; private set; } = MissileStage.Idle;

            private List<IMyGyro> _gyros = new List<IMyGyro>();
            private IMyShipConnector _connector;
            private List<IMyWarhead> _payload = new List<IMyWarhead>();
            private List<IMyThrust> _thrusters = new List<IMyThrust>();

            private DateTime _lastRunTime;

            private MissileGuidance _missileGuidance;
            private PIDControl _pitchController;
            private PIDControl _yawController;

            private float _missileMass;
            private float _maxForwardAccel;
            private float _maxRadialAccel;
            private float _maxAccel;

            private MissileType _type;
            private MissileGuidanceType _guidanceType;
            private MissilePayload _payloadType;

            private EntityInfo _target;
            private EntityInfo _launcher;

            private List<ThrusterInfo> _thrusterInfos = new List<ThrusterInfo>();
            private Dictionary<Direction, float> _maxThrust = new Dictionary<Direction, float>();

            public MissileControl(int ID, float missileMass, MissileType type, MissileGuidanceType guidanceType, MissilePayload payload)
            {
                this.ID = ID;
                _missileMass = missileMass;
                _type = type;
                _guidanceType = guidanceType;
                _payloadType = payload;
                GetBlocks();
                Init();
            }

            private void GetBlocks()
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

            private void Init()
            {
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
                        _maxThrust[Direction.Forward] += thruster.MaxThrust;
                    }
                    else if (localThrusterDirection.Z >= 1 - epsilon)
                    {
                        ThrusterInfo thrusterInfo = new ThrusterInfo(thruster, Direction.Backward);
                        _thrusterInfos.Add(thrusterInfo);
                        _maxThrust[Direction.Backward] += thruster.MaxThrust;
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
                _maxForwardAccel = _maxThrust[Direction.Forward] / _missileMass;
                _maxRadialAccel = _maxThrust[Direction.Right] / _missileMass;
                _maxAccel = (float)Math.Sqrt(_maxForwardAccel * _maxForwardAccel + _maxRadialAccel * _maxRadialAccel);

                _pitchController = new PIDControl(1.0f, 0, 0.2f);
                _yawController = new PIDControl(1.0f, 0, 0.2f);

                _missileGuidance = new MissileGuidance(_maxAccel, 0.75f, 3.5f, maxSpeed: 100);
            }

            public void Run(DateTime time)
            {
                if (_lastRunTime == DateTime.MinValue)
                {
                    _lastRunTime = time;
                    return;
                }

                if (Stage > MissileStage.Idle)
                {
                    float timeDelta = (float)(time - _lastRunTime).TotalSeconds;

                    Vector3 missilePos = SystemCoordinator.ReferencePosition;
                    Vector3 missileVel = SystemCoordinator.ReferenceVelocity;

                    Vector3 estimatedTargetPos = _target.Position;
                    Vector3 estimatedLauncherPos = _launcher.Position;
                    if (_target.TimeRecorded < time)
                    {
                        float secSinceLastUpdate = (float)(time - _target.TimeRecorded).TotalSeconds;
                        estimatedTargetPos = _target.Position + _target.Velocity * secSinceLastUpdate;
                    }
                    if (_launcher.TimeRecorded < time)
                    {
                        float secSinceLastUpdate = (float)(time - _launcher.TimeRecorded).TotalSeconds;
                        estimatedLauncherPos = _launcher.Position + _launcher.Velocity * secSinceLastUpdate;
                    }
                    Vector3 relTargetPos = estimatedTargetPos - missilePos;
                    float distToTarget = relTargetPos.Length();
                    Vector3 relTargetDir = relTargetPos == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(relTargetPos);
                    Vector3 relVel = _target.Velocity - missileVel;
                    float closingSpeed = -Vector3.Dot(relTargetDir, relVel);
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

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, _target.Velocity, missilePos, missileVel);
                            vectorToAlign = relTargetDir;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            if (timeToTarget > 0 && timeToTarget < 7)
                            {
                                Stage = MissileStage.Interception;
                            }

                            break;

                        case MissileStage.Interception:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, _target.Velocity, missilePos, missileVel);
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
                    forwardVectorLocal = forwardVectorLocal == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(forwardVectorLocal);
                    vectorToAlignLocal = vectorToAlignLocal == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(vectorToAlignLocal);
                    float dot = Vector3.Dot(forwardVectorLocal, vectorToAlignLocal);
                    float epsilon = 1e-6f;
                    Vector3 rotationVector;
                    if (dot <= -1 + epsilon)
                    {
                        rotationVector = Vector3.CalculatePerpendicularVector(forwardVectorLocal);
                        rotationVector = rotationVector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(rotationVector);
                    }
                    else if (dot >= 1 - epsilon)
                    {
                        rotationVector = Vector3.Zero;
                    }
                    else
                    {
                        rotationVector = Vector3.Cross(forwardVectorLocal, vectorToAlignLocal);
                        rotationVector = rotationVector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(rotationVector);
                    }
                    dot = MathHelper.Clamp(dot, -1f, 1f);
                    float rotationAngle = (float)Math.Acos(dot);
                    Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, rotationAngle);
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
                        Vector3 desiredThrustVector = accelVector * _missileMass;
                        float maxThrust = _maxThrust[thrusterInfo.Direction];

                        if (Vector3.Dot(vectorToAlign, forwardVector) > 0.9f && maxThrust != 0)
                        {
                            thrusterInfo.Thruster.ThrustOverridePercentage = MathHelper.Clamp(Vector3.Dot(desiredThrustVector, thrusterInfo.Vector) / maxThrust, 0f, 1f);
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
                    accelVector = accelVector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(accelVector) * _maxAccel;
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

                Vector3 accelDir = accelVector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(accelVector);
                currentVectorToAlign = currentVectorToAlign == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(currentVectorToAlign);
                float dot = Vector3.Dot(accelDir, currentVectorToAlign);
                Vector3 rotationVector;
                float epsilon = 1e-6f;
                
                if (dot <= -1 + epsilon)
                {
                    rotationVector = Vector3.CalculatePerpendicularVector(currentVectorToAlign);
                    rotationVector = rotationVector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(rotationVector);
                }
                else if (dot >= 1 - epsilon)
                {
                    rotationVector = Vector3.Zero;
                }
                else
                {
                    rotationVector = Vector3.Cross(currentVectorToAlign, accelDir);
                    rotationVector = rotationVector == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(rotationVector);
                }

                float targetForwardAccel = currentForwardAccel < minForwardAccel ? minForwardAccel : maxForwardAccel;

                float currentAccelAngle = accelMag == 0 ? 0 : (float)Math.Acos(currentForwardAccel / accelMag);
                float targetAccelAngle = accelMag == 0 ? 0 : (float)Math.Acos(targetForwardAccel / accelMag);
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

            public void UpdateTarget(EntityInfo target)
            {
                _target = target;
            }

            public void UpdateLauncher(EntityInfo launcher)
            {
                _launcher = launcher;
            }

            public void Abort()
            {
                if (Stage > MissileStage.Launching)
                {
                    foreach (IMyWarhead warhead in _payload)
                    {
                        warhead.IsArmed = true;
                        warhead.Detonate();
                    }
                }
            }
        }
    }
}
