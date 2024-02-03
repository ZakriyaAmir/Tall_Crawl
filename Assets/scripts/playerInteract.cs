using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class playerInteract : MonoBehaviour
{
    public int life;
    public GameObject GameOverScreen;
    public GameObject impactParticle;
    public GameObject BeesimpactParticle;
    public Image lifebar;
    
    public float boostDuration;
    private player p1;

    public AudioClip woodImpactSound;
    public AudioClip beesImpactSound;
    public AudioClip deathSound;
    public AudioClip boostSound;
    public AudioClip healthSound;
    public AudioClip jumpSound;
    AudioSource audioSource;

    // Start is called before the first frame update
    void Start()
    {
        GameOverScreen.SetActive(false);
        life = 3;
        lifebar.fillAmount = 1;
        p1 = transform.parent.GetComponent<player>();

        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        if (boostDuration > 0)
        {
            p1.boost = true;

            boostDuration = boostDuration - Time.deltaTime;
            if (boostDuration < 0.1f)
            {
                p1.boost = false;
                StartCoroutine(Invincible());
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("branch"))
        {
            if (boostDuration > 0)
            {
                
            }
            else
            {
                //Make phone vibrate
                if (PlayerPrefs.GetInt("DisableHaptic") == 0)
                {
                    Handheld.Vibrate();
                }
                
                //Reduce healthbar
                lifebar.fillAmount = lifebar.fillAmount - 0.33f;
                if (life > 1)
                {
                    life = life - 1;
                    StartCoroutine(Invincible());
                }
                else
                {
                    death();
                }
            }

            //Spawn burst effect
            GameObject burst = Instantiate(impactParticle,other.transform.parent.position,other.transform.parent.rotation,other.transform.parent.parent);
            //Play burst sound
            audioSource.PlayOneShot(woodImpactSound, 1F);
            Destroy(other.transform.parent.gameObject);
        }

        if (other.CompareTag("bees"))
        {
            if (boostDuration > 0)
            {
                
            }
            else
            {
                Destroy(other.gameObject);
                //Make phone vibrate
                if (PlayerPrefs.GetInt("DisableHaptic") == 0)
                {
                    Handheld.Vibrate();
                }
                //Reduce healthbar
                lifebar.fillAmount = lifebar.fillAmount - 0.33f;
                if (life > 1)
                {
                    life = life - 1;
                    StartCoroutine(Invincible());
                }
                else
                {
                    death();
                }
            }
            
            //Spawn burst effect
            GameObject burst =(GameObject) Instantiate(BeesimpactParticle,transform.position,transform.rotation,other.transform.parent.parent);

            //Play burst sound
            audioSource.PlayOneShot(beesImpactSound, 1F);
        }

        if (other.CompareTag("health"))
        {
            Destroy(other.gameObject);
            audioSource.PlayOneShot(healthSound, 1F);
            //Increase healthbar
            if (life < 3)
            {
                increaseHealth();
            }
        }

        if (other.CompareTag("boost"))
        {
            Destroy(other.gameObject);
            // Activate boost
            transform.GetComponent<Animator>().SetBool("boost", true);
            boostDuration = 4;
            audioSource.PlayOneShot(boostSound, 1F);
        }
    }


    public void increaseHealth() 
    {
        lifebar.fillAmount = lifebar.fillAmount + 0.33f;
        life = life + 1;
    }

    public IEnumerator Invincible()
    {
        gameObject.GetComponent<CapsuleCollider>().enabled = false;
        transform.GetChild(0).gameObject.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        transform.GetChild(0).gameObject.SetActive(true);
        yield return new WaitForSeconds(0.4f);
        transform.GetChild(0).gameObject.SetActive(false);
        yield return new WaitForSeconds(0.4f);
        transform.GetChild(0).gameObject.SetActive(true);
        gameObject.GetComponent<CapsuleCollider>().enabled = true;
    }

    public void death()
    {
        life = 0;
        Animator playerAnim = gameObject.GetComponent<Animator>();
        playerAnim.SetTrigger("death");
        //Stop game
        transform.parent.GetComponent<player>().GameOver = true;
        transform.GetComponent<Animator>().SetBool("death", true);
        audioSource.PlayOneShot(deathSound, 1F);
        StartCoroutine(gameOver());
        GameObject.Find("Canvas").GetComponent<menu>().pauseBtn.enabled = false;
    }

    public IEnumerator gameOver()
    {
        yield return new WaitForSeconds(2);
        GameOverScreen.SetActive(true);
    }
    
    public void jump() {
        audioSource.PlayOneShot(jumpSound, 1F);
    }
}