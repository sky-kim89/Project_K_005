using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoulMercenaries
{
    // A mercenary at home, strolling around the village: walks to a free spot nearby, stops a while, walks on.
    // Drawn from its portrait (the idle frame), bobbing while it walks and facing where it goes. While it trains
    // it goes to its post in front of the training ground and swings there until the training is over.
    // It only walks over free cells (never through a building or the portal): the way is found cell by cell,
    // and a diagonal step is taken only when both cells beside it are free too (no cutting a building's corner).
    public sealed class SoulVillageWalker : MonoBehaviour
    {
        const float Speed = 70f, Size = 104f;
        public SoulMercenary Hero;
        public Vector2? Post; // in front of the training ground
        Image image;
        RectTransform rect;
        List<Vector2> spots;
        HashSet<Vector2Int> free;
        float cell;
        Vector2 position, goal;
        readonly List<Vector2> path = new List<Vector2>();
        float wait, step, swing;

        // `spots`: the centre of every free cell, laid out on a grid of `cell` pixels.
        public static SoulVillageWalker Create(Transform parent, SoulMercenary hero, List<Vector2> spots, float cell)
        {
            var go = SoulUi.Rect(hero.Name, parent).gameObject;
            var walker = go.AddComponent<SoulVillageWalker>();
            walker.Hero = hero;
            walker.spots = spots;
            walker.cell = cell;
            walker.free = new HashSet<Vector2Int>();
            foreach (var spot in spots) walker.free.Add(walker.CellOf(spot));
            walker.rect = (RectTransform)go.transform;
            walker.rect.anchorMin = walker.rect.anchorMax = new Vector2(0, 1);
            walker.rect.pivot = new Vector2(.5f, .1f);
            walker.rect.sizeDelta = new Vector2(Size, Size);
            walker.image = go.AddComponent<Image>();
            walker.image.preserveAspect = true;
            walker.image.raycastTarget = false;
            walker.position = walker.goal = spots[Random.Range(0, spots.Count)];
            walker.wait = Random.Range(0f, 2f);
            return walker;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var campaign = SoulCampaign.Current;
            bool training = Post.HasValue && campaign != null && campaign.IsTraining(Hero);
            if (training && goal != Post.Value) { GoTo(Post.Value); wait = 0; }
            Sprite frame = null;
            if (wait > 0) wait -= dt;
            else if (path.Count > 0)
            {
                var delta = path[0] - position;
                float move = Speed * dt;
                if (delta.magnitude <= move) { position = path[0]; path.RemoveAt(0); }
                else position += delta.normalized * move;
                step += dt;
                if (Mathf.Abs(delta.x) > 1) rect.localScale = new Vector3(delta.x < 0 ? -1 : 1, 1, 1);
            }
            else if (training)
            {
                swing += dt;
                frame = SoulAttackLoop.Frame(Hero, swing);
                rect.localScale = Vector3.one; // facing the dummy
            }
            else
            {
                wait = Random.Range(1.2f, 4f);
                GoTo(NextSpot());
                swing = 0;
            }
            image.sprite = frame != null ? frame : SoulPortraits.For(Hero);
            image.enabled = image.sprite != null;
            bool walking = wait <= 0 && path.Count > 0;
            rect.anchoredPosition = position + Vector2.up * (walking ? Mathf.Abs(Mathf.Sin(step * 9)) * 5 : 0);
        }

        // Somewhere not too far (the village is walked in short strolls).
        Vector2 NextSpot()
        {
            for (int i = 0; i < 12; i++)
            {
                var spot = spots[Random.Range(0, spots.Count)];
                if ((spot - position).magnitude < 420) return spot;
            }
            return spots[Random.Range(0, spots.Count)];
        }

        Vector2Int CellOf(Vector2 spot) => new Vector2Int(Mathf.RoundToInt(spot.x / cell - .5f), Mathf.RoundToInt(-spot.y / cell - .7f));
        Vector2 SpotOf(Vector2Int c) => new Vector2((c.x + .5f) * cell, -(c.y + .7f) * cell);

        // The way to `target` over free cells (breadth first); no way: stay where it is.
        void GoTo(Vector2 target)
        {
            goal = target;
            path.Clear();
            Vector2Int from = CellOf(position), to = CellOf(target);
            if (!free.Contains(to)) return;
            var came = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
            var open = new Queue<Vector2Int>();
            open.Enqueue(from);
            while (open.Count > 0 && !came.ContainsKey(to))
            {
                var at = open.Dequeue();
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var next = new Vector2Int(at.x + dx, at.y + dy);
                        if (!free.Contains(next) || came.ContainsKey(next)) continue;
                        if (dx != 0 && dy != 0 && (!free.Contains(new Vector2Int(at.x + dx, at.y)) || !free.Contains(new Vector2Int(at.x, at.y + dy)))) continue;
                        came[next] = at;
                        open.Enqueue(next);
                    }
            }
            if (!came.ContainsKey(to)) return;
            for (var c = to; c != from; c = came[c]) path.Insert(0, SpotOf(c));
            if (from != to || position != target) path.Insert(0, SpotOf(from)); // back onto the grid first
        }
    }
}
