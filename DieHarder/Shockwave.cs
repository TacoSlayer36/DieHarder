using Il2CppOculus.Platform;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Shockwave : MonoBehaviour
    {
        public GameObject ForceField;
        public Renderer ForceFieldRenderer;
        private float forceFieldScale = 0f;
        private float forceFieldProximityDistance = 0.2f;
        private float forceFieldOpacity = 0.2f;

        public float Timer = 0f;

        void Start()
        {
            ForceField = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ForceField.transform.SetParent(transform, false);
            ForceField.GetComponent<SphereCollider>().enabled = false;
            ForceFieldRenderer = ForceField.GetComponent<Renderer>();
            ForceFieldRenderer.material = new Material(Core.Instance.ShockwaveShader);
        }

        void Update()
        {
            Timer += Time.deltaTime;

            if (ForceField != null)
            {
                if (Timer <= 0.5f)
                {
                    forceFieldScale += Time.deltaTime * 200f;
                    forceFieldProximityDistance += Time.deltaTime;
                }
                else
                {
                    forceFieldScale += Time.deltaTime * 2000f;
                    forceFieldProximityDistance += Time.deltaTime * 5;
                }

                if (Timer >= 1f)
                {

                    forceFieldProximityDistance -= Time.deltaTime * 20f;
                    forceFieldOpacity -= Time.deltaTime * 0.8f;
                }

                ForceField.transform.localScale = Vector3.one * forceFieldScale;
                ForceFieldRenderer.material.SetFloat("_ProximityDistance", forceFieldProximityDistance);
                ForceFieldRenderer.material.SetFloat("_BaseOpacity", forceFieldOpacity);

                if (Timer > 3f) GameObject.Destroy(ForceField);
            }

            if (ForceField == null)
            {
                GameObject.Destroy(gameObject);
            }
        }
    }
}
