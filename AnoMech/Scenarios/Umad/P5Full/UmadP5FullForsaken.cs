using System.Numerics;
using AnoMech.Core.Game;
using ForsakenIds = AnoMech.Scenarios.Umad.UmadP5ForsakenNull.Constants;

namespace AnoMech.Scenarios.Umad.P5Full;

internal sealed partial class UmadP5FullRun
{
    private void ScheduleForsaken(float start)
    {
        RunKefkaForsaken(start);
        Run_ForsakenPuddles(start);
        Run_ForsakenBonds(start);
        Run_InstanceEvents(start);
        Run_PlayerLockons(start);
        world.Events.Add(start + 47.809f, () => kefka?.Cast(ForsakenIds.ActionId.ForsakenNull));
    }

    private void ForsakenDirectorUpdate(uint step)
    {
        if (kefka is { } boss)
            world.Map.DirectorUpdate(0x80000027U, step, 0x2U, 0x1BDBU, boss.EntityId);
    }
    private void Run_InstanceEvents(float start)
    {

        world.Events.Add(start + 3.13f, () => ForsakenDirectorUpdate(0x28U));

        world.Events.Add(start + 15.20f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x1D));

        world.Events.Add(start + 18.85f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x1C));

        world.Events.Add(start + 18.85f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x14));

        world.Events.Add(start + 21.30f, () => ForsakenDirectorUpdate(0x29U));

        world.Events.Add(start + 23.40f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x1E));

        world.Events.Add(start + 27.05f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x19));

        world.Events.Add(start + 27.05f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x1A));

        world.Events.Add(start + 29.46f, () => ForsakenDirectorUpdate(0x2AU));

        world.Events.Add(start + 31.55f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x1F));

        world.Events.Add(start + 35.20f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x16));

        world.Events.Add(start + 35.20f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x1B));

        world.Events.Add(start + 37.61f, () => ForsakenDirectorUpdate(0x2BU));

        world.Events.Add(start + 39.70f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x20));

        world.Events.Add(start + 43.35f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x18));

        world.Events.Add(start + 43.35f, () => world.Map.AddEffect(packetFlags: 0x00200010U, index: (byte)0x15));

        world.Events.Add(start + 48.61f, () => world.Map.AddEffect(packetFlags: 0x00100008U, index: (byte)0x21));

        world.Events.Add(start + 48.61f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: (byte)0x1E));

        world.Events.Add(start + 48.61f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: (byte)0x1F));

        world.Events.Add(start + 48.61f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: (byte)0x1D));

        world.Events.Add(start + 48.61f, () => world.Map.AddEffect(packetFlags: 0x00080004U, index: (byte)0x20));

        world.Events.Add(start + 52.13f, () => ForsakenDirectorUpdate(0x2CU));
    }

    private void Run_PlayerLockons(float start)
    {

        world.Events.Add(start + 13.20f, () => party.Player?.AttachLockonVfx(ForsakenIds.LockonId.X_A1));

        world.Events.Add(start + 21.39f, () => party.Player?.AttachLockonVfx(ForsakenIds.LockonId.X_A1));

        world.Events.Add(start + 29.55f, () => party.Player?.AttachLockonVfx(ForsakenIds.LockonId.X_A1));

        world.Events.Add(start + 37.69f, () => party.Player?.AttachLockonVfx(ForsakenIds.LockonId.X_A1));
    }

    private void RunKefkaForsaken(float start)
    {


        world.Events.Add(start + 3.21f, () => kefka?.Cast(ForsakenIds.ActionId.Forsaken));
        world.Events.Add(start + 13.20f, () => kefka?.AddStatus(ForsakenIds.StatusId.DirectionalDisregard));
        world.Events.Add(start + 21.39f, () => kefka?.Cast(ForsakenIds.ActionId.Forsaken_BB36));
        world.Events.Add(start + 29.55f, () => kefka?.Cast(ForsakenIds.ActionId.Forsaken_BB36));
        world.Events.Add(start + 37.69f, () => kefka?.Cast(ForsakenIds.ActionId.Forsaken_BB36));
    }

    private void Run_ForsakenPuddles(float start)
    {
        world.Events.Add(start + 13.28f, () => Puddles(new Vector3(0f, 0f, 13.5f), new Vector3(-13.5f, 0f, 13.5f), Vector3.Zero));
        world.Events.Add(start + 21.48f, () => Puddles(new Vector3(-7f, 0f, 6.75f), new Vector3(13.5f, 0f, 13.5f), new Vector3(13.5f, 0f, -13.5f)));
        world.Events.Add(start + 29.63f, () => Puddles(new Vector3(-6.75f, 0f, -6.75f), new Vector3(0f, 0f, -13.5f), new Vector3(-13.5f, 0f, -13.5f)));
        world.Events.Add(start + 37.78f, () => Puddles(new Vector3(7f, 0f, -6.75f), new Vector3(0f, 0f, 13.5f), new Vector3(13.5f, 0f, 0f)));
    }

    private void Puddles(Vector3 forsaken, Vector3 ground1, Vector3 ground2)
    {
        kefka?.CastFromHelper(ForsakenIds.ActionId.Forsaken_BB38, at: new Placement(forsaken, 0f));
        kefka?.CastFromHelper(ForsakenIds.ActionId.ForsakenGround, at: new Placement(ground1, 0f));
        kefka?.CastFromHelper(ForsakenIds.ActionId.ForsakenGround, at: new Placement(ground2, 0f));
    }

    private void Run_ForsakenBonds(float start)
    {
        world.Events.Add(start + 18.32f, () => kefka?.CastFromHelper(ForsakenIds.ActionId.ForsakenBonds, party.Player));
        world.Events.Add(start + 26.47f, () => kefka?.CastFromHelper(ForsakenIds.ActionId.ForsakenBonds, party.Player));
        world.Events.Add(start + 34.62f, () => kefka?.CastFromHelper(ForsakenIds.ActionId.ForsakenBonds, party.Player));
        world.Events.Add(start + 42.78f, () => kefka?.CastFromHelper(ForsakenIds.ActionId.ForsakenBonds, party.Player));
    }
}
