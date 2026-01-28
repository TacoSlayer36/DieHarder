using MelonLoader;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Text.RegularExpressions;

namespace DieHarder
{
    internal static class HelperFunctions
    {
        public static List<Transform> FindChildrenRecursive(Transform parent)
        {
            List<Transform> children = new();
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                children.Add(child);
                children.AddRange(FindChildrenRecursive(child));
            }
            return children;
        }

        public static void DisableAllComponents(GameObject gameObject, List<Behaviour> except = null)
        {
            foreach (var behavior in gameObject.GetComponents<Behaviour>())
            {
                behavior.enabled = except != null && except.Contains(behavior);
            }
        }

        public static string SanitizeString(string Input)
        {
            string pattern = @"<[^>]*>";
            return Regex.Replace(Input, pattern, string.Empty);
        }
    }
}
