using System.Collections.Generic;
using System.Linq;

namespace CSFFCardDetailTooltip;

public static class NPCSetDutyActivePreview
{
    public static string Format(IEnumerable<NPCSetDutyActive> changes, InGameNPC context, int indent = 0)
    {
        if (changes == null) return string.Empty;
        var lines = new List<string>();
        foreach (var change in changes)
        {
            if (!change.Duty || change.Duty.DoNotShowToPlayer) continue;
            // ApplySetDutyActive stops at the first matching NPC (unlike stat changes).
            foreach (var npc in NPCStatInstantModifierPreview.Targets(change.UseAssociatedAgent, change.TargetAgent, context).Take(1))
            {
                var reference = npc.GetInfoForDuty(change.Duty);
                if (!npc.Initialized || reference == null) continue;
                bool active = change.SetActive || reference.ActivatingMode == NPCDutyActiveSettings.AlwaysActive;
                if (reference.IsActive == active) continue;
                lines.Add(Utils.FormatBasicEntry(npc.GetName(),
                    NPCStatInstantModifierPreview.Text(active ? "EnableDuty" : "DisableDuty",
                        active ? "Enable duty" : "Disable duty") + ": " + change.Duty.DutyName, indent: indent));
            }
        }
        return Utils.JoinTooltipLines(lines);
    }
}
