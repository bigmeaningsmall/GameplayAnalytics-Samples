
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public int damage = 1; // The amount of damage the bullet deals

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Stop the bullet's movement and enable gravity
        rb.linearVelocity = Vector3.zero;
        rb.useGravity = true;

        //check if the bullet hit something that has the 'IDamagable' interface  
        if (collision.gameObject.GetComponent<IDamagable>() != null){  
		
            //Get the IDamagable Interface from the collider object
            IDamagable damageable = collision.gameObject.GetComponent<IDamagable>();  
		    
            // Call the IDamagable interface to Take damage and show hit effect 
            damageable.TakeDamage(damage);  
            damageable.ShowHitEffect();  
        }
        
        // Destroy the bullet after it collides with an object
        Destroy(gameObject);
    }
}