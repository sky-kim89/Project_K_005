namespace SoulMercenaries
{
    // Every mercenary a little different: hair style and colour, a human's skin tone — picked from its name and
    // race, so the guild's preview and the hire look the same (and nothing new is saved). Clothes come from what
    // is worn: no body armour is the plain outfit everyone shares; no helmet, no helmet.
    public static class SoulLooks
    {
        public const string DefaultOutfit = "FarmerClothes";
        static readonly string[] HairStyles =
            { "Hair1", "Hair2", "Hair3", "Hair4", "Hair5", "Hair6", "Hair7", "Hair8", "Hair9", "Hair10", "Hair11", "Hair12", "Hair13", "Hair14", "Hair15" };
        static readonly string[] HairColors = { "2B2522", "5D2C28", "8A4836", "A5672F", "C64524", "D8B25A", "E6E0C8", "7C7C84" };
        static readonly string[] SkinTones = { "F6CA9F", "F9D6B8", "EDB98A", "D39972", "B07A55" };

        // Race → the mercenary's own features (not its clothes) → variation → the plain outfit, before the gear.
        public static void Dress(SoulMercenary hero, UnitAppearanceData look)
        {
            var own = hero.Look;
            own.Armor = own.Helmet = null; // the template's clothes are not worn: gear decides
            own.Apply(look);
            var random = new System.Random(Stable(hero.Name + "|" + hero.Race.Id));
            if (string.IsNullOrEmpty(hero.Race.Appearance?.Hair))
                look.Hair = HairStyles[random.Next(HairStyles.Length)] + "#" + HairColors[random.Next(HairColors.Length)];
            if (hero.Race.Id == "인간")
            {
                string tone = SkinTones[random.Next(SkinTones.Length)];
                look.Body = Tint(look.Body, tone);
                look.Head = Tint(look.Head, tone);
                look.Ears = Tint(look.Ears, tone);
            }
            look.Armor = DefaultOutfit;
            look.Helmet = "";
            // someone on the roster: its own hair, eyes, mask and cape, and the outfit it wears under no armour
            var mine = hero.Recruit != null ? hero.Recruit.Look : default;
            if (!string.IsNullOrEmpty(mine.Hair)) look.Hair = mine.Hair;
            if (!string.IsNullOrEmpty(mine.Eyes)) look.Eyes = mine.Eyes;
            if (!string.IsNullOrEmpty(mine.Mask)) look.Mask = mine.Mask;
            if (!string.IsNullOrEmpty(mine.Cape)) look.Cape = mine.Cape;
            if (!string.IsNullOrEmpty(mine.Armor)) look.Armor = mine.Armor;
            if (!string.IsNullOrEmpty(mine.Helmet)) look.Helmet = mine.Helmet;
        }

        static string Tint(string part, string color) => string.IsNullOrEmpty(part) ? part : part.Split('#')[0] + "#" + color;

        static int Stable(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text) hash = hash * 31 + c;
                return hash & int.MaxValue;
            }
        }
    }
}
