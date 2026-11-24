using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vida del kart y reaparición. Usa HealthCanvas/HealthBar del prefab:
/// la barra se vacía suavemente sin crear cuadrados ni otro Canvas.
/// </summary>
public class DestructibleTarget : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int hitsToDestroy = 7;
    [SerializeField] private GameObject destroyVFXPrefab;
    [SerializeField] private int currentHits; // solo lectura, para ver en Play

    [Header("Respawn")]
    [Tooltip("Tildado: al morir el kart reaparece en un RespawnPoint. Destildado: se destruye para siempre.")]
    [SerializeField] private bool respawnOnDeath = true;
    [SerializeField] private float respawnDelay = 3f;
    [Tooltip("Segundos de inmunidad al reaparecer (no recibe impactos ni minas).")]
    [SerializeField] private float spawnProtectionSeconds = 2f;

    [Header("Barra de vida")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private Canvas healthCanvas;
    [SerializeField] private Image healthBar;
    [Min(0.01f)] [SerializeField] private float healthBarSmoothTime = 0.2f;

    public event Action<int, int> OnHealthChanged;
    public int CurrentHealth => currentHits;
    public int MaxHealth => hitsToDestroy;

    /// <summary>Todos los karts en escena (lo usa RespawnPoint para elegir un punto lejos de los demás).</summary>
    public static readonly List<DestructibleTarget> All = new List<DestructibleTarget>();
    public bool IsAlive => currentHits > 0;
    public bool IsProtected => Time.time < protectedUntil;
    public Transform KartTransform => victim != null ? victim.transform : transform;

    private GameObject victim;          // lo que se destruye al llegar a 0
    private float targetFill = 1f;
    private float fillVelocity;
    private float protectedUntil;       // inmunidad tras reaparecer
    private Vector3 spawnPos;           // donde empezó el kart (respaldo si no hay RespawnPoints)
    private Quaternion spawnRot;

    private void Awake()
    {
        hitsToDestroy = Mathf.Max(1, hitsToDestroy);
        currentHits = hitsToDestroy;

        // Si hay un Rigidbody arriba en la jerarquía, ese es "el kart".
        var rb = GetComponentInParent<Rigidbody>();
        victim = rb != null ? rb.gameObject : gameObject;

        spawnPos = victim.transform.position;
        spawnRot = victim.transform.rotation;

        if (victim.GetComponentInChildren<Collider>() == null)
        {
            Debug.LogWarning("[" + victim.name + "] no tiene ningún Collider: los proyectiles no lo van a poder golpear.");
        }

        BindHealthBar();
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    /// <summary>Lo llama el Projectile cuando impacta.</summary>
    public void TakeHit()
    {
        var combat = GetComponent<KartCombat>();
        if (combat != null && !combat.IsLocal) { combat.RequestDamage(1); return; }
        if (currentHits <= 0 || IsProtected) return;

        currentHits--;
        Debug.Log(victim.name + " recibió un impacto. Vida: " + currentHits + "/" + hitsToDestroy);

        Refresh(currentHits);
        OnHealthChanged?.Invoke(currentHits, hitsToDestroy);

        if (currentHits <= 0) Die();
    }

    /// <summary>Muerte instantánea, sin importar la vida que le quede (minas, etc.).</summary>
    public void Kill()
    {
        var combat = GetComponent<KartCombat>();
        if (combat != null && !combat.IsLocal) { combat.RequestDamage(hitsToDestroy); return; }
        if (currentHits <= 0 || IsProtected) return;

        currentHits = 0;
        Debug.Log(victim.name + " fue eliminado instantáneamente.");

        Refresh(0);
        OnHealthChanged?.Invoke(0, hitsToDestroy);
        Die();
    }

    // Las otras computadoras muestran el estado que decidió el dueño; no ejecutan otra reaparición.
    public void ApplyReplicatedHealth(int value)
    {
        int next = Mathf.Clamp(value, 0, hitsToDestroy);
        if (next == currentHits) return;
        currentHits = next;
        Refresh(currentHits);
        OnHealthChanged?.Invoke(currentHits, hitsToDestroy);
    }

    private void Die()
    {
        if (destroyVFXPrefab != null)
        {
            Instantiate(destroyVFXPrefab, victim.transform.position, Quaternion.identity);
        }

        if (respawnOnDeath)
        {
            Debug.Log("[" + victim.name + "] murió. Reaparece en " + respawnDelay +
                      "s (RespawnPoints en la escena: " + RespawnPoint.All.Count + ").");
            StartCoroutine(RespawnRoutine());
        }
        else
        {
            Debug.Log("[" + victim.name + "] murió y se destruye porque 'Respawn On Death' está destildado.");
            Destroy(victim);
        }
    }

    private IEnumerator RespawnRoutine()
    {
        // 1) "Apagar" el kart SIN desactivar el objeto: así su cámara (si es
        //    hija del kart) sigue viva y no te quedás con la pantalla vacía.
        Rigidbody rb = victim.GetComponent<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        // Guardamos solo lo que estaba encendido, para devolverlo igual después.
        var hiddenRenderers = new List<Renderer>();
        foreach (Renderer r in victim.GetComponentsInChildren<Renderer>())
            if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }

        var disabledColliders = new List<Collider>();
        foreach (Collider c in victim.GetComponentsInChildren<Collider>())
            if (c.enabled) { c.enabled = false; disabledColliders.Add(c); }

        var disabledScripts = new List<MonoBehaviour>();
        foreach (MonoBehaviour m in victim.GetComponentsInChildren<MonoBehaviour>())
        {
            // Un componente "Missing Script" (de un script que borraste) llega como null.
            if (m == null) continue;
            if (m is KartCombat || m is KartHealth || m is Photon.Pun.PhotonView || m is Photon.Pun.IPunObservable ||
                (m.GetType().Namespace != null && m.GetType().Namespace.StartsWith("Photon."))) continue;
            if (m != this && m.enabled) { m.enabled = false; disabledScripts.Add(m); }
        }

        // 2) Esperar.
        yield return new WaitForSeconds(respawnDelay);

        // 3) Pedir un punto al selector de reaparición de la escena.
        RespawnPoint point = RespawnPoint.GetBest(this);
        Vector3 pos = point != null ? point.transform.position : spawnPos;
        Quaternion rot = point != null ? point.transform.rotation : spawnRot;

        victim.transform.SetPositionAndRotation(pos, rot);
        if (rb != null)
        {
            rb.position = pos;
            rb.rotation = rot;
        }
        Physics.SyncTransforms();

        // 4) Revivir: vida completa, inmunidad corta y todo encendido de nuevo.
        currentHits = hitsToDestroy;
        Refresh(currentHits);
        protectedUntil = Time.time + spawnProtectionSeconds;

        foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
        foreach (Collider c in disabledColliders) if (c != null) c.enabled = true;
        foreach (MonoBehaviour m in disabledScripts) if (m != null) m.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // Avisarle al script de movimiento que se resetee (velocidad, giro, drift).
        victim.SendMessage("OnRespawned", SendMessageOptions.DontRequireReceiver);

        OnHealthChanged?.Invoke(currentHits, hitsToDestroy);
        Debug.Log(victim.name + " reapareció" + (point != null ? " en " + point.name : " en su lugar de inicio") + ".");
    }

    /// <summary>
    /// Dado un Transform (por ejemplo el de un collider que tocó algo),
    /// devuelve el DestructibleTarget del kart al que pertenece, buscando
    /// hacia arriba y en los hijos cercanos. Devuelve null si no hay.
    /// </summary>
    public static DestructibleTarget Find(Transform from)
    {
        var t = from.GetComponentInParent<DestructibleTarget>();
        if (t != null) return t;

        Transform p = from;
        for (int i = 0; i < 3 && p != null && t == null; i++)
        {
            t = p.GetComponentInChildren<DestructibleTarget>();
            p = p.parent;
        }
        return t;
    }

    /// <summary>Restaura vida sin mantener un segundo contador en KartHealth.</summary>
    public void HealHits(int amount)
    {
        var combat = GetComponent<KartCombat>();
        if (combat != null && !combat.IsLocal) return;
        if (amount <= 0 || !IsAlive) return;
        int next = Mathf.Min(hitsToDestroy, currentHits + amount);
        if (next == currentHits) return;
        currentHits = next;
        Refresh(currentHits);
        OnHealthChanged?.Invoke(currentHits, hitsToDestroy);
    }

    private void BindHealthBar()
    {
        if (healthCanvas == null)
        {
            Transform existingCanvas = victim.transform.Find("HealthCanvas");
            if (existingCanvas != null) healthCanvas = existingCanvas.GetComponent<Canvas>();
        }
        if (healthCanvas == null) return;
        if (healthBar == null)
        {
            Transform existingBar = healthCanvas.transform.Find("HealthBar");
            if (existingBar != null) healthBar = existingBar.GetComponent<Image>();
        }
        if (healthBar == null) return;

        // Conserva los sprites originales; solo cambia cómo se recorta el relleno.
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Horizontal;
        healthBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthBar.fillAmount = 1f;
        healthBar.raycastTarget = false;
        targetFill = 1f;
        fillVelocity = 0f;

        var canvasRect = (RectTransform)healthCanvas.transform;
        if (healthCanvas.renderMode != RenderMode.WorldSpace &&
            (canvasRect.localScale.sqrMagnitude < 0.0001f || canvasRect.sizeDelta.sqrMagnitude < 0.0001f ||
             (healthBar.rectTransform.sizeDelta.x <= 50f && healthBar.rectTransform.localScale.x < 2f)))
        {
            // El prefab original traía escala cero. Reutiliza su mismo Canvas e imágenes.
            canvasRect.localScale = Vector3.one;
            var scaler = healthCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            foreach (Transform child in healthCanvas.transform)
            {
                var rect = child as RectTransform;
                if (rect == null) continue;
                Vector2 originalPosition = rect.anchoredPosition;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -48f) + originalPosition * 4f;
                rect.localScale *= 4f;
                var graphic = child.GetComponent<Graphic>();
                if (graphic != null) graphic.raycastTarget = false;
            }
        }
        healthCanvas.gameObject.SetActive(showHealthBar);
        UpdateCanvasVisibility();
    }

    private void Refresh(int current)
    {
        targetFill = Mathf.Clamp01(current / (float)Mathf.Max(1, hitsToDestroy));
    }

    private void UpdateCanvasVisibility()
    {
        if (healthCanvas == null) return;
        bool isWorldBar = healthCanvas.renderMode == RenderMode.WorldSpace;
        healthCanvas.enabled = showHealthBar &&
            (isWorldBar || PowerUpDamage.IsLocallyControlled(victim));
    }

    private void LateUpdate()
    {
        UpdateCanvasVisibility();
        if (healthBar == null) return;
        healthBar.fillAmount = Mathf.SmoothDamp(healthBar.fillAmount, targetFill,
            ref fillVelocity, Mathf.Max(0.01f, healthBarSmoothTime), Mathf.Infinity, Time.deltaTime);
        if (Mathf.Abs(healthBar.fillAmount - targetFill) < 0.0001f)
            healthBar.fillAmount = targetFill;
    }
}
