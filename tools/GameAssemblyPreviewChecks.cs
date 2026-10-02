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

    static void CheckTooltipFormatting()
    {
        var stats = Resources.FindObjectsOfTypeAll<GameStat>().Take(2).ToArray();
        Check(stats.Length == 2, "stat formatting fixtures available");
        var first = new StatModifier { Stat = stats[0], ValueModifier = Vector2.one * 2, ApplyEachTick = true };
        var second = new StatModifier { Stat = stats[1], RateModifier = Vector2.one * 3,
            MinValueModifier = Vector2.one * -1, MaxValueModifier = Vector2.one * 4, ApplyEachTick = true };
        first = new StatModifier(first, 1, false, "Source A", "", 0);
        second = new StatModifier(second, 1, false, "Source B", "", 0);
        string heading = Utils.LcStr("CSFFCardDetailTooltip.PerTick", "Per action tick");
        var once = new StatModifier { Stat = stats[0], ValueModifier = Vector2.one * -24 };
        var empty = new StatModifier { Stat = stats[0], ApplyEachTick = true };
        string text = StatModifierPreview.Format(new[] { first, empty, once, second }, 2);
        Check(text.Split(new[] { heading }, StringSplitOptions.None).Length == 2, "one heading for multiple per-tick effects");
        Check(text.IndexOf("-24", StringComparison.Ordinal) < text.IndexOf(heading, StringComparison.Ordinal), "one-time effects stay outside per-tick group");
        Check(text.Contains("Source A") && text.Contains("Source B"), "modifier sources retained");
        Check(text.Contains(stats[0].GameName.ToString()) && text.Contains(stats[1].GameName.ToString()), "distinct stat labels retained");
        Check(text.Contains(Utils.LcStr("CSFFCardDetailTooltip.Minimum", "Minimum")) &&
            text.Contains(Utils.LcStr("CSFFCardDetailTooltip.Maximum", "Maximum")), "minimum and maximum changes retained");
        Check(text.Contains(Utils.LcStr("CSFFCardDetailTooltip.Rate", "Rate")), "rate changes retained");
        Check(!text.Split('\n').Any(string.IsNullOrWhiteSpace), "grouped stat output has no empty lines");
        Check(StatModifierPreview.Format(new[] { empty, default(StatModifier) }) == "", "empty effects omit their heading");
        Check(StatModifierPreview.Format(null) == "", "missing modifier list is empty");
        Check(!StatModifierPreview.Format(new[] { once }).Contains(heading), "one-time-only effects have no per-tick heading");
        Check(Utils.JoinTooltipLines(new[] { "", "first\r\n\n", null, " \n  second", "" }) == "first\n  second",
            "empty lines removed without losing child indentation");

        var counter = ScriptableObject.CreateInstance<LocalTickCounter>();
        counter.name = "Counter fixture";
        try
        {
            object boxed = new LocalCounterEffect { Counter = counter };
            AccessTools.Field(typeof(LocalCounterEffect), "UsageRateModifier").SetValue(boxed, new OptionalFloatValue(false, 99));
            Check(LocalCounterEffectPreview.FormatRateEntry((LocalCounterEffect)boxed, DurabilitiesTypes.Usage) == "",
                "disabled counter with nonzero stored value is hidden");
            AccessTools.Field(typeof(LocalCounterEffect), "UsageRateModifier").SetValue(boxed, null);
            Check(LocalCounterEffectPreview.FormatRateEntry((LocalCounterEffect)boxed, DurabilitiesTypes.Usage) == "", "missing counter value is hidden");
            AccessTools.Field(typeof(LocalCounterEffect), "UsageRateModifier").SetValue(boxed, new OptionalFloatValue(true, 0));
            Check(LocalCounterEffectPreview.FormatRateEntry((LocalCounterEffect)boxed, DurabilitiesTypes.Usage) == "", "zero counter value is hidden");
            AccessTools.Field(typeof(LocalCounterEffect), "UsageRateModifier").SetValue(boxed, new OptionalFloatValue(true, 3));
            string active = LocalCounterEffectPreview.FormatRateEntry((LocalCounterEffect)boxed, DurabilitiesTypes.Usage);
            Check(active.Contains("+3") && active.Contains(counter.name), "active counter value and source retained");
        }
        finally { UnityEngine.Object.Destroy(counter); }
    }

    static void CheckNativeTooltipPreservation()
    {
        var root = new GameObject("Tooltip preservation fixture");
        var provider = root.AddComponent<TooltipProvider>();
        var tooltip = Tooltip.Instance;
        var render = AccessTools.Method(typeof(Tooltip), "LateUpdate");
        var list = (List<TooltipText>)AccessTools.Field(typeof(Tooltip), "CurrentTooltips").GetValue(tooltip);
        var enabled = Plugin.Enabled;
        var foreign = new TooltipText { Priority = int.MaxValue, TooltipContent = "Other provider" };
        try
        {
            Plugin.Enabled = true;
            const string original = "Author text\n\n<b>New system</b>";
            provider.SetTooltip("Author title", original, "Author hold prompt");
            var source = (TooltipText)AccessTools.Field(typeof(TooltipProvider), "MyTooltip").GetValue(provider);
            source.Priority = int.MaxValue - 1;
            provider.OnHoverEnter();
            var count = list.Count;
            TooltipProviderPreview.Set(provider, "Mod details");
            render.Invoke(tooltip, null);
            Check(provider.Content == original, "native tooltip source remains verbatim");
            Check(tooltip.TooltipContent.text == original + "\nMod details", "append preserves blank lines and rich text");
            Check(tooltip.TooltipTitle.text == "Author title" && source.HoldText == "Author hold prompt", "native title and hold prompt preserved");
            Check(list.Count == count, "preview registers no replacement tooltip");
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == original + "\nMod details", "repeated frames do not accumulate preview text");
            provider.SetTooltip("Updated title", "Author live update", "Updated hold prompt");
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Author live update\nMod details", "live native updates immediately preserved");
            Tooltip.AddTooltip(foreign);
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Other provider", "higher priority provider is never supplemented with unrelated details");
            Tooltip.RemoveTooltip(foreign);
            provider.NormalizedHoldTime = 0.5f;
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Author live update" && source.NormalizedHoldTime == 0.5f, "hold progress preserved without appended preview");
            provider.NormalizedHoldTime = 0;
            Plugin.Enabled = false;
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Author live update", "disabled mod leaves native content untouched");
            Plugin.Enabled = true;
            provider.OnHoverExit();
            provider.OnHoverEnter();
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Author live update", "hover exit clears stale preview");
            TooltipProviderPreview.Set(provider, "Stale details");
            root.SetActive(false);
            root.SetActive(true);
            provider.SetTooltip("Reused", "Reused content", null);
            provider.OnHoverEnter();
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Reused content", "disabled and reused provider clears stale preview");
            provider.SetTooltip("Empty", "", null);
            TooltipProviderPreview.Set(provider, "Mod details");
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Mod details" && provider.Content == "", "empty native content gains no leading blank line");
            AccessTools.Method(typeof(TooltipProvider), "CancelTooltip").Invoke(provider, null);
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Mod details", "provider without native tooltip still shows mod details");
            Check(AccessTools.Field(typeof(TooltipProvider), "MyTooltip").GetValue(provider) == null, "fallback never creates or replaces native source");
            Tooltip.AddTooltip(foreign);
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "Other provider", "fallback yields to native tooltip priority");
            Tooltip.RemoveTooltip(foreign);
            provider.SetTooltip("New source", "New native text", null);
            provider.OnHoverEnter();
            render.Invoke(tooltip, null);
            Check(tooltip.TooltipContent.text == "New native text\nMod details", "new native source replaces fallback while keeping its text");
        }
        finally
        {
            provider.OnHoverExit();
            Tooltip.RemoveTooltip(foreign);
            UnityEngine.Object.DestroyImmediate(root);
            Plugin.Enabled = enabled;
        }
    }

    static void RunChecks()
    {
        CheckNativeTooltipPreservation();
        CheckTooltipFormatting();
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
        var emptyPreview = CardActionPreview.PreviewAction(action, knife, null);
        emptyPreview.AllStatModifiers = new List<StatModifier> { default };
        emptyPreview.AllTemporaryStatModifiers = new List<StatModifier> { default };
        string emptySections = (string)AccessTools.Method(typeof(Utils), "FormatResolvedCardAction")
            .Invoke(null, new object[] { emptyPreview, knife, 0, null });
        Check(!emptySections.Contains(Utils.LcStr("CSFFCardDetailTooltip.StatModifier", "Stat Modifier")) &&
            !emptySections.Contains(Utils.LcStr("CSFFCardDetailTooltip.DuringAction", "During this action only")),
            "empty action stat groups omit section headings");
        Check(string.IsNullOrEmpty(emptySections) || !emptySections.Split('\n').Any(string.IsNullOrWhiteSpace),
            "action output has no empty lines");
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
        CheckNPCPreviews(knife);
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
                Check(EnemyValuesModifiersPreview.Format(default) == "", "empty enemy effects hidden");
                Check(EncounterTemporaryEffectPreview.Format(null, encounter, false) == "", "null temporary effect hidden");
                var effect = new EncounterTemporaryEffect
                {
                    EffectID = "Tooltip encounter test", Duration = Vector2Int.zero,
                    EffectApplies = EnemyActionEffectCondition.OnlyOnHit,
                    EnemyValuesModifiers = new EnemyValuesModifiers { BloodModifier = Vector2.one * -7, BloodApplies = EnemyActionEffectCondition.OnlyOnHitAndWound },
                    PlayerValuesModifiers = new[] { new PlayerValuesModifier { ReachModifier = Vector2.one * 13, ApplyOnlyToEscapeActions = true } }
                };
                string effectRng = RandomState();
                var nativeRandom = UnityEngine.Random.state;
                try
                {
                    var body = new BodyLocationModifiers { HeadDefenseModifier = Vector2.one * 15 };
                    Check(body.Instantiate.HeadDefenseModifier == Vector2.zero, "0.68b native temporary body modifiers are discarded");
                    var nativeModifier = effect.PlayerValuesModifiers[0].Instantiate;
                    Check(!nativeModifier.ApplyOnlyToEscapeActions && nativeModifier.WeaponFilter == null, "0.68b native temporary modifier restrictions are discarded");
                }
                finally { UnityEngine.Random.state = nativeRandom; }
                string effectText = EncounterTemporaryEffectPreview.Format(effect, encounter, false);
                Check(effectText.Contains("13") && effectText.Contains("7") && effectText.Contains(GenericEncounterPlayerActionPreview.Condition(EnemyActionEffectCondition.OnlyOnHitAndWound)), "temporary effects include ranges and nested hit conditions");
                Check(effectText.Contains(GenericEncounterPlayerActionPreview.Text("UntilEncounterEnds", "Until encounter ends")), "zero duration means encounter lifetime");
                Check(effectRng == RandomState(), "effect previews never roll chance or instantiate effects");
                Check(!effectText.Contains(GenericEncounterPlayerActionPreview.Text("EscapeOnly", "Escape actions only")), "temporary preview reflects native discarded restrictions");
                encounter.TemporaryEffects.Add(effect);
                string refreshed = EncounterTemporaryEffectPreview.Format(effect, encounter, false);
                Check(!refreshed.Contains("13") && refreshed.Contains(GenericEncounterPlayerActionPreview.Text("RefreshDuration", "Refreshes duration; does not stack values")), "existing effect refresh does not promise stacked values");
                encounter.TemporaryEffects.Remove(effect);
                effect.Duration = new Vector2Int(2, 4);
                Check(EncounterTemporaryEffectPreview.Format(effect, encounter, true).Contains(GenericEncounterPlayerActionPreview.Text("SubActionDuration", "Sub-action: duration reduced by one immediately")), "sub-action duration adjustment disclosed");
                var staleAction = new GenericEncounterPlayerAction { IsEscapeAction = true };
                encounter.CurrentPlayerAction = staleAction;
                string baselineIncoming = Utils.FormatEnemyHitResult(encounter, encounter.CurrentEnemyAction, popup);
                encounter.CurrentPlayerAction = null;
                Check(baselineIncoming == Utils.FormatEnemyHitResult(encounter, encounter.CurrentEnemyAction, popup), "incoming baseline ignores stale player escape action");
                encounter.CurrentPlayerAction = staleAction;
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
                        Check(ReferenceEquals(encounter.CurrentPlayerAction, staleAction), "hover restores selected player action");
                        Check(!text.Contains("\n\n"), "encounter tooltip hides empty lines");
                        combatAction.DontShowSuccessChance = true;
                        Check(!Utils.FormatEncounterPlayerAction(combatAction, popup).Contains(GenericEncounterPlayerActionPreview.Text("EstimatedSuccess", "Native success estimate")), "native hidden success chance respected");
                        combatAction.DontShowSuccessChance = false;
                        bool cannotFail = combatAction.CannotFailClash;
                        combatAction.CannotFailClash = true;
                        Check(Utils.FormatEncounterPlayerAction(combatAction, popup).Contains(Utils.LcStr("CSFFCardDetailTooltip.Encounter.CannotFail", "Action clash cannot fail")), "guaranteed clash overrides native estimate");
                        combatAction.CannotFailClash = cannotFail;
                        var actionType = combatAction.ActionType;
                        combatAction.ActionType = EncounterPlayerActionType.SubAction;
                        string subActionText = Utils.FormatEncounterPlayerAction(combatAction, popup);
                        Check(!subActionText.Contains(GenericEncounterPlayerActionPreview.Text("EstimatedEnemyHit", "Native enemy hit estimate"))
                            && !subActionText.Contains(GenericEncounterPlayerActionPreview.Text("IncomingAverage", "Incoming wounds at average damage/defense (if hit):")), "sub-actions do not imply enemy retaliation");
                        combatAction.ActionType = actionType;
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
    static void CheckNPCPreviews(InGameCardBase workCard)
    {
        var root = new GameObject("NPC preview fixture");
        root.SetActive(false);
        var npc = root.AddComponent<InGameNPC>();
        var otherObject = new GameObject("Second NPC preview fixture");
        otherObject.SetActive(false);
        var other = otherObject.AddComponent<InGameNPC>();
        var duty = ScriptableObject.CreateInstance<NPCDuty>();
        var step = ScriptableObject.CreateInstance<AffectItemsDutyAction>();
        var model = Resources.FindObjectsOfTypeAll<NPCAgent>().First(m => m.AlliedWithPlayer);
        var statModel = Resources.FindObjectsOfTypeAll<NPCStat>().First();
        var gm = GameManager.Instance;
        try
        {
            foreach (var worker in new[] { npc, other })
            {
                AccessTools.Property(typeof(InGameNPC), "NPCModel").SetValue(worker, model, null);
                AccessTools.Property(typeof(InGameNPC), "Initialized").SetValue(worker, true, null);
                AccessTools.Property(typeof(InGameNPC), "AssociatedCard").SetValue(worker, workCard, null);
                var stat = worker.gameObject.AddComponent<InGameNPCStat>();
                stat.Model = new NPCStatInstance { ModelStat = statModel };
                stat.ParentNPC = worker;
                var dict = (Dictionary<NPCStat, InGameNPCStat>)AccessTools.Field(typeof(InGameNPC), "NPCStatsDict").GetValue(worker);
                dict.Add(statModel, stat);
                gm.AllNPCs.Add(worker);
            }
            Check(NPCStatInstantModifierPreview.Targets(false, model, null).Contains(npc)
                && NPCStatInstantModifierPreview.Targets(false, model, null).Contains(other), "explicit NPC stat target includes all matching instances");
            Check(NPCStatInstantModifierPreview.Targets(true, null, npc).SequenceEqual(new[] { npc }), "associated NPC target is isolated from other instances");
            var modifier = new NPCStatInstantModifier { TargetStat = statModel, UseAssociatedAgent = true, ValueChange = Vector2.one * -7 };
            Check(NPCStatInstantModifierPreview.Format(new[] { modifier }, npc).Contains("7"), "NPC stat change shown for actual target");
            modifier.ValueChange = Vector2.zero;
            Check(NPCStatInstantModifierPreview.Format(new[] { modifier }, npc) == "", "zero NPC stat change hidden");
            Check(NPCStatInstantModifierPreview.Format(new[] { modifier }, null) == "", "unresolved associated NPC has no invented effect");
            duty.DutyName = new LocalizedString { DefaultText = "NPC preview duty", LocalizationKey = "IGNOREKEY" };
            step.AffectType = AffectItemsDutyAction.AffectTypes.PerformActionOnCard;
            duty.ActionSequence = new NPCDutyAction[] { step };
            var reference = new NPCDutyRef { TargetDuty = duty };
            // Pick a non-always-active mode without depending on its serialization name.
            reference.ActivatingMode = (NPCDutyActiveSettings)Enum.GetValues(typeof(NPCDutyActiveSettings)).Cast<NPCDutyActiveSettings>().First(m => m != NPCDutyActiveSettings.AlwaysActive);
            npc.AllDuties.Add(reference);
            AccessTools.Field(typeof(InGameNPC), "DutiesDict").SetValue(npc, new Dictionary<NPCDuty, NPCDutyRef> { [duty] = reference });
            var dutyChange = new NPCSetDutyActive { Duty = duty, UseAssociatedAgent = true, SetActive = true };
            Check(NPCSetDutyActivePreview.Format(new[] { dutyChange }, npc).Contains("NPC preview duty"), "NPC duty enable effect shown");
            reference.SetActive(true);
            Check(NPCSetDutyActivePreview.Format(new[] { dutyChange }, npc) == "", "already active duty effect hidden");
            Check(NPCDutySelectionInfoPreview.Format(default) == "", "uninitialized duty report is not a failure");
            var report = new NPCDutySelectionInfo(npc, duty) { AtHomeConditionValid = false };
            reference.WeightInfo = report;
            Check(NPCDutySelectionInfoPreview.Format(report) == NPCStatInstantModifierPreview.Text("NeedsHome", "must be at home"), "only recorded duty failures are displayed");
            npc.CurrentDuty = new SelectedNPCDuty(duty);
            // Select a step without running StartDutyAction or its item-selection side effects.
            object boxed = npc.CurrentDuty;
            AccessTools.Field(typeof(SelectedNPCDuty), "CurrentActionIndex").SetValue(boxed, 0);
            npc.CurrentDuty = (SelectedNPCDuty)boxed;
            npc.CurrentDuty.ActionStarted = true;
            npc.CurrentDuty.StartWaiting(8);
            npc.CurrentDuty.UpdateWaiting();
            var action = new DismantleCardAction { ActionName = new LocalizedString { DefaultText = "Selected NPC test work", LocalizationKey = "IGNOREKEY" },
                NPCStatModifications = new[] { new NPCStatInstantModifier { TargetStat = statModel, UseAssociatedAgent = true, ValueChange = Vector2.one * -3 } } };
            npc.CurrentDuty.ActionCards.Add(workCard);
            npc.CurrentDuty.DismantleActions.Add(action);
            string before = JsonUtility.ToJson(npc.CurrentDuty), originalAction = JsonUtility.ToJson(action), random = RandomState();
            string text = SelectedNPCDutyPreview.Format(npc);
            Check(text.Contains("Selected NPC test work") && text.Contains("7") && text.Contains("3"), "selected NPC work displays step countdown and target effects");
            Check(before == JsonUtility.ToJson(npc.CurrentDuty) && originalAction == JsonUtility.ToJson(action) && random == RandomState(), "NPC work preview preserves duty action caches and RNG");
            Check(!text.Contains("\n\n"), "NPC work preview has no blank lines");
            duty.DoNotShowToPlayer = true;
            Check(SelectedNPCDutyPreview.Format(npc) == "", "hidden NPC duty details remain hidden");
            duty.DoNotShowToPlayer = false;
            var playerStat = Resources.FindObjectsOfTypeAll<GameStat>().FirstOrDefault(s => GameStatsToNPCStats.GetCorrespondingStat(s, model));
            Check(playerStat, "loaded allied NPC exposes player-to-NPC stat mapping");
            action.StatModifications = new[] { new StatModifier { Stat = playerStat, ValueModifier = Vector2.one * 2 } };
            var player = CardActionPreview.PreviewAction(action, workCard, null);
            var workerPreview = CardActionPreview.PreviewAction(action, workCard, null, npc);
            Check(player.AllStatModifiers.Any(m => m.Stat == playerStat) && !workerPreview.AllStatModifiers.Any(m => m.Stat == playerStat)
                && workerPreview.AllNPCStatModifiers.Any(m => m.TargetStat == GameStatsToNPCStats.GetCorrespondingStat(playerStat, model)), "worker preview maps stats to NPC while player button stays player-specific");
        }
        finally
        {
            gm.AllNPCs.Remove(npc); gm.AllNPCs.Remove(other);
            UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(otherObject);
            UnityEngine.Object.Destroy(duty); UnityEngine.Object.Destroy(step);
        }
    }
}
