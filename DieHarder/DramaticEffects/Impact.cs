using DieHarder.DramaticEffects;
using Il2CppLiv.Lck.Smoothing;
using Il2CppLiv.Lck.Tablet;
using Il2CppPlayFab.ClientModels;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Integrations.LIV;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Comfort;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Recording.LCK;
using Il2CppRUMBLE.Recording.LCK.Extensions;
using Il2CppRUMBLE.Settings;
using Il2CppRUMBLE.Utilities;
using Liv.Lck.Smoothing;
using MelonLoader;
using RumbleModdingAPI;
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

        public bool LivPlayersActive = true;

        public bool HowardInvolved = false;

        public Vector3 DamagePos => InvolvedStructure != null ?
                                    (InvolvedStructure.Pos + DamagedPlayer.ParentController.GetChest().position) / 2 :
                                    DamagedPlayer.ParentController.GetChest().position;

        object AnimationCoroutine;
        public bool IsAnimationRunning = false;

        public static float FogEndDistanceStorage = -1f;

        public static Color GetColorFromSetting(string colorString, bool isPrimary)
        {
            if (colorString == "match")
            {
                Core.MatchResult matchResult = Core.Instance.GetMatchResult();
                Color color;

                switch (matchResult)
                {
                    case Core.MatchResult.Won: color = Color.green; break;
                    case Core.MatchResult.Lost: color = Color.red; break;
                    default: color = Color.yellow; break;
                }

                if (!isPrimary && ModUISettings.PrimaryEffectColor == "match" && ModUISettings.SecondaryEffectColor == "match")
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
            FreezeCameras();
        }

        public void FreezeCameras()
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
                        playerSilhouette.Camera.transform.rotation = PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.rotation;
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
            LivPlayersActive = Core.Instance.LIVPlayersInstance.m_Active;
            Core.Instance.LIVPlayersInstance.m_Active = false;
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
            }

            // Create Howard silhouette if necessary
            if (HowardInvolved)
            {
                SkinnedMeshRenderer howardSmr = Core.Instance.HowardSmr;
                howardSmr.material = Core.Instance.PrimarySilhouetteMat;
                howardSmr.gameObject.layer = Core.Instance.VisualLayer;
            }

            // Create structure silhouette
            if (ModUISettings.IncludeStructureInImpact && InvolvedStructure != null)
                CreateStructureSilhouette(InvolvedStructure);

            // Create background
            CreateSphereBackground();

            // Play pre-impact sound
            Core.PreImpactAudioSource.volume = ModUISettings.DramaticEffectsVolume;
            Core.PreImpactAudioSource.Play();
            MelonCoroutines.Start(AudioManager.SilenceAudioAfter(Core.PreImpactAudioSource, (ModUISettings.ImpactFrameDuration - 170) / 1000f));

            // ---- FREEZE ----
            yield return new WaitForSeconds(ModUISettings.ImpactFrameDuration / 1000f);

            // Create shockwave
            bool howardDied = false;
            if (HowardInvolved && Core.Instance.Howard != null && Core.Instance.Howard.currentHp == 0) howardDied = true;

            if (!HowardInvolved || (HowardInvolved && !howardDied))
                Core.Instance.CreateShockwave(DamagePos, DamagedPlayer.ParentController);
            else
                Core.Instance.CreateShockwave(Core.Instance.HowardSmr.transform.position + Vector3.up * 0.8f, DamagedPlayer.ParentController);

            // Create ragdoll
            if (!HowardInvolved || (HowardInvolved && !howardDied))
                Core.Instance.CreateRagdollIfNecessary(DamagedPlayer.ParentController);

            // Flash the screen again
            ScreenFlash.CreateScreenFlash(PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform, LayerMask.NameToLayer("PlayerController"));

            // Shake the camera
            PlayerHaptics ph = PlayerManager.instance.localPlayer.Controller.GetSubsystem<PlayerHaptics>();
            if (ModUISettings.DramaticEffectsScreenShake == 2)
            {
                ph.AddHapticsSignal(10f, 10f, 10f);
            }
            else if (ModUISettings.DramaticEffectsScreenShake == 1)
            {
                ph.AddHapticsSignal(ph.comfortSettings.ControllerShakeStrength, ph.comfortSettings.ControllerShakeStrength, ph.comfortSettings.CameraShakeStrength);
            }

            // End
            CancelAnimation(false);
        }

        void OnDestroy()
        {
            if (IsAnimationRunning) CancelAnimation();
        }

        public void CancelAnimation(bool strong = true)
        {
            IsAnimationRunning = false;

            if (strong)
            {
                foreach (StructureKillStorage structureKillStorage in Core.Instance.StructureKillStorages)
                {
                    structureKillStorage.Kill();
                    Core.Instance.StructureKillStorages.Clear();
                }
            }
            else
            {
                MelonCoroutines.Start(StructureKillStorage.C_KillStructuresFromShockwave());
            }

            ClearPlayerSilhouettes();
            ClearStructureSilhouettes();

            if (PlayerManager.Instance.LocalPlayer?.Controller?.GetCamera() != null)
                PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = true;

            Core.Instance.LIVPlayersInstance.m_Active = LivPlayersActive;

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
                howardSmr.material = Core.Instance.HowardMat;
                howardSmr.gameObject.layer = 0;
            }

            if (SphereBackground != null)
                GameObject.Destroy(SphereBackground);

            if (strong)
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

            newStructureSilhouette.name = "StructureSilhouette";
            newStructureSilhouette.GetComponent<MeshRenderer>().sharedMaterial = Core.Instance.PrimarySilhouetteMat;
            newStructureSilhouette.layer = Core.Instance.VisualLayer;

            if (structureInfo.Type == StructureStorage.StructureType.BoulderBall)
            {
                newStructureSilhouette.transform.GetChild(0).GetComponent<MeshRenderer>().sharedMaterial = Core.Instance.PrimarySilhouetteMat;
                newStructureSilhouette.transform.GetChild(0).gameObject.layer = Core.Instance.VisualLayer;
            }

            StructureSilhouettes.Add(newStructureSilhouette);
        }
        public void ClearPlayerSilhouettes()
        {
            foreach (PlayerVisualsClone playerSilhouette in InvolvedPlayers)
            {
                if (playerSilhouette.Camera != null)
                    playerSilhouette.Camera.enabled = false;
                if (playerSilhouette != null)
                {
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
            SphereBackground.transform.localScale = Vector3.one * 200f;
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

        public void Setup(GameObject setupObject = null)
        {
            if (setupObject == null) setupObject = gameObject;
            Visuals = setupObject;

            Transform chest = Visuals.transform.GetChild(1).GetChild(0).GetChild(4).GetChild(0);
            ShiftStones[0] = chest?.GetChild(1)?.gameObject?.GetComponentInChildren<ShiftStone>();
            ShiftStones[1] = chest?.GetChild(2)?.gameObject?.GetComponentInChildren<ShiftStone>();
            if (Type == VisualsType.Silhouette)
            {
                ShiftStones[0]?.transform?.SetParent(Visuals.transform);
                ShiftStones[1]?.transform?.SetParent(Visuals.transform);
            }

            foreach (Rigidbody rb in Visuals.GetComponentsInChildren<Rigidbody>())
                GameObject.Destroy(rb);

            foreach (Joint joint in Visuals.GetComponentsInChildren<Joint>())
                GameObject.Destroy(joint);

            foreach (Collider c in Visuals.GetComponentsInChildren<Collider>())
                GameObject.Destroy(c);

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
                        m.sharedMaterial = Core.Instance.PrimarySilhouetteMat;
                        m.gameObject.layer = Core.Instance.VisualLayer;

                        float isLocal = ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local ? 1f : 0f;
                        m.sharedMaterial.SetFloat("_IsLocal", isLocal);
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
            }
            GameObject.Destroy(Visuals.transform.GetChild(2)?.gameObject);
        }

        public void UpdateShiftStones()
        {
            PlayerShiftstoneSystem parentStoneSystem = ParentController.GetSubsystem<PlayerShiftstoneSystem>();
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
            smr.material = Core.Instance.PrimarySilhouetteMat;

            if (ShiftStones[0] != null)
            {
                ShiftStones[0].GetComponentInChildren<MeshRenderer>().material = Core.Instance.PrimarySilhouetteMat;
                ShiftStones[0].transform.position = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[0].assignedShifstone.transform.position;
                ShiftStones[0].transform.rotation = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[0].assignedShifstone.transform.rotation;
                ShiftStones[0].transform.parent.localScale = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[0].assignedShifstone.transform.parent.localScale;
                ShiftStones[0].transform.localScale = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[0].assignedShifstone.transform.localScale;
                ShiftStones[0].transform.localScale *= (ParentController.assignedPlayer.Data.PlayerMeasurement.ArmSpan / 1.6f);
            }
            if (ShiftStones[1] != null)
            {
                ShiftStones[1].GetComponentInChildren<MeshRenderer>().material = Core.Instance.PrimarySilhouetteMat;
                ShiftStones[1].transform.position = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[1].assignedShifstone.transform.position;
                ShiftStones[1].transform.rotation = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[1].assignedShifstone.transform.rotation;
                ShiftStones[1].transform.parent.localScale = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[1].assignedShifstone.transform.parent.localScale;
                ShiftStones[1].transform.localScale = ParentController.GetSubsystem<PlayerShiftstoneSystem>().shiftStoneSockets[1].assignedShifstone.transform.localScale;
                ShiftStones[1].transform.localScale *= (ParentController.assignedPlayer.Data.PlayerMeasurement.ArmSpan / 1.5f);
            }

            bool rockCamBeingUsed = false;
            try
            {
                PlayerLIV playerLiv = PlayerManager.Instance.LocalPlayer.Controller.GetSubsystem<PlayerLIV>();
                Il2CppRUMBLE.Recording.LCK.Extensions.LCKCameraController lckCamera = playerLiv.LckTablet.gameObject.GetComponent<Il2CppRUMBLE.Recording.LCK.Extensions.LCKCameraController>();
                LCKTabletDetachedPreview lckPreview = playerLiv.LckTablet.gameObject.GetComponent<LCKTabletDetachedPreview>();

                // If you're using rock cam (recording or projecting to monitor) and it's not in first person
                if (lckCamera.CurrentCameraMode != Il2CppRUMBLE.Recording.LCK.Extensions.CameraMode.FirstPerson && (PlayerLIV.LCKIsRecording || lckPreview.ActivePreviewNo == 5))
                    rockCamBeingUsed = true;
            }
            catch { }

            float isLocal = ParentController.controllerType == Il2CppRUMBLE.Players.ControllerType.Local && !rockCamBeingUsed ? 1f : 0f;
            foreach (Renderer renderer in Visuals.GetComponentsInChildren<Renderer>())
            {
                renderer.material.SetFloat("_IsLocal", isLocal);
            }

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
            myRenderer.material = ParentController.GetSubsystem<PlayerVisuals>().NonHeadClippedMaterial;
        }
    }
}
