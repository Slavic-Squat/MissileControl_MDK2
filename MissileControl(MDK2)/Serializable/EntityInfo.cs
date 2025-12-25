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
        public struct EntityInfo
        {
            public long EntityID { get; private set; }
            public EntityType Type { get; private set; }
            public EntityInfoSubType SubType { get; private set; }
            public Vector3D Position { get; private set;  }
            public Vector3D Velocity { get; private set; }
            public double TimeRecorded { get; private set; }
            public MissileInfoLite? MissileInfoLite { get; private set; }
            public MissileInfo? MissileInfo { get; private set; }
            public bool IsValid { get; private set; }

            public EntityInfo(long entityID, Vector3D position, Vector3D velocity, double timeRecorded)
            {
                EntityID = entityID;
                Type = EntityType.Target;
                SubType = EntityInfoSubType.None;
                Position = position;
                Velocity = velocity;
                TimeRecorded = timeRecorded;
                MissileInfoLite = null;
                MissileInfo = null;
                IsValid = true;
            }

            public EntityInfo(MyDetectedEntityInfo entityInfo, double timeRecorded)
            {
                EntityID = entityInfo.EntityId;
                Type = EntityType.Target;
                SubType = EntityInfoSubType.None;
                Position = entityInfo.Position;
                Velocity = entityInfo.Velocity;
                TimeRecorded = timeRecorded;
                MissileInfoLite = null;
                MissileInfo = null;
                IsValid = true;
            }

            public EntityInfo(long entityID, Vector3D position, Vector3D velocity, double timeRecorded, MissileInfoLite missileInfoLite)
            {
                EntityID = entityID;
                Type = EntityType.Missile;
                SubType = EntityInfoSubType.MissileInfoLite;
                Position = position;
                Velocity = velocity;
                TimeRecorded = timeRecorded;
                MissileInfoLite = missileInfoLite;
                MissileInfo = null;
                IsValid = true;
            }

            public EntityInfo(long entityID, Vector3D position, Vector3D velocity, double timeRecorded, MissileInfo missileInfo)
            {
                EntityID = entityID;
                Type = EntityType.Missile;
                SubType = EntityInfoSubType.MissileInfo;
                Position = position;
                Velocity = velocity;
                TimeRecorded = timeRecorded;
                MissileInfoLite = null;
                MissileInfo = missileInfo;
                IsValid = true;
            }

            public EntityInfo Merge(EntityInfo entityInfo)
            {
                if (EntityID != entityInfo.EntityID)
                {
                    return this;
                }
                if (TimeRecorded < entityInfo.TimeRecorded)
                {
                    Position = entityInfo.Position;
                    Velocity = entityInfo.Velocity;
                    TimeRecorded = entityInfo.TimeRecorded;
                    if (entityInfo.MissileInfo.HasValue)
                    {
                        MissileInfo = entityInfo.MissileInfo;
                        Type = EntityType.Missile;
                        SubType = EntityInfoSubType.MissileInfo;
                        MissileInfoLite = null;
                    }
                }
                if (Type == EntityType.Target && entityInfo.Type == EntityType.Missile)
                {
                    Type = EntityType.Missile;
                    if (entityInfo.MissileInfo.HasValue)
                    {
                        MissileInfo = entityInfo.MissileInfo;
                        SubType = EntityInfoSubType.MissileInfo;
                        MissileInfoLite = null;
                    }
                    else if (entityInfo.MissileInfoLite.HasValue)
                    {
                        MissileInfoLite = entityInfo.MissileInfoLite;
                        SubType = EntityInfoSubType.MissileInfoLite;
                        MissileInfo = null;
                    }
                }
                return this;
            }

            public EntityInfo MergeKinematics(EntityInfo entityInfo)
            {
                if (EntityID != entityInfo.EntityID)
                {
                    return this;
                }
                if (TimeRecorded < entityInfo.TimeRecorded)
                {
                    Position = entityInfo.Position;
                    Velocity = entityInfo.Velocity;
                    TimeRecorded = entityInfo.TimeRecorded;
                }
                return this;
            }

            public byte[] Serialize()
            {
                List<byte> bytes = new List<byte>();

                bytes.Add((byte)Type);
                bytes.Add((byte)SubType);
                bytes.AddRange(BitConverter.GetBytes(EntityID));

                bytes.AddRange(BitConverter.GetBytes(Position.X));
                bytes.AddRange(BitConverter.GetBytes(Position.Y));
                bytes.AddRange(BitConverter.GetBytes(Position.Z));

                bytes.AddRange(BitConverter.GetBytes(Velocity.X));
                bytes.AddRange(BitConverter.GetBytes(Velocity.Y));
                bytes.AddRange(BitConverter.GetBytes(Velocity.Z));

                bytes.AddRange(BitConverter.GetBytes(TimeRecorded));

                switch (SubType)
                {
                    case EntityInfoSubType.MissileInfoLite:
                        {
                            bytes.AddRange(MissileInfoLite.Value.Serialize());
                            break;
                        }
                    case EntityInfoSubType.MissileInfo:
                        {
                            bytes.AddRange(MissileInfo.Value.Serialize());
                            break;
                        }
                }

                return bytes.ToArray();
            }

            public static EntityInfo Deserialize(byte[] bytes, int offset)
            {
                int index = offset;

                EntityType type = (EntityType)bytes[index];
                index += 1;

                EntityInfoSubType subType = (EntityInfoSubType)bytes[index];
                index += 1;

                long entityID = BitConverter.ToInt64(bytes, index);
                index += 8;

                double xPos = BitConverter.ToDouble(bytes, index);
                index += 8;

                double yPos = BitConverter.ToDouble(bytes, index);
                index += 8;

                double zPos = BitConverter.ToDouble(bytes, index);
                index += 8;

                Vector3D pos = new Vector3D(xPos, yPos, zPos);

                double xVel = BitConverter.ToDouble(bytes, index);
                index += 8;

                double yVel = BitConverter.ToDouble(bytes, index);
                index += 8;

                double zVel = BitConverter.ToDouble(bytes, index);
                index += 8;

                Vector3D vel = new Vector3D(xVel, yVel, zVel);

                double timeRecorded = BitConverter.ToDouble(bytes, index);
                index += 8;

                switch (subType)
                {
                    case EntityInfoSubType.MissileInfo:
                        {
                            MissileInfo missileInfo = Program.MissileInfo.Deserialize(bytes, index);
                            return new EntityInfo(entityID, pos, vel, timeRecorded, missileInfo);
                        }
                    case EntityInfoSubType.MissileInfoLite:
                        {
                            MissileInfoLite missileInfoLite = Program.MissileInfoLite.Deserialize(bytes, index);
                            return new EntityInfo(entityID, pos, vel, timeRecorded, missileInfoLite);
                        }
                    default:
                        {
                            return new EntityInfo(entityID, pos, vel, timeRecorded);
                        }
                }
            }
        }
    }
}
