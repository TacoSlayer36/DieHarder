using Il2CppRootMotion;
using Il2CppRUMBLE.MoveSystem;
using Il2CppSystem;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
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

        public static List<GameObject> ProcessedStructuresForPhysics = new();

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

                    Rigidbody rb = go.GetComponent<Rigidbody>();
                    Vector3 vel = rb?.velocity == null ? Vector3.zero : rb.velocity;

                    StructureStorage newStorage = new StructureStorage
                    {
                        StructureGO = go,
                        Pos = go.transform.position,
                        Rot = go.transform.rotation,
                        Velocity = vel,
                        Type = structureType,
                    };
                    structures.Add(newStorage);

                    if (!ProcessedStructuresForPhysics.Contains(go))
                        ProcessStructureForPhysics(newStorage);
                }
            }

            return structures;
        }

        public static void ProcessStructureForPhysics(StructureStorage structureStorage)
        {
            GameObject structureGo = structureStorage.StructureGO;
            Transform t = structureGo.transform;
            ProcessedStructuresForPhysics.Add(structureGo);

            StructureStorage.StructureType structureType = structureStorage.Type;
            GameObject newCollider = new GameObject("RagdollCollider");

            structureGo.GetComponentInChildren<Rigidbody>().excludeLayers = Core.Instance.PhysicsLayerMask;

            if (structureType is StructureType.Disc or StructureType.Ball)
            {
                MeshCollider meshCollider = t.GetChild(0).GetComponent<MeshCollider>();
                newCollider.AddComponent<MeshCollider>().sharedMesh = meshCollider.sharedMesh;
            }

            if (structureType is StructureType.Pillar or StructureType.RockCube or StructureType.Wall)
            {
                BoxCollider boxCollider = t.GetChild(0).GetComponent<BoxCollider>();
                newCollider.AddComponent<BoxCollider>().size = boxCollider.size;
            }

            if (structureType is StructureType.SmallRock or StructureType.LargeRock or StructureType.BoulderBall)
            {
                MeshCollider meshCollider = t.GetComponent<MeshCollider>();
                newCollider.AddComponent<MeshCollider>().sharedMesh = meshCollider.sharedMesh;
            }

            newCollider.layer = Core.Instance.PhysicsLayer;
            newCollider.transform.SetParent(t, false);
            Rigidbody rb = newCollider.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.includeLayers = Core.Instance.PhysicsLayerMask;
        }
    }

    [RegisterTypeInIl2Cpp]
    public class SubmissiveCollider : MonoBehaviour
    {
        Rigidbody self;

        void Start()
        {
            self = GetComponent<Rigidbody>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Move"))
            {

                Vector3 impulse = collision.impulse;
                List<ContactPoint> contacts = new();
                contacts = collision.contacts.ToList();

                //impulse = impulse / contacts.Count; // number of contacts, length of the array

                foreach (ContactPoint contact in contacts)
                {
                    self.AddForceAtPosition(contact.impulse, contact.point, ForceMode.Impulse);
                    collision.rigidbody.AddForceAtPosition(contact.impulse, contact.point, ForceMode.Impulse);
                    //contact.impulse = Vector3.zero;
                }
            }
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
