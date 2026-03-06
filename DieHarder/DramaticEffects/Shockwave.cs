using Il2CppOculus.Platform;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.MoveSystem;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Pools;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DieHarder
{
    [RegisterTypeInIl2Cpp]
    public class Shockwave : MonoBehaviour
    {
        public PlayerController DamagedPlayer;

        public bool HowardInvolved = false;

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
            CreateHitEffect();
            PlayAudio();
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
                else if (Timer <= 0.9f)
                {
                    forceFieldScale += Time.deltaTime * 7000f;
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
                AudioManager.SilenceAudioAfter(Core.ImpactAudioSource, 0f);
            }

            if (Timer > 11f)
            {
                GameObject.Destroy(gameObject);
            }

            if (Timer <= 10f)
            {
                float fogReturn = Mathf.Lerp(10000f, Impact.FogEndDistanceStorage, Timer / 10f);
                RenderSettings.fogEndDistance = Mathf.Clamp(fogReturn, Impact.FogEndDistanceStorage, 10000f);
            }

            if (ForceField == null) return;
            foreach (Ragdoll.RagdollPool pool in Ragdoll.RagdollPools.Values)
            {
                foreach (Ragdoll ragdoll in pool.PoolItems)
                {
                    if (movedRagdolls.Contains(ragdoll)) continue;
                    float distFromShockwave = Vector3.Distance(ragdoll.Chest.position, ForceField.transform.position);
                    if (distFromShockwave <= ForceField.transform.localScale.x)
                    {
                        movedRagdolls.Add(ragdoll);

                        if (ragdoll.Chest.GetComponent<Rigidbody>().velocity.magnitude < 0.3f)
                        {
                            Vector3 shockwavePosOffset = new Vector3(ForceField.transform.position.x, -1f, ForceField.transform.position.z);
                            ragdoll.AddVelocity((ragdoll.Chest.position - shockwavePosOffset).normalized * 40f);
                        }
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
            if (!HowardInvolved && DamagedPlayer.PlayerMovement.WasGrounded)
            {
                Structure randomCube = PoolManager.Instance.resourcesToPool[68].Resource.GetComponent<Structure>();

                PooledVisualEffect pooledVisualEffect = PoolManager.instance.availablePools[63].FetchFromPool(DamagedPlayer.GetStandingPosition(), Quaternion.identity).gameObject.GetComponent<PooledVisualEffect>();
                pooledVisualEffect.parameterCollection.Apply(pooledVisualEffect.visualEffect, randomCube);

                Dusts.Add(pooledVisualEffect.gameObject);
                pooledVisualEffect.transform.localScale = Vector3.one * 2.5f;
            }
        }

        public void CreateHitEffect()
        {
            Vector3 pos;
            bool howardDied = false;
            if (HowardInvolved && Core.Instance.Howard != null && Core.Instance.Howard.currentHp == 0) howardDied = true;

            if (!HowardInvolved || (HowardInvolved && !howardDied)) pos = DamagedPlayer.GetChest().position;
            else
            {
                if (Core.Instance.HowardSmr) pos = Core.Instance.HowardSmr.transform.position;
                else pos = DamagedPlayer.GetChest().position;
            }

            GameObject hitMarker = PoolManager.instance.availablePools[46].FetchFromPool(pos, Quaternion.identity).gameObject;
            PlayerHitmarker phm = hitMarker?.gameObject?.GetComponent<PlayerHitmarker>();
            if (phm != null)
            {
                phm.SetDamage(7f);
                MelonCoroutines.Start(C_EnlargeVFX(phm));
            }
        }

        static IEnumerator C_EnlargeVFX(PlayerHitmarker phm)
        {
            yield return new WaitForSeconds(0.1f);
            phm.SetDamage(15f);
        }

        public void PlayAudio()
        {
            Core.ImpactAudioSource.volume = ModUISettings.DramaticEffectsVolume;
            Core.ImpactAudioSource.Play();
        }
    }
}
