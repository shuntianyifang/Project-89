using System.Linq;
using System.Threading.Tasks;
using Godot;
using ColdWarWargame.Systems.Gameplay;

namespace ColdWarWargame.Tests.Gameplay
{
    public static class HudLifecycleTests
    {
        public static async Task<int> RunAll(Node root)
        {
            int fails = 0;
            void Check(bool ok, string message)
            {
                if (ok) GD.Print("[HUD LIFECYCLE PASS] " + message);
                else { fails++; GD.PrintErr("[HUD LIFECYCLE FAIL] " + message); }
            }

            foreach (bool resultDialog in new[] { false, true })
            {
                var canvas = new CanvasLayer();
                root.AddChild(canvas);
                var hud = new GameHud(canvas, () => { }, () => { });
                hud.Initialize();
                int restarts = 0;
                hud.ConfigureCampaignActions(() => { }, () =>
                {
                    restarts++;
                    root.RemoveChild(canvas);
                    canvas.QueueFree();
                }, () => { }, () => { });
                var dialog = canvas.GetChildren().OfType<AcceptDialog>().Single(d =>
                    resultDialog ? d.Title.Contains("结算") : d.Title == "重新开局");
                void Request()
                {
                    if (resultDialog) dialog.EmitSignal(AcceptDialog.SignalName.CustomAction, "restart");
                    else dialog.EmitSignal(AcceptDialog.SignalName.Confirmed);
                }
                Request();
                Request();
                Check(dialog.IsInsideTree() && restarts == 0,
                    $"{dialog.Title}: input callback keeps the dialog in the tree");
                await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
                await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(restarts == 1, "Repeated restart signals rebuild exactly once after dispatch");
            }

            // A pending callback must not revive a HUD whose session was shut down.
            var abandonedCanvas = new CanvasLayer();
            root.AddChild(abandonedCanvas);
            var abandonedHud = new GameHud(abandonedCanvas, () => { }, () => { });
            int staleRestarts = 0;
            abandonedHud.ConfigureCampaignActions(() => { }, () => staleRestarts++, () => { }, () => { });
            abandonedCanvas.GetChildren().OfType<ConfirmationDialog>().First()
                .EmitSignal(AcceptDialog.SignalName.Confirmed);
            root.RemoveChild(abandonedCanvas);
            abandonedCanvas.QueueFree();
            await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
            await root.ToSignal(root.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(staleRestarts == 0, "Shutdown cancels a pending HUD restart");
            return fails;
        }
    }
}
