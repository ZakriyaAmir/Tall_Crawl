using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Mime;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class hurdlesSpawner : MonoBehaviour
{
    public GameObject Branchhurdle;
    public GameObject BeesHurdle;
    public Transform hurdleParent;
    private int hurdlesCount;
    public int chunkLength = 2;

    private float yDivide;
    private float LastyDivide;
    private Vector3 LastChunkPosition;
    private bonusSpawner BonusComponent;
    private bool spawnable;

    public void Start()
    {
        spawnable = true;
        BonusComponent = transform.GetComponent<bonusSpawner>();
        StartCoroutine(spawnBranch());
        StartCoroutine(spawnBees());
    }

    public IEnumerator spawnBranch()
    {
        hurdlesCount = Random.Range(1, 7);
        if (transform.GetComponent<bonusSpawner>().isActiveAndEnabled)
        {
            StartCoroutine(BonusComponent.spawnHealth());
            StartCoroutine(BonusComponent.spawnBoost());
            StartCoroutine(BonusComponent.spawnCoin());
        }

        for (int i = 0; i <= hurdlesCount; i++)
        {
            yDivide = LastyDivide + Random.Range(0.1F, 1f);
            Vector3 rot = new Vector3(0, Random.Range(0, 360), Random.Range(0, 360));
            LastyDivide = yDivide;
            branchSpawner(yDivide, rot);
        }

        yield return new WaitForSeconds(1f);
    }


    public void branchSpawner(float y, Vector3 rot)
    {
        GameObject branch = (GameObject) Instantiate(Branchhurdle, hurdleParent);
        Vector3 position = new Vector3(0, y, 0);
        branch.transform.position = position;
        branch.transform.eulerAngles = rot;
        if (branch.transform.position.x > LastChunkPosition.x + 0.4f ||
            branch.transform.position.y > LastChunkPosition.y + 0.4f)
        {
            LastChunkPosition = branch.transform.position;
        }
        else
        {
            Destroy(branch);
        }
    }


    public IEnumerator spawnBees()
    {
        hurdlesCount = Random.Range(0, 2);
        if (hurdlesCount == 1)
        {
            hurdlesCount = Random.Range(0, 2);
        }

        if (hurdlesCount == 1)
        {
            hurdlesCount = Random.Range(0, 2);
        }

        if (hurdlesCount == 1 && spawnable)
        {
            yDivide = LastyDivide + Random.Range(0.1F, 1f);
            LastyDivide = yDivide;
            beesSpawner(yDivide);
            spawnable = false;
            yield return new WaitForSeconds(3f);
            spawnable = true;
        }
    }

    public void beesSpawner(float y)
    {
        GameObject branch = (GameObject) Instantiate(BeesHurdle, hurdleParent);
        Vector3 position = new Vector3(0, y, 0);
        branch.transform.position = position;
        if (branch.transform.position.x > LastChunkPosition.x + 0.4f ||
            branch.transform.position.y > LastChunkPosition.y + 0.4f)
        {
            LastChunkPosition = branch.transform.position;
        }
        else
        {
            StartCoroutine(spawnBees());
            return;
        }
    }
}