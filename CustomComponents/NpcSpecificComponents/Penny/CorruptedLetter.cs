/*
using PixelInternalAPI.Classes;
using TMPro;
using UnityEngine;

namespace BBTimes.CustomComponents.NpcSpecificComponents.Penny;

public class CorruptedLetter : MonoBehaviour
{
    public GameObject owner;
    public TextMeshPro text;
    public Collider collider, blockingCollider;
    public EnvironmentController ec;
    public string[] corruptedLetterSet = [":", "/", "\\", "^", "~", "]", "[", "ª", "º", "+", "-", ")", "(", "*", "&", "¨", "%", "$", "#", "@", "!", "?", ";", "<", ">", ".", ",", "§"];
    public float minSize = 185f, maxSize = 350f, maxDelayToChangeText, lifeTime = 120f;
    public float minLetterCircleRadius = 4f, maxLetterCircleRadius = 7f, minRadiusSpeed = 2f, maxRadiusSpeed = 5f;

    float delayToChangeCorruptionText = 0f, letterCircleRadius, radiusSpeed;
    int lastCharIndex = -1;
    readonly MovementModifier moveMod = new(Vector3.zero, 0.9f);

    ActivityModifier targetToAttach;
    Vector3 randomizedOffset;
    float circleAngle = 0f;

    void Start()
    {
        maxDelayToChangeText = Random.Range(0.075f, 0.1f);
        letterCircleRadius = Random.Range(minLetterCircleRadius, maxLetterCircleRadius);
        radiusSpeed = Random.Range(minRadiusSpeed, maxRadiusSpeed);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger || owner == other.gameObject || targetToAttach) return;

        if ((other.CompareTag("NPC") || other.CompareTag("Player")) && other.TryGetComponent(out targetToAttach))
        {
            collider.enabled = false;
            blockingCollider.enabled = true;
            targetToAttach.moveMods.Add(moveMod);
            randomizedOffset = Random.onUnitSphere * Random.Range(0.25f, 2.45f);
        }
    }

    void Update()
    {
        if (targetToAttach)
        {
            circleAngle += radiusSpeed * Time.deltaTime * ec.EnvironmentTimeScale;
            circleAngle %= 360f;
            float angle = circleAngle + (2 * Mathf.PI);
            transform.position =
            targetToAttach.transform.position + randomizedOffset +
            (new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * letterCircleRadius);

            lifeTime -= ec.EnvironmentTimeScale * Time.deltaTime;
            if (lifeTime <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (delayToChangeCorruptionText > 0f)
        {
            delayToChangeCorruptionText -= ec.EnvironmentTimeScale * Time.deltaTime;
            return;
        }
        int index = Random.Range(0, corruptedLetterSet.Length);
        while (corruptedLetterSet.Length != 1 && lastCharIndex == index)
            index = Random.Range(0, corruptedLetterSet.Length);
        text.text = corruptedLetterSet[index];
        lastCharIndex = index;

        delayToChangeCorruptionText += maxDelayToChangeText;
    }

    void OnDestroy()
    {
        targetToAttach?.moveMods.Remove(moveMod);
    }

}
*/