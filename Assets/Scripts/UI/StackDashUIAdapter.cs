using UnityEngine;

// Display the existing stack dash without installing a second Shift movement handler.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class StackDashUIAdapter : MonoBehaviour, ISkillCooldown
{
    private PlayerMovement movement;
    private void Awake() { movement = GetComponent<PlayerMovement>(); }
    public int Charges => movement != null ? movement.DashCharges : 0;
    public int MaxCharges => movement != null ? movement.MaxDashCharges : 3;
    public float Cooldown => movement != null ? movement.DashRechargeDuration : 3f;
    public float CooldownRemaining => movement != null ? movement.DashRechargeRemaining : 0f;
    public string KeyLabel => "LeftShift";
}
