using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CSFFCardDetailTooltip;

// Preview adapter for the original CookingRecipe game type.
public static class CookingRecipePreview
{
    public static bool IsActive(CookingRecipe recipe, InGameCardBase ingredient, InGameCardBase cooker)
    {
        if (recipe == null || !cooker || !cooker.IsCooking()) return false;
        if (ingredient && (ingredient.IgnoreTickDurabilityChanges || ingredient.IsInBpMadeByNPC)) return false;
        return recipe.Conditions.ConditionsValid(false,
            recipe.ConditionsCard == CookingConditionsCard.Cooker ? cooker : ingredient, null, null);
    }

    public static CookingRecipe ActiveIngredientRecipe(InGameCardBase ingredient)
    {
        if (!ingredient) return null;
        // Liquids may be registered directly on their container or on its cooking station.
        var container = ingredient.CurrentContainer;
        for (var cooker = container; cooker; cooker = cooker.CurrentContainer)
        {
            if (cooker.CookingCards == null || !cooker.CookingCards.Any(status => status.Card == ingredient)) continue;
            var recipe = cooker.CardModel.GetRecipeForCard(ingredient, cooker);
            return IsActive(recipe, ingredient, cooker) &&
                   recipe.IngredientChanges.ModType == CardModifications.DurabilityChanges ? recipe : null;
        }
        return null;
    }

    public static CardStateChange AverageRecipeChanges(CookingRecipe recipe)
    {
        var change = recipe.IngredientChanges;
        float duration = (recipe.MinDuration + recipe.MaxDuration) / 2f;
        Vector2 Average(Vector2 range) => Vector2.one * ((range.x + range.y) / (2 * duration));
        change.SpoilageChange = Average(change.SpoilageChange);
        change.UsageChange = Average(change.UsageChange);
        change.FuelChange = Average(change.FuelChange);
        change.ChargesChange = Average(change.ChargesChange);
        change.LiquidQuantityChange = Average(change.LiquidQuantityChange);
        change.Special1Change = Average(change.Special1Change);
        change.Special2Change = Average(change.Special2Change);
        change.Special3Change = Average(change.Special3Change);
        change.Special4Change = Average(change.Special4Change);
        return change;
    }
}
