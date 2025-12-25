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
            public double MaxAccel { get; set; }
            public double M { get; set; }
            public double N { get; set; }
            public double MaxSpeed { get; set; }

            public MissileGuidance(double maxAccel, double m, double n, double maxSpeed = 100)
            {
                MaxAccel = maxAccel;
                M = m;
                N = n;
                MaxSpeed = maxSpeed;
            }

            public Vector3D CalculateTotalAccel(Vector3D targetPos, Vector3D targetVel, Vector3D missilePos, Vector3D missileVel)
            {
                double missileSpeed = missileVel.Length();
                Vector3D missileVelDir = missileSpeed == 0 ? Vector3D.Zero : missileVel / missileSpeed;
                Vector3D range = targetPos - missilePos;
                double dist = range.Length();
                Vector3D dirToTarget = dist == 0 ? Vector3D.Zero : range / dist;

                Vector3D relVel = targetVel - missileVel;
                Vector3D rotationVector = dist == 0 ? Vector3D.Zero : Vector3D.Cross(range, relVel) / (dist * dist);

                double desiredRelAccelMag = MaxAccel;
                Vector3D desiredRelAccel = dirToTarget * desiredRelAccelMag;

                double alignment = Vector3D.Dot(missileVelDir, dirToTarget);
                if (missileSpeed > 0.98 * MaxSpeed && alignment > 0.75)
                {
                    desiredRelAccel = Vector3D.Zero;
                }

                Vector3D proNavAccel = M * desiredRelAccel - N * Vector3D.Cross(rotationVector, relVel) + Vector3D.Cross(rotationVector, Vector3D.Cross(rotationVector, range));
                double proNavAccelMag = proNavAccel.Length();

                if (proNavAccelMag > MaxAccel)
                {
                    proNavAccel = proNavAccel / proNavAccelMag * MaxAccel;
                }

                return proNavAccel;
            }
        }
    }
}
