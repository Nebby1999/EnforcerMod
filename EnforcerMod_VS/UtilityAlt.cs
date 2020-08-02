﻿using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace EntityStates.Enforcer
{
    public class StunGrenade : BaseSkillState
    {
        public static float baseDuration = 0.4f;
        public static float damageCoefficient = 1.5f;
        public static float procCoefficient = 0.6f;
        public static float bulletRecoil = 2f;

        public static string muzzleString = "GrenadeMuzzle";

        private float duration;
        private ChildLocator childLocator;
        private Animator animator;

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = StunGrenade.baseDuration / this.attackSpeedStat;
            this.childLocator = base.GetModelTransform().GetComponent<ChildLocator>();
            this.animator = base.GetModelAnimator();

            this.animator.SetBool("gunUp", true);

            //base.PlayAnimation("Gesture, Override", "FireShotgun", "FireShotgun.playbackRate", this.duration);

            Util.PlaySound(EnforcerPlugin.Sounds.LaunchStunGrenade, base.gameObject);

            base.AddRecoil(-2f * StunGrenade.bulletRecoil, -3f * StunGrenade.bulletRecoil, -1f * StunGrenade.bulletRecoil, 1f * StunGrenade.bulletRecoil);
            base.characterBody.AddSpreadBloom(0.33f * StunGrenade.bulletRecoil);
            EffectManager.SimpleMuzzleFlash(Commando.CommandoWeapon.FirePistol.effectPrefab, base.gameObject, StunGrenade.muzzleString, false);

            if (base.isAuthority)
            {
                Ray aimRay = base.GetAimRay();
                FireProjectileInfo info = new FireProjectileInfo()
                {
                    crit = base.RollCrit(),
                    damage = StunGrenade.damageCoefficient * this.damageStat,
                    damageColorIndex = DamageColorIndex.Default,
                    damageTypeOverride = DamageType.Stun1s,
                    force = 0,
                    owner = base.gameObject,
                    position = childLocator.FindChild(StunGrenade.muzzleString).position,
                    procChainMask = default(ProcChainMask),
                    projectilePrefab = EnforcerPlugin.EnforcerPlugin.stunGrenade,
                    rotation = Quaternion.LookRotation(base.GetAimRay().direction),
                    useFuseOverride = false,
                    useSpeedOverride = false,
                    target = null
                };
                ProjectileManager.instance.FireProjectile(info);
            }
        }

        public override void OnExit()
        {
            base.OnExit();

            this.animator.SetBool("gunUp", false);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
}