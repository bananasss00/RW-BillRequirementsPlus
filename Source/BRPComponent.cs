using System.Linq;
using Verse;

namespace BillRequirementsPlus {
    public class BRPComponent : GameComponent {
        public Game game;

        public BRPComponent() {
        }

        public BRPComponent(Game game) {
            this.game = game;
        }

        public override void GameComponentOnGUI() {
            if (BRPDefOf.BRPWindowOpen != null && BRPDefOf.BRPWindowOpen.IsDownEvent)
                if (Find.WindowStack.Windows.Count(window => window is BRPWindow) <= 0)
                    Find.WindowStack.Add(new BRPWindow());
        }
    }
}