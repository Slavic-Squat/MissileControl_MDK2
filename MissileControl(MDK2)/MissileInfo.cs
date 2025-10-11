using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
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
        public struct MissileInfo
        {
            public long LauncherID { get; private set; }
            public MissileStage Stage { get; private set; }
            public MissileType Type { get; private set; }
            public MissileGuidanceType GuidanceType { get; private set; }
            public MissilePayload Payload { get; private set; }
            public long TargetID { get; private set; }
            public bool IsValid { get; private set; }

            public MissileInfo(long launcherID, long targetID, MissileStage stage, MissileType type, MissileGuidanceType guidanceType, MissilePayload payload)
            {
                LauncherID = launcherID;
                TargetID = targetID;
                Stage = stage;
                Type = type;
                Payload = payload;
                GuidanceType = guidanceType;
                IsValid = true;
            }

            public byte[] Serialize()
            {
                List<byte> bytes = new List<byte>();

                bytes.AddRange(BitConverter.GetBytes(LauncherID));
                bytes.Add((byte)Stage);
                bytes.Add((byte)Type);
                bytes.Add((byte)GuidanceType);
                bytes.Add((byte)Payload);
                bytes.AddRange(BitConverter.GetBytes(TargetID));
                return bytes.ToArray();
            }

            public static MissileInfo Deserialize(byte[] data, int offset)
            {
                int index = offset;
                long launcherID = BitConverter.ToInt64(data, index);
                index += 8;
                MissileStage stage = (MissileStage)data[index];
                index += 1;
                MissileType type = (MissileType)data[index];
                index += 1;
                MissileGuidanceType guidanceType = (MissileGuidanceType)data[index];
                index += 1;
                MissilePayload payload = (MissilePayload)data[index];
                index += 1;
                long targetID = BitConverter.ToInt64(data, index);
                index += 8;
                return new MissileInfo(launcherID, targetID, stage, type, guidanceType, payload);
            }
        }
    }
}
