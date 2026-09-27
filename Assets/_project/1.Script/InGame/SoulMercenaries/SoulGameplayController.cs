using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulMercenaries
{
    public sealed class SoulGameplayController : MonoBehaviour
    {
        public SoulDungeonData Dungeon;
        public SoulStatRules StatRules;
        public SoulMercenaryData[] Party;
        public int Seed = 12345;
        [Tooltip("A new seed (so a new procedural map) on every play.")]
        public bool RandomSeed = true;
        public SoulDungeonSession Session { get; private set; }
        // The expedition on screen (sent from the village): its session lives in the campaign and runs in
        // SoulExpeditionRunner whether this scene is open or not. Null: a stand-alone test run.
        public SoulExpedition Expedition { get; private set; }

        // Shared by the world (click-to-move) and the HUD (cards, detail panel).
        int selectedHero;
        public int SelectedHero
        {
            get => Session == null ? 0 : Mathf.Clamp(selectedHero, 0, Mathf.Max(0, Session.Mercenaries.Count - 1));
            set => selectedHero = value;
        }
        // HUD toggle: move orders go to the whole party (formation) instead of the selected mercenary.
        public bool MoveParty;

        public SoulMercenary SelectedMercenary => Session != null && Session.Mercenaries.Count > 0 ? Session.Mercenaries[SelectedHero] : null;

        // A cleared floor waiting to hand its party to the next one across the scene reload (stand-alone runs).
        static SoulDungeonSession carried;
        bool reloading;

        void Awake()
        {
            SoulExpeditionRunner.Ensure();
            var campaign = SoulCampaign.Current;
            if (campaign != null && campaign.Watching != null)
            {
                Expedition = campaign.Watching;
                Session = Expedition.Session;
                Session.CombatEvents.Clear(); // what happened unseen is not replayed
                carried = null;
                return;
            }
            int seed = RandomSeed ? System.Environment.TickCount & int.MaxValue : Seed;
            Session = carried != null ? carried.NextFloor(seed) : new SoulDungeonSession(Dungeon, StatRules, Party, seed);
            carried = null;
        }

        public void NextFloor()
        {
            if (Session == null || !Session.HasNextFloor) return;
            if (Expedition != null) SoulCampaign.Current.NextFloor(Expedition);
            else carried = Session;
            Reload();
        }

        void Update()
        {
            if (Expedition == null && Session != null)
            {
                // a session of its own (no village): the game speed in steps the session takes (at most .05 s each)
                for (float left = Time.deltaTime * SoulExpeditionRunner.GameSpeed; left > 1e-4f && !Session.Finished && !Session.Defeated && !Session.Recalled; left -= .05f)
                    Session.Tick(Mathf.Min(left, .05f));
            }
            else if (Expedition.Session != Session) Reload(); // the party went a floor down by itself
        }

        public bool FromVillage => Expedition != null;

        // Another party down there: its floor on screen.
        public void Watch(SoulExpedition trip)
        {
            var campaign = SoulCampaign.Current;
            if (campaign == null || trip == null || trip == Expedition || trip.Settled) return;
            campaign.Watching = trip;
            Reload();
        }

        // Enhancement scrolls, watching an expedition: read straight from the village store.
        public int StoreScrolls(string id) => Expedition != null && SoulSupplies.PlayerScroll(id) ? SoulCampaign.Current?.Supply(id) ?? 0 : 0;

        // Why it cannot be read now (null: it can).
        public string ScrollBlock(string id)
        {
            if (StoreScrolls(id) <= 0) return "창고에 없음";
            Session.Pouch[id] = SoulDungeonSession.Count(Session.Pouch, id) + 1;
            string block = Session.SupplyBlock(id);
            Take(id);
            return block;
        }

        public bool ReadScroll(string id)
        {
            if (ScrollBlock(id) != null) return false;
            Session.Pouch[id] = SoulDungeonSession.Count(Session.Pouch, id) + 1;
            if (!Session.UseSupply(id, SelectedMercenary)) { Take(id); return false; }
            if (SoulDungeonSession.Count(Session.Pouch, id) <= 0) Session.Pouch.Remove(id); // UseSupply read the one put in
            SoulCampaign.Current.AddSupply(id, -1);
            return true;
        }

        void Take(string id)
        {
            if (!Session.Pouch.TryGetValue(id, out int count)) return;
            if (count <= 1) Session.Pouch.Remove(id);
            else Session.Pouch[id] = count - 1;
        }

        // Back to the village. `escape`: a party that stopped after a floor comes up with it now. Otherwise it
        // stays down (exploring, or waiting to be thrown out) and the village only stops watching it.
        public void ReturnToVillage(bool escape = false)
        {
            var campaign = SoulCampaign.Current;
            if (campaign == null) return;
            if (escape && Expedition != null) campaign.Escape(Expedition);
            campaign.Watching = null;
            carried = null;
            PopupManager.Instance?.CloseAll();
            SoulTooltip.Hide();
            SceneManager.LoadScene(campaign.Data.VillageScene);
        }

        public void Restart()
        {
            carried = null;
            Reload();
        }

        void Reload()
        {
            if (reloading) return;
            reloading = true;
            PopupManager.Instance?.CloseAll();
            SoulTooltip.Hide();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
