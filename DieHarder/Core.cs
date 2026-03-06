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
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Networking.MatchFlow;
using Il2CppRUMBLE.Environment.Howard;
using Il2CppRUMBLE.Recording.LCK;
using UnityEngine.Rendering.Universal;
using System.IO;

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
        public const string Version = "2.0.6";
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
        public static MelonPreferences_Category prefs_Category;
        public static MelonPreferences_Entry<bool> Prefs_DebugEnabled;
        public static MelonPreferences_Entry<bool> Prefs_LegacyRagdollJank;
        public static MelonPreferences_Entry<bool> Prefs_SmashBrosLaunch;
        public static AudioSource PreImpactAudioSource;
        public static AudioSource ImpactAudioSource;

        bool warnedAboutImpactFile = false;
        bool warnedAboutPreImpactFile = false;
        bool warnedAboutRagdollAudioDir = false;
        bool warnedAboutRagdollAudio = false;

        public bool DebugEnabled => Prefs_DebugEnabled.Value;

        public enum MatchResult
        {
            Undecided = 0,
            Won = 1,
            Lost = 2,
            Tied = 3
        }

        public string CurrentScene => RumbleModdingAPI.RMAPI.Calls.Scene.GetSceneName();
        public bool IsInMatch => CurrentScene.Contains("Map") && PlayerManager.Instance.AllPlayers.Count >= 2;
        public bool HasRoundEnded = false;
        public bool WasMatchEnd = false;

        public List<Pool<PooledMonoBehaviour>> StructurePools = new();
        public List<StructureKillStorage> StructureKillStorages = new();

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
                    _shockwaveMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
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
                    _primarySilhouetteMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                }
                _primarySilhouetteMat.color = Impact.GetColorFromSetting(ModUISettings.PrimaryEffectColor, true);
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
                    _secondarySilhouetteMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                }
                _secondarySilhouetteMat.color = Impact.GetColorFromSetting(ModUISettings.SecondaryEffectColor, false);
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
                    _ghostMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                }
                return _ghostMat;
            }
        }

        public const string ImpactAudioPath = "UserData/" + BuildInfo.Name + "/impact.wav";
        public const string PreImpactAudioPath = "UserData/" + BuildInfo.Name + "/pre-impact.wav";
        public const string RagdollAudioPath = "UserData/" + BuildInfo.Name + "/ragdoll_sounds/";
        public AudioClip ImpactAudioClip;
        public AudioClip PreImpactAudioClip;
        public List<AudioClip> RagdollAudioClipsSoft = new();
        public List<AudioClip> RagdollAudioClipsHard = new();

        public Dictionary<PlayerController, PlayerVisualsClone> PlayerSilhouettes = new();
        public List<Impact> Impacts = new();

        private List<PlayerController> playersProcessedThisFrame = new();
        public Dictionary<PlayerController, int> PlayerHealths = new();

        public Howard Howard;
        public SkinnedMeshRenderer HowardSmr
        {
            get
            {
                if (Howard != null)
                {
                    Transform t = Howard.transform.GetChild(2);
                    if (t != null)
                        return t.GetComponentInChildren<SkinnedMeshRenderer>();
                }
                return null;
            }
        }
        private bool howardDied;
        public Material HowardMat = null;

        //public ScriptableRendererFeature LIVPlayersInstance;

        public override void OnLateInitializeMelon()
        {
            Instance = this;
            UI.instance.UI_Initialized += OnUIInit;
            RumbleModdingAPI.RMAPI.Actions.onMapInitialized += sceneReady;

            prefs_Category = MelonPreferences.CreateCategory("DieHarder");
            Prefs_LegacyRagdollJank = prefs_Category.CreateEntry<bool>("LegacyRagdollJank", false);
            Prefs_SmashBrosLaunch = prefs_Category.CreateEntry<bool>("SmashBrosLaunch", false);
            Prefs_DebugEnabled = prefs_Category.CreateEntry<bool>("debug_mode_enabled", false);

            SilhouetteShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "SolidColorUnlit");
            SilhouetteShader.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            ShockwaveShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "Shockwave");
            ShockwaveShader.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            GhostShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "Ghost");
            GhostShader.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
        }

        public override void OnUpdate()
        {
            if (DebugEnabled)
            {
                if (!Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
                {
                    OnPlayerHealthDepleted(PlayerManager.Instance.localPlayer.Controller.PlayerHealth);
                }

                if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
                {
                    Ragdoll raggy = Ragdoll.SpawnRagdoll(PlayerManager.Instance.localPlayer.Controller, FindClosestStructure(PlayerManager.Instance.LocalPlayer.Controller));
                    if (ModUISettings.CleanupOutsideMatches > 0)
                    {
                        raggy.UndoGhostOnClear = true;
                        raggy.ClearAfter(ModUISettings.CleanupOutsideMatches);
                    }
                }
            }
        }

        public override void OnFixedUpdate()
        {
            if (!GlobalInit) return;

            if (Time.timeSinceLevelLoad > 10f)
            {
                foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
                {
                    if (pool == null || pool.parentController == null || pool.parentController.gameObject == null)
                    {
                        if (pool != null && pool.Transform != null && pool.Transform.gameObject != null) GameObject.Destroy(pool.Transform.gameObject);
                        Ragdoll.RagdollPools.Remove(pool.parentController);
                    }
                }
            }

            if (Howard != null && Howard.currentHp == 0 && !howardDied)
            {
                howardDied = true;
                ActiveImpact = CreateImpact(PlayerManager.Instance.LocalPlayer.Controller, true);
                //ActiveImpact.InvolvedStructure = FindClosestStructure(HowardSmr.transform.position + Vector3.up * 0.8f);
            }
            if (Howard != null && Howard.currentHp > 0 && howardDied)
            {
                howardDied = false;
            }

            if (Time.timeSinceLevelLoad > 10f)
            {
                foreach (PlayerVisualsClone pvc in PlayerSilhouettes.Values)
                {
                    if (pvc == null || pvc.ParentController == null || pvc.ParentController.gameObject == null)
                    {
                        try
                        {
                            GameObject.Destroy(pvc?.gameObject);
                        }
                        catch { }
                        PlayerSilhouettes.Remove(pvc?.ParentController);
                    }
                }
            }

            StructureStorage.GameStates.Add(StructureStorage.GenerateStructureStorages());
            if (StructureStorage.GameStates.Count > 3) StructureStorage.GameStates.Remove(StructureStorage.GameStates.First());

            playersProcessedThisFrame.Clear();

            // Ragdoll on damage
            if ((IsInMatch && ModUISettings.RagdollsInMatches >= 3) || (!IsInMatch && ModUISettings.RagdollsOutsideMatches >= 2))
            {
                foreach (Player player in PlayerManager.Instance.AllPlayers)
                {
                    if (player == null || player.Controller == null) continue;

                    int storedHealth = 0;
                    if (PlayerHealths.ContainsKey(player.Controller)) storedHealth = PlayerHealths[player.Controller];

                    if (player.Controller.PlayerHealth.IsRegeneratingHealth) continue;

                    int damageAmount = storedHealth - player.Data.HealthPoints;

                    if (damageAmount > 0)
                    {
                        StructureStorage closestStructure = FindClosestStructure(player.Controller);

                        if ((IsInMatch && ModUISettings.RagdollsInMatches < 4) || (!IsInMatch && ModUISettings.RagdollsOutsideMatches < 3))
                            damageAmount = 1;
                        for (int i = 0; i < damageAmount; i++)
                        {
                            Ragdoll newRagdoll = Ragdoll.SpawnRagdoll(player.Controller, closestStructure);
                            newRagdoll.Hit(closestStructure);
                            if (IsInMatch && ModUISettings.CleanupInMatches >= 2) newRagdoll.ClearAfter(ModUISettings.CleanupInMatches);
                            else if (!IsInMatch && ModUISettings.CleanupOutsideMatches > 0) newRagdoll.ClearAfter(ModUISettings.CleanupOutsideMatches);
                        }
                    }
                    PlayerHealths[player.Controller] = player.Data.HealthPoints;
                }
            }
        }

        public IEnumerator C_SlowFixedUpdate()
        {
            while (true)
            {
                if (Time.timeSinceLevelLoad > 3f)
                {
                    bool anyRagdollsEnabled = false;
                    foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
                        if (pool.AnyRagdollsEnabled) anyRagdollsEnabled = true;

                    if (!anyRagdollsEnabled)
                        Ragdoll.UnGhostifyAllGhosts();
                }

                yield return new WaitForSeconds(5f);
            }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            Impact.FogEndDistanceStorage = -1f;
            Ragdoll.PlayerMats.Clear();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Loader") return;


            ModObject_Parent = new GameObject("DieHarder");
            ModObject_Silhouettes = new GameObject("Silhouettes");
            ModObject_Silhouettes.transform.SetParent(ModObject_Parent.transform);
            ModObject_DramaticEffects = new GameObject("DramaticEffects");
            ModObject_DramaticEffects.transform.SetParent(ModObject_Parent.transform);
            ModObject_Ragdolls = new GameObject("Ragdolls");
            ModObject_Ragdolls.transform.SetParent(ModObject_Parent.transform);
            
            ActiveImpact?.CancelAnimation();
            PlayerHealths.Clear();
            PlayerSilhouettes.Clear();

            HasRoundEnded = false;

            Ragdoll.RagdollPools.Clear();
            Ragdoll.LocalHeadClippedMat = null;
            ActiveImpact = null;
            ActiveShockwave = null;

            if (sceneName == "Gym" && !GlobalInit)
            {
                RunGlobalInit();
            }

            PreImpactAudioClip = AudioManager.LoadWavFile(PreImpactAudioPath);
            if (PreImpactAudioClip == null && !warnedAboutPreImpactFile)
            {
                Debug.Log("Did not find pre-impact.mp3", false, 1);
                warnedAboutPreImpactFile = true;
            }
            PreImpactAudioSource.clip = PreImpactAudioClip;

            ImpactAudioClip = AudioManager.LoadWavFile(ImpactAudioPath);
            if (ImpactAudioClip == null && !warnedAboutImpactFile)
            {
                Debug.Log("Did not find impact.mp3", false, 1);
                warnedAboutImpactFile = true;
            }
            ImpactAudioSource.clip = ImpactAudioClip;

            if (Directory.Exists(RagdollAudioPath))
            {
                if (Directory.GetFiles(RagdollAudioPath).Length == 0)
                {
                    if (!warnedAboutRagdollAudio)
                    {
                        Debug.Log("Did not find any ragdoll sounds in ragdoll_sounds folder", false, 1);
                        warnedAboutRagdollAudio = true;
                    }
                }
                else
                {
                    foreach (var file in Directory.GetFiles(RagdollAudioPath))
                    {
                        AudioClip newClip = AudioManager.LoadWavFile(file);
                        newClip.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                        if (file.Contains("soft")) RagdollAudioClipsSoft.Add(newClip);
                        else RagdollAudioClipsHard.Add(newClip);
                    }
                }
            }
            else if (!warnedAboutRagdollAudioDir)
            {
                Debug.Log("Did not find ragdoll_sounds folder", false, 1);
                warnedAboutRagdollAudioDir = true;
            }
        }

        void sceneReady(string _)
        {
            if (CurrentScene == "Gym")
            {
                MelonCoroutines.Start(C_ListenForLandButton("FlatLand"));
                MelonCoroutines.Start(C_ListenForLandButton("VoidLand"));

                MelonCoroutines.Start(C_GrabHowardStuff());
            }
        }

        private IEnumerator C_GrabHowardStuff()
        {
            yield return new WaitForSeconds(3f);

            Howard = RumbleModdingAPI.RMAPI.GameObjects.Gym.INTERACTABLES.Howard.GetGameObject().GetComponentInChildren<Howard>();
            SkinnedMeshRenderer howardSmr = Core.Instance.HowardSmr;
            if (howardSmr != null)
            {
                Core.Instance.HowardMat = howardSmr.material;
                Core.Instance.HowardMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            }
        }

        private IEnumerator C_ListenForLandButton(string landType)
        {
            yield return new WaitForSeconds(3f);
            GameObject.Find(landType)?.
                GetComponentInChildren<InteractionButton>().
                onPressed.
                AddListener(new System.Action(() =>
                {
                    MelonCoroutines.Start(C_OnLandEntered());
                }));
            yield break;
        }

        private IEnumerator C_OnLandEntered()
        {
            yield return new WaitForSeconds(1.5f);
            ModObject_Parent.SetActive(true);
        }

        public void RunGlobalInit()
        {
            //MelonCoroutines.Start(C_SlowFixedUpdate());
            fetchStructurePools();

            //LCKTabletUtility lckTabletUtility = PlayerManager.Instance.LocalPlayer.Controller.PlayerLIV.LckTablet;
            //if (lckTabletUtility != null)
            //{
            //    LIVPlayersInstance = lckTabletUtility.firstPersonCamera._camera.GetUniversalAdditionalCameraData().scriptableRenderer.rendererFeatures[0];
            //}

            ModObject_DDOLParent = GameObject.Instantiate(RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<GameObject>(this, "DieHarder.assets.dieharder", "DieHarderDDOL"));
            ModObject_DDOLParent.name = "DieHarderDDOL";
            GameObject.DontDestroyOnLoad(ModObject_DDOLParent);
            ModObject_DDOLParent.transform.GetChild(0).gameObject.SetActive(false);

            {
                GameObject audioSourceGo = new GameObject("PreImpactAudioPlayer");
                audioSourceGo.transform.SetParent(ModObject_DDOLParent.transform);
                PreImpactAudioSource = audioSourceGo.AddComponent<AudioSource>();
                PreImpactAudioSource.clip = Core.Instance.PreImpactAudioClip;
                PreImpactAudioSource.spatialBlend = 0f;
            }

            {
                GameObject audioSourceGo = new GameObject("ImpactAudioPlayer");
                audioSourceGo.transform.SetParent(ModObject_DDOLParent.transform);
                ImpactAudioSource = audioSourceGo.AddComponent<AudioSource>();
                ImpactAudioSource.spatialBlend = 0f;
            }

            GlobalInit = true;
        }

        public void DetermineIfMatchEnd()
        {
            bool isMatchEnd = false;
            if (MatchHandler.instance == null)
            {
                WasMatchEnd = false;
                return;
            }

            int currentRound = MatchHandler.instance.CurrentRound;
            bool wonThisRound = Core.Instance.GetMatchResult() == Core.MatchResult.Won;
            List<int> roundResults = MatchHandler.instance.RoundsWonList.ToList();

            if (currentRound == 0) isMatchEnd = false;
            else if (currentRound == 1)
            {
                isMatchEnd = roundResults[0] == 1 && wonThisRound;
            }
            else if (currentRound == 2) isMatchEnd = true;

            WasMatchEnd = isMatchEnd;
        }

        public MatchResult GetMatchResult()
        {
            bool localHealthEmpty = true;
            bool otherHealthEmpty = false;
            if (PlayerManager.Instance.LocalPlayer != null)
                localHealthEmpty = PlayerManager.Instance.LocalPlayer.Data.HealthPoints == 0;
            foreach (Player player in PlayerManager.Instance.AllPlayers)
            {
                if (player.Controller.controllerType != Il2CppRUMBLE.Players.ControllerType.Local && player.Data.HealthPoints == 0)
                    otherHealthEmpty = true;
            }

            if (localHealthEmpty && !otherHealthEmpty) return MatchResult.Lost;
            else if (localHealthEmpty && otherHealthEmpty) return MatchResult.Tied;
            else return MatchResult.Won;
        }

        private void fetchStructurePools()
        {
            foreach (var pool in PoolManager.instance.availablePools)
            {
                string poolName = pool.PoolParent.name;
                if (poolName.Contains("RUMBLE.MoveSystem.Structure") && !poolName.Contains("StructureTarget"))
                    StructurePools.Add(pool);
            }
        }

        public void ProcessNewPlayer(PlayerController player)
        {
            if (playersProcessedThisFrame.Contains(player)) return;

            PlayerHealth playerHealth = player.PlayerHealth;
            player.PlayerHealth.OnHealthDepleted.AddListener((UnityAction)(() => OnPlayerHealthDepleted(playerHealth)));

            SkinnedMeshRenderer smr = player.PlayerVisuals.GetComponentInChildren<SkinnedMeshRenderer>();
            Material playerMat = new Material(smr.material);
            playerMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            if (player.controllerType == Il2CppRUMBLE.Players.ControllerType.Local)
            {
                Material newMat = new Material(playerMat);
                newMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
                Ragdoll.LocalHeadClippedMat = new Material(newMat);
            }
            playerMat.SetInt("_IsLocalPlayer", 0);
            Ragdoll.PlayerMats[player] = playerMat;

            try
            {
                CreateSilhouetteFromPlayer(player);
            }
            catch
            {
                Debug.Log("Could not create silhouette for player " + HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername), false, 2);
            }

            playersProcessedThisFrame.Add(player);
        }

        public void OnPlayerHealthDepleted(PlayerHealth playerHealth)
        {
            if (playerHealth.IsRegeneratingHealth) return;
            if (IsInMatch && HasRoundEnded) return;
            if (IsInMatch) HasRoundEnded = true;

            bool howardInvolved = false;
            if (Howard?.playerControllerInRange != null)
                howardInvolved = true;

            DetermineIfMatchEnd();

            PlayerController damagedPlayer = playerHealth.ParentController;

            if (IsInMatch)
            {
                if (ModUISettings.DramaticEffectsInMatches == 2)
                {
                    ActiveImpact = CreateImpact(damagedPlayer, howardInvolved);
                }
                if (ModUISettings.DramaticEffectsInMatches == 1 && WasMatchEnd)
                {
                    ActiveImpact = CreateImpact(damagedPlayer, howardInvolved);
                }
            }
            else
            {
                if (ModUISettings.DramaticEffectsOutsideMatches == 1)
                {
                    ActiveImpact = CreateImpact(damagedPlayer, howardInvolved);
                }
            }

            if (ActiveImpact == null)
            {
                CreateRagdollIfNecessary(damagedPlayer);
            }
        }

        public void CreateRagdollIfNecessary(PlayerController damagedPlayer)
        {
            StructureStorage closestStructure = FindClosestStructure(damagedPlayer);
            Ragdoll newRagdoll = null;

            if (IsInMatch)
            {
                if ((ModUISettings.RagdollsInMatches == 1 && WasMatchEnd) || ModUISettings.RagdollsInMatches == 2)
                {
                    newRagdoll = Ragdoll.SpawnRagdoll(damagedPlayer, closestStructure);
                    newRagdoll.Hit(closestStructure);
                    newRagdoll.UndoGhostOnClear = true;
                    if (ModUISettings.CleanupInMatches >= 2)
                        newRagdoll.ClearAfter(ModUISettings.CleanupInMatches);
                }
            }
            else
            {
                if (ModUISettings.RagdollsOutsideMatches == 1)
                {
                    newRagdoll = Ragdoll.SpawnRagdoll(damagedPlayer, closestStructure);
                    newRagdoll.Hit(closestStructure);
                    if (ModUISettings.CleanupOutsideMatches > 0)
                    {
                        newRagdoll.UndoGhostOnClear = true;
                        newRagdoll.ClearAfter(ModUISettings.CleanupOutsideMatches);
                    }
                }
            }

            if (Prefs_SmashBrosLaunch.Value && newRagdoll != null)
            {
                newRagdoll.DoSmashLaunch = true;
                Vector3 launchLateral = Vector3.zero;
                if (closestStructure != null)
                {
                    launchLateral = (newRagdoll.Chest.position - closestStructure.Pos).normalized;
                }
                else
                {
                    try
                    {
                        PlayerController player1 = GetInvolvedPlayers(damagedPlayer)[0].ParentController;
                        PlayerController player2 = GetInvolvedPlayers(damagedPlayer)[1].ParentController;
                        if (player1.controllerType == Il2CppRUMBLE.Players.ControllerType.Local)
                            launchLateral = (newRagdoll.Chest.position - player2.GetStandingPosition()).normalized;
                        else
                            launchLateral = (newRagdoll.Chest.position - player1.GetStandingPosition()).normalized;
                    }
                    catch { }
                }
                newRagdoll.SmashLaunchDir = new Vector3(launchLateral.x, 1.6f, launchLateral.z);
            }
        }

        public List<PlayerVisualsClone> GetInvolvedPlayers(PlayerController damagedPlayer)
        {
            List<PlayerVisualsClone> involvedPlayers = new();
            if (IsInMatch)
            {
                if (PlayerSilhouettes.Count > 0)
                    involvedPlayers.Add(PlayerSilhouettes[PlayerManager.Instance.AllPlayers[0].Controller]);
                if (PlayerSilhouettes.Count > 1)
                    involvedPlayers.Add(PlayerSilhouettes[PlayerManager.Instance.AllPlayers[1].Controller]);
            }
            else
            {
                if (PlayerSilhouettes.Count > 0)
                    involvedPlayers.Add(PlayerSilhouettes[damagedPlayer]);
                if (damagedPlayer.controllerType != Il2CppRUMBLE.Players.ControllerType.Local)
                    involvedPlayers.Add(PlayerSilhouettes[PlayerManager.instance.localPlayer.Controller]);
            }
            return involvedPlayers;
        }

        public void CreateShockwave(Vector3 pos, PlayerController damagedPlayer, bool howardInvolved = false)
        {
            GameObject shockwaveGO = new GameObject("Shockwave");
            shockwaveGO.transform.SetParent(ModObject_DramaticEffects.transform);
            shockwaveGO.transform.position = pos;
            ActiveShockwave = shockwaveGO.AddComponent<Shockwave>();
            ActiveShockwave.DamagedPlayer = damagedPlayer;
            ActiveShockwave.HowardInvolved = howardInvolved;
        }

        public void CreateSilhouetteFromPlayer(PlayerController player, float waitTime = 0f)
        {
            MelonCoroutines.Start(C_CreateSilhouetteFromPlayer(player, waitTime));
        }

        public IEnumerator C_CreateSilhouetteFromPlayer(PlayerController player, float waitTime)
        {
            yield return new WaitForSeconds(waitTime);

            if (player == null) yield break;

            if (PlayerSilhouettes.TryGetValue(player, out PlayerVisualsClone ps))
            {
                ps?.ReapplyVisuals();
                yield break;
            }

            GameObject newClone = GameObject.Instantiate(player.PlayerVisuals.gameObject);
            PlayerVisualsClone playerSilhouette = newClone.AddComponent<PlayerVisualsClone>();
            playerSilhouette.ParentController = player;
            playerSilhouette.Setup();
            newClone.SetActive(false);
            newClone.transform.SetParent(ModObject_Silhouettes.transform);
            newClone.name = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername) + "Silhouette";

            PlayerSilhouettes[player] = playerSilhouette;
        }

        public Impact CreateImpact(PlayerController damagedPlayer, bool howardInvolved = false)
        {
            if (damagedPlayer == null) return null;

            GameObject newImpactGO = new GameObject("Impact");
            newImpactGO.transform.SetParent(ModObject_DramaticEffects.transform);
            Impact newImpact = newImpactGO.AddComponent<Impact>();
            newImpact.HowardInvolved = howardInvolved;
            Impacts.Add(newImpact);

            newImpact.InvolvedPlayers = GetInvolvedPlayers(damagedPlayer);
            newImpact.DamagedPlayer = PlayerSilhouettes[damagedPlayer];

            if (!howardInvolved)
            {
                StructureStorage closestStructure = FindClosestStructure(damagedPlayer);
                newImpact.InvolvedStructure = closestStructure;
            }

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
            return FindClosestStructure(playerPos);
        }

        public StructureStorage FindClosestStructure(Vector3 pos)
        {
            List<StructureStorage> structuresWithinRange = StructureStorage.GameStates.Last()?.Where(obj => Vector3.Distance(obj.Pos, pos) <= 3f).ToList();
            StructureStorage closestStructure = structuresWithinRange?.OrderBy(obj => Vector3.Distance(obj.Pos, pos))?.FirstOrDefault();
            return closestStructure;
        }

        public static bool FindRockCamBeingUsed()
        {
            bool rockCamBeingUsed = false;
            try
            {
                PlayerLIV playerLiv = PlayerManager.Instance.LocalPlayer.Controller.PlayerLIV;
                Il2CppRUMBLE.Recording.LCK.Extensions.LCKCameraController lckCamera = playerLiv.LckTablet.gameObject.GetComponent<Il2CppRUMBLE.Recording.LCK.Extensions.LCKCameraController>();
                LCKTabletDetachedPreview lckPreview = playerLiv.LckTablet.gameObject.GetComponent<LCKTabletDetachedPreview>();

                // If you're using rock cam (recording or projecting to monitor) and it's not in first person
                if (lckCamera.CurrentCameraMode != Il2CppRUMBLE.Recording.LCK.Extensions.CameraMode.FirstPerson && (PlayerLIV.LCKIsRecording || lckPreview.ActivePreviewNo == 5))
                    rockCamBeingUsed = true;
            }
            catch { }
            return rockCamBeingUsed;
        }
    }
}