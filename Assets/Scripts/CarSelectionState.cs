using System.Collections.Generic;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Datos compartidos del flujo de selección; cada jugador conserva su propio auto.</summary>
public static class CarSelectionState
{
    public const string ClassicPrefab = "Auto.4.3 Variant 1";
    public const string NissanPrefab = "NissanR34";
    public const string PrefabProperty = "kartPrefab";
    public const string ReadyProperty = "kartReady";
    public const string PhaseProperty = "selectionPhase";
    public const string MapPhase = "map";
    public const string CarPhase = "cars";
    public const string PlayingPhase = "playing";
    public const string SelectionScene = "CarSelection";

    public static bool IsAllowedPrefab(string prefab)
    {
        return prefab == ClassicPrefab || prefab == NissanPrefab;
    }

    public static bool IsConfirmed(Hashtable properties)
    {
        return properties != null && IsAllowedPrefab(properties[PrefabProperty] as string) &&
               properties[ReadyProperty] is bool ready && ready;
    }

    public static bool AllConfirmed(IEnumerable<Hashtable> activePlayerProperties)
    {
        int count = 0;
        foreach (Hashtable properties in activePlayerProperties)
        {
            count++;
            if (!IsConfirmed(properties)) return false;
        }
        return count > 0;
    }

    public static bool AllActivePlayersConfirmed(IEnumerable<Player> players)
    {
        List<Hashtable> properties = new List<Hashtable>();
        foreach (Player player in players)
            if (!player.IsInactive) properties.Add(player.CustomProperties);
        return AllConfirmed(properties);
    }

    public static string SceneForRoom(Hashtable roomProperties)
    {
        string map = roomProperties?[MapSelection.MapProperty] as string;
        if (!MapSelection.IsPlayableMap(map)) return "MapSelection";
        return (roomProperties[PhaseProperty] as string) == PlayingPhase ? map : SelectionScene;
    }

    public static Hashtable ResetProperties()
    {
        return new Hashtable { { PrefabProperty, string.Empty }, { ReadyProperty, false } };
    }
}
