using System.Collections.Generic;
using System.Linq;

namespace CSFFCardDetailTooltip;

// Supplements the selected provider's rendered text, never its native TooltipText.
public static class TooltipProviderPreview
{
    private static readonly Dictionary<TooltipProvider, string> Details = new();
    private static readonly Dictionary<TooltipProvider, TooltipText> Fallbacks = new();

    public static void Set(TooltipProvider owner, string content)
    {
        if (!owner) return;
        if (string.IsNullOrWhiteSpace(content)) Remove(owner);
        else Details[owner] = content;
    }

    public static void Remove(TooltipProvider owner)
    {
        if (ReferenceEquals(owner, null)) return;
        Details.Remove(owner);
        if (Fallbacks.TryGetValue(owner, out var fallback)) Tooltip.RemoveTooltip(fallback);
        Fallbacks.Remove(owner);
    }

    public static void ClearCards()
    {
        foreach (var owner in Details.Keys.Where(p => !p || p is InGameCardBase).ToArray()) Remove(owner);
    }

    public static void PrepareDisplay()
    {
        foreach (var owner in Details.Keys.Where(p => !p).ToArray()) Remove(owner);
        foreach (var entry in Details)
        {
            var owner = entry.Key;
            bool needsFallback = Plugin.Enabled && owner.isActiveAndEnabled && owner.IsHovered && owner.MyTooltip == null;
            if (!needsFallback)
            {
                if (Fallbacks.TryGetValue(owner, out var old)) Tooltip.RemoveTooltip(old);
                Fallbacks.Remove(owner);
                continue;
            }
            // Some cards have no native tooltip. Never outrank a native provider.
            if (!Fallbacks.TryGetValue(owner, out var fallback))
                Fallbacks[owner] = fallback = new TooltipText { Priority = int.MinValue };
            fallback.TooltipContent = entry.Value;
            Tooltip.AddTooltip(fallback);
        }
    }

    public static string Compose(string original, string details)
    {
        if (string.IsNullOrWhiteSpace(details)) return original;
        // Preserve the original verbatim, including intentional blank lines and rich text.
        return (original ?? "") + (string.IsNullOrEmpty(original) ? "" : "\n") + details;
    }

    public static void AppendToDisplay(Tooltip tooltip)
    {
        if (!Plugin.Enabled || !tooltip || !tooltip.MainCam || !tooltip.TooltipContent || tooltip.CurrentTooltips.Count == 0) return;
        var selected = tooltip.CurrentTooltips[tooltip.CurrentTooltips.Count - 1];
        if (selected == null || selected.ShowHoldBar) return;
        foreach (var entry in Details)
        {
            if (!entry.Key.isActiveAndEnabled || !entry.Key.IsHovered || !ReferenceEquals(entry.Key.MyTooltip, selected)) continue;
            tooltip.TooltipContent.text = Compose(tooltip.TooltipContent.text, entry.Value);
            break;
        }
    }
}
