using Il2CppLIV.SDK.Unity;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Recording.LCK;
using Il2CppRUMBLE.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace DieHarder
{
    public partial class Core
    {
        public List<CameraInfo> StoredCameraInfos = new();

        public LayerMask VisualLayer = LayerMask.NameToLayer("Clouds");
        public LayerMask VisualLayerMask => 1 << VisualLayer;
        public int PhysicsLayer = 3;
        public LayerMask PhysicsLayerMask => 1 << PhysicsLayer;

        public static List<CameraInfo> GenerateCamInfos()
        {
            List<CameraInfo> cameraInfos = new();

            cameraInfos.Add(new CameraInfo(PlayerManager.Instance.LocalPlayer.Controller.GetCamera()){ IsVRCam = true });
            cameraInfos.Add(new CameraInfo(RecordingCamera.Instance.LegacyCamera) { IsRecordingCam = true });
            Camera livCam = LivCaptureService.Service?.render?.cameraInstance;
            if (livCam != null) cameraInfos.Add(new CameraInfo(livCam));
            LCKTabletUtility lckTabletUtility = PlayerManager.Instance.LocalPlayer.Controller.GetSubsystem<PlayerLIV>().LckTablet;
            if (lckTabletUtility != null)
            {
                cameraInfos.Add(new CameraInfo(lckTabletUtility.firstPersonCamera._camera));
                cameraInfos.Add(new CameraInfo(lckTabletUtility.selfieCamera._camera));
                cameraInfos.Add(new CameraInfo(lckTabletUtility.thirdPersonCamera._camera));
            }

            return cameraInfos;
        }
    }

    public class CameraInfo
    {
        public int CullingMask;
        public Camera ParentComponent;

        public Vector3 FreezePos;
        public Quaternion FreezeRot;

        public float NearClipPlane;

        public bool IsFirstPerson => ParentComponent.IsFirstPerson();
        public bool IsVRCam = false;
        public bool IsRecordingCam = false;

        public CameraInfo(Camera camera)
        {
            CullingMask = camera.cullingMask;
            ParentComponent = camera;
            FreezePos = camera.transform.position;
            FreezeRot = camera.transform.rotation;
            NearClipPlane = camera.nearClipPlane;
        }
    }
}
