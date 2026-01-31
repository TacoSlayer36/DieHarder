using Il2CppRootMotion;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Ragdoll : PlayerVisualsClone
    {
        public List<BoneRef> BoneRefs = new();

        private Dictionary<Joint, Transform> boneAnchorCache = new();

        public static GameObject CreateRagdoll(PlayerController player)
        {
            GameObject newGo = GameObject.Instantiate(Core.Instance.ModObject_DDOLRagdoll);
            newGo.name = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername + "Ragdoll");
            newGo.transform.SetParent(Core.Instance.ModObject_Ragdolls.transform);
            Ragdoll newRagdoll = newGo.AddComponent<Ragdoll>();
            newRagdoll.ParentController = player;
            newRagdoll.SetupRagdoll();
            return newGo;
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

            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                string[] layers = new string[] { "Floor", "CombatFloor", "Environment", "LeanableEnvironment", "PedestalFloor" };
                rb.includeLayers = new LayerMask().AddToMask(layers);
                rb.includeLayers = rb.includeLayers | Core.Instance.PhysicsLayerMask;
                rb.excludeLayers = LayerMask.GetMask("Move");
                rb.gameObject.layer = Core.Instance.PhysicsLayer;
                rb.ResetCenterOfMass();

                //rb.gameObject.AddComponent<SubmissiveCollider>();
            }

            MelonCoroutines.Start(C_EnableCollideWithPlayers(0.5f));

            CacheOriginalJointData();
            CopyPose();
        }

        IEnumerator C_EnableCollideWithPlayers(float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                string[] layers = new string[] { "PlayerOnPlayerInteraction" };
                rb.includeLayers = rb.includeLayers.AddToMask(layers);
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
    }
}
