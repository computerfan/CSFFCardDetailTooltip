using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

public static class Utils
{
    public static string JoinTooltipLines(IEnumerable<string> sections)
    {
        return string.Join("\n", sections.Where(s => !string.IsNullOrWhiteSpace(s))
            .SelectMany(s => s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }
    public static string LcStr(string key, string defaultText = null)
    {
        if (LocalizationManager.CurrentTexts != null && LocalizationManager.CurrentTexts.TryGetValue(key, out string value))
            return value;
        return defaultText ?? key;
    }

#if MELON_LOADER
public static void GetWoundsForSeverity_il2cpp(this PlayerWounds playerWounds, WoundSeverity _WoundSeverity, List<PlayerWound> _List)
  {
    switch (_WoundSeverity)
    {
      case WoundSeverity.Minor:
        if (playerWounds.MinorWounds == null)
          break;
        _List.AddRange(playerWounds.MinorWounds);
        break;
      case WoundSeverity.Medium:
        if (playerWounds.MediumWounds == null)
          break;
        _List.AddRange(playerWounds.MediumWounds);
        break;
      case WoundSeverity.Serious:
        if (playerWounds.SeriousWounds == null)
          break;
        _List.AddRange(playerWounds.SeriousWounds);
        break;
      default:
        if (playerWounds.UnharmedResults == null)
          break;
        _List.AddRange(playerWounds.UnharmedResults);
        break;
    }
  }
#else
    public static T get_Item<T>(this List<T> list, int index)
    {
        return list[index];
    }
#endif

    public static string FormatEncounterPlayerAction(GenericEncounterPlayerAction action, EncounterPopup popup, int indent = 0)
    {
        var encounter = popup.CurrentEncounter;
        if (action == null || !encounter || encounter.CurrentEnemyAction == null) return string.Empty;
        var savedMelee = popup.CurrentRoundMeleeClashResult;
        var savedRanged = popup.CurrentRoundRangedClashResult;
        var savedAction = encounter.CurrentPlayerAction;
        var random = UnityEngine.Random.state;
        MeleeClashResultsReport melee;
        RangedClashResultReport ranged;
        float chance;
        EncounterDistanceChange distanceChange;
        try
        {
            // Native distance resolution also reads CurrentPlayerAction indirectly.
            encounter.CurrentPlayerAction = action;
            distanceChange = popup.ChangeDistanceBeforeResolving(action);
            chance = popup.CalculateActionClashChance(action);
            melee = popup.CurrentRoundMeleeClashResult;
            ranged = popup.CurrentRoundRangedClashResult;
        }
        finally
        {
            popup.CurrentRoundMeleeClashResult = savedMelee;
            popup.CurrentRoundRangedClashResult = savedRanged;
            encounter.CurrentPlayerAction = savedAction;
            UnityEngine.Random.state = random;
        }
        bool distant = encounter.Distant;
        switch (distanceChange)
        {
            case EncounterDistanceChange.AddDistance: distant = true; break;
            case EncounterDistanceChange.CloseDistance: distant = false; break;
        }
        bool rangedClash = distant
            ? !(action.ActionRange == ActionRange.Melee && encounter.CurrentEnemyAction.ActionRange == ActionRange.Melee)
            : action.ActionRange == ActionRange.Ranged && encounter.CurrentEnemyAction.ActionRange == ActionRange.Ranged;
        var common = rangedClash ? ranged.CommonClashReport : melee.CommonClashReport;
        StringBuilder summary = new();
        bool enemyCanRespond = action.ActionType == EncounterPlayerActionType.MainAction && !encounter.CurrentEnemyAction.DoesNotAttack;
        summary.AppendLine(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.Encounter.PlayerAction", "Player Action"), action.GeneratedActionName, indent: indent));
        if (!action.DontShowSuccessChance)
            summary.AppendLine(action.CannotFailClash
                ? LcStr("CSFFCardDetailTooltip.Encounter.CannotFail", "Action clash cannot fail")
                : FormatBasicEntry(LcStr("CSFFCardDetailTooltip.Encounter.EstimatedSuccess", "Native success estimate"), $"≈{chance:P0}", indent: indent));
        summary.AppendLine(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.Encounter.PowerComparison", "Power Comparison"),
            $"{(rangedClash ? ranged.PlayerClashValue : common.PlayerClashValue):0.##} : {(rangedClash ? ranged.EnemyClashValue : common.EnemyClashValue):0.##}", indent: indent));
        if (enemyCanRespond)
            summary.AppendLine(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.Encounter.EstimatedEnemyHit", "Native enemy hit estimate"),
                encounter.CurrentEnemyAction.CannotFailClash ? "100%" : $"≈{(rangedClash ? ranged.EnemySuccessChance : melee.EnemySuccessChance):P0}", indent: indent));
        summary.AppendLine(rangedClash ? ranged.PlayerSummary() : common.PlayerSummary(0, true));
        if (!action.DoesNotAttack)
        {
            var rolls = EncounterPlayerDamageReportPreview.PlayerDamageRolls(action, popup);
            Vector2 damage = rolls.Aggregate(Vector2.zero, (total, roll) => total + roll);
            summary.AppendLine(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.Encounter.DamagePower", "Damage Power"), FormatMinMaxValue(damage), indent: indent));
            summary.AppendLine(FormatPlayerHitResult(encounter, action, popup, damage));
        }
        summary.AppendLine(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.Encounter.DistanceChange", "Distance Change"), GetDistanceChangeText(distanceChange), indent: indent));
        summary.AppendLine(GenericEncounterPlayerActionPreview.FormatEffects(action, encounter, indent));
        if (enemyCanRespond)
        {
            string incoming = FormatEnemyHitResult(encounter, encounter.CurrentEnemyAction, popup, indent + 2, action, distant);
            if (!string.IsNullOrWhiteSpace(incoming))
                summary.AppendLine(LcStr("CSFFCardDetailTooltip.Encounter.IncomingAverage", "Incoming wounds at average damage/defense (if hit):") + "\n" + incoming);
        }
        return JoinTooltipLines(new[] { summary.ToString() });
    }

    public static EnemyActionSelectionReport GenEnemyActionSelection(InGameEncounter _FromEncounter,
        List<EnemyAction> _ActionsList)
    {
        EnemyActionSelectionReport result = default;
        if (!_FromEncounter || !_FromEncounter.EncounterModel || _ActionsList == null || _ActionsList.Count == 0)
        {
            result.Actions = Array.Empty<EnemyActionSelectionInfo>();
            return result;
        }

        int num = 0;
        result.Actions = new EnemyActionSelectionInfo[_ActionsList.Count];
        for (int i = 0; i < _ActionsList.Count; i++)
        {
            result.Actions[i] = default;
            result.Actions[i].ActionName = new LocalizedString
            { LocalizationKey = _ActionsList[i].ActionLog.MainLogKey };
            result.Actions[i].BaseWeight = _ActionsList[i].BaseWeight;
            result.Actions[i].DistanceWeightMod = _FromEncounter.Distant ? _ActionsList[i].DistanceWeightModifier : 0;
            result.Actions[i].CloseWeightMod = _FromEncounter.Distant ? 0 : _ActionsList[i].CloseRangeWeightModifier;
            result.Actions[i].EnemyHiddenWeightMod =
                _FromEncounter.EnemyHidden ? _ActionsList[i].EnemyHiddenWeightModifier : 0;
            result.Actions[i].PlayerHiddenWeightMod =
                _FromEncounter.PlayerHidden ? _ActionsList[i].PlayerHiddenWeightModifier : 0;
            result.Actions[i].StatWeightMods = new();
            _ActionsList[i].GetStatWeightMods(_FromEncounter.EncounterModel, result.Actions[i].StatWeightMods);
            result.Actions[i].CardWeightMods = new();
            _ActionsList[i].GetCardWeightMods(result.Actions[i].CardWeightMods);
            result.Actions[i].ValuesWeightMods =
                new EnemyValuesWeightModReport(_ActionsList[i].ValuesWeightModifiers, _FromEncounter);
            result.Actions[i].WoundsWeightMods =
                new EnemyWoundsWeightModReport(_ActionsList[i].WoundsWeightModifiers, _FromEncounter);
            if (result.Actions[i].FinalWeight <= 0)
            {
                result.Actions[i].RangeUpTo = -1;
            }
            else
            {
                num += result.Actions[i].FinalWeight;
                result.Actions[i].RangeUpTo = num;
            }
        }

        result.TotalWeight = num;
        return result;
    }

    public static string GetDistanceChangeText(EncounterDistanceChange distanceChange)
    {
        return distanceChange switch
        {
            EncounterDistanceChange.DontChangeDistance => $"{LcStr("CSFFCardDetailTooltip.Encounter.NoChange", "No Change")}",
            EncounterDistanceChange.AddDistance => $"{LcStr("CSFFCardDetailTooltip.Encounter.IncreaseDistance", "Increase Distance")}",
            EncounterDistanceChange.CloseDistance => $"{LcStr("CSFFCardDetailTooltip.Encounter.DecreaseDistance", "Decrease Distance")}",
            _ => ""
        };
    }

    public static string FormatPlayerHitResult(InGameEncounter encounter, GenericEncounterPlayerAction action,
        EncounterPopup popup, Vector2 playerActionDamage, int indent = 2)
    {
        bool flag = action.ActionRange == ActionRange.Melee;
        StringBuilder result = new();
        global::Encounter encounterModel = encounter.EncounterModel;
        EnemyBodyLocationSelectionReport resultReport = default;

        // 计算命中权重
        resultReport.Ranged = !flag;
        resultReport.Filter = action.WoundLocationFilter;
        resultReport.BaseWeights.Head = flag
            ? encounterModel.EnemyBodyTemplate.Head.MeleeHitChanceWeight
            : encounterModel.EnemyBodyTemplate.Head.RangedHitChanceWeight;
        resultReport.BaseWeights.Torso = flag
            ? encounterModel.EnemyBodyTemplate.Torso.MeleeHitChanceWeight
            : encounterModel.EnemyBodyTemplate.Torso.RangedHitChanceWeight;
        resultReport.BaseWeights.LArm = flag
            ? encounterModel.EnemyBodyTemplate.LArm.MeleeHitChanceWeight
            : encounterModel.EnemyBodyTemplate.LArm.RangedHitChanceWeight;
        resultReport.BaseWeights.RArm = flag
            ? encounterModel.EnemyBodyTemplate.RArm.MeleeHitChanceWeight
            : encounterModel.EnemyBodyTemplate.RArm.RangedHitChanceWeight;
        resultReport.BaseWeights.LLeg = flag
            ? encounterModel.EnemyBodyTemplate.LLeg.MeleeHitChanceWeight
            : encounterModel.EnemyBodyTemplate.LLeg.RangedHitChanceWeight;
        resultReport.BaseWeights.RLeg = flag
            ? encounterModel.EnemyBodyTemplate.RLeg.MeleeHitChanceWeight
            : encounterModel.EnemyBodyTemplate.RLeg.RangedHitChanceWeight;
        resultReport.ArmorWeights.Head = encounterModel.EnemyArmor.HeadHitProbabilityModifier;
        resultReport.ArmorWeights.Torso = encounterModel.EnemyArmor.TorsoHitProbabilityModifier;
        resultReport.ArmorWeights.LArm = encounterModel.EnemyArmor.LArmHitProbabilityModifier;
        resultReport.ArmorWeights.RArm = encounterModel.EnemyArmor.RArmHitProbabilityModifier;
        resultReport.ArmorWeights.LLeg = encounterModel.EnemyArmor.LLegHitProbabilityModifier;
        resultReport.ArmorWeights.RLeg = encounterModel.EnemyArmor.RLegHitProbabilityModifier;
        resultReport.TrackingWeights.Head = encounter.CurrentEnemyBodyProbabilities.CurrentHeadProbModifier;
        resultReport.TrackingWeights.Torso = encounter.CurrentEnemyBodyProbabilities.CurrentTorsoProbModifier;
        resultReport.TrackingWeights.LArm = encounter.CurrentEnemyBodyProbabilities.CurrentLArmProbModifier;
        resultReport.TrackingWeights.RArm = encounter.CurrentEnemyBodyProbabilities.CurrentRArmProbModifier;
        resultReport.TrackingWeights.LLeg = encounter.CurrentEnemyBodyProbabilities.CurrentLLegProbModifier;
        resultReport.TrackingWeights.RLeg = encounter.CurrentEnemyBodyProbabilities.CurrentRLegProbModifier;

        // 计算各个部位的基础护甲
        BodyLocations[] bodyParts = new[]
        {
            BodyLocations.Head, BodyLocations.Torso, BodyLocations.LArm, BodyLocations.RArm, BodyLocations.LLeg,
            BodyLocations.RLeg
        };
        float[] bodyPartArmors = new float[bodyParts.Length];
        BodyTemplate body = encounterModel.EnemyBodyTemplate;
        bodyPartArmors[(int)BodyLocations.Head] = body.Head.GetArmor(action.DamageTypes);
        bodyPartArmors[(int)BodyLocations.Torso] = body.Torso.GetArmor(action.DamageTypes);
        bodyPartArmors[(int)BodyLocations.LArm] = body.LArm.GetArmor(action.DamageTypes);
        bodyPartArmors[(int)BodyLocations.RArm] = body.RArm.GetArmor(action.DamageTypes);
        bodyPartArmors[(int)BodyLocations.LLeg] = body.LLeg.GetArmor(action.DamageTypes);
        bodyPartArmors[(int)BodyLocations.RLeg] = body.RLeg.GetArmor(action.DamageTypes);


        // 计算各个部位的实时防御力
        float[] bodyPartArmorDefenses = new float[bodyParts.Length];
        float[] trackingDefenses = new float[bodyParts.Length];
        foreach (BodyLocations part in bodyParts)
        {
            bodyPartArmorDefenses[(int)part] =
                encounterModel.EnemyArmor.CalculateArmorForLocation(action.DamageTypes, part);
            trackingDefenses[(int)part] = encounter.CurrentEnemyBodyProbabilities.GetDefenseModifierForLocation(part);
        }

        // 计算最终的Enemy Defense
        float sizeDefense = encounter.CurrentEnemySize;
        float[] enemyDefenses = new float[bodyParts.Length];
        for (int i = 0; i < bodyParts.Length; i++)
            enemyDefenses[i] = sizeDefense + bodyPartArmors[i] + bodyPartArmorDefenses[i] + trackingDefenses[i];

        var rolls = EncounterPlayerDamageReportPreview.PlayerDamageRolls(action, popup);
        double fatality = 0;
        string spaces = new(' ', indent);
        foreach (BodyLocations part in bodyParts)
        {
            // GenerateEnemyWound falls back to torso when every weight is zero.
            double hitChance = resultReport.TotalWeight > 0
                ? resultReport.GetBodyLocationHitWeight(part) / resultReport.TotalWeight
                : part == BodyLocations.Torso ? 1 : 0;
            if (hitChance <= 0) continue;
            var probabilities = EncounterPlayerDamageReportPreview.WoundProbabilities(rolls, enemyDefenses[(int)part], popup);
            result.AppendLine($"{spaces}{BodyTemplate.LocationName(part)}: {hitChance:P1} ({LcStr("CSFFCardDetailTooltip.Encounter.TotalDefense", "Total Defense")}: {enemyDefenses[(int)part]:0.##})");
            result.AppendLine(spaces + string.Join(" | ", probabilities.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value:P1}")));
            foreach (var probability in probabilities)
            {
                var wounds = body.GetBodyLocation(part).GetWoundsForSeverityDamageType(probability.Key, action.DamageTypes);
                if (wounds == null || wounds.Length == 0) wounds = new[] { body.DefaultWound };
                foreach (var wound in wounds)
                {
                    if (wound == null) continue;
                    var hit = probability.Key == WoundSeverity.NoWound ? EnemyActionEffectCondition.OnlyOnHit : EnemyActionEffectCondition.OnlyOnHitAndWound;
                    if (!wound.EnemyValuesModifiers.Applies(EnemyValueNames.Blood, hit)) continue;
                    Vector2 loss = -wound.EnemyValuesModifiers.BloodModifier;
                    float low = Mathf.Min(loss.x, loss.y), high = Mathf.Max(loss.x, loss.y);
                    double lethal = low == high ? (low >= encounter.CurrentEnemyBlood ? 1 : 0)
                        : Mathf.Clamp01((high - encounter.CurrentEnemyBlood) / (high - low));
                    fatality += hitChance * probability.Value * lethal / wounds.Length;
                }
            }
        }
        result.AppendLine($"{LcStr("CSFFCardDetailTooltip.DirectWoundFatality", "Direct wound fatality on hit")}: {fatality:P2}");
        return result.ToString();
    }
    public static string FormatEnemyHitResult(InGameEncounter encounter, EnemyAction action, EncounterPopup popup,
        int indent = 2, GenericEncounterPlayerAction playerAction = null, bool? distant = null)
    {
        StringBuilder result = new();
        GameManager gm = GameManager.Instance;
        PlayerBodyLocationSelectionReport playerBodyLocationHit = default;
        EncounterEnemyDamageReport currentRoundEnemyDamageReport = new();

        BodyLocations[] bodyParts = {
            BodyLocations.Head, BodyLocations.Torso, BodyLocations.LArm, BodyLocations.RArm, BodyLocations.LLeg,
            BodyLocations.RLeg
        };
        float[] bodyPartArmors = new float[bodyParts.Length];
        float[] armors = new float[bodyParts.Length];

        if (action.ActionRange == ActionRange.Melee)
        {
            playerBodyLocationHit.Ranged = false;
            playerBodyLocationHit.BaseWeights.Head = popup.Head.MeleeHitChanceWeight;
            playerBodyLocationHit.BaseWeights.Torso = popup.Torso.MeleeHitChanceWeight;
            playerBodyLocationHit.BaseWeights.LArm = popup.LArm.MeleeHitChanceWeight;
            playerBodyLocationHit.BaseWeights.RArm = popup.RArm.MeleeHitChanceWeight;
            playerBodyLocationHit.BaseWeights.LLeg = popup.LLeg.MeleeHitChanceWeight;
            playerBodyLocationHit.BaseWeights.RLeg = popup.RLeg.MeleeHitChanceWeight;
        }
        else
        {
            playerBodyLocationHit.BaseWeights.Head = popup.Head.RangedHitChanceWeight;
            playerBodyLocationHit.BaseWeights.Torso = popup.Torso.RangedHitChanceWeight;
            playerBodyLocationHit.BaseWeights.LArm = popup.LArm.RangedHitChanceWeight;
            playerBodyLocationHit.BaseWeights.RArm = popup.RArm.RangedHitChanceWeight;
            playerBodyLocationHit.BaseWeights.LLeg = popup.LLeg.RangedHitChanceWeight;
            playerBodyLocationHit.BaseWeights.RLeg = popup.RLeg.RangedHitChanceWeight;
            if ((distant ?? encounter.Distant) && gm && gm.CoverCards != null)
                for (int i = 0; i < gm.CoverCards.Count; i++)
                    if (!gm.CoverCards.get_Item(i).CardModel.AppliesCoverWhenEquipped ||
                        (gm.CoverCards.get_Item(i).CardModel.AppliesCoverWhenEquipped &&
                         GraphicsManager.Instance.CharacterWindow.HasCardEquipped(gm.CoverCards.get_Item(i))))
                    {
                        playerBodyLocationHit.CoverWeights.Head = gm.CoverCards.get_Item(i).CardModel
                            .PlayerCoverHitProbabilityModifiers.HeadHitProbabilityModifier;
                        playerBodyLocationHit.CoverWeights.Torso = gm.CoverCards.get_Item(i).CardModel
                            .PlayerCoverHitProbabilityModifiers.TorsoHitProbabilityModifier;
                        playerBodyLocationHit.CoverWeights.LArm = gm.CoverCards.get_Item(i).CardModel
                            .PlayerCoverHitProbabilityModifiers.LArmHitProbabilityModifier;
                        playerBodyLocationHit.CoverWeights.RArm = gm.CoverCards.get_Item(i).CardModel
                            .PlayerCoverHitProbabilityModifiers.RArmHitProbabilityModifier;
                        playerBodyLocationHit.CoverWeights.LLeg = gm.CoverCards.get_Item(i).CardModel
                            .PlayerCoverHitProbabilityModifiers.LLegHitProbabilityModifier;
                        playerBodyLocationHit.CoverWeights.RLeg = gm.CoverCards.get_Item(i).CardModel
                            .PlayerCoverHitProbabilityModifiers.RLegHitProbabilityModifier;
                    }
        }

        if (gm && gm.ArmorCards != null)
            foreach (var armorCard in gm.ArmorCards)
                if (GraphicsManager.Instance.CharacterWindow.HasCardEquipped(armorCard))
                {
                    AddArmor(armorCard);
                    if (armorCard.ContainedLiquid) AddArmor(armorCard.ContainedLiquid);
                }

        void AddArmor(InGameCardBase card)
        {
            var weights = ArmorValuesPreview.ArmorHitWeights(card);
            playerBodyLocationHit.ArmorWeights.Head += weights.Head;
            playerBodyLocationHit.ArmorWeights.Torso += weights.Torso;
            playerBodyLocationHit.ArmorWeights.LArm += weights.LArm;
            playerBodyLocationHit.ArmorWeights.RArm += weights.RArm;
            playerBodyLocationHit.ArmorWeights.LLeg += weights.LLeg;
            playerBodyLocationHit.ArmorWeights.RLeg += weights.RLeg;
            for (int k = 0; k < bodyParts.Length; k++)
                armors[k] += ArmorValuesPreview.ArmorDefense(card, action.DamageTypes, bodyParts[k]);
        }
        playerBodyLocationHit.EnemyActionWeights.Head +=
            action.AddedPlayerLocationHitProbabilities.HeadHitProbabilityModifier;
        playerBodyLocationHit.EnemyActionWeights.Torso +=
            action.AddedPlayerLocationHitProbabilities.TorsoHitProbabilityModifier;
        playerBodyLocationHit.EnemyActionWeights.LArm +=
            action.AddedPlayerLocationHitProbabilities.LArmHitProbabilityModifier;
        playerBodyLocationHit.EnemyActionWeights.RArm +=
            action.AddedPlayerLocationHitProbabilities.RArmHitProbabilityModifier;
        playerBodyLocationHit.EnemyActionWeights.LLeg +=
            action.AddedPlayerLocationHitProbabilities.LLegHitProbabilityModifier;
        playerBodyLocationHit.EnemyActionWeights.RLeg +=
            action.AddedPlayerLocationHitProbabilities.RLegHitProbabilityModifier;

        // 计算玩家护甲
        // Debug.Log(string.Join(",", action.DamageTypes.Select(t => t.Name)));
        foreach (BodyLocations bodyPart in bodyParts)
            bodyPartArmors[(int)bodyPart] = popup.GetBodyLocation(bodyPart).GetArmor(action.DamageTypes);

        // 计算玩家体型防御和状态防御加成
        float sizeDefense = popup.PlayerSize;
        Vector2 statsDefense = Vector2.zero;
        if (popup.PlayerExtraDefenseCalculation != null)
            for (int k = 0; k < popup.PlayerExtraDefenseCalculation.Length; k++)
                statsDefense += popup.PlayerExtraDefenseCalculation[k].GenerateRandomRange();
        currentRoundEnemyDamageReport.SizeDefense = sizeDefense;
        currentRoundEnemyDamageReport.StatsDefense = Mathf.Lerp(statsDefense.x, statsDefense.y, 0.5f);
        // 计算敌人行动伤害
        currentRoundEnemyDamageReport.SizeDamage =
            action.ActionRange == ActionRange.Melee ? encounter.CurrentEnemySize : 0f;
        currentRoundEnemyDamageReport.ActionDamage = Mathf.Lerp(action.Damage.x, action.Damage.y, 0.5f);
        currentRoundEnemyDamageReport.ValuesDamage = action.AddedDamageFromEnemyValues(encounter, false);
        currentRoundEnemyDamageReport.WoundsDamage = action.AddedDamageValueFromWounds(encounter, false);
        currentRoundEnemyDamageReport.StatsAddedDamage = action.AddedDamageFromStats(false);
        currentRoundEnemyDamageReport.WrestlingDamage = encounter.Wrestling ? action.WrestlingDamageModifier.RangeMidValue() : 0;
        currentRoundEnemyDamageReport.VsVulnerableDamage = encounter.PlayerVulnerable ? action.DmgVsVulnerableModifier.RangeMidValue() : 0;
        currentRoundEnemyDamageReport.VsEscapeDamage = playerAction != null && playerAction.IsEscapeAction
            ? action.AddedDamageVsEscape(false) : 0;

        foreach (BodyLocations bodyPart in bodyParts)
        {
            // Debug.Log(string.Join(",", armors));
            currentRoundEnemyDamageReport.ArmorDefense = armors[(int)bodyPart];
            currentRoundEnemyDamageReport.BodyPartArmor = bodyPartArmors[(int)bodyPart];
            WoundSeverity woundSeverity = popup.GenerateWoundSeverity(currentRoundEnemyDamageReport.EnemyDamage,
                currentRoundEnemyDamageReport.PlayerDefense);
            currentRoundEnemyDamageReport.AttackSeverity = woundSeverity;
            List<PlayerWound> wounds = new();
#if MELON_LOADER
            action.PlayerWounds.GetWoundsForSeverity_il2cpp(woundSeverity, wounds);
            if (wounds.Count == 0)
                encounter.EncounterModel.DefaultPlayerWounds.GetWoundsForSeverity_il2cpp(woundSeverity, wounds);
#else
            action.PlayerWounds.GetWoundsForSeverity(woundSeverity, ref wounds);
            if (wounds.Count == 0)
                encounter.EncounterModel.DefaultPlayerWounds.GetWoundsForSeverity(woundSeverity, ref wounds);
#endif
            var dropped = wounds.Where(w => w != null && w.DroppedCards != null).SelectMany(w => w.DroppedCards).Where(c => c).Distinct().ToArray();
            if (dropped.Length == 0) continue;
            if (playerBodyLocationHit.GetBodyLocationHitWeight(bodyPart) > 0)
                result.AppendLine(
                    $"{new string(' ', indent)}{new LocalizedString { LocalizationKey = $"CSFFCardDetailTooltip.BodyParts.{bodyPart}", DefaultText = bodyPart.ToString()}.ToString()}({playerBodyLocationHit.GetBodyLocationHitWeight(bodyPart) / playerBodyLocationHit.TotalWeight * 100f:0.#}%): {dropped.Select(c => c.CardName.ToString()).Join()} ({LcStr("CSFFCardDetailTooltip.Encounter.AttackDefenseRatio", "Attack-Defense Ratio")}: {currentRoundEnemyDamageReport.EnemyDamage}:{currentRoundEnemyDamageReport.PlayerDefense})");
        }

        return result.ToString();
    }

    public static BodyLocation GetBodyLocation(this EncounterPopup popup, BodyLocations bodyLocation)
    {
        return bodyLocation switch
        {
            BodyLocations.Head => popup.Head,
            BodyLocations.Torso => popup.Torso,
            BodyLocations.LArm => popup.LArm,
            BodyLocations.RArm => popup.RArm,
            BodyLocations.LLeg => popup.LLeg,
            BodyLocations.RLeg => popup.RLeg,
            _ => null
        };
    }

    public static float GetBodyLocationHitWeight(this PlayerBodyLocationSelectionReport report,
        BodyLocations bodyLocation)
    {
        return bodyLocation switch
        {
            BodyLocations.Head => report.HeadHitWeight,
            BodyLocations.Torso => report.TorsoHitWeight,
            BodyLocations.LArm => report.LArmHitWeight,
            BodyLocations.RArm => report.RArmHitWeight,
            BodyLocations.LLeg => report.LLegHitWeight,
            BodyLocations.RLeg => report.RLegHitWeight,
            _ => 0f
        };
    }

    public static float GetBodyLocationHitWeight(this EnemyBodyLocationSelectionReport report,
        BodyLocations bodyLocation)
    {
        return bodyLocation switch
        {
            BodyLocations.Head => report.HeadHitWeight,
            BodyLocations.Torso => report.TorsoHitWeight,
            BodyLocations.LArm => report.LArmHitWeight,
            BodyLocations.RArm => report.RArmHitWeight,
            BodyLocations.LLeg => report.LLegHitWeight,
            BodyLocations.RLeg => report.RLegHitWeight,
            _ => 0f
        };
    }

    public static string FormatCardOnCardAction(CardOnCardAction action, InGameCardBase recivingCard,
        InGameCardBase givenCard, int indent = 0)
    {
        List<string> texts = new();
        action = (CardOnCardAction)CardActionPreview.PreviewAction(action, recivingCard, givenCard);
        string cardActionText = FormatResolvedCardAction(action, recivingCard, indent, givenCard);
        if (!string.IsNullOrWhiteSpace(cardActionText)) texts.Add(cardActionText);
        CardStateChange stateChange = CardStateChangePreview.PreviewStateChange(action.GivenCardChanges, givenCard,
            recivingCard, action.DurabilitiesLiquidScale, false);
        string cardModText = FormatStateChange(stateChange, givenCard, indent);
        cardModText += FormatAddedDurabilities(action.GivenDurabilityChanges, givenCard, indent);
        if (!string.IsNullOrWhiteSpace(cardModText))
        {
            texts.Add(FormatBasicEntry(
                new LocalizedString
                {
                    LocalizationKey = "CSFFCardDetailTooltip.GivenCardStateChange",
                    DefaultText = "Given Card State Change"
                }, ""));
            texts.Add(cardModText);
        }

        LiquidDrop currentLiquidDrop = action.CreatedLiquidInGivenCard;
        if (currentLiquidDrop.LiquidCard)
        {
            string liquidDropText =
                $"{FormatMinMaxValue(currentLiquidDrop.Quantity)} ({currentLiquidDrop.LiquidCard.CardType}){currentLiquidDrop.LiquidCard.CardName.ToString()}";
            texts.Add(FormatBasicEntry(
                $"<size=55%>{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Action.LiquidDrops", DefaultText = "Liquid Drops" }.ToString()}</size>",
                "<size=55%>" + liquidDropText + "</size>", indent: indent));
        }

        return JoinTooltipLines(texts);
    }

    public static string FormatCardAction(CardAction action, InGameCardBase fromCard, int indent = 0,
        InGameCardBase givenCard = null)
    {
        return FormatResolvedCardAction(CardActionPreview.PreviewAction(action, fromCard, givenCard), fromCard, indent, givenCard);
    }

    private static string FormatResolvedCardAction(CardAction action, InGameCardBase fromCard, int indent,
        InGameCardBase givenCard)
    {
        List<string> texts = new();

        string timeModText = FormatTimeCostModifiers(action, fromCard, givenCard, indent);
        if (!timeModText.IsNullOrWhiteSpace())
        {
            texts.Add(FormatBasicEntry(new LocalizedString()
            {
                LocalizationKey = "CSFFCardDetailTooltip.TimeCostModifiers",
                DefaultText = "Time Cost Modifiers"
            }, "", indent: indent));
            texts.Add(timeModText);
        }

        string stateModText = StatModifierPreview.Format(action.AllStatModifiers, indent + 2);
        if (!string.IsNullOrWhiteSpace(stateModText))
        {
            texts.Add(FormatBasicEntry(
                new LocalizedString
                { LocalizationKey = "CSFFCardDetailTooltip.StatModifier", DefaultText = "Stat Modifier" }
                    .ToString(),
                "", indent: indent));
            texts.Add(stateModText);
        }

        string temporaryText = StatModifierPreview.Format(action.AllTemporaryStatModifiers, indent + 2);
        if (!string.IsNullOrWhiteSpace(temporaryText))
        {
            texts.Add(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.DuringAction", "During this action only"), "", indent: indent));
            texts.Add(temporaryText);
        }

        if (action.AllNPCStatModifiers != null)
            foreach (var modifier in action.AllNPCStatModifiers)
                if (modifier.TargetStat)
                    texts.Add(FormatBasicEntry(FormatMinMaxValue(modifier.ValueChange),
                        $"{(modifier.UseAssociatedAgent ? LcStr("CSFFCardDetailTooltip.AssociatedNPC", "Associated NPC") : modifier.TargetAgent ? modifier.TargetAgent.AgentName.ToString() : "NPC")}: {modifier.TargetStat.GameName}", indent: indent + 2));

        CardStateChange stateChange = CardStateChangePreview.PreviewStateChange(action.ReceivingCardChanges, fromCard,
            givenCard, action.DurabilitiesLiquidScale, !action.InstantDurabilityModifications);
        string cardModText = FormatStateChange(stateChange, fromCard, indent);
        if (!action.InstantDurabilityModifications || stateChange.ModType != CardModifications.Destroy)
            cardModText += FormatAddedDurabilities(action.ReceivingDurabilityChanges, fromCard, indent);
        if (!string.IsNullOrWhiteSpace(cardModText))
        {
            texts.Add(FormatBasicEntry(
                new LocalizedString
                {
                    LocalizationKey = "CSFFCardDetailTooltip.CardStateChange",
                    DefaultText = "Card State Change"
                }
                    .ToString(),
                "", indent: indent));
            texts.Add(cardModText);
        }

        return JoinTooltipLines(texts);
    }

    private static string FormatAddedDurabilities(TransferedDurabilities added, InGameCardBase card, int indent)
    {
        if (!card || added == null || added.IsEmpty) return string.Empty;
        CardStateChange change = new() { ModType = CardModifications.DurabilityChanges };
        change.ApplyDurabilityChanges(added);
        string changes = FormatStateChange(change, card, indent);
        if (string.IsNullOrWhiteSpace(changes)) return string.Empty;
        return "\n" + FormatBasicEntry(LcStr("CSFFCardDetailTooltip.AdditionalDurabilityChanges", "Additional durability changes"), "", indent: indent)
               + "\n" + changes;
    }

    private static string FormatTimeCostModifiers(CardAction action, InGameCardBase _ReceivingCard,
        InGameCardBase givenCard, int indent)
    {
        if (action == null) return string.Empty;

        GameManager gm = MBSingleton<GameManager>.Instance;
        List<ActionModifier> modifiers = new();
        bool notInBase = gm.NotInBase;

        void AddApplicable(IEnumerable<ActionModifier> source, InGameCardBase sourceCard)
        {
            if (source == null) return;
            foreach (ActionModifier mod in source)
            {
                if (mod != null && mod.DurationModifier != 0 &&
                    mod.AppliesToAction(action, notInBase, sourceCard, givenCard, _DoneByNPC: null))
                    modifiers.Add(mod);
            }
        }

        AddApplicable(gm.CurrentActionModifiers, _ReceivingCard);
        AddApplicable(action.BpActionModifiers, _ReceivingCard);

        if ((bool)_ReceivingCard && (bool)_ReceivingCard.CardModel)
        {
            AddApplicable(_ReceivingCard.CardModel.ActionModifiers, _ReceivingCard);

            CardTag[] cardTags = _ReceivingCard.CardModel.CardTags;
            if (cardTags != null && cardTags.Length != 0)
            {
                foreach (CardTag tags in cardTags)
                {
                    if (!tags || tags.ActionModifiers == null || tags.ActionModifiers.Length == 0) continue;
                    AddApplicable(tags.ActionModifiers, _ReceivingCard);
                }
            }
        }

        if (givenCard && givenCard.CardModel)
        {
            // Match CardAction.CollectActionModifiers: direct given-card modifiers use
            // that card as the source; its tag modifiers use the receiving card.
            AddApplicable(givenCard.CardModel.ActionModifiers, givenCard);
            if (givenCard.CardModel.CardTags != null)
                foreach (CardTag tag in givenCard.CardModel.CardTags)
                    if (tag) AddApplicable(tag.ActionModifiers, _ReceivingCard);
        }

        if (modifiers.Count == 0) return string.Empty;

        LocalizedString defaultModifySource = new() { LocalizationKey = "CSFFCardDetailTooltip.NoNameModifier", DefaultText = "Unknown Modifier" };
        List<string> texts = new();
        foreach (ActionModifier mod in modifiers)
        {
            string label = string.IsNullOrWhiteSpace(mod.ActionAddedSuffix) ? defaultModifySource.ToString() : mod.ActionAddedSuffix;
            texts.Add(FormatBasicEntry($"{ColorFloat(mod.DurationModifier)}", label, indent: indent + 2));
        }

        texts.Add(FormatBasicEntry(LcStr("CSFFCardDetailTooltip.FinalDuration", "Final duration"),
            HoursDisplay.HoursToCompleteString(GameManager.TickToHours(action.TotalDaytimeCost, action.MiniTicksCost)), indent: indent + 2));

        return JoinTooltipLines(texts);
    }

    private static string FormatStateChange(CardStateChange stateChange, InGameCardBase fromCard, int indent = 0)
    {
        if (!fromCard || !fromCard.CardModel) return string.Empty;
        List<string> cardModTexts = new();
        if (stateChange.ModType == CardModifications.DurabilityChanges)
        {
            if (stateChange.SpoilageChange.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.SpoilageChange),
                    string.IsNullOrEmpty(fromCard.CardModel.SpoilageTime.CardStatName)
                        ? new LocalizedString
                        { LocalizationKey = "CSFFCardDetailTooltip.Spoilage", DefaultText = "Spoilage" }
                            .ToString()
                        : fromCard.CardModel.SpoilageTime.CardStatName, indent: indent + 2));
            if (stateChange.UsageChange.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.UsageChange),
                    string.IsNullOrEmpty(fromCard.CardModel.UsageDurability.CardStatName)
                        ? new LocalizedString
                        { LocalizationKey = "CSFFCardDetailTooltip.Usage", DefaultText = "Usage" }.ToString()
                        : fromCard.CardModel.UsageDurability.CardStatName, indent: indent + 2));
            if (stateChange.FuelChange.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.FuelChange),
                    string.IsNullOrEmpty(fromCard.CardModel.FuelCapacity.CardStatName)
                        ? new LocalizedString
                        { LocalizationKey = "CSFFCardDetailTooltip.Fuel", DefaultText = "Fuel" }.ToString()
                        : fromCard.CardModel.FuelCapacity.CardStatName, indent: indent + 2));
            if (stateChange.ChargesChange.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.ChargesChange),
                    string.IsNullOrEmpty(fromCard.CardModel.Progress.CardStatName)
                        ? new LocalizedString
                        { LocalizationKey = "CSFFCardDetailTooltip.Progress", DefaultText = "Progress" }
                            .ToString()
                        : fromCard.CardModel.Progress.CardStatName, indent: indent + 2));
            if (stateChange.LiquidQuantityChange.magnitude != 0 && (stateChange.ModifyLiquid || fromCard.IsLiquid))
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.LiquidQuantityChange),
                    new LocalizedString
                    {
                        LocalizationKey = "CSFFCardDetailTooltip.LiquidQuantityChange",
                        DefaultText = "Liquid Quantity"
                    }.ToString(), indent: indent + 2));
            if (stateChange.Special1Change.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.Special1Change),
                    string.IsNullOrEmpty(fromCard.CardModel.SpecialDurability1.CardStatName)
                        ? "SpecialDurability1"
                        : fromCard.CardModel.SpecialDurability1.CardStatName, indent: indent + 2));
            if (stateChange.Special2Change.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.Special2Change),
                    string.IsNullOrEmpty(fromCard.CardModel.SpecialDurability2.CardStatName)
                        ? "SpecialDurability2"
                        : fromCard.CardModel.SpecialDurability2.CardStatName, indent: indent + 2));
            if (stateChange.Special3Change.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.Special3Change),
                    string.IsNullOrEmpty(fromCard.CardModel.SpecialDurability3.CardStatName)
                        ? "SpecialDurability3"
                        : fromCard.CardModel.SpecialDurability3.CardStatName, indent: indent + 2));
            if (stateChange.Special4Change.magnitude != 0)
                cardModTexts.Add(FormatBasicEntry(FormatMinMaxValue(stateChange.Special4Change),
                    string.IsNullOrEmpty(fromCard.CardModel.SpecialDurability4.CardStatName)
                        ? "SpecialDurability4"
                        : fromCard.CardModel.SpecialDurability4.CardStatName, indent: indent + 2));
        }
        else if (stateChange.ModType == CardModifications.Transform && stateChange.TransformInto)
        {
            cardModTexts.Add(FormatBasicEntry(
                new LocalizedString
                { LocalizationKey = "CSFFCardDetailTooltip.TransformInto", DefaultText = "Transform into" }
                    .ToString(),
                $"{stateChange.TransformInto.CardName}", indent: indent + 2));
        }
        else if (stateChange.ModType == CardModifications.Destroy)
        {
            cardModTexts.Add(FormatBasicEntry(
                new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Destroy", DefaultText = "Destroy" }
                    .ToString(),
                fromCard.CardModel.CardName.ToString(), "red", indent + 2));
        }

        if (fromCard && fromCard.ContainedLiquid && stateChange.ModifyLiquid)
        {
            cardModTexts.Add(FormatBasicEntry(
                new LocalizedString
                { LocalizationKey = "CSFFCardDetailTooltip.ModifyLiquid", DefaultText = "Modify Liquid" }
                    .ToString(), "", indent: indent + 2));
            cardModTexts.Add(FormatBasicEntry(
                FormatMinMaxValue(stateChange.LiquidQuantityChange), fromCard.ContainedLiquidModel.CardName, indent: indent + 4));
        }

        return JoinTooltipLines(cardModTexts);
    }
    public static string FormatActionDurationModifiers(ActionModifier modifier, int indent = 0)
    {
        List<string> texts = [];
        if (modifier == null || modifier.AppliesTo == null || modifier.DurationModifier == 0) return "";
        foreach (var tag in modifier.AppliesTo)
        {
            if (tag) texts.Add(FormatBasicEntry(tag.name, ColorFloat(modifier.DurationModifier), indent: indent));
        }
        return JoinTooltipLines(texts);
    }
    public static string FormatStatModifier(StatModifier statModifier, int indent = 0)
    {
        return StatModifierPreview.Format(new[] { statModifier }, indent);
    }

    public static string FormatMinMaxValue(Vector2 minMax)
    {
        if (Mathf.Approximately(minMax.x, minMax.y)) return $"{ColorFloat(minMax.x)}";
        return $"[{ColorFloat(minMax.x)}, {ColorFloat(minMax.y)}]";
    }

    public static string ColorTagFromFloat(float num)
    {
        return num switch
        {
            > 0f => "<color=\"green\">",
            < 0f => "<color=\"red\">",
            _ => "<color=\"yellow\">"
        };
    }

    public static string ColorFloat(float num, bool asPercent = false, bool reverseColor = false, bool isMultiply = false)
    {
        string suffix = isMultiply ? "x" : string.Empty;
        return asPercent
            ? $"{ColorTagFromFloat(reverseColor ? -num : num)}{num,-3:+0.##%;-0.##%;+0}{suffix}</color>"
            : $"{ColorTagFromFloat(reverseColor ? -num : num)}{num,-3:+0.##;-0.##;+0}{suffix}</color>";
    }

    public static string FormatWeight(float weight)
    {
        return
            $"<color=\"yellow\">{weight:0.#}</color> {new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.FormatWeight.Weight", DefaultText = "Weight" }.ToString()}";
    }

    public static string FormatProgressAndRate(float current, float max, string name, float rate,
        InGameCardBase currentCard = null, DurabilityStat stat = null, int indent = 0)
    {
        return JoinTooltipLines(new[] { FormatProgress(current, max, name, indent),
            FormatRate(rate, current, max, currentCard: currentCard, stat: stat) });
    }

    public static string FormatProgress(float current, float max, string name, int indent = 0)
    {
        return $"{new string(' ', indent)}<color=\"yellow\">{current:0.##}/{max:0.##}</color> {name}";
    }

    public static string FormatWeaponStats(Vector2 clash, Vector2 damage, float reach, int indent = 0, bool attacks = true)
    {
        LocalizedString title = new()
        { LocalizationKey = "CSFFCardDetailTooltip.WeaponStats", DefaultText = "Weapon Stats" };
        LocalizedString clashTitle = new()
        { LocalizationKey = "CSFFCardDetailTooltip.WeaponStats.Clash", DefaultText = "Clash" };
        LocalizedString damageTitle = new()
        { LocalizationKey = "CSFFCardDetailTooltip.WeaponStats.Damage", DefaultText = "Damage" };
        LocalizedString reachTitle = new()
        { LocalizationKey = "CSFFCardDetailTooltip.WeaponStats.Reach", DefaultText = "Reach" };

        return $"{FormatBasicEntry(title, "", indent: indent)}\n" +
               $"<size=75%>{FormatBasicEntry(FormatMinMaxValue(clash), clashTitle, indent: indent + 2)}\n" +
               (attacks ? $"{FormatBasicEntry(FormatMinMaxValue(damage), damageTitle, indent: indent + 2)}\n"
                   : $"{FormatBasicEntry(LcStr("CSFFCardDetailTooltip.NoAttack", "Does not deal attack damage"), "", indent: indent + 2)}\n") +
               $"{FormatBasicEntry(ColorFloat(reach), reachTitle, indent: indent + 2)}" +
               $"</size>";
    }

    public static string FormatWeaponStats(InGameCardBase card)
    {
        if (!EncounterPopup.Instance)
            return FormatWeaponStats(card.CardModel.BaseClashValue, card.CardModel.WeaponDamage, card.CardModel.WeaponReach);
        List<string> texts = new();
        var moves = card.CardModel.WeaponMoves;
        var random = UnityEngine.Random.state;
        try
        {
            foreach (var move in moves != null && moves.Length > 0 ? moves : new WeaponMove[] { null })
            {
                GenericEncounterPlayerAction preview = new();
                preview.InitializeWithCardAndAction(card, move, "", new List<PlayerValuesModifier>());
                Vector2 clash = preview.InitialClashValue;
                if (preview.ClashStatsAddedValues != null)
                    foreach (var modifier in preview.ClashStatsAddedValues) clash += modifier.Value;
                texts.Add(FormatBasicEntry(move ? move.ActionName.ToString() : card.CardModel.CardName.ToString(), ""));
                texts.Add(FormatWeaponStats(clash, preview.InitialDamage + preview.DamageStatSum, preview.Reach, 2, !preview.DoesNotAttack));
                if (preview.NeedsAmmo)
                    texts.Add(LcStr("CSFFCardDetailTooltip.WithoutAmmo", "Before ammunition bonuses"));
            }
        }
        finally { UnityEngine.Random.state = random; }
        texts.Add(LcStr("CSFFCardDetailTooltip.BeforeTargetBonuses", "Current condition and skills; before target and encounter bonuses"));
        return string.Join("\n", texts);
    }
    public static string FormatWeaponStats(CardData card, int indent = 2)
    {
        List<string> texts = new();
        if (card.IsWeapon)
        {
            texts.Add(FormatBasicEntry(FormatMinMaxValue(card.BaseClashValue),
                LcStr("CSFFCardDetailTooltip.WeaponStats.Clash", "Clash")));

            texts.Add(FormatBasicEntry(FormatMinMaxValue(card.WeaponDamage),
                LcStr("CSFFCardDetailTooltip.WeaponStats.WeaponDamage", "Weapon Damage")));

            texts.Add(FormatBasicEntry(ColorFloat(card.WeaponReach),
                LcStr("CSFFCardDetailTooltip.WeaponStats.WeaponReach", "Weapon Reach")));

            texts.Add(FormatBasicEntry(FormatMinMaxValue(card.ClashIneffectiveRangeMalus),
                LcStr("CSFFCardDetailTooltip.WeaponStats.ClashIneffectiveRangeMalus", "Clash Ineffective Range Malus")));

            texts.Add(FormatBasicEntry(FormatMinMaxValue(card.ClashVsEscapeBonus),
                LcStr("CSFFCardDetailTooltip.WeaponStats.ClashVsEscapeBonus", "Clash Vs Escape Bonus")));

            texts.Add(FormatBasicEntry(FormatMinMaxValue(card.DmgVsEscapeBonus),
                LcStr("CSFFCardDetailTooltip.WeaponStats.DmgVsEscapeBonus", "Damage Vs Escape Bonus")));

            texts.Add(FormatBasicEntry(FormatMinMaxValue(card.ClashStealthBonus),
                LcStr("CSFFCardDetailTooltip.WeaponStats.ClashStealthBonus", "Clash Stealth Bonus")));
        }

        if (card.IsCover)
        {
            texts.Add(FormatBasicEntry(ColorFloat(card.PlayerAddedCover),
                LcStr("CSFFCardDetailTooltip.ArmorStats.PlayerAddedCover", "Player Added Cover")));

            texts.Add(FormatBasicEntry(ColorFloat(card.EnemyAddedCover),
                LcStr("CSFFCardDetailTooltip.ArmorStats.EnemyAddedCover", "Enemy Added Cover")));

            texts.Add(FormatBasicEntry(ColorFloat(card.PlayerAddedStealth),
                LcStr("CSFFCardDetailTooltip.ArmorStats.PlayerAddedStealth", "Player Added Stealth")));

            texts.Add(FormatBasicEntry(ColorFloat(card.EnemyAddedStealth),
                LcStr("CSFFCardDetailTooltip.ArmorStats.EnemyAddedStealth", "Enemy Added Stealth")));
        }

        LocalizedString title = new()
        { LocalizationKey = "CSFFCardDetailTooltip.WeaponStats", DefaultText = "Weapon Stats" };

        return $"{FormatBasicEntry(title, "", indent: indent)}\n" +
               $"<size=75%>{JoinTooltipLines(texts)}</size>";
    }

    public static string TimeSpanFormat(TimeSpan ts)
    {
        return ts.Days >= 1
            ? $"{ts.Days:0}{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.d", DefaultText = "d" }.ToString()}{ts.Hours:0}{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.h", DefaultText = "h" }.ToString()}"
            : $"{ts.Hours:0}{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.h", DefaultText = "h" }.ToString()}";
    }

    public static string FormatRate(float value, float current, float max, float min = 0,
        InGameCardBase currentCard = null, DurabilityStat stat = null)
    {
        string est = "";
        string statOnFullZeroText = "";
        string dropList = "";
        string statOnFullZeroTitle = "";
        if (value > 0 && current < max)
        {
            float time = Math.Abs((max - current) / value);
            TimeSpan timeSpan = new(0, (int)(Math.Ceiling(time) * 15), 0);
            est =
                $" ({new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.est.", DefaultText = "est." }.ToString()} {Math.Ceiling(time)}t/{TimeSpanFormat(timeSpan)})";
            if (stat != null && currentCard != null && stat.HasActionOnFull && stat.OnFull != null)
            {
                statOnFullZeroTitle = FormatBasicEntry(new LocalizedString
                { LocalizationKey = "CSFFCardDetailTooltip.statOnFullTitle", DefaultText = "On Full" }
                    .ToString(), "", indent: 4);
                CollectionDropReport collectionDropsReport =
                    CollectionDropReportPreview.Create(stat.OnFull, currentCard, null, InGameNPCOrPlayer.PlayerAgent, false);
                dropList = Action.FormatCardDropList(
                    collectionDropsReport, currentCard,
                    action: stat.OnFull, indent: 6);
                statOnFullZeroText = FormatCardAction(stat.OnFull, currentCard, 6);
            }
        }
        else if (value < 0 && current > min)
        {
            float time = Math.Abs((current - min) / value);
            TimeSpan timeSpan = new(0, (int)(Math.Ceiling(time) * 15), 0);
            est =
                $" ({new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.est.", DefaultText = "est." }.ToString()} {Math.Ceiling(time)}t/{TimeSpanFormat(timeSpan)})";
            if (stat != null && currentCard != null && stat.HasActionOnZero && stat.OnZero != null)
            {
                statOnFullZeroTitle = FormatBasicEntry(new LocalizedString
                { LocalizationKey = "CSFFCardDetailTooltip.statOnZeroTitle", DefaultText = "On Zero" }
                    .ToString(), "", indent: 4);
                bool uniqueOnBoard = currentCard.CardModel.UniqueOnBoard;
                if (currentCard.CardModel.CardType == CardTypes.Weather) currentCard.CardModel.UniqueOnBoard = false;
                CollectionDropReport collectionDropsReport =
                    CollectionDropReportPreview.Create(stat.OnZero, currentCard, null, InGameNPCOrPlayer.PlayerAgent, false);
                currentCard.CardModel.UniqueOnBoard = uniqueOnBoard;
                dropList = Action.FormatCardDropList(
                    collectionDropsReport, currentCard,
                    action: stat.OnZero, indent: 6);
                statOnFullZeroText = FormatCardAction(stat.OnZero, currentCard, 6);
            }
        }

        List<string> texts = new()
        {
            FormatTooltipEntry(value,
                $"{new LocalizedString { LocalizationKey = "CSFFCardDetailTooltip.Rate", DefaultText = "Rate" }.ToString()}<size=70%>{est}</size>",
                2)
        };
        if (!string.IsNullOrWhiteSpace(statOnFullZeroTitle)) texts.Add(statOnFullZeroTitle);
        if (!string.IsNullOrWhiteSpace(dropList)) texts.Add(dropList);
        if (!string.IsNullOrWhiteSpace(statOnFullZeroText)) texts.Add(statOnFullZeroText);
        return JoinTooltipLines(texts);
    }

    public static string FormatRateEntry(float value, string name, bool isMultiply = false)
    {
        return FormatTooltipEntry(value, name, 4, isMultiply);
    }

    public static string FormatTooltipEntry(float value, string name, int indent = 0, bool isMultiply = false)
    {
        return $"<indent={indent / 2.2:0.##}em>{ColorFloat(value, isMultiply: isMultiply)} {name}</indent>";
    }

    public static string FormatTooltipEntry(OptionalFloatValue value, string name, int indent = 0)
    {
        return !value ? null : FormatTooltipEntry(value.FloatValue, name, indent);
    }

    public static bool GetEffectMultiply(object effect, string memberName, bool defaultValue = false)
    {
        if (effect == null) return defaultValue;
        Type t = effect.GetType();
        try
        {
            var field = AccessTools.Field(t, memberName);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(effect);

            var prop = AccessTools.Property(t, memberName);
            if (prop != null && prop.CanRead && prop.PropertyType == typeof(bool))
                return (bool)prop.GetValue(effect, null);
        }
        catch
        {
            // ignore
        }
        return defaultValue;
    }

    public static string FormatBasicEntry(string s1, string s2, string s1Color = "yellow", int indent = 0)
    {
        return $"<indent={indent / 2.2:0.##}em><color=\"{s1Color}\">{s1}</color> {s2}</indent>";
    }
}
