using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Preview adapter for the original CardAction game type.
public static class CardActionPreview
{
    public static CardAction PreviewAction(CardAction source, InGameCardBase receiving, InGameCardBase given)
    {
        var preview = (CardAction)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(source, null);
        // CollectActionModifiers clears these caches. A shallow copy alone would clear the live action.
        preview.AllStatModifiers = new List<StatModifier>();
        preview.AllNPCStatModifiers = new List<NPCStatInstantModifier>();
        preview.AllNPCSetDuties = new List<NPCSetDutyActive>();
        preview.AllTemporaryStatModifiers = new List<StatModifier>();
        preview.IgnoredStatConditions = new List<GameStat>();
        preview.AddedDropDurabilities = new List<AddedDurabilityModifier>();
        preview.FlavoursConsumed = null;
        preview.SpicesConsumed = null;
        var random = UnityEngine.Random.state;
        try { preview.CollectActionModifiers(receiving, given, null); }
        finally { UnityEngine.Random.state = random; }
        return preview;
    }
}
