using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// GetCollectionDropsReport rolls quantities and fills mutable collection caches even for UI callers.
public static class CollectionDropReportPreview
{
    public static CollectionDropReport Create(CardAction action, InGameCardBase receiving,
        InGameCardBase given, InGameNPCOrPlayer user, bool checkEnvironment = false)
    {
        var random = Random.state;
        var manager = GameManager.Instance;
        var nextEnvironment = manager.NextEnvironment;
        try
        {
            var copy = (CardAction)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(action, null);
            copy.ProducedCards = action.ProducedCards.Select(collection => collection.Copy()).ToArray();
            return manager.GetCollectionDropsReport(copy, receiving, given, user, checkEnvironment);
        }
        finally
        {
            manager.NextEnvironment = nextEnvironment;
            Random.state = random;
        }
    }
}
