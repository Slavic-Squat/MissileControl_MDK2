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
        public enum MissileType : byte
        {
            Unknown, AntiShip, AntiMissile, Cluster
        }

        public static MissileType GetMissileType(string typeStr)
        {
            switch (typeStr.ToUpper())
            {
                case "ANTI-SHIP": return MissileType.AntiShip;
                case "ANTI-MISL": return MissileType.AntiMissile;
                case "CLUSTER": return MissileType.Cluster;
                default: return MissileType.Unknown;
            }
        }

        public static string GetDisplayString(MissileType type)
        {
            switch (type)
            {
                case MissileType.Unknown: return "N/A";
                case MissileType.AntiShip: return "ANTI-SHIP";
                case MissileType.AntiMissile: return "ANTI-MISL";
                case MissileType.Cluster: return "CLUSTER";
                default: return "N/A";
            }
        }

        public enum MissileGuidanceType : byte
        {
            Unknown, MCLOS,
        }

        public static MissileGuidanceType GetMissileGuidanceType(string typeStr)
        {
            switch (typeStr.ToUpper())
            {
                case "MCLOS": return MissileGuidanceType.MCLOS;
                default: return MissileGuidanceType.Unknown;
            }
        }

        public static string GetDisplayString(MissileGuidanceType type)
        {
            switch (type)
            {
                case MissileGuidanceType.Unknown: return "N/A";
                case MissileGuidanceType.MCLOS: return "MCLOS";
                default: return "N/A";
            }
        }

        public enum MissilePayload : byte
        {
            Unknown, HE, Nuclear, Kinectic
        }

        public static MissilePayload GetMissilePayload(string payloadStr)
        {
            switch (payloadStr.ToUpper())
            {
                case "HE": return MissilePayload.HE;
                case "NUKE": return MissilePayload.Nuclear;
                case "KINECTIC": return MissilePayload.Kinectic;
                default: return MissilePayload.Unknown;
            }
        }

        public static string GetDisplayString(MissilePayload payload)
        {
            switch (payload)
            {
                case MissilePayload.Unknown: return "N/A";
                case MissilePayload.HE: return "HE";
                case MissilePayload.Nuclear: return "NUKE";
                case MissilePayload.Kinectic: return "KINECTIC";
                default: return "N/A";
            }
        }

        public enum MissileStage : byte
        {
            Unknown, Idle, Active, Launching, Flying, Interception
        }

        public enum EntityType : byte
        {
            Target, Missile
        }

        public enum EntityInfoSubType : byte
        {
            None, MissileInfoLite, MissileInfo,
        }

        public enum Direction
        {
            Left, Right, Up, Down, Forward, Backward
        }
    }
}
