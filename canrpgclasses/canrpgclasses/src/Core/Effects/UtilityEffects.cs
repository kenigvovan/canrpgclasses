using effectshud.src;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace canrpgclasses.Core.Effects
{
    /// <summary>Utility buffs whose mechanic runs on clients (light, fall speed, water walking): the effect only
    /// raises a synced WatchedAttributes flag that <c>UtilityBuffsClient</c> reads.</summary>
    public static class UtilityEffectIds
    {
        public const string MagicLight = "canrpg_magic_light";
        public const string SlowFall = "canrpg_slow_fall";
        public const string WaterWalk = "canrpg_water_walk";
    }

    public static class UtilityFlags
    {
        public const string MagicLight = "canrpgMagicLight";
        public const string SlowFall = "canrpgSlowFall";
        public const string WaterWalk = "canrpgWaterWalk";
    }

    /// <summary>Holds a WatchedAttributes flag while active. Re-asserted each tick, so a relog keeps it.</summary>
    public abstract class FlagEffect : Effect
    {
        protected abstract string Flag { get; }

        private void Set(bool on)
        {
            var wa = entity.WatchedAttributes;
            if (on && !wa.GetBool(Flag)) wa.SetBool(Flag, true);
            else if (!on && wa.HasAttribute(Flag)) wa.RemoveAttribute(Flag);
        }

        public override void OnStart() => Set(true);
        public override void OnTick() => Set(true);
        public override void OnExpire() => Set(false);
        public override bool OnDeath() { Set(false); return base.OnDeath(); }
    }

    [EffectRegistration(UtilityEffectIds.MagicLight)]
    public class MagicLightEffect : FlagEffect
    {
        protected override string Flag => UtilityFlags.MagicLight;
    }

    /// <summary>The client caps the fall speed; the server also drops fall damage in case a landing slips through.</summary>
    [EffectRegistration(UtilityEffectIds.SlowFall)]
    public class SlowFallEffect : FlagEffect
    {
        protected override string Flag => UtilityFlags.SlowFall;

        public override void OnShouldEntityReceiveDamage(ref float damage, DamageSource dmgSource)
        {
            if (dmgSource.Source == EnumDamageSource.Fall) damage = 0f;
        }
    }

    /// <summary>Water walking (client) plus water breathing: oxygen is topped up every tick.</summary>
    [EffectRegistration(UtilityEffectIds.WaterWalk)]
    public class WaterWalkEffect : FlagEffect
    {
        protected override string Flag => UtilityFlags.WaterWalk;

        public override void OnTick()
        {
            base.OnTick();
            var breathe = entity.GetBehavior<EntityBehaviorBreathe>();
            if (breathe != null && breathe.Oxygen < breathe.MaxOxygen) breathe.Oxygen = breathe.MaxOxygen;
        }
    }
}
