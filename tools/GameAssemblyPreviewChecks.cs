// Run Commands.Run() as a coroutine inside a game with a copied save and the candidate plugin.
// This is a diagnostic assembly, not a BepInEx plugin and not part of the release package.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using CSFFCardDetailTooltip;
using HarmonyLib;
using UnityEngine;

public static class Commands
{
    static int passed;
    static string LogPath => Path.Combine(Paths.BepInExRootPath, "GameAssemblyPreviewChecks.log");
    static void Log(string text) => File.AppendAllText(LogPath, text + "\n");
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL " + name);
        passed++;
        Log("PASS " + name);
    }
    static void Equal(double actual, double expected, string name) => Check(Math.Abs(actual - expected) < 0.0001, name + $" ({actual:0.####})");
    static string RandomState() => JsonUtility.ToJson(UnityEngine.Random.state);

    public static IEnumerator Run()
    {
        File.WriteAllText(LogPath, "Game " + Application.version + "\n");
        passed = 0;
        try { RunChecks(); Log("DONE " + passed + " checks passed"); }
        catch (Exception ex) { Log(ex.ToString()); }
        yield break;
    }

    static void CheckCookingAndDrops(InGameCardBase knife, CardData[] assets)
    {
        var root = new GameObject("Tooltip CookingRecipe fixture");
        root.SetActive(false);
        var cooker = root.AddComponent<InGameCardBase>();
        var vesselObject = new GameObject("Tooltip liquid vessel fixture");
        vesselObject.SetActive(false);
        var vessel = vesselObject.AddComponent<InGameCardBase>();
        var ingredientObject = new GameObject("Tooltip ingredient fixture");
        ingredientObject.SetActive(false);
        var ingredient = ingredientObject.AddComponent<InGameCardBase>();
        var model = ScriptableObject.CreateInstance<CardData>();
        model.name = "TooltipTestCooker";
        model.CardType = CardTypes.Location;
        model.MaxWeightCapacity = 100;
        try
        {
            AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(cooker, model, null);
            AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(vessel, knife.CardModel, null);
            AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(ingredient, knife.CardModel, null);
            AccessTools.Property(typeof(InGameCardBase), "CurrentContainer").SetValue(ingredient, vessel, null);
            AccessTools.Property(typeof(InGameCardBase), "CurrentContainer").SetValue(vessel, cooker, null);
            var status = (CookingCardStatus)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CookingCardStatus));
            status.Card = ingredient;
            cooker.CookingCards.Add(status);
            var recipe = new CookingRecipe { CompatibleCards = new[] { knife.CardModel },
                IngredientChanges = new CardStateChange { ModType = CardModifications.DurabilityChanges, UsageChange = Vector2.one * -4 } };
            model.CookingRecipes = new[] { recipe };
            Check(CookingRecipePreview.ActiveIngredientRecipe(ingredient) == recipe, "nested ingredient resolves registered cooking station");
            Check(CookingRecipePreview.ActiveIngredientRecipe(vessel) == null, "liquid ingredient rate is not applied to its vessel");
            recipe.Conditions = new GeneralCondition { RequiredContainer = new[] { model } };
            Check(model.GetRecipeForCard(ingredient, cooker) == recipe, "game lookup returns failing-condition fallback recipe");
            Check(CookingRecipePreview.ActiveIngredientRecipe(ingredient) == null, "paused fallback recipe contributes no rate");
            recipe.Conditions = default;
            ingredient.IgnoreTickDurabilityChanges = true;
            Check(CookingRecipePreview.ActiveIngredientRecipe(ingredient) == null, "busy ingredient contributes no cooking rate");
            ingredient.IgnoreTickDurabilityChanges = false;
            AccessTools.Property(typeof(InGameCardBase), "IsPinned").SetValue(cooker, true, null);
            Check(CookingRecipePreview.ActiveIngredientRecipe(ingredient) == null, "pinned cooker contributes no rate");
            AccessTools.Property(typeof(InGameCardBase), "IsPinned").SetValue(cooker, false, null);
            cooker.CookingCards.Clear();
            Check(CookingRecipePreview.ActiveIngredientRecipe(ingredient) == null, "unregistered ingredient contributes no rate");

            var mill = assets.First(c => c.name == "ClickMill");
            Check(mill.UseContainerForEffectConditions && mill.EffectsToInventoryContent.Any(e => e.EffectName == "Mill"),
                "ClickMill uses inventory passive effects with cooker conditions");
            AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(cooker, mill, null);
            var millEffect = mill.EffectsToInventoryContent.First(e => e.EffectName == "Mill").Instantiate(cooker, null);
            foreach (float progress in new[] { 0f, 2f, 4f })
            {
                cooker.CurrentProgress = progress;
                Check(millEffect.ConditionsValid(vessel, null, false) == (progress == 2),
                    "ClickMill effect checks cooker progress " + progress);
            }
            // Compare stacked/scaled contributions to repeated calls to the game's application method.
            var effect = new PassiveEffect { EffectStacksWithRequiredCards = true,
                Special3RateModifier = new OptionalFloatValue(true, 2),
                EffectScalesWithDurabilities = new InterpolatedDurabilityScaling { Active = true } };
            object boxedEffect = effect;
            AccessTools.Property(typeof(PassiveEffect), "CurrentStack").SetValue(boxedEffect, 3, null);
            AccessTools.Property(typeof(PassiveEffect), "CurrentScaling").SetValue(boxedEffect, 1.5f, null);
            effect = (PassiveEffect)boxedEffect;
            AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(ingredient,
                assets.First(c => c.SpecialDurability3), null);
            var baseRate = AccessTools.Field(typeof(InGameCardBase), "BaseSpecial3Rate");
            var rateMultiplier = AccessTools.Field(typeof(InGameCardBase), "Special3RateMultiplier");
            foreach (bool multiply in new[] { false, true })
            {
                baseRate.SetValue(ingredient, 0f);
                rateMultiplier.SetValue(ingredient, 1f);
                effect.MultiplySpecial3Rate = multiply;
                for (int i = 0; i < 3; i++)
                    AccessTools.Method(typeof(InGameCardBase), "ApplyPassiveEffectDurabilities").Invoke(ingredient, new object[] { effect });
                Equal(PassiveEffectPreview.RateContribution(effect, 2, multiply),
                    (float)(multiply ? rateMultiplier : baseRate).GetValue(ingredient),
                    "scaled and stacked passive contribution agrees with game multiply=" + multiply);
            }
            foreach (var stat in Resources.FindObjectsOfTypeAll<GameStat>().Where(s =>
                System.Text.RegularExpressions.Regex.IsMatch(s.name, "mould|mold|lungrot|arousal|winter", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
                Log("RELEASE STAT " + stat.name + " / " + stat.GameName);

            int drops = 0;
            foreach (var card in UnityEngine.Object.FindObjectsOfType<InGameCardBase>().Where(c => c.CardModel && !c.IsPinned))
                foreach (var action in card.DismantleActions ?? Array.Empty<DismantleCardAction>())
                {
                    if (!(action.ProducedCards?.Length > 0)) continue;
                    string rng = RandomState(), before = JsonUtility.ToJson(action);
                    var caches = action.ProducedCards.Select(c => c.CurrentDrop).ToArray();
                    var counts = caches.Select(c => c?.Count ?? -1).ToArray();
                    string destination = JsonUtility.ToJson(GameManager.Instance.NextEnvironment);
                    var report = CollectionDropReportPreview.Create(action, card, null, InGameNPCOrPlayer.PlayerAgent);
                    Check(rng == RandomState() && before == JsonUtility.ToJson(action)
                        && destination == JsonUtility.ToJson(GameManager.Instance.NextEnvironment)
                        && caches.Select((cache, i) => ReferenceEquals(cache, action.ProducedCards[i].CurrentDrop)
                            && (cache?.Count ?? -1) == counts[i]).All(v => v), "isolated drop preview " + card.CardModel.name + "/" + action.ActionName);
                    Check(!ReferenceEquals(report.FromAction, action) && report.DropsInfo.Length == action.ProducedCards.Length,
                        "drop report owns preview collections " + card.CardModel.name);
                    drops++;
                }
            Check(drops > 0, "real drop collections exercised");

            var travel = new CardAction { ProducedCards = Array.Empty<CardsDropCollection>(),
                ExplicitTravelToEnv = new EnvID(assets.First(c => c.CardType == CardTypes.Environment && !c.InstancedEnvironment)) };
            string next = JsonUtility.ToJson(GameManager.Instance.NextEnvironment);
            CollectionDropReportPreview.Create(travel, null, null, InGameNPCOrPlayer.PlayerAgent);
            Check(next == JsonUtility.ToJson(GameManager.Instance.NextEnvironment), "travel preview restores next environment");
            Log("RELEASE ASSETS " + string.Join(", ", assets.Where(c => c.name.IndexOf("mill", StringComparison.OrdinalIgnoreCase) >= 0
                || c.name.IndexOf("mould", StringComparison.OrdinalIgnoreCase) >= 0).Select(c => c.name)));
        }
        finally
        {
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(vesselObject);
            UnityEngine.Object.Destroy(ingredientObject);
            UnityEngine.Object.Destroy(model);
        }
    }

    static void RunChecks()
    {
        var triangle = new[] { new Vector2(0, 1), new Vector2(0, 1) };
        Equal(EncounterPlayerDamageReportPreview.ProbabilityBelow(triangle, 0.5), 0.125, "independent rolls lower tail");
        Equal(EncounterPlayerDamageReportPreview.ProbabilityBelow(triangle, 1), 0.5, "independent rolls midpoint");
        Equal(EncounterPlayerDamageReportPreview.ProbabilityBelow(triangle, 1.5), 0.875, "independent rolls upper tail");
        Equal(EncounterPlayerDamageReportPreview.ProbabilityBelow(new[] { new Vector2(3, 1) }, 2), 0.5, "reversed range");
        Equal(EncounterPlayerDamageReportPreview.ProbabilityBelow(new[] { Vector2.one * 3 }, 3), 0, "constant at boundary");
        var recipe = new CookingRecipe { IngredientChanges = new CardStateChange { UsageChange = new Vector2(-6, -2) } };
        AccessTools.Field(typeof(CookingRecipe), "Duration").SetValue(recipe, 4);
        AccessTools.Field(typeof(CookingRecipe), "DurationRdmVariation").SetValue(recipe, 0);
        Equal(CookingRecipePreview.AverageRecipeChanges(recipe).UsageChange.x, -1, "recipe average accounts for range and duration");
        var popup = EncounterPopup.Instance;
        Check(popup, "encounter rules loaded");
        foreach (float defense in new[] { 0f, 1f, 50f })
        {
            var constant = EncounterPlayerDamageReportPreview.WoundProbabilities(new[] { Vector2.one * 40 }, defense, popup);
            Equal(constant[popup.GenerateWoundSeverity(40, defense)], 1, "fixed damage matches game severity " + defense);
            Equal(EncounterPlayerDamageReportPreview.WoundProbabilities(new[] { new Vector2(0, 80), new Vector2(0, 20) }, defense, popup).Values.Sum(),
                1, "wound probabilities sum to one " + defense);
        }
        var knife = UnityEngine.Object.FindObjectsOfType<InGameCardBase>().First(c => c.CardModel && c.CardModel.name == "Knife_Flint");
        var action = knife.DismantleActions.First();
        var originalStats = action.AllStatModifiers;
        string original = JsonUtility.ToJson(action), random = RandomState();
        var preview = CardActionPreview.PreviewAction(action, knife, null);
        Check(!ReferenceEquals(originalStats, preview.AllStatModifiers) && ReferenceEquals(originalStats, action.AllStatModifiers), "action caches isolated");
        Check(JsonUtility.ToJson(action) == original, "action source unchanged");
        Check(RandomState() == random, "action preview preserves random state");
        string weaponText = Utils.FormatWeaponStats(knife);
        Check(RandomState() == random, "weapon preview preserves random state");
        Check(knife.CardModel.WeaponMoves.All(m => weaponText.Contains(m.ActionName.ToString())), "knife move names displayed");
        Check(!weaponText.Contains("NaN") && !weaponText.Contains("Infinity"), "weapon numbers finite");
        Check(weaponText.Contains(Utils.LcStr("CSFFCardDetailTooltip.NoAttack", "Does not deal attack damage")), "knife feint is labelled as non-damaging");
        Log("KNIFE " + weaponText);
        foreach (var card in UnityEngine.Object.FindObjectsOfType<InGameCardBase>().Where(c => c.CardModel && !c.IsPinned))
            foreach (var a in card.DismantleActions ?? Array.Empty<DismantleCardAction>())
            {
                string before = JsonUtility.ToJson(a);
                random = RandomState();
                Utils.FormatCardAction(a, card);
                Check(before == JsonUtility.ToJson(a) && random == RandomState(), "read-only action " + card.CardModel.name + "/" + a.ActionName);
            }

        // Compare the preview against the game's execution-side calculation using fixed values.
        var change = new CardStateChange { ModType = CardModifications.DurabilityChanges,
            UsageChange = Vector2.one * -4, Special1Change = Vector2.one * 8 };
        var resolved = CardStateChangePreview.PreviewStateChange(change, knife, null, 0.5f, true);
        var savedRandom = UnityEngine.Random.state;
        try
        {
            var actual = new OverTimeDurabilityChanges(change, 2, 0.5f, knife, null, InGameNPCOrPlayer.PlayerAgent);
            Equal(resolved.UsageChange.x, actual.UsagePerTick * 2, "usage agrees with game execution");
            Equal(resolved.Special1Change.x, actual.Special1PerTick * 2, "special durability agrees with game execution");
        }
        finally { UnityEngine.Random.state = savedRandom; }

        var assets = Resources.FindObjectsOfTypeAll<CardData>();
        CheckCookingAndDrops(knife, assets);
        var fixtureObject = new GameObject("Tooltip CardStateChange fixture");
        fixtureObject.SetActive(false);
        var fixture = fixtureObject.AddComponent<InGameCardBase>();
        try
        {
            var armor = assets.First(c => c.name == "LeatherCuirass_");
            AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(fixture, armor, null);
            fixture.CurrentUsageDurability = armor.UsageDurability.Max * 0.01f;
            fixture.CurrentSpecial1 = fixture.CurrentSpecial2 = fixture.CurrentSpecial3 = fixture.CurrentSpecial4 = 50;
            foreach (var curve in (DurabilityInterpolatedValue[])AccessTools.Field(typeof(CardData), "ArmorValueDurabilitiesMultiplier").GetValue(armor))
                Log("ARMOR CURVE " + JsonUtility.ToJson(curve));
            float multiplier = armor.ArmorValueMultiplier(fixture);
            Check(multiplier != 1, "armour fixture exercises durability multiplier");
            Equal(ArmorValuesPreview.ArmorDefense(fixture, new List<DamageType>(), BodyLocations.Torso),
                armor.ArmorValues.CalculateArmorForLocation(new List<DamageType>(), BodyLocations.Torso) * multiplier,
                "armour condition affects defense");
            Equal(ArmorValuesPreview.ArmorHitWeights(fixture).Torso,
                armor.ArmorValues.TorsoHitProbabilityModifier * armor.ArmorProbabilitiesMultiplier(fixture),
                "armour condition affects hit weight");
            Log("ARMOR multiplier=" + multiplier);

            int interpolated = 0, changed = 0;
            foreach (var model in assets)
            {
                var actions = (model.DismantleActions ?? new List<DismantleCardAction>()).Cast<CardAction>()
                    .Concat(model.CardInteractions ?? Array.Empty<CardOnCardAction>())
                    .Concat(model.AlternateDismantleActions ?? new List<DismantleCardAction>());
                foreach (var a in actions)
                {
                    var changes = new List<CardStateChange> { a.ReceivingCardChanges };
                    if (a is CardOnCardAction onCard) changes.Add(onCard.GivenCardChanges);
                    foreach (var raw in changes)
                    {
                        if (a.ActionName.DefaultText != null && (a.ActionName.DefaultText.ToLowerInvariant().Contains("sharpen") || a.ActionName.DefaultText.ToLowerInvariant().Contains("grind")))
                            Log("SHARPEN " + model.name + "/" + a.ActionName + " " + JsonUtility.ToJson(raw));
                        if (raw.ModType != CardModifications.DurabilityChanges ||
                            (!(raw.StatInterpolatedDurabilityChanges?.Length > 0) && !(raw.DurabilityInterpolatedTransfers?.Length > 0))) continue;
                        AccessTools.Property(typeof(InGameCardBase), "CardModel").SetValue(fixture, model, null);
                        var p = CardStateChangePreview.PreviewStateChange(raw, fixture, knife, -1, true);
                        var state = UnityEngine.Random.state;
                        try
                        {
                            var actual = new OverTimeDurabilityChanges(raw, 1, -1, fixture, knife, InGameNPCOrPlayer.PlayerAgent);
                            var ranges = new[] { p.SpoilageChange, p.UsageChange, p.FuelChange, p.ChargesChange, p.LiquidQuantityChange,
                                p.Special1Change, p.Special2Change, p.Special3Change, p.Special4Change };
                            var values = new[] { actual.SpoilagePerTick, actual.UsagePerTick, actual.FuelPerTick, actual.ProgressPerTick, actual.LiquidPerTick,
                                actual.Special1PerTick, actual.Special2PerTick, actual.Special3PerTick, actual.Special4PerTick };
                            Check(values.Select((value, i) => value >= Mathf.Min(ranges[i].x, ranges[i].y) - 0.001f &&
                                value <= Mathf.Max(ranges[i].x, ranges[i].y) + 0.001f).All(v => v), "interpolated action all durabilities " + model.name + "/" + a.ActionName);
                            if (p.UsageChange != raw.UsageChange || p.Special1Change != raw.Special1Change || p.Special2Change != raw.Special2Change ||
                                p.Special3Change != raw.Special3Change || p.Special4Change != raw.Special4Change) changed++;
                            Log("INTERPOLATED " + model.name + "/" + a.ActionName + " resolved=" + string.Join(";", ranges.Select(r => r.ToString())));
                            interpolated++;
                        }
                        finally { UnityEngine.Random.state = state; }
                    }
                }
            }
            Check(interpolated > 0, "real interpolated action assets exercised");
            Check(changed > 0, "interpolation changes the displayed values");
            var encounterObject = new GameObject("Tooltip encounter fixture");
            encounterObject.SetActive(false);
            var encounter = encounterObject.AddComponent<InGameEncounter>();
            var previousEncounter = popup.CurrentEncounter;
            try
            {
                encounter.EncounterModel = Resources.FindObjectsOfTypeAll<global::Encounter>()
                    .First(e => e.EnemyBodyTemplate && e.EnemyActions?.Length > 0);
                encounter.CurrentEnemyAction = encounter.EncounterModel.EnemyActions.First(a => !a.DoesNotAttack);
                encounter.CurrentEnemySize = 50;
                encounter.CurrentEnemyBlood = 100;
                AccessTools.Property(typeof(EncounterPopup), "CurrentEncounter").SetValue(popup, encounter, null);
                foreach (var move in knife.CardModel.WeaponMoves)
                    foreach (bool distant in new[] { false, true })
                    {
                        AccessTools.Property(typeof(InGameEncounter), "Distant").SetValue(encounter, distant, null);
                        AccessTools.Property(typeof(InGameEncounter), "Wrestling").SetValue(encounter, !distant, null);
                        encounter.EnemyVulnerable = !distant;
                        var combatAction = new GenericEncounterPlayerAction();
                        combatAction.InitializeWithCardAndAction(knife, move, encounter.EnemyName, new List<PlayerValuesModifier>());
                        string before = JsonUtility.ToJson(encounter), rng = RandomState();
                        var meleeField = AccessTools.Field(typeof(EncounterPopup), "CurrentRoundMeleeClashResult");
                        var rangedField = AccessTools.Field(typeof(EncounterPopup), "CurrentRoundRangedClashResult");
                        string melee = JsonUtility.ToJson(meleeField.GetValue(popup)), ranged = JsonUtility.ToJson(rangedField.GetValue(popup));
                        string text = Utils.FormatEncounterPlayerAction(combatAction, popup);
                        Check(!text.Contains("NaN") && !text.Contains("Infinity") && text.Length > 0, "combat preview " + move.name + "/" + distant);
                        Check(rng == RandomState() && before == JsonUtility.ToJson(encounter)
                            && melee == JsonUtility.ToJson(meleeField.GetValue(popup)) && ranged == JsonUtility.ToJson(rangedField.GetValue(popup)),
                            "combat state and RNG preserved " + move.name + "/" + distant);
                        Log("COMBAT " + move.name + "/" + distant + " " + text);
                    }
                foreach (var enemyAction in encounter.EncounterModel.EnemyActions.Where(a => !a.DoesNotAttack))
                {
                    string rng = RandomState();
                    Utils.FormatEnemyHitResult(encounter, enemyAction, popup);
                    Check(rng == RandomState(), "enemy wound preview preserves RNG");
                }
            }
            finally
            {
                AccessTools.Property(typeof(EncounterPopup), "CurrentEncounter").SetValue(popup, previousEncounter, null);
                UnityEngine.Object.Destroy(encounterObject);
            }
        }
        finally { UnityEngine.Object.Destroy(fixtureObject); }
    }
}
