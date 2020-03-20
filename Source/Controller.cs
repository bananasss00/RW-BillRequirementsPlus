using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using RimWorld;
using UnityEngine;
using Verse;

namespace BillRequirementsPlus {
    public class BillRequirementsMod : Mod {
        private const int UpdateDelayThingsCount = 180;
        private static readonly Dictionary<ThingDef, CountInfo> ThingCountOnMapCached =
            new Dictionary<ThingDef, CountInfo>();

        public static ModSettings Settings;

        public BillRequirementsMod(ModContentPack content) : base(content) {
            HarmonyInstance.Create("rimworld.harmony.bill_requirements_plus").PatchAll(Assembly.GetExecutingAssembly());
            Settings = GetSettings<ModSettings>();
            Log.Message("BillRequirementsPlus :: Initialized");
        }

        /// <summary>
        /// Current opened Bill
        /// </summary>
        public static Bill_Production CurrentBill { get; set; }

        public override string SettingsCategory() {
            return Settings.SettingsCategory();
        }

        public override void DoSettingsWindowContents(Rect inRect) {
            Settings.DoSettingsWindowContents(inRect);
        }

        public class NotResolved {
            public ThingDef Def;
            public IngredientCount IngCount;
            public ERecipeType IngType;
            public float Count;
        }

        public static IEnumerable<NotResolved> GetNotResolvedIngs(Bill bill) {
            var billProduction = (Bill_Production) bill;
            var recipe = bill.recipe;
            foreach (var recipeIngredient in recipe.ingredients) {
                var countMax = 0f;
                ThingDef def = null;
                ERecipeType type = ERecipeType.Unknown;
                foreach (var td in recipeIngredient.filter.AllowedThingDefs) {
                    var ingType = GetType(td, recipeIngredient, billProduction);
                    if (ingType != ERecipeType.Unknown && ingType != ERecipeType.SelectableNotAllowed) {
                        var count = ThingCountOnMap(td);

                        if (td.smallVolume)
                            count *= 10f;

                        if (count > countMax || countMax < 0.0001f) {
                            countMax = count * recipe.IngredientValueGetter.ValuePerUnitOf(td);
                            def = td;
                            type = ingType;
                        }
                    }
                }

                if (countMax < recipeIngredient.GetBaseCount())
                    yield return new NotResolved {
                        Def = def,
                        IngCount = recipeIngredient,
                        IngType = type,
                        Count = countMax
                    };
            }
        }

        public static IEnumerable<Pair<IngredientCount, float>> GetNotAllowedCount(Bill bill) {
            var billProduction = (Bill_Production) bill;
            var recipe = bill.recipe;
            foreach (var recipeIngredient in recipe.ingredients) {
                var countMax = 0f;
                foreach (var td in recipeIngredient.filter.AllowedThingDefs) {
                    var ingType = GetType(td, recipeIngredient, billProduction);
                    if (ingType != ERecipeType.Unknown && ingType != ERecipeType.SelectableNotAllowed) {
                        var count = ThingCountOnMap(td);

                        if (td.smallVolume)
                            count *= 10f;

                        if (count > countMax || countMax < 0.0001f)
                            countMax = count * recipe.IngredientValueGetter.ValuePerUnitOf(td);
                    }
                }

                if (countMax < recipeIngredient.GetBaseCount())
                    yield return new Pair<IngredientCount, float>(recipeIngredient, countMax);
            }
        }

        public static string GetMaxAllowedCount(RecipeDef r, IngredientCount ing, bool isNutrition) {
            float countMax = 0f, countMaxNutrition = 0f;

            var bill = CurrentBill;
            if (bill == null)
                return "";

            foreach (var td in ing.filter.AllowedThingDefs) {
                var ingType = GetType(td, ing, bill);
                if (ingType != ERecipeType.Unknown && ingType != ERecipeType.SelectableNotAllowed) {
                    var count = ThingCountOnMap(td);

                    if (td.smallVolume)
                        count *= 10f;

                    if (count > countMax) {
                        countMax = count;

                        if (isNutrition) countMaxNutrition = count * NutritionValuePerUnitOf(td);
                    }


                    //Log.Message(String.Format("thing: {0}({1}), is_fixed: {2}, in_rff: {3}, in_bff: {4}, in_bif: {5}",
                    //    td.LabelCap,
                    //    count,
                    //    ing.IsFixedIngredient,
                    //    r.fixedIngredientFilter.Allows(td),
                    //    bill.recipe.fixedIngredientFilter.Allows(td),
                    //    bill.ingredientFilter.Allows(td)));
                }
            }

            string result;

            if (isNutrition)
                result = !Settings.UseDashes || countMaxNutrition >= ing.GetBaseCount()
                    ? $" ({countMaxNutrition} / {countMax})"
                    : " (-)";
            else
                result = !Settings.UseDashes || countMax >= ing.GetBaseCount() ? $" ({countMax})" : " (-)";

            return result;
        }

        public static float NutritionValuePerUnitOf(ThingDef t) {
            if (!t.IsNutritionGivingIngestible) return 0f;
            return t.GetStatValueAbstract(StatDefOf.Nutrition);
        }

        private static ERecipeType GetType(ThingDef td, IngredientCount ing, Bill_Production bill) {
            if (ing.IsFixedIngredient) {
                if (!bill.recipe.fixedIngredientFilter.Allows(td))
                    return ERecipeType.FixedNotSelectable;

                if (bill.ingredientFilter.Allows(td))
                    return ERecipeType.FixedSelectableAllowed;

                return ERecipeType.Unknown;
            }

            if (bill.recipe.fixedIngredientFilter.Allows(td))
                return bill.ingredientFilter.Allows(td)
                    ? ERecipeType.SelectableAllowed
                    : ERecipeType.SelectableNotAllowed;

            return ERecipeType.Unknown;
        }

        private static float ThingCountOnMap(ThingDef d) {
            if (!ThingCountOnMapCached.TryGetValue(d, out var countInfo)) {
                countInfo = new CountInfo();
                ThingCountOnMapCached[d] = countInfo;
            }

            var ticks = Find.TickManager.TicksGame;
            if (Math.Abs(ticks - countInfo.TickUpdated) > UpdateDelayThingsCount) {
                float count = 0;
                var list = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
                foreach (var thing in list)
                    if (!thing.Position.Fogged(thing.Map) && d == thing.def &&
                        (Settings.CountForbiddenItems || !thing.IsForbidden(Faction.OfPlayer)))
                        count += thing.stackCount;

                countInfo.Count = count;
                countInfo.TickUpdated = ticks;
            }

            return countInfo.Count;
        }

        public enum ERecipeType {
            FixedNotSelectable,
            FixedSelectableAllowed,
            SelectableAllowed,
            SelectableNotAllowed,
            Unknown
        }

        public class CountInfo {
            public float Count;
            public int TickUpdated;
        }
    }
}