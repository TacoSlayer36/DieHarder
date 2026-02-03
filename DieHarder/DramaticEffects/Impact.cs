using DieHarder.DramaticEffects;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
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
        public AudioSource AudioPlayer;

        public PlayerVisualsClone DamagedPlayer;
        public Vector3 DamagePos => InvolvedStructure != null ?
                                    (InvolvedStructure.Pos + DamagedPlayer.ParentController.GetChest().position) / 2 :
                                    DamagedPlayer.ParentController.GetChest().position;

        object AnimationCoroutine;
        public bool IsAnimationRunning = false;

        private bool fogEnabledStorage = false;

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
            Core.Instance.StoredCameraInfos = Core.GenerateCamInfos();
            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                Camera parentComponent = cameraInfo.ParentComponent;
                parentComponent.cullingMask = 1 << Core.Instance.VisualLayer;

                if (cameraInfo.IsRecordingCam) parentComponent.nearClipPlane = 0.05f;

                if (cameraInfo.IsRecordingCam) cameraInfo.ParentComponent.GetComponent<RecordingCamera>().enabled = false;
            }

            // Disable fog
            fogEnabledStorage = RenderSettings.fog;
            RenderSettings.fog = false;

            // Move each silhouette into place (and turn on their camera)
            PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = false;
            foreach (PlayerVisualsClone playerSilhouette in InvolvedPlayers)
            {
                playerSilhouette.CopyPose();
                playerSilhouette.Visuals.SetActive(true);
                if (playerSilhouette.Camera != null) playerSilhouette.Camera.enabled = true;
            }

            // Create structure silhouette
            if (ModUISettings.IncludeStructureInImpact && InvolvedStructure != null)
                CreateStructureSilhouette(InvolvedStructure);

            // Create background
            CreateSphereBackground();

            // Play pre-impact sound
            CreateAudio();
            AudioManager.PlaySoundIfFileExists(Core.PreImpactAudioPath);

            // ---- FREEZE ----
            yield return new WaitForSeconds(ModUISettings.ImpactFrameDuration / 1000f);

            // Play impact sound
            AudioManager.PlaySoundIfFileExists(Core.ImpactAudioPath);

            // Create shockwave
            Core.Instance.CreateShockwave(DamagePos, DamagedPlayer.ParentController);

            // Create ragdoll
            Core.Instance.CreateRagdollIfNecessary(DamagedPlayer.ParentController);

            // Flash the screen again
            ScreenFlash.CreateScreenFlash(PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform, LayerMask.NameToLayer("PlayerController"));

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

            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                if (cameraInfo != null && cameraInfo.ParentComponent != null)
                {
                    cameraInfo.ParentComponent.cullingMask = cameraInfo.CullingMask;
                    cameraInfo.ParentComponent.nearClipPlane = cameraInfo.NearClipPlane;
                    if (cameraInfo.IsRecordingCam) cameraInfo.ParentComponent.GetComponent<RecordingCamera>().enabled = true;
                }
            }

            if (SphereBackground != null)
                GameObject.Destroy(SphereBackground);
            if (AudioPlayer != null && AudioPlayer.gameObject != null)
                GameObject.Destroy(AudioPlayer?.gameObject);

            RenderSettings.fog = fogEnabledStorage;

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

        public void CreateAudio()
        {
            GameObject audioPlayerGO = new GameObject("AudioPlayer");
            AudioPlayer = audioPlayerGO.AddComponent<AudioSource>();
            AudioPlayer.spatialBlend = 0f;
            AudioPlayer.transform.SetParent(Core.Instance.ModObject_DramaticEffects.transform);
        }
        public void PlayAudio(AudioClip clip)
        {
            AudioPlayer.clip = clip;
            AudioPlayer.Play();
            AudioPlayer.volume = ModUISettings.DramaticEffectsVolume;
        }
    }

    [RegisterTypeInIl2Cpp]
    public class PlayerVisualsClone : MonoBehaviour
    {
        public PlayerController ParentController;
        public GameObject Visuals;
        public Transform LIV;
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
                else if (Type == VisualsType.Ragdoll)
                    Core.Instance.PlayerRagdolls.Remove(ParentController);

                GameObject.DestroyImmediate(Visuals);
            }
        }

        public void Setup(GameObject setupObject = null)
        {
            if (setupObject == null) setupObject = gameObject;
            Visuals = setupObject;

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
                        if (ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local) m.material.SetFloat("_IsLocal", 1);
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

        public void CopyPose()
        {
            if (Visuals == null) return;

            SkinnedMeshRenderer smr = Visuals.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.material = Core.Instance.PrimarySilhouetteMat;
            if (ParentController.controllerType == Il2CppRUMBLE.Players.ControllerType.Local) smr.material.SetFloat("_IsLocal", 1f);

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

        public void Remove()
        {
            if (Type == VisualsType.Silhouette)
                Core.Instance.PlayerSilhouettes.Remove(ParentController);
            else if (Type == VisualsType.Ragdoll)
                Core.Instance.PlayerRagdolls.Remove(ParentController);

            GameObject.DestroyImmediate(Visuals);
        }
    }
}
