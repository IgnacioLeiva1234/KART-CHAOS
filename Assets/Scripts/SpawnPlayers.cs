using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class SpawnPlayers : MonoBehaviourPunCallbacks
{
    [Header("Prefab del jugador")]
    [Tooltip("Referencias disponibles en Resources. La elección personal se guarda en kartPrefab.")]
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
    private bool spawning;
    private bool waitingToSpawn;
    private bool releasePending;
    private KartController localKart;
    private float nextReservationAttempt;

    private void Start()
    {
        InitializeRoomPlayer();
    }

    public override void OnJoinedRoom()
    {
        // PUN puede terminar de cargar la escena sincronizada antes de recibir
        // el evento Join que cambia el estado Joining a Joined.
        InitializeRoomPlayer();
    }

    private void InitializeRoomPlayer()
    {
        if (!PhotonNetwork.InRoom) return;
        if (scenePreviewKart != null) scenePreviewKart.SetActive(false);
        if (!TrySpawnSelectedKart() && !CarSelectionState.IsConfirmed(PhotonNetwork.LocalPlayer.CustomProperties))
        {
            CarSelection.ShowForLateJoin(TrySpawnSelectedKart);
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom || Time.unscaledTime < nextReservationAttempt) return;
        if (releasePending) ReleaseReservation();
        else if (waitingToSpawn)
        {
            TrySpawnSelectedKart();
            nextReservationAttempt = Time.unscaledTime + .25f;
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (!changedProps.ContainsKey(SpawnReservations.Property) && !changedProps.ContainsKey(SpawnReservations.SeedProperty)) return;
        if (releasePending) ReleaseReservation();
        else if (waitingToSpawn) TrySpawnSelectedKart();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RemoveDepartedReservations();
        if (waitingToSpawn) TrySpawnSelectedKart();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RemoveDepartedReservations();
        if (waitingToSpawn) TrySpawnSelectedKart();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer.IsLocal && !spawned) TrySpawnSelectedKart();
    }

    public override void OnLeftRoom()
    {
        if (FindAnyObjectByType<CarSelection>() != null) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(CarSelectionState.ResetProperties());
        SceneManager.LoadScene("Lobby");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        CarSelection.ShowForLateJoin(TrySpawnSelectedKart);
    }

    public bool TrySpawnSelectedKart()
    {
        if (spawning) return false;
        if (spawned)
        {
            // Si el evento de Photon llegó antes de Start, la escena ya está
            // completamente cargada aquí y puede terminar de enlazarse su HUD.
            if (localKart != null) DriftChargeUI.BindSceneToLocalKart(localKart);
            return true;
        }
        if (!PhotonNetwork.InRoom) return false;
        if (scenePreviewKart != null) scenePreviewKart.SetActive(false);
        if (!CarSelectionState.IsConfirmed(PhotonNetwork.LocalPlayer.CustomProperties))
        { waitingToSpawn = false; return false; }

        // También evita duplicar un kart si hay dos spawners en la escena.
        foreach (KartController kart in FindObjectsByType<KartController>())
        {
            PhotonView view = kart.GetComponent<PhotonView>();
            if (view != null && view.InstantiationId > 0 && !view.IsRoomView && view.OwnerActorNr > 0 &&
                view.OwnerActorNr == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                spawned = true;
                waitingToSpawn = false;
                localKart = kart;
                DriftChargeUI.BindSceneToLocalKart(kart);
                ReleaseReservation();
                CloseLateJoinOverlay();
                return true;
            }
        }

        string prefabName = PhotonNetwork.LocalPlayer.CustomProperties[CarSelectionState.PrefabProperty] as string;
        GameObject selectedPrefab = Resources.Load<GameObject>(prefabName);
        if (selectedPrefab == null || selectedPrefab.GetComponent<PhotonView>() == null || selectedPrefab.GetComponent<KartController>() == null)
        {
            Debug.LogError("SpawnPlayers: falta un prefab de kart válido en Resources/" + prefabName);
            waitingToSpawn = false;
            PhotonNetwork.LocalPlayer.SetCustomProperties(CarSelectionState.ResetProperties());
            return false;
        }

        waitingToSpawn = true;
        if (!TryReservePoint(out RespawnPoint point)) return false;
        Vector3 position = point != null
            ? point.transform.position
            : new Vector3(Random.Range(minX, maxX), fixedValueY, Random.Range(minZ, maxZ));
        Quaternion rotation = point != null ? point.transform.rotation : Quaternion.identity;

        spawning = true;
        GameObject kartCreated;
        try { kartCreated = PhotonNetwork.Instantiate(prefabName, position, rotation); }
        finally { spawning = false; }
        if (kartCreated == null) return false;
        players.Add(kartCreated);
        localKart = kartCreated.GetComponent<KartController>();
        spawned = true;
        waitingToSpawn = false;
        DriftChargeUI.BindSceneToLocalKart(localKart);
        ReleaseReservation();
        CloseLateJoinOverlay();
        return true;
    }

    private bool TryReservePoint(out RespawnPoint point)
    {
        point = null;
        List<RespawnPoint> points = RespawnPoint.GetStablePoints();
        if (points.Count == 0) return true; // Conserva el respaldo de escenas sin puntos.
        var room = PhotonNetwork.CurrentRoom;
        if (!(room.CustomProperties[SpawnReservations.SeedProperty] is int seed) ||
            !(room.CustomProperties[SpawnReservations.Property] is string saved))
        {
            // Compatibilidad con salas creadas sin pasar por el Lobby de producción.
            if (PhotonNetwork.IsMasterClient && Time.unscaledTime >= nextReservationAttempt)
            {
                nextReservationAttempt = Time.unscaledTime + .25f;
                var initial = new Hashtable();
                if (!(room.CustomProperties[SpawnReservations.SeedProperty] is int))
                    initial[SpawnReservations.SeedProperty] = System.Guid.NewGuid().GetHashCode();
                if (!(room.CustomProperties[SpawnReservations.Property] is string)) initial[SpawnReservations.Property] = string.Empty;
                room.SetCustomProperties(initial);
            }
            return false;
        }
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        Dictionary<int, int> reservations = RespawnPoint.GetActiveReservations();
        if (reservations.TryGetValue(actor, out int owned))
        {
            if (owned < points.Count && RespawnPoint.IsFree(points[owned]))
            { point = points[owned]; return true; }
            ReleaseReservation(); // Un kart pudo moverse hasta el punto mientras se confirmaba.
            return false;
        }
        if (Time.unscaledTime < nextReservationAttempt) return false;
        int[] order = SpawnReservations.Order(points.Count, seed);
        int first = (actor - 1) % order.Length;
        for (int offset = 0; offset < order.Length; offset++)
        {
            int index = order[(first + offset) % order.Length];
            if (!RespawnPoint.IsFree(points[index]) || RespawnPoint.ReservedForOther(reservations, actor, index)) continue;
            reservations[actor] = index;
            nextReservationAttempt = Time.unscaledTime + .25f;
            // Sólo el servidor confirma la reserva. Si dos solicitudes usan la misma
            // versión, una gana; la otra reintenta con la nueva lista de reservas.
            room.SetCustomProperties(new Hashtable { { SpawnReservations.Property, SpawnReservations.Encode(reservations) } },
                new Hashtable { { SpawnReservations.Property, saved } });
            return false;
        }
        nextReservationAttempt = Time.unscaledTime + .25f;
        return false;
    }

    private void ReleaseReservation()
    {
        if (!PhotonNetwork.InRoom) { releasePending = false; return; }
        var room = PhotonNetwork.CurrentRoom;
        if (!(room.CustomProperties[SpawnReservations.Property] is string saved)) { releasePending = false; return; }
        Dictionary<int, int> reservations = RespawnPoint.GetActiveReservations();
        if (!reservations.Remove(PhotonNetwork.LocalPlayer.ActorNumber)) { releasePending = false; return; }
        releasePending = true;
        nextReservationAttempt = Time.unscaledTime + .25f;
        // Instantiate ya quedó encolado antes de liberar: los receptores procesan
        // la creación fiable del kart antes de ver el punto disponible de nuevo.
        room.SetCustomProperties(new Hashtable { { SpawnReservations.Property, SpawnReservations.Encode(reservations) } },
            new Hashtable { { SpawnReservations.Property, saved } });
    }

    private static void CloseLateJoinOverlay()
    {
        foreach (CarSelection selection in FindObjectsByType<CarSelection>())
            if (selection.IsLateJoinSelection) Destroy(selection.gameObject);
    }

    private void RemoveDepartedReservations()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        var room = PhotonNetwork.CurrentRoom;
        if (!(room.CustomProperties[SpawnReservations.Property] is string saved)) return;
        string active = SpawnReservations.Encode(RespawnPoint.GetActiveReservations());
        if (active == saved) return;
        room.SetCustomProperties(new Hashtable { { SpawnReservations.Property, active } },
            new Hashtable { { SpawnReservations.Property, saved } });
    }

}
