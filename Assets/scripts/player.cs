using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Mime;
using UnityEngine;
using UnityEngine.UI;

public class player : MonoBehaviour
{
    public float speed = 2;
    public bool boost = false;

    public Transform p1;

    private Vector2 fingerDown;
    private Vector2 fingerUp;
    public bool detectSwipeOnlyAfterRelease = false;
    public bool Ymove;
    public bool Xmove;
    public bool GameOver;
    public bool GameOverTrriggered;
    public bool jumpAllowed;
    public bool paused;
    public Text test;
    public Text score;
    public Text finalscore;
    public Text highscore;
    public float currentScore;
    public Animator playerActionAnimator;
    private float swipeUpDuration;

    public float SWIPE_THRESHOLD = 20f;
    public Camera maincam;

    public float rotSpeed = 7f;
    public int life = 3;
    private Rigidbody rb;
    public float calcSpeed;

    float _doubleTapTime;

    

    void Awake()
    {
        rb = transform.GetChild(0).GetComponent<Rigidbody>();
        jumpAllowed = true;
        maincam = GameObject.FindWithTag("MainCamera").GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!paused)
        {
            if (!GameOver)
            {
                //Count score
                currentScore = currentScore + Time.deltaTime * speed * 20;
                score.text = ((int) currentScore + "M").ToString();
                //Jump on double tap
                /*for (var i = 0; i < Input.touchCount; ++i)
                {
                    {
                        if (Input.GetTouch(i).phase == TouchPhase.Began)
                        {
                            if (Input.GetTouch(i).tapCount == 2)
                            {
                                jump();
                            }
                        }
                    }
                }*/

                // Constantly move player upwards
                if (boost)
                {
                    p1.transform.position = new Vector3(p1.transform.position.x,
                        p1.transform.position.y + calcSpeed * 2 * Time.deltaTime, p1.transform.position.z);
                    maincam.GetComponent<Animator>().SetBool("boost",true);
                }

                else
                {
                    calcSpeed = speed + currentScore / 5000;
                    p1.transform.position = new Vector3(p1.transform.position.x,
                        p1.transform.position.y + calcSpeed * Time.deltaTime, p1.transform.position.z);
                    maincam.GetComponent<Animator>().SetBool("boost",false);
                }

                foreach (Touch touch in Input.touches)
                {
                    if (touch.phase == TouchPhase.Began)
                    {
                        fingerUp = touch.position;
                        fingerDown = touch.position;
                    }

                    //Detects Swipe while finger is still moving
                    if (touch.phase == TouchPhase.Moved)
                    {
                        if (!detectSwipeOnlyAfterRelease)
                        {
                            fingerDown = touch.position;
                            checkSwipe();
                        }
                    }

                    //Detects swipe after finger is released
                    if (touch.phase == TouchPhase.Ended)
                    {
                        fingerDown = touch.position;
                        checkSwipe();
                    }


                    //Live movements
                    //Horizontal
                    if (Input.touches.Length > 0 && Input.GetTouch(0).phase == TouchPhase.Moved && Xmove)
                    {
                        float rotateSpeed = rotSpeed;
                        Touch touchZero = Input.GetTouch(0);

                        //Rotate the model based on offset
                        Vector3 localAngle = p1.localEulerAngles;
                        localAngle.y -= rotateSpeed * touchZero.deltaPosition.x;
                        p1.localEulerAngles = localAngle;
                    }

                    //Vertical
                    if (Input.touches.Length > 0 && Ymove)
                    {
                        if (Input.GetTouch(0).phase == TouchPhase.Moved)
                        {
                            swipeUpDuration = swipeUpDuration + Time.deltaTime * 7;
                            /*if (swipeUpDuration > 1)
                            {*/
                            Touch touchZero = Input.GetTouch(0);
                            if (touchZero.deltaPosition.y > 30)
                            {
                                jump();
                            }

                            //}
                        }
                        else
                        {
                            swipeUpDuration = 0;
                        }
                    }
                }
            }
            else
            {
                if (!GameOverTrriggered) 
                {
                    GameOverTrriggered = true;
                    StartCoroutine(endGame());
                }
            }
        }
    }

    IEnumerator endGame()
    {
        yield return new WaitForSeconds(2f);
        finalscore.text = ((int) currentScore).ToString();
        if (currentScore >= PlayerPrefs.GetInt("highscore"))
        {
            PlayerPrefs.SetInt("highscore", (int) currentScore);
        }

        highscore.text = PlayerPrefs.GetInt("highscore").ToString();
        //Time.timeScale = 0;

        //Show InterStitial ad
        adsplugin.instance.Showinterstial();

        //GA Event
        MyAnalytics.instance.LevelFailedEvent("Gameplay");
    }

    public void jump()
    {
        if (jumpAllowed)
        {
            playerActionAnimator.GetComponent<Animator>().SetTrigger("jump");
            playerActionAnimator.GetComponent<playerInteract>().jump();
            jumpAllowed = false;
            StartCoroutine(readyJump());
        }
    }

    IEnumerator readyJump()
    {
        yield return new WaitForSeconds(0.5f);
        jumpAllowed = true;
    }

    void checkSwipe()
    {
        //Check if Vertical swipe
        if (verticalMove() > SWIPE_THRESHOLD && verticalMove() > horizontalValMove())
        {
            if (fingerDown.y - fingerUp.y > 0) //up swipe
            {
                OnSwipeUp();
            }
            else if (fingerDown.y - fingerUp.y < 0) //Down swipe
            {
                OnSwipeDown();
            }

            fingerUp = fingerDown;
        }

        //Check if Horizontal swipe
        else if (horizontalValMove() > SWIPE_THRESHOLD && horizontalValMove() > verticalMove())
        {
            if (fingerDown.x - fingerUp.x > 0) //Right swipe
            {
                OnSwipeRight();
            }
            else if (fingerDown.x - fingerUp.x < 0) //Left swipe
            {
                OnSwipeLeft();
            }

            fingerUp = fingerDown;
        }

        //No Movement at-all
        else
        {
            Xmove = false;
            Ymove = false;
        }
    }

    float verticalMove()
    {
        return Mathf.Abs(fingerDown.y - fingerUp.y);
    }

    float horizontalValMove()
    {
        return Mathf.Abs(fingerDown.x - fingerUp.x);
    }

    //////////////////////////////////CALLBACK FUNCTIONS/////////////////////////////
    void OnSwipeUp()
    {
        Xmove = false;
        Ymove = true;
    }

    void OnSwipeDown()
    {
        Xmove = false;
        Ymove = true;
    }

    void OnSwipeLeft()
    {
        Xmove = true;
        Ymove = false;
    }

    void OnSwipeRight()
    {
        Xmove = true;
        Ymove = false;
    }
}