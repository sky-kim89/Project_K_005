using System;
using System.Collections.Generic;
using SoulMercenaries;
using UnityEditor;
using UnityEngine;
using G = IconArt.Glyph;
using B = IconArt.Bg;
using F = IconArt.Frame;
using D = IconArt.Badge;

// Editor-only builders for Soul Mercenaries presentation assets:
//  · icons drawn with the original IconArt kit (stat / status / pattern category / UI / skill / passive)
//  · the SoulIconSet asset in Resources (panel / button frames); icons go into SpriteManager's atlas
//  · PopupBase prefabs in 2.Prefabs/UI (PopupManagerEditor picks them up like the original popups)
//  · a large procedural ruins map (rooms + 2-wide corridors, always connected)
public static class SoulMercenariesAssetBuilder
{
    const string IconDir = "Assets/_project/3.Textures/Icons/SoulMercenaries";
    const string IconSetPath = "Assets/Resources/SoulIconSet.asset";
    const string PopupDir = "Assets/_project/2.Prefabs/UI";
    const int IconSize = 48;

    struct Spec
    {
        public string Key;
        public G Glyph;
        public string Hex;
        public B Back;
        public F Frame;
        public D Badge;
        public Spec(string key, G glyph, string hex, B back, F frame, D badge = D.None)
        { Key = key; Glyph = glyph; Hex = hex; Back = back; Frame = frame; Badge = badge; }
    }

