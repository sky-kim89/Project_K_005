using System;
using UnityEngine;

namespace SoulMercenaries
{
    // One mercenary of the guild's roster, always the same person: a job (the template), a grade (일반 · 정예 · 전설),
    // a title and a concept, what it has on top of the job (stats, growth, patterns, skills, its own trait) and how
    // it looks when nothing covers it. Founders (창단 멤버) start in the company and are never offered.
    [CreateAssetMenu(menuName = "Soul Mercenaries/Recruit")]
    public sealed class SoulRecruitData : ScriptableObject
    {
        [Tooltip("Kept on the mercenary (its save): never change it once people have been hired.")]
        public string Id;
        public string DisplayName;
        [Tooltip("The job: base stats, kit, race and tendencies come from it.")]
        public SoulMercenaryData Template;
        [Tooltip("Its own race instead of the job's (엘프 검사, 드워프 길잡이 …). Empty: the job's.")]
        public SoulRaceData Race;
        [Tooltip("Gold on top of (or off, negative) what the guild asks for its grade (탁발승 톰: cheaper).")]
        public int PriceShift;
        public SoulStyleRarity Rarity;
        [Tooltip("창단 멤버: in the company from the first day, never on the hire list.")]
        public bool Founder;
        [Tooltip("칭호, shown with the name (검성의 제자 …). Empty: the job.")]
        public string Title;
        [TextArea(2, 4)] public string Concept;
        [Tooltip("Its own little difference: +1 to this stat …")]
        public StatType Up = StatType.Strength;
        [Tooltip("… and −1 to this one (the same stat: none).")]
        public StatType Down = StatType.Strength;
        [Tooltip("On top of the job's base stats.")]
        public SoulStatBonus[] Stats = Array.Empty<SoulStatBonus>();
        [Tooltip("On top of the job's level-up tendencies.")]
        public SoulStatBonus[] Growth = Array.Empty<SoulStatBonus>();
        public SoulPatternData[] Patterns = Array.Empty<SoulPatternData>();
        public SoulActiveSkillData[] Actives = Array.Empty<SoulActiveSkillData>();
        [Tooltip("The first one is its 고유 특성 (shown apart); the rest come with its concept.")]
        public SoulPassiveSkillData[] Passives = Array.Empty<SoulPassiveSkillData>();
        [Tooltip("A healer that fights in the front (전투 사제): it goes in with its weapon instead of waiting behind.")]
        public bool Frontline;
        [Tooltip("Its own weapon in place of the job's (야크: 권갑). Empty: the job's.")]
        public SoulEquipmentData Weapon;
        [Tooltip("Hair (with colour), eyes, mask, cape — and the outfit it wears under no armour.")]
        public SoulAppearancePatch Look;

        public string Job => Template != null ? Template.Job : "";
        public string ShownTitle => string.IsNullOrEmpty(Title) ? Job : Title;
        public SoulPassiveSkillData Signature => Rarity != SoulStyleRarity.Normal && Passives != null && Passives.Length > 0 ? Passives[0] : null;
    }
}
