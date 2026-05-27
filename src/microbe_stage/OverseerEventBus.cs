using Arch.Core;
using System;

namespace Thrive.OverseerMod
{
    public static class OverseerEventBus
    {
        public static event Action<Entity>? OnPlayerPossessionChanged;

        public static void EmitPlayerPossessionChanged(Entity newEntity)
        {
            OnPlayerPossessionChanged?.Invoke(newEntity);
        }
    }
}