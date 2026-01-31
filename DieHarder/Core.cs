/* -- TODO --
 * NameToLayer li ike
 * Players collide with ragdolls
 * Howard compatibility
 * Replay Mod compatibility
 * Silhouette colors based on win/loss
 * Hit/Hold/Flick/Explode VFX in silhouettes
 * Heads are dangly
 * Shiftstones on ragdolls
 * Structures break from shockwave
 * Dressing room breaks things
*/

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
        public GameObject ModObject_DramaticEffects;
        public GameObject ModObject_Ragdolls;
        public GameObject ModObject_DDOLParent;
        public GameObject ModObject_DDOLRagdoll => ModObject_DDOLParent.transform.GetChild(0).gameObject;
        private MelonPreferences_Category prefs_Category;
        private MelonPreferences_Entry<bool> prefs_DebugEnabled;
        public bool DebugEnabled => prefs_DebugEnabled.Value;

        public string CurrentScene => Calls.Scene.GetSceneName();
        public bool IsInMatch => CurrentScene.Contains("Map") && PlayerManager.Instance.AllPlayers.Count >= 2;

        public List<Pool<PooledMonoBehaviour>> StructurePools = new();
        public List<StructureStorage> StructureStorages = new();
        public List<StructureKillStorage> StructureKillStorages = new();
        public bool HasMatchEnded = false;

        public Impact ActiveImpact;
        public Shockwave ActiveShockwave;

        public Shader ShockwaveShader;
        private Material _shockwaveMat;
        public Material ShockwaveMat
        {
            get
            {
                if (_shockwaveMat == null)
                {
                    _shockwaveMat = new Material(ShockwaveShader);
                    _shockwaveMat.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
                }
                return _shockwaveMat;
            }
        }
        public Shader SilhouetteShader;
        private Material _primarySilhouetteMat;
        public Material PrimarySilhouetteMat
        {
            get
            {
                if (_primarySilhouetteMat == null)
                {
                    _primarySilhouetteMat = new Material(SilhouetteShader);
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
                    _secondarySilhouetteMat = new Material(SilhouetteShader);
                    _secondarySilhouetteMat.color = Color.white;
                    _secondarySilhouetteMat.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
                }
                return _secondarySilhouetteMat;
            }
        }
        public Shader GhostShader;
        private Material _ghostMat;
        public Material GhostMat
        {
            get
            {
                if (_ghostMat == null)
                {
                    _ghostMat = new Material(GhostShader);
                    _ghostMat.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
                }
                return _ghostMat;
            }
        }

        public const string ImpactAudioPath = "UserData/" + BuildInfo.Name + "/impact.mp3";
        public const string PreImpactAudioPath = "UserData/" + BuildInfo.Name + "/pre-impact.mp3";

        public Dictionary<PlayerController, PlayerVisualsClone> PlayerSilhouettes = new();
        public Dictionary<PlayerController, Ragdoll> PlayerRagdolls = new();
        public List<Impact> Impacts = new();

        private List<PlayerController> playersProcessedThisFrame = new();

        public override void OnLateInitializeMelon()
        {
            Instance = this;
            UI.instance.UI_Initialized += OnUIInit;

            prefs_Category = MelonPreferences.CreateCategory("DieHarder");
            prefs_DebugEnabled = prefs_Category.CreateEntry<bool>("DebugModeEnabled", true);

            SilhouetteShader = Calls.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "SolidColorUnlit");
            SilhouetteShader.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
            ShockwaveShader = Calls.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "Shockwave");
            ShockwaveShader.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
            GhostShader = Calls.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "Ghost");
            GhostShader.hideFlags = HideFlags.HideAndDontSave & HideFlags.DontUnloadUnusedAsset;
        }

        public override void OnUpdate()
        {
            if (DebugEnabled)
            {
                if (!Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
                {
                    OnPlayerHealthDepleted(PlayerManager.Instance.localPlayer.Controller.GetSubsystem<PlayerHealth>());
                }

                if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
                {
                    Ragdoll raggy = Ragdoll.SpawnRagdoll(PlayerManager.Instance.localPlayer.Controller, FindClosestStructure(PlayerManager.Instance.LocalPlayer.Controller));
                    if (Input.GetKey(KeyCode.LeftControl))
                    {
                        raggy.GhostifyOwner();
                        raggy.UndoGhostOnClear = true;
                        raggy.ClearAfter(10f);
                    }
                }
            }
        }

        public void GetThisGuysLayerMaskLol(GameObject go)
        {
            MelonLogger.Msg($"Include: {(int)go.GetComponent<Rigidbody>().includeLayers}");
            MelonLogger.Msg($"Exclude: {(int)go.GetComponent<Rigidbody>().excludeLayers}");
        }

        public override void OnFixedUpdate()
        {
            if (!GlobalInit) return;

            StructureStorages.Clear();
            StructureStorages = StructureStorage.GenerateStructureStorages();

            playersProcessedThisFrame.Clear();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ModObject_Parent = new GameObject("DieHarder");
            ModObject_Silhouettes = new GameObject("Silhouettes");
            ModObject_Silhouettes.transform.SetParent(ModObject_Parent.transform);
            ModObject_DramaticEffects = new GameObject("DramaticEffects");
            ModObject_DramaticEffects.transform.SetParent(ModObject_Parent.transform);
            ModObject_Ragdolls = new GameObject("Ragdolls");
            ModObject_Ragdolls.transform.SetParent(ModObject_Parent.transform);

            HasMatchEnded = false;

            Ragdoll.RagdollPools.Clear();
            Ragdoll.LocalHeadClippedMat = null;
            ActiveImpact = null;
            ActiveShockwave = null;
            PlayerRagdolls.Clear();

            if (Calls.Scene.GetSceneName() == "Gym" && !GlobalInit)
            {
                RunGlobalInit();
            }
        }

        public void RunGlobalInit()
        {
            fetchStructurePools();

            ModObject_DDOLParent = GameObject.Instantiate(Calls.LoadAssetFromStream<GameObject>(this, "DieHarder.assets.dieharder", "DieHarderDDOL"));
            ModObject_DDOLParent.name = "DieHarderDDOL";
            GameObject.DontDestroyOnLoad(ModObject_DDOLParent);

            GlobalInit = true;
        }

        private void fetchStructurePools()
        {
            foreach (var pool in PoolManager.instance.availablePools)
            {
                if (pool.poolParent.name.Contains("RUMBLE.MoveSystem.Structure"))
                    StructurePools.Add(pool);
            }
        }

        public void ProcessNewPlayer(PlayerController player)
        {
            if (playersProcessedThisFrame.Contains(player)) return;

            PlayerHealth playerHealth = player.GetSubsystem<PlayerHealth>();
            playerHealth.OnHealthDepleted.AddListener((UnityAction)(() => OnPlayerHealthDepleted(playerHealth)));

            CreateSilhouetteFromPlayer(player);

            playersProcessedThisFrame.Add(player);
        }

        public void OnPlayerHealthDepleted(PlayerHealth playerHealth)
        {
            if (IsInMatch && HasMatchEnded) return;
            HasMatchEnded = true;

            PlayerController damagedPlayer = playerHealth.ParentController;

            if (ModUISettings.DoDramaticEffects)
            {
                ActiveImpact = CreateImpact(damagedPlayer);
            }
            else if (ModUISettings.DoSpawnRagdolls)
            {
                Ragdoll newRagdoll = Ragdoll.SpawnRagdoll(damagedPlayer);
                newRagdoll.Hit(FindClosestStructure(damagedPlayer));
                newRagdoll.UndoGhostOnClear = true;
                newRagdoll.GhostifyOwner();
                if (!IsInMatch)
                {
                    newRagdoll.UndoGhostOnClear = true;
                    newRagdoll.ClearAfter(5f);
                }
            }
        }

        public List<PlayerVisualsClone> GetInvolvedPlayers(PlayerController damagedPlayer)
        {
            List<PlayerVisualsClone> involvedPlayers = new();
            if (IsInMatch)
            {
                involvedPlayers.Add(PlayerSilhouettes[PlayerManager.Instance.AllPlayers[0].Controller]);
                involvedPlayers.Add(PlayerSilhouettes[PlayerManager.Instance.AllPlayers[1].Controller]);
            }
            else
            {
                involvedPlayers.Add(PlayerSilhouettes[damagedPlayer]);
                if (damagedPlayer.controllerType != Il2CppRUMBLE.Players.ControllerType.Local)
                    involvedPlayers.Add(PlayerSilhouettes[PlayerManager.instance.localPlayer.Controller]);
            }
            return involvedPlayers;
        }

        public void CreateShockwave(Vector3 pos, PlayerController damagedPlayer)
        {
            GameObject shockwaveGO = new GameObject("Shockwave");
            shockwaveGO.transform.SetParent(ModObject_DramaticEffects.transform);
            shockwaveGO.transform.position = pos;
            ActiveShockwave = shockwaveGO.AddComponent<Shockwave>();
            ActiveShockwave.DamagedPlayer = damagedPlayer;
        }

        public void CreateSilhouetteFromPlayer(PlayerController player, float waitTime = 0f)
        {
            MelonCoroutines.Start(C_CreateSilhouetteFromPlayer(player, waitTime));
        }

        public IEnumerator C_CreateSilhouetteFromPlayer(PlayerController player, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            if (PlayerSilhouettes.TryGetValue(player, out PlayerVisualsClone ps))
            {
                ps.ReapplyVisuals();
                yield break;
            }

            GameObject newClone = GameObject.Instantiate(player.GetSubsystem<PlayerVisuals>().gameObject);
            PlayerVisualsClone playerSilhouette = newClone.AddComponent<PlayerVisualsClone>();
            playerSilhouette.ParentController = player;
            playerSilhouette.Setup();
            newClone.SetActive(false);
            newClone.transform.SetParent(ModObject_Silhouettes.transform);
            newClone.name = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername) + "Silhouette";

            PlayerSilhouettes[player] = playerSilhouette;
        }

        public Impact CreateImpact(PlayerController damagedPlayer)
        {
            GameObject newImpactGO = new GameObject("Impact");
            newImpactGO.transform.SetParent(ModObject_DramaticEffects.transform);
            Impact newImpact = newImpactGO.AddComponent<Impact>();
            Impacts.Add(newImpact);

            newImpact.InvolvedPlayers = GetInvolvedPlayers(damagedPlayer);
            newImpact.DamagedPlayer = PlayerSilhouettes[damagedPlayer];

            StructureStorage closestStructure = FindClosestStructure(damagedPlayer);
            newImpact.InvolvedStructure = closestStructure;

            if (ActiveImpact != null)
            {
                ActiveImpact.CancelAnimation();
                GameObject.Destroy(ActiveImpact);
            }
            ActiveImpact = newImpact;
            newImpact.RunAnimation();

            return newImpact;
        }

        public StructureStorage FindClosestStructure(PlayerController damagedPlayer)
        {
            Vector3 playerPos = damagedPlayer.GetChest().position;
            List<StructureStorage> structuresWithinRange = StructureStorages?.Where(obj => Vector3.Distance(obj.Pos, playerPos) <= 3f).ToList();
            StructureStorage closestStructure = structuresWithinRange?.OrderBy(obj => Vector3.Distance(obj.Pos, playerPos))?.FirstOrDefault();
            return closestStructure;
        }
    }
}