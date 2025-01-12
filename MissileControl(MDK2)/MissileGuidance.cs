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
        public class MissileGuidance
        {
            #region Properties
            private int maxSpeed;
            private float N;
            private float maxForwardAccel;
            private float maxRadialAccel;
            #endregion

            #region Output
            public Vector3 vectorToAlign;
            public Vector3 totalAcceleration;
            #endregion

            public MissileGuidance(float maxForwardAccel, float maxRadialAccel, float N, int maxSpeed = 100)
            {
                this.maxSpeed = maxSpeed;
                this.N = N;
                this.maxForwardAccel = maxForwardAccel;
                this.maxRadialAccel = maxRadialAccel;
            }

            public void Run(Vector3 missileVelocity, Vector3 missilePosition, Vector3 targetVelocity, Vector3 targetPosition)
            {
                float maxTotalAccel = (float)Math.Sqrt((maxRadialAccel * maxRadialAccel) + (maxForwardAccel * maxForwardAccel));
                float maxAccelAngle = (float)Math.Atan(maxForwardAccel / maxRadialAccel);

                Vector3 missileVelocityHeading = Vector3.Normalize(missileVelocity);
                Vector3 rangeToTarget = targetPosition - missilePosition;
                Vector3 directionToTarget = Vector3.Normalize(rangeToTarget);

                if (rangeToTarget.Length() > 5000)
                {
                    rangeToTarget = directionToTarget * 5000;
                }
                Vector3 relativeTargetVelocity = targetVelocity - missileVelocity;
                Vector3 rotationVector = Vector3.Cross(rangeToTarget, relativeTargetVelocity) / Vector3.Dot(rangeToTarget, rangeToTarget);

                Vector3 proNavAcceleration = N * (relativeTargetVelocity.Length() / missileVelocity.Length()) * Vector3.Cross(rotationVector, missileVelocity);
                Vector3 proNavAccelerationDirection = Vector3.Normalize(proNavAcceleration);
                float proNavAccelerationMagnitude = proNavAcceleration.Length();

                Vector3 accelerationAlongVelocity;
                float accelerationAlongVelocityMagnitude;
                Vector3 alignmentReference = missileVelocityHeading;

                if (Vector3.Dot(directionToTarget, missileVelocityHeading) < 0)
                {
                    alignmentReference = -missileVelocityHeading;
                }

                if (proNavAccelerationMagnitude <= maxRadialAccel)
                {
                    accelerationAlongVelocityMagnitude = maxForwardAccel;
                    accelerationAlongVelocity = alignmentReference * accelerationAlongVelocityMagnitude;

                    if ((missileVelocity.Length() > (maxSpeed - 1)) && (Vector3.Dot(directionToTarget, alignmentReference) > 0))
                    {
                        accelerationAlongVelocity = Vector3.Zero;
                    }

                    vectorToAlign = alignmentReference;
                    totalAcceleration = accelerationAlongVelocity + proNavAcceleration;
                }
                else
                {
                    if (proNavAccelerationMagnitude > maxTotalAccel)
                    {
                        proNavAccelerationMagnitude = maxTotalAccel;
                        proNavAcceleration = proNavAccelerationDirection * proNavAccelerationMagnitude;
                    }

                    float freeForwardAccel = (float)Math.Sqrt((proNavAccelerationMagnitude * proNavAccelerationMagnitude) - (maxTotalAccel * maxTotalAccel));
                    accelerationAlongVelocityMagnitude = freeForwardAccel;
                    accelerationAlongVelocity = alignmentReference * accelerationAlongVelocityMagnitude;

                    if ((missileVelocity.Length() > (maxSpeed - 1)) && (Vector3.Dot(directionToTarget, missileVelocityHeading) > 0))
                    {
                        accelerationAlongVelocity = Vector3.Zero;
                    }

                    totalAcceleration = accelerationAlongVelocity + proNavAcceleration;
                    float totalAccelerationMagnitude = totalAcceleration.Length();
                    float totalAccelerationTargetAngle = (float)Math.Acos(proNavAccelerationMagnitude / totalAccelerationMagnitude);
                    float totalAccelerationAngle = (float)Math.Acos(maxRadialAccel / totalAccelerationMagnitude);
                    float rotation = totalAccelerationTargetAngle - totalAccelerationAngle;
                    Vector3 axisOfRotation = Vector3.Cross(proNavAccelerationDirection, alignmentReference);
                    axisOfRotation.Normalize();
                    Quaternion alignmentCorrection = Quaternion.CreateFromAxisAngle(axisOfRotation, -rotation);

                    vectorToAlign = Vector3.Transform(alignmentReference, alignmentCorrection);
                }
            }
        }
    }
}
