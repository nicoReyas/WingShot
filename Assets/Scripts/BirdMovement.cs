using UnityEngine;

public enum BirdFlightPattern { Straight, Wave, Swoop }

public class BirdMovement : MonoBehaviour
{
    // Movement settings for one bird; direction is +1 right or -1 left.
    public float speed = 3f;
    public BirdFlightPattern flightPattern;
    float direction = 1f, age, startY, amplitude = .35f, frequency = 2f;
    float minY = -3f, maxY = 3f, leftEdge = -12f, rightEdge = 12f;

    // Remember the height where this bird began its flight.
    void Start() { startY = transform.position.y; }

    // Give this bird its speed, direction, flight pattern, and screen limits.
    public void Configure(float newSpeed, float travelDirection, BirdFlightPattern pattern,
        float lowerY, float upperY, float left, float right)
    {
        speed = newSpeed;
        direction = travelDirection;
        flightPattern = pattern;
        startY = transform.position.y;
        minY = lowerY; maxY = upperY; leftEdge = left; rightEdge = right;
        amplitude = Random.Range(.22f, .55f);
        frequency = Random.Range(1.7f, 3f);
        var sprite = GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.flipX = direction < 0;
    }

    // Move the bird each frame, add a gentle flight pattern, and remove it after it leaves.
    void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        age += Time.deltaTime;
        // Sine waves make smooth vertical motion instead of sudden turns.
        float offset = 0f;
        if (flightPattern == BirdFlightPattern.Wave)
            offset = Mathf.Sin(age * frequency) * amplitude;
        else if (flightPattern == BirdFlightPattern.Swoop)
            offset = Mathf.Sin(age * frequency * .65f) * amplitude * 1.6f;
        var position = transform.position;
        position.x += direction * speed * Time.deltaTime;
        // Keep the bird above the grass and below the scoreboard.
        position.y = Mathf.Clamp(startY + offset, minY, maxY);
        transform.position = position;
        float tilt = flightPattern == BirdFlightPattern.Straight ? 0f :
            Mathf.Cos(age * frequency) * 9f * direction;
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        if ((direction > 0 && position.x > rightEdge) ||
            (direction < 0 && position.x < leftEdge) || age > 30f)
            Destroy(gameObject);
    }
}
