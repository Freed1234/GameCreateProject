using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(250), DisallowMultipleComponent]
public sealed class ChestManager : MonoBehaviour
{
    public enum Reward { Heal, Magnet }
    sealed class ChestRecord
    {
        public Vector2Int chunk;
        public Vector2 position;
        public Reward reward;
        public bool hasChest, opened, claimed;
        public float openedAt, claimedAt;
        public ChestView view;
        public bool attracting;
        public Vector2 itemPosition;
        public float itemSpeed;
    }
    [SerializeField] private PlayerHealth health;
    [SerializeField] private MapChunkManager map;
    [SerializeField] private EnemyFlowField navigation;
    [SerializeField] private ExperienceDropManager experience;
    [SerializeField] private ChestView viewPrefab;
    [SerializeField, Range(0f,1f)] private float spawnChance = .4f;
    [SerializeField, Min(.1f)] private float openRadius = 1.2f;
    [Tooltip("플레이어에게 도착했다고 판단하는 거리 (기존 설정 유지)")]
    [SerializeField, Min(.1f)] private float pickupRadius = .65f;
    [Header("아이템 흡수")]
    [SerializeField, Min(.1f)] private float itemInitialSpeed = 6f;
    [SerializeField, Min(.1f)] private float itemAcceleration = 40f;
    [SerializeField, Min(.1f)] private float itemMaxSpeed = 24f;
    [SerializeField, Min(.01f)] private float openingDuration = .2f;
    [SerializeField, Min(.01f)] private float pickupDelay = .35f;
    [SerializeField, Min(.01f)] private float fadeDuration = 1.2f;
    [SerializeField, Min(1)] private int healAmount = 3;
    [SerializeField, Min(0f)] private float healWeight = 50, magnetWeight = 20;
    [Tooltip("0: 새 게임마다 새 시드. 그 외 값: 배치 재현용 고정 시드")]
    [SerializeField] private int seedOverride;
    [SerializeField] private AudioClip openSound, pickupSound;
    [SerializeField, Range(0f,1f)] private float soundVolume = .8f;
    [SerializeField] private Font feedbackFont;
    readonly Dictionary<Vector2Int,ChestRecord> records = new Dictionary<Vector2Int,ChestRecord>();
    readonly List<ChestRecord> active = new List<ChestRecord>();
    readonly Stack<ChestView> pool = new Stack<ChestView>();
    Vector2 startPosition, previousPosition;
    int runSeed;
    AudioSource audioSource;
    string feedback;
    float feedbackUntil;
    GUIStyle feedbackStyle;
    Font runtimeFont;
    public int ExploredChunkCount => records.Count;
    public int ActiveChestCount => active.Count;

