using System.Collections.Generic;
using System.Linq;

namespace CSFFCardDetailTooltip;

// Read existing work selections; never ask the AI to select or start a duty on hover.
public static class SelectedNPCDutyPreview
{
    public static string FormatCard(InGameCardBase card)
    {
        if (!card) return string.Empty;
        var npc = card.NPCModel ? card.NPCModel : card.CurrentUser.NPC;
        if (!npc) return string.Empty;
        return Format(npc, card.NPCModel ? null : card);
    }

    public static string Format(InGameNPC npc, InGameCardBase onlyCard = null)
    {
        if (!npc || !npc.NPCModel) return string.Empty;
        string T(string key, string fallback) => NPCStatInstantModifierPreview.Text(key, fallback);
        var lines = new List<string>();
        var selected = npc.CurrentDuty;
        var duty = selected.Duty;
        if (duty && !duty.DoNotShowToPlayer)
        {
            lines.Add(Utils.FormatBasicEntry(T("Worker", "Worker"), npc.GetName()));
            lines.Add(Utils.FormatBasicEntry(T("CurrentDuty", "Current duty"), duty.DutyName));
            if (selected.CurrentDutyStep >= 0 && duty.ActionSequence != null)
                lines.Add(Utils.FormatBasicEntry(T("DutyStep", "Duty step"), $"{selected.CurrentDutyStep + 1}/{duty.ActionSequence.Length}"));
            if (selected.IsWaitingInDuty)
                lines.Add(Utils.FormatBasicEntry(T("StepTicks", "Current step: remaining ticks"), selected.RemainingWaitingDutyTicks.ToString()));
            if (!selected.MoveDutyTarget.IsNull && selected.MoveDutyTarget.EnvCard)
                lines.Add(Utils.FormatBasicEntry(T("Destination", "Destination"), selected.MoveDutyTarget.EnvCard.CardName));
            if (selected.ActionStarted && selected.CurrentAction is AffectItemsDutyAction affect && affect.AffectType == AffectItemsDutyAction.AffectTypes.PerformActionOnCard)
            {
                if (selected.DismantleActions != null && selected.ActionCards != null && selected.DismantleActions.Count == selected.ActionCards.Count)
                    for (int i = 0; i < selected.DismantleActions.Count; i++)
                        AddAction(selected.DismantleActions[i], selected.ActionCards[i], null);
                if (selected.CardOnCardActions != null && selected.CardOnCardReceivingCards != null && selected.CardOnCardGivenCards != null
                    && selected.CardOnCardActions.Count == selected.CardOnCardReceivingCards.Count && selected.CardOnCardActions.Count == selected.CardOnCardGivenCards.Count)
                    for (int i = 0; i < selected.CardOnCardActions.Count; i++)
                        AddAction(selected.CardOnCardActions[i], selected.CardOnCardReceivingCards[i], selected.CardOnCardGivenCards[i]);
            }
        }
        // Reports can short-circuit and are snapshots. Only expose failures actually recorded.
        if (!onlyCard && npc.AllDuties != null)
            foreach (var reference in npc.AllDuties)
            {
                if (reference == null || !reference.IsActive || !reference.TargetDuty || reference.TargetDuty.DoNotShowToPlayer) continue;
                string blockers = NPCDutySelectionInfoPreview.Format(reference.WeightInfo);
                if (!string.IsNullOrEmpty(blockers))
                    lines.Add(Utils.FormatBasicEntry(reference.TargetDuty.DutyName,
                        T("LastDutyCheck", "Last duty check") + ": " + blockers));
            }
        return Utils.JoinTooltipLines(lines);

        void AddAction(CardAction action, InGameCardBase receiving, InGameCardBase given)
        {
            if (action == null || !receiving || (onlyCard && onlyCard != receiving && onlyCard != given)) return;
            var preview = CardActionPreview.PreviewAction(action, receiving, given, npc);
            lines.Add(Utils.FormatBasicEntry(T("SelectedWork", "Selected work"), action.ActionName));
            lines.Add(StatModifierPreview.Format(preview.AllStatModifiers, 2));
            var associated = receiving.NPCModel ? receiving.NPCModel : npc;
            lines.Add(NPCStatInstantModifierPreview.Format(preview.AllNPCStatModifiers, associated, 2));
            lines.Add(NPCSetDutyActivePreview.Format(preview.AllNPCSetDuties, associated, 2));
            if (preview.ProducedCards != null && preview.ProducedCards.Length > 0 && preview.ProducedCards.All(c => c != null))
            {
                var report = CollectionDropReportPreview.Create(preview, receiving, given, new InGameNPCOrPlayer(npc));
                string drops = Action.FormatCardDropList(report, receiving, action: preview, indent: 2);
                if (!string.IsNullOrWhiteSpace(drops))
                    lines.Add(T("WorkDrops", "Selected work outcomes (quantities are samples)") + ":\n" + drops);
            }
        }
    }
}
