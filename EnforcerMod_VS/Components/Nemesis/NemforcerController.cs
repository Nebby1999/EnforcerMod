﻿using RoR2;
using RoR2.Skills;
using System;
using UnityEngine;

public class NemforcerController : MonoBehaviour
{
    public bool isMultiplayer;

    public SkillDef primarySkillDef;

    public EntityStateMachine mainStateMachine;

    public bool minigunUp;

    private float maxLightningIntensity = 64f;
    private float maxMoonIntensity = 16f;

    private ParticleSystem passiveLightning;
    private ParticleSystem passiveMoons;
    private ParticleSystem.EmissionModule passiveLightningEm;
    private ParticleSystem.EmissionModule passiveMoonsEm;

    public ParticleSystem hammerChargeSmall;
    public ParticleSystem hammerChargeLarge;
    public ParticleSystem hammerBurst;

    private CharacterBody charBody;
    private CharacterMotor charMotor;
    private HealthComponent charHealth;
    private CameraTargetParams cameraShit;
    private ChildLocator childLocator;

    private Vector3 previousDirection;
    private float previousAngle;
    private bool passiveIsPlaying;

    private void Start()
    {
        charBody = GetComponent<CharacterBody>();
        charMotor = GetComponent<CharacterMotor>();
        charHealth = GetComponent<HealthComponent>();
        cameraShit = GetComponent<CameraTargetParams>();
        childLocator = GetComponentInChildren<ChildLocator>();

        primarySkillDef = charBody.skillLocator.primary.skillDef;

        if (childLocator)
        {
            hammerChargeSmall = childLocator.FindChild("HammerChargeSmall").gameObject.GetComponentInChildren<ParticleSystem>();
            hammerChargeLarge = childLocator.FindChild("HammerChargeLarge").gameObject.GetComponentInChildren<ParticleSystem>();
            hammerBurst = childLocator.FindChild("HammerBurst").gameObject.GetComponentInChildren<ParticleSystem>();

            passiveLightning = childLocator.FindChild("PassiveHealEffect").GetChild(0).gameObject.GetComponent<ParticleSystem>();
            passiveMoons = childLocator.FindChild("PassiveHealEffect").GetChild(1).gameObject.GetComponent<ParticleSystem>();
            passiveLightningEm = passiveLightning.emission;
            passiveMoonsEm = passiveMoons.emission;

            UpdatePassiveEffect();
        }

        InitWeapon();

        Invoke("ModelCheck", 0.2f);

        UpdateCamera();
    }

    private void FixedUpdate()
    {
        if (mainStateMachine)
        {
            previousDirection = mainStateMachine.commonComponents.characterDirection.forward;
        }

        UpdatePassiveEffect();
    }

    private void UpdatePassiveEffect()
    {
        //dont give dedede the cool effect for obvious reasons
        if (charBody.baseNameToken == "DEDEDE_NAME") return;
        if (passiveLightning == null || passiveMoons == null) return;

        float healthRemaining = charHealth.combinedHealth / charHealth.fullCombinedHealth;

        if (healthRemaining <= 0.5f)
        {
            if (!passiveIsPlaying)
            {
                passiveIsPlaying = true;
                passiveLightning.Play();
                passiveMoons.Play();
            }

            float lerp = Mathf.InverseLerp(0.2f, 0.6f, healthRemaining);

            passiveLightningEm.rateOverTime = Mathf.SmoothStep(maxLightningIntensity, 0, lerp);
            passiveMoonsEm.rateOverTime = Mathf.SmoothStep(maxMoonIntensity, 0, lerp);
        }
        else
        {
            if (passiveIsPlaying)
            {
                passiveIsPlaying = false;
                passiveLightning.Stop();
                passiveMoons.Stop();
            }
        }
    }

    public void UpdateCamera()
    {
        isMultiplayer = Run.instance.participatingPlayerCount > 1;

        if (isMultiplayer)
        {
            cameraShit.cameraParams.standardLocalCameraPos = new Vector3(0, 0.5f, -12);
        }
        else
        {
            if (!minigunUp)
            {
                cameraShit.cameraParams.standardLocalCameraPos = new Vector3(0, 0.5f, -12);
            }
            else
            {
                cameraShit.cameraParams.standardLocalCameraPos = new Vector3(-1.2f, -0.5f, -9f);
            }
        }
    }

