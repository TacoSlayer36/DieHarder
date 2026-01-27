using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Impact : MonoBehaviour
    {
        public List<PlayerSilhouette> InvolvedPlayers = new();
        public GameObject InvolvedStructure;
        public List<GameObject> StructureSilhouettes = new();
        public GameObject SphereBackground;
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
                if (parentComponent.IsFirstPerson()) parentComponent.nearClipPlane = 0.01f;
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

            yield return new WaitForSeconds(1f);

            CancelAnimation();
        }

        void OnDestroy()
        {
            if (IsAnimationRunning) CancelAnimation();
        }

        public void CancelAnimation()
        {
            ClearPlayerSilhouettes();
            ClearStructureSilhouettes();

            PlayerManager.Instance.localPlayer.Controller.GetCamera().enabled = true;

            foreach (CameraInfo cameraInfo in Core.Instance.StoredCameraInfos)
            {
                cameraInfo.ParentComponent.cullingMask = cameraInfo.CullingMask;
                cameraInfo.ParentComponent.nearClipPlane = cameraInfo.NearClipPlane;
            }

            GameObject.Destroy(SphereBackground);

            RenderSettings.fog = fogEnabledStorage;

            if (AnimationCoroutine != null) MelonCoroutines.Stop(AnimationCoroutine);
            IsAnimationRunning = false;

            GameObject.Destroy(gameObject);
        }

        public void ClearStructureSilhouettes()
        {
            foreach (GameObject structure in StructureSilhouettes)
                GameObject.Destroy(structure);
            StructureSilhouettes.Clear();
        }

        public void CreateStructureSilhouette(GameObject structure)
        {
            GameObject newStructureSilhouette = GameObject.Instantiate(structure.GetComponentInChildren<MeshRenderer>().gameObject);

            Vector3 pos = structure.transform.position;
            Quaternion rot = structure.transform.rotation;
            newStructureSilhouette.transform.SetParent(Core.Instance.ModObject_Silhouettes.transform);
            newStructureSilhouette.transform.position = pos;
            newStructureSilhouette.transform.rotation = rot;

            GameObject.Destroy(newStructureSilhouette.GetComponent<BoxCollider>());
            GameObject.Destroy(newStructureSilhouette.GetComponent<MeshCollider>());

            newStructureSilhouette.name = "StructureSilhouette";
            newStructureSilhouette.GetComponent<MeshRenderer>().sharedMaterial = Core.Instance.PrimarySilhouetteMat;
            newStructureSilhouette.layer = Core.Instance.VisualLayer;

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
            SphereBackground.GetComponent<Renderer>().material = Core.Instance.SecondarySilhouetteMat;
            SphereBackground.layer = Core.Instance.VisualLayer;
            SphereBackground.transform.SetParent(Core.Instance.ModObject_ImpactParent.transform);
            SphereBackground.transform.localScale = Vector3.one * 200f;
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
            Camera.transform.position = ParentController.GetCamera().transform.position;

            for (int i = 0; i < parentBones.Count; i++)
            {
                myBones[i].Transform.position = parentBones[i].Transform.position;
                myBones[i].Transform.rotation = parentBones[i].Transform.rotation;
                myBones[i].Transform.localScale = parentBones[i].Transform.localScale;
            }
        }
    }
}
