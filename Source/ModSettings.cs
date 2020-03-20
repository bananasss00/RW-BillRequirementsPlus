using UnityEngine;
using Verse;

namespace BillRequirementsPlus {
    public class ModSettings : Verse.ModSettings {
        public bool CountForbiddenItems;
        public bool UseDashes = true;

        public string SettingsCategory() {
            return "BillRequirementsPlus_SettingsCategory".Translate();
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref UseDashes, "UseDashes");
            Scribe_Values.Look(ref CountForbiddenItems, "CountForbiddenItems");
        }

        public void DoSettingsWindowContents(Rect rect) {
            var list = new Listing_Standard(GameFont.Small) {ColumnWidth = rect.width / 2};
            list.Begin(rect);
            list.Gap();

            list.CheckboxLabeled("DashesIfLessRequiredAmount".Translate(), ref UseDashes,
                "DashesIfLessRequiredAmount".Translate());
            list.CheckboxLabeled("CountForbiddenItems".Translate(), ref CountForbiddenItems,
                "CountForbiddenItems".Translate());

            list.End();
        }
    }
}