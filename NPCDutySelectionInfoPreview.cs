using System.Collections.Generic;

namespace CSFFCardDetailTooltip;

public static class NPCDutySelectionInfoPreview
{
    public static string Format(NPCDutySelectionInfo report)
    {
        if (!report.Initialized) return string.Empty;
        var reasons = new List<string>();
        void Add(bool valid, string key, string text)
        {
            if (!valid) reasons.Add(NPCStatInstantModifierPreview.Text(key, text));
        }
        Add(report.DaytimeConditionsValid, "WrongTime", "outside allowed time");
        Add(report.AtHomeConditionValid, "NeedsHome", "must be at home");
        Add(report.HiddenConditionValid, "Hidden", "cannot work while hidden");
        Add(report.PerformancePerDayValid, "DailyLimit", "daily limit reached");
        Add(report.ConditionsValid, "DutyConditions", "duty requirements unmet");
        Add(report.RequiredActionsAreDoable, "ActionRequirements", "required action or material conditions unmet");
        Add(report.SelectionHasNotBeenCancelled, "SelectionCancelled", "selection excluded");
        Add(report.IsNotBlockedByDialog, "InDialog", "busy in dialogue");
        return string.Join("; ", reasons);
    }
}
