using HarmonyLib;
using Il2CppPlayFab.ClientModels;
using Il2CppRUMBLE.CharacterCreation.Interactable;
using Il2CppRUMBLE.Combat.ShiftStones;
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
using UnityEngine.VFX;
using static MelonLoader.MelonLogger;

namespace DieHarder
{
    [HarmonyPatch(typeof(PlayerVisuals), nameof(PlayerVisuals.ApplyPlayerVisuals), new Type[] { typeof(Il2CppRUMBLE.MeshGeneration.PlayerCharacterBaker.GeneratedPlayerVisuals) })]
    public static class PlayerVisuals_ApplyPlayerVisuals_Patch
    {
        private static void Postfix(ref PlayerVisuals __instance)
        {
            MelonCoroutines.Start(_(__instance.parentController));

            static IEnumerator _(PlayerController player)
            {
                yield return new WaitForSeconds(3f);
                Core.Instance.ProcessNewPlayer(player);
            }
        }
    }

    [HarmonyPatch(typeof(DressingRoom), nameof(DressingRoom.DelayedApplyData), new Type[] { typeof(Il2CppRUMBLE.MeshGeneration.PlayerCharacterBaker.GeneratedPlayerVisuals) })]
    public static class dressingRoomPatch
    {
        private static void Postfix()
        {
            Ragdoll.ReapplyVisualsFor(PlayerManager.Instance.LocalPlayer.Controller);
        }
    }

    // Actually runs when a ROUND ends
    [HarmonyPatch(typeof(MatchHandler), nameof(MatchHandler.StopMatch), new Type[] { typeof(bool) })]
    public static class MatchHandler_StopMatch_Patch
    {
        private static void Prefix()
        {
            if (Core.Instance.IsInMatch && MatchHandler.Instance?.CurrentMatchPhase == MatchHandler.MatchPhase.MatchStart)
            {
                MelonCoroutines.Start(SetRoundHasEnded());
            }
        }

        static IEnumerator SetRoundHasEnded()
        {
            yield return new WaitForSeconds(1.5f);
            Core.Instance.HasRoundEnded = true;
            Core.Instance.PlayerHealths.Clear();
        }
    }

    [HarmonyPatch(typeof(MatchHandler), nameof(MatchHandler.ExecuteNextRound), new Type[] {  })]
    public static class MatchHandler_ExecuteNextRound_Patch
    {
        private static void Postfix()
        {
            Core.Instance.HasRoundEnded = false;
            if (ModUISettings.CleanupInMatches > 0)
                Ragdoll.ClearAllRagdolls();
            foreach (Player player in PlayerManager.Instance.AllPlayers)
            {
                Ragdoll.UnGhostify(player.Controller);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerScaling), nameof(PlayerScaling.ScaleController), new Type[] { typeof(PlayerMeasurement) })]
    public static class PlayerScaling_ScaleController_Patch
    {
        private static void Postfix(ref PlayerScaling __instance)
        {
            if (__instance?.parentController == null) return;

            if (Ragdoll.RagdollPools.ContainsKey(__instance.parentController))
            {
                Ragdoll.RagdollPool pool = Ragdoll.RagdollPools[__instance.parentController];
                GameObject poolObject = pool.Transform.gameObject;
                poolObject.name += " (old calibration)";
                Ragdoll.RagdollPools.Remove(__instance.parentController);
            }
        }
    }

    [HarmonyPatch(typeof(Structure), nameof(Structure.Kill), new Type[] { typeof(Vector3), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Structure_Kill_Patch
    {
        private static bool Prefix(ref Structure __instance, ref Vector3 killVelocity, ref bool playSFX, ref bool playVFX, ref bool networked)
        {
            try
            {
                bool isAnimationRunning = Core.Instance.ActiveImpact != null && Core.Instance.ActiveImpact.IsAnimationRunning;
                if (isAnimationRunning && Core.Instance.IsInMatch && Core.Instance.HasRoundEnded)
                {
                    Core.Instance.StructureKillStorages.Add(new StructureKillStorage(__instance, killVelocity, playSFX, playVFX, networked));
                    Rigidbody rb = __instance.GetComponentInChildren<Rigidbody>();
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    foreach (Collider c in __instance.GetComponentsInChildren<Collider>())
                        c.enabled = false;
                    return false;
                }
                return true;
            }
            catch
            {
                return true;
            }
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

    [HarmonyPatch(typeof(Pool<PooledMonoBehaviour>), nameof(Pool<PooledMonoBehaviour>.FetchFromPool), new Type[] { typeof(Vector3), typeof(Quaternion) })]
    public static class PooledMonoBehaviour_OnFetchFromPool_Patch
    {
        private static void Postfix(ref PooledMonoBehaviour __result, ref Vector3 position)
        {
            if (__result.name == "ExplodeFinale_VFX")
            {
                if (StructureStorage.GameStates.Count >= 3)
                {
                    List<StructureStorage> gameState = StructureStorage.GameStates[StructureStorage.GameStates.Count - 2];
                    StructureStorage explodedStructure = StructureStorage.FindStructureStorageAt(position, gameState);
                    Ragdoll.Explode(explodedStructure);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShiftstoneSystem), nameof(PlayerShiftstoneSystem.AttachShiftStone), new Type[] { typeof(ShiftStone), typeof(int), typeof(bool), typeof(bool) })]
    public static class PlayerShifstoneSystem_AttachShiftStone_Patch
    {
        private static void Postfix(ref PlayerShiftstoneSystem __instance)
        {
            if (Core.Instance.PlayerSilhouettes.TryGetValue(__instance.parentController, out PlayerVisualsClone playerVisualsClone))
            {
                playerVisualsClone.UpdateShiftStones();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShiftstoneSystem), nameof(PlayerShiftstoneSystem.RemoveShiftStone), new Type[] { typeof(int), typeof(bool), typeof(bool) })]
    public static class PlayerShifstoneSystem_RemoveShiftStone_Patch
    {
        private static void Postfix(ref PlayerShiftstoneSystem __instance)
        {
            if (Core.Instance.PlayerSilhouettes.TryGetValue(__instance.parentController, out PlayerVisualsClone playerVisualsClone))
            {
                playerVisualsClone.UpdateShiftStones();
            }
        }
    }

    //[HarmonyPatch(typeof(LIVRenderPlayerFeature), MethodType.Constructor, new Type[] { typeof(IntPtr) })]
    //public static class LIVRenderPlayerFeature_Constructor_Patch
    //{
    //    private static void Postfix(ref LIVRenderPlayerFeature __instance)
    //    {
    //        Core.Instance.LIVPlayersInstance = __instance;
    //    }
    //}

    public static class Extensions
    {
        public static Transform GetChest(this PlayerController player) => player.GetComponentInChildren<RigDefinition>().ChestDefinition.Transform;
        public static Vector3 GetStandingPosition(this PlayerController player) => player.PlayerPhysics.footCollider.transform.position;
        public static Camera GetCamera(this PlayerController player) => player?.PlayerCamera?.Camera;
        public static List<BoneDefinition> GetBones(this PlayerController player) => player.GetComponentInChildren<RigDefinition>().BoneDefinitions.ToList();
        public static bool IsFirstPerson(this Camera camera) => PlayerManager.Instance?.LocalPlayer?.Controller?.GetCamera()?.transform == null ? true : Vector3.Distance(camera.transform.position, PlayerManager.Instance.LocalPlayer.Controller.GetCamera().transform.position) <= 0.3f;
    }
}