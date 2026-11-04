using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ScoreCounter : MonoBehaviour
{
    [SerializeField] private Transform playerHead; 
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private ParticleSystem scoreParticles;
    private int totalScore = 0;

    private void OnTriggerEnter(Collider other)
    {
        XRGrabInteractable grabInteractable = other.GetComponent<XRGrabInteractable>();

        if (grabInteractable != null && grabInteractable.isSelected)
        {
            return; 
        }

        if (other.CompareTag("Ball"))
        {
            AwardPoints();
            if (scoreParticles != null) scoreParticles.Play();
        }
    }

    private void AwardPoints()
    {
        float distance = Vector3.Distance(transform.position, playerHead.position);
        
        int points = (int)(distance*2 - 3);
        
        totalScore += points;
        
        if (scoreText != null)
        {
            scoreText.text = $"Total Score: {totalScore}\nLast Shot: +{points} pts ({distance:F1}m)";
        }
        
        Debug.Log($"Scored {points} points from {distance} meters away.");
    }
}
