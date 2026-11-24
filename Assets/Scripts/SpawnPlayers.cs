using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class SpawnPlayers : MonoBehaviour
{
    [Header("Prefab del jugador")]
    [Tooltip("Se usa el primer prefab válido de esta lista. Debe estar dentro de Resources.")]
    public List<GameObject> projectPrefabs = new List<GameObject>();

    [Header("Jugadores creados por esta computadora")]
    public List<GameObject> players = new List<GameObject>();

    [Header("Kart para probar la escena sin conectarse")]
    public GameObject scenePreviewKart;

    [Header("Posición alternativa si faltan RespawnPoints")]
    public float minX;
    public float maxX;
    public float fixedValueY;
    public float minZ;
    public float maxZ;

    private bool spawned;

    private void Start()
    {
        if (spawned || !PhotonNetwork.InRoom) return;
        if (scenePreviewKart != null) scenePreviewKart.SetActive(false);

        // También evita duplicar un kart si hay dos spawners en la escena.
        foreach (KartController kart in FindObjectsByType<KartController>(FindObjectsSortMode.None))
        {
            PhotonView view = kart.GetComponent<PhotonView>();
            if (view != null && view.ViewID != 0 && view.IsMine)
            {
                spawned = true;
                return;
            }
        }

        GameObject selectedPrefab = projectPrefabs.Find(prefab => prefab != null);
        if (selectedPrefab == null)
        {
            Debug.LogError("SpawnPlayers: asigná un prefab de kart de Resources en Project Prefabs.");
            return;
        }

        RespawnPoint point = RespawnPoint.GetRandom();
        Vector3 position = point != null
            ? point.transform.position
            : new Vector3(Random.Range(minX, maxX), fixedValueY, Random.Range(minZ, maxZ));
        Quaternion rotation = point != null ? point.transform.rotation : Quaternion.identity;

        GameObject kartCreated = PhotonNetwork.Instantiate(selectedPrefab.name, position, rotation);
        if (kartCreated == null) return;
        players.Add(kartCreated);
        spawned = true;
    }
}
