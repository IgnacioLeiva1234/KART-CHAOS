using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TODO EN UNO para cada kart: vida (7 impactos), barra de vida pixel-art
/// sobre el kart, y destrucción al llegar a 0.
///
/// La barra NO se ve normalmente: aparece sobre el kart cuando le pegan,
/// pierde un segmento por impacto y se desvanece unos segundos después.
///
/// SETUP: seleccioná la RAÍZ de cada kart (el tuyo y los enemigos) y
/// Add Component > "Destructible Target". Nada más. No hace falta crear
/// Canvas ni imágenes: la barra se dibuja sola por código.
/// El kart necesita al menos un Collider (en él o en un hijo).
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
    [Tooltip("Altura extra sobre el punto más alto del kart.")]
    [SerializeField] private float extraHeight = 0.6f;
    [SerializeField] private float barWorldWidth = 2.4f;
    [SerializeField] private float hideAfterSeconds = 3f;
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private Color aliveColor = new Color(0.93f, 0.27f, 0.25f);
    [SerializeField] private Color lostColor = new Color(1f, 0.84f, 0.74f);

    public event Action<int, int> OnHealthChanged;
    public int CurrentHealth => currentHits;
    public int MaxHealth => hitsToDestroy;

    /// <summary>Todos los karts en escena (lo usa RespawnPoint para elegir un punto lejos de los demás).</summary>
    public static readonly List<DestructibleTarget> All = new List<DestructibleTarget>();
    public bool IsAlive => currentHits > 0;
    public bool IsProtected => Time.time < protectedUntil;
    public Transform KartTransform => victim != null ? victim.transform : transform;

    // Medidas en "píxeles de textura" (pixel-art)
    private const int HeartW = 13, HeartH = 12;
    private const int SegW = 12, SegH = 9, Slant = 3;
    private const int Step = SegW - Slant + 1;
    private const int Gap = 2;
    private const float Px = 4f;

    private GameObject victim;          // lo que se destruye al llegar a 0
    private Renderer[] bodyRenderers;
    private Transform barRoot;
    private CanvasGroup group;
    private Image[] segments;
    private float visibleTimer;
    private Camera cam;
    private float protectedUntil;       // inmunidad tras reaparecer
    private Vector3 spawnPos;           // donde empezó el kart (respaldo si no hay RespawnPoints)
    private Quaternion spawnRot;

    private void Awake()
    {
        currentHits = hitsToDestroy;

        // Si hay un Rigidbody arriba en la jerarquía, ese es "el kart".
        var rb = GetComponentInParent<Rigidbody>();
        victim = rb != null ? rb.gameObject : gameObject;

        spawnPos = victim.transform.position;
        spawnRot = victim.transform.rotation;

        bodyRenderers = Array.FindAll(victim.GetComponentsInChildren<Renderer>(),
            r => r is MeshRenderer || r is SkinnedMeshRenderer);

        if (victim.GetComponentInChildren<Collider>() == null)
        {
            Debug.LogWarning("[" + victim.name + "] no tiene ningún Collider: los proyectiles no lo van a poder golpear.");
        }

        if (showHealthBar) BuildBar(hitsToDestroy);
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void OnDestroy()
    {
        if (barRoot != null) Destroy(barRoot.gameObject);
    }

    /// <summary>Lo llama el Projectile cuando impacta.</summary>
    public void TakeHit()
    {
        if (currentHits <= 0 || IsProtected) return;

        currentHits--;
        Debug.Log(victim.name + " recibió un impacto. Vida: " + currentHits + "/" + hitsToDestroy);

        Refresh(currentHits);
        visibleTimer = hideAfterSeconds;
        OnHealthChanged?.Invoke(currentHits, hitsToDestroy);

        if (currentHits <= 0) Die();
    }

    /// <summary>Muerte instantánea, sin importar la vida que le quede (minas, etc.).</summary>
    public void Kill()
    {
        if (currentHits <= 0 || IsProtected) return;

        currentHits = 0;
        Debug.Log(victim.name + " fue eliminado instantáneamente.");

        Refresh(0);
        visibleTimer = hideAfterSeconds;
        OnHealthChanged?.Invoke(0, hitsToDestroy);
        Die();
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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
            if (m != this && m.enabled) { m.enabled = false; disabledScripts.Add(m); }
        }

        // 2) Esperar.
        yield return new WaitForSeconds(respawnDelay);

        // 3) Elegir dónde reaparecer: el RespawnPoint más lejos de los enemigos,
        //    o el lugar de inicio si no hay ninguno en la escena.
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
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

    private void Refresh(int current)
    {
        if (segments == null) return;
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i].color = i < current ? aliveColor : lostColor;
        }
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        visibleTimer -= Time.deltaTime;
        float targetAlpha = visibleTimer > 0f ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        if (group.alpha <= 0.001f) return; // oculta: no gastamos nada

        // Posición: arriba del punto más alto del kart.
        Vector3 pos = victim.transform.position;
        bool has = false;
        Bounds b = default;
        foreach (var r in bodyRenderers)
        {
            if (r == null) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (has) { pos = b.center; pos.y = b.max.y; }
        pos.y += extraHeight;
        barRoot.position = pos;

        // Mirar siempre a la cámara.
        if (cam == null) cam = Camera.main;
        if (cam != null) barRoot.rotation = cam.transform.rotation;
    }

    // ---------------------------------------------------------------
    // Construcción de la barra (todo por código)
    // ---------------------------------------------------------------
    private void BuildBar(int count)
    {
        var go = new GameObject("HealthBar_" + victim.name, typeof(RectTransform));
        barRoot = go.transform;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        group = go.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f; // arranca oculta

        int totalW = HeartW + Gap + (count - 1) * Step + SegW;
        var rt = (RectTransform)barRoot;
        rt.sizeDelta = new Vector2(totalW * Px, HeartH * Px);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float scale = barWorldWidth / (totalW * Px);
        rt.localScale = new Vector3(scale, scale, scale);

        var heart = CreateImage("Heart", rt, CreateHeartSprite());
        SetRect(heart.rectTransform, 0f, HeartW, HeartH);

        var segSprite = CreateSegmentSprite();
        segments = new Image[count];
        for (int i = 0; i < count; i++)
        {
            segments[i] = CreateImage("Segment" + i, rt, segSprite);
            SetRect(segments[i].rectTransform, HeartW + Gap + i * Step, SegW, SegH);
        }
        Refresh(currentHits);
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static void SetRect(RectTransform r, float xPixels, int wPixels, int hPixels)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(xPixels * Px, 0f);
        r.sizeDelta = new Vector2(wPixels * Px, hPixels * Px);
    }

    private static Sprite CreateHeartSprite()
    {
        string[] pattern =
        {
            "..XX...XX..",
            ".XXXX.XXXX.",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXXXXXXXX.",
            "..XXXXXXX..",
            "...XXXXX...",
            "....XXX....",
            ".....X....."
        };

        var tex = NewTexture(HeartW, HeartH);
        bool[,] filled = new bool[HeartW, HeartH];
        for (int row = 0; row < pattern.Length; row++)
        {
            for (int col = 0; col < pattern[row].Length; col++)
            {
                if (pattern[row][col] != 'X') continue;
                filled[col + 1, HeartH - 1 - (row + 1)] = true;
            }
        }

        Color fill = new Color(0.93f, 0.27f, 0.25f);
        Color outline = new Color(0.38f, 0.08f, 0.10f);
        Color shine = new Color(1f, 0.72f, 0.66f);

        for (int x = 0; x < HeartW; x++)
        {
            for (int y = 0; y < HeartH; y++)
            {
                if (filled[x, y]) { tex.SetPixel(x, y, fill); continue; }
                bool near =
                    (x > 0 && filled[x - 1, y]) || (x < HeartW - 1 && filled[x + 1, y]) ||
                    (y > 0 && filled[x, y - 1]) || (y < HeartH - 1 && filled[x, y + 1]);
                if (near) tex.SetPixel(x, y, outline);
            }
        }
        tex.SetPixel(3, HeartH - 3, shine);
        tex.SetPixel(2, HeartH - 4, shine);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, HeartW, HeartH), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateSegmentSprite()
    {
        var tex = NewTexture(SegW, SegH);
        Color outline = new Color(0.38f, 0.08f, 0.10f);

        for (int y = 0; y < SegH; y++)
        {
            int shift = Mathf.RoundToInt(y * (Slant / (float)(SegH - 1)));
            int xMin = shift;
            int xMax = shift + (SegW - Slant) - 1;
            for (int x = xMin; x <= xMax; x++)
            {
                bool edge = x == xMin || x == xMax || y == 0 || y == SegH - 1;
                tex.SetPixel(x, y, edge ? outline : Color.white);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, SegW, SegH), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(new Color[w * h]); // transparente
        return tex;
    }
}