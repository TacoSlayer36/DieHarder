using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;

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

        public static void CopyAllTransforms(List<Transform> from, List<Transform> to)
        {
            for (int i = 0; i < from.Count; i++)
            {
                if (to.Count < i && from.Count < 1) continue;

                to[i].position = from[i].position;
                to[i].rotation = from[i].rotation;
                to[i].localScale = from[i].localScale;
            }
        }

        public static string SanitizeString(string Input)
        {
            string pattern = @"<[^>]*>";
            return Regex.Replace(Input, pattern, string.Empty);
        }

        public static Vector3 GetRelativeVelocity(Vector3 velocityA, Collision collision) // Collision.relativeVelocity is stripped
        {
            Rigidbody rbB = collision.rigidbody;
            Vector3 velocityB = rbB != null ? rbB.velocity : Vector3.zero;

            return velocityA - velocityB;
        }

        public static byte[] LoadEmbeddedResource(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
                throw new FileNotFoundException($"Embedded resource '{resourcePath}' not found.");
            using MemoryStream ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
