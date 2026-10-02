using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Read-only presentation of GenericEncounterPlayerAction and its conditional effects.
public static class GenericEncounterPlayerActionPreview
{
    public static string Text(string key, string fallback) => Utils.LcStr("CSFFCardDetailTooltip.Encounter." + key, fallback);
    public static string Condition(EnemyActionEffectCondition condition) => Text(condition.ToString(), condition switch
    {
        EnemyActionEffectCondition.OnlyOnHit => "On hit",
        EnemyActionEffectCondition.OnlyOnMiss => "On miss",
        EnemyActionEffectCondition.OnlyOnHitAndWound => "On wounding hit",
        _ => "Always"
    });

    public static string FormatEffects(GenericEncounterPlayerAction action, InGameEncounter encounter, int indent = 0)
    {
        var lines = new List<string>();
        void Add(string title, string body)
        {
            if (!string.IsNullOrWhiteSpace(body)) lines.Add(new string(' ', indent) + title + ":\n" + body);
        }
        Add(Text("ActionStats", "Action stat changes"), StatModifierPreview.Format(action.ActionStatChanges, indent + 2));
        Add(Text("EnemyValues", "Enemy values"), EnemyValuesModifiersPreview.Format(action.EnemyValuesModifiers, indent + 2));
        if (action.AdditionalEnemyValuesModifiers != null)
            foreach (var modifier in action.AdditionalEnemyValuesModifiers)
                Add(Text("EnemyValues", "Enemy values"), EnemyValuesModifiersPreview.Format(modifier, indent + 2));
        var wrestling = action.WrestlingStateChange;
        if (wrestling.WrestlingStateChange != EncounterWrestlingChange.DontChangeWrestling)
            lines.Add(new string(' ', indent) + Text(wrestling.WrestlingStateChange.ToString(), wrestling.WrestlingStateChange == EncounterWrestlingChange.StartWrestling ? "Start wrestling" : "Stop wrestling") + " (" + Condition(wrestling.ChangeApplies) + ")");
        void Vulnerable(VulnerableActionEffect effect, bool already, string who)
        {
            float chance = effect.PercentageChance.UseChance ? Mathf.Clamp01(effect.PercentageChance.ChanceValue) : 1;
            if (already || chance <= 0 || effect.Change == VulnerableStateChange.DontMakeVulnerable) return;
            var condition = effect.Change == VulnerableStateChange.MakeVulnerableOnSuccess ? EnemyActionEffectCondition.OnlyOnHit
                : effect.Change == VulnerableStateChange.MakeVulnerableOnFailure ? EnemyActionEffectCondition.OnlyOnMiss : EnemyActionEffectCondition.Always;
            lines.Add(new string(' ', indent) + who + $": {chance:P0} ({Condition(condition)})");
        }
        Vulnerable(action.SelfVulnerableStateChange, encounter.PlayerVulnerable, Text("PlayerVulnerable", "Make player vulnerable"));
        Vulnerable(action.OpponentVulnerableStateChange, encounter.EnemyVulnerable, Text("EnemyVulnerable", "Make enemy vulnerable"));
        if (action.TemporaryEffects != null)
            foreach (var effect in action.TemporaryEffects)
                lines.Add(EncounterTemporaryEffectPreview.Format(effect, encounter, action.ActionType != EncounterPlayerActionType.MainAction, indent));
        return Utils.JoinTooltipLines(lines);
    }
}
