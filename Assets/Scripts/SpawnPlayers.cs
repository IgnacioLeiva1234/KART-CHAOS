using UnityEngine;
using System.Collections;
using Photon.Pun;

public class SpawnPlayers : MonoBehaviour
{
    public GameObject playerPrefab;

    public float minX;
    public float maxX;
    public float fixedValueY;
    public float minZ;
    public float maxZ;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Vector3 randomPosition = new Vector3 (Random.Range (minX, maxX), fixedValueY, Random.Range (minZ, maxZ));

        PhotonNetwork.Instantiate (playerPrefab.name, randomPosition, Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
