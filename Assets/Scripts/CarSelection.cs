using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Selección personal antes de comenzar; al entrar tarde se muestra solo en ese equipo.</summary>
public class CarSelection : MonoBehaviourPunCallbacks
{
    private Button classicButton;
    private Button nissanButton;
    private Button readyButton;
    private Button leaveButton;
    private Text statusText;
    private Text playersText;
    private Text readyText;
    private string selectedPrefab;
    private string error;
    private bool loading;
    private bool leaving;
    private bool confirmationPending;
    private bool lateJoin;
    private Func<bool> spawnSelected;
    private GameObject ui;
    private static CarSelection localOverlay;

    public string SelectedPrefab => selectedPrefab;
    public bool IsLateJoinSelection => lateJoin;

    public static void ShowForLateJoin(Func<bool> spawn)
    {
        if (localOverlay != null)
        {
            return;
        }
        GameObject host = new GameObject("Local Car Selection");
        localOverlay = host.AddComponent<CarSelection>();
        localOverlay.lateJoin = true;
        localOverlay.spawnSelected = spawn;
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        string saved = PhotonNetwork.LocalPlayer.CustomProperties[CarSelectionState.PrefabProperty] as string;
        selectedPrefab = CarSelectionState.IsAllowedPrefab(saved) ? saved : CarSelectionState.ClassicPrefab;
        BuildUI();
        Refresh();
        AdvanceIfReady();
    }

    public override void OnJoinedRoom()
    {
        Refresh();
        AdvanceIfReady();
    }

    public void SelectClassic() { SelectCar(CarSelectionState.ClassicPrefab); }
    public void SelectNissan() { SelectCar(CarSelectionState.NissanPrefab); }

    public void SelectCar(string prefabName)
    {
        if (loading || leaving || confirmationPending || !PhotonNetwork.InRoom || !CarSelectionState.IsAllowedPrefab(prefabName)) return;
        if (CarSelectionState.IsConfirmed(PhotonNetwork.LocalPlayer.CustomProperties)) return;
        if (!PrefabAvailable(prefabName)) { error = "Este auto no está disponible en esta versión."; Refresh(); return; }
        selectedPrefab = prefabName;
        error = null;
        Refresh();
    }

