using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BillRequirementsPlus {
    /// <summary>
    /// Get current opened Bill
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig))]
    [HarmonyPatch(nameof(Dialog_BillConfig.DoWindowContents))]
    internal static class Patch_Dialog_BillConfig_DoWindowContents {
        private static readonly FieldInfo BillFld = AccessTools.Field(typeof(Dialog_BillConfig), "bill");

        private static void Prefix(Dialog_BillConfig __instance, Rect inRect) {
            BillRequirementsMod.CurrentBill = (Bill_Production) BillFld.GetValue(__instance);
        }
    }

    /// <summary>
    /// Patch modify info about bill requirements
    /// </summary>
    [HarmonyPatch(typeof(IngredientValueGetter_Volume))]
    [HarmonyPatch(nameof(IngredientValueGetter_Volume.BillRequirementsDescription))]
    internal static class Patch_IngredientValueGetter_Volume {
        private static bool Prefix(ref string __result, RecipeDef r, IngredientCount ing) {
            if (!ing.filter.AllowedThingDefs.Any(td => td.smallVolume)
                || ing.filter.AllowedThingDefs.Any(td =>
                    td.smallVolume && !r.GetPremultipliedSmallIngredients().Contains(td))) {
                __result = "BillRequires"
                    .Translate(ing.GetBaseCount(),
                        ing.filter.Summary + BillRequirementsMod.GetMaxAllowedCount(r, ing, false)
                    );

                return false;
            }

            __result = "BillRequires"
                .Translate(ing.GetBaseCount() * 10f,
                    ing.filter.Summary + BillRequirementsMod.GetMaxAllowedCount(r, ing, false)
                );

            return false;
        }
    }

    /// <summary>
    /// Patch modify info about bill requirements(nutrition)
    /// </summary>
    [HarmonyPatch(typeof(IngredientValueGetter_Nutrition))]
    [HarmonyPatch(nameof(IngredientValueGetter_Nutrition.BillRequirementsDescription))]
    internal static class Patch_IngredientValueGetter_Nutrition {
        private static bool Prefix(ref string __result, RecipeDef r, IngredientCount ing) {
            __result = "BillRequiresNutrition"
                           .Translate(ing.GetBaseCount() + BillRequirementsMod.GetMaxAllowedCount(r, ing, true))
                       + " (" + ing.filter.Summary + ")";

            return false;
        }
    }
}