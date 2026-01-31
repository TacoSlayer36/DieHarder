using RumbleModUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            Mod.AddToList("Dramatic Effects", true, 0, "Freeze frame, shockwaves, loud sound—the whole ninety yards", new Tags());
            Mod.AddToList("Impact Frame Duration", 500f, "The time in milliseconds the freeze frame will last", new Tags());
            Mod.AddToList("Include Structure In Impact", true, 0, "Include the structure that delivered the killing blow in the impact frame", new Tags());
            Mod.AddToList("Ragdolls", true, 0, "", new Tags());
            Mod.AddToList("Clean ragdolls after", 0, "The amount of time to wait before a ragdoll vanishes on its own\n(outisde of matches)", new Tags());

            Mod.GetFromFile();
            Mod.ModSaved += OnUISave;

            UI.instance.AddMod(Mod);
        }

        public void OnUISave()
        {
            
        }
    }

    public static class ModUISettings
    {
        public static bool DoDramaticEffects => (bool)Core.Instance.Mod.Settings[1].SavedValue;
        public static float FreezeFrameDuration => (float)Core.Instance.Mod.Settings[2].SavedValue;
        public static bool IncludeStructureSilhouette => (bool)Core.Instance.Mod.Settings[3].SavedValue;
        public static bool DoSpawnRagdolls => (bool)Core.Instance.Mod.Settings[4].SavedValue;
        public static float CleanRagdollsAfter => (float)Core.Instance.Mod.Settings[5].SavedValue;
    }
}
