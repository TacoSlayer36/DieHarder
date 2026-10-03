using DieHarder.DramaticEffects;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Comfort;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Settings;
using Il2CppRUMBLE.Utilities;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Impact : MonoBehaviour
    {
        public List<PlayerVisualsClone> InvolvedPlayers = new();
        public StructureStorage InvolvedStructure;
        public List<GameObject> StructureSilhouettes = new();
        public GameObject SphereBackground;

        public PlayerVisualsClone DamagedPlayer;
        public static PlayerHaptics PlayerHaptics => PlayerManager.instance.localPlayer.Controller.PlayerHaptics;
        public static object ForceHapticsRoutine;

        public float Drama = 1f;

        bool playedRumbleAudio = false;
        AudioSource activeRumbleAudio;

        private bool frozen = false;
        private float waitTime = 0.5f;
        private float waitedTime = 0f;

        public bool LivPlayersActive = true;

        public bool HowardInvolved = false;

        public Vector3 DamagePos => InvolvedStructure != null ?
                                    (InvolvedStructure.Pos + DamagedPlayer.ParentController.GetChest().position) / 2 :
                                    DamagedPlayer.ParentController.GetChest().position;

        object AnimationCoroutine;
        public static object StructureKillRoutine;
        public bool IsAnimationRunning = false;

        public static float FogEndDistanceStorage = -1f;

        public static Color GetColorFromSetting(string colorString, bool isPrimary)
        {
            if (colorString.ToLower() == "match")
            {
                Core.MatchResult matchResult = Core.Instance.GetMatchResultEdgeCase();
                Color color;

                switch (matchResult)
                {
                    case Core.MatchResult.Won: color = Color.green; break;
                    case Core.MatchResult.Lost: color = Color.red; break;
                    case Core.MatchResult.Tied: color = Color.yellow; break;
                    default: color = Color.gray; break;
                }

                if (!isPrimary && Config.PrimaryEffectColor.Value == "match" && Config.SecondaryEffectColor.Value == "match")
                    color = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f);
                return color;
            }
            else
            {
                Color color;
                if (ColorUtility.TryParseHtmlString(colorString, out color))
                {
                    return color;
                }
                else return isPrimary ? Color.white : Color.black;
            }
        }

        void Update()
        {
            if (frozen)
            {
                waitedTime += Time.deltaTime;
                runRumbleEffect();
            }
            freezeCameras();
        }

        void runRumbleEffect()
        {
            if (waitTime <= 0.8f) return;

            if (!playedRumbleAudio && waitedTime >= 0.3f)
            {
                playedRumbleAudio = true;
                activeRumbleAudio = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(Core.Buildup, DamagePos)?.AudioSource;
                if (activeRumbleAudio != null) Core.RemoveAudioFalloff(activeRumbleAudio);
            }

            float t = waitedTime / waitTime;
            float strength = Mathf.Clamp01(t);

            if (t <= 0.8)
            {
                strength = Mathf.Pow(strength, 2f);
                AddHaptics(strength, strength, strength);
            }
            if (t > 0.9)
            {
                activeRumbleAudio?.Stop();
            }
        }

        void freezeCameras()
        {
            if (!IsAnimationRunning) return;

            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                if (!cameraInfo.IsVRCam)
                {
                    cameraInfo.ParentComponent.transform.position = cameraInfo.FreezePos;
                    if (!cameraInfo.IsFirstPerson)
                        cameraInfo.ParentComponent.transform.rotation = cameraInfo.FreezeRot;
                }

                foreach (PlayerVisualsClone playerSilhouette in Core.Instance.PlayerSilhouettes.Values)
                {
                    if (playerSilhouette.Camera != null)
                    {
                        playerSilhouette.Camera.transform.rotation = PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.rotation;
                    }
                }
            }
        }

        public void RunAnimation()
        {
            AnimationCoroutine = MelonCoroutines.Start(C_RunAnimation());
        }

        public IEnumerator C_RunAnimation()
        {
            // Remove previously running animations on this Impact
            if (IsAnimationRunning) CancelAnimation();
            IsAnimationRunning = true;

            // Store and modify all camera info
            Core.Instance.StoredCameraInfos = Core.GenerateCamInfos();
            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                Camera parentComponent = cameraInfo.ParentComponent;
                parentComponent.cullingMask = Core.Instance.VisualLayerMask;

                if (cameraInfo.IsRecordingCam) parentComponent.nearClipPlane = 0.05f;

                if (cameraInfo.IsRecordingCam) cameraInfo.ParentComponent.GetComponent<RecordingCamera>().enabled = false;

                var stabilizer = cameraInfo.ParentComponent.GetComponentInParent<Il2CppLiv.Lck.Smoothing.LckStabilizer>();
                if (stabilizer != null) stabilizer.enabled = false;
            }

            // Disable fog
            if (FogEndDistanceStorage == -1)
                FogEndDistanceStorage = RenderSettings.fogEndDistance;
            RenderSettings.fogEndDistance = 10000f;

            // Move each silhouette into place (and turn on their camera)
            PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = false;
            foreach (PlayerVisualsClone playerSilhouette in InvolvedPlayers)
            {
                playerSilhouette.CopyPose();
                playerSilhouette.Visuals.SetActive(true);
                if (playerSilhouette.Camera != null) playerSilhouette.Camera.enabled = true;
                if (playerSilhouette.AudioListener != null) playerSilhouette.AudioListener.enabled = true;
            }

            // Create Howard silhouette if necessary
            if (HowardInvolved)
            {
                SkinnedMeshRenderer howardSmr = Core.Instance.HowardSmr;
                if (howardSmr != null)
                {
                    if (Config.PrimaryEffectColor.Value.ToLower() != "none")
                        howardSmr.material = Core.Instance.PrimarySilhouetteMat;
                    howardSmr.gameObject.layer = Core.Instance.VisualLayer;
                }
            }

            // Create structure silhouette
            if (Config.IncludeStructureInImpact.Value && InvolvedStructure != null)
                CreateStructureSilhouette(InvolvedStructure);

            // Create background
            CreateSphereBackground();

            // Play pre-impact sound
            Core.Instance.PlayBlendedAudio(DamagePos, true, Drama);

            // ---- FREEZE ----
            if (Config.VariableEffects.Value <= 0)
            {
                waitTime = Config.ImpactFrameDuration.Value / 1000f;
            }
            else
            {
                waitTime = 0.5f * Mathf.Pow(Drama, 1.7f);
                waitTime = Mathf.Clamp(waitTime, 0f, 2f);
            }

            if (waitTime > 0)
            {
                frozen = true;
                yield return new WaitForSeconds(waitTime);
            }

            frozen = false;

            // Create shockwave
            bool howardDied = false;
            if (HowardInvolved && Core.Instance.Howard != null && Core.Instance.Howard.currentHp == 0) howardDied = true;

            if (!HowardInvolved || (HowardInvolved && !howardDied) || Core.Instance.HowardSmr == null)
                Core.Instance.CreateShockwave(DamagePos, DamagedPlayer.ParentController, Drama);
            else
            {
                Vector3 howardPos = Core.Instance.HowardSmr.transform.position + Vector3.up * 0.8f;
                Core.Instance.CreateShockwave(howardPos, DamagedPlayer.ParentController, 1f, true);
            }

            // Break killing structure
            //InvolvedStructure?.StructureComponent?.Kill(InvolvedStructure.Velocity, true, true);

            // Create ragdoll
            if (!HowardInvolved || (HowardInvolved && !howardDied))
                Core.Instance.CreateRagdollIfNecessary(DamagedPlayer.ParentController, null, Drama);

            // Flash the screen again
            ScreenFlash.CreateScreenFlash(PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform, LayerMask.NameToLayer("PlayerController"));

            // Shake the camera
            AddHaptics(1f, 1f, 1f);

            // End
            CancelAnimation(false);
        }

        void OnDestroy()
        {
            if (IsAnimationRunning) CancelAnimation();
        }

        public static void AddHaptics(float leftIntensity, float rightIntensity, float screenShake)
        {
            if (Config.DramaticEffectsHaptics.Value is Config.EffectsHaptics_Type.None) return;
            
            if (Config.DramaticEffectsHaptics.Value is Config.EffectsHaptics_Type.AlwaysShake)
            {
                if (ForceHapticsRoutine != null) MelonCoroutines.Stop(ForceHapticsRoutine);
                MelonCoroutines.Start(forceHapticsFor(2f));
            }

            PlayerHaptics.AddHapticsSignal(leftIntensity, rightIntensity, screenShake);

            IEnumerator forceHapticsFor(float seconds)
            {
                PlayerHaptics.comfortSettings.cameraShakeStrengthRange = new(1f, 1f);
                yield return new WaitForSeconds(seconds);
                PlayerHaptics.comfortSettings.cameraShakeStrengthRange = new(0f, 1f);
            }
        }

        public void CancelAnimation(bool strong = true)
        {
            IsAnimationRunning = false;
            Core.LastDamagedPlayer = null;

            if (strong)
            {
                if (Impact.StructureKillRoutine != null) MelonCoroutines.Stop(Impact.StructureKillRoutine);
                Impact.StructureKillRoutine = null;
                foreach (StructureKillStorage structureKillStorage in Core.Instance.StructureKillStorages)
                {
                    structureKillStorage.Kill();
                }
                Core.Instance.StructureKillStorages.Clear();
                StructureStorage.ClearKillDelayedStructures();
            }
            else
            {
                if (Core.Instance?.ActiveShockwave?.ForceField != null)
                    StructureKillRoutine = MelonCoroutines.Start(StructureKillStorage.C_KillStructuresFromShockwave());
            }

            activeRumbleAudio?.Stop();

            ClearPlayerSilhouettes();
            ClearStructureSilhouettes();

            if (PlayerManager.Instance.LocalPlayer?.Controller?.GetCamera() != null)
                PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = true;

            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                if (cameraInfo != null && cameraInfo.ParentComponent != null)
                {
                    cameraInfo.ParentComponent.cullingMask = cameraInfo.CullingMask;
                    cameraInfo.ParentComponent.nearClipPlane = cameraInfo.NearClipPlane;
                    if (cameraInfo.IsRecordingCam) cameraInfo.ParentComponent.GetComponent<RecordingCamera>().enabled = true;

                    var stabilizer = cameraInfo.ParentComponent.GetComponentInParent<Il2CppLiv.Lck.Smoothing.LckStabilizer>();
                    if (stabilizer != null) stabilizer.enabled = true;
                }
            }

            if (HowardInvolved)
            {
                SkinnedMeshRenderer howardSmr = Core.Instance.HowardSmr;
                if (howardSmr != null)
                {
                    howardSmr.material = Core.Instance.HowardMat;
                    howardSmr.gameObject.layer = 0;
                }
            }

            if (SphereBackground != null)
                GameObject.Destroy(SphereBackground);

            //if (strong)
            {
                if (FogEndDistanceStorage != -1)
                RenderSettings.fogEndDistance = FogEndDistanceStorage;
            }

            if (AnimationCoroutine != null) MelonCoroutines.Stop(AnimationCoroutine);

            if (this != null && gameObject != null)
                GameObject.Destroy(gameObject);

            Core.Instance.Impacts.Remove(this);
        }

        public void ClearStructureSilhouettes()
        {
            foreach (GameObject structure in StructureSilhouettes)
                GameObject.Destroy(structure);
            StructureSilhouettes.Clear();
        }

        public void CreateStructureSilhouette(StructureStorage structureInfo)
        {
            GameObject meshObject = structureInfo.StructureGO.GetComponentInChildren<MeshRenderer>().gameObject;
            Quaternion rot = meshObject.transform.rotation;
            GameObject newStructureSilhouette = GameObject.Instantiate(meshObject);

            Vector3 pos = structureInfo.StructureGO.transform.position;
            newStructureSilhouette.transform.SetParent(Core.Instance.ModObject_Silhouettes.transform);
            newStructureSilhouette.transform.position = pos;
            newStructureSilhouette.transform.rotation = rot;

            HelperFunctions.DisableAllComponents(newStructureSilhouette);
            GameObject.Destroy(newStructureSilhouette.GetComponent<Rigidbody>());

            foreach (Renderer r in newStructureSilhouette.GetComponentsInChildren<Renderer>())
            {
                if (Config.PrimaryEffectColor.Value.ToLower() != "none")
                {
                    r.SetMaterial(Core.Instance.PrimarySilhouetteMat);
                    r.materials = new Material[1] { Core.Instance.PrimarySilhouetteMat };
                }
                r.gameObject.layer = Core.Instance.VisualLayer;
            }

            newStructureSilhouette.name = "StructureSilhouette";

            StructureSilhouettes.Add(newStructureSilhouette);
        }

        public static PlayerVisualsClone CreatePlayerSilhouette(PlayerController player)
        {
            if (player == null) return null;

            if (Core.Instance.PlayerSilhouettes.TryGetValue(player, out PlayerVisualsClone ps))
            {
                ps?.ReapplyVisuals();
                return ps;
            }

            GameObject newClone = GameObject.Instantiate(player.PlayerVisuals.gameObject);
            PlayerVisualsClone playerSilhouette = newClone.AddComponent<PlayerVisualsClone>();
            playerSilhouette.ParentController = player;
            playerSilhouette.SetUp();
            newClone.SetActive(false);
            newClone.transform.SetParent(Core.Instance.ModObject_Silhouettes.transform);
            newClone.name = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername) + "Silhouette";

            Core.Instance.PlayerSilhouettes[player] = playerSilhouette;
            return playerSilhouette;
        }
        public void ClearPlayerSilhouettes()
        {
            foreach (PlayerVisualsClone playerSilhouette in InvolvedPlayers)
            {
                if (playerSilhouette != null)
                {
                    if (playerSilhouette.Camera != null)
                        playerSilhouette.Camera.enabled = false;
                    if (playerSilhouette.AudioListener != null)
                        playerSilhouette.AudioListener.enabled = true;
                    if (playerSilhouette.Visuals != null)
                        playerSilhouette.Visuals.SetActive(false);
                }
                else
                {
                    Core.Instance.PlayerSilhouettes.Remove(playerSilhouette.ParentController);
                }
            }
        }

        public void CreateSphereBackground()
        {
            SphereBackground = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SphereBackground.GetComponent<SphereCollider>().enabled = false;
            SphereBackground.GetComponent<Renderer>().material = Core.Instance.SecondarySilhouetteMat;
            SphereBackground.layer = Core.Instance.VisualLayer;
            SphereBackground.transform.SetParent(Core.Instance.ModObject_DramaticEffects.transform);
            SphereBackground.transform.localScale = Vector3.one * 400f;
            SphereBackground.transform.position = PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.position;
        }
    }

    [RegisterTypeInIl2Cpp]
    public class PlayerVisualsClone : MonoBehaviour
    {
        public PlayerController ParentController;
        public GameObject Visuals;
        public Transform LIV;
        public ShiftStone[] ShiftStones = { null, null };
        public Camera Camera = null;
        public AudioListener AudioListener = null;

        public VisualsType Type = VisualsType.Silhouette;
        public enum VisualsType
        {
            Silhouette,
            Ragdoll
        }

        void Update()
        {
            if (Visuals == null) return;

            if (ParentController == null)
            {
                if (Type == VisualsType.Silhouette)
                    Core.Instance.PlayerSilhouettes.Remove(ParentController);

                GameObject.DestroyImmediate(Visuals);
            }
        }

        public void SetUp(GameObject setupObject = null)
        {
            if (setupObject == null) setupObject = gameObject;
            Visuals = setupObject;

            Transform chest = Visuals.transform.GetChild(1).GetChild(0).GetChild(4).GetChild(0);
            ShiftStones[0] = chest?.GetChild(1)?.gameObject?.GetComponentInChildren<ShiftStone>(true);
            ShiftStones[1] = chest?.GetChild(2)?.gameObject?.GetComponentInChildren<ShiftStone>(true);
            if (Type == VisualsType.Silhouette)
            {
                ShiftStones[0]?.transform?.SetParent(Visuals.transform);
                ShiftStones[1]?.transform?.SetParent(Visuals.transform);
            }

            foreach (var m in Visuals.GetComponentsInChildren<Renderer>())
            {
                if (m.name == "FadeScreenRenderer")
                {
                    GameObject.Destroy(m.gameObject);
                }
                else
                {
                    if (Type == VisualsType.Silhouette)
                    {
                        m.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        if (Config.PrimaryEffectColor.Value.ToLower() != "none")
                            m.sharedMaterial = Core.Instance.PrimarySilhouetteMat;
                        m.gameObject.layer = Core.Instance.VisualLayer;

                        int isLocal = ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local ? 1 : 0;
                        m.material.SetInt("_IsLocalPlayer", isLocal);
                    }
                }
            }

            Transform visualsChest = Visuals.transform.GetChild(1).GetChild(0).GetChild(4).GetChild(0);
            for (int i = visualsChest.childCount - 1; i > 0; i--)
            {
                if (visualsChest.GetChild(i).name.Contains("VFX")) visualsChest.GetChild(i).gameObject.SetActive(false);
            }

            foreach (var c in Visuals.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            HelperFunctions.DisableAllComponents(Visuals, new List<Behaviour>{ this, Visuals.GetComponent<RigDefinition>() });
            if (Type == VisualsType.Silhouette && ParentController.controllerType == Il2CppRUMBLE.Players.ControllerType.Local)
            {
                GameObject newCam = new GameObject("Camera");
                newCam.transform.SetParent(Visuals.transform);
                Camera = newCam.AddComponent<Camera>();
                Camera.CopyFrom(PlayerManager.Instance.LocalPlayer.Controller.GetCamera());
                Camera.nearClipPlane = 0.01f;
                Camera.depth = -10;
                Camera.enabled = false;
                Camera.cullingMask = 1 << Core.Instance.VisualLayer;

                AudioListener = Camera.gameObject.AddComponent<AudioListener>();
                AudioListener.enabled = false;
            }
            GameObject.Destroy(Visuals.transform.GetChild(2)?.gameObject);
        }

        public void UpdateShiftStones()
        {
            PlayerShiftstoneSystem parentStoneSystem = ParentController.PlayerShiftstones;
            ShiftStone parentStone0 = parentStoneSystem?.shiftStoneSockets[0]?.assignedShifstone;
            ShiftStone parentStone1 = parentStoneSystem?.shiftStoneSockets[1]?.assignedShifstone;

            ShiftStone oldStone0 = ShiftStones[0];
            ShiftStone oldStone1 = ShiftStones[1];

            if (parentStone0 != null)
            {
                GameObject newStone = GameObject.Instantiate(parentStone0.gameObject);
                ShiftStones[0] = newStone.GetComponent<ShiftStone>();
                newStone.transform.SetParent(Visuals.transform);
                newStone.GetComponentInChildren<MeshRenderer>().gameObject.layer = Core.Instance.VisualLayer;
            }

            if (parentStone1 != null)
            {
                GameObject newStone = GameObject.Instantiate(parentStone1.gameObject);
                ShiftStones[1] = newStone.GetComponent<ShiftStone>();
                newStone.transform.SetParent(Visuals.transform);
                newStone.GetComponentInChildren<MeshRenderer>().gameObject.layer = Core.Instance.VisualLayer;
            }

            if (oldStone0 != null)
                GameObject.DestroyImmediate(oldStone0.gameObject);
            if (oldStone1 != null)
                GameObject.DestroyImmediate(oldStone1.gameObject);
        }

        public void CopyPose()
        {
            if (Visuals == null) return;

            SkinnedMeshRenderer smr = Visuals.GetComponentInChildren<SkinnedMeshRenderer>();
            if (Config.PrimaryEffectColor.Value.ToLower() != "none")
                smr.material = Core.Instance.PrimarySilhouetteMat;
            else if (Ragdoll.PlayerMats.ContainsKey(ParentController.assignedPlayer.Data.GeneralData.PlayFabMasterId))
                ReapplyVisuals();

            if (ShiftStones[0] != null)
            {
                if (Config.PrimaryEffectColor.Value.ToLower() != "none")
                    ShiftStones[0].GetComponentInChildren<MeshRenderer>().material = Core.Instance.PrimarySilhouetteMat;
                ShiftStones[0].transform.position = ParentController.PlayerShiftstones.shiftStoneSockets[0].assignedShifstone.transform.position;
                ShiftStones[0].transform.rotation = ParentController.PlayerShiftstones.shiftStoneSockets[0].assignedShifstone.transform.rotation;
                ShiftStones[0].transform.parent.localScale = ParentController.PlayerShiftstones.shiftStoneSockets[0].assignedShifstone.transform.parent.localScale;
                ShiftStones[0].transform.localScale = ParentController.PlayerShiftstones.shiftStoneSockets[0].assignedShifstone.transform.localScale;
                ShiftStones[0].transform.localScale *= (ParentController.assignedPlayer.Data.PlayerMeasurement.ArmSpan / 1.6f);
            }
            if (ShiftStones[1] != null)
            {
                if (Config.PrimaryEffectColor.Value.ToLower() != "none")
                    ShiftStones[1].GetComponentInChildren<MeshRenderer>().material = Core.Instance.PrimarySilhouetteMat;
                ShiftStones[1].transform.position = ParentController.PlayerShiftstones .shiftStoneSockets[1].assignedShifstone.transform.position;
                ShiftStones[1].transform.rotation = ParentController.PlayerShiftstones.shiftStoneSockets[1].assignedShifstone.transform.rotation;
                ShiftStones[1].transform.parent.localScale = ParentController.PlayerShiftstones.shiftStoneSockets[1].assignedShifstone.transform.parent.localScale;
                ShiftStones[1].transform.localScale = ParentController.PlayerShiftstones.shiftStoneSockets[1].assignedShifstone.transform.localScale;
                ShiftStones[1].transform.localScale *= (ParentController.assignedPlayer.Data.PlayerMeasurement.ArmSpan / 1.5f);
            }

            //bool rockCamBeingUsed = Core.FindRockCamBeingUsed();
            
            int isLocal = ParentController.controllerType == Il2CppRUMBLE.Players.ControllerType.Local ? 1 : 0;
            smr.material.SetFloat("_IsLocal", isLocal);
            smr.material.SetInt("_IsLocalPlayer", isLocal);

            List<Transform> parentBones = ParentController.GetBones()
                .Select(bone => bone.Transform)
                .ToList();
            List<Transform> myBones = Visuals.GetComponent<RigDefinition>().BoneDefinitions
                .Select(bone => bone.Transform)
                .ToList();

            if (Camera != null) Camera.transform.position = ParentController.GetCamera().transform.position;

            
            HelperFunctions.CopyAllTransforms(parentBones, myBones);
        }

        public void ReapplyVisuals()
        {
            if (Visuals == null) return;

            SkinnedMeshRenderer myRenderer = Visuals.GetComponentInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer parentRenderer = ParentController.transform.GetChild(1).GetComponentInChildren<SkinnedMeshRenderer>();
            myRenderer.sharedMesh = parentRenderer.sharedMesh;
            if (!Ragdoll.PlayerMats.ContainsKey(ParentController.assignedPlayer.Data.GeneralData.PlayFabMasterId)) return;
            myRenderer.material = Ragdoll.PlayerMats[ParentController.assignedPlayer.Data.GeneralData.PlayFabMasterId];
        }
    }
}