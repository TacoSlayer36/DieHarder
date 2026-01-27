using RumbleModdingAPI;
using RumbleModUI;

using MelonLoader;
using Il2CppRUMBLE.Pools;
using System.Collections.Generic;
using UnityEngine;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using UnityEngine.Events;
using System.Linq;
using System.Collections;
using Il2CppRUMBLE.Managers;
using Il2CppPhoton.Compression;

[assembly: MelonInfo(typeof(DieHarder.Core), DieHarder.BuildInfo.Name, DieHarder.BuildInfo.Version, DieHarder.BuildInfo.Author)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonColor(255, 255, 248, 231)]
[assembly: MelonAuthorColor(255, 255, 248, 231)]

namespace DieHarder
{
    public static class BuildInfo
    {
        public const string Name = "DieHarder";
        public const string Author = "TacoSlayer36";
        public const string Version = "2.0.0";
        public const string Description = "That death goes hard";
    }

    public partial class Core : MelonMod
    {
        public bool GlobalInit = false;
        public static Core Instance;
        public GameObject ModObject_Parent;
        public GameObject ModObject_Silhouettes;
        public GameObject ModObject_ImpactParent;
        public string CurrentScene => Calls.Scene.GetSceneName();

        public List<Pool<PooledMonoBehaviour>> StructurePools = new();
        public List<StructureStorage> StructureStorages = new();

        public Impact ActiveImpact;

        private Material _primarySilhouetteMat;
        public Material PrimarySilhouetteMat
        {
            get
            {
                if (_primarySilhouetteMat == null)
                {
                    _primarySilhouetteMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    _primarySilhouetteMat.color = Color.black;
                    _primarySilhouetteMat.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
                }
                return _primarySilhouetteMat;
            }
        }
        private Material _secondarySilhouetteMat;
        public Material SecondarySilhouetteMat
        {
            get
            {
                if (_secondarySilhouetteMat == null)
                {
                    _secondarySilhouetteMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    _secondarySilhouetteMat.SetInt("_Cull", 1);
                    _secondarySilhouetteMat.color = Color.white;
                    _secondarySilhouetteMat.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
                }
                return _secondarySilhouetteMat;
            }
        }

        public Dictionary<PlayerController, PlayerSilhouette> PlayerSilhouettes = new();
        public List<Impact> Impacts = new();

        public override void OnLateInitializeMelon()
        {
            Instance = this;
            Calls.onMapInitialized += sceneReady;
            UI.instance.UI_Initialized += OnUIInit;
        }

        public override void OnUpdate()
        {
            //if (MelonPreferences.GetCategory("Debugging"))
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    OnPlayerHealthDepleted(PlayerManager.Instance.localPlayer.Controller.GetSubsystem<PlayerHealth>());
                }
            }
        }

        public override void OnFixedUpdate()
        {
            if (!GlobalInit) return;

            StructureStorages.Clear();
            StructureStorages = StructureStorage.GenerateStructureStorages();
        }

        private void sceneReady()
        {
            ModObject_Parent = new GameObject("DieHarder");
            ModObject_Silhouettes = new GameObject("Silhouettes");
            ModObject_Silhouettes.transform.SetParent(ModObject_Parent.transform);
            ModObject_ImpactParent = new GameObject("Impacts");
            ModObject_ImpactParent.transform.SetParent(ModObject_Parent.transform);


            if (Calls.Scene.GetSceneName() == "Gym" && !GlobalInit)
            {
                fetchStructurePools();
                GlobalInit = true;
            }
        }

        private void fetchStructurePools()
        {
            foreach (var pool in Il2CppRUMBLE.Managers.PoolManager.instance.availablePools)
            {
                if (pool.poolParent.name.Contains("RUMBLE.MoveSystem.Structure"))
                    StructurePools.Add(pool);
            }
        }

        public void ProcessNewPlayer(PlayerController player)
        {
            PlayerHealth playerHealth = player.GetSubsystem<PlayerHealth>();
            playerHealth.OnHealthDepleted.AddListener((UnityAction)(() => OnPlayerHealthDepleted(playerHealth)));

            CreateSilhouetteFromPlayer(player, 0f);
        }

        public void OnPlayerHealthDepleted(PlayerHealth playerHealth)
        {
            PlayerController parentController = playerHealth.ParentController;

            Impact newImpact = CreateImpact();

            List<PlayerSilhouette> involvedPlayers = new();
            if (CurrentScene.Contains("Map"))
            {
                foreach (var player in PlayerManager.Instance.AllPlayers)
                    involvedPlayers.Add(PlayerSilhouettes[player.Controller]);
            }
            else
            {
                involvedPlayers.Add(PlayerSilhouettes[parentController]);
                if (parentController.controllerType != Il2CppRUMBLE.Players.ControllerType.Local)
                    involvedPlayers.Add(PlayerSilhouettes[PlayerManager.instance.localPlayer.Controller]);
            }
            newImpact.InvolvedPlayers = involvedPlayers;

            Vector3 playerPos = parentController.GetChest().position;
            List<StructureStorage> structuresWithinRange = StructureStorages?.Where(obj => Vector3.Distance(obj.Pos, playerPos) <= 3f).ToList();
            StructureStorage closestStructure = structuresWithinRange?.OrderBy(obj => Vector3.Distance(obj.Pos, playerPos))?.FirstOrDefault();
            newImpact.InvolvedStructure = closestStructure?.StructureGO;

            if (ActiveImpact != null)
            {
                ActiveImpact.CancelAnimation();
                GameObject.Destroy(ActiveImpact);
            }
            ActiveImpact = newImpact;
            newImpact.RunAnimation();
        }

        public void CreateSilhouetteFromPlayer(PlayerController player, float waitTime)
        {
            MelonCoroutines.Start(C_CreateSilhouetteFromPlayer(player, waitTime));
        }

        public IEnumerator C_CreateSilhouetteFromPlayer(PlayerController player, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            GameObject newClone = GameObject.Instantiate(player.GetSubsystem<PlayerVisuals>().gameObject);
            PlayerSilhouette playerSilhouette = newClone.AddComponent<PlayerSilhouette>();
            playerSilhouette.ParentController = player;
            playerSilhouette.Setup();
            newClone.SetActive(false);
            newClone.transform.SetParent(ModObject_Silhouettes.transform);
            newClone.name = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername) + "Silhouette";

            PlayerSilhouettes[player] = playerSilhouette;
        }

        public Impact CreateImpact()
        {
            GameObject newImpactGO = new GameObject("Impact");
            newImpactGO.transform.SetParent(ModObject_ImpactParent.transform);
            Impact newImpact = newImpactGO.AddComponent<Impact>();
            Impacts.Add(newImpact);
            return newImpact;
        }
    }
}