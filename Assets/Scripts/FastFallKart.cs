using UnityEngine;

public class FastFallKart : MonoBehaviour
{
    public float factorGravedad = 2.0f; //La gravedad se multiplica por este numero
    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate ()
    {
        //Aplica una fuerza constante hacia abajo para simular mas peso:
        rb.AddForce(Physics.gravity * (factorGravedad - 1) * rb.mass);
    }
}
