using Godot;
using AutoEvo;
using System.Threading.Tasks;

namespace Thrive.OverseerMod
{
    public partial class OverseerAutoEvoController : Node
    {
        public MicrobeStage Stage { get; set; } = null!;
        private AutoEvoRun? currentRun;

        public override void _Ready()
        {
            // Allow this node to process even when the scene tree is paused
            ProcessMode = ProcessModeEnum.Always;
        }

        public void TriggerAutoEvo()
        {
            if (currentRun != null && !currentRun.Finished)
            {
                Stage.HUD.HUDMessages.ShowMessage("Auto-Evo is already running!", DisplayDuration.Short);
                return;
            }

            Stage.HUD.HUDMessages.ShowMessage("Calculating Auto-Evo Generation...", DisplayDuration.Long);
            
            // Freeze the physics and simulation loop
            GetTree().Paused = true;

            var globalCache = new AutoEvoGlobalCache(Stage.GameWorld.WorldSettings);
            currentRun = AutoEvo.AutoEvo.CreateRun(Stage.GameWorld, globalCache);
            currentRun.FullSpeed = true;
            
            currentRun.Start();
        }

        public override void _Process(double delta)
        {
            if (currentRun != null)
            {
                if (currentRun.Finished)
                {
                    try
                    {
                        if (currentRun.WasSuccessful)
                        {
                            currentRun.CalculateAndApplyFinalExternalEffectSizes();
                            currentRun.ApplyAllResults(true);

                            // Force a flush of the spawner queues to load the new patch demographic
                            var currentPatch = Stage.GameWorld.Map.CurrentPatch;
                            if (currentPatch != null)
                            {
                                Stage.PatchManager.UpdateSpawners(currentPatch, Stage);
                                Stage.HUD.UpdateEnvironmentalBars(currentPatch.Biome);
                            }

                            Stage.HUD.HUDMessages.ShowMessage("Auto-Evo Generation Complete!", DisplayDuration.Short);
                        }
                        else
                        {
                            Stage.HUD.HUDMessages.ShowMessage("Auto-Evo Failed!", DisplayDuration.Short);
                        }
                    }
                    catch (System.Exception e)
                    {
                        GD.PrintErr($"Auto-Evo error: {e}");
                        Stage.HUD.HUDMessages.ShowMessage("Auto-Evo encountered an error!", DisplayDuration.Short);
                    }
                    finally
                    {
                        // ALWAYS unpause no matter what happened
                        GetTree().Paused = false;
                        currentRun = null;
                    }
                }
            }
        }
    }
}
