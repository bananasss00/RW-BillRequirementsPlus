using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BillRequirementsPlus
{
    [DefOf]
    public static class BRPDefOf
    {
        public static KeyBindingDef BRPWindowOpen;
    }

    public class BRPComponent : GameComponent
    {
        public BRPComponent() { }
        public BRPComponent(Game game) { this.game = game; }

        public override void GameComponentOnGUI()
        {
            if (BRPDefOf.BRPWindowOpen != null && BRPDefOf.BRPWindowOpen.IsDownEvent)
            {
                if (Find.WindowStack.Windows.Count(window => window is BRPWindow) <= 0)
                {
                    Find.WindowStack.Add(new BRPWindow());
                }
            }
        }

        public Game game;
    }

    public class BRPWindow : Window
    {
        int nextUpdateTick;
        Vector2 resultsAreaScroll;
        List<DrawEntry> toDraw;

        public override Vector2 InitialSize { get => new Vector2(640f, 480f); }

        public BRPWindow()
        {
            optionalTitle = "BillRequirementsPlus";
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            draggable = true;
            doCloseX = true;
            nextUpdateTick = 0;
            toDraw = new List<DrawEntry>();
        }

        class DrawEntry
        {
            public Thing building;
            public Bill_Production bill;
            public string billStr;
            public List<string> ingredientsStrs;
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            if (Find.TickManager.TicksGame >= nextUpdateTick)
            {
                UpdateDrawerList();
                nextUpdateTick = Find.TickManager.TicksGame + 360;
            }

            Text.Font = GameFont.Tiny;

            Listing_Standard listing = new Listing_Standard();
            listing.ColumnWidth = inRect.width/* / 2.1f*/;
            listing.Begin(inRect);

            Rect outRect = new Rect(inRect);
            outRect.yMin += 5f;
            outRect.yMax -= 5f;
            outRect.width -= 5f;

            int linesToDraw = 0;
            toDraw.ForEach(x => linesToDraw += x.ingredientsStrs.Count + 1);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, (float)linesToDraw * 25f + 100f);
            Widgets.BeginScrollView(outRect, ref resultsAreaScroll, viewRect);

            float num = 0f;
            foreach (DrawEntry e in toDraw)
            {
                Rect billRect = new Rect(0f, num, viewRect.width * 0.90f, 23f);
                GUIStyle mainLineStyle = new GUIStyle(GUI.skin.label);
                mainLineStyle.normal.textColor = Color.cyan;

                GUI.Label(billRect, e.billStr, mainLineStyle);
                billRect.x = billRect.xMax;
                billRect.width = viewRect.width * 0.05f;

                if (Widgets.ButtonText(billRect, "W"))
                {
                    CameraJumper.TryJump(e.building);
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(e.building);
                }
                billRect.x = billRect.xMax;
                if (Widgets.ButtonText(billRect, "B"))
                {
                    Find.WindowStack.Add(new Dialog_BillConfig(e.bill, ((Thing)e.bill.billStack.billGiver).Position));
                }
                num += 25f;
                
                foreach (var ingredient in e.ingredientsStrs)
                {
                    Rect ingRect = new Rect(0f, num, viewRect.width * 1f, 23f);
                    GUI.Label(ingRect, ingredient);
                    num += 25f;
                }
            }

            Widgets.EndScrollView();

            listing.End();
        }

        private void UpdateDrawerList()
        {
            toDraw.Clear();

            foreach (var thing in Find.CurrentMap.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver)))
            {
                IBillGiver billGiver = thing as IBillGiver;
                if (billGiver != null)
                {
                    foreach (var bill in billGiver.BillStack.Bills)
                    {
                        if (bill.suspended || !bill.ShouldDoNow() || ((Bill_Production)bill).repeatMode == BillRepeatModeDefOf.Forever)
                            continue;

                        IEnumerable<Pair<IngredientCount, float>> notAllowedCounts = BillRequirementsMod.GetNotAllowedCount(bill);
                        if (notAllowedCounts.ToList().Count > 0)
                        {
                            var drawEntry = new DrawEntry
                            {
                                building = thing,
                                bill = (Bill_Production)bill,
                                billStr = $"{thing.LabelCap}: {bill.LabelCap}",
                                ingredientsStrs = new List<string>()
                            };
                            
                            foreach (var notAllowedCount in notAllowedCounts)
                            {
                                drawEntry.ingredientsStrs.Add($"    {notAllowedCount.First.Summary} ({notAllowedCount.Second}/{notAllowedCount.First.GetBaseCount()})");
                            }

                            toDraw.Add(drawEntry);
                        }
                    }
                }
            }
        }
    }
}