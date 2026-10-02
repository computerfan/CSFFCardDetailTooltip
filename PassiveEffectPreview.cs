using UnityEngine;

namespace CSFFCardDetailTooltip;

// Mirrors InGameCardBase.ApplyPassiveEffectDurabilities without applying an effect.
public static class PassiveEffectPreview
{
    public static float RateContribution(PassiveEffect effect, float value, bool multiply)
    {
        int count = effect.EffectStacksWithRequiredCards ? effect.CurrentStack : 1;
        float scaled = value * (effect.EffectScalesWithDurabilities.Active ? effect.CurrentScaling : 1);
        return multiply ? Mathf.Pow(scaled, count) : scaled * count;
    }

    public static string FormatRateEntry(PassiveEffect effect, float value, string name, bool multiply = false)
        => Utils.FormatRateEntry(RateContribution(effect, value, multiply), name, multiply);
}
