﻿using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace EntityStates.Nemforcer
{
    public class HammerCharge : BaseSkillState
    {
        public static float baseChargeDuration = 2.25f;

        private const float lookthreshold = -0.6f;
        private const float fallSpeedThreshold = -18f;

        private float chargeDuration;
        private bool finishedCharge;
        private ChildLocator childLocator;
        private Animator animator;
        private Transform modelBaseTransform;
        private uint chargePlayID;
        private uint flameLoopPlayID;
        private NemforcerController nemController;
        private float fallTime;
        private bool slamming;
        private Vector3 forwardDirection;
        private bool moving;

        public override void OnEnter()
        {
            base.OnEnter();
            this.chargeDuration = HammerCharge.baseChargeDuration / this.attackSpeedStat;
            this.childLocator = base.GetModelChildLocator();
            this.modelBaseTransform = base.GetModelBaseTransform();
            this.animator = base.GetModelAnimator();
            this.nemController = base.GetComponent<NemforcerController>();

            bool grounded = base.characterMotor.isGrounded;
            this.moving = this.animator.GetBool("isMoving");

            base.PlayAnimation("Gesture, Override", "HammerCharge", "HammerCharge.playbackRate", this.chargeDuration);
            if (grounded && !moving) {
                base.PlayAnimation("Legs, Override", "HammerCharge", "HammerCharge.playbackRate", this.chargeDuration);
            }

            this.chargePlayID = Util.PlayAttackSpeedSound(EnforcerPlugin.Sounds.NemesisStartCharge, base.gameObject, this.attackSpeedStat);
            this.flameLoopPlayID = Util.PlaySound(EnforcerPlugin.Sounds.NemesisFlameLoop, base.gameObject);

            if (base.cameraTargetParams)
            {
                base.cameraTargetParams.aimMode = CameraTargetParams.AimType.OverTheShoulder;
            }

            if (this.nemController) this.nemController.hammerChargeSmall.Play();

            if (NetworkServer.active) base.characterBody.AddBuff(EnforcerPlugin.Modules.Buffs.smallSlowBuff);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            float charge = this.CalcCharge();

            AkSoundEngine.SetRTPCValue("M2_Charge", 100f * charge);

            //implemented body rotating thing for charging the hammer, but pending better strafing animations it really does not look good
            //except when you're standing still that looks gucci
                //so it'll hang in this ugly if block for now
            if (!this.moving) {

                this.moving = this.animator.GetBool("isMoving");

                float rot = this.animator.GetFloat("baseRotate") * 0.8f;
                Vector3 forwardInput;
                if (base.isAuthority && base.inputBank && base.characterDirection) {

                    forwardInput = ((base.inputBank.moveVector == Vector3.zero) ? base.GetAimRay().direction : base.inputBank.moveVector);

                    this.forwardDirection = Vector3.Lerp(this.forwardDirection, forwardInput, 0.1f);
                    this.nemController.pseudoAimMode(rot, this.forwardDirection);
                } else {

                    this.nemController.pseudoAimMode(rot);
                } 
            }

            if (charge >= 1f && !this.finishedCharge)
            {
                this.finishedCharge = true;
                Util.PlaySound(EnforcerPlugin.Sounds.NemesisMaxCharge, base.gameObject);

                if (this.nemController) this.nemController.hammerChargeLarge.Play();

                if (base.cameraTargetParams)
                {
                    base.cameraTargetParams.aimMode = CameraTargetParams.AimType.Standard;
                }

                if (NetworkServer.active) base.characterBody.RemoveBuff(EnforcerPlugin.Modules.Buffs.smallSlowBuff);
            }

            if (base.characterMotor.velocity.y <= 0) this.fallTime += Time.fixedDeltaTime;
            else this.fallTime = 0;

            Vector3 dir = GetAimRay().direction.normalized;

            bool looking = dir.y <= HammerCharge.lookthreshold && !base.characterMotor.isGrounded;
            bool falling = base.characterMotor.velocity.y <= HammerCharge.fallSpeedThreshold;

            bool slamming = looking && falling && !base.characterMotor.isGrounded;

            if (this.animator) this.animator.SetFloat("airSlamReady", slamming ? -1 : 0, 0.1f, Time.fixedDeltaTime);

            if (base.isAuthority && ((!base.IsKeyDownAuthority() && base.fixedAge >= 0.1f)) && !base.IsKeyDownAuthority())
            {
                if (slamming)
                {
                    HammerAirSlam nextState = new HammerAirSlam();
                    nextState.charge = charge;
                    nextState.baseFallTime = this.fallTime;
                    this.outer.SetNextState(nextState);
                }
                else
                {
                    HammerUppercut nextState = new HammerUppercut();
                    nextState.charge = charge;
                    this.outer.SetNextState(nextState);
                }
            }

            if (this.animator) this.animator.SetBool("inCombat", true);
        }

        protected float CalcCharge()
        {
            return Mathf.Clamp01(base.fixedAge / this.chargeDuration);
        }

        public override void OnExit()
        {
            base.OnExit();
            base.PlayAnimation("Gesture, Override", "BufferEmpty");

            if (this.chargePlayID != 0) AkSoundEngine.StopPlayingID(this.chargePlayID);
            if (this.flameLoopPlayID != 0) AkSoundEngine.StopPlayingID(this.flameLoopPlayID);

            if (base.cameraTargetParams)
            {
                base.cameraTargetParams.aimMode = CameraTargetParams.AimType.Standard;
            }

            if (this.nemController)
            {
                if (this.nemController.hammerChargeSmall) this.nemController.hammerChargeSmall.Stop();
                if (this.nemController.hammerChargeLarge) this.nemController.hammerChargeLarge.Stop();
                if (this.nemController.hammerBurst && this.CalcCharge() >= 0.21f) this.nemController.hammerBurst.Play();
            }

            if (NetworkServer.active && base.characterBody && base.characterBody.HasBuff(EnforcerPlugin.Modules.Buffs.smallSlowBuff)) base.characterBody.RemoveBuff(EnforcerPlugin.Modules.Buffs.smallSlowBuff);
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }

    public class HammerUppercut : BaseSkillState
    {
        public float charge;
        public static string hitboxString = "UppercutHitbox";
        public static float maxDamageCoefficient = 25f;
        public static float minDamageCoefficient = 4.5f;
        public static float procCoefficient = 1f;
        public static float maxRecoil = 5f;
        public static float minRecoil = 0.4f;
        public static float initialMaxSpeedCoefficient = 16f;
        public static float initialMinSpeedCoefficient = 2f;
        public static float finalSpeedCoefficient = 0f;
        public static float minDuration = 0.65f;
        public static float maxDuration = 0.9f;
        public static float minknockupForce = 500f;
        public static float maxknockupForce = 5000f;
        public static float maxHopVelocity = 25f;
        public static float minHopVelocity = 5f;
        public static float dashDuration = 0.15f;

        private float speedCoefficient;
        private float damageCoefficient;
        private float recoil;
        private float duration;
        private float knockupForce;
        private float hopVelocity;

        private float dashSpeed;
        private Vector3 forwardDirection;
        private Vector3 previousPosition;

        private float stopwatch;
        private ChildLocator childLocator;
        private bool hasFired;
        private bool hasPlayedUppercutAnim;
        private float hitPauseTimer;
        private OverlapAttack attack;
        private bool inHitPause;
        private Animator animator;
        private BaseState.HitStopCachedState hitStopCachedState;
        private int hitPauseAmounts;
        private Transform modelBaseTransform;
        private Vector3 storedVelocity;
        private NemforcerController nemController;

        public override void OnEnter()
        {
            base.OnEnter();
            this.stopwatch = 0f;
            this.hasFired = false;
            this.hasPlayedUppercutAnim = false;
            base.characterBody.isSprinting = true;

            if (this.charge > 0.21f) this.duration = Util.Remap(this.charge, 0f, 1f, HammerUppercut.minDuration, HammerUppercut.maxDuration);
            else this.duration = Util.Remap(this.charge, 0f, 1f, HammerUppercut.minDuration, HammerUppercut.maxDuration) / this.attackSpeedStat;
            this.speedCoefficient = Util.Remap(this.charge, 0f, 1f, HammerUppercut.initialMinSpeedCoefficient, HammerUppercut.initialMaxSpeedCoefficient);
            this.damageCoefficient = Util.Remap(this.charge, 0f, 1f, HammerUppercut.minDamageCoefficient, HammerUppercut.maxDamageCoefficient);
            this.recoil = Util.Remap(this.charge, 0f, 1f, HammerUppercut.minRecoil, HammerUppercut.maxRecoil);
            this.knockupForce = Util.Remap(this.charge, 0f, 1f, HammerUppercut.minknockupForce, HammerUppercut.maxknockupForce);
            this.hopVelocity = Util.Remap(this.charge, 0f, 1f, HammerUppercut.minHopVelocity, HammerUppercut.maxHopVelocity);

            this.childLocator = base.GetModelChildLocator();
            this.modelBaseTransform = base.GetModelBaseTransform();
            this.animator = base.GetModelAnimator();
            this.nemController = base.GetComponent<NemforcerController>();

            if (base.isAuthority && base.inputBank && base.characterDirection)
            {
                this.forwardDirection = ((base.inputBank.moveVector == Vector3.zero) ? base.GetAimRay().direction : base.inputBank.moveVector).normalized;
            }

            if (this.charge >= 0.6f) Util.PlaySound(EnforcerPlugin.Sounds.NemesisFlameBurst, base.gameObject);

            this.RecalculateSpeed();

            if (base.characterMotor && base.characterDirection)
            {
                base.characterMotor.velocity.y *= 0.1f;
                base.characterMotor.velocity = this.forwardDirection * this.dashSpeed;
            }

            Vector3 b = base.characterMotor ? base.characterMotor.velocity : Vector3.zero;
            this.previousPosition = base.transform.position - b;

            HitBoxGroup hitBoxGroup = Array.Find<HitBoxGroup>(base.GetModelTransform().GetComponents<HitBoxGroup>(), (HitBoxGroup element) => element.groupName == "Uppercut");
                                                                                                                                      
            base.PlayCrossfade("FullBody, Override", "DashForward", "DashForward.playbackRate", HammerUppercut.dashDuration * this.duration, 0.1f * this.duration);
            base.PlayAnimation("Gesture, Override", "BufferEmpty");
            this.animator.SetFloat("charge", this.charge);

            //ill optimize this effect later maybe
            //if (base.isAuthority && this.charge >= 0.9f) EffectManager.SimpleMuzzleFlash(EnforcerPlugin.Assets.nemDashFX, base.gameObject, "MainHurtbox", true);

            NetworkSoundEventDef hitSound = EnforcerPlugin.Assets.nemHammerHitSoundEvent;

            if (base.characterBody.skinIndex == 2) hitSound = EnforcerPlugin.Assets.nemAxeHitSoundEvent;

            this.attack = new OverlapAttack();
            this.attack.damageType = DamageType.Stun1s;
            this.attack.attacker = base.gameObject;
            this.attack.inflictor = base.gameObject;
            this.attack.teamIndex = base.GetTeam();
            this.attack.damage = this.damageCoefficient * this.damageStat;
            this.attack.procCoefficient = 1;
            this.attack.hitEffectPrefab = EnforcerPlugin.Assets.nemHeavyImpactFX;
            if (base.characterBody.skinIndex == 2) this.attack.hitEffectPrefab = EnforcerPlugin.Assets.nemAxeImpactFXVertical;
            this.attack.forceVector = Vector3.up * this.knockupForce;
            this.attack.pushAwayForce = 500f;
            this.attack.hitBoxGroup = hitBoxGroup;
            this.attack.isCrit = base.RollCrit();
            this.attack.impactSound = hitSound.index;
        }

        private void RecalculateSpeed()
        {
            if (this.hasFired && this.charge > 0.21f) this.dashSpeed = 2f;
            else this.dashSpeed = (4 + (0.25f * this.moveSpeedStat)) * Mathf.Lerp(this.speedCoefficient, HammerUppercut.finalSpeedCoefficient, this.stopwatch / this.duration);
        }

        public override void OnExit()
        {
            if (base.characterMotor) base.characterMotor.disableAirControlUntilCollision = false;

            if (base.cameraTargetParams)
            {
                base.cameraTargetParams.fovOverride = -1f;
            }

            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            base.characterBody.isSprinting = true;

            float rot = animator.GetFloat("baseRotate");
            this.nemController.pseudoAimMode(rot);

            if (!this.inHitPause) this.stopwatch += Time.fixedDeltaTime;

            if (this.stopwatch >= this.duration)
            {
                this.outer.SetNextStateToMain();
                return;
            }

            this.RecalculateSpeed();

            if (base.cameraTargetParams)
            {
                base.cameraTargetParams.fovOverride = Mathf.Lerp(Commando.DodgeState.dodgeFOV, 60f, this.stopwatch / this.duration);
            }

            if (this.stopwatch >= (HammerUppercut.dashDuration * this.duration) && !this.hasPlayedUppercutAnim)
            {
                this.hasPlayedUppercutAnim = true;
                base.PlayCrossfade("FullBody, Override", "Uppercut", "Uppercut.playbackRate", (this.duration - (this.duration * HammerUppercut.dashDuration)) * 1.5f, this.duration * 0.1f);
                base.PlayAnimation("Legs, Override", "BufferEmpty");
                if (this.charge >= 0.75f) Util.PlaySound(EnforcerPlugin.Sounds.NemesisSwingSecondary, base.gameObject);
                else Util.PlaySound(EnforcerPlugin.Sounds.NemesisSwingL, base.gameObject);
            }

            if (base.isAuthority)
            {
                if (!this.inHitPause)
                {
                    Vector3 normalized = (base.transform.position - this.previousPosition).normalized;

                    if (base.characterDirection)
                    {
                        if (normalized != Vector3.zero)
                        {
                            Vector3 vector = normalized * this.dashSpeed;
                            float d = Mathf.Max(Vector3.Dot(vector, this.forwardDirection), 0f);
                            vector = this.forwardDirection * d;
                            vector.y = base.characterMotor.velocity.y;
                            base.characterMotor.velocity = vector;
                        }

                        base.characterDirection.forward = this.forwardDirection;
                    }

                    this.previousPosition = base.transform.position;

                    if (this.stopwatch >= (HammerUppercut.dashDuration * this.duration))
                    {
                        if (!this.hasFired)
                        {
                            this.hasFired = true;

                            if (this.charge > 0.21f) base.SmallHop(base.characterMotor, this.hopVelocity);
                            base.AddRecoil(-1f * this.recoil, -2f * this.recoil, -0.5f * this.recoil, 0.5f * this.recoil);

                            EffectManager.SimpleMuzzleFlash(EnforcerPlugin.Assets.nemUppercutSwingFX, base.gameObject, "SwingUppercut", true);
                        }

                        if (this.stopwatch <= 0.75f * this.duration && this.attack.Fire())//lazily hardcoding dont mind me
                        {
                            if (this.charge >= 1 && UnityEngine.Random.value <= 0.01f)
                            {
                                Util.PlaySound(EnforcerPlugin.Sounds.HomeRun, healthComponent.gameObject);
                            }

                            if (base.characterMotor.velocity != Vector3.zero) this.storedVelocity = base.characterMotor.velocity;
                            this.hitStopCachedState = base.CreateHitStopCachedState(base.characterMotor, this.animator, "Uppercut.playbackRate");
                            this.inHitPause = true;

                            float pauseTimeScaling =  4f - (0.005f * hitPauseAmounts * hitPauseAmounts * hitPauseAmounts);
                            pauseTimeScaling = pauseTimeScaling > 0.69f ? pauseTimeScaling : 0.69f;

                            this.hitPauseTimer = pauseTimeScaling * EntityStates.Merc.GroundLight.hitPauseDuration / this.attackSpeedStat;
                            this.hitPauseAmounts++;
                        }
                    }
                    else
                    {
                        if (this.attack.Fire())
                        {
                            if (this.charge >= 1 && UnityEngine.Random.value <= 0.01f)
                            {
                                Util.PlaySound(EnforcerPlugin.Sounds.HomeRun, healthComponent.gameObject);
                            }

                            if (base.characterMotor.velocity != Vector3.zero) this.storedVelocity = base.characterMotor.velocity;
                            this.hitStopCachedState = base.CreateHitStopCachedState(base.characterMotor, this.animator, "Uppercut.playbackRate");
                            this.inHitPause = true;
                            this.hitPauseTimer = (1.5f * EntityStates.Merc.GroundLight.hitPauseDuration) / this.attackSpeedStat;
                        }

                        base.characterMotor.velocity.y *= 0.1f;
                    }
                }
                else
                {
                    base.characterMotor.velocity = Vector3.zero;
                    if (this.animator) this.animator.SetFloat("Uppercut.playbackRate", 0f);
                    this.hitPauseTimer -= Time.fixedDeltaTime;
                    if (this.hitPauseTimer < 0f)
                    {
                        base.ConsumeHitStopCachedState(this.hitStopCachedState, base.characterMotor, this.animator);
                        this.inHitPause = false;
                        if (this.storedVelocity != Vector3.zero) base.characterMotor.velocity = this.storedVelocity;
                    }
                }
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(this.forwardDirection);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            this.forwardDirection = reader.ReadVector3();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            if (this.stopwatch >= HammerUppercut.dashDuration * this.duration) return InterruptPriority.Skill;
            else return InterruptPriority.Frozen;
        }
    }

    public class HammerAirSlam : BaseSkillState
    {
        public float charge;
        public float baseFallTime;
        public static string hitboxString = "UppercutHitbox";
        public static float maxDamageCoefficient = 25f;
        public static float minDamageCoefficient = 3f;
        public static float procCoefficient = 1f;
        public static float maxRecoil = 15f;
        public static float minRecoil = 0.4f;
        public static float baseDuration = 0.3f;
        public static float knockupForce = -12000f;
        public static float minFallVelocity = 40f;
        public static float maxFallVelocity = 80f;
        public static float maxRadius = 180f;
        public static float minRadius = 6f;

        private float damageCoefficient;
        private float recoil;
        private float duration;
        private float fallVelocity;
        private float fallStopwatch;

        private float storedY;
        private float radius;
        private ChildLocator childLocator;
        private bool hasFired;
        private float hitPauseTimer;
        private OverlapAttack attack;
        private bool inHitPause;
        private Animator animator;
        private BaseState.HitStopCachedState hitStopCachedState;
        private Transform modelBaseTransform;

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = HammerAirSlam.baseDuration / this.attackSpeedStat;
            this.hasFired = false;
            base.characterBody.isSprinting = false;
            this.damageCoefficient = Util.Remap(this.charge, 0f, 1f, HammerAirSlam.minDamageCoefficient, HammerAirSlam.maxDamageCoefficient);
            this.recoil = Util.Remap(this.charge, 0f, 1f, HammerAirSlam.minRecoil, HammerAirSlam.maxRecoil);
            this.fallVelocity = Util.Remap(this.charge, 0f, 1f, HammerAirSlam.minFallVelocity, HammerAirSlam.maxFallVelocity);

            this.childLocator = base.GetModelChildLocator();
            this.modelBaseTransform = base.GetModelBaseTransform();
            this.animator = base.GetModelAnimator();

            if (NetworkServer.active) base.characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);

            if (base.characterMotor)
            {
                base.characterMotor.velocity.y -= this.fallVelocity;
            }

            if (this.charge >= 0.6f) Util.PlaySound(EnforcerPlugin.Sounds.NemesisFlameBurst, base.gameObject);

            HitBoxGroup hitBoxGroup = Array.Find<HitBoxGroup>(base.GetModelTransform().GetComponents<HitBoxGroup>(), (HitBoxGroup element) => element.groupName == "Uppercut");

            base.PlayAnimation("FullBody, Override", "HammerAirSlam", "HammerCharge.playbackRate", this.duration);

            if (base.isAuthority)
            {
                EffectManager.SimpleMuzzleFlash(EnforcerPlugin.Assets.nemSlamSwingFX, base.gameObject, "SwingUppercut", true);
                if (this.charge >= 0.6f) EffectManager.SimpleMuzzleFlash(EnforcerPlugin.Assets.nemSlamDownFX, base.gameObject, "MainHurtbox", true);
            }

            NetworkSoundEventDef hitSound = EnforcerPlugin.Assets.nemHammerHitSoundEvent;

            if (base.characterBody.skinIndex == 2) hitSound = EnforcerPlugin.Assets.nemAxeHitSoundEvent;

            this.attack = new OverlapAttack();
            this.attack.damageType = DamageType.Stun1s;
            this.attack.attacker = base.gameObject;
            this.attack.inflictor = base.gameObject;
            this.attack.teamIndex = base.GetTeam();
            this.attack.damage = this.damageCoefficient * this.damageStat;
            this.attack.procCoefficient = 1;
            this.attack.hitEffectPrefab = EnforcerPlugin.Assets.nemHeavyImpactFX;
            if (base.characterBody.skinIndex == 2) this.attack.hitEffectPrefab = EnforcerPlugin.Assets.nemAxeImpactFXVertical;
            this.attack.forceVector = Vector3.up * HammerAirSlam.knockupForce;
            this.attack.pushAwayForce = 50f;
            this.attack.hitBoxGroup = hitBoxGroup;
            this.attack.isCrit = base.RollCrit();
            this.attack.impactSound = hitSound.index;

            base.characterMotor.disableAirControlUntilCollision = true;
        }

        public override void OnExit()
        {
            if (base.cameraTargetParams)
            {
                base.cameraTargetParams.fovOverride = -1f;
            }

            base.PlayAnimation("FullBody, Override", "BufferEmpty");

            if (NetworkServer.active) base.characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);

            this.FireBlast();

            base.OnExit();
        }

        private void FireBlast()
        {
            Vector3 sex = this.childLocator.FindChild("HammerHitbox").transform.position;
            this.radius = Util.Remap(this.fallStopwatch + this.baseFallTime, 0f, 8f, HammerAirSlam.minRadius, HammerAirSlam.maxRadius);
            this.recoil += 0.5f * this.radius;

            base.characterMotor.velocity *= 0.1f;

            base.SmallHop(base.characterMotor, this.radius * 0.3f);

            AkSoundEngine.SetRTPCValue("M2_Charge", 100f * this.charge);
            Util.PlaySound(EnforcerPlugin.Sounds.NemesisSmash, base.gameObject);

            if (base.isAuthority)
            {
                base.AddRecoil(-1f * this.recoil, -2f * this.recoil, -0.5f * this.recoil, 0.5f * this.recoil);

                BlastAttack blastAttack = new BlastAttack();
                blastAttack.radius = this.radius;
                blastAttack.procCoefficient = 1f;
                blastAttack.position = sex;
                blastAttack.attacker = base.gameObject;
                blastAttack.crit = this.attack.isCrit;
                blastAttack.baseDamage = base.characterBody.damage * (0.2f * this.damageCoefficient);
                blastAttack.falloffModel = BlastAttack.FalloffModel.None;
                blastAttack.baseForce = 5000;
                blastAttack.teamIndex = TeamComponent.GetObjectTeam(blastAttack.attacker);
                blastAttack.damageType = DamageType.Stun1s;
                blastAttack.attackerFiltering = AttackerFiltering.NeverHit;
                BlastAttack.Result result = blastAttack.Fire();

                Vector3 directionFlat = base.GetAimRay().direction;
                directionFlat.y = 0;
                directionFlat.Normalize();

                GameObject impactEffect = Resources.Load<GameObject>("Prefabs/Effects/ImpactEffects/PodGroundImpact");

                for (int i = 5; i <= Mathf.RoundToInt(this.radius) + 1; i += 2)
                {
                    EffectManager.SpawnEffect(impactEffect, new EffectData
                    {
                        origin = base.transform.position + i * directionFlat.normalized - 1.8f * Vector3.up,
                        scale = 0.5f
                    }, true);
                }
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            base.characterBody.isSprinting = true;
            this.fallStopwatch += Time.fixedDeltaTime;

            if (!base.characterMotor.disableAirControlUntilCollision  || base.characterMotor.isGrounded)
            {
                this.outer.SetNextStateToMain();
                return;
            }

            if (base.cameraTargetParams)
            {
                base.cameraTargetParams.fovOverride = Mathf.Lerp(Commando.DodgeState.dodgeFOV, 60f, base.fixedAge / this.duration);
            }

            if (base.isAuthority)
            {
                if (!this.inHitPause)
                {
                    if (!this.hasFired)
                    {
                        this.hasFired = true;

                        string soundString = EnforcerPlugin.Sounds.NemesisSwing2;
                        if (base.characterBody.skinIndex == 2) soundString = EnforcerPlugin.Sounds.NemesisSwingAxe;

                        Util.PlaySound(soundString, base.gameObject);
                    }

                    if (this.attack.Fire())
                    {
                        if (base.characterMotor.velocity.y != 0) this.storedY = base.characterMotor.velocity.y;
                        this.hitStopCachedState = base.CreateHitStopCachedState(base.characterMotor, this.animator, "HammerCharge.playbackRate");
                        this.inHitPause = true;
                        this.hitPauseTimer = (2.5f * EntityStates.Merc.GroundLight.hitPauseDuration) / this.attackSpeedStat;
                    }
                }
                else
                {
                    base.characterMotor.velocity = Vector3.zero;
                    if (this.animator) this.animator.SetFloat("HammerCharge.playbackRate", 0f);
                    this.hitPauseTimer -= Time.fixedDeltaTime;
                    if (this.hitPauseTimer < 0f)
                    {
                        base.ConsumeHitStopCachedState(this.hitStopCachedState, base.characterMotor, this.animator);
                        this.inHitPause = false;
                        base.characterMotor.velocity.y = this.storedY;
                        this.storedY = 0;
                    }
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }
}