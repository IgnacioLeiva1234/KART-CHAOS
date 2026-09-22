using UnityEngine;
using UnityEngine.UI;

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

    private void Awake()
    {
        if (fillImage != null) fillRect = fillImage.rectTransform;
    }

    private void Update()
    {
        if (kart == null || fillImage == null) return;

        float charge = kart.DriftCharge01;
        bool drifting = kart.IsDrifting;

        // mostrar/ocultar toda la barra según si se está derrapando
        if (root != null && hideWhenNotDrifting)
        {
            root.SetActive(drifting);
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

    private Color GetColorForCharge(float charge)
    {
        if (charge >= kart.Tier3Threshold) return tier3Color;
        if (charge >= kart.Tier2Threshold) return tier2Color;
        if (charge >= kart.Tier1Threshold) return tier1Color;
        return noTierColor;
    }
}
