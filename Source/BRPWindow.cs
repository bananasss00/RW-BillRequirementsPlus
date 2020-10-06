using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BillRequirementsPlus {
    public class BRPWindow : Window {
        private readonly List<BadBill> _badBills;
        private int _nextUpdateTick;
        private string _quickSearch = "";
        private Vector2 _resultsAreaScroll;
        private int _openedTime;
        private const int SetFocusDelay = 30;

        public BRPWindow() {
            //optionalTitle = "BillRequirementsPlus";
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            forcePause = true;
            draggable = true;
            doCloseX = true;
            _nextUpdateTick = 0;
            _badBills = new List<BadBill>();
        }

        public override Vector2 InitialSize => new Vector2(640f, 480f);

        public override void PostOpen() {
            base.PostOpen();
            _openedTime = Time.frameCount;
        }

        public override void DoWindowContents(Rect inRect) {
            if (Find.TickManager.TicksGame >= _nextUpdateTick) {
                UpdateDrawerList();
                _nextUpdateTick = Find.TickManager.TicksGame + 360;
            }

            Text.Font = GameFont.Tiny;
            
            var listing = new Listing_Standard {ColumnWidth = inRect.width};
            listing.Begin(inRect);

            GUI.SetNextControlName("QuickSearchField");
            _quickSearch = Widgets.TextField(new Rect(0, 0, inRect.width - 16f, 30f), _quickSearch);
            if (Time.frameCount > _openedTime + SetFocusDelay) // prevent input bind key
                UI.FocusControl("QuickSearchField", this);

            var outRect = new Rect(inRect);
            outRect.yMin += 35f;
            outRect.yMax -= 5f;
            outRect.width -= 5f;

            var linesToDraw = 0;
            var drawEntrys = string.IsNullOrEmpty(_quickSearch)
                ? _badBills
                : _badBills.Where(x => x.Bill.recipe.ProducedThingDef?.LabelCap.ToString().ToLower().Contains(_quickSearch) ?? true)
                    .ToList();

            drawEntrys.ForEach(x => linesToDraw += x.IngredientsStrs.Count + 1);

            var viewRect = new Rect(0f, 0f, outRect.width - 16f, linesToDraw * 25f/* + 100f*/);
            Widgets.BeginScrollView(outRect, ref _resultsAreaScroll, viewRect);

            var num = 0f;
            foreach (var e in drawEntrys) {
                if (e.ProducedThingDef != null)
                    Widgets.ThingIcon(new Rect(0, num, 23f, 23f), e.ProducedThingDef);

                var billRect = new Rect(25f, num, viewRect.width - 64f - 26f, 23f);
                var mainLineStyle = new GUIStyle(GUI.skin.label) {normal = {textColor = Color.cyan}};

                GUI.Label(billRect, e.BillName, mainLineStyle);
                billRect.x = billRect.xMax;
                billRect.width = 23f;

                if (Widgets.ButtonText(billRect, "W")) {
                    CameraJumper.TryJump(e.Building);
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(e.Building);
                }

                billRect.x = billRect.xMax;
                if (Widgets.ButtonText(billRect, "B"))
                    Find.WindowStack.Add(new Dialog_BillConfig(e.Bill, ((Thing) e.Bill.billStack.billGiver).Position));
                num += 25f;

                foreach (var ingredient in e.IngredientsStrs) {
                    var ingRect = new Rect(0f, num, viewRect.width * 1f, 23f);
                    GUI.Label(ingRect, ingredient);
                    num += 25f;
                }
            }

            Widgets.EndScrollView();

            listing.End();
        }

        private void UpdateDrawerList() {
            _badBills.Clear();

            foreach (var thing in Find.CurrentMap.listerThings.ThingsMatching(
                ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver))) {
                if (thing is IBillGiver billGiver)
                    foreach (var bill in billGiver.BillStack.Bills) {
                        var billProduction = bill as Bill_Production;
                        if (billProduction == null || billProduction.suspended || !billProduction.ShouldDoNow() ||
                            billProduction.repeatMode == BillRepeatModeDefOf.Forever)
                            continue;

                        var notAllowedCounts = BillRequirementsMod.GetNotAllowedCount(bill).ToList();
                        if (notAllowedCounts.Any())
                            _badBills.Add(new BadBill {
                                Building = thing,
                                Bill = billProduction,
                                ProducedThingDef = billProduction.recipe.ProducedThingDef,
                                BillName = $"{thing.LabelCap}: {bill.LabelCap}",
                                NotResolvedIngs = notAllowedCounts
                            });
                    }
            }
        }

        //private List<BadBill> FilterByProducedName(string name) {

        //}

        private class BadBill {
            public Bill_Production Bill;
            public string BillName;
            public Thing Building;
            public ThingDef ProducedThingDef;

            public List<Pair<IngredientCount, float>> NotResolvedIngs;

            public List<string> IngredientsStrs {
                get {
                    var result = new List<string>();

                    foreach (var ing in NotResolvedIngs)
                        result.Add($"    {ing.First.Summary} ({ing.Second}/{ing.First.GetBaseCount()})");

                    return result;
                }
            }
        }
    }
}