using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Preview adapter for the original CardStateChange game type.
public static class CardStateChangePreview
{
    public static CardStateChange PreviewStateChange(CardStateChange change, InGameCardBase card,
        InGameCardBase other, float liquidScale, bool overTime)
    {
        if (change.ModType != CardModifications.DurabilityChanges) return change;
        // The two game execution paths apply transfers in different orders.
        Vector2 liquid = change.ModifyLiquid ? change.LiquidQuantityChange : Vector2.zero;
        if (!overTime) CopyOtherDurabilities(ref change, other);
        else if (!change.ModifyLiquid) change.LiquidQuantityChange = Vector2.zero;
        change.ApplyStatInterpolatedChanges(InGameNPCOrPlayer.PlayerAgent);
        change.ApplyDurabilityInterpolatedTransfers(card, other);
        change.ApplyLiquidQuantityScaling(card, other);
        if (overTime) CopyOtherDurabilities(ref change, other);
        else change.LiquidQuantityChange = liquid; // ApplyCardStateChange consumes liquid before interpolation.
        float scale = liquidScale < 0 ? 1 : liquidScale;
        change.SpoilageChange *= scale;
        change.UsageChange *= scale;
        change.FuelChange *= scale;
        change.ChargesChange *= scale;
        change.Special1Change *= scale;
        change.Special2Change *= scale;
        change.Special3Change *= scale;
        change.Special4Change *= scale;
        return change;
    }

    private static void CopyOtherDurabilities(ref CardStateChange change, InGameCardBase other)
    {
        if (change.GetSpoilageFromOtherCard) change.SpoilageChange = Vector2.one * (other ? other.CurrentSpoilage : 0);
        if (change.GetUsageFromOtherCard) change.UsageChange = Vector2.one * (other ? other.CurrentUsageDurability : 0);
        if (change.GetFuelFromOtherCard) change.FuelChange = Vector2.one * (other ? other.CurrentFuel : 0);
        if (change.GetProgressFromOtherCard) change.ChargesChange = Vector2.one * (other ? other.CurrentProgress : 0);
        if (change.GetSpecial1FromOtherCard) change.Special1Change = Vector2.one * (other ? other.CurrentSpecial1 : 0);
        if (change.GetSpecial2FromOtherCard) change.Special2Change = Vector2.one * (other ? other.CurrentSpecial2 : 0);
        if (change.GetSpecial3FromOtherCard) change.Special3Change = Vector2.one * (other ? other.CurrentSpecial3 : 0);
        if (change.GetSpecial4FromOtherCard) change.Special4Change = Vector2.one * (other ? other.CurrentSpecial4 : 0);
    }
}
