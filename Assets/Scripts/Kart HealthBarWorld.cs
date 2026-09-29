using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida de un kart.
/// Cada kart tiene su propia barra y su propio DestructibleTarget.
///
/// Estructura:
///
/// Kart
/// ├── DestructibleTarget
/// └── HealthBarCanvas
///     ├── Background
///     └── Fill
///
/// La barra se conecta automáticamente al DestructibleTarget
/// del mismo kart.
/// </summary>
public class KartHealthBarWorld : MonoBehaviour
{
    [Header("Referencias")]

    [Tooltip("Se encuentra automáticamente en el kart padre.")]
    [SerializeField] private DestructibleTarget target;

    [SerializeField] private Image healthFill;

    [SerializeField] private Image healthBackground;


    [Header("Posición")]

    [Tooltip("Altura de la barra sobre el kart.")]
    [SerializeField] private float heightAboveKart = 2f;


    [Header("Animación")]

    [SerializeField] private float healthAnimationSpeed = 5f;


    [Header("Mostrar barra")]

    [SerializeField] private bool onlyShowWhenDamaged = true;

    [SerializeField] private float hideDelayAfterHit = 3f;

    [SerializeField] private float fadeSpeed = 6f;


    private CanvasGroup canvasGroup;

    private Camera cam;

    private float targetHealth01;

    private float displayedHealth01;

    private float hideTimer;


    private void Awake()
    {
        // =====================================================
        // CANVAS GROUP
        // =====================================================

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }


        // =====================================================
        // CÁMARA
        // =====================================================

        cam = Camera.main;


        // =====================================================
        // BUSCAR AUTOMÁTICAMENTE EL DESTRUCTIBLE TARGET
        // =====================================================

        if (target == null)
        {
            target = GetComponentInParent<DestructibleTarget>();
        }


        // =====================================================
        // CONFIGURAR LA IMAGEN ROJA
        // =====================================================

        if (healthFill != null)
        {
            healthFill.type = Image.Type.Filled;

            healthFill.fillMethod = Image.FillMethod.Horizontal;

            healthFill.fillOrigin =
                (int)Image.OriginHorizontal.Left;

            healthFill.fillAmount = 1f;
        }


        // =====================================================
        // CONECTAR CON LA VIDA DEL KART
        // =====================================================

        if (target != null)
        {
            target.OnHitsChanged += HandleHitsChanged;

            target.OnDestroyed += HandleDestroyed;


            // Vida inicial
            targetHealth01 =
                (float)target.HitsRemaining /
                Mathf.Max(1, target.MaxHits);


            displayedHealth01 = targetHealth01;


            UpdateHealthBar(displayedHealth01);
        }
        else
        {
            Debug.LogWarning(
                "KartHealthBarWorld no encontró un DestructibleTarget en el kart padre.",
                this
            );
        }
    }


    private void OnDestroy()
    {
        if (target != null)
        {
            target.OnHitsChanged -= HandleHitsChanged;

            target.OnDestroyed -= HandleDestroyed;
        }
    }


    // =========================================================
    // CUANDO EL KART RECIBE DAÑO
    // =========================================================

    private void HandleHitsChanged(int remaining, int max)
    {
        if (max <= 0)
            return;


        // Convertimos la vida a porcentaje.

        targetHealth01 =
            (float)remaining / max;


        // Mostrar la barra.

        if (onlyShowWhenDamaged)
        {
            hideTimer = hideDelayAfterHit;
        }
    }


    // =========================================================
    // ACTUALIZAR LA BARRA
    // =========================================================

    private void UpdateHealthBar(float health01)
    {
        if (healthFill == null)
            return;


        healthFill.fillAmount =
            Mathf.Clamp01(health01);
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }


        if (cam == null)
            return;


        // =====================================================
        // BILLBOARD
        // =====================================================

        // La barra siempre mira hacia la cámara.

        transform.rotation =
            cam.transform.rotation;


        // =====================================================
        // ANIMACIÓN DE VIDA
        // =====================================================

        displayedHealth01 =
            Mathf.MoveTowards(
                displayedHealth01,
                targetHealth01,
                Time.deltaTime * healthAnimationSpeed
            );


        UpdateHealthBar(displayedHealth01);


        // =====================================================
        // MOSTRAR / OCULTAR
        // =====================================================

        if (onlyShowWhenDamaged)
        {
            bool fullHealth =
                target != null &&
                target.HitsRemaining >= target.MaxHits;


            if (hideTimer > 0f)
            {
                hideTimer -= Time.deltaTime;
            }


            float targetAlpha =
                (!fullHealth && hideTimer > 0f)
                ? 1f
                : 0f;


            canvasGroup.alpha =
                Mathf.MoveTowards(
                    canvasGroup.alpha,
                    targetAlpha,
                    Time.deltaTime * fadeSpeed
                );
        }
    }


    // =========================================================
    // KART DESTRUIDO
    // =========================================================

    private void HandleDestroyed()
    {
        canvasGroup.alpha = 0f;
    }
}