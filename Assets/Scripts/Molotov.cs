using UnityEngine;

public class Molotov : MonoBehaviour
{
    GameObject molotov;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Molotov scriptMolotov = FindFirstObjectByType <Molotov>();

        if (scriptMolotov != null)
        {
            molotov = scriptMolotov.gameObject; //"molotov" es la variable que representa al objeto 3d de la molotov en la escena
        }

        else
        {
            Debug.LogWarning ("No se encontro ningun gameObject con el script 'Molotov'.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
