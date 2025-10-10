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
            public float MaxAccel { get; set; }
            public float M { get; set; }
            public float N { get; set; }
            public float MaxSpeed { get; set; }

            public MissileGuidance(float maxAccel, float m, float n, float maxSpeed = 100)
            {
                MaxAccel = maxAccel;
                M = m;
                N = n;
                MaxSpeed = maxSpeed;
            }

            public Vector3 CalculateTotalAccel(Vector3 targetPos, Vector3 targetVel, Vector3 missilePos, Vector3 missileVel)
            {
                Vector3 missileVelDir = Vector3.Normalize(missileVel);
                Vector3 range = targetPos - missilePos;
                Vector3 dirToTarget = Vector3.Normalize(range);

                Vector3 relVel = targetVel - missileVel;
                Vector3 rotationVector = Vector3.Cross(range, relVel) / Vector3.Dot(range, range);

                float desiredRelAccelMag = MaxAccel;
                Vector3 desiredRelAccel = dirToTarget * desiredRelAccelMag;

                float alignment = Vector3.Dot(missileVelDir, dirToTarget);
                if (missileVel.Length() > 0.9f * MaxSpeed && alignment > 0.75f)
                {
                    desiredRelAccel = Vector3.Zero;
                }

                Vector3 proNavAccel = M * desiredRelAccel - N * Vector3.Cross(rotationVector, relVel) + Vector3.Cross(rotationVector, Vector3.Cross(rotationVector, range));
                Vector3 proNavAccelDir = Vector3.Normalize(proNavAccel);
                float proNavAccelMag = proNavAccel.Length();

                if (proNavAccelMag > MaxAccel)
                {
                    proNavAccel = proNavAccelDir * MaxAccel;
                }

                return proNavAccel;
            }
        }
    }
}
