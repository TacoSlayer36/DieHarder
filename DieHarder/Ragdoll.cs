using Il2CppRootMotion;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Ragdoll : PlayerVisualsClone
    {
        public List<BoneRef> BoneRefs = new();
        private Dictionary<Joint, Transform> boneAnchorCache = new();

        private static Dictionary<PlayerController, RagdollPool> ragdollPools = new();

        public static Ragdoll SpawnRagdoll(PlayerController player, StructureStorage killingStructure = null)
        {
            Ragdoll newRagdoll;
            RagdollPool ownerPool = FindOrCreateRagdollPool(player);
            newRagdoll = ownerPool.FetchRagdoll();

            if (killingStructure != null)
            {
                newRagdoll.Hit(killingStructure);
            }

            return newRagdoll;
        }

        public static RagdollPool FindOrCreateRagdollPool(PlayerController player)
        {
            if (ragdollPools.ContainsKey(player)) return ragdollPools[player];
            
            string sanitizedName = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername + "RagdollPool");
            GameObject newGo = new GameObject(sanitizedName);
            newGo.transform.SetParent(Core.Instance.ModObject_Ragdolls.transform);
            RagdollPool newPool = new RagdollPool { parentController = player, Transform = newGo.transform };
            ragdollPools[player] = newPool;
            return newPool;
        }

        public void SetupRagdoll()
        {
            Type = VisualsType.Ragdoll;
            Visuals = GameObject.Instantiate(ParentController.GetSubsystem<PlayerVisuals>().gameObject);
            Setup(Visuals);

            Visuals.transform.SetParent(transform);

            PlayerMeasurement parentMeasurement = ParentController.assignedPlayer.Data.PlayerMeasurement;
            float height = parentMeasurement.Length;
            float armSpam = parentMeasurement.ArmSpan;

            foreach (BoneDefinition visualBone in Visuals.GetComponent<RigDefinition>().boneDefinitions)
            {
                foreach (Transform ragdollBone in HelperFunctions.FindChildrenRecursive(transform.GetChild(0)))
                {
                    if (visualBone.Transform.name == ragdollBone.name)
                    {
                        BoneRefs.Add(new BoneRef(visualBone.Transform.name, visualBone.Transform, ragdollBone));
                        break;
                    }
                }
            }

            if (ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local)
                Visuals.GetComponentInChildren<Renderer>().material = ParentController.GetSubsystem<PlayerVisuals>().NonHeadClippedMaterial;
            else
                Visuals.GetComponentInChildren<Renderer>().material = ParentController.GetComponentInChildren<SkinnedMeshRenderer>().material;

            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                string[] layers = new string[] { "Floor", "CombatFloor", "Environment", "LeanableEnvironment", "PedestalFloor" };
                rb.includeLayers = new LayerMask().AddToMask(layers);
                rb.excludeLayers = LayerMask.GetMask("Move", "PlayerController", "PlayerHitbox", "PlayerPhysicsBone", "PlayerFeet", "PlayerOnPlayerInteraction");
                rb.excludeLayers = rb.excludeLayers | Core.Instance.PhysicsLayerMask;
                rb.gameObject.layer = Core.Instance.PhysicsLayer;
                rb.ResetCenterOfMass();
            }

            MelonCoroutines.Start(C_EnableCollideWithPlayers(0.5f));

            CacheOriginalJointData();
            CopyPose();
        }

        IEnumerator C_EnableCollideWithPlayers(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
            {
                rb.excludeLayers = rb.excludeLayers.RemoveFromMask(new string[] { "PlayerOnPlayerInteraction" });
                rb.excludeLayers = rb.excludeLayers & ~Core.Instance.PhysicsLayerMask;
                rb.includeLayers = rb.includeLayers.AddToMask(new string[] { "PlayerOnPlayerInteraction" });
                rb.includeLayers = rb.includeLayers | Core.Instance.PhysicsLayerMask;
            }
        }

        void Update()
        {
            foreach (BoneRef boneRef in BoneRefs)
            {
                boneRef.VisualBone.position = boneRef.RagdollBone.position;
                boneRef.VisualBone.rotation = boneRef.RagdollBone.rotation;
                boneRef.VisualBone.localScale = boneRef.RagdollBone.localScale;
            }
        }

        private void CacheOriginalJointData()
        {
            foreach (BoneRef boneRef in BoneRefs)
            {
                Transform ragdollBone = boneRef.RagdollBone;

                Joint joint = ragdollBone.GetComponent<Joint>();
                if (joint != null)
                {
                    joint.autoConfigureConnectedAnchor = false;

                    GameObject newGo = new GameObject();
                    newGo.transform.localScale = Vector3.one * 0.03f;
                    newGo.transform.SetParent(joint.connectedBody.transform);
                    newGo.transform.position = joint.transform.position;
                    newGo.name = joint.name;
                    boneAnchorCache[joint] = newGo.transform;
                }
            }
        }

        new public void CopyPose()
        {
            List<Transform> parentBones = new();
            foreach (var bone in ParentController.GetComponentInChildren<RigDefinition>().boneDefinitions)
                parentBones.Add(bone.Transform);

            List<Transform> ragdollBones = new();
            foreach (BoneRef boneRef in BoneRefs)
                ragdollBones.Add(boneRef.RagdollBone);

            HelperFunctions.CopyAllTransforms(parentBones, ragdollBones);

            // Reset all joint anchors based on new scales
            foreach (Transform ragdollBone in ragdollBones)
            {
                Joint joint = ragdollBone.GetComponent<Joint>();
                if (joint == null) continue;

                Vector3 newAnchorPos = joint.transform.InverseTransformPoint(boneAnchorCache[joint].transform.position);
                joint.connectedAnchor *= ragdollBone.lossyScale.x / 100f;
            }
        }

        public void AddVelocity(Vector3 velocity)
        {
            foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
                rb.AddForce(velocity, ForceMode.VelocityChange);
        }

        public void Hit(StructureStorage killingStructure)
        {
            Rigidbody chestRB = transform.GetChild(0).GetChild(0).GetChild(3).GetComponent<Rigidbody>();
            if (killingStructure.Velocity.magnitude > 0.01f)
            {
                Vector3 defaultVel = (killingStructure.Pos - chestRB.transform.position).normalized;
                Vector3 actualVel = killingStructure.Velocity.magnitude < 0.5f ? defaultVel * killingStructure.Mass : killingStructure.Velocity * killingStructure.Mass;
                chestRB.AddForceAtPosition(actualVel * 0.09f, killingStructure.Pos, ForceMode.Impulse);
            }
            else
            {
                Vector3 playerVel = ParentController.GetSubsystem<PlayerPhysics>().physicsRigidbody.velocity;
                playerVel = new Vector3(playerVel.x, playerVel.y / 2f, playerVel.z);
                chestRB.AddForce(playerVel * 10f, ForceMode.VelocityChange);
            }
        }

        private struct JointData
        {
            public Vector3 worldAnchor;
            public Vector3 worldConnectedAnchor;
            public bool hasConnectedBody;
        }

        public class BoneRef
        {
            public string Name;
            public Transform VisualBone;
            public Transform RagdollBone;
            public BoneRef(string name, Transform visualsBone, Transform ragdollBone)
            {
                Name = name;
                VisualBone = visualsBone;
                RagdollBone = ragdollBone;
            }
        }

        public class RagdollPool
        {
            public PlayerController parentController;
            private List<Ragdoll> poolItems => Transform.GetComponentsInChildren<Ragdoll>().ToList();
            public Transform Transform;

            public Ragdoll FetchRagdoll()
            {
                Ragdoll poolRagdoll;

                if (Transform.childCount == 0)
                {
                    poolRagdoll = CreateRagdoll();
                }
                else
                {
                    List<Ragdoll> inactivePoolItems = poolItems.Where(pr => !pr.gameObject.activeSelf).ToList();
                    if (inactivePoolItems.Count == 0) poolRagdoll = CreateRagdoll();
                    else poolRagdoll = inactivePoolItems.First();
                }
                
                poolRagdoll.gameObject.SetActive(true);
                return poolRagdoll;
            }

            public Ragdoll CreateRagdoll()
            {
                GameObject newGo = GameObject.Instantiate(Core.Instance.ModObject_DDOLRagdoll);
                newGo.name = HelperFunctions.SanitizeString(parentController.assignedPlayer.Data.GeneralData.PublicUsername + "Ragdoll");
                newGo.transform.SetParent(ragdollPools[parentController].Transform);
                Ragdoll newRagdoll = newGo.AddComponent<Ragdoll>();
                newRagdoll.ParentController = parentController;
                newRagdoll.SetupRagdoll();
                return newRagdoll;
            }
        }
    }
}
