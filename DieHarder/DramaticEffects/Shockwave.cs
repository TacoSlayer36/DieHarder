using Il2CppOculus.Platform;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.MoveSystem;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Pools;
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
        public PlayerController DamagedPlayer;

        public GameObject ForceField;
        public Renderer ForceFieldRenderer;
        private float forceFieldScale = 0f;
        private float forceFieldProximityDistance = 0.2f;
        private float forceFieldOpacity = 0.2f;

        public static List<GameObject> Dusts = new();

        public List<Ragdoll> movedRagdolls = new();

        public float Timer = 0f;

        void Start()
        {
            CreateForceField();
            CreateDust();
        }

        void Update()
        {
            Timer += Time.deltaTime;

            if (ForceField != null)
            {
                if (Timer <= 0.5f)
                {
                    forceFieldScale += Time.deltaTime * 150f;
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

            if (Timer > 10f)
            {
                GameObject.Destroy(gameObject);
            }

            float fogReturn = Mathf.Lerp(10000f, Impact.FogEndDistanceStorage, Timer / 10f);
            RenderSettings.fogEndDistance = Mathf.Clamp(fogReturn, Impact.FogEndDistanceStorage, 10000f);

            foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
            {
                foreach (Ragdoll ragdoll in pool.PoolItems)
                {
                    if (movedRagdolls.Contains(ragdoll)) continue;
                    float distFromShockwave = Vector3.Distance(ragdoll.Chest.position, transform.position);
                    if (distFromShockwave <= transform.localScale.x)
                    {
                        movedRagdolls.Add(ragdoll);
                        ragdoll.AddVelocity((ragdoll.Chest.position - transform.position).normalized * Core.Instance.V_ShockwaveMove);
                    }
                }
            }
        }

        public void CreateForceField()
        {
            ForceField = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ForceField.transform.SetParent(transform, false);
            ForceField.GetComponent<SphereCollider>().enabled = false;
            ForceFieldRenderer = ForceField.GetComponent<Renderer>();
            ForceFieldRenderer.material = new Material(Core.Instance.ShockwaveShader);
        }

        public void CreateDust()
        {
            if (DamagedPlayer.GetSubsystem<PlayerMovement>().WasGrounded)
            {
                Structure randomCube = PoolManager.Instance.resourcesToPool[55].Resource.GetComponent<Structure>();

                PooledVisualEffect pooledVisualEffect = PoolManager.instance.availablePools[50].FetchFromPool(DamagedPlayer.GetStandingPosition(), Quaternion.identity).gameObject.GetComponent<PooledVisualEffect>();
                pooledVisualEffect.SetStructureData(randomCube);
                Dusts.Add(pooledVisualEffect.gameObject);
                pooledVisualEffect.transform.localScale = Vector3.one * 1.7f;
            }
        }
    }
}
