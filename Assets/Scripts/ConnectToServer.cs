using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class ConnectToServer : MonoBehaviourPunCallbacks
{
    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        RetryConnection();
    }

    public void RetryConnection()
    {
        ConnectionFeedback.Hide(this);
        if (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient) PhotonNetwork.LoadLevel(CarSelectionState.SceneForRoom(PhotonNetwork.CurrentRoom.CustomProperties));
            return;
        }
        if (PhotonNetwork.OfflineMode || PhotonNetwork.InLobby)
            SceneManager.LoadScene("Lobby");
        else if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
        {
            if (!PhotonNetwork.JoinLobby()) ConnectionFeedback.Show(this, "No se pudo entrar al Lobby.", RetryConnection);
        }
        else if (!PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.ConnectUsingSettings()) ConnectionFeedback.Show(this, "No se pudo iniciar la conexión con Photon.", RetryConnection);
        }
    }

    public override void OnConnectedToMaster()
    {
        if (!PhotonNetwork.JoinLobby()) ConnectionFeedback.Show(this, "No se pudo entrar al Lobby.", RetryConnection);
    }

    public override void OnJoinedLobby()
    {
        SceneManager.LoadScene("Lobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        ConnectionFeedback.Show(this, "No se pudo conectar con Photon (" + cause + "). Revisá la conexión y reintentá.", RetryConnection);
    }
}
