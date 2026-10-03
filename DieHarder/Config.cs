using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using MelonLoader;
using System.ComponentModel;
using UIFramework;
using System.ComponentModel.DataAnnotations;
using System.Numerics;

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
        public enum EffectsHaptics_Type
        {
            [Display(Name = "None")]
            None = 0,
            [Display(Name = "Use In-Game Settings")]
            UseSettings = 1,
            [Display(Name = "Always Shake Screen")]
            AlwaysShake = 2
        }
        public static MelonPreferences_Entry<EffectsHaptics_Type> DramaticEffectsHaptics;
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

        public static MelonPreferences_Category Cat_Filming;
        public enum ControllerButton
        {
            [Display(Name = "None")]
            None,
            [Display(Name = "Left Primary")]
            LeftPrimary,
            [Display(Name = "Right Primary")]
            RightPrimary,
            [Display(Name = "Left Secondary")]
            LeftSecondary,
            [Display(Name = "Right Secondary")]
            RightSecondary,
            [Display(Name = "Left Trigger")]
            LeftTrigger,
            [Display(Name = "Right Trigger")]
            RightTrigger,
            [Display(Name = "Left Grip")]
            LeftGrip,
            [Display(Name = "Right Grip")]
            RightGrip,
            [Display(Name = "Left Joystick")]
            LeftJoystick,
            [Display(Name = "Right Joystick")]
            RightJoystick
        }
        public enum RagdollVelocityType
        {
            [Display(Name = "Default")]
            Default,
            [Display(Name = "Inherit From Player")]
            Inherit,
            [Display(Name = "Motionless")]
            Motionless,
        }
        public static MelonPreferences_Entry<bool> EnableFilmingFeatures;
        public static MelonPreferences_Entry<bool> RagdollOnNextHit;
        public static MelonPreferences_Entry<ControllerButton> RagdollOnButton;
        public static MelonPreferences_Entry<RagdollVelocityType> RagdollVelocity;
        public static MelonPreferences_Entry<bool> InvisibleGhosts;
        public static MelonPreferences_Entry<ControllerButton> ResetRagdollsButton;
        public static MelonPreferences_Entry<bool> EffectsOnNextHit;
        public static MelonPreferences_Entry<ControllerButton> EffectsOnButton;
        public static MelonPreferences_Entry<float> DramaValue;
        public static MelonPreferences_Entry<bool> DisableReplayBlock;

        public static void SetUpUI()
        {
            Config.Cat_DramaticEffects = MelonPreferences.CreateCategory("DramaticEffects", "Dramatic Effects");
            Config.Cat_DramaticEffects.SetFilePath(PreferencesFilePath);
            Config.DramaticEffectsInMatches = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsInMatches", EffectsInMatches_Type.OnRoundEnd, "In Matches", "");
            Config.DramaticEffectsOutsideMatches = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsOutsideMatches", EffectsOutsideMatches_Type.Disabled, "Outside Matches", "");
            Config.IncludeStructureInImpact = Config.Cat_DramaticEffects.CreateEntry("IncludeStructureInImpact", true, "Include Structure", "Include the structure that delivered the killing blow in the impact frame");
            Config.DramaticEffectsVolume = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsVolume", 1f, "Volume", "Volume multiplier for the dramatic sounds\n0-1; default is 1");
            Config.DramaticEffectsHaptics = Config.Cat_DramaticEffects.CreateEntry("DramaticEffectsHaptics", EffectsHaptics_Type.AlwaysShake, "Haptics", "Whether to shake the screen and vibrate controllers for dramatic effects");
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

            Config.Cat_Filming = MelonPreferences.CreateCategory("Filming", "Filming");
            Config.Cat_Filming.SetFilePath(PreferencesFilePath);
            Config.EnableFilmingFeatures = Config.Cat_Filming.CreateEntry("EnableFilmingFeatures", false, "Enable Filming Features", "Easily enable/disable all the below features");
            Config.RagdollOnNextHit = Config.Cat_Filming.CreateEntry("RagdollOnNextHit", false, "Ragdoll Next Hit", "Force a ragdoll to spawn the next time someone takes damage");
            Config.RagdollOnButton = Config.Cat_Filming.CreateEntry("RagdollOnButton", ControllerButton.None, "Ragdoll On Button", "Force a ragdoll to spawn when this controller button is pressed");
            Config.RagdollVelocity = Config.Cat_Filming.CreateEntry("RagdollVelocity", RagdollVelocityType.Inherit, "Ragdoll Velocity", "The way velocity is applied to all ragdolls");
            Config.InvisibleGhosts = Config.Cat_Filming.CreateEntry("InvisibleGhosts", false, "Invisible Ghosts", "Makes players invisible instead of ghostly after being ragdoll-ed");
            Config.ResetRagdollsButton = Config.Cat_Filming.CreateEntry("ResetRagdollsOnButton", ControllerButton.None, "Reset Ragdolls Button", "Clear all ragdolls when this controller button is pressed");
            UI.CreateButtonEntry(Cat_Filming, "Reset", "Reset Ragdolls", "Remove all ragdolls and make their players visible", Ragdoll.ClearAllRagdolls);
            Config.EffectsOnNextHit = Config.Cat_Filming.CreateEntry("EffectsOnNextHit", false, "Dramatic Effects Next Hit", "Force dramatic effects the next time someone takes damage");
            Config.EffectsOnButton = Config.Cat_Filming.CreateEntry("EffectsOnButton", ControllerButton.None, "Effects On Button", "Force dramatic effects when this controller button is pressed");
            Config.DramaValue = Config.Cat_Filming.CreateEntry("DramaValue", -1f, "Drama Value", "The forced intensity any dramatic effects (usually 0.0 - 2.0; -1 for defaults)");
            Config.DisableReplayBlock = Config.Cat_Filming.CreateEntry("DieHarder-DisableReplayBlock", false, "Disable Replay Block", "Allow mod to activate during ReplayMod replays (this is not officially supported and apt to break)");

            if (PlayerManager.Instance?.LocalPlayer?.Data?.GeneralData?.PlayFabMasterId == "A38F5067A38BDDC9")
            {
                UI.RegisterMelon((MelonBase)Core.Instance, Config.Cat_DramaticEffects, Config.Cat_Ragdolls, Config.Cat_Filming, Cat_Hidden);
            }
            else
            {
                UI.RegisterMelon((MelonBase)Core.Instance, Config.Cat_DramaticEffects, Config.Cat_Ragdolls, Config.Cat_Filming);
            }

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
