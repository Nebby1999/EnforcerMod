﻿using RoR2;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;

public class GrenadeController : MonoBehaviour
{
    public BlastAttack blastAttack;
    float initialTime;
    bool never = true;

    GameObject explodePrefab = Resources.Load<GameObject>("prefabs/effects/omnieffect/OmniExplosionVFX");

    void Start()
    {
        initialTime = Time.fixedTime;
    }

    void OnTriggerEnter(Collider other)
    {
        HurtBox component = other.GetComponent<HurtBox>();
        if (component && never)
        {
            never = false;
            explode();
        }
    }

    void Update()
    {
        if (Time.fixedTime - initialTime > 2)
        {
            explode();
        }
    }

    void explode()
    {
        blastAttack.position = transform.position;
        blastAttack.Fire();

        EffectData effectData = new EffectData();
        effectData.origin = transform.position;
        effectData.scale = 5f;
        EffectManager.SpawnEffect(explodePrefab, effectData, false);

        Rigidbody rig = this.GetComponentInParent<Rigidbody>();
        Destroy(rig.gameObject);
    }
}