using Il2CppRootMotion;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Ragdoll : PlayerVisualsClone
    {
        public static Dictionary<PlayerController, RagdollPool> RagdollPools = new();
        public static Material LocalHeadClippedMat = null;

        public List<BoneRef> BoneRefs = new();
        private Dictionary<Joint, Transform> boneAnchorPosStorage = new();
        private Dictionary<Joint, Vector3> originalBoneAnchors = new();
        public float Age = 0f;
        public float ClearAfterSeconds = 0f;
        public bool UndoGhostOnClear = false;
        public Transform Chest;

        public static Ragdoll SpawnRagdoll(PlayerController player, StructureStorage killingStructure = null)
        {
            Ragdoll newRagdoll;
            RagdollPool ownerPool = FindOrCreateRagdollPool(player);
            newRagdoll = ownerPool.FetchRagdoll();

            bool launchFromGutter = Core.Instance.CurrentScene == "Map0" && newRagdoll.Chest.position.y <= -3 && newRagdoll.Chest.position.y >= -8;

            if (killingStructure != null)
            {
                newRagdoll.Hit(killingStructure);
            }
            else if (!launchFromGutter)
            {
                newRagdoll.AddVelocity(new Vector3(Random.RandomRange(-30, 30), 70, Random.RandomRange(-30, 30)));
            }
            else
            {
                Vector3 launchLateral = new Vector3(newRagdoll.Chest.position.x, 0f, newRagdoll.Chest.position.z).normalized * -25f;
                newRagdoll.AddVelocity(launchLateral + Vector3.up * 100f);
            }

            return newRagdoll;
        }

        public static RagdollPool FindOrCreateRagdollPool(PlayerController player)
        {
            if (RagdollPools.ContainsKey(player)) return RagdollPools[player];
            
            string sanitizedName = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername + "RagdollPool");
            GameObject newGo = new GameObject(sanitizedName);
            newGo.transform.SetParent(Core.Instance.ModObject_Ragdolls.transform);
            RagdollPool newPool = new RagdollPool { parentController = player, Transform = newGo.transform };
            RagdollPools[player] = newPool;
            return newPool;
        }

        public static void ClearAllRagdolls()
        {
            foreach (RagdollPool pool in RagdollPools.Values)
            {
                foreach (Ragdoll ragdoll in pool?.PoolItems)
                {
                    ragdoll?.SetActive(false);
                }
            }
        }

        public static void ReapplyVisualsFor(PlayerController player)
        {
            foreach (RagdollPool ragdollPool in RagdollPools.Values)
            {
                foreach (Ragdoll ragdoll in ragdollPool.PoolItems)
                {
                    if (ragdoll.ParentController == player)
                        ragdoll.ReapplyVisuals();
                }
            }    
        }

        public static void Explode(StructureStorage source)
        {
            if (source == null) return;

            int structureType = (int)source.Type;
            float structureMult = 0f;
            if (structureType >= 2 && structureType <= 4)
                structureMult = 1f;
            else if (structureType == 5 || structureType == 6)
                structureMult = 2f;
            else if (structureType == 7)
                structureMult = 3f;
            else if (structureType == 8)
                structureMult = 5f;

            foreach (RagdollPool pool in RagdollPools.Values)
            {
                foreach (Ragdoll ragdoll in pool.PoolItems)
                {
                    float dist = Vector3.Distance(ragdoll.Chest.position, source.Pos);
                    dist = Mathf.Clamp(dist, 0.2f, 2.5f);
                    float proximityMult = 2.5f - dist;
                    Vector3 explodeDir = (ragdoll.Chest.position - source.Pos).normalized;
                    ragdoll.AddVelocity(explodeDir * proximityMult * structureMult * 100f);
                }
            }
        }

        public void SetupRagdoll()
        {
            Type = VisualsType.Ragdoll;
            Visuals = GameObject.Instantiate(ParentController.GetSubsystem<PlayerVisuals>().gameObject);
            Setup(Visuals);

            Visuals.transform.SetParent(transform);

            Chest = transform.GetChild(0).GetChild(0).GetChild(3);

            PlayerMeasurement parentMeasurement = ParentController.assignedPlayer.Data.PlayerMeasurement;
            float height = parentMeasurement.Length;
            float armSpam = parentMeasurement.ArmSpan;

            foreach (BoneDefinition visualBone in Visuals.GetComponent<RigDefinition>().boneDefinitions)
            {
                foreach (Transform ragdollBone in HelperFunctions.FindChildrenRecursive(transform.GetChild(0)))
                {
                    if (visualBone.Transform.name == ragdollBone.name)
                    {
                        Joint joint = ragdollBone.GetComponent<Joint>();
                        if (joint != null)
                        {
                            originalBoneAnchors[joint] = joint.connectedAnchor;
                        }

                        BoneRefs.Add(new BoneRef(visualBone.Transform.name, visualBone.Transform, ragdollBone));
                        break;
                    }
                }
            }

            PlayerVisuals parentPv = ParentController.GetSubsystem<PlayerVisuals>();
            SkinnedMeshRenderer parentSmr = parentPv.GetComponentInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer mySmr = Visuals.GetComponentInChildren<SkinnedMeshRenderer>();
            if (ParentController.ControllerType != Il2CppRUMBLE.Players.ControllerType.Local)
            {
                parentPv.NonHeadClippedMaterial = parentSmr.material;
                mySmr.material = new Material(parentPv.NonHeadClippedMaterial);
            }
            else mySmr.material = parentPv.NonHeadClippedMaterial;

            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                string[] layers = new string[] { "Floor", "CombatFloor", "Environment", "LeanableEnvironment", "PedestalFloor" };
                rb.includeLayers = new LayerMask().AddToMask(layers);
                rb.excludeLayers = LayerMask.GetMask("Move", "PlayerController", "PlayerHitbox", "PlayerPhysicsBone", "PlayerFeet", "PlayerOnPlayerInteraction");
                rb.excludeLayers = rb.excludeLayers | Core.Instance.PhysicsLayerMask;
                rb.gameObject.layer = Core.Instance.PhysicsLayer;
                rb.ResetCenterOfMass();
            }

            MelonCoroutines.Start(C_EnableCollideWithPlayers(1.0f));

            CacheOriginalJointData();
            CopyPose();
        }

        IEnumerator C_EnableCollideWithPlayers(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            if (this?.transform?.GetChild(0) != null)
            {
                foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
                {
                    if (rb == null) continue;
                    rb.excludeLayers = rb.excludeLayers.RemoveFromMask(new string[] { "PlayerOnPlayerInteraction" });
                    rb.excludeLayers = rb.excludeLayers & ~Core.Instance.PhysicsLayerMask;
                    rb.includeLayers = rb.includeLayers.AddToMask(new string[] { "PlayerOnPlayerInteraction" });
                    rb.includeLayers = rb.includeLayers | Core.Instance.PhysicsLayerMask;
                }
            }
        }

        void Update()
        {
            Age += Time.deltaTime;

            foreach (BoneRef boneRef in BoneRefs)
            {
                boneRef.VisualBone.transform.position = boneRef.RagdollBone.transform.position;
                boneRef.VisualBone.transform.rotation = boneRef.RagdollBone.transform.rotation;
            }

            if (ClearAfterSeconds > 0 && Age >= ClearAfterSeconds)
            {
                if (UndoGhostOnClear)
                {
                    UnGhostifyOwner();
                }
                SetActive(false);
            }

            if (transform.GetChild(0).GetChild(0).position.y < -20f) SetActive(false);
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
                    newGo.transform.SetParent(joint.connectedBody.transform);
                    newGo.transform.position = joint.transform.position;
                    newGo.name = joint.name;
                    boneAnchorPosStorage[joint] = newGo.transform;
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

            foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null) continue;
                rb.velocity = Vector3.zero;
                rb.excludeLayers = rb.excludeLayers.AddToMask(new string[] { "PlayerOnPlayerInteraction" });
                rb.includeLayers = rb.includeLayers.RemoveFromMask(new string[] { "PlayerOnPlayerInteraction" });
            }

            MelonCoroutines.Start(C_EnableCollideWithPlayers(1f));

            HelperFunctions.CopyAllTransforms(parentBones, ragdollBones);

            if (!ModUISettings.LegacyRagdollJank)
                ResetAnchors();
        }

        public void ResetAnchors()
        {
            List<Transform> ragdollBones = new();
            foreach (BoneRef boneRef in BoneRefs)
                ragdollBones.Add(boneRef.RagdollBone);

            foreach (Transform ragdollBone in ragdollBones)
            {
                Joint joint = ragdollBone.GetComponent<Joint>();
                if (joint == null) continue;

                Vector3 newAnchorPos = joint.transform.InverseTransformPoint(boneAnchorPosStorage[joint].transform.position);
                joint.connectedAnchor = originalBoneAnchors[joint] * (ragdollBone.lossyScale.x / 100f);
            }
        }

        public void AddVelocity(Vector3 velocity)
        {
            Chest.GetComponent<Rigidbody>().AddForce(velocity, ForceMode.VelocityChange);
        }

        public void Hit(StructureStorage killingStructure)
        {
            if (killingStructure == null || killingStructure.StructureGO == null) return;

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

        public void SetActive(bool active)
        {
            if (UndoGhostOnClear)
                UnGhostifyOwner();
            foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null) continue;
                rb.velocity = Vector3.zero;
            }
            gameObject.SetActive(active);
        }

        public void GhostifyOwner()
        {
            SkinnedMeshRenderer smr = ParentController.GetSubsystem<PlayerVisuals>().GetComponentInChildren<SkinnedMeshRenderer>();
            if (LocalHeadClippedMat == null)
            {
                LocalHeadClippedMat = smr.material;
                LocalHeadClippedMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            }
            smr.material = Core.Instance.GhostMat;
            float isLocal = ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local ? 1f : 0f;
            smr.material.SetFloat("_IsLocal", isLocal);
        }

        public void UnGhostifyOwner()
        {
            PlayerVisuals pv = ParentController.GetSubsystem<PlayerVisuals>();
            SkinnedMeshRenderer smr = pv.GetComponentInChildren<SkinnedMeshRenderer>();
            if (ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local)
                smr.material = LocalHeadClippedMat;
            else 
                smr.material = pv.NonHeadClippedMaterial;
        }

        public void ClearAfter(float time)
        {
            ClearAfterSeconds = Age + time;
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
            public List<Ragdoll> PoolItems
            {
                get
                {
                    List<Ragdoll> poolItems = Transform?.GetComponentsInChildren<Ragdoll>(true)?.ToList();
                    if (poolItems == null) return new List<Ragdoll>();
                    return poolItems;
                }
            }
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
                    List<Ragdoll> inactivePoolItems = PoolItems.Where(pr => !pr.gameObject.activeSelf).ToList();
                    if (inactivePoolItems.Count == 0) poolRagdoll = CreateRagdoll();
                    else poolRagdoll = inactivePoolItems.First();
                }
                
                poolRagdoll.gameObject.SetActive(true);
                poolRagdoll.CopyPose();
                return poolRagdoll;
            }

            public Ragdoll CreateRagdoll()
            {
                GameObject newGo = GameObject.Instantiate(Core.Instance.ModObject_DDOLRagdoll);
                newGo.name = HelperFunctions.SanitizeString(parentController.assignedPlayer.Data.GeneralData.PublicUsername + "Ragdoll");
                newGo.transform.SetParent(RagdollPools[parentController].Transform);
                Ragdoll newRagdoll = newGo.AddComponent<Ragdoll>();
                newRagdoll.ParentController = parentController;
                newRagdoll.SetupRagdoll();
                return newRagdoll;
            }
        }
    }
}
