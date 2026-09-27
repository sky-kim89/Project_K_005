using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulMercenaries
{
    //  '.' floor        — walk ✓  sight ✓
    //  '#' wall         — walk ✗  sight ✗   (stage 1 has only these)
    //  '~' lava         — walk ✗  sight ✓
    //  '+' hidden door  — walk only by forced movement until discovered (then ✓) · sight ✗
    public enum SoulTile { Floor, Wall, Lava, HiddenDoor }

    // A planned route in world units (tile units, y down). Partial: only the first stretch of a long
    // route is detailed; walk it and plan again from where it ends.
    public sealed class SoulPath
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        public bool Partial;
        public bool Found => Points.Count > 0;
        public void Clear() { Points.Clear(); Partial = false; }
    }

    // Tiles are the map's building blocks (walls, fog, minimap); movement runs on a finer navigation grid
    // (NavResolution² nav cells per tile). Path finding is hierarchical: the nav grid is cut into chunks, each
    // chunk into connected regions per body-size class, and a route is first found over the region graph,
    // then detailed on the nav grid only inside the next few regions of that corridor.
    public sealed class SoulMap
    {
        readonly string[] rows;
        readonly bool[] discovered;
        readonly bool[] open;
        public int Width { get; }
        public int Height => rows.Length;
        public int Revision { get; private set; }

        public SoulMap(string mapRows)
        {
            rows = mapRows.Replace("\r", "").Trim('\n').Split('\n');
            if (rows.Length == 0 || rows[0].Length == 0) throw new ArgumentException("Map is empty.");
            Width = rows[0].Length;
            foreach (var row in rows) if (row.Length != Width) throw new ArgumentException("Map rows must have equal width.");
            discovered = new bool[Width * Height];
            open = new bool[Width * Height];
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) open[y * Width + x] = rows[y][x] == '.';
            nav = new NavGrid(this);
        }

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public SoulTile Tile(int x, int y)
        {
            if (!Inside(x, y)) return SoulTile.Wall;
            switch (rows[y][x])
            {
                case '#': return SoulTile.Wall;
                case '~': return SoulTile.Lava;
                case '+': return SoulTile.HiddenDoor;
                default: return SoulTile.Floor;
            }
        }
        public SoulTile Tile(Vector2Int cell) => Tile(cell.x, cell.y);

        // Walkable for normal movement and path finding.
        public bool Open(int x, int y) => Inside(x, y) && open[y * Width + x];
        public bool Open(Vector2Int cell) => Open(cell.x, cell.y);

        // Forced movement (knockback) also passes undiscovered hidden doors.
        public bool Passable(int x, int y) => Open(x, y) || Tile(x, y) == SoulTile.HiddenDoor;

        // Sight passes floors and lava; walls and hidden doors block it (sight and attacks alike).
        public bool Transparent(int x, int y)
        {
            var tile = Tile(x, y);
            return tile == SoulTile.Floor || tile == SoulTile.Lava;
        }

        public bool IsDiscovered(Vector2Int cell) => Inside(cell.x, cell.y) && discovered[cell.y * Width + cell.x];

        public bool Discover(Vector2Int cell)
        {
            if (Tile(cell) != SoulTile.HiddenDoor || discovered[cell.y * Width + cell.x]) return false;
            discovered[cell.y * Width + cell.x] = true;
            open[cell.y * Width + cell.x] = true;
            nav.TileChanged(cell.x, cell.y);
            Revision++;
            return true;
        }

        // Line of sight between two points: every cell the ray crosses must be transparent.
        public bool Sight(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .1f));
            for (int i = 0; i <= steps; i++)
            {
                var cell = Cell(Vector2.Lerp(from, to, i / (float)steps));
                if (!Transparent(cell.x, cell.y)) return false;
            }
            return true;
        }
        public Vector2 Center(Vector2Int cell) => new Vector2(cell.x + .5f, cell.y + .5f);
        public Vector2Int Cell(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));

        public bool Clear(Vector2 point, float radius, bool forced = false)
        {
            // A point: the distance test below can never be "< 0", so check the cell itself instead.
            if (radius <= 0) { var c = Cell(point); return forced ? Passable(c.x, c.y) : Open(c); }
            int x0 = Mathf.FloorToInt(point.x - radius), x1 = Mathf.FloorToInt(point.x + radius);
            int y0 = Mathf.FloorToInt(point.y - radius), y1 = Mathf.FloorToInt(point.y + radius);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                if (forced ? Passable(x, y) : Open(x, y)) continue;
                float dx = point.x - Mathf.Clamp(point.x, x, x + 1f);
                float dy = point.y - Mathf.Clamp(point.y, y, y + 1f);
                if (dx * dx + dy * dy < radius * radius) return false;
            }
            return true;
        }

        public bool Straight(Vector2 from, Vector2 to, float radius)
        {
            float distance = Vector2.Distance(from, to);
            // 0.1-cell samples: coarser steps let a diagonal graze a wall corner between samples.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .1f));
            for (int i = 0; i <= steps; i++)
                if (!Clear(Vector2.Lerp(from, to, i / (float)steps), radius)) return false;
            return true;
        }

        // ── navigation ───────────────────────────────────────────

        // Nav cells per tile edge. Odd, so one nav cell sits on every tile center (1-wide corridors stay walkable).
        public const int NavResolution = 3;
        readonly NavGrid nav;

        public int NavWidth => Width * NavResolution;
        public int NavHeight => Height * NavResolution;
        public Vector2Int NavCell(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x * NavResolution), Mathf.FloorToInt(point.y * NavResolution));
        public Vector2 NavCenter(Vector2Int cell) => new Vector2((cell.x + .5f) / NavResolution, (cell.y + .5f) / NavResolution);
        public bool NavWalkable(Vector2Int cell, float radius) => nav.Walkable(cell.x, cell.y, NavGrid.ClassOf(radius));

        // Route from a point to a point for a body of this radius. False when unreachable.
        public bool Plan(Vector2 from, Vector2 goal, float radius, SoulPath path) => nav.Plan(from, goal, radius, path);

        // O(1) after the first call: same connected area for this body size.
        public bool Reachable(Vector2 from, Vector2 to, float radius) => nav.Reachable(from, to, radius);

        // Nearest point to `wanted` the body fits and can walk to from `from`.
        public bool ClosestReachable(Vector2 wanted, Vector2 from, float radius, out Vector2 point) => nav.ClosestReachable(wanted, from, radius, out point);

        // Tile-level wrappers (tests, spawn, UI).
        public Vector2Int ClosestOpen(Vector2Int wanted, Vector2Int from, float radius)
            => ClosestReachable(Center(wanted), Center(from), radius, out var point) ? Cell(point) : new Vector2Int(-1, -1);

        // Whole route as tiles (runs every partial stretch). Meant for checks, not per-tick use.
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, float radius)
        {
            var cells = new List<Vector2Int>();
            if (!Open(start) || !Open(goal) || !Clear(Center(goal), radius)) return cells;
            var path = new SoulPath();
            Vector2 at = Center(start), target = Center(goal);
            cells.Add(start);
            for (int guard = 0; guard < 10000; guard++)
            {
                if (!Plan(at, target, radius, path)) { cells.Clear(); return cells; }
                Vector2 previous = at;
                foreach (var point in path.Points)
                {
                    float length = Vector2.Distance(previous, point);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(length / .25f));
                    for (int i = 1; i <= steps; i++)
                    {
                        var cell = Cell(Vector2.Lerp(previous, point, i / (float)steps));
                        if (cells[cells.Count - 1] != cell) cells.Add(cell);
                    }
                    previous = point;
                }
                at = path.Points[path.Points.Count - 1];
                if (!path.Partial) break;
            }
            if (cells[cells.Count - 1] != goal) cells.Add(goal);
            return cells;
        }

        // Tile route (A*, 8 ways, no corner cutting) where stepping onto a tile costs its length × (1 + toll[tile]):
        // the way around what the caller would rather not walk past. `route` gets the tiles after `start`, up to
        // `goal`. Coarse (tile centers, not body sizes) — for picking waypoints, the walk itself uses Plan.
        public bool TileRoute(Vector2Int start, Vector2Int goal, float[] toll, List<Vector2Int> route)
        {
            route.Clear();
            if (!Open(start) || !Open(goal)) return false;
            int size = Width * Height;
            if (tileCost == null || tileCost.Length < size) { tileCost = new float[size]; tileParent = new int[size]; tileStamps = new int[size]; }
            if (tileSearch > int.MaxValue / 2 - 4) { Array.Clear(tileStamps, 0, tileStamps.Length); tileSearch = 0; }
            int stamp = ++tileSearch * 2; // stamp = seen, stamp + 1 = closed
            int from = start.y * Width + start.x, to = goal.y * Width + goal.x;
            float Octile(int x, int y) { int dx = Mathf.Abs(x - goal.x), dy = Mathf.Abs(y - goal.y); return Mathf.Max(dx, dy) + .41421356f * Mathf.Min(dx, dy); }
            tileHeap.Clear();
            tileStamps[from] = stamp; tileCost[from] = 0; tileParent[from] = -1;
            tileHeap.Push(from, Octile(start.x, start.y));
            while (tileHeap.Count > 0)
            {
                int current = tileHeap.Pop();
                if (tileStamps[current] == stamp + 1) continue;
                tileStamps[current] = stamp + 1;
                if (current == to) break;
                int cx = current % Width, cy = current / Width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (!Open(nx, ny) || dx != 0 && dy != 0 && (!Open(cx + dx, cy) || !Open(cx, cy + dy))) continue;
                    int next = ny * Width + nx;
                    if (tileStamps[next] == stamp + 1) continue;
                    float step = tileCost[current] + (dx != 0 && dy != 0 ? 1.41421356f : 1f) * (1 + (toll != null ? toll[next] : 0));
                    if (tileStamps[next] == stamp && step >= tileCost[next]) continue;
                    tileStamps[next] = stamp; tileCost[next] = step; tileParent[next] = current;
                    tileHeap.Push(next, step + Octile(nx, ny));
                }
            }
            if (tileStamps[to] != stamp + 1) return false;
            for (int id = to; id != from; id = tileParent[id]) route.Add(new Vector2Int(id % Width, id / Width));
            route.Reverse();
            return true;
        }

        float[] tileCost;
        int[] tileParent, tileStamps;
        int tileSearch;
        readonly MinHeap tileHeap = new MinHeap();

        // Connected nav regions for this body size (diagnostics).
        public int RegionCount(float radius) => nav.RegionCount(NavGrid.ClassOf(radius));

        sealed class NavGrid
        {
            // Body classes: a body of radius r uses the smallest class that is at least r.
            static readonly float[] ClassRadius = { .25f, .35f, .45f };
            const int Classes = 3, ChunkTiles = 16, Window = 4;
            public static int ClassOf(float radius)
            {
                for (int k = 0; k < Classes; k++) if (radius <= ClassRadius[k] + 1e-4f) return k;
                return Classes - 1;
            }

            readonly SoulMap map;
            readonly int w, h, chunk, chunksX, chunksY;
            readonly byte[] level;            // per nav cell: how many classes may stand here
            readonly ushort[][] label;        // per class, per nav cell: region id + 1 (0 = not walkable)
            readonly List<Region>[] regions;  // per class; dead regions stay in the list (ids are stable)
            readonly bool[] componentsDirty = new bool[Classes];

            sealed class Region
            {
                public int Chunk;
                public bool Alive = true;
                public int Component;
                public Vector2Int Cell;
                public readonly List<Edge> Edges = new List<Edge>();
            }

            struct Edge
            {
                public int To;
                public Vector2Int From, Into; // middle crossing: a cell on this side, the neighbor on the other
            }

            public NavGrid(SoulMap map)
            {
                this.map = map;
                w = map.Width * NavResolution; h = map.Height * NavResolution;
                chunk = ChunkTiles * NavResolution;
                chunksX = (w + chunk - 1) / chunk; chunksY = (h + chunk - 1) / chunk;
                level = new byte[w * h];
                label = new ushort[Classes][];
                regions = new List<Region>[Classes];
                for (int k = 0; k < Classes; k++) { label[k] = new ushort[w * h]; regions[k] = new List<Region>(); }
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) level[y * w + x] = Level(x, y);
                for (int k = 0; k < Classes; k++)
                {
                    for (int c = 0; c < chunksX * chunksY; c++) LabelChunk(k, c);
                    for (int c = 0; c < chunksX * chunksY; c++) { LinkBorder(k, c, c + 1, true); LinkBorder(k, c, c + chunksX, false); }
                    componentsDirty[k] = true;
                }
            }

            public int RegionCount(int k) { int n = 0; foreach (var r in regions[k]) if (r.Alive) n++; return n; }

            // Distance from the nav cell center to the nearest blocked tile, expressed as a class count.
            // Only the 3×3 tiles around matter: anything further is more than the largest class radius away.
            byte Level(int nx, int ny)
            {
                float px = (nx + .5f) / NavResolution, py = (ny + .5f) / NavResolution;
                int tx = nx / NavResolution, ty = ny / NavResolution;
                if (!map.Open(tx, ty)) return 0;
                float best = float.MaxValue;
                for (int y = ty - 1; y <= ty + 1; y++) for (int x = tx - 1; x <= tx + 1; x++)
                {
                    if (map.Open(x, y)) continue;
                    float dx = px - Mathf.Clamp(px, x, x + 1f), dy = py - Mathf.Clamp(py, y, y + 1f);
                    best = Mathf.Min(best, dx * dx + dy * dy);
                }
                byte count = 0;
                for (int k = 0; k < Classes; k++) if (best >= ClassRadius[k] * ClassRadius[k]) count++;
                return count;
            }

            public bool Walkable(int x, int y, int k) => x >= 0 && y >= 0 && x < w && y < h && level[y * w + x] > k;

            RectInt ChunkRect(int c)
            {
                int cx = c % chunksX, cy = c / chunksX;
                return new RectInt(cx * chunk, cy * chunk, Mathf.Min(chunk, w - cx * chunk), Mathf.Min(chunk, h - cy * chunk));
            }

            int ChunkOf(int x, int y) => (y / chunk) * chunksX + x / chunk;

            readonly Stack<int> fill = new Stack<int>();

            // 4-connected regions inside one chunk (a diagonal step without corner cutting is 4-connected too).
            void LabelChunk(int k, int c)
            {
                var rect = ChunkRect(c);
                var cells = label[k];
                for (int y = rect.yMin; y < rect.yMax; y++) for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    int i = y * w + x;
                    if (cells[i] != 0 || level[i] <= k) continue;
                    var region = new Region { Chunk = c, Cell = new Vector2Int(x, y) };
                    regions[k].Add(region);
                    if (regions[k].Count >= ushort.MaxValue) throw new InvalidOperationException("Too many nav regions.");
                    ushort id = (ushort)regions[k].Count;
                    long sx = 0, sy = 0; int n = 0;
                    cells[i] = id; fill.Push(i);
                    while (fill.Count > 0)
                    {
                        int at = fill.Pop();
                        int ax = at % w, ay = at / w;
                        sx += ax; sy += ay; n++;
                        if (ax > rect.xMin) Visit(k, at - 1, id);
                        if (ax < rect.xMax - 1) Visit(k, at + 1, id);
                        if (ay > rect.yMin) Visit(k, at - w, id);
                        if (ay < rect.yMax - 1) Visit(k, at + w, id);
                    }
                    // Representative cell: the member nearest to the centroid.
                    region.Cell = Nearest(k, id, rect, new Vector2((float)sx / n, (float)sy / n));
                }
            }

            void Visit(int k, int i, ushort id)
            {
                if (label[k][i] != 0 || level[i] <= k) return;
                label[k][i] = id; fill.Push(i);
            }

            Vector2Int Nearest(int k, ushort id, RectInt rect, Vector2 centroid)
            {
                var best = new Vector2Int(-1, -1);
                float bestDistance = float.MaxValue;
                for (int y = rect.yMin; y < rect.yMax; y++) for (int x = rect.xMin; x < rect.xMax; x++)
                {
                    if (label[k][y * w + x] != id) continue;
                    float d = (x - centroid.x) * (x - centroid.x) + (y - centroid.y) * (y - centroid.y);
                    if (d < bestDistance) { bestDistance = d; best = new Vector2Int(x, y); }
                }
                return best;
            }

            // Edges across the border of chunk a and its right (horizontal) or lower neighbor b: one per
            // contiguous run of crossings between the same two regions, through the run's middle.
            void LinkBorder(int k, int a, int b, bool horizontal)
            {
                if (horizontal ? (a % chunksX == chunksX - 1 || b >= chunksX * chunksY) : b >= chunksX * chunksY) return;
                var ra = ChunkRect(a);
                int length = horizontal ? ra.height : ra.width;
                int runStart = -1, runA = 0, runB = 0;
                for (int t = 0; t <= length; t++)
                {
                    int la = 0, lb = 0;
                    if (t < length)
                    {
                        int ax = horizontal ? ra.xMax - 1 : ra.xMin + t, ay = horizontal ? ra.yMin + t : ra.yMax - 1;
                        int bx = horizontal ? ax + 1 : ax, by = horizontal ? ay : ay + 1;
                        la = label[k][ay * w + ax]; lb = label[k][by * w + bx];
                        if (la == 0 || lb == 0) la = lb = 0;
                    }
                    if (runStart >= 0 && (la != runA || lb != runB))
                    {
                        int mid = (runStart + t - 1) / 2;
                        var from = horizontal ? new Vector2Int(ra.xMax - 1, ra.yMin + mid) : new Vector2Int(ra.xMin + mid, ra.yMax - 1);
                        var into = horizontal ? from + Vector2Int.right : from + Vector2Int.up;
                        regions[k][runA - 1].Edges.Add(new Edge { To = runB - 1, From = from, Into = into });
                        regions[k][runB - 1].Edges.Add(new Edge { To = runA - 1, From = into, Into = from });
                        runStart = -1;
                    }
                    if (runStart < 0 && la != 0) { runStart = t; runA = la; runB = lb; }
                }
            }

            // A hidden door opened: re-derive the nav cells around it and re-cut the chunks they touch.
            public void TileChanged(int tx, int ty)
            {
                var touched = new HashSet<int>();
                for (int ny = (ty - 1) * NavResolution; ny < (ty + 2) * NavResolution; ny++)
                    for (int nx = (tx - 1) * NavResolution; nx < (tx + 2) * NavResolution; nx++)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        level[ny * w + nx] = Level(nx, ny);
                        touched.Add(ChunkOf(nx, ny));
                    }
                for (int k = 0; k < Classes; k++)
                {
                    foreach (int c in touched)
                    {
                        // Drop the chunk's regions and every edge pointing at them.
                        for (int id = 0; id < regions[k].Count; id++)
                        {
                            var region = regions[k][id];
                            if (!region.Alive || region.Chunk != c) continue;
                            region.Alive = false;
                            foreach (var edge in region.Edges) regions[k][edge.To].Edges.RemoveAll(e => e.To == id);
                            region.Edges.Clear();
                        }
                        var rect = ChunkRect(c);
                        for (int y = rect.yMin; y < rect.yMax; y++) for (int x = rect.xMin; x < rect.xMax; x++) label[k][y * w + x] = 0;
                    }
                    foreach (int c in touched) LabelChunk(k, c);
                    var linked = new HashSet<long>();
                    foreach (int c in touched)
                    {
                        int cx = c % chunksX;
                        if (cx > 0 && linked.Add(Pair(c - 1, c))) LinkBorder(k, c - 1, c, true);
                        if (linked.Add(Pair(c, c + 1))) LinkBorder(k, c, c + 1, true);
                        if (c - chunksX >= 0 && linked.Add(Pair(c - chunksX, c))) LinkBorder(k, c - chunksX, c, false);
                        if (linked.Add(Pair(c, c + chunksX))) LinkBorder(k, c, c + chunksX, false);
                    }
                    componentsDirty[k] = true;
                }
            }

            static long Pair(int a, int b) => (long)a << 32 | (uint)b;

            void Components(int k)
            {
                if (!componentsDirty[k]) return;
                componentsDirty[k] = false;
                var list = regions[k];
                foreach (var region in list) region.Component = -1;
                var queue = new Queue<int>();
                int component = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!list[i].Alive || list[i].Component >= 0) continue;
                    list[i].Component = component;
                    queue.Enqueue(i);
                    while (queue.Count > 0)
                        foreach (var edge in list[queue.Dequeue()].Edges)
                            if (list[edge.To].Component < 0) { list[edge.To].Component = component; queue.Enqueue(edge.To); }
                    component++;
                }
            }

            int RegionAt(int k, Vector2Int cell) => cell.x < 0 || cell.y < 0 || cell.x >= w || cell.y >= h ? -1 : label[k][cell.y * w + cell.x] - 1;

            // The nav cell a body at this point starts from: its own, or the nearest walkable one it can step to.
            bool Anchor(Vector2 point, float radius, int k, out Vector2Int cell)
            {
                cell = map.NavCell(point);
                if (Walkable(cell.x, cell.y, k)) return true;
                var origin = cell;
                float best = float.MaxValue;
                bool found = false;
                for (int ring = 1; ring <= 3 && !found; ring++)
                    for (int dy = -ring; dy <= ring; dy++) for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                        var candidate = origin + new Vector2Int(dx, dy);
                        if (!Walkable(candidate.x, candidate.y, k)) continue;
                        float d = (map.NavCenter(candidate) - point).sqrMagnitude;
                        if (d < best) { best = d; cell = candidate; found = true; }
                    }
                return found;
            }

            public bool Reachable(Vector2 from, Vector2 to, float radius)
            {
                int k = ClassOf(radius);
                if (!Anchor(from, radius, k, out var a) || !Anchor(to, radius, k, out var b)) return false;
                Components(k);
                return regions[k][RegionAt(k, a)].Component == regions[k][RegionAt(k, b)].Component;
            }

            public bool ClosestReachable(Vector2 wanted, Vector2 from, float radius, out Vector2 point)
            {
                point = wanted;
                int k = ClassOf(radius);
                if (!Anchor(from, radius, k, out var start)) return false;
                Components(k);
                int component = regions[k][RegionAt(k, start)].Component;
                if (map.Clear(wanted, radius) && Anchor(wanted, radius, k, out var direct) && regions[k][RegionAt(k, direct)].Component == component)
                    return true;
                // Rings of nav cells around the wanted point (a click on a wall, on lava, across a gap).
                var origin = map.NavCell(wanted);
                int limit = Mathf.Max(w, h);
                for (int ring = 1; ring < limit; ring++)
                {
                    float best = float.MaxValue;
                    bool found = false;
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        int step = Mathf.Abs(dy) == ring ? 1 : 2 * ring;
                        for (int dx = -ring; dx <= ring; dx += step)
                        {
                            var cell = origin + new Vector2Int(dx, dy);
                            if (!Walkable(cell.x, cell.y, k) || regions[k][RegionAt(k, cell)].Component != component) continue;
                            float d = (map.NavCenter(cell) - wanted).sqrMagnitude;
                            if (d < best) { best = d; point = map.NavCenter(cell); found = true; }
                        }
                    }
                    if (found) return true;
                }
                return false;
            }

            // ── route search ─────────────────────────────────────

            readonly List<int> corridor = new List<int>();
            readonly List<Edge> corridorEdges = new List<Edge>();
            readonly List<Vector2Int> cells = new List<Vector2Int>();

            public bool Plan(Vector2 from, Vector2 goal, float radius, SoulPath path)
            {
                path.Clear();
                int k = ClassOf(radius);
                if (!Anchor(from, radius, k, out var start) || !Anchor(goal, radius, k, out var target)) return false;
                bool exactGoal = map.Clear(goal, radius);
                Vector2 end = exactGoal ? goal : map.NavCenter(target);
                // Close and in the open: straight there.
                if ((end - from).sqrMagnitude < 144f && map.Straight(from, end, radius)) { path.Points.Add(end); return true; }
                Components(k);
                int startRegion = RegionAt(k, start), goalRegion = RegionAt(k, target);
                if (regions[k][startRegion].Component != regions[k][goalRegion].Component) return false;
                if (!Corridor(k, startRegion, goalRegion, start, map.NavCenter(target))) return false;

                // Detail only the first Window regions; a longer corridor ends at the crossing into the next one.
                int last = Mathf.Min(corridor.Count - 1, Window);
                bool partial = last < corridor.Count - 1;
                var aim = partial ? corridorEdges[last].Into : target;
                if (!Detail(k, start, aim, last)) return false;
                Simplify(path, from);
                if (path.Points.Count > 0 && !map.Straight(from, path.Points[0], radius)) path.Points.Insert(0, map.NavCenter(start));
                if (!partial)
                {
                    if (path.Points.Count > 0) path.Points[path.Points.Count - 1] = end;
                    else path.Points.Add(end);
                }
                path.Partial = partial;
                return path.Points.Count > 0;
            }

            // Region-graph A*: g follows the crossing points actually used, h is the straight distance.
            float[] regionCost = new float[0];
            int[] regionStamp = new int[0], regionParent = new int[0];
            Vector2Int[] regionEntry = new Vector2Int[0];
            Edge[] regionVia = new Edge[0];
            int regionSearch;
            readonly MinHeap heap = new MinHeap();

            bool Corridor(int k, int startRegion, int goalRegion, Vector2Int startCell, Vector2 goalPoint)
            {
                corridor.Clear(); corridorEdges.Clear();
                if (startRegion == goalRegion) { corridor.Add(startRegion); return true; }
                var list = regions[k];
                if (regionCost.Length < list.Count)
                {
                    int size = list.Count * 2;
                    regionCost = new float[size]; regionStamp = new int[size]; regionParent = new int[size];
                    regionEntry = new Vector2Int[size]; regionVia = new Edge[size];
                }
                int stamp = ++regionSearch * 2; // stamp = seen, stamp + 1 = closed
                heap.Clear();
                regionStamp[startRegion] = stamp; regionCost[startRegion] = 0; regionParent[startRegion] = -1;
                regionEntry[startRegion] = startCell;
                heap.Push(startRegion, 0);
                while (heap.Count > 0)
                {
                    int current = heap.Pop();
                    if (regionStamp[current] == stamp + 1) continue;
                    regionStamp[current] = stamp + 1;
                    if (current == goalRegion) break;
                    var entry = regionEntry[current];
                    foreach (var edge in list[current].Edges)
                    {
                        if (regionStamp[edge.To] == stamp + 1) continue;
                        float cost = regionCost[current] + Distance(entry, edge.From) + 1;
                        if (regionStamp[edge.To] == stamp && cost >= regionCost[edge.To]) continue;
                        regionStamp[edge.To] = stamp; regionCost[edge.To] = cost;
                        regionParent[edge.To] = current; regionEntry[edge.To] = edge.Into; regionVia[edge.To] = edge;
                        heap.Push(edge.To, cost + Vector2.Distance(map.NavCenter(edge.Into), goalPoint) * NavResolution);
                    }
                }
                if (regionStamp[goalRegion] != stamp + 1) return false;
                for (int r = goalRegion; r >= 0; r = regionParent[r]) { corridor.Add(r); if (regionParent[r] >= 0) corridorEdges.Add(regionVia[r]); }
                corridor.Reverse(); corridorEdges.Reverse();
                // corridorEdges[i] leads from corridor[i] into corridor[i + 1]; index by the region entered.
                corridorEdges.Insert(0, default);
                return true;
            }

            static float Distance(Vector2Int a, Vector2Int b)
            {
                int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
                return Mathf.Max(dx, dy) + .41421356f * Mathf.Min(dx, dy);
            }

            // Nav-grid A* inside the bounding box of the corridor's first regions, on their cells only.
            float[] cost = new float[0];
            int[] parent = new int[0], stamps = new int[0];
            int search;
            int[] allowedMark = new int[0];
            int allowedStamp;

            bool Detail(int k, Vector2Int start, Vector2Int aim, int last)
            {
                if (allowedMark.Length < regions[k].Count + 1) allowedMark = new int[(regions[k].Count + 1) * 2];
                int mark = ++allowedStamp;
                RectInt box = ChunkRect(regions[k][corridor[0]].Chunk);
                for (int i = 0; i <= last; i++)
                {
                    allowedMark[corridor[i] + 1] = mark;
                    var rect = ChunkRect(regions[k][corridor[i]].Chunk);
                    int x0 = Mathf.Min(box.xMin, rect.xMin), y0 = Mathf.Min(box.yMin, rect.yMin);
                    int x1 = Mathf.Max(box.xMax, rect.xMax), y1 = Mathf.Max(box.yMax, rect.yMax);
                    box = new RectInt(x0, y0, x1 - x0, y1 - y0);
                }
                int bw = box.width, size = bw * box.height;
                if (cost.Length < size) { cost = new float[size]; parent = new int[size]; stamps = new int[size]; }
                if (search > int.MaxValue / 2 - 4) { Array.Clear(stamps, 0, stamps.Length); search = 0; }
                int stamp = ++search * 2;
                var cellsOfClass = label[k];
                int Local(Vector2Int c) => (c.y - box.yMin) * bw + (c.x - box.xMin);
                bool Allowed(int x, int y) => x >= box.xMin && y >= box.yMin && x < box.xMax && y < box.yMax && allowedMark[cellsOfClass[y * w + x]] == mark;

                heap.Clear();
                int startId = Local(start), aimId = Local(aim);
                stamps[startId] = stamp; cost[startId] = 0; parent[startId] = -1;
                heap.Push(startId, Distance(start, aim));
                bool reached = false;
                while (heap.Count > 0)
                {
                    int current = heap.Pop();
                    if (stamps[current] == stamp + 1) continue;
                    stamps[current] = stamp + 1;
                    if (current == aimId) { reached = true; break; }
                    int cx = current % bw + box.xMin, cy = current / bw + box.yMin;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (!Allowed(nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (!Allowed(cx + dx, cy) || !Allowed(cx, cy + dy))) continue; // no corner cutting
                        int next = (ny - box.yMin) * bw + (nx - box.xMin);
                        if (stamps[next] == stamp + 1) continue;
                        float step = cost[current] + (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                        if (stamps[next] == stamp && step >= cost[next]) continue;
                        stamps[next] = stamp; cost[next] = step; parent[next] = current;
                        heap.Push(next, step + Distance(new Vector2Int(nx, ny), aim) * 1.001f);
                    }
                }
                cells.Clear();
                if (!reached) return false;
                for (int id = aimId; id >= 0; id = parent[id]) cells.Add(new Vector2Int(id % bw + box.xMin, id / bw + box.yMin));
                cells.Reverse();
                return true;
            }

            // Keep only the cells where the direction changes (the walker string-pulls the rest).
            void Simplify(SoulPath path, Vector2 from)
            {
                for (int i = 1; i < cells.Count; i++)
                {
                    bool turn = i == cells.Count - 1 || cells[i + 1] - cells[i] != cells[i] - cells[i - 1];
                    if (turn) path.Points.Add(map.NavCenter(cells[i]));
                }
                if (path.Points.Count == 0 && cells.Count > 0) path.Points.Add(map.NavCenter(cells[0]));
            }
        }

        sealed class MinHeap
        {
            int[] ids = new int[256];
            float[] scores = new float[256];
            public int Count { get; private set; }
            public void Clear() => Count = 0;
            public void Push(int id, float score)
            {
                if (Count == ids.Length) { Array.Resize(ref ids, Count * 2); Array.Resize(ref scores, Count * 2); }
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (scores[p] <= score) break;
                    ids[i] = ids[p]; scores[i] = scores[p]; i = p;
                }
                ids[i] = id; scores[i] = score;
            }
            public int Pop()
            {
                int result = ids[0];
                int lastId = ids[--Count]; float lastScore = scores[Count];
                if (Count == 0) return result;
                int i = 0;
                while (i * 2 + 1 < Count)
                {
                    int c = i * 2 + 1;
                    if (c + 1 < Count && scores[c + 1] < scores[c]) c++;
                    if (lastScore <= scores[c]) break;
                    ids[i] = ids[c]; scores[i] = scores[c]; i = c;
                }
                ids[i] = lastId; scores[i] = lastScore;
                return result;
            }
        }
    }
}
