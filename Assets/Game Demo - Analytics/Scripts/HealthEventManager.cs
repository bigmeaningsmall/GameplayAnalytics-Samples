public static class HealthEventManager
{
    // Define a delegate that handles health-related events
    public delegate void HealthEvent(int currentHealth);

    // Called when any object implementing IDamagable takes damage
    public static HealthEvent OnObjectDamaged;

    // Called when any object implementing IDamagable is destroyed
    public static HealthEvent OnObjectDestroyed;
}