    public void ModelCheck()
    {
        if (charBody && charBody.master)
        {
            if (charBody.baseNameToken == "DEDEDE_NAME") return;

            if (charBody.master.inventory)
            {
                //this hides the hammer
                var characterModel = charBody.modelLocator.modelTransform.GetComponentInChildren<CharacterModel>();
                if (characterModel)
                {
                    characterModel.baseRendererInfos[0].defaultMaterial = characterModel.gameObject.GetComponent<ModelSkinController>().skins[charBody.skinIndex].rendererInfos[0].defaultMaterial;
                    if (charBody.master.inventory.GetItemCount(RoR2Content.Items.ArmorReductionOnHit) > 0) characterModel.baseRendererInfos[0].defaultMaterial = null;
                }
            }
        }
    }

    private int GetWeapon()
    {
        int weapon = 0;

        if (charBody && charBody.skillLocator)
        {
            string skillString = primarySkillDef.skillNameToken;
            switch (skillString)
            {
                case "NEMFORCER_PRIMARY_HAMMER_NAME":
                    weapon = 0;
                    break;
                case "NEMFORCER_PRIMARY_THROWHAMMER_NAME":
                    weapon = 1;
                    break;
            }
        }

        return weapon;
    }

    public void InitWeapon()
    {
        if (charBody.baseNameToken == "DEDEDE_NAME") return;

        int weapon = GetWeapon();
        EquipWeapon(weapon);
        SetCrosshair(weapon);
    }

    public void HideWeapon()
    {
        if (childLocator)
        {
            childLocator.FindChild("HammerModel").gameObject.SetActive(false);
            childLocator.FindChild("AltHammer").gameObject.SetActive(false);
        }
    }

    public void DelayedResetWeapon()
    {
        Invoke("ResetWeapon", 0.1f);
    }

    public void ResetCrosshair()
    {
        int weapon = GetWeapon();
        SetCrosshair(weapon);
    }

    private void EquipWeapon(int weapon)
    {
        if (childLocator)
        {
            HideWeapon();

            switch (weapon)
            {
                case 0:
                    childLocator.FindChild("HammerModel").gameObject.SetActive(true);
                    break;
                case 1:
                    childLocator.FindChild("AltHammer").gameObject.SetActive(true);
                    break;
            }
        }
    }

    private void SetCrosshair(int weapon)
    {
        if (charBody)
        {
            if (minigunUp)
            {
                charBody.crosshairPrefab = Resources.Load<GameObject>("Prefabs/Crosshair/BanditCrosshair");
            }
            else
            {
                switch (weapon)
                {
                    case 0:
                        charBody.crosshairPrefab = Resources.Load<GameObject>("Prefabs/Crosshair/SimpleDotCrosshair");
                        break;
                    case 1:
                        charBody.crosshairPrefab = Resources.Load<GameObject>("Prefabs/Crosshair/StandardCrosshair");
                        break;
                }
            }
        }
    }

    public void resetAimAngle() {
        previousAngle = 0;
    }

    public void pseudoAimMode(float angle) {

        Vector3 aimDirection = mainStateMachine.commonComponents.inputBank.aimDirection;
        pseudoAimMode(angle, aimDirection);
    }

    public void pseudoAimMode(float angle, Vector3 aimDirection) {
        //rotationResetTimer = 1;

        if (!mainStateMachine)
            return;

        //angle = Mathf.Lerp(previousAngle, angle, 0.1f);

        aimDirection = aimDirection.normalized;
        aimDirection.y = 0;

        //direction perpendicular to the aiming
        Vector3 turningDirection = Vector3.Cross(aimDirection, Vector3.up);

        float rot = angle;

        Vector3 turnedDirection = Vector3.RotateTowards(aimDirection, turningDirection, -rot * Mathf.Deg2Rad, 1);

        turnedDirection = Vector3.Lerp(previousDirection, turnedDirection, 0.5f);

        pseudoAimMode(turnedDirection);
        previousAngle = angle;
    }

    //copied and pasted only what we need from SetAimMode cause using the whole thing is a little fucky
    public void pseudoAimMode(Vector3 direction) {
        mainStateMachine.commonComponents.characterDirection.forward = direction;
        mainStateMachine.commonComponents.characterDirection.moveVector = direction;

        if (mainStateMachine.commonComponents.modelLocator) {
            Transform modelTransform = mainStateMachine.commonComponents.modelLocator.modelTransform;
            if (modelTransform) {
                AimAnimator component = modelTransform.GetComponent<AimAnimator>();
                component.AimImmediate();
            }
        }
    }
}
