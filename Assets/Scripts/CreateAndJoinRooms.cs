using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class CreateAndJoinRooms : MonoBehaviourPunCallbacks
{
    public TMP_InputField createInput;
    public TMP_InputField joinInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void createRoom()
    {
        if (string.IsNullOrWhiteSpace (createInput.text))
        {
            Debug.Log ("El campo de texto está vacío, por favor ingrese al menos un carácter para nombrar su sala.");
            return;
        }

        else
        {
            PhotonNetwork.CreateRoom(createInput.text);
        }
    }

    public void joinRoom()
    {
        if (string.IsNullOrWhiteSpace (joinInput.text))
        {
            Debug.Log ("El campo de texto está vacío, por favor ingrese al menos un carácter para que se tome como válida la sala a la que se quiera unir.");
            return;
        }

        else
        {
            PhotonNetwork.JoinRoom(joinInput.text);
        }
    }

    public override void OnJoinedRoom()
    {
        PhotonNetwork.LoadLevel("GameScene");
    }
}
