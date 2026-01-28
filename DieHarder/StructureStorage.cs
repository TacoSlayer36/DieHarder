using Il2CppRUMBLE.MoveSystem;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DieHarder
{
    public class StructureStorage
    {
        public GameObject StructureGO;
        public Vector3 Pos;
        public Quaternion Rot;
        public Vector3 Velocity;

        public enum StructureType
        {
            Unknown = 0,
            Disc = 1,
            Ball = 2,
            Pillar = 3,
            BoulderBall = 4,
            RockCube = 5,
            SmallRock = 6,
            Wall = 7,
            LargeRock = 8
        }
        public StructureType Type;

        public static StructureType ParseType(string type)
        {
            switch (type)
            {
                case "Disc": return StructureType.Disc;
                case "Ball": return StructureType.Ball;
                case "Pillar": return StructureType.Pillar;
                case "BoulderBall": return StructureType.BoulderBall;
                case "RockCube": return StructureType.RockCube;
                case "SmallRock": return StructureType.SmallRock;
                case "Wall": return StructureType.Wall;
                case "LargeRock": return StructureType.LargeRock;
                default: return StructureType.Unknown;
            }
        }

        public override string ToString()
        {
            return $"{Type}: {Pos}";
        }

        public static List<StructureStorage> GenerateStructureStorages()
        {
            List<StructureStorage> structures = new();

            foreach (var pool in Core.Instance.StructurePools)
            {
                foreach (var poolObject in pool.PooledObjects)
                {
                    GameObject go = poolObject.gameObject;

                    if (!go.activeSelf) continue;

                    Match match = Regex.Match(pool.poolParent.name, @"Pool: ([a-zA-Z]+) ");
                    string structureTypeString = match.Groups[1].Value;
                    StructureStorage.StructureType structureType = StructureStorage.ParseType(structureTypeString);

                    StructureStorage newStorage = new StructureStorage
                    {
                        StructureGO = go,
                        Pos = go.transform.position,
                        Rot = go.transform.rotation,
                        Velocity = go.GetComponent<Rigidbody>().velocity,
                        Type = structureType,
                    };
                    structures.Add(newStorage);
                }
            }

            return structures;
        }
    }

    public class StructureKillStorage
    {
        Structure __instance = null;
        Vector3 killVelocity;
        bool playSFX;
        bool playVFX;
        bool networked;

        public void Kill()
        {
            if (__instance != null)
                __instance.Kill(killVelocity, playSFX, playVFX, networked);
        }

        public StructureKillStorage(Structure instance, Vector3 killVelocity, bool playSFX, bool playVFX, bool networked)
        {
            __instance = instance;
            this.killVelocity = killVelocity;
            this.playSFX = playSFX;
            this.playVFX = playVFX;
            this.networked = networked;
        }
    }
}
