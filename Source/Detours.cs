using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Harmony;
using RimWorld;
using Verse;

namespace BillRequirementsPlus
{
    [HarmonyPatch(typeof(Dialog_BillConfig))]
    [HarmonyPatch(nameof(Dialog_BillConfig.DoWindowContents))]
    static class Patch_Dialog_BillConfig_DoWindowContents
    {
        static void Prefix(Dialog_BillConfig __instance, Rect inRect)
        {
            BillRequirementsMod.CurrentBill = (Bill_Production)_bill.GetValue(__instance);
        }

        private static FieldInfo _bill = AccessTools.Field(typeof(Dialog_BillConfig), "bill");
    }

    [HarmonyPatch(typeof(IngredientValueGetter_Volume))]
    [HarmonyPatch(nameof(IngredientValueGetter_Volume.BillRequirementsDescription))]
    static class Patch_IngredientValueGetter_Volume
    {
        static bool Prefix(ref string __result, RecipeDef r, IngredientCount ing)
        {
            if (!ing.filter.AllowedThingDefs.Any((ThingDef td) => td.smallVolume)
                || ing.filter.AllowedThingDefs.Any((ThingDef td) => td.smallVolume && !r.GetPremultipliedSmallIngredients().Contains(td)))
            {
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

    [HarmonyPatch(typeof(IngredientValueGetter_Nutrition))]
    [HarmonyPatch(nameof(IngredientValueGetter_Nutrition.BillRequirementsDescription))]
    static class Patch_IngredientValueGetter_Nutrition
    {
        static bool Prefix(ref string __result, RecipeDef r, IngredientCount ing)
        {
            __result = "BillRequiresNutrition"
                           .Translate(ing.GetBaseCount() + BillRequirementsMod.GetMaxAllowedCount(r, ing, true))
                           + " (" + ing.filter.Summary + ")";

            return false;
        }
    }
}
