using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        public static void DisableAllComponents(GameObject gameObject, List<Behaviour> except)
        {
            foreach (var behavior in gameObject.GetComponents<Behaviour>())
            {
                if (!except.Contains(behavior))
                    behavior.enabled = false;
            }
        }

        public static string SanitizeString(string input)
        {
            return new string(input.Where(c => char.IsLetter(c)).ToArray());
        }
    }
}
