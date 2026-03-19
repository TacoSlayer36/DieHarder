using RumbleModdingAPI;
using RumbleModUI;

using MelonLoader;
using Il2CppRUMBLE.Pools;
using System.Collections.Generic;
using UnityEngine;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using System.Linq;
using System.Collections;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Networking.MatchFlow;
using Il2CppRUMBLE.Environment.Howard;
using Il2CppRUMBLE.Recording.LCK;
using System.IO;
using System;
using Il2CppRUMBLE.Audio;
using static Il2CppRUMBLE.Audio.AudioCall;
using AsmResolver.PE.DotNet.ReadyToRun;
using System.Linq.Expressions;

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
        public const string Version = "2.0.7";
        public const string Description = "That death goes hard";
    }

    public partial class Core : MelonMod
    {
        public float AA_Drama = 3f;

        public bool GlobalInit = false;
        public static Core Instance;
        public GameObject ModObject_Parent;
        public GameObject ModObject_Silhouettes;
        public GameObject ModObject_DramaticEffects;
        public GameObject ModObject_Ragdolls;
        public GameObject ModObject_DDOLParent;
        public GameObject ModObject_DDOLRagdoll => ModObject_DDOLParent.transform.GetChild(0).gameObject;

        public static MelonPreferences_Category Prefs_Category;
        public static MelonPreferences_Entry<bool> Prefs_DebugEnabled;
        public static MelonPreferences_Entry<bool> Prefs_LegacyRagdollJank;
        public static MelonPreferences_Entry<bool> Prefs_SmashBrosLaunch;

        public static AudioCall PreImpactLight;
        public static AudioCall PreImpactMedium;
        public static AudioCall PreImpactHard;
        public static AudioCall ImpactLight;
        public static AudioCall ImpactMedium;
        public static AudioCall ImpactHard;
        public static AudioCall Buildup;

        bool warnedAboutImpactAudio = false;
        bool warnedAboutPreImpactAudio = false;
        bool warnedAboutBuildupAudio = false;
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

        public const string UserDataPath = "UserData/" + BuildInfo.Name + "/";
        public const string RagdollAudioPath = "UserData/" + BuildInfo.Name + "/ragdoll_sounds/";
        public AudioClip ImpactAudioClip;
        public AudioClip PreImpactAudioClip;
        public List<AudioClip> RagdollAudioClipsSoft = new();
        public List<AudioClip> RagdollAudioClipsHard = new();

        public Dictionary<PlayerController, PlayerVisualsClone> PlayerSilhouettes = new();
        public List<Impact> Impacts = new();

        private List<PlayerController> playersProcessedThisFrame = new();
        public Dictionary<PlayerController, Tuple<int, int>> PlayerDamages = new();

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

            Prefs_Category = MelonPreferences.CreateCategory("DieHarder");
            Prefs_LegacyRagdollJank = Prefs_Category.CreateEntry<bool>("LegacyRagdollJank", false);
            Prefs_SmashBrosLaunch = Prefs_Category.CreateEntry<bool>("SmashBrosLaunch", false);
            Prefs_DebugEnabled = Prefs_Category.CreateEntry<bool>("debug_mode_enabled", false);

            SilhouetteShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "SolidColorUnlit");
            SilhouetteShader.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            ShockwaveShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "Shockwave");
            ShockwaveShader.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            GhostShader = RumbleModdingAPI.RMAPI.AssetBundles.LoadAssetFromStream<Shader>(this, "DieHarder.assets.dieharder", "Ghost");
            GhostShader.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;

            bool foundPreImpact = false;
            bool foundImpact = false;
            bool foundBuildup = false;

            if (
            createAudioCall(ref PreImpactLight, "pre_impact", "-light") &&
            createAudioCall(ref PreImpactMedium, "pre_impact", "-medium") &&
            createAudioCall(ref PreImpactHard, "pre_impact", "-hard"))
                foundPreImpact = true;

            if (
            createAudioCall(ref ImpactLight, "impact", "-light") &&
            createAudioCall(ref ImpactMedium, "impact", "-medium") &&
            createAudioCall(ref ImpactHard, "impact", "-hard"))
                foundImpact = true;

            if (
            createAudioCall(ref Buildup, "buildup", ""))
                foundBuildup = true;

            bool createAudioCall(ref AudioCall audioCall, string fileName, string suffix)
            {
                string path = UserDataPath + fileName + suffix + ".wav";
                if (File.Exists(path))
                {
                    audioCall = RumbleModdingAPI.RMAPI.AudioManager.CreateAudioCall(path, 1);
                    return true;
                }

                path = UserDataPath + fileName + ".wav";
                if (File.Exists(path))
                {
                    audioCall = RumbleModdingAPI.RMAPI.AudioManager.CreateAudioCall(path, 1);
                    return true;
                }

                return false;
            }

            if (!foundPreImpact && !warnedAboutPreImpactAudio)
            {
                Debug.Log("Could not find audio files for Pre-Impact sound effect", false, 1);
                warnedAboutPreImpactAudio = true;
            }

            if (!foundImpact && !warnedAboutImpactAudio)
            {
                Debug.Log("Could not find audio files for Impact sound effect", false, 1);
                warnedAboutImpactAudio = true;
            }

            if (!foundBuildup && !warnedAboutBuildupAudio)
            {
                Debug.Log("Could not find audio files for Buildup sound effect", false, 1);
                warnedAboutBuildupAudio = true;
            }
        }

        public override void OnUpdate()
        {
            if (!GlobalInit) return;

            if (DebugEnabled)
            {
                if (!Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
                {
                    ActiveImpact = CreateImpact(PlayerManager.Instance.localPlayer.Controller, AA_Drama);
                }

                if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
                {
                    Ragdoll raggy = Ragdoll.SpawnRagdoll(PlayerManager.Instance.localPlayer.Controller, FindClosestStructure(PlayerManager.Instance.LocalPlayer.Controller), AA_Drama);
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
                ActiveImpact = CreateImpact(PlayerManager.Instance.LocalPlayer.Controller, 1f, true);
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
            PlayerDamages.Clear();
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
            PlayerDamages.Clear();
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
            PlayerDamages.Clear();
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
                        AudioClip newClip = RumbleModdingAPI.RMAPI.AudioManager.LoadWavFile(file);
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

            SkinnedMeshRenderer smr = player.PlayerVisuals.GetComponentInChildren<SkinnedMeshRenderer>();
            Material playerMat = new Material(smr.material);
            playerMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
            if (player.controllerType == Il2CppRUMBLE.Players.ControllerType.Local)
            {
                Material newMat = new Material(playerMat);
                Ragdoll.LocalHeadClippedMat = newMat;
                Ragdoll.LocalHeadClippedMat.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
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

        public float CalculateDrama(PlayerController player, StructureStorage closestStructure)
        {
            // BASE VALUES ( (n-1)/3 ):
            /* 1 damage: 0.0
             * 2 damage: 0.3
             * 3 damage: 0.6
             * 4 damage: 1.0
             * 5 damage: 1.3
             * 6 damage: 1.6
             * 7 damage: 2.0
            */

            if (ModUISettings.VariableEffects <= 0) return 1f;

            float damage = GetPlayerDamage(player, closestStructure);

            float overkill = GetPlayerOverkill(player, closestStructure);
            if (ModUISettings.VariableEffects >= 3) overkill *= 1.5f;
            if (ModUISettings.VariableEffects >= 2) damage += overkill;

            // Normalized as if 1 were the minimum damage and 4 were the "normal" amount
            float drama = Mathf.Clamp((damage - 1f) / 3f, 0f, float.MaxValue);

            return drama;
        }

        public void PlayBlendedAudio(Vector3 pos, bool preImpact, float drama = 1f)
        {
            if (drama < 0) return;

            AudioSource audioSource1;
            AudioSource audioSource2;
            AudioSource audioSource3;

            float lightVolume;
            float mediumVolume;
            float hardVolume;
            if (drama <= 1)
            {
                hardVolume = 0;
                mediumVolume = drama;
                lightVolume = 1 - mediumVolume;
            }
            else if (drama <= 2)
            {
                hardVolume = drama - 1;
                mediumVolume = 1 - hardVolume;
                lightVolume = 0;
            }
            else
            {
                hardVolume = 1;
                mediumVolume = 0;
                lightVolume = 0;
            }

            hardVolume *= ModUISettings.DramaticEffectsVolume * 0.8f;
            mediumVolume *= ModUISettings.DramaticEffectsVolume;
            lightVolume *= ModUISettings.DramaticEffectsVolume * 0.8f;

            try
            {
                {
                    GeneralAudioSettings generalSettings = new();
                    generalSettings.Pitch = 1;
                    generalSettings.SetVolume(lightVolume);
                    if (ImpactLight != null) ImpactLight.generalSettings = generalSettings;
                    if (PreImpactLight != null) PreImpactLight.generalSettings = new GeneralAudioSettings(generalSettings.Pointer);
                }
                {
                    GeneralAudioSettings generalSettings = new();
                    generalSettings.Pitch = 1;
                    generalSettings.SetVolume(mediumVolume);
                    if (ImpactMedium != null) ImpactMedium.generalSettings = generalSettings;
                    if (PreImpactMedium != null) PreImpactMedium.generalSettings = generalSettings;
                }
                {
                    GeneralAudioSettings generalSettings = new();
                    generalSettings.Pitch = 1;
                    generalSettings.SetVolume(hardVolume);
                    if (ImpactHard != null) ImpactHard.generalSettings = generalSettings;
                    if (ImpactMedium != null) PreImpactHard.generalSettings = generalSettings;
                }

                if (preImpact)
                {
                    audioSource1 = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(PreImpactLight, pos).AudioSource;
                    audioSource2 = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(PreImpactMedium, pos).AudioSource;
                    audioSource3 = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(PreImpactHard, pos).AudioSource;
                }
                else
                {
                    audioSource1 = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(ImpactLight, pos).AudioSource;
                    audioSource2 = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(ImpactMedium, pos).AudioSource;
                    audioSource3 = RumbleModdingAPI.RMAPI.AudioManager.PlaySound(ImpactHard, pos).AudioSource;
                }
            }
            catch
            {
                return;
            }

            AnimationCurve flatCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

            RemoveAudioFalloff(audioSource1);
            RemoveAudioFalloff(audioSource2);
            RemoveAudioFalloff(audioSource3);
        }

        public static void RemoveAudioFalloff(AudioSource audioSource)
        {
            if (audioSource == null) return;
            AnimationCurve flatCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            audioSource.rolloffMode = AudioRolloffMode.Custom;
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, flatCurve);
        }

        public void OnPlayerDamage(PlayerController player, int newHealth, int previousHealth)
        {
            if (player == null) return;
            int damage = previousHealth - newHealth;

            // Ragdoll on damage
            if ((IsInMatch && ModUISettings.RagdollsInMatches >= 3) || (!IsInMatch && ModUISettings.RagdollsOutsideMatches >= 2))
            {
                if (damage > 0)
                {
                    StructureStorage closestStructure = FindClosestStructure(player);
                    float drama = CalculateDrama(player, closestStructure);

                    if ((IsInMatch && ModUISettings.RagdollsInMatches < 4) || (!IsInMatch && ModUISettings.RagdollsOutsideMatches < 3))
                        damage = 1;
                    for (int i = 0; i < damage; i++)
                    {
                        Ragdoll newRagdoll = Ragdoll.SpawnRagdoll(player, closestStructure, drama);
                        newRagdoll.Hit(closestStructure);
                        if (IsInMatch && ModUISettings.CleanupInMatches >= 2) newRagdoll.ClearAfter(ModUISettings.CleanupInMatches);
                        else if (!IsInMatch && ModUISettings.CleanupOutsideMatches > 0) newRagdoll.ClearAfter(ModUISettings.CleanupOutsideMatches);
                    }
                }
            }

            PlayerDamages[player] = new Tuple<int, int>(damage, previousHealth);
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
            StructureStorage closestStructure = FindClosestStructure(damagedPlayer);
            float drama = CalculateDrama(damagedPlayer, closestStructure);

            if (IsInMatch)
            {
                if (ModUISettings.DramaticEffectsInMatches == 2)
                {
                    ActiveImpact = CreateImpact(damagedPlayer, drama, howardInvolved);
                }
                if (ModUISettings.DramaticEffectsInMatches == 1 && WasMatchEnd)
                {
                    ActiveImpact = CreateImpact(damagedPlayer, drama, howardInvolved);
                }
            }
            else
            {
                if (ModUISettings.DramaticEffectsOutsideMatches == 1)
                {
                    ActiveImpact = CreateImpact(damagedPlayer, drama, howardInvolved);
                }
            }

            if (ActiveImpact == null)
            {
                CreateRagdollIfNecessary(damagedPlayer, closestStructure, drama);
            }
        }

        public void CreateRagdollIfNecessary(PlayerController damagedPlayer, StructureStorage closestStructure = null, float drama = 1f)
        {
            if (closestStructure == null)
                closestStructure = FindClosestStructure(damagedPlayer);

            Ragdoll newRagdoll = null;

            if (IsInMatch)
            {
                if ((ModUISettings.RagdollsInMatches == 1 && WasMatchEnd) || ModUISettings.RagdollsInMatches == 2)
                {
                    newRagdoll = Ragdoll.SpawnRagdoll(damagedPlayer, closestStructure, drama);
                    newRagdoll.Hit(closestStructure);
                    newRagdoll.UndoGhostOnClear = true;
                    if (ModUISettings.CleanupInMatches >= 2)
                        newRagdoll.ClearAfter(ModUISettings.CleanupInMatches);
                    else
                        newRagdoll.GhostifyOwner();
                }
            }
            else
            {
                if (ModUISettings.RagdollsOutsideMatches == 1)
                {
                    newRagdoll = Ragdoll.SpawnRagdoll(damagedPlayer, closestStructure, drama);
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

        public int GetPlayerDamage(PlayerController player, StructureStorage closestStructure)
        {
            int damage = 0;
            int prevHealth = 0;

            if (PlayerDamages.ContainsKey(player))
            {
                damage = PlayerDamages[player].Item1;
                prevHealth = PlayerDamages[player].Item2;
            }

            int supposedDamage = closestStructure == null ? 0 : closestStructure.StructureComponent.GetTotalTier();

            damage = Mathf.Max(damage, supposedDamage);

            return damage;
        }

        public int GetPlayerOverkill(PlayerController player, StructureStorage closestStructure)
        {
            int damage = 0;
            int prevHealth = 0;

            if (PlayerDamages.ContainsKey(player))
            {
                damage = GetPlayerDamage(player, closestStructure);
                prevHealth = PlayerDamages[player].Item2;
            }

            int health = player.assignedPlayer.Data.HealthPoints - damage;
            int overkill = health - (prevHealth - damage);

            return overkill;
        }

        public PlayerVisualsClone FindOrCreateSilhouette(PlayerController player)
        {
            if (PlayerSilhouettes.ContainsKey(player))
                return PlayerSilhouettes[player];

            return CreateSilhouetteFromPlayer(player);
        }

        public List<PlayerVisualsClone> GetInvolvedPlayers(PlayerController damagedPlayer)
        {
            List<PlayerVisualsClone> involvedPlayers = new();
            if (IsInMatch)
            {
                if (PlayerSilhouettes.Count > 0)
                    involvedPlayers.Add(FindOrCreateSilhouette(PlayerManager.Instance.AllPlayers[0].Controller));
                if (PlayerSilhouettes.Count > 1)
                    involvedPlayers.Add(FindOrCreateSilhouette(PlayerManager.Instance.AllPlayers[1].Controller));
            }
            else
            {
                if (PlayerSilhouettes.Count > 0)
                    involvedPlayers.Add(FindOrCreateSilhouette(damagedPlayer));
                if (damagedPlayer.controllerType != Il2CppRUMBLE.Players.ControllerType.Local)
                    involvedPlayers.Add(FindOrCreateSilhouette(PlayerManager.instance.localPlayer.Controller));
            }
            return involvedPlayers;
        }

        public void CreateShockwave(Vector3 pos, PlayerController damagedPlayer, float drama = 1f, bool howardInvolved = false)
        {
            GameObject shockwaveGO = new GameObject("Shockwave");
            shockwaveGO.transform.SetParent(ModObject_DramaticEffects.transform);
            shockwaveGO.transform.position = pos;
            ActiveShockwave = shockwaveGO.AddComponent<Shockwave>();
            ActiveShockwave.DamagedPlayer = damagedPlayer;
            ActiveShockwave.HowardInvolved = howardInvolved;
            ActiveShockwave.Drama = drama;
            ActiveShockwave.SetUp();
        }

        public PlayerVisualsClone CreateSilhouetteFromPlayer(PlayerController player)
        {
            if (player == null) return null;

            if (PlayerSilhouettes.TryGetValue(player, out PlayerVisualsClone ps))
            {
                ps?.ReapplyVisuals();
                return ps;
            }

            GameObject newClone = GameObject.Instantiate(player.PlayerVisuals.gameObject);
            PlayerVisualsClone playerSilhouette = newClone.AddComponent<PlayerVisualsClone>();
            playerSilhouette.ParentController = player;
            playerSilhouette.SetUp();
            newClone.SetActive(false);
            newClone.transform.SetParent(ModObject_Silhouettes.transform);
            newClone.name = HelperFunctions.SanitizeString(player.assignedPlayer.Data.GeneralData.PublicUsername) + "Silhouette";

            PlayerSilhouettes[player] = playerSilhouette;
            return playerSilhouette;
        }

        public Impact CreateImpact(PlayerController damagedPlayer, float drama = 1f, bool howardInvolved = false)
        {
            if (damagedPlayer == null) return null;

            GameObject newImpactGO = new GameObject("Impact");
            newImpactGO.transform.SetParent(ModObject_DramaticEffects.transform);
            Impact newImpact = newImpactGO.AddComponent<Impact>();
            newImpact.HowardInvolved = howardInvolved;
            newImpact.Drama = drama;
            Impacts.Add(newImpact);

            newImpact.InvolvedPlayers = GetInvolvedPlayers(damagedPlayer);
            newImpact.DamagedPlayer = FindOrCreateSilhouette(damagedPlayer);

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

        //public static bool FindRockCamBeingUsed()
        //{
        //    bool rockCamBeingUsed = false;
        //    try
        //    {
        //        PlayerLIV playerLiv = PlayerManager.Instance.LocalPlayer.Controller.PlayerLIV;
        //        Il2CppRUMBLE.Recording.LCK.Extensions.LCKCameraController lckCamera = playerLiv.LckTablet.gameObject.GetComponent<Il2CppRUMBLE.Recording.LCK.Extensions.LCKCameraController>();
        //        LCKTabletUtility lckTabletUtility = playerLiv.LckTablet.gameObject.GetComponent<LCKTabletUtility>();
        //        LCKTabletDetachedPreview lckPreview = playerLiv.LckTablet.gameObject.GetComponent<LCKTabletDetachedPreview>();
        //
        //        // If you're using rock cam (recording or projecting to monitor) and it's not in first person
        //        if (lckCamera.CurrentCameraMode != Il2CppRUMBLE.Recording.LCK.Extensions.CameraMode.FirstPerson && (lckTabletUtility.isRecording || lckPreview.ActivePreviewNo == 5))
        //            rockCamBeingUsed = true;
        //    }
        //    catch { }
        //    return rockCamBeingUsed;
        //}
    }
}