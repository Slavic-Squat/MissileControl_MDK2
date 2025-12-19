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
            public double Time { get; private set; }
            public MissileStage Stage { get; private set; } = MissileStage.Idle;

            private List<Gyro> _gyros = new List<Gyro>();
            private IMyShipConnector _connector;
            private List<IMyWarhead> _payload = new List<IMyWarhead>();
            private Dictionary<Direction, ThrusterGroup> _thrusterGroups = new Dictionary<Direction, ThrusterGroup>();

            private MissileGuidance _missileGuidance;
            private PIDControl _pitchController;
            private PIDControl _yawController;

            private float _missileMass;
            private float _maxSpeed;
            private float _m;
            private float _n;
            private float _kp;
            private float _ki;
            private float _kd;
            private float _maxForwardAccel;
            private float _maxRadialAccel;
            private float _maxAccel;

            private MissileType _type;
            private MissileGuidanceType _guidanceType;
            private MissilePayload _payloadType;
            private Direction _launchDirection;

            private EntityInfo _target;
            private double _launchTime;

            public MissileControl(float missileMass, float maxSpeed, MissileType type, MissileGuidanceType guidanceType, MissilePayload payload, float m, float n, float kp, float ki, float kd, Direction launchDirection)
            {
                _missileMass = missileMass;
                _maxSpeed = maxSpeed;
                _type = type;
                _guidanceType = guidanceType;
                _payloadType = payload;
                _m = m;
                _n = n;
                _kp = kp;
                _ki = ki;
                _kd = kd;
                _launchDirection = launchDirection;
                GetBlocks();
                Init();
            }

            private void GetBlocks()
            {
                _thrusterGroups[Direction.Up] = new ThrusterGroup(Direction.Up, AllGridBlocks.Where(b => b is IMyThrust && b.CustomData.ToUpper().Contains("-UP")).Select(b => new Thruster(b as IMyThrust, Direction.Up)).ToArray());
                _thrusterGroups[Direction.Down] = new ThrusterGroup(Direction.Down, AllGridBlocks.Where(b => b is IMyThrust && b.CustomData.ToUpper().Contains("-DOWN")).Select(b => new Thruster(b as IMyThrust, Direction.Down)).ToArray());
                _thrusterGroups[Direction.Left] = new ThrusterGroup(Direction.Left, AllGridBlocks.Where(b => b is IMyThrust && b.CustomData.ToUpper().Contains("-LEFT")).Select(b => new Thruster(b as IMyThrust, Direction.Left)).ToArray());
                _thrusterGroups[Direction.Right] = new ThrusterGroup(Direction.Right, AllGridBlocks.Where(b => b is IMyThrust && b.CustomData.ToUpper().Contains("-RIGHT")).Select(b => new Thruster(b as IMyThrust, Direction.Right)).ToArray());
                _thrusterGroups[Direction.Forward] = new ThrusterGroup(Direction.Forward, AllGridBlocks.Where(b => b is IMyThrust && b.CustomData.ToUpper().Contains("-FORWARD")).Select(b => new Thruster(b as IMyThrust, Direction.Forward)).ToArray());
                _thrusterGroups[Direction.Backward] = new ThrusterGroup(Direction.Backward, AllGridBlocks.Where(b => b is IMyThrust && b.CustomData.ToUpper().Contains("-BACKWARD")).Select(b => new Thruster(b as IMyThrust, Direction.Backward)).ToArray());

                if (_thrusterGroups.Count == 0)
                {
                    DebugWrite("Error: no thrusters found!\n", true);
                    throw new Exception("No thrusters found!\n");
                }
                _gyros = AllGridBlocks.Where(b => b is IMyGyro).Select(b => new Gyro(b as IMyGyro)).ToList();
                if (_gyros.Count == 0)
                {
                    DebugWrite("Error: no gyros found!\n", true);
                    throw new Exception("No gyros found!\n");
                }

                _connector = AllGridBlocks.Find(b => b is IMyShipConnector) as IMyShipConnector;
                if (_connector == null)
                {
                    DebugWrite("Error: no connector found!\n", true);
                    throw new Exception("No connector found!\n");
                }

                _payload = AllGridBlocks.Where(b => b is IMyWarhead).Cast<IMyWarhead>().ToList();
                if (_payload.Count == 0)
                {
                    DebugWrite("Error: no warheads found!\n", true);
                    throw new Exception("No warheads found!\n");
                }
            }

            private void Init()
            {
                _maxForwardAccel = _thrusterGroups[Direction.Forward].MaxThrust / _missileMass;
                _maxRadialAccel = _thrusterGroups[Direction.Right].MaxThrust / _missileMass;
                _maxAccel = (float)Math.Sqrt(_maxForwardAccel * _maxForwardAccel + _maxRadialAccel * _maxRadialAccel);

                _pitchController = new PIDControl(_kp, _ki, _kd);
                _yawController = new PIDControl(_kp, _ki, _kd);

                _missileGuidance = new MissileGuidance(_maxAccel, _m, _n, maxSpeed: _maxSpeed);
            }

            public void Run(double time)
            {
                if (Time == 0)
                {
                    Time = time;
                    return;
                }
                double globalTime = SystemCoordinator.GlobalTime;

                if ( Stage == MissileStage.Idle)
                {
                    foreach (var thrusterGroup in _thrusterGroups.Values)
                    {
                        thrusterGroup.ThrustOverride = 0;
                    }
                    foreach (Gyro gyro in _gyros)
                    {
                        gyro.Pitch = 0;
                        gyro.Yaw = 0;
                        gyro.Roll = 0;
                    }
                    return;
                }
                if (Stage > MissileStage.Idle)
                {
                    double timeDelta = time - Time;

                    Vector3 missilePos = SystemCoordinator.ReferencePosition;
                    Vector3 missileVel = SystemCoordinator.ReferenceVelocity;

                    Vector3 estimatedTargetPos = _target.Position;
                    if (_target.TimeRecorded < globalTime)
                    {
                        double secSinceLastUpdate = globalTime - _target.TimeRecorded;
                        estimatedTargetPos = _target.Position + _target.Velocity * (float)secSinceLastUpdate;
                    }
                    Vector3 range = estimatedTargetPos - missilePos;
                    float dist = range.Length();
                    Vector3 relTargetDir = dist == 0 ? Vector3.Zero : range / dist;
                    Vector3 relVel = _target.Velocity - missileVel;
                    float closingSpeed = -Vector3.Dot(relTargetDir, relVel);
                    float timeToTarget = dist / closingSpeed;

                    Vector3 forwardVector = SystemCoordinator.ReferenceWorldMatrix.Forward;
                    
                    Vector3 vectorToAlign;
                    Vector3 accelVector;
                    switch (Stage)
                    {
                        case MissileStage.Launching:

                            _connector.Disconnect();
                            Vector3 launchVector;
                            float launchAccel = _thrusterGroups[_launchDirection].MaxThrust / _missileMass;
                            switch (_launchDirection)
                            {
                                case Direction.Up:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Up;
                                    break;
                                case Direction.Down:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Down;
                                    break;
                                case Direction.Left:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Left;
                                    break;
                                case Direction.Right:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Right;
                                    break;
                                case Direction.Forward:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Forward;
                                    break;
                                case Direction.Backward:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Backward;
                                    break;
                                default:
                                    launchVector = SystemCoordinator.ReferenceWorldMatrix.Forward;
                                    break;
                            }
                            vectorToAlign = forwardVector;
                            accelVector = launchVector * launchAccel;

                            if (time - _launchTime > 3)
                            {
                                Stage = MissileStage.Flying;
                            }

                            break;

                        case MissileStage.Flying:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, _target.Velocity, missilePos, missileVel);
                            vectorToAlign = relTargetDir;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            if (timeToTarget > 0 && timeToTarget < 10)
                            {
                                Stage = MissileStage.Interception;
                            }

                            break;

                        case MissileStage.Interception:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, _target.Velocity, missilePos, missileVel);
                            vectorToAlign = relTargetDir;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            _payload.ForEach(w => w.IsArmed = true);
                            if (timeToTarget <= 0)
                            {
                                _payload.ForEach(w => w.Detonate());
                            }
                            break;

                        default:
                            vectorToAlign = forwardVector;
                            accelVector = Vector3.Zero;
                            break;
                    }
                    Vector3 forwardVectorLocal = Vector3.TransformNormal(forwardVector, Matrix.Transpose(SystemCoordinator.ReferenceWorldMatrix));
                    Vector3 vectorToAlignLocal = Vector3.TransformNormal(vectorToAlign, Matrix.Transpose(SystemCoordinator.ReferenceWorldMatrix));
                    float dot = Vector3.Dot(forwardVectorLocal, vectorToAlignLocal);
                    float epsilon = 1e-6f;
                    Vector3 rotationVector;
                    if (dot <= -1 + epsilon)
                    {
                        rotationVector = Vector3.CalculatePerpendicularVector(forwardVectorLocal);
                    }
                    else if (dot >= 1 - epsilon)
                    {
                        rotationVector = Vector3.Zero;
                    }
                    else
                    {
                        rotationVector = Vector3.Cross(forwardVectorLocal, vectorToAlignLocal);
                    }
                    float rotationAngle = (float)Math.Acos(MathHelper.Clamp(dot, -1f, 1f));
                    Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, rotationAngle);
                    Matrix alignedMatrixLocal = Matrix.CreateFromQuaternion(quaternion);

                    float yawError = (float)Math.Atan2(-alignedMatrixLocal.M13, alignedMatrixLocal.M11);
                    float pitchError = (float)Math.Atan2(-alignedMatrixLocal.M32, alignedMatrixLocal.M22);
                    float yawCorrection = _yawController.Run(yawError, (float)timeDelta);
                    float pitchCorrection = _pitchController.Run(pitchError, (float)timeDelta);

                    foreach (Gyro gyro in _gyros)
                    {
                        gyro.Pitch = pitchCorrection;
                        gyro.Yaw = yawCorrection;
                    }


                    float alignment = Vector3.Dot(vectorToAlign, forwardVector);
                    Vector3 desiredThrustVector = accelVector * _missileMass;
                    foreach (var thrusterGroup in _thrusterGroups.Values)
                    {
                        if (alignment > 0.9f)
                        {
                            float value = Vector3.Dot(desiredThrustVector, thrusterGroup.Vector);
                            if (value < 0) value = 0;
                            thrusterGroup.ThrustOverride = value;
                        }
                        else
                        {
                            thrusterGroup.ThrustOverride = 0;
                        }
                    }
                }
                Time = time;
            }

            private void ClampAndAlign(Vector3 currentVectorToAlign, ref Vector3 accelVector, out Vector3 newVectorToAlign)
            {
                float accelMag = accelVector.Length();
                if (accelMag > _maxAccel)
                {
                    accelVector = accelVector / accelMag * _maxAccel;
                    accelMag = _maxAccel;
                }                

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

                Vector3 accelDir = accelMag == 0 ? Vector3.Zero : accelVector / accelMag;
                float dot = Vector3.Dot(accelDir, currentVectorToAlign);
                Vector3 rotationVector;
                float epsilon = 1e-6f;
                
                if (dot <= -1 + epsilon)
                {
                    rotationVector = Vector3.CalculatePerpendicularVector(currentVectorToAlign);
                }
                else if (dot >= 1 - epsilon)
                {
                    rotationVector = Vector3.Zero;
                }
                else
                {
                    rotationVector = Vector3.Cross(currentVectorToAlign, accelDir);
                }

                float targetForwardAccel = currentForwardAccel < minForwardAccel ? minForwardAccel : maxForwardAccel;

                float currentAccelAngle = accelMag == 0 ? 0 : (float)Math.Acos(MathHelper.Clamp(currentForwardAccel / accelMag, -1, 1));
                float targetAccelAngle = accelMag == 0 ? 0 : (float)Math.Acos(MathHelper.Clamp(targetForwardAccel / accelMag, -1, 1));
                float rotationAngle = -1f * (targetAccelAngle - currentAccelAngle);

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

            public void Deactivate()
            {
                Stage = MissileStage.Idle;
            }

            public void Launch()
            {
                if (Stage != MissileStage.Active)
                {
                    return;
                }
                Stage = MissileStage.Launching;
                _launchTime = Time;
            }

            public void UpdateTarget(EntityInfo target)
            {
                _target = target;
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
