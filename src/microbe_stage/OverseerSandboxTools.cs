using Godot;
using Arch.Core;
using Arch.Core.Extensions;
using System.Linq;
using Components;
using System;

namespace Thrive.OverseerMod
{
    public partial class OverseerSandboxTools : Node
    {
        public MicrobeStage Stage { get; set; } = null!;
        private Compound currentPaintCompound = Compound.Glucose;

        public override void _UnhandledInput(InputEvent @event)
        {
            var overseer = Stage.GetNodeOrNull<OverseerCamera>("OverseerCamera");
            if (overseer == null || !overseer.IsActive) return;
            if (PauseManager.Instance.Paused) return;

            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.IsEcho())
            {
                if (keyEvent.Keycode == Key.U)
                {
                    CyclePaintCompound();
                }

                if (keyEvent.Keycode == Key.P)
                {
                    PaintEnvironment(overseer.CursorWorldPos);
                }
                
                if (keyEvent.Keycode == Key.O)
                {
                    PlaceIsolationWall(overseer.CursorWorldPos);
                }

                if (keyEvent.Keycode == Key.Bracketleft)
                {
                    ChangeTimeScale(-1f);
                }
                if (keyEvent.Keycode == Key.Bracketright)
                {
                    ChangeTimeScale(1f);
                }
                if (keyEvent.Keycode == Key.T)
                {
                    // Snap toggle between 1.0 and 5.0
                    if (Stage.WorldSimulation.WorldTimeScale > 1.0f)
                        Stage.WorldSimulation.WorldTimeScale = 1.0f;
                    else
                        Stage.WorldSimulation.WorldTimeScale = 5.0f;
                        
                    Stage.HUD.HUDMessages.ShowMessage($"Time Scale: {Stage.WorldSimulation.WorldTimeScale}x", DisplayDuration.Short);
                }

                if (keyEvent.Keycode == Key.K)
                {
                    SmiteCell();
                }
                if (keyEvent.Keycode == Key.L)
                {
                    ZapCell();
                }
            }
        }

        private void CyclePaintCompound()
        {
            var clouds = SimulationParameters.Instance.GetCloudCompounds().Select(c => c.ID).ToList();
            if (clouds.Count > 0)
            {
                int index = clouds.IndexOf(currentPaintCompound);
                index = (index + 1) % clouds.Count;
                currentPaintCompound = clouds[index];
                
                var compoundDef = SimulationParameters.GetCompound(currentPaintCompound);
                Stage.HUD.HUDMessages.ShowMessage($"Selected Paint: {compoundDef.Name}", DisplayDuration.Short);
            }
        }

        private void PaintEnvironment(Vector3 pos)
        {
            float amount = 500f; 
            Stage.Clouds.AddCloud(currentPaintCompound, amount, pos);
            var compoundDef = SimulationParameters.GetCompound(currentPaintCompound);
            Stage.HUD.HUDMessages.ShowMessage($"Painted {amount} {compoundDef.Name}", DisplayDuration.Short);
        }

        private void PlaceIsolationWall(Vector3 pos)
        {
            var boxShape = new BoxShape3D();
            boxShape.Size = new Vector3(50f, 100f, 20f); // 20f thickness to prevent tunneling

            // Overlap check to prevent depenetration physics explosions
            var spaceState = Stage.GetViewport().GetWorld3D().DirectSpaceState;
            var query = new PhysicsShapeQueryParameters3D();
            query.Shape = boxShape;
            query.Transform = new Transform3D(Basis.Identity, new Vector3(pos.X, 0, pos.Z));
            query.CollideWithBodies = true;
            query.CollideWithAreas = true;
            query.CollisionMask = 0xFFFFFFFF;
            
            var intersectResult = spaceState.IntersectShape(query);
            if (intersectResult.Count > 0)
            {
                GD.PrintErr("Wall placement blocked: Cannot build over living entities.");
                Stage.HUD.HUDMessages.ShowMessage("Wall blocked (Entity overlapping)", DisplayDuration.Short);
                return;
            }

            var body = new StaticBody3D();
            body.CollisionLayer = 0xFFFFFFFF;
            body.CollisionMask = 0xFFFFFFFF;
            
            var collisionShape = new CollisionShape3D();
            collisionShape.Shape = boxShape;
            
            body.AddChild(collisionShape);
            
            // Add a MeshInstance for visibility
            var meshInstance = new MeshInstance3D();
            var boxMesh = new BoxMesh();
            boxMesh.Size = boxShape.Size;
            var material = new StandardMaterial3D();
            material.AlbedoColor = new Color(0.1f, 0.5f, 1.0f, 0.4f);
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            boxMesh.Material = material;
            meshInstance.Mesh = boxMesh;
            body.AddChild(meshInstance);
            
            body.Position = new Vector3(pos.X, 0, pos.Z);
            Stage.AddChild(body);
            
            Stage.HUD.HUDMessages.ShowMessage("Placed Isolation Wall", DisplayDuration.Short);
        }
        
        private void ChangeTimeScale(float amount)
        {
            Stage.WorldSimulation.WorldTimeScale = Mathf.Max(0.1f, Stage.WorldSimulation.WorldTimeScale + amount);
            Stage.HUD.HUDMessages.ShowMessage($"Time Scale: {Stage.WorldSimulation.WorldTimeScale}x", DisplayDuration.Short);
        }
        
        private void SmiteCell()
        {
            var target = Stage.HoverInfo.Entities.FirstOrDefault();
            if (target != default && target != Entity.Null && target.IsAliveAndHas<Health>())
            {
                if (target.Has<AttachedToEntity>())
                {
                    Stage.HUD.HUDMessages.ShowMessage("Target is already being digested!", DisplayDuration.Short);
                    return;
                }
                
                ref var health = ref target.Get<Health>();
                health.CurrentHealth = 0;
                Stage.HUD.HUDMessages.ShowMessage("Smote cell!", DisplayDuration.Short);
            }
        }
        
        private void ZapCell()
        {
            var target = Stage.HoverInfo.Entities.FirstOrDefault();
            if (target != default && target != Entity.Null && target.IsAliveAndHas<SpeciesMember>())
            {
                if (target.Has<AttachedToEntity>())
                {
                    Stage.HUD.HUDMessages.ShowMessage("Cannot zap a cell that is currently being digested!", DisplayDuration.Short);
                    return;
                }
                
                ref var speciesMember = ref target.Get<SpeciesMember>();
                var oldSpecies = speciesMember.Species;
                
                // Scramble by grabbing a random species from the current game world ecosystem
                var speciesList = Stage.GameWorld.Species.Values.ToList();
                if (speciesList.Count > 0)
                {
                    // Pick a random species that isn't this one if possible
                    var newSpecies = speciesList[Random.Shared.Next(speciesList.Count)];
                    
                    var pos = target.Has<WorldPosition>() ? target.Get<WorldPosition>().Position : Vector3.Zero;
                    bool wasPlayer = target.Has<PlayerMarker>();
                    
                    SpawnHelpers.SpawnMicrobe(Stage.WorldSimulation, Stage, newSpecies as MicrobeSpecies, pos, !wasPlayer);
                    
                    // Kill the original
                    if (target.Has<Health>())
                    {
                        ref var health = ref target.Get<Health>();
                        health.CurrentHealth = 0;
                    }
                    
                    Stage.HUD.HUDMessages.ShowMessage("Zapped cell! Mutated to new species.", DisplayDuration.Short);
                }
            }
        }
    }
}