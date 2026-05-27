using Godot;
using Arch.Core;
using Arch.Core.Extensions;
using Components;
using System;
using System.Linq;

namespace Thrive.OverseerMod
{
    public partial class OverseerCamera : Camera3D
    {
        private const float PAN_SPEED = 80f;
        private const float ZOOM_SPEED = 5f;
        private const float MIN_HEIGHT = 10f;
        private const float MAX_HEIGHT = 150f;
        
        public MicrobeStage Stage { get; set; } = null!;
        public bool IsActive { get; private set; } = false;
        public Vector3 CursorWorldPos { get; private set; } = Vector3.Zero;
        private Entity? pendingPossessionTarget = null;

        public override void _Ready()
        {
            if (Stage != null && Stage.Camera != null)
            {
                Projection = Stage.Camera.Projection;
                Fov = Stage.Camera.Fov;
                Size = Stage.Camera.Size;
                Far = Stage.Camera.Far;
                Near = Stage.Camera.Near;
            }
        }

        public void Activate()
        {
            IsActive = true;
            MakeCurrent();
            
            if (Stage.HasPlayer && Stage.Player.Has<WorldPosition>())
            {
                var pos = Stage.Player.Get<WorldPosition>().Position;
                Position = new Vector3(pos.X, Stage.Camera.Position.Y, pos.Z);
            }
            else
            {
                Position = Stage.Camera.Position;
            }
        }

        public void Deactivate()
        {
            IsActive = false;
            if (Stage.Camera != null)
            {
                Stage.Camera.SetCustomCurrentStatus(true);
            }
        }

        public override void _Process(double delta)
        {
            if (!IsActive) return;

            Vector3 velocity = Vector3.Zero;

            if (Input.IsActionPressed("ui_right")) velocity.X += 1;
            if (Input.IsActionPressed("ui_left")) velocity.X -= 1;
            if (Input.IsActionPressed("ui_down")) velocity.Z += 1;
            if (Input.IsActionPressed("ui_up")) velocity.Z -= 1;

            Position += velocity.Normalized() * PAN_SPEED * (float)delta * (Position.Y / 50f);

            if (Stage.HasPlayer && !Stage.HasAlivePlayer)
            {
                GD.Print("Possessed cell died. Safely detaching OverseerCamera.");
                Stage.SetPlayer(Entity.Null);
            }

            if (pendingPossessionTarget.HasValue)
            {
                Stage.SetPlayer(pendingPossessionTarget.Value);
                OverseerEventBus.EmitPlayerPossessionChanged(pendingPossessionTarget.Value);
                pendingPossessionTarget = null;
            }
        }
        
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!IsActive) return;
            if (PauseManager.Instance.Paused) return;
            
            if (@event is InputEventMouseButton mouseBtn)
            {
                if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
                {
                    Position = new Vector3(Position.X, Mathf.Max(MIN_HEIGHT, Position.Y - ZOOM_SPEED), Position.Z);
                }
                else if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
                {
                    Position = new Vector3(Position.X, Mathf.Min(MAX_HEIGHT, Position.Y + ZOOM_SPEED), Position.Z);
                }
                else if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed && !mouseBtn.IsEcho())
                {
                    TryPossessCell();
                }
            }
            else if (@event is InputEventMouseMotion mouseMotion)
            {
                var from = ProjectRayOrigin(mouseMotion.Position);
                var dir = ProjectRayNormal(mouseMotion.Position);
                if (dir.Y != 0)
                {
                    float t = -from.Y / dir.Y;
                    CursorWorldPos = from + dir * t;
                }
            }
        }

        private void TryPossessCell()
        {
            if (Stage.HoverInfo == null || Stage.HoverInfo.Entities == null) return;
            
            var target = Stage.HoverInfo.Entities.FirstOrDefault();
            if (target != default && target != Entity.Null && target.IsAliveAndHas<MicrobeSpeciesMember>())
            {
                if (target.Has<AttachedToEntity>())
                {
                    Stage.HUD.HUDMessages.ShowMessage("Cannot possess a cell that is currently being digested!", DisplayDuration.Short);
                    return;
                }

                var commandBuffer = Stage.WorldSimulation.StartRecordingEntityCommands();

                if (Stage.HasPlayer)
                {
                    if (Stage.Player.Has<PlayerMarker>())
                        commandBuffer.Remove<PlayerMarker>(Stage.Player);
                }
                
                // Flush MicrobeColonyMember if possessing a subordinate colony cell
                if (target.Has<MicrobeColonyMember>())
                    commandBuffer.Remove<MicrobeColonyMember>(target);

                if (!target.Has<PlayerMarker>())
                    commandBuffer.Add(target, new PlayerMarker());
                    
                Stage.WorldSimulation.FinishRecordingEntityCommands(commandBuffer);
                
                // Defer setting the player to the next _Process cycle (Main Thread)
                pendingPossessionTarget = target;
                
                GD.Print($"Queued possession of cell: {target.Id}");
                Stage.HUD.HUDMessages.ShowMessage("Possessing cell...", DisplayDuration.Short);
            }
        }
    }
}