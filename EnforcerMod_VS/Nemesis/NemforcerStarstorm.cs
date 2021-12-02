﻿using Moonstorm;
using R2API;
using System.Collections.Generic;
using UnityEngine;
using RoR2;
using Moonstorm.Starstorm2.ScriptableObjects;
using RoR2.Navigation;

namespace EnforcerPlugin
{
    public static class NemforcerStarstorm
    {
        public static EventDirectorCard NemforcerInvasionCard { get; private set; }
        public static EventSceneDeck[] NemforcerSceneDecks { get; private set; }
        public static NemesisInventory NemforcerInventory { get; private set; }
        public static NemesisSpawnCard NemforcerSpawnCard { get; private set; }
        internal static void Init()
        {
            CreateEventCard();
            CreateEventDeckArray();
            CreateNemesisInventory();
            CreateSpawnCard();
            AddEventDeck();
        }

        private static void CreateEventCard()
        {
            var eventStart = "ENFORCER_EVENT_NEMFORCERBOSS_START";
            var eventEnd = "ENFORCER_EVENT_NEMFORCERBOSS_END";

            LanguageAPI.Add(eventStart, "An unnatural force emanates from the void... </style>");
            LanguageAPI.Add(eventEnd, "The void's influence fades...</style>");

            LoadoutAPI.AddSkill(typeof(NemforcerInvasion));

            NemforcerInvasionCard = ScriptableObject.CreateInstance<EventDirectorCard>();
            NemforcerInvasionCard.identifier = "NemforcerBoss";
            NemforcerInvasionCard.activationState = new EntityStates.SerializableEntityStateType(typeof(NemforcerInvasion));
            NemforcerInvasionCard.directorCreditCost = 80;
            NemforcerInvasionCard.selectionWeight = 1.3f;
            NemforcerInvasionCard.minimumStageCompletions = 0;
            NemforcerInvasionCard.eventFlags = EventDirectorCard.EventFlags.AfterLoop |
                                               EventDirectorCard.EventFlags.AfterVoidFields |
                                               EventDirectorCard.EventFlags.EnemySpawn |
                                               EventDirectorCard.EventFlags.OncePerRun;
            NemforcerInvasionCard.repeatedSelectionWeight = 0;
            NemforcerInvasionCard.requiredUnlockableDef = EnforcerPlugin.Modules.EnforcerUnlockables.enforcerUnlockableDef;
            NemforcerInvasionCard.startMessageToken = eventStart;
            NemforcerInvasionCard.endMessageToken = eventEnd;
            NemforcerInvasionCard.messageColor = NemforcerPlugin.characterColor;
        }

        private static void CreateEventDeckArray()
        {
            EventSceneDeck CreateDeck(string sceneName)
            {
                var eventDeck = ScriptableObject.CreateInstance<EventSceneDeck>();
                eventDeck.sceneName = sceneName;
                eventDeck.sceneDeck = new EventCardDeck { eventCards = new EventDirectorCard[] { NemforcerInvasionCard } };
                return eventDeck;
            }

            var sceneNames = new string[] { "blackbeach", "dampcavesimple", "foggyswamp", "frozenwall", "golemplains", "goolake", "rootjungle", "shipgraveyard", "skymeadow", "wispgraveyard" };
            List<EventSceneDeck> sceneDecks = new List<EventSceneDeck>();
            
            foreach(string scene in sceneNames)
            {
                sceneDecks.Add(CreateDeck(scene));
            }
            NemforcerSceneDecks = sceneDecks.ToArray();
        }

        //TODO: Create nemesis inventory
        private static void CreateNemesisInventory()
        {
        }

        private static void CreateSpawnCard()
        {
            NemesisSpawnCard.StatModifier CreateModifier(string fieldName, float modifier, NemesisSpawnCard.StatModifierType statModifierType)
            {
                return new NemesisSpawnCard.StatModifier { fieldName = fieldName, modifier = modifier, statModifierType = statModifierType };
            }
            var spawnCard = ScriptableObject.CreateInstance<NemesisSpawnCard>();
            spawnCard.prefab = null; //TODO: new master prefab;
            spawnCard.sendOverNetwork = true;
            spawnCard.hullSize = RoR2.HullClassification.Human;
            spawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
            spawnCard.requiredFlags = NodeFlags.None;
            spawnCard.forbiddenFlags = NodeFlags.None;
            spawnCard.occupyPosition = false;
            spawnCard.eliteRules = SpawnCard.EliteRules.ArtifactOnly;

            spawnCard.nemesisInventory = NemforcerInventory;
            spawnCard.useOverrideState = true;
            spawnCard.overrideSpawnState = new EntityStates.SerializableEntityStateType(); //Todo: Find EST for spawn state;

            List<NemesisSpawnCard.StatModifier> modifiers = new List<NemesisSpawnCard.StatModifier>();
            modifiers.Add(CreateModifier("baseMaxHealth", 1000, NemesisSpawnCard.StatModifierType.Override));
            modifiers.Add(CreateModifier("levelMaxHealth", 300, NemesisSpawnCard.StatModifierType.Override));
            modifiers.Add(CreateModifier("baseDamage", 16, NemesisSpawnCard.StatModifierType.Override));
            modifiers.Add(CreateModifier("levelDamage", 3.2f, NemesisSpawnCard.StatModifierType.Override));
            modifiers.Add(CreateModifier("baseCrit", 0, NemesisSpawnCard.StatModifierType.Override));
            spawnCard.statModifiers = modifiers.ToArray();

            spawnCard.skillOverrides = null; //Todo, maybe find skill overrides

            spawnCard.visualEffect = null; //Todo, maybe find visual effect since nemesis arent massive anymore, lol.
            spawnCard.childName = "";
            spawnCard.itemDef = Moonstorm.Starstorm2.Assets.mainAssetBundle.LoadAsset<ItemDef>("Augury");
        }

        private static void AddEventDeck()
        {
            EventCatalog.AddEventDecks(NemforcerSceneDecks);
        }
    }
}