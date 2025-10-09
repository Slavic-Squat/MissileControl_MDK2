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
        public static class MiscUtilities
        {
            public static float LoopInRange(float value, float min, float max)
            {
                if (min >= max)
                    throw new ArgumentException("min must be less than max");

                if (value >= min && value < max)
                    return value;

                float range = max - min;
                float shifted = value - min;
                shifted -= range * (float)Math.Floor(shifted / range);
                return min + shifted;
            }

            public static int LoopInRange(int value, int min, int max)
            {
                if (min >= max)
                    throw new ArgumentException("min must be less than max");

                if (value >= min && value < max)
                    return value;

                int range = max - min;
                int shifted = value - min;
                shifted %= range;
                if (shifted < 0)
                    shifted += range;
                return min + shifted;
            }
        }
    }
}
