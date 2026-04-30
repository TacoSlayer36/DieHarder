using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using MelonLoader;
using System.ComponentModel;
using UIFramework;
using System.ComponentModel.DataAnnotations;

namespace DieHarder
{
    public static class Config
    {
        public const string PreferencesFilePath = Core.UserDataPath + "Config.cfg";

        public static MelonPreferences_Category Cat_DramaticEffects;
        public enum EffectsInMatches_Type
          { [Display(Name = "Disabled")]
            Disabled = 0,
            [Display(Name = "On Match End")]
            OnMatchEnd = 1,
            [Display(Name = "On Round End")]
            OnRoundEnd = 2 }
        public static MelonPreferences_Entry<EffectsInMatches_Type> DramaticEffectsInMatches;
        public enum EffectsOutsideMatches_Type
        {
            [Display(Name = "Disabled")]
            Disabled = 0,
            [Display(Name = "On Death")]
            OnDeath = 1
        }
        public static MelonPreferences_Entry<EffectsOutsideMatches_Type> DramaticEffectsOutsideMatches;
        public static MelonPreferences_Entry<bool> IncludeStructureInImpact;
        public static MelonPreferences_Entry<float> DramaticEffectsVolume;
        public static MelonPreferences_Entry<bool> DramaticEffectsHaptics;
        public static MelonPreferences_Entry<string> PrimaryEffectColor;
        public static MelonPreferences_Entry<string> SecondaryEffectColor;
        public enum VariableEffects_Type
        {
            [Display(Name = "Disabled")]
            Disabled = 0,
            [Display(Name = "Normal")]
            Normal = 1,
            [Display(Name = "Overkill is weighted more")]
            OverkillBias = 2
        }
        public static MelonPreferences_Entry<VariableEffects_Type> VariableEffects;
        public static MelonPreferences_Entry<float> ImpactFrameDuration;

        public static MelonPreferences_Category Cat_Ragdolls;
        public enum RagdollsInMatches_Type
        {
            [Display(Name = "Disabled")]
            Disabled = 0,
            [Display(Name = "On Match End")]
            OnMatchEnd = 1,
            [Display(Name = "On Round End")]
            OnRoundEnd = 2,
            [Description("<#F80>On hit (prone to lag)")]
            OnHit = 3,
            [Description("<#F00>Per damage (God help you)")]
            PerDamage = 4
        }
        public static MelonPreferences_Entry<RagdollsInMatches_Type> RagdollsInMatches;
        public enum RagdollsOutsideMatches_Type
        {
            [Display(Name = "Disabled")]
            Disabled = 0,
            [Display(Name = "On Death")]
            OnDeath = 1,
            [Description("<#F80>On hit (prone to lag)")]
            OnHit = 2,
            [Description("<#F00>Per damage (God help you)")]
            PerDamage = 3
        }
        public static MelonPreferences_Entry<RagdollsOutsideMatches_Type> RagdollsOutsideMatches;
        public static MelonPreferences_Entry<int> CleanupInMatches;
        public static MelonPreferences_Entry<int> CleanupOutsideMatches;
        public static MelonPreferences_Entry<float> RagdollSoundsVolume;
        public static MelonPreferences_Entry<bool> EnableGhostification;

        public static MelonPreferences_Category Cat_Hidden;
        public static MelonPreferences_Entry<bool> DebugEnabled;
        public static MelonPreferences_Entry<bool> LegacyRagdollJank;
        public static MelonPreferences_Entry<bool> SmashBrosLaunch;

