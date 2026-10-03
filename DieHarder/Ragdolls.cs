using Il2CppPlayFab.MultiplayerModels;
using Il2CppRootMotion;
using Il2CppRUMBLE.Audio;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.MoveSystem;
using Il2CppRUMBLE.Physics.Utility;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Presence;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SocialPlatforms;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Ragdoll : PlayerVisualsClone
    {
        public static Dictionary<PlayerController, RagdollPool> RagdollPools = new();
        public static Dictionary<string, Material> PlayerMats = new();
        public static Dictionary<Renderer, Material> MiscMats = new();
        public static Material LocalHeadClippedMat = null;
        public static List<PlayerController> ghosts = new();

        public List<BoneRef> BoneRefs = new();
        private Dictionary<Joint, Transform> boneAnchorPosStorage = new();
        private Dictionary<Joint, Vector3> originalBoneAnchors = new();

        public bool IsActive = true;
        public float Age = 0f;
        public float ClearAfterSeconds = 0f;
        public bool UndoGhostOnClear = true;
        public Transform Chest;

        public float Drama = 1f;

        public object SinkRoutine = null;

        public bool IsJanky = false;
        public bool DoSmashLaunch = false;
        public Vector3 SmashLaunchDir = Vector3.zero;

        public static Ragdoll SpawnRagdoll(PlayerController player, StructureStorage killingStructure = null, float drama = 1f)
        {
            Ragdoll newRagdoll;
            RagdollPool ownerPool = FindOrCreateRagdollPool(player);
            newRagdoll = ownerPool.FetchRagdoll();

            if (Core.Instance.CurrentScene == "Map0")
            {
                Vector3 pos = player.GetStandingPosition();
                float lateralDist = new Vector3(pos.x, 0f, pos.z).magnitude;
                if (player.GetStandingPosition().y <= -0.09f || lateralDist >= 12f)
                    Core.Instance.PlayersKilledToGutter.Add(player);
            }

            bool launchFromGutter = Core.Instance.PlayersKilledToGutter.Contains(player);

            bool isPerDamage = Core.Instance.IsInMatch && (int)Config.RagdollsInMatches.Value >= 4 || !Core.Instance.IsInMatch && (int)Config.RagdollsOutsideMatches.Value >= 3;
            newRagdoll.Drama = drama;
            float dramaToUse = launchFromGutter ? 1f : drama;

            if (killingStructure != null)
            {
                newRagdoll.Hit(killingStructure);
            }
            else if (!launchFromGutter)
            {
                Vector3 vel = new Vector3(Random.RandomRange(-15, 15), Random.RandomRange(23, 29), Random.RandomRange(-15, 15));
                newRagdoll.AddVelocity(vel * dramaToUse);
            }
            else
            {
                Vector3 launchLateral = new Vector3(newRagdoll.Chest.position.x, 0f, newRagdoll.Chest.position.z).normalized * -35f;
                //if (isPerDamage) launchLateral *= 0.85f;
                Vector3 launchDir = launchLateral + Vector3.up * Random.RandomRange(90f, 150f) * 0.7f;
                newRagdoll.AddVelocity(launchDir);
            }

            if (isPerDamage)
            {
                newRagdoll.AddVelocity(newRagdoll.Chest.GetComponentInChildren<Rigidbody>().velocity);
                Vector3 vel = new Vector3(Random.RandomRange(-15, 15), Random.RandomRange(23, 29), Random.RandomRange(-15, 15));
                vel *= 0.85f;
                newRagdoll.AddVelocity(vel * dramaToUse);
            }

            return newRagdoll;
        }

        public static RagdollPool FindOrCreateRagdollPool(PlayerController player)
        {
            if (player?.PlayerVisuals?.GetComponentInChildren<SkinnedMeshRenderer>() == null)
            {
                Debug.Log("Could not create silhouette for player " + HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername), false, 2);
                return null;
            }

            if (RagdollPools.ContainsKey(player)) return RagdollPools[player];

            string sanitizedName = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername + "RagdollPool");
            GameObject newGo = new GameObject(sanitizedName);
            newGo.transform.SetParent(Core.Instance.ModObject_Ragdolls.transform, true);
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
                    if (ragdoll == null) continue;
                    ragdoll.SinkRoutine = MelonCoroutines.Start(ragdoll.Sink());
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
                    ragdoll.AddVelocity(explodeDir * proximityMult * structureMult * 75f);
                }
            }
        }

        public void SetupRagdoll()
        {
            Type = VisualsType.Ragdoll;
            Visuals = GameObject.Instantiate(ParentController.PlayerVisuals.gameObject);
            Visuals.SetActive(false);
            SetUp(Visuals);

            Visuals.transform.SetParent(transform);
            Chest = transform.GetChild(0).GetChild(0).GetChild(3);

            PlayerMeasurement parentMeasurement = ParentController.assignedPlayer.Data.PlayerMeasurement;
            float height = parentMeasurement.Length;
            float armSpam = parentMeasurement.ArmSpan;

            GrabBoneRefs();

            SkinnedMeshRenderer mySmr = Visuals.GetComponentInChildren<SkinnedMeshRenderer>();
            mySmr.gameObject.layer = 0;
            if (PlayerMats.ContainsKey(ParentController.assignedPlayer.Data.GeneralData.PlayFabMasterId))
            {
                mySmr.material = PlayerMats[ParentController.assignedPlayer.Data.GeneralData.PlayFabMasterId];
            }
            
            mySmr.material.SetInt("_IsLocalPlayer", 0);

            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
            {
                string[] layers = new string[] { "Floor", "CombatFloor", "Environment", "Leanable", "PedestalFloor" };
                rb.includeLayers = new LayerMask().AddToMask(layers);
                rb.includeLayers = rb.includeLayers | Core.Instance.PhysicsLayerMask;
                rb.gameObject.tag = "Audio_Stone";
                if (!Config.LegacyRagdollJank.Value)
                rb.excludeLayers = LayerMask.GetMask("Move", "PlayerController", "PlayerHitbox", "PlayerPhysics", "PlayerPhysicsTransform", "PlayerFeet", "PlayerOnPlayerInteraction");
                rb.gameObject.layer = Core.Instance.PhysicsLayer;
                if (rb.name.Contains("Foot") || rb.name.Contains("Head") || rb.name.Contains("Hand") || rb.name.Contains("Spine_A"))
                    AddImpactAudio(rb.gameObject, true);
                if (rb.name.Contains("Pelvis"))
                    AddImpactAudio(rb.gameObject, false);
                rb.ResetCenterOfMass();
            }

            CacheOriginalJointData();
            CopyPose();

            if (Config.LegacyRagdollJank.Value)
            {
                float mult = Random.RandomRange(0.5f, 1.5f);
                if (Random.RandomRangeInt(0, 2) == 0)
                    mult *= mult;
                if (Random.RandomRangeInt(0, 15) == 0)
                    mult *= mult * mult * mult;

                foreach (BoneRef boneRef in BoneRefs)
                {
                    boneRef.RagdollBone.localScale *= mult;
                }
            }
        }

        public void GrabBoneRefs()
        {
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
        }

        IEnumerator C_SetLayersDelayed()
        {
            foreach (BoneRef boneRef in BoneRefs)
            {
                boneRef.RagdollBone.gameObject.layer = 0;
            }

            yield return new WaitForSeconds(1f);

            foreach (BoneRef boneRef in BoneRefs)
            {
                boneRef.RagdollBone.gameObject.layer = Core.Instance.PhysicsLayer;
            }
        }

        void Update()
        {
            Age += Time.deltaTime;
        }

        void FixedUpdate()
        {
            if (SinkRoutine != null) return;
            
            TrackVisualsToRbBones();

            if (ClearAfterSeconds > 0 && Age >= ClearAfterSeconds)
            {
                SinkRoutine = MelonCoroutines.Start(Sink());
            }

            if (!Core.Instance.IsInMatch && Config.CleanupOutsideMatches.Value > 0f && Age >= 8.5f && ghosts.Contains(ParentController)) UnGhostifyOwner();

            if (Chest.position.y < -20f) SetActive(false);
            if (Chest.position.magnitude > 300f) SetActive(false);

            if (DoSmashLaunch && SmashLaunchDir.magnitude > 0.1f)
            {
                AddVelocity(SmashLaunchDir * 20f);
            }
        }

        private void TrackVisualsToRbBones()
        {
            foreach (BoneRef boneRef in BoneRefs)
            {
                if (!IsJanky || !Config.LegacyRagdollJank.Value)
                {
                    boneRef.VisualBone.transform.position = boneRef.RagdollBone.transform.position;
                    boneRef.VisualBone.transform.rotation = boneRef.RagdollBone.transform.rotation;
                }
                else
                {
                    boneRef.VisualBone.transform.localPosition = boneRef.RagdollBone.transform.position;
                    boneRef.VisualBone.transform.localRotation = boneRef.RagdollBone.transform.rotation;
                }
            }
        }

        private void AddImpactAudio(GameObject gameObject, bool isImpact)
        {
            AudioSource aud = gameObject.AddComponent<AudioSource>();
            aud.spatialBlend = 1.0f;
            aud.spatialize = true;
            gameObject.AddComponent<RagdollAudio>().IsImpact = isImpact;
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
            }

            HelperFunctions.CopyAllTransforms(parentBones, ragdollBones);

            ResetAnchors();

            if ((Core.Instance.IsInMatch && (int)Config.RagdollsInMatches.Value == 4) || (!Core.Instance.IsInMatch && (int)Config.RagdollsOutsideMatches.Value == 3))
            {
                MelonCoroutines.Start(C_SetLayersDelayed());
            }

            TrackVisualsToRbBones();
            Visuals.SetActive(true);
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

                if (!boneAnchorPosStorage.ContainsKey(joint))
                    CacheOriginalJointData();

                Vector3 newAnchorPos = joint.transform.InverseTransformPoint(boneAnchorPosStorage[joint].transform.position);
                if (!Config.LegacyRagdollJank.Value)
                {
                    joint.connectedAnchor = originalBoneAnchors[joint] * (ragdollBone.lossyScale.x / 100f);
                }
                else
                {
                    float divisor = Random.RandomRange(50f, 180f);
                    joint.connectedAnchor = originalBoneAnchors[joint] * (ragdollBone.lossyScale.x / divisor);
                }
            }
        }

        public void AddVelocity(Vector3 velocity)
        {
            if (Config.RagdollVelocity.EditedValue is not Config.RagdollVelocityType.Default && Config.EnableFilmingFeatures.EditedValue) return;
            Chest.GetComponent<Rigidbody>().AddForce(velocity, ForceMode.VelocityChange);
        }

        public void SetVelocity(Vector3 velocity)
        {
            foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>()) rb.velocity = velocity;
        }

        public void Hit(StructureStorage killingStructure)
        {
            if (killingStructure == null || killingStructure.StructureGO == null) return;

            Rigidbody chestRB = transform.GetChild(0).GetChild(0).GetChild(3).GetComponent<Rigidbody>();
            if (killingStructure.Velocity.magnitude > 0.01f)
            {
                Vector3 defaultVel = (killingStructure.Pos - chestRB.transform.position).normalized;
                Vector3 actualVel = killingStructure.Velocity.magnitude < 0.5f ? defaultVel : killingStructure.Velocity;
                chestRB.AddForceAtPosition(actualVel * Drama * 20f, killingStructure.Pos, ForceMode.Impulse);
            }
            else
            {
                Vector3 playerVel = ParentController.PlayerPhysics.physicsRigidbody.velocity;
                playerVel = new Vector3(playerVel.x, playerVel.y / 2f, playerVel.z);
                chestRB.AddForce(playerVel * 10f * Drama, ForceMode.VelocityChange);
            }
        }

        public void SetActive(bool active)
        {
            if (!active)
            {
                if (UndoGhostOnClear)
                    UnGhostifyOwner();
                foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
                {
                    rb.isKinematic = false;
                    rb.velocity = Vector3.zero;
                    Collider c = rb.GetComponent<Collider>();
                    if (c != null) c.enabled = true;
                }
                IsJanky = false;
                DoSmashLaunch = false;
            }

            gameObject.SetActive(false);
        }

        public IEnumerator Sink()
        {
            int t = 0;

            foreach (Rigidbody rb in transform.GetChild(0).GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
                Collider c = rb.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }

            while (t++ < 50)
            {
                yield return new WaitForFixedUpdate();
                Visuals.transform.Translate(Vector3.down * 0.0005f * t);
            }
            SetActive(false);
        }

        public static void Ghostify(PlayerController player)
        {
            if (!Config.EnableGhostification.Value) return;
            if (player.controllerType is ControllerType.Local && LocalHeadClippedMat == null) return;
            if (player.controllerType is not ControllerType.Local && !PlayerMats.ContainsKey(player.assignedPlayer.Data.GeneralData.PlayFabMasterId)) return;

            SkinnedMeshRenderer smr = player.PlayerVisuals.GetComponentInChildren<SkinnedMeshRenderer>();
            if (!(Config.InvisibleGhosts.EditedValue && Config.EnableFilmingFeatures.EditedValue))
                smr.material = Core.Instance.GhostMat;
            else
                smr.material = Core.Instance.InvisibleMat;

                //bool rockCamBeingUsed = Core.FindRockCamBeingUsed();

            float isLocal = player.controllerType == Il2CppRUMBLE.Players.ControllerType.Local ? 1f : 0f;
            smr.material.SetFloat("_IsLocal", isLocal);

            foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;

                if (r.transform.parent.GetComponent<ShiftStone>() != null)
                {
                    if (!MiscMats?.ContainsKey(r) ?? false)
                    {
                        r.material.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                        MiscMats[r] = r.material;
                    }
                    if (!(Config.InvisibleGhosts.EditedValue && Config.EnableFilmingFeatures.EditedValue))
                        r.material = Core.Instance.GhostMat;
                    else
                        r.material = Core.Instance.InvisibleMat;
                }
            }

            ghosts.Add(player);
        }

        public void GhostifyOwner()
        {
            Ghostify(ParentController);
        }

        public static void UnGhostify(PlayerController player)
        {
            PlayerVisuals pv = player.PlayerVisuals;
            SkinnedMeshRenderer smr = pv.GetComponentInChildren<SkinnedMeshRenderer>();
            if (player.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local && LocalHeadClippedMat != null)
                smr.material = LocalHeadClippedMat;
            else
            {
                if (!PlayerMats.ContainsKey(player.assignedPlayer.Data.GeneralData.PlayFabMasterId)) return;

                smr.material = PlayerMats[player.assignedPlayer.Data.GeneralData.PlayFabMasterId];
            }

            foreach (Renderer r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;

                if (MiscMats?.ContainsKey(r) ?? false)
                {
                    r.material = MiscMats[r];
                }
            }

            if (ghosts.Contains(player)) ghosts.Remove(player);
        }

        public static void UnGhostifyAllGhosts()
        {
            List<PlayerController> ghostList = new List<PlayerController>(ghosts);
            foreach (PlayerController player in ghostList)
                UnGhostify(player);
        }

        public void UnGhostifyOwner()
        {
            UnGhostify(ParentController);
        }

        public void ClearAfter(float time, bool ghostify = true)
        {
            if (ghostify) GhostifyOwner();
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
            public bool AnyRagdollsEnabled => PoolItems?.Any(r => r.IsActive && r.gameObject.active) ?? false;
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
                    List<Ragdoll> inactivePoolItems = PoolItems.Where(pr => !pr.IsActive).ToList();
                    if (inactivePoolItems.Count == 0) poolRagdoll = CreateRagdoll();
                    else poolRagdoll = inactivePoolItems.First();
                }

                if (poolRagdoll.SinkRoutine != null) MelonCoroutines.Stop(poolRagdoll.SinkRoutine);
                poolRagdoll.SinkRoutine = null;
                poolRagdoll.gameObject.SetActive(true);
                poolRagdoll.IsActive = true;
                poolRagdoll.CopyPose();

                if (Config.LegacyRagdollJank.Value)
                {
                    poolRagdoll.IsJanky = Random.RandomRangeInt(0, 30) == 0;
                    if (Random.RandomRangeInt(0, 10) == 0)
                    {
                        poolRagdoll.AddVelocity(new Vector3(Random.RandomRange(-100f, 100f), Random.RandomRange(-100f, 100f), Random.RandomRange(-100f, 100f)));
                    }
                }

                return poolRagdoll;
            }

            public Ragdoll CreateRagdoll()
            {
                GameObject newGo = GameObject.Instantiate(Core.Instance.ModObject_DDOLRagdoll);
                newGo.SetActive(true);
                newGo.name = HelperFunctions.SanitizeString(parentController.assignedPlayer.Data.GeneralData.PublicUsername + "Ragdoll");
                newGo.transform.SetParent(RagdollPools[parentController].Transform);
                Ragdoll newRagdoll = newGo.AddComponent<Ragdoll>();
                newRagdoll.ParentController = parentController;
                newRagdoll.SetupRagdoll();
                return newRagdoll;
            }
        }
    }

    [RegisterTypeInIl2Cpp]
    public class RagdollAudio : MonoBehaviour
    {
        AudioSource audioSource;
        Rigidbody rigidbody;
        public bool IsImpact = true;
        bool isWhooshing = false;
        List<Vector3> vels = new();

        public AudioClip AudioClip => audioSource.clip;
        public float Volume => audioSource.volume;

        public void Play()
        {
            audioSource.Play();
        }

        void Start()
        {
            audioSource = GetComponent<AudioSource>();
            rigidbody = GetComponent<Rigidbody>();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (audioSource == null) return;
            if (!IsImpact) return;

            float relativeVel = HelperFunctions.GetRelativeVelocity(vels[0], collision).magnitude;

            bool playSoft = false;
            if (relativeVel <= 2 && Core.Instance.RagdollAudioClipsSoft.Count > 0) playSoft = true;

            if (!playSoft)
            {
                if (Core.Instance.RagdollAudioClipsHard.Count > 0)
                {
                    audioSource.clip = Core.Instance.RagdollAudioClipsHard[Random.RandomRangeInt(0, Core.Instance.RagdollAudioClipsHard.Count)];
                    audioSource.volume = Mathf.Clamp01(relativeVel * 0.4f) * Config.RagdollSoundsVolume.Value;
                }
            }
            else
            {
                audioSource.clip = Core.Instance.RagdollAudioClipsSoft[Random.RandomRangeInt(0, Core.Instance.RagdollAudioClipsSoft.Count)];
                audioSource.volume = Mathf.Clamp01(relativeVel * 0.6f) * Config.RagdollSoundsVolume.Value;
            }

            audioSource.Play();
        }

        void FixedUpdate()
        {
            vels.Add(rigidbody.velocity);
            if (vels.Count > 1) vels.RemoveAt(0);

            if (IsImpact) return;
            if (audioSource == null) return;

            if (audioSource.clip == null)
            {
                audioSource.clip = Core.Instance.StructurePools[0]?.PoolItem?.GetComponentInChildren<Structure>()?.whooshAudioCall?.Clips?.FirstOrDefault()?.Clip;
            }
            if (audioSource == null) return;

            audioSource.loop = true;

            if (rigidbody.velocity.magnitude > 0.5f && !isWhooshing)
            {
                audioSource.Play();
                isWhooshing = true;
            }
            if (rigidbody.velocity.magnitude < 0.5f && isWhooshing)
            {
                audioSource.Stop();
                isWhooshing = false;
            }

            audioSource.volume = Mathf.Clamp01((rigidbody.velocity.magnitude - 1f) * 0.4f) * Config.RagdollSoundsVolume.Value;
        }
    }
}
