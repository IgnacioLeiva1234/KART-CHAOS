using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

/// <summary>
/// Barra de carga del derrape (mini-turbo). Lee kart.DriftCharge01 cada
/// frame y actualiza una Image de tipo "Filled" en el Canvas.
///
/// SETUP EN EL EDITOR:
/// 1. En el Canvas de tu HUD: click derecho > UI > Image. Renombrala
///    "DriftBarBackground". Dale un color oscuro semi-transparente
///    (fondo de la barra).
/// 2. Click derecho sobre "DriftBarBackground" > UI > Image, para
///    crearla como hija. Renombrala "DriftBarFill".
///    - En el Inspector de "DriftBarFill": Image Type = Filled,
///      Fill Method = Horizontal (o Radial 360 si la querés circular),
///      Fill Origin = Left.
/// 3. Creá un GameObject vacío como hijo del Canvas (o usá
///    "DriftBarBackground" mismo) y agregale este script
///    "DriftChargeUI.cs".
/// 4. En el Inspector del script, arrastrá:
///    - Kart: el GameObject "Kart" (el que tiene KartController).
///    - Fill Image: arrastrá "DriftBarFill".
///    - Root (opcional): arrastrá "DriftBarBackground" si querés que
///      toda la barra se oculte cuando no estás derrapando.
/// </summary>
public class DriftChargeUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KartController kart;
    [SerializeField] private Image fillImage;
    [SerializeField] private GameObject root; // opcional: se oculta cuando no hay drift activo

    [Header("Colores por tier (deben coincidir con los del KartController)")]
    [SerializeField] private Color noTierColor = new Color(0.6f, 0.6f, 0.6f);   // gris, antes del tier 1
    [SerializeField] private Color tier1Color = new Color(0.25f, 0.65f, 0.88f); // celeste
    [SerializeField] private Color tier2Color = new Color(0.88f, 0.28f, 0.25f); // rojo
    [SerializeField] private Color tier3Color = new Color(0.72f, 0.24f, 0.88f); // violeta

    [Header("Comportamiento")]
    [SerializeField] private bool hideWhenNotDrifting = true;
    [SerializeField] private float pulseScaleAtMaxCharge = 1.15f; // "late" visual al llegar al tier 3
    [SerializeField] private float pulseSpeed = 8f;

    private RectTransform fillRect;
    private float pulseT;
    private CanvasGroup visibility;
    private float nextKartSearch;

    private void Awake()
    {
        PrepareVisuals();
    }

    private void PrepareVisuals()
    {
        if (fillImage != null) fillRect = fillImage.rectTransform;
        if (root != null)
        {
            visibility = root.GetComponent<CanvasGroup>();
            if (visibility == null) visibility = root.AddComponent<CanvasGroup>();
            visibility.blocksRaycasts = false;
            visibility.interactable = false;
            visibility.alpha = kart != null && (!hideWhenNotDrifting || kart.IsDrifting) ? 1f : 0f;
        }
    }

    /// <summary>Activa solamente el HUD de la escena del kart que controla este equipo.</summary>
    public static void BindSceneToLocalKart(KartController localKart)
    {
        if (!IsLocalKart(localKart)) return;
        var scene = localKart.gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded) return;
        if (PhotonNetwork.InRoom)
        {
            string expectedMap = PhotonNetwork.CurrentRoom.CustomProperties[MapSelection.MapProperty] as string;
            if (MapSelection.IsPlayableMap(expectedMap) && scene.name != expectedMap) return;
        }
        foreach (DriftChargeUI hud in FindObjectsByType<DriftChargeUI>(FindObjectsInactive.Include))
        {
            if (hud.gameObject.scene != scene) continue;
            KartController parentKart = hud.GetComponentInParent<KartController>(true);
            if (parentKart != null && parentKart != localKart) continue;
            hud.BindToLocalKart(localKart);
        }
    }

    public void BindToLocalKart(KartController localKart)
    {
        if (!IsLocalKart(localKart) || gameObject.scene != localKart.gameObject.scene) return;
        KartController parentKart = GetComponentInParent<KartController>(true);
        if (parentKart != null && parentKart != localKart) return;
        if (kart == localKart && isActiveAndEnabled && fillImage != null && fillImage.isActiveAndEnabled) return;

        kart = localKart;
        // Puede llamarse antes de Awake: el objeto estaba desactivado en la escena.
        PrepareVisuals();
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.enabled = true;
            fillImage.raycastTarget = false;
            fillImage.fillAmount = kart.DriftCharge01;
            fillImage.color = GetColorForCharge(kart.DriftCharge01);
            ActivateUIBranch(fillImage.transform);
        }
        if (root != null) ActivateUIBranch(root.transform);
        ActivateUIBranch(transform);
        enabled = true;
    }

    private static void ActivateUIBranch(Transform leaf)
    {
        Canvas canvas = leaf.GetComponentInParent<Canvas>(true);
        if (canvas == null) { leaf.gameObject.SetActive(true); return; }
        // No activa hermanos, otros karts ni ancestros externos al Canvas.
        for (Transform node = leaf; node != null; node = node.parent)
        {
            node.gameObject.SetActive(true);
            if (node == canvas.transform) break;
        }
        canvas.enabled = true;
    }

    private void Update()
    {
        if (!IsLocalKart(kart))
        {
            kart = null;
            if (Time.unscaledTime >= nextKartSearch)
            {
                nextKartSearch = Time.unscaledTime + .25f;
                foreach (KartController candidate in FindObjectsByType<KartController>())
                    if (IsLocalKart(candidate)) { kart = candidate; break; }
            }
        }
        if (kart == null || fillImage == null)
        {
            if (visibility != null) visibility.alpha = 0f;
            return;
        }

        float charge = kart.DriftCharge01;
        bool drifting = kart.IsDrifting;

        // mostrar/ocultar toda la barra según si se está derrapando
        if (visibility != null)
        {
            // El script vive en root en las escenas existentes. Desactivarlo
            // impediría ejecutar Update y volver a mostrar la barra.
            visibility.alpha = !hideWhenNotDrifting || drifting ? 1f : 0f;
        }

        fillImage.fillAmount = charge;
        fillImage.color = GetColorForCharge(charge);

        // pequeño "pulso" de escala cuando llega al tier 3, para que se sienta el momento
        if (fillRect != null)
        {
            if (charge >= kart.Tier3Threshold && drifting)
            {
                pulseT += Time.deltaTime * pulseSpeed;
                float pulse = 1f + Mathf.Abs(Mathf.Sin(pulseT)) * (pulseScaleAtMaxCharge - 1f);
                fillRect.localScale = new Vector3(pulse, pulse, 1f);
            }
            else
            {
                pulseT = 0f;
                fillRect.localScale = Vector3.one;
            }
        }
    }

    private static bool IsLocalKart(KartController candidate)
    {
        if (candidate == null || !candidate.isActiveAndEnabled) return false;
        if (!PhotonNetwork.InRoom) return true;
        PhotonView view = candidate.GetComponent<PhotonView>();
        return view != null && view.InstantiationId > 0 && !view.IsRoomView &&
               view.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber;
    }

    private Color GetColorForCharge(float charge)
    {
        if (charge >= kart.Tier3Threshold) return tier3Color;
        if (charge >= kart.Tier2Threshold) return tier2Color;
        if (charge >= kart.Tier1Threshold) return tier1Color;
        return noTierColor;
    }
}
