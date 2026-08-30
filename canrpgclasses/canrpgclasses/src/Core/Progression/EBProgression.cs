using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using canrpgclasses.Core.Talents;
using canrpgclasses.Core.Config;

namespace canrpgclasses.Core.Progression
{
    /// <summary>
    /// XP and level, one point of talent per level. Both are stored under class-namespaced keys, so switching
    /// class swaps to that class's own progress rather than carrying one shared level around.
    /// </summary>
    public class EBProgression : EntityBehavior
    {
        public const string Name = "canrpgprogression";

        private string XpKey => Core.AttrKeys.Xp(TalentState.CurrentClass(entity));
        private string LevelKey => Core.AttrKeys.Level(TalentState.CurrentClass(entity));

        // Config-driven, with the shipped defaults inline. Read live rather than cached: these are looked up on
        // level-up and by the character sheet, not on a hot path, and an admin's edit should apply at once.
        //
        // The default of 24 points at cap is about one full tree plus a real dip, so a build commits to a main
        // tree instead of filling two. Tune alongside tree size.
        public const int DefaultMaxLevel = 25;

        public static int MaxLevel => (int)BalanceConfig.Global("maxLevel", DefaultMaxLevel);
        public static int PointsPerLevel => (int)BalanceConfig.Global("talentPointsPerLevel", 1f);
        /// <summary>XP awarded per point of the victim's max health.</summary>
        public static float XpPerHealth => BalanceConfig.Global("xpPerHealth", 2f);

        /// <summary>The XP curve: <c>xpCurveBase * (level-1) ^ xpCurveExponent</c>. Both ends are config, so a
        /// server can flatten or steepen levelling without a rebuild.</summary>
        public static float XpCurveBase => BalanceConfig.Global("xpCurveBase", 20f);
        public static float XpCurveExponent => BalanceConfig.Global("xpCurveExponent", 1.6f);

        public EBProgression(Entity entity) : base(entity) { }
        public override string PropertyName() => Name;

        private bool IsServer => entity.Api.Side == EnumAppSide.Server;

        public long Xp
        {
            get => entity.WatchedAttributes.GetLong(XpKey, 0);
            private set => entity.WatchedAttributes.SetLong(XpKey, value);
        }

        public int Level
        {
            get => Math.Max(1, entity.WatchedAttributes.GetInt(LevelKey, 1));
            private set => entity.WatchedAttributes.SetInt(LevelKey, value);
        }

        /// <summary>Total talent points the character has earned from leveling (level 1 grants none).</summary>
        public int TotalTalentPoints => (Level - 1) * PointsPerLevel;

        /// <summary>Total XP required to reach the given level (from 0).</summary>
        public static long XpForLevel(int level)
        {
            if (level <= 1) return 0;
            return (long)(XpCurveBase * Math.Pow(level - 1, XpCurveExponent));
        }

        public long XpIntoLevel => Xp - XpForLevel(Level);
        public long XpForNextLevel => Level >= MaxLevel ? 0 : XpForLevel(Level + 1) - XpForLevel(Level);

        public void AddXp(long amount)
        {
            if (!IsServer || amount <= 0 || Level >= MaxLevel) return;
            Xp += amount;
            RecalcLevel();
        }

        /// <summary>Debug: jump directly to a level (XP set to that level's threshold).</summary>
        public void SetLevel(int level)
        {
            if (!IsServer) return;
            level = GameMath.Clamp(level, 1, MaxLevel);
            Xp = XpForLevel(level);
            Level = level;
            entity.GetBehavior<canrpgclasses.Core.EB.EBTalents>()?.ReapplyStatTalents();
        }

        private void RecalcLevel()
        {
            int newLevel = Level;
            while (newLevel < MaxLevel && Xp >= XpForLevel(newLevel + 1)) newLevel++;
            if (newLevel == Level) return;

            Level = newLevel;
            entity.GetBehavior<canrpgclasses.Core.EB.EBTalents>()?.ReapplyStatTalents(); // re-derive level-scaled stats (HpPerLevel)
            if ((entity as EntityPlayer)?.Player is IServerPlayer sp)
            {
                sp.SendMessage(GlobalConstants.GeneralChatGroup,
                    Lang.Get("canrpgclasses:msg-levelup", newLevel, TotalTalentPoints),
                    EnumChatType.Notification);
            }
        }
    }
}
