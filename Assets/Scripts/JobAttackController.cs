using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerProgression))]
public class JobAttackController : MonoBehaviour
{
    [Serializable]
    public class JobAttackSet
    {
        public PlayerJob job;
        [Tooltip("이 직업에서 켤 공격/스킬 컴포넌트만 넣으세요.")]
        public MonoBehaviour[] attacks = new MonoBehaviour[0];
        [Tooltip("이 구성에 AutoAttack이 있을 경우 적용할 부채꼴 거리입니다.")]
        [Min(0.1f)] public float coneRange = 1.5f;
    }

    [SerializeField] private JobAttackSet[] jobAttacks = new JobAttackSet[0];
    [Tooltip("보관만 하고 사용하지 않을 공격 컴포넌트입니다.")]
    [SerializeField] private MonoBehaviour[] unusedAttacks = new MonoBehaviour[0];
    private PlayerProgression progression;
    private PlayerHealth health;
    private readonly HashSet<MonoBehaviour> managed = new HashSet<MonoBehaviour>();
    private readonly HashSet<MonoBehaviour> selected = new HashSet<MonoBehaviour>();
    private bool deathHandled;

    private void Awake()
    {
        progression = GetComponent<PlayerProgression>();
        health = GetComponent<PlayerHealth>();
        if (jobAttacks == null || jobAttacks.Length == 0)
            ConfigureDefaults();
        RebuildManaged();
        DisableAll();
    }

    private void OnEnable()
    {
        if (progression == null) progression = GetComponent<PlayerProgression>();
        progression.JobChanged += OnJobChanged;
        ApplyCurrentJob();
    }

    private void Start() { ApplyCurrentJob(); }

    private void Update()
    {
        if (!deathHandled && health != null && health.IsDead)
        {
            deathHandled = true;
            DisableAll();
        }
    }

    [ContextMenu("직업 공격 자동 연결")]
    public void ConfigureDefaults()
    {
        // 비활성 자식도 찾아 연결합니다. GameObject는 사용자가 켜두어야 합니다.
        AutoAttack[] basic = GetComponentsInChildren<AutoAttack>(true);
        AutoProjectileAttack[] arrows = GetComponentsInChildren<AutoProjectileAttack>(true);
        float noviceRange = 1.5f;
        if (jobAttacks != null)
            foreach (JobAttackSet set in jobAttacks)
                if (set != null && set.job == PlayerJob.None) { noviceRange = set.coneRange; break; }
        var archer = new List<MonoBehaviour>();
        archer.AddRange(arrows);
        archer.AddRange(GetComponentsInChildren<ArcherVolleySkill>(true));
        archer.AddRange(GetComponentsInChildren<ArrowRainSkill>(true));
        archer.AddRange(GetComponentsInChildren<CrystalArrowSkill>(true));
        archer.AddRange(GetComponentsInChildren<EvasiveShotSkill>(true));
        jobAttacks = new[]
        {
            new JobAttackSet { job = PlayerJob.None, attacks = basic, coneRange = noviceRange },
            new JobAttackSet { job = PlayerJob.Archer, attacks = archer.ToArray() }
        };
        var unused = new List<MonoBehaviour>();
        unused.AddRange(GetComponentsInChildren<AutoGunAttack>(true));
        unused.AddRange(GetComponentsInChildren<PiercingProjectileSkill>(true));
        unusedAttacks = unused.ToArray();
        if (Application.isPlaying)
        {
            // 기존 관리 대상도 먼저 정지시켜 연결을 제거한 공격이 남지 않게 합니다.
            DisableAll();
            RebuildManaged();
            ApplyCurrentJob();
        }
    }

    private bool IsValid(MonoBehaviour attack)
    {
        return attack != null && attack != this && attack.transform.IsChildOf(transform)
            && !(attack is PlayerProgression) && !(attack is PlayerHealth)
            && !(attack is PlayerMovement) && !(attack is LevelAndJobUI);
    }

    private void RebuildManaged()
    {
        managed.Clear();
        if (jobAttacks != null)
            foreach (JobAttackSet set in jobAttacks)
                if (set != null && set.attacks != null)
                    foreach (MonoBehaviour attack in set.attacks)
                        if (IsValid(attack)) managed.Add(attack);
        if (unusedAttacks != null)
            foreach (MonoBehaviour attack in unusedAttacks)
                if (IsValid(attack)) managed.Add(attack);
    }

    private void OnJobChanged(PlayerJob job) { ApplyCurrentJob(); }

    public void ApplyCurrentJob()
    {
        if (!isActiveAndEnabled || progression == null) return;
        if (health != null && health.IsDead)
        {
            DisableAll();
            return;
        }
        selected.Clear();
        JobAttackSet choice = null;
        if (jobAttacks != null)
            foreach (JobAttackSet set in jobAttacks)
                if (set != null && set.job == progression.CurrentJob)
                {
                    choice = set;
                    break;
                }

        if (choice != null && choice.attacks != null)
            foreach (MonoBehaviour attack in choice.attacks)
                if (IsValid(attack)) selected.Add(attack);
        if (unusedAttacks != null)
            foreach (MonoBehaviour attack in unusedAttacks) selected.Remove(attack);

        // 이전 공격을 먼저 끈 다음 새 공격을 켭니다.
        // The retired single piercing shot must never return through an old attack list.
        selected.RemoveWhere(attack => attack is PiercingProjectileSkill);

        foreach (MonoBehaviour attack in managed)
            if (attack != null && !selected.Contains(attack)) attack.enabled = false;
        foreach (MonoBehaviour attack in selected)
        {
            if (attack is AutoAttack cone) cone.SetAttackRange(choice.coneRange);
            attack.enabled = true;
        }
    }

    private void DisableAll()
    {
        foreach (MonoBehaviour attack in managed)
            if (attack != null) attack.enabled = false;
    }

    private void OnDisable()
    {
        if (progression != null) progression.JobChanged -= OnJobChanged;
        DisableAll();
    }
}
