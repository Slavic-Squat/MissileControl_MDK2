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
            Unknown, MCLOS,
        }

        public enum MissilePayload : byte
        {
            Unknown, HE, Nuclear, Kinectic
        }

        public enum MissileStage : byte
        {
            Unknown, Active, Launching, Flying, Interception
        }

        public enum EntityType : byte
        {
            Target, Missile
        }

        public enum SerializedTypes : byte
        {
            Command, EntityInfo
        }

        public enum EntityInfoSubType : byte
        {
            None, MissileInfoLite, MissileInfo,
        }

        public enum NavigationDirection
        {
            Left, Right, Up, Down
        }

        public static string GetName(EntityType type)
        {
            switch (type)
            {
                case EntityType.Target: return "TRGT";
                case EntityType.Missile: return "MISL";
                default: return "N/A";
            }
        }

        public static string GetName(MissileStage stage)
        {
            switch (stage)
            {
                case MissileStage.Unknown: return "N/A";
                case MissileStage.Launching: return "LAUNCHING";
                case MissileStage.Flying: return "FLYING";
                case MissileStage.Interception: return "INTERCEPTION";
                default: return "N/A";
            }
        }

        public static string GetName(MissileType type)
        {
            switch (type)
            {
                case MissileType.Unknown: return "N/A";
                case MissileType.MCLOS: return "MCLOS";
                default: return "N/A";
            }
        }

        public static string GetName(MissilePayload payload)
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
    }
}
