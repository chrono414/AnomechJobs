using System;
using System.Numerics;
using AnoMech.Core.Native.Implementations.Interop;
using AnoMech.Core.Native.Implementations.Pointers;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace AnoMech.Core.Native.Implementations;

internal static unsafe class AreaObjects
{
    private const uint FirstEntityId = 0x4000FC00;
    private const uint LastEntityId = FirstEntityId + 0xFF;
    private static uint nextEntityId = FirstEntityId;

    public static IDisposable? Spawn(uint baseId, uint ownerId, float radius, Vector3 worldPosition)
    {
        var objects = GameObjectManager.Instance();
        if (objects == null || ownerId is 0 or 0xE0000000) return null;

        for (var i = FirstEntityId; i <= LastEntityId; i++)
        {
            var entityId = nextEntityId;
            nextEntityId = entityId == LastEntityId ? FirstEntityId : entityId + 1;
            if (objects->Objects.GetObjectByEntityId(entityId) != null) continue;

            var packet = BuildPacket(baseId, entityId, ownerId, radius, worldPosition);
            if (EventObjectHelper.Create(&packet, out var slot, out var obj)
                && obj->EntityId == entityId && obj->ObjectKind == ObjectKind.AreaObject)
                return new AreaObjectHandle(slot, entityId);

            DiagnosticLog.Warn($"[AreaObjects] Failed to spawn area object {baseId} for {ownerId:X8}.");
            return null;
        }
        return null;
    }

    internal static SpawnObjectPacket BuildPacket(uint baseId, uint entityId, uint ownerId, float radius, Vector3 worldPosition)
        => new()
        {
            ObjectIndex = byte.MaxValue,
            ObjectKind = (byte)ObjectKind.AreaObject,
#pragma warning disable CS0618
            TargetableStatus = 4,
#pragma warning restore CS0618
            BaseId = baseId,
            EntityId = entityId,
            OwnerId = ownerId,
            Radius = radius,
            Rotation = 0x7FFF,
            PositionX = worldPosition.X,
            PositionY = worldPosition.Y,
            PositionZ = worldPosition.Z,
        };

    private sealed class AreaObjectHandle(int slot, uint entityId) : IDisposable
    {
        public void Dispose()
        {
            var manager = EventObjectManager.Instance();
            if (manager == null) return;
            var obj = EventObjectManagerPointers.GetEventObjectByIndex(manager, (uint)slot);
            // The engine may already have removed the object or reused its slot.
            if (obj == null || obj->EntityId != entityId || obj->ObjectKind != ObjectKind.AreaObject) return;
            byte index = (byte)slot;
            PacketDispatcherPointers.HandleDespawnObjectPacket(0, &index);
        }
    }
}
