using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Preview adapter for the original ArmorValues game type.
public static class ArmorValuesPreview
{
    public static float ArmorDefense(InGameCardBase card, List<DamageType> damageTypes, BodyLocations location)
    {
        if (card.IsLiquid && card.CurrentContainer &&
            card.CurrentContainer.CardModel.ArmorValues.CalculateArmorForLocation(null, location) == 0) return 0;
        return card.CardModel.ArmorValues.CalculateArmorForLocation(damageTypes, location) *
               card.CardModel.ArmorValueMultiplier(card);
    }

    public static BodyLocationReportWeights ArmorHitWeights(InGameCardBase card)
    {
        var armor = card.CardModel.ArmorValues;
        var mask = card.IsLiquid && card.CurrentContainer ? card.CurrentContainer.CardModel.ArmorValues : armor;
        float scale = card.CardModel.ArmorProbabilitiesMultiplier(card);
        return new BodyLocationReportWeights
        {
            Head = mask.HeadHitProbabilityModifier != 0 ? armor.HeadHitProbabilityModifier * scale : 0,
            Torso = mask.TorsoHitProbabilityModifier != 0 ? armor.TorsoHitProbabilityModifier * scale : 0,
            LArm = mask.LArmHitProbabilityModifier != 0 ? armor.LArmHitProbabilityModifier * scale : 0,
            RArm = mask.RArmHitProbabilityModifier != 0 ? armor.RArmHitProbabilityModifier * scale : 0,
            LLeg = mask.LLegHitProbabilityModifier != 0 ? armor.LLegHitProbabilityModifier * scale : 0,
            RLeg = mask.RLegHitProbabilityModifier != 0 ? armor.RLegHitProbabilityModifier * scale : 0
        };
    }
}
