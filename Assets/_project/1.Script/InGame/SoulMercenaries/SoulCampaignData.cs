namespace SoulMercenaries
{
    // The Soul Mercenaries campaign as one UserDataManager section (SaveKey.SoulCampaign). It only keeps the JSON
    // that SoulSave writes; gold, soul stones and supplies are in ItemData, saved alongside.
    public sealed class SoulCampaignData : ISaveSection
    {
        public SaveKey SaveKey => SaveKey.SoulCampaign;
        public string Json; // null: no campaign yet

        public string Serialize() => Json ?? "";
        public void Deserialize(string json) => Json = string.IsNullOrEmpty(json) ? null : json;
        public void SetDefaults() => Json = null;
    }
}
