using Arch.Core;
using System;

namespace Thrive.OverseerMod
{
    public static class OverseerEventBus
    {
        public static event Action<Entity>? OnPlayerPossessionChanged;
        public static event Action<string>? OnOverseerToolChanged;

        public static void EmitPlayerPossessionChanged(Entity newEntity)
        {
            OnPlayerPossessionChanged?.Invoke(newEntity);
        }
        
        public static void EmitOverseerToolChanged(string toolName)
        {
            OnOverseerToolChanged?.Invoke(toolName);
        }
    }
}