    public void ConfirmSelection()
    {
        if (loading || leaving || confirmationPending || !PhotonNetwork.InRoom || !CarSelectionState.IsAllowedPrefab(selectedPrefab)) return;
        if (!PrefabAvailable(selectedPrefab)) { error = "Falta el prefab del auto seleccionado."; Refresh(); return; }
        error = null;
        confirmationPending = true;
        bool sent = PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { CarSelectionState.PrefabProperty, selectedPrefab },
            { CarSelectionState.ReadyProperty, true }
        });
        if (!sent) { confirmationPending = false; error = "No se pudo confirmar. Intentá nuevamente."; }
        Refresh();
        AdvanceIfReady();
    }

    public void LeaveRoom()
    {
        if (leaving) return;
        leaving = true;
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            Refresh();
        }
        else SceneManager.LoadScene(PhotonNetwork.IsConnectedAndReady ? "Lobby" : "LoadingScene");
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(CarSelectionState.ResetProperties());
        SceneManager.LoadScene("Lobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        loading = false;
        confirmationPending = false;
        leaving = false;
        error = "Se perdió la conexión. Volvé al inicio para reconectarte.";
        Refresh();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer.IsLocal && (changedProps.ContainsKey(CarSelectionState.ReadyProperty) || changedProps.ContainsKey(CarSelectionState.PrefabProperty)))
        {
            confirmationPending = false;
            string saved = targetPlayer.CustomProperties[CarSelectionState.PrefabProperty] as string;
            if (CarSelectionState.IsAllowedPrefab(saved)) selectedPrefab = saved;
        }
        Refresh();
        AdvanceIfReady();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Refresh();
        AdvanceIfReady();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) { Refresh(); }
    public override void OnPlayerLeftRoom(Player otherPlayer) { Refresh(); AdvanceIfReady(); }
    public override void OnMasterClientSwitched(Player newMasterClient) { Refresh(); AdvanceIfReady(); }

    private void AdvanceIfReady()
    {
        if (loading || leaving || !PhotonNetwork.InRoom) return;
        if (lateJoin)
        {
            if (CarSelectionState.IsConfirmed(PhotonNetwork.LocalPlayer.CustomProperties) && spawnSelected != null && spawnSelected())
                Destroy(gameObject);
            return;
        }
        if (!PhotonNetwork.IsMasterClient) return;
        string map = PhotonNetwork.CurrentRoom.CustomProperties[MapSelection.MapProperty] as string;
        if (!MapSelection.IsPlayableMap(map)) return;
        string phase = PhotonNetwork.CurrentRoom.CustomProperties[CarSelectionState.PhaseProperty] as string;
        // Si el anfitrión salió entre confirmar el comienzo y cargar la escena,
        // el nuevo anfitrión completa esa misma transición.
        if (phase != CarSelectionState.PlayingPhase && !CarSelectionState.AllActivePlayersConfirmed(PhotonNetwork.PlayerList)) return;
        loading = true;
        if (phase != CarSelectionState.PlayingPhase)
        {
            if (!PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { CarSelectionState.PhaseProperty, CarSelectionState.PlayingPhase } }))
            { loading = false; error = "No se pudo iniciar la partida. Esperando conexión…"; Refresh(); return; }
        }
        Refresh();
        PhotonNetwork.LoadLevel(map);
    }

    private static bool PrefabAvailable(string name)
    {
        GameObject prefab = Resources.Load<GameObject>(name);
        return prefab != null && prefab.GetComponent<PhotonView>() != null && prefab.GetComponent<KartController>() != null;
    }

    private void Refresh()
    {
        if (statusText == null) return;
        bool confirmed = PhotonNetwork.InRoom && CarSelectionState.IsConfirmed(PhotonNetwork.LocalPlayer.CustomProperties);
        bool canSelect = PhotonNetwork.InRoom && !confirmed && !loading && !leaving && !confirmationPending;
        classicButton.interactable = canSelect && PrefabAvailable(CarSelectionState.ClassicPrefab);
        nissanButton.interactable = canSelect && PrefabAvailable(CarSelectionState.NissanPrefab);
        readyButton.interactable = canSelect && PrefabAvailable(selectedPrefab);
        leaveButton.interactable = !leaving && !loading;
        classicButton.GetComponent<Image>().color = selectedPrefab == CarSelectionState.ClassicPrefab ? new Color(.72f, .46f, .19f) : new Color(.30f, .23f, .17f);
        nissanButton.GetComponent<Image>().color = selectedPrefab == CarSelectionState.NissanPrefab ? new Color(.19f, .52f, .68f) : new Color(.11f, .24f, .31f);
        readyText.text = confirmed ? "LISTO" : confirmationPending ? "CONFIRMANDO…" : lateJoin ? "ENTRAR CON ESTE AUTO" : "CONFIRMAR AUTO";
        if (!string.IsNullOrEmpty(error)) statusText.text = error;
        else if (!PhotonNetwork.InRoom) statusText.text = "Entrá a una sala desde el Lobby para elegir tu auto.";
        else if (leaving) statusText.text = "Saliendo de la sala…";
        else if (loading) statusText.text = "Todos listos. Cargando el mapa…";
        else if (lateJoin) statusText.text = "La partida ya empezó. Elegí tu auto para entrar.";
        else if (confirmed) statusText.text = "Tu auto está confirmado. Esperando a los demás…";
        else statusText.text = "Cada jugador elige su propio auto. Confirmá cuando estés listo.";

        if (!PhotonNetwork.InRoom) { playersText.text = string.Empty; return; }
        playersText.text = string.Join("   ·   ", PhotonNetwork.PlayerList.Where(p => !p.IsInactive).OrderBy(p => p.ActorNumber).Select(p =>
        {
            string name = string.IsNullOrWhiteSpace(p.NickName) ? "Jugador " + p.ActorNumber : p.NickName;
            string chosen = p.CustomProperties[CarSelectionState.PrefabProperty] as string;
            return name + ": " + (CarSelectionState.IsConfirmed(p.CustomProperties) ? (chosen == CarSelectionState.NissanPrefab ? "Nissan R34 ✓" : "Auto 4.3 ✓") : "eligiendo");
        }));
    }

    private void BuildUI()
    {
        if (Camera.allCamerasCount == 0)
        {
            GameObject cameraObject = new GameObject("Selection Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .075f, .11f);
        }
        ui = new GameObject("Car Selection UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        Canvas canvas = ui.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        CanvasScaler scaler = ui.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = .5f;
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(ui.transform, false);
        RectTransform rect = background.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(.055f, .075f, .11f);
        MakeText(background.transform, "ELEGÍ TU AUTO", 44, new Vector2(0f, 240f), new Vector2(1100f, 70f)).fontStyle = FontStyle.Bold;
        statusText = MakeText(background.transform, string.Empty, 22, new Vector2(0f, 170f), new Vector2(1120f, 70f));
        statusText.color = new Color(.75f, .81f, .88f);
        classicButton = MakeButton(background.transform, "Auto 4.3", new Vector2(-245f, 25f), new Vector2(440f, 190f), SelectClassic);
        nissanButton = MakeButton(background.transform, "Nissan R34", new Vector2(245f, 25f), new Vector2(440f, 190f), SelectNissan);
        readyButton = MakeButton(background.transform, "CONFIRMAR AUTO", new Vector2(0f, -145f), new Vector2(480f, 65f), ConfirmSelection);
        readyButton.GetComponent<Image>().color = new Color(.15f, .43f, .30f);
        readyText = readyButton.GetComponentInChildren<Text>(); readyText.fontSize = 24;
        playersText = MakeText(background.transform, string.Empty, 20, new Vector2(0f, -230f), new Vector2(1160f, 95f));
        leaveButton = MakeButton(background.transform, "SALIR", new Vector2(-520f, -310f), new Vector2(170f, 45f), LeaveRoom);
        leaveButton.GetComponent<Image>().color = new Color(.19f, .24f, .28f);
        leaveButton.GetComponentInChildren<Text>().fontSize = 19;
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        GameObject host = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        Button button = host.GetComponent<Button>(); button.targetGraphic = host.GetComponent<Image>(); button.onClick.AddListener(click);
        MakeText(host.transform, label, 34, Vector2.zero, size - new Vector2(20f, 10f)).fontStyle = FontStyle.Bold;
        return button;
    }

    private static Text MakeText(Transform parent, string content, int size, Vector2 position, Vector2 dimensions)
    {
        GameObject host = new GameObject("Label", typeof(RectTransform), typeof(Text)); host.transform.SetParent(parent, false);
        RectTransform rect = host.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        Text text = host.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; text.text = content;
        return text;
    }

    private void OnDestroy() { if (localOverlay == this) localOverlay = null; }
}
