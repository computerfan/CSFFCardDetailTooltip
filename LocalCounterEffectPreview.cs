namespace CSFFCardDetailTooltip;

public static class LocalCounterEffectPreview
{
    public static string FormatRateEntry(LocalCounterEffect effect, DurabilitiesTypes type)
    {
        // GetModifier respects OptionalFloatValue.Active and missing values.
        float value = effect.GetModifier(type);
        return value == 0 || !effect.Counter ? string.Empty : Utils.FormatRateEntry(value, effect.Counter.name);
    }
}
