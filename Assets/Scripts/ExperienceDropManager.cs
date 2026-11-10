using System.Collections.Generic;
using UnityEngine;

// Owns one run's drops independently of disposable map chunks. No Physics2D objects per pickup.
[DefaultExecutionOrder(200), DisallowMultipleComponent]
public sealed class ExperienceDropManager : MonoBehaviour
{
    public static ExperienceDropManager Instance { get; private set; }
    [SerializeField] private PlayerProgression player;
    [SerializeField] private MapChunkManager map;
    [SerializeField] private ExperiencePickupView viewPrefab;
    [SerializeField, Min(.01f)] private float attractionRadius = 2f;
    [SerializeField, Min(.01f)] private float collectRadius = .3f;
    [SerializeField, Min(.01f)] private float initialSpeed = 6f;
    [SerializeField, Min(0f)] private float acceleration = 40f;
    [SerializeField, Min(.01f)] private float maxSpeed = 24f;
    [SerializeField, Min(.01f)] private float mergeRadius = .75f;
    [SerializeField, Min(.01f)] private float searchInterval = .05f;

    sealed class Drop
    {
        public long id;
        public int amount;
        public Vector2 position;
        public Vector2Int cell, chunk;
        public bool attracting;
        public bool magnetCarrier;
        public float speed;
        public ExperiencePickupView view;
    }
    const float CellSize = 2f;
    readonly Dictionary<long, Drop> drops = new Dictionary<long, Drop>();
    readonly Dictionary<Vector2Int, HashSet<long>> cells = new Dictionary<Vector2Int, HashSet<long>>();
    readonly Dictionary<Vector2Int, HashSet<long>> chunks = new Dictionary<Vector2Int, HashSet<long>>();
    readonly HashSet<long> visible = new HashSet<long>();
    readonly List<Drop> attracting = new List<Drop>();
    readonly Stack<ExperiencePickupView> pool = new Stack<ExperiencePickupView>();
    readonly List<long> scratch = new List<long>();
    readonly List<Vector2> path = new List<Vector2>();
    readonly Queue<int> magnetQueue = new Queue<int>();
    long magnetQueuedAmount;
    int magnetCarriers;
    int magnetSequence;
    public long MagnetQueuedAmount => magnetQueuedAmount;
    public int MagnetCarrierCount => magnetCarriers;
    public const int MaxMagnetCarriers = 16;
    long nextId;
    Vector2 previousPosition;
    Vector2Int visibleCenter;
    bool initialized;
    float searchElapsed;
    public long SpawnedTotal { get; private set; }
    public long CollectedTotal { get; private set; }
    public long PendingTotal => SpawnedTotal - CollectedTotal;
    public int RecordCount => drops.Count;
    public int VisibleCount => visible.Count;
    public int AttractingCount => attracting.Count;
    public bool HasWaitingExperience => cells.Count > 0;

