using RumbleModUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DieHarder
{
    public partial class Core
    {
        public Mod Mod = new Mod();

        public void OnUIInit()
        {
            Mod.ModName = BuildInfo.Name;
            Mod.ModVersion = BuildInfo.Version;
            Mod.SetFolder(BuildInfo.Name);
            Mod.AddDescription("Description", "", BuildInfo.Description, new Tags { IsSummary = true });

            Mod.AddToList("Dramatic Effects In Matches", 2, "0: Disabled\n1: On match end\n2: On round end", new Tags());
            Mod.AddToList("Dramatic Effects Outside Matches", 0, "0: Disabled\n1: On death", new Tags());
            Mod.AddToList("Impact Frame Duration", 500f, "The time in milliseconds the freeze frame will last\n0ms - 1500ms\n(It's recommended to keep this slightly longer than your pre-impact audio length)", new Tags());
            Mod.AddToList("Include Structure In Impact", true, 0, "Include the structure that delivered the killing blow in the impact frame", new Tags());
            Mod.AddToList("Dramatic Effects Volume", 1f, "Volume of the sounds for the dramatic effects\n0-1; default is 1", new Tags());
            Mod.AddToList("Dramatic Effects Screen Shake", 1, "When to shake the screen during dramatic effects\n0: Never\n1: Take comfort settings into account\n2: Always fully", new Tags());
            Mod.AddToList("Primary Effect Color", "#000000", "The color of the player/structure silhouettes during dramatic effects\nUse \"Match\" to base it on winning/losing matches and rounds", new Tags());
            Mod.AddToList("Secondary Effect Color", "Match", "The color of the background during dramatic effects\nUse \"Match\" to base it on winning/losing matches and rounds", new Tags());
            Mod.AddToList("Ragdolls In Matches", 2, "0: Disabled\n1: On match end\n2: On round end\n<#F80>3: On hit (prone to lag)\n<#F00>4: Per damage (God help you)", new Tags());
            Mod.AddToList("Ragdolls Outside Matches", 1, "0: Disabled\n1: On death\n<#F80>2: On hit (prone to lag)\n<#F00>3: Per damage (God help you)", new Tags());
            Mod.AddToList("Cleanup In Matches", 1, "When to remove ragdolls in matches\n0: Between matches\n1: Between rounds\n2 and above: Seconds until vanishing", new Tags());
            Mod.AddToList("Cleanup Outside Matches", 7, "When to remove ragdolls outside matches\n0: On scene change\n1 and above: Seconds until vanishing", new Tags());

            Mod.GetFromFile();
            Mod.ModSaved += OnUISave;

            UI.instance.AddMod(Mod);
        }

        public void OnUISave()
        {
            if (IsInMatch && ModUISettings.CleanupInMatches >= 2)
            {
                foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
                {
                    foreach (Ragdoll ragdoll in pool.PoolItems)
                    {
                        ragdoll.ClearAfter(ModUISettings.CleanupInMatches);
                    }
                }
            }
            else if (!IsInMatch && ModUISettings.CleanupOutsideMatches >= 1)
            {
                foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
                {
                    foreach (Ragdoll ragdoll in pool.PoolItems)
                    {
                        ragdoll.ClearAfter(ModUISettings.CleanupOutsideMatches);
                    }
                }
            }
        }
    }

    public static class ModUISettings
    {
        public static int DramaticEffectsInMatches => (int)Core.Instance.Mod.Settings[1].SavedValue;
        public static int DramaticEffectsOutsideMatches => (int)Core.Instance.Mod.Settings[2].SavedValue;
        public static float ImpactFrameDuration => Mathf.Clamp((float)Core.Instance.Mod.Settings[3].SavedValue, 0, Core.Instance.DebugEnabled ? float.MaxValue : 1500f);
        public static bool IncludeStructureInImpact => (bool)Core.Instance.Mod.Settings[4].SavedValue;
        public static float DramaticEffectsVolume=> (float)Core.Instance.Mod.Settings[5].SavedValue;
        public static int DramaticEffectsScreenShake=> (int)Core.Instance.Mod.Settings[6].SavedValue;
        public static string PrimaryEffectColor => ((string)Core.Instance.Mod.Settings[7].SavedValue).ToLower();
        public static string SecondaryEffectColor => ((string)Core.Instance.Mod.Settings[8].SavedValue).ToLower();
        public static int RagdollsInMatches => (int)Core.Instance.Mod.Settings[9].SavedValue;
        public static int RagdollsOutsideMatches => (int)Core.Instance.Mod.Settings[10].SavedValue;
        public static int CleanupInMatches => (int)Core.Instance.Mod.Settings[11].SavedValue;
        public static int CleanupOutsideMatches => (int)Core.Instance.Mod.Settings[12].SavedValue;
    }
}
