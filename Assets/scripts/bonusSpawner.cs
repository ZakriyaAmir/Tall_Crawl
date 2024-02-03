using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bonusSpawner : MonoBehaviour
{
    public GameObject health;
    public GameObject boost;
    public GameObject corns;
    public Transform bonusParent;
    private int cornsCount;
    private int healthCount;
    private int boostCount;
    public int chunkLength = 2;
    
    public float LastYCoordinate;

    public void Start()
    {
        //StartCoroutine(spawnCorns());
        StartCoroutine(spawnBoost());
    }

    public IEnumerator spawnHealth()
    {
        healthCount = Random.Range(0,2);
        if (healthCount == 1)
        {
             healthCount = Random.Range(0,2);
        }
        if (healthCount == 1)
        {
            healthCount = Random.Range(0,2);
        }
        if (healthCount == 1)
        {
            healthCount = Random.Range(0,2);
        }
        if (healthCount == 1)
        {
            healthCount = Random.Range(0,2);
        }

        if (healthCount > 0)
        {
            LastYCoordinate = GameObject.Find("player").transform.position.y + Random.Range(3f,7f);
            Vector3 rot = new Vector3(0,Random.Range(0,360),0);
            healthSpawner(LastYCoordinate,rot);
            
        }

        yield return new WaitForSeconds(1f);
    }
    
    public void healthSpawner(float y,Vector3 rot)
    {
        GameObject prop = (GameObject) Instantiate(health,bonusParent);
        Vector3 position = new Vector3(0, y, 0);
        prop.transform.position = position;
        prop.transform.eulerAngles = rot;
    }
    
    
    
    
    
    
    
    
    
    
    
    
    public IEnumerator spawnBoost()
    {
        boostCount = Random.Range(0,2);
        if (boostCount == 1)
        {
            boostCount = Random.Range(0,2);
        }
        if (boostCount == 1)
        {
            boostCount = Random.Range(0,2);
        }

        if (boostCount > 0)
        {
            LastYCoordinate = GameObject.Find("player").transform.position.y + Random.Range(3f,7f);
            Vector3 rot = new Vector3(0,Random.Range(0,360),0);
            boostSpawner(LastYCoordinate,rot);
            
        }

        yield return new WaitForSeconds(1f);
    }
    
    public void boostSpawner(float y,Vector3 rot)
    {
        GameObject prop = (GameObject) Instantiate(boost,bonusParent);
        Vector3 position = new Vector3(0, y, 0);
        prop.transform.position = position;
        prop.transform.eulerAngles = rot;
    }
}