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
        public struct MissileInfoLite
        {
            public long LauncherID { get; private set; }
            public bool IsValid { get; private set; }

            public MissileInfoLite(long launcherID)
            {
                LauncherID = launcherID;
                IsValid = true;
            }

            public byte[] Serialize()
            {
                List<byte> bytes = new List<byte>();
                bytes.AddRange(BitConverter.GetBytes(LauncherID));
                return bytes.ToArray();
            }

            public static MissileInfoLite Deserialize(byte[] data, int offset)
            {
                int index = offset;
                long launcherID = BitConverter.ToInt64(data, index);
                return new MissileInfoLite(launcherID);
            }
        }
    }
}
