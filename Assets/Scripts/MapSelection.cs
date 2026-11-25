using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>El anfitrión elige el mapa y PUN lleva a toda la sala a esa escena.</summary>
public class MapSelection : MonoBehaviourPunCallbacks
{
    public const string MapProperty = "map";
    private Button dustyButton;
    private Button roadButton;
    private Text statusText;
    private bool loading;

    public static bool IsPlayableMap(string sceneName)
    {
        return sceneName == "DustyTerrain" || sceneName == "LargeRoad";
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        BuildUI();
        RefreshStatus();
        LoadSelectedMapIfMaster();
    }

    public void SelectDustyTerrain() { SelectMap("DustyTerrain"); }
    public void SelectLargeRoad() { SelectMap("LargeRoad"); }

    public override void OnJoinedRoom()
    {
        RefreshStatus();
        LoadSelectedMapIfMaster();
    }

    private void SelectMap(string sceneName)
    {
        if (loading || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !IsPlayableMap(sceneName)) return;
        // Guardar la elección permite recuperarla incluso si cambia el anfitrión.
        loading = true;
        bool saved = PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { MapProperty, sceneName },
            { CarSelectionState.PhaseProperty, CarSelectionState.CarPhase }
        });
        if (!saved)
        {
            loading = false;
            Debug.LogWarning("No se pudo confirmar el mapa. Intentá nuevamente cuando vuelva la conexión.");
            RefreshStatus();
            return;
        }
        RefreshStatus();
        PhotonNetwork.LoadLevel(CarSelectionState.SelectionScene);
    }

    private void LoadSelectedMapIfMaster()
    {
        if (loading || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        string sceneName = PhotonNetwork.CurrentRoom.CustomProperties[MapProperty] as string;
        if (IsPlayableMap(sceneName))
        {
            loading = true;
            RefreshStatus();
            PhotonNetwork.LoadLevel(CarSelectionState.SceneForRoom(PhotonNetwork.CurrentRoom.CustomProperties));
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshStatus();
        LoadSelectedMapIfMaster();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(MapProperty) || propertiesThatChanged.ContainsKey(CarSelectionState.PhaseProperty))
        {
            RefreshStatus();
            LoadSelectedMapIfMaster();
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        loading = false;
        RefreshStatus();
        ConnectionFeedback.Show(this, "Se perdió la conexión con Photon. Podés volver a conectarte.",
            () => SceneManager.LoadScene("LoadingScene"));
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(CarSelectionState.ResetProperties());
        SceneManager.LoadScene("Lobby");
    }

    private void RefreshStatus()
    {
        if (statusText == null) return;
        bool canSelect = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && !loading;
        dustyButton.interactable = canSelect;
        roadButton.interactable = canSelect;
        if (!PhotonNetwork.InRoom)
            statusText.text = "Entrá a una sala desde el Lobby para elegir el mapa.";
        else if (loading)
            statusText.text = "Preparando la selección de autos…";
        else if (PhotonNetwork.IsMasterClient)
            statusText.text = "Sos el anfitrión. Elegí dónde jugará toda la sala.";
        else
            statusText.text = "Esperando a que el anfitrión elija el mapa…";
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("Map Selection UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.11f);

        Text title = MakeText("Title", background.transform, "ELEGÍ EL MAPA", 44, new Vector2(0f, 180f), new Vector2(1000f, 70f));
        title.fontStyle = FontStyle.Bold;
        statusText = MakeText("Status", background.transform, string.Empty, 22, new Vector2(0f, 105f), new Vector2(1050f, 70f));
        statusText.color = new Color(0.75f, 0.81f, 0.88f);
        dustyButton = MakeButton(background.transform, "Dusty Terrain", new Vector2(-245f, -45f), new Color(0.57f, 0.35f, 0.16f));
        roadButton = MakeButton(background.transform, "Large road", new Vector2(245f, -45f), new Color(0.14f, 0.36f, 0.48f));
        dustyButton.onClick.AddListener(SelectDustyTerrain);
        roadButton.onClick.AddListener(SelectLargeRoad);
        MakeText("Hint", background.transform, "Todos los jugadores entrarán al mismo mapa.", 20, new Vector2(0f, -200f), new Vector2(1000f, 50f));

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 position, Color color)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(440f, 190f);
        rect.anchoredPosition = position;
        buttonObject.GetComponent<Image>().color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        Text text = MakeText("Label", buttonObject.transform, label, 34, Vector2.zero, new Vector2(420f, 100f));
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    private static Text MakeText(string name, Transform parent, string text, int size, Vector2 position, Vector2 dimensions)
    {
        GameObject label = new GameObject(name, typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        Text component = label.GetComponent<Text>();
        component.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        component.fontSize = size;
        component.alignment = TextAnchor.MiddleCenter;
        component.color = Color.white;
        component.text = text;
        component.raycastTarget = false;
        return component;
    }
}
