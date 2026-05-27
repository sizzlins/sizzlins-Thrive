using Godot;
using System;
using System.Linq;

namespace Thrive.OverseerMod
{
    public partial class DynamicSpawner : Node
    {
        public MicrobeStage Stage { get; set; } = null!;

        public override void _UnhandledInput(InputEvent @event)
        {
            var overseer = Stage.GetNodeOrNull<OverseerCamera>("OverseerCamera");
            if (overseer == null || !overseer.IsActive) return;

            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.IsEcho())
            {
                if (keyEvent.Keycode == Key.Key1) SpawnSpecies(0);
                if (keyEvent.Keycode == Key.Key2) SpawnSpecies(1);
                if (keyEvent.Keycode == Key.Key3) SpawnSpecies(2);
                if (keyEvent.Keycode == Key.Key4) SpawnSpecies(3);
                if (keyEvent.Keycode == Key.Key5) SpawnSpecies(4);
            }
        }

        private void SpawnSpecies(int index)
        {
            var overseer = Stage.GetNodeOrNull<OverseerCamera>("OverseerCamera");
            if (overseer == null) return;

            var speciesList = Stage.GameWorld.Species.Values.ToList();
            if (speciesList.Count == 0) return;

            var speciesToSpawn = speciesList[index % speciesList.Count];
            var spawnLocation = overseer.CursorWorldPos;

            SpawnHelpers.SpawnMicrobe(Stage.WorldSimulation, Stage, speciesToSpawn, spawnLocation, true);
            GD.Print($"God-Mode Spawned: {speciesToSpawn.FormattedIdentifier} at {spawnLocation}");
            
            // Pop up a notice message on the HUD (simulating God-mode feedback)
            Stage.HUD.HUDMessages.ShowMessage($"Spawned {speciesToSpawn.FormattedName}", DisplayDuration.Short);
        }
    }
}