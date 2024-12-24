﻿using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using static CSFFCardDetailTooltip.Utils;

namespace CSFFCardDetailTooltip;

internal class Encounter
{
    public static TooltipText EncounterTooltip = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnHoverEnter")]
    public static void OnHoverEnter(EncounterOptionButton __instance)
    {
        if (!Plugin.Enabled) return;
        EncounterPopup popup = __instance.GetComponentInParent<EncounterPopup>();
        if (popup == null) return;
        //int actionIndex = __instance.Index;
        //if (actionIndex < 0 || actionIndex > popup.GeneralPlayerActions.Length - 1) return;
        List<string> texts = new();
        if(__instance.SubActions.Count == 1)
            texts.Add(FormatEncounterPlayerAction(__instance.SubActions[0], popup));

        string newContent = texts.Join(delimiter: "\n");
        if (!string.IsNullOrWhiteSpace(newContent))
        {
            EncounterTooltip.TooltipTitle = __instance.Title;
            string orgContent = __instance.MyTooltip == null ? "" : __instance.MyTooltip.TooltipContent;
            EncounterTooltip.TooltipContent = orgContent + (string.IsNullOrEmpty(orgContent) ? "" : "\n") +
                                              "<size=70%>" + newContent + "</size>";
            EncounterTooltip.HoldText = __instance.MyTooltip == null ? "" : __instance.MyTooltip.HoldText;
            Tooltip.AddTooltip(EncounterTooltip);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(EncounterPopup), "DisplayPlayerActions")]
    public static void OnEncounterDisplayPlayerActionsPatch(EncounterPopup __instance)
    {
        if (!Plugin.Enabled || !Plugin.AdditionalEncounterLogMessage) return;
        InGameEncounter encounter = __instance.CurrentEncounter;
        IEnumerable<string> actionTexts = encounter.EncounterModel.EnemyActions
            .Where(a => a is { DoesNotAttack: false }).Select(a => FormatEnemyHitResult(encounter, a, __instance, 1));
        
        if (actionTexts.Any() && !actionTexts.All(string.IsNullOrEmpty))
        {
            __instance.AddToLog(new EncounterLogMessage
            {
                LogText = new LocalizedString
                    { LocalizationKey = "CSFFCardDetailTooltip.Encounter.PossibleWoundsHint", DefaultText = "If I am hit by an enemy, I might get hurt: (on average)" }
            });
            __instance.AddToLog(new EncounterLogMessage
            {
                LogText = new LocalizedString
                    { LocalizationKey = "IGNOREKEY", DefaultText = string.Join("\n", actionTexts) }
            });
        }
        else
        {
            __instance.AddToLog(new EncounterLogMessage
            {
                LogText = new LocalizedString
                    { LocalizationKey = "CSFFCardDetailTooltip.Encounter.ImpossibleWoundsHint", DefaultText = "I am confident it can't hurt me! (on average)" }
            });
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EncounterPopup), "GenerateEnemyWound")]
    public static void PostGenerateEnemyWoundPatch(EncounterPopup __instance)
    {
        if (!Plugin.Enabled || !Plugin.AdditionalEncounterLogMessage) return;
        static string SeverityText(WoundSeverity s)
        {
            return s switch
            {
                WoundSeverity.Minor => $"{LcStr("CSFFCardDetailTooltip.Encounter.DamageThisRound", "This Round's Damage")}: {LcStr("CSFFCardDetailTooltip.Encounter.Minor", "Minor")}",
                WoundSeverity.Medium => $"{LcStr("CSFFCardDetailTooltip.Encounter.DamageThisRound", "This Round's Damage")}: {LcStr("CSFFCardDetailTooltip.Encounter.Medium", "Medium")}",
                WoundSeverity.Serious => $"{LcStr("CSFFCardDetailTooltip.Encounter.DamageThisRound", "This Round's Damage")}: {LcStr("CSFFCardDetailTooltip.Encounter.Serious", "Serious")}",
                _ => ""
            };
        }

        EncounterPlayerDamageReport report = __instance.CurrentRoundPlayerDamageReport;
        if (report.AttackSeverity > WoundSeverity.NoWound)
            __instance.AddToLog(
                new EncounterLogMessage
                {
                    LogText = new LocalizedString
                        { LocalizationKey = "IGNOREKEY", DefaultText = SeverityText(report.AttackSeverity) }
                }
            );
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnHoverExit")]
    public static void EncounterOptionButtonOnHoverExitPatch(EncounterOptionButton __instance)
    {
        Tooltip.RemoveTooltip(EncounterTooltip);
        Tooltip.Instance.TooltipContent.pageToDisplay = 1;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TooltipProvider), "OnDisable")]
    public static void EncounterOptionButtonOnDisablePatch(EncounterOptionButton __instance)
    {
        Tooltip.RemoveTooltip(EncounterTooltip);
        Tooltip.Instance.TooltipContent.pageToDisplay = 1;
    }
}