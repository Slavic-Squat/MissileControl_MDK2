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
        public class Gyro
        {
            public IMyGyro GyroBlock { get; private set; }
            public float Pitch
            {
                get { return -GyroBlock.Pitch; }
                set { GyroBlock.Pitch = -value; }
            }
            public float Yaw
            {
                get { return -GyroBlock.Yaw; }
                set { GyroBlock.Yaw = -value; }
            }
            public float Roll
            {
                get { return -GyroBlock.Roll; }
                set { GyroBlock.Roll = -value; }
            }

            public Gyro(IMyGyro gyro)
            {
                GyroBlock = gyro;

                if (GyroBlock == null)
                {
                    DebugWrite($"Gyro is null!\n", true);
                    throw new Exception($"Gyro is null!\n");
                }
            }

            public Gyro(string gyroName)
            {
                gyroName = gyroName.ToUpper();
                GyroBlock = AllGridBlocks.Where(b => b is IMyGyro && b.CustomName.ToUpper().Contains(gyroName)).FirstOrDefault() as IMyGyro;
                if (GyroBlock == null)
                {
                    DebugWrite($"Gyro '{gyroName}' not found!\n", true);
                    throw new Exception($"Gyro '{gyroName}' not found!\n");
                }
            }
        }
    }
}
