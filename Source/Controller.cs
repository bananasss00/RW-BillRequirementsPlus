using System;
using System.Reflection;
using Harmony;
using Verse;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace BillRequirementsPlus
{
    public class BillRequirementsMod : Verse.Mod
    {
        public BillRequirementsMod(ModContentPack content) : base(content)
        {
            HarmonyInstance.Create("rimworld.harmony.bill_requirements_plus").PatchAll(Assembly.GetExecutingAssembly());
            Settings = GetSettings<ModSettings>();
            Log.Message($"BillRequirementsPlus :: Initialized");
        }

        public override string SettingsCategory()
        {
            return Settings.SettingsCategory();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public static IEnumerable<Pair<IngredientCount, float>> GetNotAllowedCount(Bill bill)
        {
            Bill_Production billProduction = (Bill_Production) bill;
            RecipeDef recipe = bill.recipe;
            foreach (var recipeIngredient in recipe.ingredients)
            {
                float countMax = 0f;
                foreach (var td in recipeIngredient.filter.AllowedThingDefs)
                {
                    var ingType = GetType(td, recipeIngredient, billProduction);
                    if (ingType != ERecipeType.Unknown && ingType != ERecipeType.SelectableNotAllowed)
                    {
                        float count = ThingCountOnMap(td);

                        if (td.smallVolume)
                            count *= 10f;

                        if (count > countMax || countMax < 0.0001f)
                        {
                            countMax = count * recipe.IngredientValueGetter.ValuePerUnitOf(td);
                        }
                    }
                }

                if (countMax < recipeIngredient.GetBaseCount()/* && ingredientCount != null*/)
                    yield return new Pair<IngredientCount, float>(/*ingredientCount*/recipeIngredient, countMax);
            }
        }

        public static string GetMaxAllowedCount(RecipeDef r, IngredientCount ing, bool isNutrition)
        {
            float countMax = 0f, countMaxNutrition = 0f;

            var bill = CurrentBill;
            if (bill == null)
                return "";

            foreach (var td in ing.filter.AllowedThingDefs)
            {
                var ingType = GetType(td, ing, bill);
                if (ingType != ERecipeType.Unknown && ingType != ERecipeType.SelectableNotAllowed)
                {
                    float count = ThingCountOnMap(td);

                    if (td.smallVolume)
                        count *= 10f;

                    if (count > countMax)
                    {
                        countMax = count;

                        if (isNutrition)
                        {
                            countMaxNutrition = count * NutritionValuePerUnitOf(td);
                        }
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
                result = !Settings.UseDashes || countMaxNutrition >= ing.GetBaseCount() ? $" ({countMaxNutrition} / {countMax})" : " (-)";
            else
                result = !Settings.UseDashes || countMax >= ing.GetBaseCount() ? $" ({countMax})" : " (-)";

            return result;
        }

        public static float NutritionValuePerUnitOf(ThingDef t)
        {
            if (!t.IsNutritionGivingIngestible)
            {
                return 0f;
            }
            return t.GetStatValueAbstract(StatDefOf.Nutrition);
        }

        enum ERecipeType
        {
            FixedNotSelectable,
            FixedSelectableAllowed,
            SelectableAllowed,
            SelectableNotAllowed,
            Unknown
        }

        static ERecipeType GetType(ThingDef td, IngredientCount ing, Bill_Production bill)
        {
            if (ing.IsFixedIngredient)
            {
                if (!bill.recipe.fixedIngredientFilter.Allows(td))
                    return ERecipeType.FixedNotSelectable;

                if (bill.ingredientFilter.Allows(td))
                    return ERecipeType.FixedSelectableAllowed;

                return ERecipeType.Unknown;
            }

            if (bill.recipe.fixedIngredientFilter.Allows(td))
            {
                return bill.ingredientFilter.Allows(td) ? ERecipeType.SelectableAllowed : ERecipeType.SelectableNotAllowed;
            }

            return ERecipeType.Unknown;
        }
        
        static Bill_Production GetBill(RecipeDef r)
        {
            IBillGiver billGiver = Find.Selector.SingleSelectedThing as IBillGiver;
            
            if (billGiver != null)
            {
                return (Bill_Production)billGiver.BillStack.Bills.FirstOrDefault((Bill b) => b.recipe == r);
            }

            return null;
        }

        static float ThingCountOnMap(ThingDef d)
        {
            if (!_thingCountOnMapCached.TryGetValue(d, out CountInfo countInfo))
            {
                countInfo = new CountInfo();
                _thingCountOnMapCached[d] = countInfo;
            }

            int ticks = Find.TickManager.TicksGame;
            if (Math.Abs(ticks - countInfo.tickUpdated) > 180)
            {
                float count = 0;
                List<Thing> list = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
                foreach (Thing thing in list)
                {
                    if (!thing.Position.Fogged(thing.Map) && d == thing.def && (Settings.CountForbiddenItems || !thing.IsForbidden(Faction.OfPlayer)))
                        count += thing.stackCount;
                }

                countInfo.count = count;
                countInfo.tickUpdated = ticks;
            }

            return countInfo.count;
        }

        public class CountInfo
        {
            public int tickUpdated = 0;
            public float count = 0;
        }

        private static Dictionary<ThingDef, CountInfo> _thingCountOnMapCached = new Dictionary<ThingDef, CountInfo>();

        public static ModSettings Settings;
        public static Bill_Production CurrentBill { get; set; }
    }
}
