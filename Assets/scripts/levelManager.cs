using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class levelManager : MonoBehaviour
{
    public float lastChunkY;
    public float distanceBetweenChunks = 1.4f;
    public GameObject stemChunk;
    public Transform stemParent;
    
    // Start is called before the first frame update
    void Awake()
    {
        for (int i = 0; i <= 5; i++)
        {
            MakeChunk();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void DeleteChunk(GameObject obj)
    {
        Destroy(obj);
        MakeChunk();
    }

    public void MakeChunk()
    {
        GameObject newChunk = (GameObject) Instantiate(stemChunk,stemParent);
        lastChunkY = lastChunkY + distanceBetweenChunks;
        newChunk.transform.position = new Vector3(0, lastChunkY,0);
        //Make hurdles
        hurdlesSpawner branch = transform.GetComponent<hurdlesSpawner>();
        branch.Start();
    }
}
