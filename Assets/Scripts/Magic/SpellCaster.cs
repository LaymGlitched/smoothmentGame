using System;
using FPMovement;
using GameCode.Magic;
using GameCode.PlayerScripts;
using GameCode.Shared;
using Nanodogs.API.Nanoshake;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameCode.Magic
{
    public class SpellCaster : MonoBehaviour
    {
        [Header("References")]
        public Transform CastOrigin;
        public LayerMask HitMask = -1;

        [Header("FPMovement Integration")]
        [SerializeField]
        private MouseLookController mouseLook;

        [SerializeField]
        private RigidbodyFPController controller;

        [SerializeField]
        private FPAnimationController animationController;

        [SerializeField]
        private Camera playerCamera;

        [SerializeField]
        private Transform cameraTransform; // Direct reference to camera transform

        [Header("Spells")]
        public Spell[] AvailableSpells;
        private int currentSpellIndex = 0;

        [Header("Input Actions")]
        public InputActionReference CastAction;
        public InputActionReference ChargeAction;

        [Header("Spell Switching")]
        public InputActionReference NextSpellAction;
        public InputActionReference PreviousSpellAction;

        [Header("Mana System")]
        [SerializeField]
        private Mana manaSystem;

        [Header("Debug")]
        public bool ShowDebugInfo = true;

        private Spell currentSpell;
        private float currentCooldown;
        private float chargeTime = 0f;
        private bool isCharging = false;
        
        private string activeOverrideName = "";
        private float overrideDisplayTimer = 0f;

        private SpellContext pendingContext;
        private bool hasPendingSpell = false;

        private void Awake()
        {
            // Auto-find references
            if (mouseLook == null)
                mouseLook = GetComponentInChildren<MouseLookController>();

            if (controller == null)
                controller = GetComponent<RigidbodyFPController>();

            if (animationController == null)
                animationController = GetComponent<FPAnimationController>();

            if (playerCamera == null)
                playerCamera = Camera.main;

            // CRITICAL: Get the actual camera transform
            if (cameraTransform == null && playerCamera != null)
                cameraTransform = playerCamera.transform;
            else if (cameraTransform == null && mouseLook != null)
                cameraTransform = mouseLook.transform;
            else if (cameraTransform == null)
                cameraTransform = transform; // Fallback

            if (manaSystem == null)
                manaSystem = GetComponent<Mana>();
        }

        private void Start()
        {
            // Equip first spell if available
            if (AvailableSpells.Length > 0)
            {
                EquipSpell(AvailableSpells[0]);
            }
        }

        private void OnEnable()
        {
            // Enable all actions manually
            if (CastAction != null)
                CastAction.action.Enable();

            if (ChargeAction != null)
                ChargeAction.action.Enable();

            if (NextSpellAction != null)
                NextSpellAction.action.Enable();

            if (PreviousSpellAction != null)
                PreviousSpellAction.action.Enable();

            // Subscribe to input events
            if (CastAction != null)
                CastAction.action.performed += OnCastPerformed;

            if (ChargeAction != null)
            {
                ChargeAction.action.started += OnChargeStarted;
                ChargeAction.action.canceled += OnChargeCanceled;
            }

            if (NextSpellAction != null)
                NextSpellAction.action.performed += ctx => CycleSpell(1);

            if (PreviousSpellAction != null)
                PreviousSpellAction.action.performed += ctx => CycleSpell(-1);

            if (animationController != null)
                animationController.SpellCasted += OnAnimationSpellCasted;
        }

        private void OnDisable()
        {
            // Disable all actions manually
            if (CastAction != null)
                CastAction.action.Disable();

            if (ChargeAction != null)
                ChargeAction.action.Disable();

            if (NextSpellAction != null)
                NextSpellAction.action.Disable();

            if (PreviousSpellAction != null)
                PreviousSpellAction.action.Disable();

            // Unsubscribe from input events
            if (CastAction != null)
                CastAction.action.performed -= OnCastPerformed;

            if (ChargeAction != null)
            {
                ChargeAction.action.started -= OnChargeStarted;
                ChargeAction.action.canceled -= OnChargeCanceled;
            }

            if (NextSpellAction != null)
                NextSpellAction.action.performed -= ctx => CycleSpell(1);

            if (PreviousSpellAction != null)
                PreviousSpellAction.action.performed -= ctx => CycleSpell(-1);

            if (animationController != null)
                animationController.SpellCasted -= OnAnimationSpellCasted;
        }

        private void Update()
        {
            // Update cooldown
            if (currentCooldown > 0)
                currentCooldown -= Time.deltaTime;

            // Update charging
            if (isCharging)
            {
                chargeTime += Time.deltaTime;
                chargeTime = Mathf.Min(chargeTime, 3f);

                if (ShowDebugInfo && chargeTime % 0.5f < Time.deltaTime)
                    Debug.Log($"Charging: {chargeTime:F1}s");
            }
            
            if (overrideDisplayTimer > 0)
            {
                overrideDisplayTimer -= Time.deltaTime;
            }

            // DEBUG: Visualize aim direction in real-time
            if (ShowDebugInfo)
            {
                Vector3 aimDir = GetAimDirection();
                Debug.DrawRay(
                    CastOrigin != null ? CastOrigin.position : transform.position,
                    aimDir * 10f,
                    Color.red
                );
            }
        }

        private void OnCastPerformed(InputAction.CallbackContext context)
        {
            // Don't cast if dead or disabled
            if (controller != null && !controller.enabled)
                return;

            CastSpell();
        }

        private void OnChargeStarted(InputAction.CallbackContext context)
        {
            isCharging = true;
            chargeTime = 0f;
        }

        private void OnChargeCanceled(InputAction.CallbackContext context)
        {
            isCharging = false;
            ReleaseChargedSpell();
        }

        public void EquipSpell(Spell spell)
        {
            currentSpell = spell;
            if (spell != null)
                Debug.Log($"Equipped spell: {spell.Name}");
            else
                Debug.Log("Unequipped spell");
        }

        public void EquipSpellByIndex(int index)
        {
            if (index >= 0 && index < AvailableSpells.Length)
            {
                currentSpellIndex = index;
                EquipSpell(AvailableSpells[index]);
            }
        }

        public void CycleSpell(int direction)
        {
            if (AvailableSpells.Length == 0)
                return;

            currentSpellIndex =
                (currentSpellIndex + direction + AvailableSpells.Length) % AvailableSpells.Length;
            EquipSpell(AvailableSpells[currentSpellIndex]);
        }

        public void CastSpell()
        {
            if (currentSpell == null)
            {
                Debug.LogWarning("No spell equipped!");
                return;
            }

            if (currentCooldown > 0)
            {
                Debug.Log($"Spell on cooldown: {currentCooldown:F1}s");
                return;
            }

            // Check mana
            if (manaSystem != null && currentSpell.Stats.ManaCost > manaSystem.CurrentMana)
            {
                Debug.Log("Not enough mana!");
                return;
            }

            // Check if player is dead
            if (controller != null && !controller.enabled)
                return;

            // Get the direction from cast origin to aim point
            Vector3 aimDirection = GetAimDirection();
            Vector3 targetPoint = GetTargetPoint();

            // DEBUG: Log aim direction
            if (ShowDebugInfo)
            {
                Debug.Log($"Aim Direction: {aimDirection}");
                Debug.Log($"Target Point: {targetPoint}");
            }

            // Create context
            var context = new SpellContext
            {
                Spell = currentSpell,
                Caster = gameObject,
                CastOrigin = CastOrigin ?? transform,
                Direction = aimDirection,
                TargetPoint = targetPoint,
                HitMask = HitMask,
                ChargeAmount = 0f,
                ManaUsed = currentSpell.Stats.ManaCost,
            };

            EvaluateMovementOverrides(context);

            // Deduct mana
            if (manaSystem != null)
                manaSystem.TakeMana(currentSpell.Stats.ManaCost);

            // Start cooldown
            currentCooldown = currentSpell.Stats.Cooldown;

            // Trigger animation or cast immediately
            pendingContext = context;
            hasPendingSpell = true;

            if (animationController != null)
            {
                animationController.CastSpell();
                if (ShowDebugInfo)
                    Debug.Log($"Started Cast Animation for {currentSpell.Name}");
            }
            else
            {
                ExecutePendingSpell();
                if (ShowDebugInfo)
                    Debug.Log($"Cast {currentSpell.Name} immediately");
            }
        }

        public void ReleaseChargedSpell()
        {
            if (currentSpell == null || chargeTime < 0.1f)
            {
                chargeTime = 0f;
                return;
            }

            // Check if player is dead
            if (controller != null && !controller.enabled)
                return;

            // Check mana for charged spell (costs more)
            float chargedManaCost = currentSpell.Stats.ManaCost * (1 + chargeTime / 3f);
            if (manaSystem != null && chargedManaCost > manaSystem.CurrentMana)
            {
                Debug.Log("Not enough mana for charged spell!");
                chargeTime = 0f;
                return;
            }

            // Get the direction from cast origin to aim point
            Vector3 aimDirection = GetAimDirection();
            Vector3 targetPoint = GetTargetPoint();

            // Create context with charge
            var context = new SpellContext
            {
                Spell = currentSpell,
                Caster = gameObject,
                CastOrigin = CastOrigin ?? transform,
                Direction = aimDirection,
                TargetPoint = targetPoint,
                HitMask = HitMask,
                ChargeAmount = chargeTime / 3f, // Normalized 0-1
                ManaUsed = chargedManaCost,
            };

            EvaluateMovementOverrides(context);

            // Deduct mana
            if (manaSystem != null)
                manaSystem.TakeMana(chargedManaCost);

            // Start cooldown (longer for charged spells)
            currentCooldown = currentSpell.Stats.Cooldown * (1 + chargeTime / 3f);

            // Trigger animation or cast immediately
            pendingContext = context;
            hasPendingSpell = true;

            if (animationController != null)
            {
                animationController.CastSpell();
                if (ShowDebugInfo)
                    Debug.Log($"Started Cast Animation for charged spell: {context.ChargeAmount:P0}");
            }
            else
            {
                ExecutePendingSpell();
                if (ShowDebugInfo)
                    Debug.Log($"Cast charged spell immediately: {context.ChargeAmount:P0}");
            }

            chargeTime = 0f;
        }

        private void OnAnimationSpellCasted()
        {
            if (hasPendingSpell)
            {
                ExecutePendingSpell();
            }
        }

        private void ExecutePendingSpell()
        {
            if (!hasPendingSpell || pendingContext == null)
                return;

            // Recompute aim direction and target point at the exact moment of casting
            pendingContext.Direction = GetAimDirection();
            pendingContext.TargetPoint = GetTargetPoint();

            if (pendingContext.ActiveShape != null)
            {
                pendingContext.ActiveShape.Cast(pendingContext);
                
                if (pendingContext.ChargeAmount > 0)
                    Nanoshake.Shake(false, null, 1f, 0.4f, 1f);
                else
                    Nanoshake.Shake(false, null, 0.5f, 0.3f, 0.5f);
            }
            else
            {
                Debug.LogError($"Spell {pendingContext.Spell.Name} has no Shape assigned!");
            }

            hasPendingSpell = false;
            pendingContext = null;
        }

        private void EvaluateMovementOverrides(SpellContext context)
        {
            context.ActiveShape = context.Spell.Shape;
            context.ActiveModifiers.AddRange(context.Spell.Modifiers);

            if (controller != null && context.Spell.MovementOverrides != null)
            {
                foreach (var overrideDef in context.Spell.MovementOverrides)
                {
                    if (overrideDef.IsConditionMet(controller))
                    {
                        if (overrideDef.OverrideShape != null)
                        {
                            context.ActiveShape = overrideDef.OverrideShape;
                        }
                        if (overrideDef.AdditionalModifiers != null)
                        {
                            context.ActiveModifiers.AddRange(overrideDef.AdditionalModifiers);
                        }
                        
                        activeOverrideName = overrideDef.name;
                        overrideDisplayTimer = 2f;
                        
                        if (ShowDebugInfo)
                        {
                            Debug.Log($"Applied Movement Override for {context.Spell.Name}");
                        }
                        break; // Apply only the first matching override
                    }
                }
            }
        }

        private Vector3 GetAimDirection()
        {
            // Get the camera's forward direction
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = FindObjectOfType<Camera>();
            }

            if (cam != null)
            {
                // Return the camera's forward direction (this is where the player is looking)
                return cam.transform.forward;
            }

            // Fallback
            Debug.LogError("No camera found! Spells will fire in world forward direction!");
            return Vector3.forward;
        }

        private Vector3 GetTargetPoint()
        {
            // Try using MouseLookController's camera
            Camera aimCamera = null;

            // Find the camera through MouseLookController
            if (mouseLook != null)
            {
                aimCamera = mouseLook.GetComponentInChildren<Camera>();
                if (aimCamera == null)
                    aimCamera = mouseLook.GetComponent<Camera>();
            }

            // If not found, use playerCamera
            if (aimCamera == null && playerCamera != null)
                aimCamera = playerCamera;

            // If still not found, try to find any camera
            if (aimCamera == null)
                aimCamera = Camera.main;

            if (aimCamera != null)
            {
                // Use screen center for crosshair aiming
                Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 1000f, HitMask))
                {
                    return hit.point;
                }
                return ray.GetPoint(100f);
            }

            // Fallback: use transform.forward (this shouldn't happen)
            Debug.LogWarning("No camera found for aiming! Using transform.forward");
            return transform.position + transform.forward * 100f;
        }

        private float GetCurrentMana()
        {
            return manaSystem != null ? manaSystem.CurrentMana : 100f;
        }

        private void UseMana(float amount)
        {
            if (manaSystem != null)
                manaSystem.TakeMana(amount);
        }

        // Visualize aim in editor
        private void OnDrawGizmos()
        {
            if (!ShowDebugInfo)
                return;

            if (CastOrigin != null)
            {
                Vector3 direction = GetAimDirection();
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(CastOrigin.position, direction * 10f);

                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(CastOrigin.position + direction * 5f, 0.5f);
            }
        }

        void OnGUI()
        {
            Health health = GetComponent<Health>();
            GUI.Label(new Rect(0, 0, 200, 50), $"spell: {currentSpell.Name}");
            GUI.Label(new Rect(0, 50, 200, 50), $"cooldown: {Math.Round(currentCooldown, 2)}s");
            GUI.Label(
                new Rect(0, 100, 200, 50),
                $"mana: {Math.Round(manaSystem.CurrentMana, 2)} / {Math.Round(manaSystem.MaxMana, 2)}"
            );
            GUI.Label(
                new Rect(0, 150, 200, 50),
                $"health: {Math.Round(health.CurrentHealth, 2)} / {Math.Round(health.MaxHealth, 2)}"
            );
            
            if (overrideDisplayTimer > 0)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(0, 200, 300, 50), $"OVERRIDE ACTIVE: {activeOverrideName}");
                GUI.color = Color.white;
            }
        }

        public Spell CurrentSpell => currentSpell;
        public bool IsCharging => isCharging;
        public float ChargeAmount => chargeTime / 3f;
        public float CurrentCooldown => currentCooldown;
    }
}
