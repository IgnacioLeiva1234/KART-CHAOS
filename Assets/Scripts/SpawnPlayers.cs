using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

public class SpawnPlayers : MonoBehaviour
{
    [Header("Project prefabs")]
    [Tooltip("Prefab dentro de una carpeta Resources requerida por Photon")]
    public List <GameObject> projectPrefabs = new List <GameObject>();
    [Header("Game prefabs (players)")]
    [Tooltip("Prefab dentro de una carpeta Resources requerida por Photon")]
    public List <GameObject> players = new List <GameObject>();

    [Header("Along-normals ranged spawn values")]
    [Tooltip("Rango de valores de los ejes X, Y e Z en los que apareceran los jugadores")]
    public float minX;
    public float maxX;
    public float fixedValueY; //Se mantiene ya que los jugadores spawnean a la misma altura
    public float minZ;
    public float maxZ;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        if (PhotonNetwork.IsConnectedAndReady)
        {
            foreach (GameObject prefab in projectPrefabs)
            {
                Vector3 randomPosition = new Vector3 (Random.Range (minX, maxX), fixedValueY, Random.Range (minZ, maxZ));
                GameObject clonCreado = PhotonNetwork.Instantiate (prefab.name, randomPosition, Quaternion.identity);

                players.Add (clonCreado);
                StartCoroutine (SpawnDelay());

                IEnumerator SpawnDelay(float WaitTime = 0.0111f)
                {
                    yield return new WaitForSeconds (WaitTime);
                }   
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
