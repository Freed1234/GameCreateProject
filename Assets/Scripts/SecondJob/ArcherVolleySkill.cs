using System.Collections;
using UnityEngine;

// [2차 Z] 부채꼴 연사 (임시, 메르세데스 듀얼샷 + 애쉬 W 느낌)
// 조금 큰 관통 화살을 한 발씩 따다다닥 쏘면서, 부채꼴로 쓸어가며 퍼지게 발사.
// 방향은 발동 순간 가장 가까운 적 (없으면 마지막 이동 방향).
[DisallowMultipleComponent]
public class ArcherVolleySkill : ArcherSkillBase, ISecondJobSkill
{
    [Header("부채꼴 연사 (한 발씩)")]
    [SerializeField, Range(1, 30)] private int arrowCount = 12;
    [Tooltip("부채꼴 전체 각도")]
    [SerializeField, Range(0f, 120f)] private float sweepAngle = 50f;
    [Tooltip("화살 사이 간격(초). 작을수록 빠르게 따다다닥")]
    [SerializeField, Min(0.01f)] private float interval = 0.035f;
    [Tooltip("한 번 쓸고 끝(false) / 갔다가 돌아오기(true)")]
    [SerializeField] private bool sweepBack = false;
    [SerializeField, Min(1)] private int damage = 6;
    [SerializeField, Min(1f)] private float speed = 18f;
    [SerializeField, Min(1f)] private float range = 12f;
    [Tooltip("화살 크기 배율 (1 = 기본)")]
    [SerializeField, Min(0.5f)] private float arrowScale = 1.5f;

    private void Reset()
    {
        key = KeyCode.Z;
        cooldown = 6f;
    }

    protected override bool Cast()
    {
        StartCoroutine(Sweep(AimDirection()));
        return true;
    }

    // 왼쪽 끝 → 오른쪽 끝으로 한 발씩 (매 발 플레이어 현재 위치에서)
    private IEnumerator Sweep(Vector2 aim)
    {
        int total = sweepBack ? arrowCount * 2 - 1 : arrowCount;
        for (int i = 0; i < total; i++)
        {
            int step = i < arrowCount ? i : (arrowCount - 1) - (i - arrowCount + 1);
            float t = arrowCount == 1 ? 0.5f : step / (arrowCount - 1f);
            float angle = Mathf.Lerp(-sweepAngle * 0.5f, sweepAngle * 0.5f, t);

            SkillProjectile.Spawn(arrowSprite, arrowColor, Origin, SkillUtil.Rotate(aim, angle),
                speed, range, SkillModifiers.Damage(damage), 0.28f, true, false, Enemies, arrowScale);
            yield return new WaitForSeconds(interval);
        }
    }
}