    static readonly Spec[] Specs =
    {
        // upper stats
        new Spec("stat:Strength", G.Fist, "E0533C", B.Radial, F.Round),
        new Spec("stat:Vitality", G.Heart, "58C85A", B.Radial, F.Round),
        new Spec("stat:Agility", G.Boot, "58B8E8", B.Radial, F.Round),
        new Spec("stat:Magic", G.Spiral, "A070F0", B.Radial, F.Round),
        new Spec("stat:Will", G.Eye, "E8C860", B.Radial, F.Round),
        new Spec("stat:Luck", G.Coin, "F0D040", B.Radial, F.Round),
        new Spec("stat:Regeneration", G.Cross, "70E090", B.Radial, F.Round),
        new Spec("stat:Durability", G.Shield, "A0A8B8", B.Radial, F.Round),
        new Spec("stat:AntiMagic", G.Aura, "E060D0", B.Radial, F.Round),
        new Spec("stat:Recovery", G.Potion, "40D0D0", B.Radial, F.Round),
        new Spec("stat:SlashPower", G.Sword, "F04848", B.Radial, F.Round),
        new Spec("stat:ImpactPower", G.Anvil, "F09030", B.Radial, F.Round),
        new Spec("stat:PiercePower", G.Arrows, "D0D050", B.Radial, F.Round),
        // lower (combat) stats
        new Spec("stat:MaxHp", G.Heart, "E04848", B.Plate, F.Cut),
        new Spec("stat:MaxStamina", G.Bolt, "F0C040", B.Plate, F.Cut),
        new Spec("stat:MaxMp", G.Drop, "4080F0", B.Plate, F.Cut),
        new Spec("stat:Attack", G.Sword, "F08040", B.Plate, F.Cut),
        new Spec("stat:Armor", G.Shield, "6090E0", B.Plate, F.Cut),
        new Spec("stat:ActionSpeed", G.Hourglass, "C0C0E0", B.Plate, F.Cut),
        new Spec("stat:MoveSpeed", G.Boot, "80D0F0", B.Plate, F.Cut),
        new Spec("stat:Accuracy", G.Eye, "F0F0A0", B.Plate, F.Cut),
        new Spec("stat:Evasion", G.Spiral, "90E0C0", B.Plate, F.Cut),
        new Spec("stat:PhysicalResist", G.Anvil, "B0B0B0", B.Plate, F.Cut, D.Minus),
        new Spec("stat:MagicResist", G.Aura, "D080F0", B.Plate, F.Cut, D.Minus),
        new Spec("stat:Threat", G.Horn, "F06060", B.Plate, F.Cut),
        new Spec("stat:BodyWeight", G.Scales, "C8A070", B.Plate, F.Cut),
        new Spec("stat:KnockbackResist", G.Anvil, "8090A0", B.Plate, F.Cut, D.Down),
        new Spec("stat:PatternCostReduce", G.Gear, "90C090", B.Plate, F.Cut, D.Minus),
        new Spec("stat:ExecuteBonus", G.Skull, "F05050", B.Plate, F.Cut, D.Up),
        // statuses
        new Spec("status:Bleed", G.Drop, "D02020", B.Halo, F.Rivet),
        new Spec("status:Poison", G.Skull, "60D040", B.Halo, F.Rivet),
        new Spec("status:Burn", G.Flame, "F08020", B.Halo, F.Rivet),
        new Spec("status:Chill", G.Pulse, "70D0F0", B.Halo, F.Rivet),
        new Spec("status:Freeze", G.Spiral, "A0E0FF", B.Halo, F.Rivet),
        new Spec("status:Stun", G.Star, "F0E040", B.Halo, F.Rivet),
        new Spec("status:Fear", G.Eye, "A050E0", B.Halo, F.Rivet),
        new Spec("status:Petrify", G.Chain, "A0A0A0", B.Halo, F.Rivet),
        new Spec("status:Slow", G.Boot, "8098C8", B.Halo, F.Rivet, D.Down),
        new Spec("status:Confuse", G.Spiral, "E070C0", B.Halo, F.Rivet, D.Star),
        new Spec("status:Weaken", G.Fist, "A8A060", B.Halo, F.Rivet, D.Down),
        // pattern categories
        new Spec("category:Attack", G.Sword, "F06040", B.Split, F.Double),
        new Spec("category:Defense", G.Shield, "5090F0", B.Split, F.Double),
        new Spec("category:Movement", G.Boot, "60D080", B.Split, F.Double),
        new Spec("category:Encounter", G.Eye, "F0C040", B.Split, F.Double),
        new Spec("category:Support", G.Aura, "70D0B0", B.Split, F.Double),
        new Spec("category:Chain", G.Chain, "F0A040", B.Split, F.Double),
        // one icon per pattern (pattern:<asset file>), so patterns of one category can be told apart at a glance
        new Spec("pattern:Slash", G.Sword, "F05050", B.Diagonal, F.Double),
        new Spec("pattern:Swing", G.Anvil, "F09030", B.Diagonal, F.Double),
        new Spec("pattern:Thrust", G.Arrows, "E0D060", B.Diagonal, F.Double),
        new Spec("pattern:Bow", G.Bow, "C0E070", B.Diagonal, F.Double),
        new Spec("pattern:Charge", G.Horn, "F07040", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:Guard", G.Shield, "5090F0", B.Diagonal, F.Double),
        new Spec("pattern:Dodge", G.Boot, "70C0F0", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:Roll", G.Spiral, "70C0F0", B.Diagonal, F.Double),
        new Spec("pattern:Approach", G.Boot, "60D080", B.Diagonal, F.Double),
        new Spec("pattern:Flank", G.Arrows, "60D080", B.Diagonal, F.Double, D.Star),
        new Spec("pattern:KeepDistance", G.Eye, "60D080", B.Diagonal, F.Double, D.Minus),
        new Spec("pattern:MagicCast", G.Spiral, "A070F0", B.Diagonal, F.Double),
        new Spec("pattern:ManaBolt", G.Bolt, "8080F0", B.Diagonal, F.Double),
        new Spec("pattern:Taunt", G.Horn, "E04040", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:Counter", G.Sword, "5090F0", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:Cover", G.Shield, "80B0E0", B.Diagonal, F.Double, D.Plus),
        new Spec("pattern:Claw", G.Skull, "E06060", B.Diagonal, F.Double),
        new Spec("pattern:Whirl", G.Spiral, "F05050", B.Diagonal, F.Double, D.Star),
        new Spec("pattern:DoubleThrust", G.Arrows, "F0A040", B.Diagonal, F.Double, D.Plus),
        new Spec("pattern:Kite", G.Boot, "60D0C0", B.Diagonal, F.Double, D.Down),
        new Spec("pattern:Rush", G.Boot, "F0C040", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:Escort", G.Shield, "60D080", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:AlertShot", G.Bow, "F0E060", B.Diagonal, F.Double, D.Star),
        new Spec("pattern:PoisonSting", G.Drop, "70D040", B.Diagonal, F.Double),
        new Spec("pattern:AnkleStrike", G.Boot, "8098C8", B.Diagonal, F.Double, D.Down),
        new Spec("pattern:Ambush", G.Skull, "9060D0", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:ApplyPoison", G.Drop, "60D040", B.Diagonal, F.Double, D.Plus),
        new Spec("pattern:Encourage", G.Banner, "F0C040", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:FirstAid", G.Cross, "70E090", B.Diagonal, F.Double, D.Plus),
        new Spec("pattern:Meditate", G.Aura, "6090F0", B.Diagonal, F.Double, D.Plus),
        new Spec("pattern:ManaBreath", G.Drop, "4080F0", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:EvadeFollowup", G.Boot, "F0A040", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:SecondWind", G.Pulse, "F0C040", B.Diagonal, F.Double, D.Up),
        new Spec("passive:effortless", G.Bolt, "F0E060", B.Plate, F.Notch, D.Star),
        new Spec("passive:meditation", G.Drop, "80A8FF", B.Plate, F.Notch, D.Up),
        new Spec("passive:staff_bound", G.Spiral, "A070F0", B.Plate, F.Notch, D.Down),
        new Spec("stat:FreePatternChance", G.Bolt, "F0E060", B.Plate, F.Cut, D.Percent),
        new Spec("stat:RepeatCostReduce", G.Sword, "F0C040", B.Plate, F.Cut, D.Down),
        new Spec("stat:TirelessWalk", G.Boot, "F0C040", B.Plate, F.Cut, D.Up),
        new Spec("stat:LowHpCostReduce", G.Heart, "F0C040", B.Plate, F.Cut, D.Down),
        new Spec("passive:endurance", G.Bolt, "C0A060", B.Plate, F.Notch, D.Plus),
        new Spec("passive:battle_breath", G.Skull, "F0C040", B.Plate, F.Notch, D.Bolt),
        new Spec("passive:familiar_weapon", G.Sword, "C0C0E0", B.Plate, F.Notch, D.Down),
        new Spec("passive:tireless", G.Boot, "90D0A0", B.Plate, F.Notch, D.Up),
        // 고유 특성 (the roster's 정예: a double frame · 전설: a halo and a crown mark)
        new Spec("passive:sig_serin", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_doyun", G.Shield, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_bella", G.Flame, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_mira", G.Drop, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_asha", G.Eye, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_rin", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_yuna", G.Coin, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_rosa", G.Cross, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_leon", G.Sword, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_baron", G.Shield, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_isolde", G.Aura, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_ragna", G.Fist, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_ethan", G.Boot, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_elena", G.Crown, "FFC040", B.Halo, F.Rivet, D.Star),
        // the rest of the roster's traits
        new Spec("passive:sig_gareth", G.Sword, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_owen", G.Sword, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_marta", G.Sword, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_jude", G.Sword, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_camilla", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_volk", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_ian", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_hargen", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_seira", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_rohan", G.Sword, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_arte", G.Sword, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_gail", G.Sword, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_gordon", G.Shield, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_hanna", G.Shield, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_toby", G.Shield, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_edgar", G.Shield, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_brunhild", G.Shield, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_greta", G.Shield, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_olaf", G.Shield, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_sebastian", G.Shield, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_lumen", G.Shield, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_gor", G.Shield, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_aegis", G.Shield, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_nate", G.Aura, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_alma", G.Aura, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_pipi", G.Aura, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_oscar", G.Aura, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_lucy", G.Aura, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_aidan", G.Aura, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_zahara", G.Aura, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_morgan", G.Aura, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_sylvia", G.Aura, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_theron", G.Aura, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_astaro", G.Aura, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_belian", G.Aura, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_nova", G.Aura, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_bor", G.Fist, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_ruru", G.Fist, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_grom", G.Fist, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_yak", G.Fist, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_fenri", G.Fist, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_haku", G.Fist, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_zena", G.Fist, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_nua", G.Fist, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_bahar", G.Fist, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_kin", G.Fist, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_gar", G.Fist, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_finn", G.Bow, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_mei", G.Bow, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_skip", G.Bow, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_olga", G.Bow, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_jesse", G.Bow, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_reina", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_cob", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_nile", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_batel", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_aura", G.Bow, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_crow", G.Bow, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_sian", G.Bow, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_philip", G.Cross, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_agnes", G.Cross, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_sui", G.Cross, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_harin", G.Cross, "C8C8D0", B.Plate, F.Notch, D.Up),
        new Spec("passive:sig_seraphin", G.Cross, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_gabriel", G.Cross, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_noel", G.Cross, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_magda", G.Cross, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_eve", G.Cross, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_luciel", G.Cross, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:sig_aria", G.Cross, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:adrenaline", G.Heart, "F05050", B.Plate, F.Notch, D.Bolt),
        new Spec("pattern:GuardThrust", G.Shield, "F0A040", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:RollSlash", G.Spiral, "F05050", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:ComboFinish", G.Fist, "F0A040", B.Diagonal, F.Double, D.Star),
        new Spec("pattern:KiteMaster", G.Boot, "60D0C0", B.Diagonal, F.Double, D.Star),
        new Spec("pattern:CatchBreath", G.Pulse, "90D0A0", B.Diagonal, F.Double, D.Clock),
        new Spec("pattern:Kihap", G.Horn, "F0C040", B.Diagonal, F.Double, D.Bolt),
        // ui
        new Spec("ui:soul", G.Aura, "B070FF", B.Burst, F.Notch),
        new Spec("ui:level", G.Star, "F0C040", B.Burst, F.Notch, D.Up),
        new Spec("ui:gold", G.Coin, "F0C040", B.Burst, F.Notch),
        new Spec("ui:preserve", G.Chain, "70C0F0", B.Burst, F.Notch),
        new Spec("ui:vault", G.Crown, "C080FF", B.Burst, F.Notch),
        new Spec("ui:exit", G.Banner, "F0C040", B.Burst, F.Notch),
        new Spec("ui:map", G.Eye, "4090D0", B.Burst, F.Notch),
        new Spec("ui:follow", G.Arrows, "60D0D0", B.Burst, F.Notch),
        new Spec("ui:expand", G.Gear, "7090C0", B.Burst, F.Notch, D.Plus),
        new Spec("ui:gear", G.Gear, "B0B8D0", B.Plate, F.Notch),
        new Spec("ui:fold", G.Pulse, "7090C0", B.Burst, F.Notch, D.Minus),
        new Spec("ui:unfold", G.Pulse, "7090C0", B.Burst, F.Notch, D.Plus),
        new Spec("ui:close", G.Cross, "F05050", B.Burst, F.Round),
        new Spec("ui:party", G.Banner, "60B0F0", B.Burst, F.Notch),
        new Spec("ui:equipment", G.Anvil, "B0A080", B.Plate, F.Cut),
        new Spec("ui:race", G.Horn, "C0A060", B.Plate, F.Cut),
        new Spec("ui:base", G.Gear, "A0A0B0", B.Plate, F.Cut),
        new Spec("ui:passive", G.Aura, "80C0A0", B.Plate, F.Cut),
        new Spec("ui:skill", G.Bolt, "F0A040", B.Plate, F.Cut),
        new Spec("ui:trigger", G.Bolt, "F0E060", B.Plate, F.Cut, D.Clock),
        new Spec("ui:cooldown", G.Hourglass, "90A0E0", B.Plate, F.Cut, D.Clock),
        new Spec("ui:range", G.Arrows, "E0E0E0", B.Plate, F.Cut),
        new Spec("ui:pierce", G.Arrows, "F0D060", B.Plate, F.Cut, D.Up),
        new Spec("ui:knockback", G.Fist, "C0C0C0", B.Plate, F.Cut, D.Bolt),
        new Spec("ui:leap", G.Boot, "F0A040", B.Plate, F.Cut, D.Up),
        new Spec("ui:height", G.Arrows, "A0C0F0", B.Plate, F.Cut, D.Up),
        new Spec("ui:look", G.Eye, "C080F0", B.Plate, F.Cut),
        new Spec("ui:lock", G.Chain, "707080", B.Plate, F.Cut),
        new Spec("ui:kills", G.Skull, "E06060", B.Plate, F.Cut),
        new Spec("ui:buff", G.Star, "70E070", B.Plate, F.Cut, D.Up),
        new Spec("ui:dot_hero", G.Shield, "60E060", B.Halo, F.Round),
        new Spec("ui:leader", G.Crown, "F0C040", B.Halo, F.Round),
        // supplies: potions (belt) and scrolls / tools (pouch)
        new Spec("ui:potion", G.Potion, "E04848", B.Radial, F.Round),
        new Spec("ui:potion_stamina", G.Potion, "F0C040", B.Radial, F.Round),
        new Spec("ui:potion_mana", G.Potion, "4080F0", B.Radial, F.Round),
        new Spec("ui:antidote", G.Potion, "60D040", B.Radial, F.Round, D.Plus),
        new Spec("ui:scroll_heal", G.Cross, "70E090", B.Plate, F.Double),
        new Spec("ui:scroll_fury", G.Sword, "F06040", B.Plate, F.Double, D.Up),
        new Spec("ui:scroll_guard", G.Shield, "6090E0", B.Plate, F.Double, D.Up),
        new Spec("ui:scroll_gale", G.Boot, "80D0F0", B.Plate, F.Double, D.Up),
        new Spec("ui:scroll_breath", G.Bolt, "F0C040", B.Plate, F.Double, D.Plus),
        new Spec("ui:scroll_purify", G.Aura, "E0E0FF", B.Plate, F.Double),
        new Spec("ui:scroll_return", G.Banner, "F0C040", B.Burst, F.Double, D.Star),
        new Spec("ui:camp", G.Flame, "F09030", B.Radial, F.Round),
        new Spec("skill:alert_shot", G.Bow, "F0D060", B.Burst, F.Double, D.Up),
        new Spec("ui:dot_monster", G.Skull, "E05050", B.Halo, F.Round),
        // skills
        new Spec("skill:fire_bolt", G.Flame, "F07030", B.Burst, F.Double),
        new Spec("skill:mana_bolt", G.Bolt, "8080F0", B.Burst, F.Double),
        new Spec("skill:healing_light", G.Cross, "F0E080", B.Burst, F.Double, D.Plus),
        new Spec("skill:resurrection", G.Star, "FFF0A0", B.Burst, F.Double, D.Up),
        new Spec("skill:fire_enchant", G.Flame, "F07030", B.Burst, F.Double, D.Plus),
        new Spec("skill:frost_enchant", G.Pulse, "70D0F0", B.Burst, F.Double, D.Plus),
        new Spec("skill:haste", G.Boot, "F0E060", B.Burst, F.Double, D.Up),
        new Spec("skill:curse_weakness", G.Skull, "A060D0", B.Burst, F.Double, D.Down),
        new Spec("skill:slow_spell", G.Hourglass, "8098C8", B.Burst, F.Double, D.Down),
        new Spec("skill:frost_shard", G.Pulse, "70D0F0", B.Burst, F.Double),
        new Spec("skill:rage_charge", G.Horn, "E07040", B.Burst, F.Double, D.Bolt),
        new Spec("skill:taunt", G.Banner, "F05050", B.Burst, F.Double, D.Up),
        new Spec("skill:leap_shockwave", G.Anvil, "F0A040", B.Burst, F.Double, D.Bolt),
        new Spec("skill:ground_slam", G.Anvil, "C060F0", B.Burst, F.Double, D.Bolt),
        // passives
        new Spec("passive:adaptability", G.Gear, "90C0F0", B.Plate, F.Notch),
        new Spec("passive:sturdy_build", G.Anvil, "C0A070", B.Plate, F.Notch),
        new Spec("passive:precise_senses", G.Eye, "90F0B0", B.Plate, F.Notch),
        new Spec("passive:hunter_instinct", G.Skull, "F08050", B.Plate, F.Notch),
        new Spec("passive:tracking", G.Arrows, "A0A0F0", B.Plate, F.Notch),
        new Spec("passive:stone_skin", G.Shield, "B0B0B0", B.Plate, F.Notch),
        new Spec("passive:venom_body", G.Drop, "70D040", B.Plate, F.Notch),
        new Spec("passive:pathfinder_eye", G.Eye, "F0D060", B.Plate, F.Notch, D.Plus),
        new Spec("passive:cartography", G.Banner, "F0C040", B.Plate, F.Notch, D.Star),
        new Spec("passive:secret_paths", G.Chain, "C0A060", B.Plate, F.Notch, D.Up),
        // floor monster souls
        new Spec("pattern:FireSpit", G.Flame, "F07030", B.Diagonal, F.Double),
        new Spec("pattern:FrostSpit", G.Drop, "80D0F0", B.Diagonal, F.Double),
        new Spec("pattern:DreadBolt", G.Skull, "B080F0", B.Diagonal, F.Double, D.Bolt),
        new Spec("skill:holy_touch", G.Cross, "F0E0A0", B.Burst, F.Double, D.Plus),
        // job skills (books) and the 전설's own (a halo, a rivet frame)
        new Spec("skill:issen", G.Sword, "F0F0FF", B.Burst, F.Double, D.Bolt),
        new Spec("skill:execution", G.Skull, "F05050", B.Burst, F.Double, D.Bolt),
        new Spec("skill:guardian_cry", G.Banner, "70B0F0", B.Burst, F.Double, D.Plus),
        new Spec("skill:iron_wall", G.Shield, "B0B8C8", B.Burst, F.Double, D.Up),
        new Spec("skill:roar", G.Horn, "C080F0", B.Burst, F.Double, D.Down),
        new Spec("skill:frenzy", G.Heart, "F04040", B.Burst, F.Double, D.Up),
        new Spec("skill:smoke_bomb", G.Aura, "A0A0B0", B.Burst, F.Double, D.Down),
        new Spec("skill:trap", G.Chain, "C0A060", B.Burst, F.Double, D.Down),
        new Spec("skill:piercing_shot", G.Arrows, "F0D060", B.Burst, F.Double, D.Bolt),
        new Spec("skill:frost_prison", G.Pulse, "90E0FF", B.Burst, F.Double, D.Clock),
        new Spec("skill:blizzard", G.Pulse, "70B0F0", B.Burst, F.Double, D.Down),
        new Spec("skill:confusion", G.Spiral, "C080F0", B.Burst, F.Double, D.Down),
        new Spec("skill:petrify_gaze", G.Eye, "A09070", B.Burst, F.Double, D.Down),
        new Spec("skill:arcane_burst", G.Star, "A070F0", B.Burst, F.Double, D.Bolt),
        new Spec("skill:chain_lightning", G.Bolt, "F0F070", B.Burst, F.Double, D.Bolt),
        new Spec("skill:mana_shield", G.Shield, "6080F0", B.Burst, F.Double, D.Plus),
        new Spec("skill:prayer", G.Cross, "F0E080", B.Burst, F.Double, D.Up),
        new Spec("skill:judgment", G.Star, "F0E080", B.Burst, F.Double, D.Bolt),
        new Spec("skill:purify", G.Drop, "A0F0E0", B.Burst, F.Double, D.Plus),
        new Spec("skill:sacrifice", G.Heart, "F0E080", B.Burst, F.Double, D.Down),
        new Spec("skill:sanctuary", G.Aura, "F0E080", B.Burst, F.Double, D.Clock),
        new Spec("skill:ward_prayer", G.Shield, "F0E080", B.Burst, F.Double, D.Up),
        new Spec("skill:life_blessing", G.Heart, "F0E080", B.Burst, F.Double, D.Up),
        new Spec("skill:consecrate", G.Sword, "F0E080", B.Burst, F.Double, D.Star),
        new Spec("skill:holy_shield", G.Shield, "F0E080", B.Burst, F.Double, D.Star),
        new Spec("skill:renewal_vow", G.Heart, "F0E080", B.Burst, F.Double, D.Star),
        new Spec("skill:blessed_rain", G.Drop, "F0E080", B.Burst, F.Double, D.Star),
        new Spec("skill:fire_wall", G.Flame, "FF7040", B.Burst, F.Double, D.Star),
        new Spec("skill:ice_lance", G.Arrows, "80D0FF", B.Burst, F.Double, D.Star),
        new Spec("skill:gravity_well", G.Spiral, "B080FF", B.Burst, F.Double, D.Star),
        new Spec("skill:thunder_strike", G.Bolt, "FFE060", B.Burst, F.Double, D.Star),
        new Spec("skill:quick_shot", G.Bow, "F0D060", B.Burst, F.Double, D.Bolt),
        new Spec("skill:arrow_rain", G.Arrows, "F0D060", B.Burst, F.Double, D.Down),
        new Spec("skill:aimed_shot", G.Eye, "F0D060", B.Burst, F.Double, D.Star),
        new Spec("skill:pin_shot", G.Chain, "F0D060", B.Burst, F.Double, D.Down),
        new Spec("skill:summon_fire", G.Flame, "FF9040", B.Radial, F.Double, D.Star),
        new Spec("skill:summon_frost", G.Drop, "80D0FF", B.Radial, F.Double, D.Star),
        new Spec("skill:summon_wind", G.Spiral, "80F0C8", B.Radial, F.Double, D.Star),
        new Spec("skill:summon_light", G.Star, "FFE080", B.Radial, F.Double, D.Up),
        new Spec("passive:sig_dale", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_pip", G.Boot, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_mina", G.Eye, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_lyra", G.Bow, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_bran", G.Eye, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_sylvaine", G.Star, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_hawk", G.Eye, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_lumi", G.Aura, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_odo", G.Drop, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_sora", G.Spiral, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_ember", G.Flame, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_nerea", G.Drop, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_aeris", G.Crown, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("passive:sig_kael", G.Spiral, "80A8FF", B.Radial, F.Double, D.Star),
        new Spec("skill:war_cry", G.Banner, "F07040", B.Burst, F.Double, D.Up),
        new Spec("skill:battle_focus", G.Eye, "F0C040", B.Burst, F.Double, D.Up),
        new Spec("skill:pack_call", G.Horn, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:shortcut", G.Boot, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("pattern:Sprint", G.Boot, "F0E060", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:SpellFocus", G.Spiral, "A070F0", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:NatureCommunion", G.Aura, "80F0A0", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:Prayer", G.Cross, "F0E080", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:Kick", G.Boot, "F0A040", B.Diagonal, F.Double, D.Bolt),
        new Spec("pattern:Punch", G.Fist, "F09030", B.Diagonal, F.Double),
        new Spec("pattern:ThrowingKnife", G.Sword, "C0C0E0", B.Diagonal, F.Double, D.Up),
        new Spec("pattern:Rake", G.Fist, "F05050", B.Diagonal, F.Double, D.Star),
        new Spec("pattern:PoisonArrow", G.Bow, "70D040", B.Diagonal, F.Double, D.Down),
        new Spec("stat:ManaShield", G.Shield, "6080F0", B.Plate, F.Cut, D.Percent),
        // jobs (a badge on the mercenary's portrait): job:<SoulIconSet.JobKey>
        new Spec("job:swordsman", G.Sword, "E8E8F0", B.Burst, F.Round),
        new Spec("job:guardian", G.Shield, "6090E0", B.Burst, F.Round),
        new Spec("job:fighter", G.Fist, "F06040", B.Burst, F.Round),
        new Spec("job:pathfinder", G.Eye, "60D080", B.Burst, F.Round),
        new Spec("job:mage", G.Spiral, "A070F0", B.Burst, F.Round),
        new Spec("job:priest", G.Cross, "F0E080", B.Burst, F.Round),
        new Spec("job:archer", G.Bow, "C0E070", B.Burst, F.Round),
        new Spec("job:summoner", G.Aura, "80F0D0", B.Burst, F.Round),
        new Spec("stat:SpellReflect", G.Star, "A0F0F0", B.Plate, F.Cut, D.Percent),
        // every other stat gets its own too (tooltips, 상세 스탯, the marks of an enchant on a piece)
        new Spec("stat:CritChance", G.Star, "F0D040", B.Plate, F.Cut, D.Percent),
        new Spec("stat:CritDamage", G.Star, "F06040", B.Plate, F.Cut, D.Plus),
        new Spec("stat:HpRegen", G.Heart, "70E090", B.Plate, F.Cut, D.Up),
        new Spec("stat:StaminaRegen", G.Bolt, "F0D060", B.Plate, F.Cut, D.Up),
        new Spec("stat:MpRegen", G.Drop, "60A0FF", B.Plate, F.Cut, D.Up),
        new Spec("stat:ManaRegenRate", G.Drop, "80C0FF", B.Plate, F.Cut, D.Clock),
        new Spec("stat:SpellPower", G.Spiral, "C090FF", B.Plate, F.Cut, D.Up),
        new Spec("stat:DownResist", G.Boot, "E8C860", B.Plate, F.Cut, D.Plus),
        new Spec("stat:BleedResist", G.Drop, "D02020", B.Plate, F.Cut, D.Minus),
        new Spec("stat:PoisonResist", G.Skull, "60D040", B.Plate, F.Cut, D.Minus),
        new Spec("stat:BurnResist", G.Flame, "F08020", B.Plate, F.Cut, D.Minus),
        new Spec("stat:ChillResist", G.Pulse, "70D0F0", B.Plate, F.Cut, D.Minus),
        new Spec("stat:FearResist", G.Eye, "A050E0", B.Plate, F.Cut, D.Minus),
        new Spec("stat:StunResist", G.Star, "F0E040", B.Plate, F.Cut, D.Minus),
        new Spec("stat:PetrifyResist", G.Chain, "A0A0A0", B.Plate, F.Cut, D.Minus),
        new Spec("stat:FireAttack", G.Flame, "F06020", B.Burst, F.Cut, D.Plus),
        new Spec("stat:ColdAttack", G.Pulse, "80D8FF", B.Burst, F.Cut, D.Plus),
        new Spec("stat:LightningAttack", G.Bolt, "F0F070", B.Burst, F.Cut, D.Plus),
        new Spec("stat:EarthAttack", G.Anvil, "C09050", B.Burst, F.Cut, D.Plus),
        new Spec("stat:WindAttack", G.Spiral, "A0F0C0", B.Burst, F.Cut, D.Plus),
        new Spec("stat:ToxicAttack", G.Drop, "80D040", B.Burst, F.Cut, D.Plus),
        new Spec("stat:FireResist", G.Flame, "F06020", B.Split, F.Cut, D.Minus),
        new Spec("stat:ColdResist", G.Pulse, "80D8FF", B.Split, F.Cut, D.Minus),
        new Spec("stat:LightningResist", G.Bolt, "F0F070", B.Split, F.Cut, D.Minus),
        new Spec("stat:EarthResist", G.Anvil, "C09050", B.Split, F.Cut, D.Minus),
        new Spec("stat:WindResist", G.Spiral, "A0F0C0", B.Split, F.Cut, D.Minus),
        new Spec("stat:ToxicResist", G.Drop, "80D040", B.Split, F.Cut, D.Minus),
        new Spec("stat:LoadRatio", G.Scales, "E0A060", B.Plate, F.Cut, D.Up),
        new Spec("stat:LifeSteal", G.Drop, "E03050", B.Burst, F.Cut, D.Plus),
        new Spec("stat:KillStamina", G.Skull, "F0C040", B.Plate, F.Cut, D.Bolt),
        new Spec("stat:EliteDamage", G.Crown, "F08040", B.Plate, F.Cut, D.Up),
        new Spec("stat:DoubleHit", G.Sword, "F0A0A0", B.Plate, F.Cut, D.Plus),
        new Spec("stat:ArmorShred", G.Shield, "F06060", B.Plate, F.Cut, D.Down),
        new Spec("stat:FreeSkillChance", G.Spiral, "F0E060", B.Plate, F.Cut, D.Percent),
        new Spec("stat:Thorns", G.Shield, "C0E060", B.Plate, F.Cut, D.Star),
        new Spec("stat:DeathDefy", G.Heart, "F0D040", B.Plate, F.Cut, D.Star),
        new Spec("stat:WoundResist", G.Cross, "E06060", B.Plate, F.Cut, D.Minus),
        new Spec("stat:GoldFind", G.Coin, "F0C040", B.Plate, F.Cut, D.Up),
        new Spec("stat:LootFind", G.Anvil, "B0A080", B.Plate, F.Cut, D.Up),
        new Spec("stat:SoulFind", G.Aura, "B070FF", B.Plate, F.Cut, D.Up),
        new Spec("stat:CursedHp", G.Heart, "9050B0", B.Plate, F.Cut, D.Minus),
        new Spec("stat:DuelFocus", G.Sword, "F0E0A0", B.Plate, F.Cut, D.Star),
        new Spec("stat:CrowdFury", G.Soldiers, "F06040", B.Plate, F.Cut, D.Up),
        new Spec("stat:DarkSight", G.Eye, "80A0FF", B.Plate, F.Cut, D.Plus),
        new Spec("stat:FearAura", G.Eye, "A050E0", B.Halo, F.Cut, D.Down),
        new Spec("stat:RegenAura", G.Cross, "70E090", B.Halo, F.Cut, D.Plus),
        new Spec("stat:GuardAura", G.Shield, "6090E0", B.Halo, F.Cut, D.Up),
        new Spec("stat:DefyRage", G.Fist, "F04040", B.Plate, F.Cut, D.Star),
        new Spec("stat:BookDiscount", G.Coin, "80C0F0", B.Plate, F.Cut, D.Down),
        new Spec("stat:FireFocus", G.Flame, "F08020", B.Plate, F.Cut, D.Star),
        new Spec("stat:ChainBonus", G.Chain, "F0E060", B.Plate, F.Cut, D.Plus),
        new Spec("stat:BattleCaster", G.Spiral, "F0A040", B.Plate, F.Cut, D.Bolt),
        new Spec("stat:PotionPower", G.Potion, "70E0C0", B.Plate, F.Cut, D.Up),
        new Spec("stat:HolyBane", G.Cross, "F0E0A0", B.Plate, F.Cut, D.Bolt),
        new Spec("stat:StatusGrade", G.Skull, "C080F0", B.Plate, F.Cut, D.Up),
        new Spec("stat:KillMana", G.Skull, "6090F0", B.Plate, F.Cut, D.Plus),
        new Spec("stat:LowHpDamage", G.Heart, "F04040", B.Plate, F.Cut, D.Bolt),
        new Spec("stat:WoundMend", G.Cross, "70E090", B.Plate, F.Cut, D.Plus),
        new Spec("stat:BuffDuration", G.Banner, "F0C040", B.Plate, F.Cut, D.Clock),
        new Spec("stat:StaffBound", G.Spiral, "A070F0", B.Plate, F.Cut, D.Down),
        new Spec("status:Silence", G.Chain, "F0E0A0", B.Halo, F.Rivet),
        // monster souls' skills (영혼 스킬): a split plate · the bosses' a halo with a rivet frame
        new Spec("skill:soul_mud_roll", G.Shield, "A08050", B.Split, F.Cut, D.None),
        new Spec("skill:soul_wolf_howl", G.Horn, "8090C0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_shadow_pounce", G.Fist, "6060A0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_statue", G.Anvil, "A0A0A0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_arrow_rain", G.Arrows, "C0A060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_dynamite", G.Flame, "E08030", B.Split, F.Cut, D.None),
        new Spec("skill:soul_pickpocket", G.Coin, "F0D060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_curse_doll", G.Skull, "A060D0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_acid_pool", G.Drop, "70D040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_flame_burst", G.Flame, "F06030", B.Split, F.Cut, D.None),
        new Spec("skill:soul_split", G.Drop, "F08040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_ice_floor", G.Pulse, "90E0FF", B.Split, F.Cut, D.None),
        new Spec("skill:soul_plague_cloud", G.Skull, "80C040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_blood_suck", G.Drop, "C02030", B.Split, F.Cut, D.None),
        new Spec("skill:soul_tongue_lash", G.Chain, "60C080", B.Split, F.Cut, D.None),
        new Spec("skill:soul_slime_spit", G.Drop, "80D0A0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_troll_regen", G.Heart, "60C060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_boulder_throw", G.Anvil, "A08060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_splash", G.Drop, "60A0F0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_tidal", G.Pulse, "4080F0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_bog_summon", G.Drop, "508040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_swallow", G.Skull, "608040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_whirlpool", G.Spiral, "4060D0", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_dragon_scale", G.Shield, "4090D0", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_bone_breaker", G.Fist, "C08040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_war_frenzy", G.Banner, "D05030", B.Split, F.Cut, D.None),
        new Spec("skill:soul_tail_sweep", G.Spiral, "80A060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_molt", G.Pulse, "A0C080", B.Split, F.Cut, D.None),
        new Spec("skill:soul_fire_breath", G.Flame, "F07030", B.Split, F.Cut, D.None),
        new Spec("skill:soul_lava_pool", G.Flame, "D04020", B.Split, F.Cut, D.None),
        new Spec("skill:soul_dragon_breath", G.Flame, "FF5020", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_dive_bomb", G.Bolt, "FF8040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_bone_shield", G.Shield, "E0E0D0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_bone_rise", G.Skull, "E0E0D0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_bone_spear", G.Sword, "D0D0C0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_devour", G.Heart, "806050", B.Split, F.Cut, D.None),
        new Spec("skill:soul_paralyze_claw", G.Fist, "90A060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_blood_pact", G.Drop, "D02040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_bat_form", G.Boot, "704060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_life_drain", G.Heart, "A02060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_beastform", G.Horn, "A07040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_death_sentence", G.Skull, "8040C0", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_legion_of_dead", G.Soldiers, "B0B0A0", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_light_barrier", G.Aura, "F0E080", B.Split, F.Cut, D.None),
        new Spec("skill:soul_seal", G.Chain, "F0E0A0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_hellfire_smash", G.Flame, "E03020", B.Split, F.Cut, D.None),
        new Spec("skill:soul_demon_pact", G.Heart, "B02020", B.Split, F.Cut, D.None),
        new Spec("skill:soul_agony_curse", G.Skull, "902060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_soul_sap", G.Spiral, "6040A0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_shadow_arrow", G.Bow, "504080", B.Split, F.Cut, D.None),
        new Spec("skill:soul_shadow_hide", G.Eye, "404060", B.Split, F.Cut, D.None),
        new Spec("skill:soul_plunder", G.Coin, "D0A040", B.Split, F.Cut, D.None),
        new Spec("skill:soul_crystal_wall", G.Shield, "80E0F0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_crystal_reflect", G.Star, "A0F0F0", B.Split, F.Cut, D.None),
        new Spec("skill:soul_gods_wrath", G.Bolt, "FFE060", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_abyss_gate", G.Spiral, "6020A0", B.Halo, F.Rivet, D.Star),
        new Spec("skill:soul_void", G.Eye, "302050", B.Halo, F.Rivet, D.Star),
        new Spec("skill:heaven_blade", G.Sword, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:bastion", G.Shield, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:meteor", G.Flame, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("skill:miracle", G.Star, "FFC040", B.Halo, F.Rivet, D.Star),
        new Spec("passive:fire_body", G.Flame, "F07030", B.Plate, F.Notch),
        new Spec("passive:frost_body", G.Drop, "80D0F0", B.Plate, F.Notch),
        new Spec("passive:plague", G.Drop, "B080F0", B.Plate, F.Notch, D.Bolt),
        new Spec("passive:devour", G.Heart, "E04050", B.Plate, F.Notch, D.Plus),
        new Spec("passive:ore_sense", G.Coin, "F0D060", B.Plate, F.Notch, D.Star),
        new Spec("passive:frog_legs", G.Boot, "80D060", B.Plate, F.Notch, D.Up),
        new Spec("passive:troll_blood", G.Heart, "70C050", B.Plate, F.Notch, D.Up),
        new Spec("passive:war_breath", G.Pulse, "F0A040", B.Plate, F.Notch, D.Bolt),
        new Spec("passive:scales", G.Shield, "60C0A0", B.Plate, F.Notch, D.Down),
        new Spec("passive:tide_guard", G.Aura, "60B0F0", B.Plate, F.Notch),
        new Spec("passive:magma_scale", G.Shield, "F06030", B.Plate, F.Notch, D.Bolt),
        new Spec("passive:undying", G.Skull, "E0D8C0", B.Plate, F.Notch, D.Up),
        new Spec("passive:rot_flesh", G.Cross, "90A070", B.Plate, F.Notch, D.Down),
        new Spec("passive:blood_thirst", G.Drop, "E03040", B.Plate, F.Notch, D.Plus),
        new Spec("passive:frenzy", G.Fist, "F04040", B.Plate, F.Notch, D.Bolt),
        new Spec("passive:execution", G.Sword, "C02030", B.Plate, F.Notch, D.Star),
        new Spec("passive:soul_harvest", G.Spiral, "C090F0", B.Plate, F.Notch, D.Plus),
        new Spec("passive:vital_spot", G.Eye, "F05050", B.Plate, F.Notch, D.Bolt),
        new Spec("passive:scent", G.Eye, "D0B070", B.Plate, F.Notch, D.Star),
        new Spec("passive:crystal_hide", G.Shield, "90C0FF", B.Plate, F.Notch, D.Plus),
        new Spec("passive:swamp_blood", G.Drop, "60B040", B.Plate, F.Notch, D.Up),
        new Spec("passive:death_lord", G.Crown, "E0D8C0", B.Plate, F.Notch, D.Star),
        new Spec("passive:abyss", G.Crown, "C02040", B.Plate, F.Notch, D.Bolt),
    };

    static string FileFor(string key) => $"{IconDir}/{key.Replace(':', '_')}.png";

    public static SoulIconSet BuildIcons()
    {
        IconGenerator.EnsureDir(IconDir);
        foreach (var spec in Specs)
        {
            var style = new IconArt.Style(spec.Glyph, IconGenerator.Hex(spec.Hex), spec.Back, spec.Frame, spec.Badge);
            IconGenerator.Save(IconSize, IconSize, FileFor(spec.Key), p => IconArt.Compose(p, style));
        }
        AssetDatabase.Refresh();
        IconGenerator.ApplySpriteImportSettings(IconDir, IconSize);

        System.IO.Directory.CreateDirectory("Assets/Resources");
        var set = AssetDatabase.LoadAssetAtPath<SoulIconSet>(IconSetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<SoulIconSet>();
            AssetDatabase.CreateAsset(set, IconSetPath);
        }
        set.Panel = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PixelFantasy/Common/Sprites/UI/Common/Round24.png");
        set.Button = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PixelFantasy/Common/Sprites/UI/Common/Round16.png");
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        SpriteManagerCreator.Create(); // the icons are served by SpriteManager (Atlas_SoulMercenaries)
        return set;
    }

    // ── popups ───────────────────────────────────────────────

    public static void BuildPopupPrefabs()
    {
        Popup<SoulMercenaryPopup>("SoulMercenaryPopup", PopupType.SoulMercenary);
        Popup<SoulMapPopup>("SoulMapPopup", PopupType.SoulMap);
        Popup<SoulGuildPopup>("SoulGuildPopup", PopupType.SoulGuild);
        Popup<SoulShopPopup>("SoulShopPopup", PopupType.SoulShop);
        Popup<SoulChurchPopup>("SoulChurchPopup", PopupType.SoulChurch);
        Popup<SoulTrainingPopup>("SoulTrainingPopup", PopupType.SoulTraining);
        Popup<SoulBlacksmithPopup>("SoulBlacksmithPopup", PopupType.SoulBlacksmith);
        Popup<SoulStoragePopup>("SoulStoragePopup", PopupType.SoulStorage);
        Popup<SoulLibraryPopup>("SoulLibraryPopup", PopupType.SoulLibrary);
        Popup<SoulMemorialPopup>("SoulMemorialPopup", PopupType.SoulMemorial);
        Popup<SoulAltarPopup>("SoulAltarPopup", PopupType.SoulAltar);
        Popup<SoulMerchantPopup>("SoulMerchantPopup", PopupType.SoulMerchant);
        Popup<SoulBoardPopup>("SoulBoardPopup", PopupType.SoulBoard);
        Popup<ConfirmPopup>("ConfirmPopup", PopupType.Confirm);
    }

    // Root = RectTransform + CanvasGroup + popup script; content is built at runtime.
    static void Popup<T>(string name, PopupType type) where T : PopupBase
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var popup = go.AddComponent<T>();
        var so = new SerializedObject(popup);
        so.FindProperty("_popupType").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(PopupType)), type);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(go, $"{PopupDir}/{name}.prefab");
        UnityEngine.Object.DestroyImmediate(go);
    }

    // The original scenes keep PopupManager under DontDestroyOnLoad > CanvasPopup (sorting 200).
    public static PopupManager CreatePopupManager()
    {
        var root = new GameObject("DontDestroyOnLoad", typeof(DontDestroyOnLoad));
        root.GetComponent<DontDestroyOnLoad>().Unique = true; // village ↔ dungeon: one popup root, not one per load
        var canvasObject = new GameObject("CanvasPopup", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var managerObject = new GameObject("PopupManager", typeof(RectTransform));
        managerObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)managerObject.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var manager = managerObject.AddComponent<PopupManager>();
        PopupManagerEditor.LoadPopupPrefabs(manager);
        // sound lives next to the popups: one AudioManager for village and dungeon (the root is unique)
        var audio = new GameObject("AudioManager").AddComponent<AudioManager>();
        audio.transform.SetParent(root.transform, false);
        AudioManagerEditor.LoadClips(audio);
        return manager;
    }
}
