using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class CreateAndJoinRooms : MonoBehaviourPunCallbacks
{
    public TMP_InputField createInput;
    public TMP_InputField joinInput;

    private enum Request { None, Create, Join }
    private Request pending;
    private string requestedRoom;
    private bool busy;
    private bool awaitingReconnect;

    private void Awake() { PhotonNetwork.AutomaticallySyncScene = true; }

    // Se conservan los nombres conectados a los botones existentes del Lobby.
    public void createRoom() { Begin(Request.Create, createInput); }
    public void joinRoom() { Begin(Request.Join, joinInput); }

    private void Begin(Request request, TMP_InputField input)
    {
        if (busy || PhotonNetwork.InRoom) return;
        if (input == null || string.IsNullOrWhiteSpace(input.text))
        {
            ConnectionFeedback.Show(this, "Escribí el nombre de la sala primero.");
            return;
        }
        requestedRoom = input.text.Trim();
        awaitingReconnect = false;
        pending = request;
        busy = true;
        if (CanSend()) SendPending();
        else
        {
            ConnectionFeedback.Show(this, "Conectando con Photon…");
            if (!PhotonNetwork.IsConnected && !PhotonNetwork.ConnectUsingSettings())
                Fail("No se pudo iniciar la conexión.", true);
        }
    }

    private static bool CanSend()
    {
        return PhotonNetwork.OfflineMode || PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer ||
               PhotonNetwork.NetworkClientState == ClientState.JoinedLobby;
    }

    private void SendPending()
    {
        if (pending == Request.None || !CanSend()) return;
        Request request = pending;
        // Una operación enviada no se reenvía al volver del Game Server al
        // Master Server después de una sala inexistente o un nombre duplicado.
        pending = Request.None;
        PhotonNetwork.LocalPlayer.SetCustomProperties(CarSelectionState.ResetProperties());
        ConnectionFeedback.Show(this, request == Request.Create ? "Creando sala…" : "Entrando a la sala…");
        bool sent;
        if (request == Request.Create)
        {
            RoomOptions options = new RoomOptions
            {
                CustomRoomProperties = new Hashtable
                {
                    { MapSelection.MapProperty, string.Empty },
                    { CarSelectionState.PhaseProperty, CarSelectionState.MapPhase },
                    { SpawnReservations.SeedProperty, System.Guid.NewGuid().GetHashCode() },
                    { SpawnReservations.Property, string.Empty }
                }
            };
            sent = PhotonNetwork.CreateRoom(requestedRoom, options);
        }
        else sent = PhotonNetwork.JoinRoom(requestedRoom);
        // PUN no emite un callback de fallo cuando la operación devuelve false.
        if (!sent) Fail("La conexión todavía no está lista. Volvé a intentar.", false);
    }

    public void Reconnect()
    {
        if (busy) return;
        if (CanSend()) { ConnectionFeedback.Hide(this); return; }
        busy = true;
        awaitingReconnect = true;
        ConnectionFeedback.Show(this, "Reconectando con Photon…");
        if (!PhotonNetwork.IsConnected && !PhotonNetwork.ConnectUsingSettings())
            Fail("No se pudo iniciar la conexión.", true);
    }

    public override void OnConnectedToMaster()
    {
        if (pending != Request.None) SendPending();
        else if (awaitingReconnect)
        {
            awaitingReconnect = false;
            busy = false;
            ConnectionFeedback.Hide(this);
        }
    }

    public override void OnJoinedLobby()
    {
        if (pending != Request.None) SendPending();
    }

    public override void OnJoinedRoom()
    {
        busy = false;
        awaitingReconnect = false;
        pending = Request.None;
        ConnectionFeedback.Hide(this);
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel(CarSelectionState.SceneForRoom(PhotonNetwork.CurrentRoom.CustomProperties));
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Fail(returnCode == ErrorCode.GameIdAlreadyExists
            ? "Ya existe una sala con ese nombre. Usá otro nombre o uníte a ella."
            : "No se pudo crear la sala. " + message, false);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        string explanation = returnCode == ErrorCode.GameDoesNotExist ? "No existe una sala con ese nombre."
            : returnCode == ErrorCode.GameFull ? "La sala está llena."
            : returnCode == ErrorCode.GameClosed ? "La sala está cerrada."
            : "No se pudo entrar a la sala. " + message;
        Fail(explanation, false);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        requestedRoom = null;
        Fail("Se perdió la conexión con Photon (" + cause + "). Podés reconectarte y volver a entrar.", true);
    }

    private void Fail(string message, bool allowReconnect)
    {
        busy = false;
        awaitingReconnect = false;
        pending = Request.None;
        ConnectionFeedback.Show(this, message, allowReconnect ? (System.Action)Reconnect : null);
        Debug.LogWarning(message);
    }
}
