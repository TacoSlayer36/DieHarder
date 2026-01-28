using Il2CppPhoton.Pun;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Utilities;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Networking;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Impact : MonoBehaviour
    {
        public List<PlayerSilhouette> InvolvedPlayers = new();
        public StructureStorage InvolvedStructure;
        public List<GameObject> StructureSilhouettes = new();
        public GameObject SphereBackground;
        public AudioSource AudioPlayer;

        public PlayerSilhouette DamagedPlayer;
        public Vector3 DamagePos => InvolvedStructure != null ?
                                    (InvolvedStructure.Pos + DamagedPlayer.ParentController.GetChest().position) / 2 :
                                    DamagedPlayer.ParentController.GetChest().position;

        object AnimationCoroutine;
        public bool IsAnimationRunning = false;

        private bool fogEnabledStorage = false;

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

                foreach (PlayerSilhouette playerSilhouette in Core.Instance.PlayerSilhouettes.Values)
                {
                    if (playerSilhouette.Camera != null)
                        playerSilhouette.Camera.transform.rotation = PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.rotation;
                }
            }
        }

        public void RunAnimation()
        {
            if (IsAnimationRunning) CancelAnimation();
            AnimationCoroutine = MelonCoroutines.Start(C_RunAnimation());
        }

        public IEnumerator C_RunAnimation()
        {
            if (IsAnimationRunning) CancelAnimation();

            IsAnimationRunning = true;

            Core.Instance.StoredCameraInfos = Core.GenerateCamInfos();
            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                Camera parentComponent = cameraInfo.ParentComponent;
                parentComponent.cullingMask = 1 << Core.Instance.VisualLayer;
                if (parentComponent.IsFirstPerson()) parentComponent.nearClipPlane = 0.08f;

                if (cameraInfo.IsRecordingCam) cameraInfo.ParentComponent.GetComponent<RecordingCamera>().enabled = false;
            }

            fogEnabledStorage = RenderSettings.fog;
            RenderSettings.fog = false;

            PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = false;

            foreach (PlayerSilhouette playerSilhouette in InvolvedPlayers)
            {
                playerSilhouette.CopyPose();
                playerSilhouette.gameObject.SetActive(true);
                if (playerSilhouette.Camera != null) playerSilhouette.Camera.enabled = true;
            }

            if (ModUISettings.IncludeStructureSilhouette && InvolvedStructure != null)
                CreateStructureSilhouette(InvolvedStructure);

            CreateSphereBackground();
            
            CreateAudio();
            AudioManager.PlaySoundIfFileExists(Core.PreImpactAudioPath);

            yield return new WaitForSeconds(ModUISettings.FreezeFrameDuration / 1000f); // ---- FREEZE ----

            AudioManager.PlaySoundIfFileExists(Core.ImpactAudioPath);


            Core.Instance.CreateShockwave(DamagePos);

            CancelAnimation();
        }

        void OnDestroy()
        {
            if (IsAnimationRunning) CancelAnimation();
        }

        public void CancelAnimation()
        {
            IsAnimationRunning = false;

            foreach (StructureKillStorage structureKillStorage in Core.Instance.StructureKillStorages)
                structureKillStorage.Kill();
            Core.Instance.StructureKillStorages.Clear();

            ClearPlayerSilhouettes();
            ClearStructureSilhouettes();

            PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = true;

            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                cameraInfo.ParentComponent.cullingMask = cameraInfo.CullingMask;
                cameraInfo.ParentComponent.nearClipPlane = cameraInfo.NearClipPlane;
                if (cameraInfo.IsRecordingCam) cameraInfo.ParentComponent.GetComponent<RecordingCamera>().enabled = true;
            }

            GameObject.Destroy(SphereBackground);
            GameObject.Destroy(AudioPlayer.gameObject);

            RenderSettings.fog = fogEnabledStorage;

            if (AnimationCoroutine != null) MelonCoroutines.Stop(AnimationCoroutine);

            GameObject.Destroy(gameObject);
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
            foreach (PlayerSilhouette playerSilhouette in InvolvedPlayers)
            {
                if (playerSilhouette.Camera != null)
                    playerSilhouette.Camera.enabled = false;
                playerSilhouette.gameObject.SetActive(false);
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
        }
    }

    [RegisterTypeInIl2Cpp]
    public class PlayerSilhouette : MonoBehaviour
    {
        public PlayerController ParentController;
        public GameObject Visuals;
        public Transform LIV;
        public Camera Camera = null;

        void Update()
        {
            if (ParentController == null)
            {
                Core.Instance.PlayerSilhouettes.Remove(ParentController);
                GameObject.DestroyImmediate(gameObject);
            }
        }

        public void Setup()
        {
            foreach (var m in gameObject.GetComponentsInChildren<Renderer>())
            {
                if (m.name == "FadeScreenRenderer")
                {
                    GameObject.Destroy(m.gameObject);
                }
                else
                {
                    m.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    m.sharedMaterial = Core.Instance.PrimarySilhouetteMat;
                    if (ParentController.ControllerType == Il2CppRUMBLE.Players.ControllerType.Local) m.material.SetFloat("_IsLocal", 1);
                    m.gameObject.layer = Core.Instance.VisualLayer;
                }
            }

            HelperFunctions.DisableAllComponents(gameObject, new List<Behaviour>{ this, GetComponent<RigDefinition>() });
            if (ParentController.controllerType == Il2CppRUMBLE.Players.ControllerType.Local)
            {
                GameObject newCam = new GameObject("Camera");
                newCam.transform.SetParent(transform);
                Camera = newCam.AddComponent<Camera>();
                Camera.depth = -10;
                Camera.enabled = false;
                Camera.cullingMask = 1 << Core.Instance.VisualLayer;
            }
            GameObject.Destroy(transform.GetChild(2)?.gameObject);
        }

        public void CopyPose()
        {
            List<BoneDefinition> parentBones = ParentController.GetBones();
            List<BoneDefinition> myBones = GetComponent<RigDefinition>().BoneDefinitions.ToList();
            if (Camera != null) Camera.transform.position = ParentController.GetCamera().transform.position;

            for (int i = 0; i < parentBones.Count; i++)
            {
                myBones[i].Transform.position = parentBones[i].Transform.position;
                myBones[i].Transform.rotation = parentBones[i].Transform.rotation;
                myBones[i].Transform.localScale = parentBones[i].Transform.localScale;
            }
        }

        public void ReapplyVisuals()
        {
            SkinnedMeshRenderer myRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            SkinnedMeshRenderer parentRenderer = ParentController.transform.GetChild(1).GetComponentInChildren<SkinnedMeshRenderer>();
            myRenderer.sharedMesh = parentRenderer.sharedMesh;
        }

        public void Remove()
        {
            Core.Instance.PlayerSilhouettes.Remove(ParentController);
            GameObject.DestroyImmediate(gameObject);
        }
    }
}
