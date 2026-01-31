using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DieHarder.DramaticEffects
{
    [RegisterTypeInIl2Cpp]
    public class ScreenFlash : MonoBehaviour
    {
        private float timer = 0f;
        private const float flashDuration = 0.07f;
        private Renderer renderer;

        public static void CreateScreenFlash(Transform parent, int layer)
        {
            GameObject newFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            newFlash.transform.SetParent(parent, false);
            newFlash.transform.localScale = Vector3.one * 0.2f;
            newFlash.GetComponent<SphereCollider>().enabled = false;
            newFlash.layer = layer;
            Renderer renderer = newFlash.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            renderer.material.SetInt("_Cull", (int)CullMode.Front);
            renderer.material.SetFloat("_Surface", 1f);
            renderer.material.SetFloat("_Blend", 0f);
            newFlash.AddComponent<ScreenFlash>();
        }

        void Update()
        {
            timer += Time.deltaTime;

            if (renderer == null) renderer = GetComponent<Renderer>();

            if (renderer != null)
            {
                float opacity = Mathf.Lerp(1, 0, timer / flashDuration);
                renderer.material.color = new Color(1f, 1f, 1f, opacity);
            }

            if (timer > flashDuration)
            {
                GameObject.Destroy(gameObject);
            }
        }
    }
}
