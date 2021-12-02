using System;
using System.Collections.Generic;
using System.Text;
using EntityStates.Events;
using Moonstorm.Starstorm2.ScriptableObjects;

namespace EnforcerPlugin
{
    public class NemforcerInvasion : GenericNemesisEvent
    {
        public override void OnEnter()
        {
            spawnCard = NemforcerStarstorm.NemforcerSpawnCard;
            spawnDistanceString = "Close";
            eventCard = NemforcerStarstorm.NemforcerInvasionCard;
            drizzleDuration = 0;
            typhoonDuration = 0;
            warningDuration = 15;
            base.OnEnter();
        }
    }
}
