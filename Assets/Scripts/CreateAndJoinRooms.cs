using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class CreateAndJoinRooms : MonoBehaviourPunCallbacks
{
    public TMP_InputField createInput;
    public TMP_InputField joinInput;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void createRoom()
    {
        if (createInput == null || string.IsNullOrWhiteSpace(createInput.text))
        {
            Debug.Log ("El campo de texto está vacío, por favor ingrese al menos un carácter para nombrar su sala.");
            return;
        }

        else
        {
            RoomOptions options = new RoomOptions
            {
                CustomRoomProperties = new Hashtable { { MapSelection.MapProperty, string.Empty } }
            };
            PhotonNetwork.CreateRoom(createInput.text.Trim(), options);
        }
    }

    public void joinRoom()
    {
        if (joinInput == null || string.IsNullOrWhiteSpace(joinInput.text))
        {
            Debug.Log ("El campo de texto está vacío, por favor ingrese al menos un carácter para que se tome como válida la sala a la que se quiera unir.");
            return;
        }

        else
        {
            PhotonNetwork.JoinRoom(joinInput.text.Trim());
        }
    }

    public override void OnJoinedRoom()
    {
        // PUN sincroniza también a quienes se unen con la partida empezada.
        if (!PhotonNetwork.IsMasterClient) return;
        string selectedMap = PhotonNetwork.CurrentRoom.CustomProperties[MapSelection.MapProperty] as string;
        PhotonNetwork.LoadLevel(MapSelection.IsPlayableMap(selectedMap) ? selectedMap : "MapSelection");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning("No se pudo crear la sala: " + message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning("No se pudo entrar a la sala: " + message);
    }
}
