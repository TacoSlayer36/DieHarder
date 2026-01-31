using HarmonyLib;
using Il2CppRUMBLE.CharacterCreation.Interactable;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.MoveSystem;
using Il2CppRUMBLE.Networking.MatchFlow;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Scaling;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Pools;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

namespace DieHarder
{
    [HarmonyPatch(typeof(PlayerVisuals), nameof(PlayerVisuals.ApplyPlayerVisuals), new Type[] { typeof(Il2CppRUMBLE.MeshGeneration.PlayerCharacterBaker.GeneratedPlayerVisuals) })]
    public static class PlayerVisuals_ApplyPlayerVisuals_Patch
    {
        private static void Prefix(ref PlayerVisuals __instance)
        {
            Core.Instance.ProcessNewPlayer(__instance.parentController);
        }

        private static void Postfix(ref PlayerVisuals __instance)
        {
            if (__instance.parentController.controllerType == Il2CppRUMBLE.Players.ControllerType.Local)
            Ragdoll.LocalHeadClippedMat = __instance.parentController.GetSubsystem<PlayerVisuals>().GetComponentInChildren<SkinnedMeshRenderer>().material;
        }
    }

    [HarmonyPatch(typeof(MatchHandler), nameof(MatchHandler.StopMatch), new Type[] { typeof(bool) })]
    public static class MatchHandler_StopMatch_Patch
    {
        private static void Prefix()
        {
            if (Core.Instance.IsInMatch && MatchHandler.Instance?.CurrentMatchPhase == MatchHandler.MatchPhase.MatchStart)
            {
                MelonCoroutines.Start(SetMatchHasEnded());
            }
        }

        static IEnumerator SetMatchHasEnded()
        {
            yield return new WaitForSeconds(1.5f);
            Core.Instance.HasMatchEnded = true;
        }
    }

    [HarmonyPatch(typeof(MatchHandler), nameof(MatchHandler.ExecuteNextRound), new Type[] {  })]
    public static class MatchHandler_ExecuteNextRound_Patch
    {
        private static void Postfix()
        {
            Core.Instance.HasMatchEnded = false;
            Ragdoll.ClearAllRagdolls();
        }
    }

    [HarmonyPatch(typeof(Structure), nameof(Structure.Kill), new Type[] { typeof(Vector3), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Structure_Kill_Patch
    {
        private static bool Prefix(ref Structure __instance, ref Vector3 killVelocity, ref bool playSFX, ref bool playVFX, ref bool networked)
        {
            bool isAnimationRunning = Core.Instance.ActiveImpact != null && Core.Instance.ActiveImpact.IsAnimationRunning;
            if (isAnimationRunning && Core.Instance.IsInMatch && Core.Instance.HasMatchEnded)
            {
                Core.Instance.StructureKillStorages.Add(new StructureKillStorage(__instance, killVelocity, playSFX, playVFX, networked));
                return false;
            }
            else return true;
        }
    }

    [HarmonyPatch(typeof(PooledMonoBehaviour), nameof(PooledMonoBehaviour.ReturnToPool), new Type[] {  })]
    public static class PooledMonoBehaviour_ReturnToPool_Patch
    {
        private static void Prefix(ref Structure __instance)
        {
            if (Shockwave.Dusts == null || Shockwave.Dusts.Count == 0 || __instance?.gameObject == null) return;

            if (Shockwave.Dusts.Contains(__instance.gameObject))
            {
                __instance.gameObject.transform.localScale = Vector3.one;
                Shockwave.Dusts.Remove(__instance.gameObject);
            }
        }
    }

    public static class Extensions
    {
        public static Transform GetChest(this PlayerController player) => player.GetComponentInChildren<RigDefinition>().ChestDefinition.Transform;
        public static Vector3 GetStandingPosition(this PlayerController player) => player.GetSubsystem<PlayerPhysics>().footCollider.transform.position;
        public static Camera GetCamera(this PlayerController player) => player.GetSubsystem<PlayerCamera>().Camera;
        public static List<BoneDefinition> GetBones(this PlayerController player) => player.GetComponentInChildren<RigDefinition>().BoneDefinitions.ToList();
        public static bool IsFirstPerson(this Camera camera) => Vector3.Distance(camera.transform.position, PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.position) <= 0.3f;
    }
}