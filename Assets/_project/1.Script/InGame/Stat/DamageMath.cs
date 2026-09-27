using UnityEngine;

// ============================================================
//  DamageMath.cs
//  방어율(내성) 적용 공식 — 소프트캡을 넘는 방어율은 overflowRate 만큼만 인정하고,
//  최종 방어율에서 관통을 뺀다 (관통이 소프트캡 공식을 거꾸로 타고 증폭되지 않게).
// ============================================================

public static class DamageMath
{
    /// <param name="pierce">공격자의 방어율 관통 (0~1). 소프트캡·상한을 모두 적용한 최종 방어율에서 뺀다.</param>
    public static float AfterDefense(float rawDamage, float rawDefense, float pierce,
                                     float softCap, float overflowRate, float effectiveCap)
    {
        float eff = rawDefense <= softCap ? rawDefense : softCap + (rawDefense - softCap) * overflowRate;
        float defense = Mathf.Min(eff, effectiveCap);
        defense = Mathf.Max(0f, defense - Mathf.Clamp01(pierce));
        return Mathf.Max(rawDamage * (1f - defense), 1f);
    }
}
