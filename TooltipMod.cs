using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CSFFCardDetailTooltip;

internal class TooltipMod
{
    public static ContentSizeFitter Fitter;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Tooltip), "LateUpdate")]
    public static void PrepareTooltipDetails() => TooltipProviderPreview.PrepareDisplay();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Tooltip), "Awake")]
    public static void TooltipAwakePatch(Tooltip __instance)
    {
        Fitter = __instance.GetComponentInParent<ContentSizeFitter>();
        if (Plugin.Enabled) Fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        TextMeshProUGUI content = __instance.TooltipContent;
        content.overflowMode = TextOverflowModes.Page;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Tooltip), "LateUpdate")]
    public static void TooltipLateUpdatePatch(Tooltip __instance)
    {
        TooltipProviderPreview.AppendToDisplay(__instance);
        if (Fitter == null || Fitter.IsDestroyed()) Fitter = __instance.GetComponentInParent<ContentSizeFitter>();
        if (!Plugin.Enabled || __instance.TooltipCount <= 0) return;
        RectTransform parentRect = __instance.ScreenRect;
        VerticalLayoutGroup lg = __instance.GetComponent<VerticalLayoutGroup>();
        RectTransform rect = __instance.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x,
            Mathf.Min(parentRect.rect.height * 0.95f, lg.preferredHeight));
        TextMeshProUGUI title = __instance.TooltipTitle;
        Traverse.Create(title).Field("m_minHeight").SetValue(title.preferredHeight);
        TextMeshProUGUI content = __instance.TooltipContent;
        int totalpages = content.textInfo.pageCount;
        if (Input.GetKeyDown(Plugin.TooltipNextPageHotKey) && content.pageToDisplay < totalpages)
            content.pageToDisplay++;
        else if (Input.GetKeyDown(Plugin.TooltipPreviousPageHotKey) && content.pageToDisplay > 1)
            content.pageToDisplay--;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnHoverExit")]
    public static void ClearProviderDetails(TooltipProvider __instance)
    {
        TooltipProviderPreview.Remove(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnDisable")]
    public static void ClearDisabledProviderDetails(TooltipProvider __instance) => TooltipProviderPreview.Remove(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnDestroy")]
    public static void ClearDestroyedProviderDetails(TooltipProvider __instance) => TooltipProviderPreview.Remove(__instance);
}
