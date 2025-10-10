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
        public enum MissileGuidanceType : byte
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

        public enum Direction
        {
            Left, Right, Up, Down, Forward, Backward
        }
    }
}
