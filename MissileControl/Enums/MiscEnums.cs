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
        public enum Direction
        {
            Left, Right, Up, Down, Forward, Backward
        }

        public static class MiscEnumHelper
        {
            public static Direction GetDirection(string dirStr)
            {
                switch (dirStr.ToUpper())
                {
                    case "LEFT":
                        return Direction.Left;
                    case "RIGHT":
                        return Direction.Right;
                    case "UP":
                        return Direction.Up;
                    case "DOWN":
                        return Direction.Down;
                    case "FORWARD":
                        return Direction.Forward;
                    case "BACKWARD":
                        return Direction.Backward;
                    default:
                        return Direction.Forward;
                }
            }

            public static string GetDirectionStr(Direction dir)
            {
                switch (dir)
                {
                    case Direction.Left:
                        return "LEFT";
                    case Direction.Right:
                        return "RIGHT";
                    case Direction.Up:
                        return "UP";
                    case Direction.Down:
                        return "DOWN";
                    case Direction.Forward:
                        return "FORWARD";
                    case Direction.Backward:
                        return "BACKWARD";
                    default:
                        return "FORWARD";
                }
            }
        }
    }
}