        public static void SetUpUI()
        {
            Config.Cat_DramaticEffects = MelonPreferences.CreateCategory("DramaticEffects", "Dramatic Effects");
            Config.Cat_DramaticEffects.SetFilePath(PreferencesFilePath);
            Config.DramaticEffectsInMatches = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsInMatches", EffectsInMatches_Type.OnRoundEnd, "In Matches", "");
            Config.DramaticEffectsOutsideMatches = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsOutsideMatches", EffectsOutsideMatches_Type.Disabled, "Outside Matches", "");
            Config.IncludeStructureInImpact = Config.Cat_DramaticEffects.CreateEntry("IncludeStructureInImpact", true, "Include Structure", "Include the structure that delivered the killing blow in the impact frame");
            Config.DramaticEffectsVolume = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsVolume", 1f, "Volume", "Volume multiplier for the dramatic sounds\n0-1; default is 1");
            Config.DramaticEffectsHaptics = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsHaptics", true, "Enable Haptics", "Whether to shake the screen and vibrate controllers for dramatic effects\n(Takes into account in-game settings by default)");
            Config.PrimaryEffectColor = Config.Cat_DramaticEffects.CreateEntry("PrimaryEffectColor", "#000000", "Primary Color", "The color of the player/structure silhouettes during the impact frame\nUse \"Match\" to base it on winning/losing matches and rounds\nUse \"None\" to prevent silhouette-ing");
            Config.SecondaryEffectColor = Config.Cat_DramaticEffects.CreateEntry("SecondaryEffectColor", "Match", "Secondary Color", "The color of the background during dramatic effects\nUse \"Match\" to base it on winning/losing matches and rounds");
            Config.VariableEffects = Config.Cat_DramaticEffects.CreateEntry("VariableEffects", VariableEffects_Type.OverkillBias, "Variable Effects", "Whether to base the intensity of the effects on the power of the hit");
            Config.ImpactFrameDuration = Config.Cat_DramaticEffects.CreateEntry("ImpactFrameDuration", 500f, "Impact Frame Duration", "The time in milliseconds the freeze frame will last\nOnly applies when Variable Effects is disabled\n0ms - 1500ms");

            Config.Cat_Ragdolls = MelonPreferences.CreateCategory("Ragdolls", "Ragdolls");
            Config.Cat_Ragdolls.SetFilePath(PreferencesFilePath);
            Config.RagdollsInMatches = Config.Cat_Ragdolls.CreateEntry("RagdollsInMatches", RagdollsInMatches_Type.OnRoundEnd, "Spawning In Matches", "");
            Config.RagdollsOutsideMatches = Config.Cat_Ragdolls.CreateEntry("RagdollsOutsideMatches", RagdollsOutsideMatches_Type.OnDeath, "Spawning Outside Matches");
            Config.CleanupInMatches = Config.Cat_Ragdolls.CreateEntry("CleanupInMatches", 1, "Cleanup In Matches", "When to remove ragdolls in matches\n0: Between matches\n1: Between rounds\n2 and above: Seconds until vanishing");
            Config.CleanupOutsideMatches = Config.Cat_Ragdolls.CreateEntry("CleanupOutsideMatches", 7, "Cleanup Outside Matches", "When to remove ragdolls outside matches\n0: On scene change\n1 and above: Seconds until vanishing");
            Config.RagdollSoundsVolume = Config.Cat_Ragdolls.CreateEntry("RagdollSoundsVolume", 0.5f, "Collision Volume", "Volume of sounds when ragdolls collide with the environment\n0-1; default is 0.5");
            Config.EnableGhostification = Config.Cat_Ragdolls.CreateEntry("EnableGhostification", true, "Enable Ghost-ification", "Players that drop ragdolls can take on a ghostly form afterwards");

            Config.Cat_Hidden = MelonPreferences.CreateCategory("Hidden", "Hidden");
            Config.Cat_Hidden.SetFilePath(PreferencesFilePath);
            Config.DebugEnabled = Config.Cat_Hidden.CreateEntry("debug_mode_enabled", false, "debug mode enabled", "For use when TacoSlayer36 tells you to");
            Config.LegacyRagdollJank = Config.Cat_Hidden.CreateEntry("LegacyRagdollJank", false, "legacy ragdoll jank", "Brings back the janky ragdolls of older versions");
            Config.SmashBrosLaunch = Config.Cat_Hidden.CreateEntry("SmashBrosLaunch", false, "smash bros launch", "Launch ragdolls into the stratosphere");

            UI.Register((MelonBase)Core.Instance, Config.Cat_DramaticEffects, Config.Cat_Ragdolls);
            Core.UIInit = true;
        }

        public static void OnMyPrefsSaved(string filePath)
        {
            if (filePath == PreferencesFilePath) return;

            if (Core.Instance.IsInMatch && Config.CleanupInMatches.Value >= 2)
            {
                foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
                {
                    foreach (Ragdoll ragdoll in pool.PoolItems)
                    {
                        ragdoll.ClearAfter(Config.CleanupInMatches.Value, false);
                    }
                }
            }
            else if (!Core.Instance.IsInMatch && Config.CleanupOutsideMatches.Value >= 1)
            {
                foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
                {
                    foreach (Ragdoll ragdoll in pool.PoolItems)
                    {
                        ragdoll.ClearAfter(Config.CleanupOutsideMatches.Value, false);
                    }
                }
            }
            if (!Config.EnableGhostification.Value)
            {
                foreach (Player player in PlayerManager.Instance.AllPlayers)
                {
                    Ragdoll.UnGhostify(player.Controller);
                }
            }
        }
    }
}
