using UnityEngine;
using System.Collections;

public class BoxesSpawner : MonoBehaviour
{
    [SerializeField] private int cantidadMinima = 8;
    [SerializeField] private int cantidadMaxima = 15;
    [SerializeField] private float velocidadMinima = 1f;
    [SerializeField] private float velocidadMaxima = 2f;
    [SerializeField] private float radioDispersion = 5f;
    [SerializeField] private float caidaVertical = 3f;

    private static bool yaGenerado = false;
    private GameObject[] cajas;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerarCajas();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    //Funcion que se ejecutara al iniciar la escena
    //Esta funcion generara aleatoriamente copias de una misma caja sopresa (entre 8 y 15) que esten dispersadas respetando un radio
    void GenerarCajas ()
    {
        if (yaGenerado) return;
        yaGenerado = true;

        int cantidad = Random.Range (cantidadMinima, cantidadMaxima + 1);
        cajas = new GameObject [cantidad];

        for (int i = 0; i < cantidad; i++)
        {
            Vector3 desplazamiento = Random.insideUnitSphere * radioDispersion;
            desplazamiento.y = 0f;
            Vector3 posicion = transform.position + desplazamiento;
            //Crea una variable de tipo GameObject llamada copia en la que se crean instancias de el mismo gameObject al que se le asigno el script
            GameObject copia = Instantiate (gameObject, posicion, transform.rotation);
            //Se iguala cajas[i] a copia para aclarar que no se deben instanciar mas copias que el mínimo y el maximo del array
            cajas [i] = copia;

            Rigidbody rb = copia.GetComponent<Rigidbody>();

            //Direccion horizontal aleatoria
            if (rb != null)
            {
                Vector2 circulo = Random.insideUnitCircle.normalized;
                Vector3 direccion = new Vector3 (circulo.x, 0f, circulo.y);

                float velocidad = Random.Range(velocidadMinima, velocidadMaxima);

                rb.linearVelocity = direccion * velocidad + Vector3.down * caidaVertical;
            }
            
        }
    }
}