    // A one-time snapshot: later drops and already moving pickups are excluded.
    public bool TryCollectAllWaiting()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || player == null || player.IsPlayerDead || !player.isActiveAndEnabled || map == null || map.GameCamera == null || viewPrefab == null
            || (GameSession.Instance != null && GameSession.Instance.IsGameOver)) return false;
        var camera = map.GameCamera;
        var waiting = new List<Drop>();
        foreach (var d in drops.Values) if (!d.attracting) waiting.Add(d);
        if (waiting.Count == 0) return false;
        long remoteAmount = 0;
        foreach (var d in waiting)
        {
            Remove(cells, d.cell, d.id); Remove(chunks, d.chunk, d.id);
            Vector3 viewport = camera.WorldToViewportPoint(d.position);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1)
            { d.attracting = true; d.speed = initialSpeed; attracting.Add(d); Show(d); }
            else { remoteAmount += d.amount; Hide(d); drops.Remove(d.id); }
        }
        // Bundle distant records without creating thousands of GameObjects. Exact integer amounts transfer to the queue.
        int bundles = (int)System.Math.Min(16L, remoteAmount);
        for (int i = bundles; i > 0; i--)
        {
            long share = remoteAmount / i;
            while (share > 0)
            {
                int part = (int)System.Math.Min(int.MaxValue, share);
                magnetQueue.Enqueue(part); magnetQueuedAmount += part;
                share -= part; remoteAmount -= part;
            }
        }
        return true;
    }

    void StartMagnetCarriers()
    {
        var camera = map.GameCamera;
        if (camera == null) return;
        while (magnetCarriers < MaxMagnetCarriers && magnetQueue.Count > 0)
        {
            int amount = magnetQueue.Dequeue(); magnetQueuedAmount -= amount;
            float angle = magnetSequence++ * 2.399963f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 half = new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize) * .96f;
            float length = 1f / Mathf.Max(Mathf.Abs(direction.x) / half.x, Mathf.Abs(direction.y) / half.y);
            Vector2 position = (Vector2)camera.transform.position + direction * length;
            var d = new Drop { id = ++nextId, amount = amount, position = position, attracting = true, magnetCarrier = true, speed = initialSpeed };
            drops.Add(d.id, d); attracting.Add(d); magnetCarriers++; Show(d);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Debug.LogError("Duplicate ExperienceDropManager", this); enabled = false; return; }
        Instance = this;
    }
    void Start()
    {
        if (player == null || map == null || viewPrefab == null)
        { Debug.LogError("Experience drops: run Tools > Game UI > Setup Experience Drops in the game scene.", this); enabled = false; }
    }
    public static void Spawn(Vector2 position, int amount)
    {
        if (amount <= 0) return;
        if (Instance == null || !Instance.isActiveAndEnabled || Instance.map == null || Instance.viewPrefab == null)
        { Debug.LogError("Experience drop manager is missing or not configured. Experience was not awarded."); return; }
        Instance.Add(position, amount);
    }
    static Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.y / CellSize));
    static void Insert(Dictionary<Vector2Int, HashSet<long>> index, Vector2Int key, long id)
    { if (!index.TryGetValue(key, out var ids)) index.Add(key, ids = new HashSet<long>()); ids.Add(id); }
    static void Remove(Dictionary<Vector2Int, HashSet<long>> index, Vector2Int key, long id)
    { if (index.TryGetValue(key, out var ids)) { ids.Remove(id); if (ids.Count == 0) index.Remove(key); } }
    bool Nearby(Vector2Int c) => Mathf.Abs(c.x - visibleCenter.x) <= 1 && Mathf.Abs(c.y - visibleCenter.y) <= 1;

    void Add(Vector2 position, int amount)
    {
        Drop nearest = null;
        float best = mergeRadius * mergeRadius;
        Vector2Int lo = Cell(position - Vector2.one * mergeRadius), hi = Cell(position + Vector2.one * mergeRadius);
        for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
        {
            if (!cells.TryGetValue(new Vector2Int(x,y), out var ids)) continue;
            foreach (long id in ids)
            {
                Drop d = drops[id]; float distance = (d.position - position).sqrMagnitude;
                if (distance <= best && d.amount <= int.MaxValue - amount &&
                    (nearest == null || distance < best || d.id < nearest.id)) { best = distance; nearest = d; }
            }
        }
        SpawnedTotal += amount;
        if (nearest != null) { nearest.amount += amount; if (nearest.view != null) nearest.view.Show(nearest.amount); return; }
        var item = new Drop { id = ++nextId, position = position, amount = amount, cell = Cell(position), chunk = map.GetChunkCoordinate(position) };
        drops.Add(item.id, item); Insert(cells, item.cell, item.id); Insert(chunks, item.chunk, item.id);
        if (initialized && Nearby(item.chunk)) Show(item);
    }
    void Show(Drop d)
    {
        if (d.view != null) return;
        d.view = pool.Count > 0 ? pool.Pop() : Instantiate(viewPrefab, transform);
        d.view.transform.position = d.position;
        d.view.Show(d.amount); d.view.gameObject.SetActive(true); visible.Add(d.id);
    }
    void Hide(Drop d)
    {
        if (d.view == null) return;
        d.view.gameObject.SetActive(false); pool.Push(d.view); d.view = null; visible.Remove(d.id);
    }
    void RefreshViews(Vector2 position)
    {
        Vector2Int center = map.GetChunkCoordinate(position);
        if (initialized && center == visibleCenter) return;
        visibleCenter = center;
        scratch.Clear();
        foreach (long id in visible) if (!drops[id].attracting && !Nearby(drops[id].chunk)) scratch.Add(id);
        foreach (long id in scratch) Hide(drops[id]);
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            if (chunks.TryGetValue(center + new Vector2Int(x,y), out var ids)) foreach (long id in ids) Show(drops[id]);
    }
    public static float SegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float t = delta.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(point-a, delta)/delta.sqrMagnitude) : 0f;
        return (point - (a + delta*t)).sqrMagnitude;
    }
    void SearchSegment(Vector2 a, Vector2 b)
    {
        // Split long traversals so searches remain a narrow strip rather than a huge diagonal rectangle.
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a,b) / CellSize));
        for (int step = 0; step < steps; step++)
        {
            Vector2 start = Vector2.Lerp(a,b,(float)step/steps), end = Vector2.Lerp(a,b,(float)(step+1)/steps);
            Vector2Int lo = Cell(Vector2.Min(start,end)-Vector2.one*attractionRadius), hi = Cell(Vector2.Max(start,end)+Vector2.one*attractionRadius);
            scratch.Clear();
            for (int y=lo.y;y<=hi.y;y++) for (int x=lo.x;x<=hi.x;x++)
                if (cells.TryGetValue(new Vector2Int(x,y),out var ids)) foreach(long id in ids)
                    if (SegmentDistanceSquared(drops[id].position,start,end) <= attractionRadius*attractionRadius) scratch.Add(id);
            foreach(long id in scratch)
            {
                Drop d=drops[id]; d.attracting=true; d.speed=initialSpeed;
                Remove(cells,d.cell,id); Remove(chunks,d.chunk,id); attracting.Add(d); Show(d);
            }
        }
    }
    void LateUpdate()
    {
        if (player == null || map == null || viewPrefab == null) return;
        Vector2 position = player.transform.position;
        if (Time.timeScale <= 0f || player.IsPlayerDead || !player.isActiveAndEnabled || (GameSession.Instance != null && GameSession.Instance.IsGameOver))
        { previousPosition=position; path.Clear(); path.Add(position); return; }
        RefreshViews(position);
        if (!initialized) { previousPosition=position; path.Add(position); initialized=true; }
        StartMagnetCarriers();
        float dt=Time.deltaTime;
        path.Add(position); searchElapsed+=dt;
        if (searchElapsed>=searchInterval)
        {
            for(int i=1;i<path.Count;i++) SearchSegment(path[i-1],path[i]);
            path.Clear(); path.Add(position); searchElapsed=0f;
        }
        float cap=Mathf.Max(maxSpeed,dt>0 ? Vector2.Distance(position,previousPosition)/dt+6f : maxSpeed);
        long award=0;
        for(int i=attracting.Count-1;i>=0;i--)
        {
            Drop d=attracting[i]; Vector2 old=d.position;
            d.speed=Mathf.MoveTowards(d.speed,cap,acceleration*dt);
            d.position=Vector2.MoveTowards(old,position,d.speed*dt);
            // Relative motion sweep detects crossings by either the player or the pickup.
            if(SegmentDistanceSquared(Vector2.zero,old-previousPosition,d.position-position)<=collectRadius*collectRadius)
            {
                award+=d.amount; Hide(d); drops.Remove(d.id); attracting.RemoveAt(i);
                if (d.magnetCarrier) magnetCarriers--;
            }
            else d.view.transform.position=d.position;
        }
        previousPosition=position;
        CollectedTotal+=award;
        while(award>0) { int batch=(int)System.Math.Min(int.MaxValue,award); player.AddExperience(batch); award-=batch; }
    }
    void OnDestroy() { if(Instance==this) Instance=null; }
}