    void Start()
    {
        if (health == null || map == null || navigation == null || experience == null || viewPrefab == null)
        { Debug.LogError("Tools > Game UI > Setup Chests로 필수 참조를 연결해주세요.",this); enabled=false; return; }
        if(feedbackFont==null) { runtimeFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},32); feedbackFont=runtimeFont; }
        startPosition=previousPosition=health.transform.position;
        runSeed=seedOverride!=0?seedOverride:unchecked((int)DateTime.UtcNow.Ticks);
        audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false; audioSource.volume=1f;
        FindAnyObjectByType<AudioVolumeController>()?.RegisterSource(audioSource);
    }
    static int Seed(int seed, Vector2Int chunk)
    { unchecked { return (seed*397 ^ chunk.x)*397 ^ chunk.y; } }
    Reward ChooseReward(System.Random random)
    {
        float a=Mathf.Max(0,healWeight),b=Mathf.Max(0,magnetWeight);
        double pick=random.NextDouble()*(a+b);
        return pick<a?Reward.Heal:Reward.Magnet;
    }
    ChestRecord CreateRecord(Vector2Int coordinate)
    {
        var random=new System.Random(Seed(runSeed,coordinate));
        var record=new ChestRecord{chunk=coordinate};
        bool chosen=random.NextDouble()<spawnChance;
        record.reward=ChooseReward(random);
        if(!chosen || healWeight+magnetWeight<=0) return record;
        float half=map.ChunkSize*.5f-2f;
        if(half<=0) return record;
        Vector2 center=new Vector2(coordinate.x*map.ChunkSize,coordinate.y*map.ChunkSize);
        for(int i=0;i<16;i++)
        {
            Vector2 point=center+new Vector2((float)(random.NextDouble()*2-1)*half,(float)(random.NextDouble()*2-1)*half);
            if(Vector2.Distance(point,startPosition)<5f || !navigation.IsPositionClear(point) || !navigation.TryGetWaypoint(point,out _)) continue;
            record.position=point; record.hasChest=true; break;
        }
        return record;
    }
    bool Nearby(Vector2Int a,Vector2Int b)=>Mathf.Abs(a.x-b.x)<=1 && Mathf.Abs(a.y-b.y)<=1;
    float Opacity(ChestRecord record) => record.claimed
        ? 1f - Mathf.Clamp01((Time.time - record.claimedAt) / Mathf.Max(.01f, fadeDuration))
        : 1f;

    void RefreshChests(Vector2 position)
    {
        Vector2Int center=map.GetChunkCoordinate(position);
        for(int i=active.Count-1;i>=0;i--)
        {
            var record=active[i];if(Opacity(record)>0f && (Nearby(record.chunk,center) || (record.opened && !record.claimed)))continue;
            record.view.gameObject.SetActive(false);pool.Push(record.view);record.view=null;active.RemoveAt(i);
        }
        // Map layout and the shared path grid must both finish before recording a failed placement.
        if(!navigation.Ready) return;
        for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
        {
            Vector2Int coordinate=center+new Vector2Int(x,y);
            if(!records.TryGetValue(coordinate,out var record)) {record=CreateRecord(coordinate);records.Add(coordinate,record);}
            if(!record.hasChest || record.view!=null || Opacity(record)<=0f)continue;
            record.view=pool.Count>0?pool.Pop():Instantiate(viewPrefab,transform);
            record.view.transform.position=record.position;record.view.gameObject.SetActive(true);active.Add(record);
            UpdateView(record);
        }
    }
    void UpdateView(ChestRecord record)
    {
        record.view.Display(record.opened,record.claimed,record.reward,record.opened?Mathf.Clamp01((Time.time-record.openedAt)/Mathf.Max(.01f,openingDuration)):0, Opacity(record));
        record.view.SetAttractionVisual(Nearby(record.chunk,map.GetChunkCoordinate(health.transform.position)),record.attracting,record.itemPosition);
    }
    bool CanUse(Reward reward)
    {
        if(reward==Reward.Heal) return health.isActiveAndEnabled && !health.IsDead;
        return experience.isActiveAndEnabled && experience.HasWaitingExperience;
    }
    void MoveItem(ChestRecord record,Vector2 playerPosition,Vector2 playerStart)
    {
        float dt=Time.deltaTime;
        float speedLimit=Mathf.Max(itemMaxSpeed,dt>0?Vector2.Distance(playerPosition,previousPosition)/dt+6f:itemMaxSpeed);
        record.itemSpeed=Mathf.MoveTowards(record.itemSpeed,speedLimit,itemAcceleration*dt);
        Vector2 old=record.itemPosition;
        record.itemPosition=Vector2.MoveTowards(old,playerPosition,record.itemSpeed*dt);
        if(ExperienceDropManager.SegmentDistanceSquared(Vector2.zero,old-playerStart,record.itemPosition-playerPosition)>pickupRadius*pickupRadius)return;
        // Another item or natural recharge can make this reward unusable during flight.
        // Preserve it at its original chest instead of consuming it or carrying it into another chunk.
        if(CanUse(record.reward))TryClaim(record);
        record.attracting=false;
    }
    bool WithinReach(Vector2 point,Vector2 a,Vector2 b,float radius)
    {
        Vector2 d=b-a;
        Vector2 nearest=a+d*(d.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude):0);
        return (point-nearest).sqrMagnitude<=radius*radius && navigation.CanTravel(nearest,point);
    }
    bool TryClaim(ChestRecord record)
    {
        if(record.claimed)return false;
        string message;
        if(record.reward==Reward.Heal)
        { PotionCount++; message="물약 +1"; }
        else {if(!experience.TryCollectAllWaiting())return false;message="경험치 회수";}
        record.claimed=true;record.claimedAt=Time.time;feedback=message;feedbackUntil=Time.time+1.2f;
        if(pickupSound!=null)audioSource.PlayOneShot(pickupSound,soundVolume);
        return true;
    }
    public int PotionCount { get; private set; }

    public bool TryUsePotion()
    {
        if (PotionCount == 0 || Time.timeScale <= 0f || (GameSession.Instance != null && GameSession.Instance.IsGameOver))
            return false;
        if (health.TryHeal(healAmount) == 0) return false;
        PotionCount--;
        return true;
    }

    void Update()
    {
        if (KeyBindings.Down(GameAction.Potion)) TryUsePotion();
    }

    void LateUpdate()
    {
        if(health==null)return;
        Vector2 position=health.transform.position;
        if(Time.timeScale<=0 || health.IsDead || (GameSession.Instance!=null && GameSession.Instance.IsGameOver))
        {previousPosition=position;return;}
        RefreshChests(position);
        foreach(var record in active)
        {
            if(!record.opened && WithinReach(record.position,previousPosition,position,openRadius))
            {
                record.opened=true;record.openedAt=Time.time;
                if(openSound!=null)audioSource.PlayOneShot(openSound,soundVolume);
            }
            if(record.opened && !record.claimed && Time.time-record.openedAt>=Mathf.Max(openingDuration,pickupDelay))
            {
                bool wasAttracting = record.attracting;
                record.attracting = CanUse(record.reward);
                if(record.attracting)
                {
                    if(!wasAttracting)
                    {
                        record.itemPosition = record.view.transform.TransformPoint(Vector3.up * ChestView.RewardHeight);
                        record.itemSpeed = itemInitialSpeed;
                    }
                    MoveItem(record, position, wasAttracting ? previousPosition : position);
                }
            }
            UpdateView(record);
        }
        previousPosition=position;
    }
    void OnDestroy() { if(runtimeFont!=null) Destroy(runtimeFont); }
    void OnGUI()
    {
        if(string.IsNullOrEmpty(feedback) || Time.time>=feedbackUntil || health==null || health.IsDead)return;
        if(feedbackStyle==null)feedbackStyle=new GUIStyle(GUI.skin.box){alignment=TextAnchor.MiddleCenter,fontSize=22,font=feedbackFont};
        GUI.Box(new Rect(Screen.width*.5f-110,Screen.height*.25f,220,42),feedback,feedbackStyle);
    }
}

