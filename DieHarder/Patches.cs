using HarmonyLib;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

namespace DieHarder
{
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.Initialize))]
    public static class PlayerController_Initialize_Patch
    {
        private static void Postfix(ref Il2CppRUMBLE.Players.Player player)
        {
            Core.Instance.ProcessNewPlayer(player.Controller);
        }
    }

    public static class Extensions
    {
        public static Transform GetChest(this PlayerController player) => player.GetComponentInChildren<RigDefinition>().ChestDefinition.Transform;
        public static Camera GetCamera(this PlayerController player) => player.GetSubsystem<PlayerCamera>().Camera;
        public static List<BoneDefinition> GetBones(this PlayerController player) => player.GetComponentInChildren<RigDefinition>().BoneDefinitions.ToList();
        public static bool IsFirstPerson(this Camera camera) => Vector3.Distance(camera.transform.position, PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.position) <= 0.3f;
    }
}