﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.XR.MagicLeap;
using MagicLeapTools;
using UnityEngine.XR;

public class SceneControl : MonoBehaviour
{
    #region Variables
    [Header("Settings Object")]
    [SerializeField]
    private Settings gameSettings = null;

    [Header("World Mesh Materials")]
    [SerializeField]
    private Material occlusionMat = null;
    [SerializeField]
    private Material colorfulMat = null;

    //prefabs to instantiate or spawn
    [Header("Prefabs")]
    [SerializeField]
    private GameObject placementArenaPrefab = null;
    private const string arenaPrefabPath = "Arena";
    private const string ballPrefabPath = "Ball";
    [SerializeField]
    private GameObject ballPrefab = null;
    private const string paddlePrefabPath = "Paddle";

    //references to scene objects
    [Header("Scene References")]
    [SerializeField]
    private Transmission transmissionComp = null;
    [SerializeField]
    private MeshRenderer worldMeshOriginal = null; //Reference to world mesh renderer to allow for visibility toggling
    [SerializeField]
    private Pointer pointer = null;                //Used to access controller events and transform
    [SerializeField]
    private GameObject headposeMenus = null;       //Reference to headpose menus, allows for easy visibility toggling
    [SerializeField]
    private GameObject pauseMenu = null;           //Ingame pause menu, cached for its frequent calls
    [SerializeField]
    private GameObject gameOverMenu = null;        //Game Over page, reference to enable quickly after the game ends

    //references to scene objects after instantiation
    private TransmissionObject paddle = null;
    private TransmissionObject ball = null;
    private TransmissionObject arena = null;
    private GameObject placementArena = null;

    //state variables
    private bool gameOwner = false;
    #endregion //Variables

    public Transform ArenaTransform { get => arena?.transform; }

    //Called when change in connection status occurs, passing current connection status
    public event Action<bool> ConnectionEvent;

    #region Unity Methods
    //Reference checking
    private void Awake()
    {
        Assert.IsNotNull(gameSettings, "gameSettings not assigned on SceneControl");
        Assert.IsNotNull(occlusionMat, "occlusionMat not assigned on SceneControl");
        Assert.IsNotNull(colorfulMat, "colorfulMat not assigned on SceneControl");
        Assert.IsNotNull(placementArenaPrefab, "placementArena is not set on SceneControl");
        Assert.IsNotNull(ballPrefab, "ballPrefab is not assigned on SceneControl");
        Assert.IsNotNull(worldMeshOriginal, "worldMeshOriginal is not assigned on SceneControl");
        Assert.IsNotNull(pointer, "pointer is not set on SceneControl");
        Assert.IsNotNull(headposeMenus, "headposeMenus is not set on SceneControl");
    }

    //Scene Initializations
    void Start()
    {
        transmissionComp.gameObject.SetActive(false);
        pointer.gameObject.SetActive(true);
    }

    void Update()
    {
        ;
    }
    #endregion //Unity Methods

    #region Public Functions
    /// <summary>
    /// Called When starting to set up a new game, shows the world mesh
    /// </summary>
    public void StartMeshing()
    {
        //Make mesh visible
        worldMeshOriginal.material = colorfulMat;
        foreach (Transform meshObj in GameObject.Find("WorldMesh").transform)
        {
            meshObj.GetComponent<MeshRenderer>().material = colorfulMat;
        }
        //Add responses to controller input
        ControlInput cIScript = pointer.GetComponent<ControlInput>();
        //When trigger pulled, move on to next setup step
        cIScript.OnTriggerDown.AddListener(() =>
        {
            //Switch mesh from visible to occluding
            worldMeshOriginal.material = occlusionMat;
            foreach (Transform meshObj in GameObject.Find("WorldMesh").transform)
            {
                meshObj.GetComponent<MeshRenderer>().material = occlusionMat;
            }
            //Enable next menu
            headposeMenus.transform.GetChild(0).Find("MeshingConfirmationPage").gameObject.SetActive(true);
            //Unsubscribe events
            cIScript.OnTriggerDown.RemoveAllListeners();
            cIScript.OnHomeButtonTap.RemoveAllListeners();
        });
        //When home button tapped, go back
        cIScript.OnHomeButtonTap.AddListener(() =>
        {
            //Switch mesh from visible to occluding
            worldMeshOriginal.material = occlusionMat;
            foreach (Transform meshObj in GameObject.Find("WorldMesh").transform)
            {
                meshObj.GetComponent<MeshRenderer>().material = occlusionMat;
            }
            //Enable previous menu
            headposeMenus.transform.GetChild(0).Find("MeshingInstructionsPage").gameObject.SetActive(true);
            //Unsubscribe events
            cIScript.OnTriggerDown.RemoveAllListeners();
            cIScript.OnHomeButtonTap.RemoveAllListeners();
        });
    }

    /// <summary>
    /// Spawns stand-in arena to place, manages confirmation and cancellation
    /// </summary>
    public void StartPlacement()
    {
        //If the placement arena hasn't already been created, set it up
        if (!placementArena)
        {
            //if this is called ingame with an already placed arena, delete it.
            if (!arena)
            {
                placementArena = Instantiate(placementArenaPrefab);
                placementArena.transform.SetParent(GameObject.Find("[_DYNAMIC]").transform);
            }
            else
            {
                placementArena = GameObject.Instantiate(placementArenaPrefab, arena.transform.position, arena.transform.rotation, GameObject.Find("[_DYNAMIC]").transform);
                arena.Despawn();
            }
        }
        //Cache references
        Placement arenaPlc = placementArena.GetComponent<Placement>();
        ControlInput cIScript = pointer.GetComponent<ControlInput>();
        //Start the placement session, OnPlacementConfirm is called when succesful placement occurs
        arenaPlc.Place(pointer.transform, new Vector3(3f, 2f, 2f), true, false, OnPlacementConfirm);
        //When the user pulls the trigger, attempt placement confirmation.
        cIScript.OnTriggerDown.AddListener(() => {
            arenaPlc.Confirm();
            if (!arenaPlc.IsRunning)
            {
                cIScript.OnTriggerDown.RemoveAllListeners();
                cIScript.OnHomeButtonTap.RemoveAllListeners();
            }
        });
        //If home is pressed, remove arena and return to placement instructions
        cIScript.OnHomeButtonTap.AddListener(() => {
            GameObject.Destroy(placementArena);
            headposeMenus.SetActive(true);
            headposeMenus.transform.GetChild(0).Find("PlacementInstructionsPage")?.gameObject.SetActive(true);
            cIScript.OnTriggerDown.RemoveAllListeners();
            cIScript.OnHomeButtonTap.RemoveAllListeners();
        });
    }

    /// <summary>
    /// Called by the game owner after setting the rules. Spawns the Arena and Ball
    /// </summary>
    public void CreateGame()
    {
        //Update state
        gameOwner = true;
        //Enable Transmission
        transmissionComp.gameObject.SetActive(true);
        //Prepare to handle connection event
        transmissionComp.OnPeerFound.AddListener(OnJoinGame);
        //Spawn Transmission Arena
        Transform arenaTransform = placementArena.transform;
        GameObject.Destroy(placementArena);
        arena = Transmission.Spawn(arenaPrefabPath, arenaTransform.position, arenaTransform.rotation, arenaTransform.localScale);
        arena.transform.SetParent(GameObject.Find("[_DYNAMIC]").transform);
        //Spawn Transmission ball
        ball = Transmission.Spawn(ballPrefabPath, arenaTransform.position + new Vector3(0, 1.25f, 0), Quaternion.identity, Vector3.one);
        ball.transform.SetParent(GameObject.Find("[_DYNAMIC]").transform);
        ApplySettings();
    }

    /// <summary>
    /// Undos create Game. Removes Transmission objects and disables transmission
    /// </summary>
    public void CancelCreateGame()
    {
        //Update state
        gameOwner = false;
        //Unsubscribe from connection event
        transmissionComp.OnPeerFound.RemoveListener(OnJoinGame);
        //Despawn transmission objects
        ball.Despawn();
        placementArena = GameObject.Instantiate(placementArenaPrefab, arena.transform.position, arena.transform.rotation, GameObject.Find("[_DYNAMIC]").transform);
        arena.Despawn();
        //Disable transmission
        transmissionComp.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when the player selects "Join Game"
    /// </summary>
    public void TryJoinGame()
    {
        transmissionComp.gameObject.SetActive(true);
        transmissionComp.OnPeerFound.AddListener(OnJoinGame);
    }

    /// <summary>
    /// Called when player cancels joining a game
    /// </summary>
    public void CancelJoinGame()
    {
        transmissionComp.OnPeerFound.RemoveListener(OnJoinGame);
        transmissionComp.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called when player confirms readiness (that they can see the ball and arena) to play.
    /// Spawns the player's own paddle, and then gets ready to respond to the other player
    /// also confirming readiness.
    /// </summary>
    public void ConfirmReadiness()
    {
        //Spawn transmission object paddle
        paddle = Transmission.Spawn(paddlePrefabPath, pointer.transform.position, pointer.transform.rotation, Vector3.one);
        paddle.ownershipLocked = true;
        paddle.transform.SetParent(GameObject.Find("[_DYNAMIC]").transform);
        paddle.transform.localScale = new Vector3(0.02f, 0.15f, 0.25f);
        //Make it move with the controller
        ParentConstraint paddlePC = paddle.GetComponent<ParentConstraint>();
        ConstraintSource pointerSource = new ConstraintSource();
        pointerSource.sourceTransform = pointer.transform;
        pointerSource.weight = 1f;
        paddlePC.locked = false;
        paddlePC.SetSource(0, pointerSource);
        paddlePC.translationOffsets[0] = pointer.transform.forward*0.25f;
        paddlePC.rotationOffsets[0] = Vector3.zero;
        paddlePC.locked = true;
        paddlePC.constraintActive = true;

        //Disable the pointer
        pointer.transform.GetChild(0).gameObject.SetActive(false);
        pointer.GetComponent<LineRenderer>().enabled = false;
        pointer.enabled = false;

        //Start listening for bool changes to check if both players are ready
        transmissionComp.OnGlobalBoolChanged.AddListener(CheckReadiness);

        //Set applicable bool
        if (gameOwner)
            Transmission.SetGlobalBool("HostReady", true);
        else
            Transmission.SetGlobalBool("PeerReady", true);
    }

    /// <summary>
    /// Called when both players have confirmed their readiness. Closes the menu,
    /// and sets up Transmission Events and variables
    /// </summary>
    public void StartGame()
    {
        //Close the ConfirmReadinessPage
        headposeMenus.transform.GetChild(1).GetChild(0).gameObject.SetActive(false);

        //manage global variables and event subscriptions with Transmission
        transmissionComp.OnGlobalBoolChanged.RemoveListener(CheckReadiness);
        if(gameOwner)
        {
            Transmission.SetGlobalFloat("scoreRed", 0);
            Transmission.SetGlobalFloat("scoreBlue", 0);
            Transmission.SetGlobalBool("gamePaused", false);
        }
        transmissionComp.OnGlobalBoolChanged.AddListener(CheckPauseGame);
        transmissionComp.OnGlobalFloatChanged.AddListener(CheckScoreChange);
        transmissionComp.OnPeerLost.AddListener(HandlePeerLost);

        //Make the pause menu open on a home button press
        pointer.GetComponent<ControlInput>().OnHomeButtonTap.AddListener(TogglePauseGame);
    }

    public void ResetBall()
    {
        ball.GetComponent<Ball>().ResetBall(Ball.BallResetType.Neutral);
    }

    //Adjust values on prefab so all instantiated instances match, called after settings are edited and confirmed
    public void ApplySettings()
    {
        if(ball != null)
        {
            ball.transform.localScale = gameSettings.BallSize;
            ball.transform.GetComponent<SphereCollider>().material = gameSettings.BallBounciness;
        }
        ballPrefab.GetComponent<Collider>().material = gameSettings.BallBounciness;
        ballPrefab.transform.localScale = gameSettings.BallSize;

        //Update win Condition so peer can adjust its game settings
        Transmission.SetGlobalFloat("MaxScore", gameSettings.MaxScore);
    }

    /// <summary>
    /// Starts ingame arena replacement session
    /// </summary>
    /// <param name="returnMenu">menu gameobject to enable when canceling placement</param>
    public void InGameStartPlacement(GameObject returnMenu)
    {
        //Cache the last set position of the arena in case the placement is canceled
        Transform lastPosition = arena.transform;
        placementArena = GameObject.Instantiate(placementArenaPrefab, arena.transform.position, arena.transform.rotation, GameObject.Find("[_DYNAMIC]").transform);
        arena.Despawn();

        //Cache references
        Placement arenaPlc = placementArena.GetComponent<Placement>();
        ControlInput cIScript = pointer.GetComponent<ControlInput>();
        //Start the placement session, InGameOnPlacementConfirm is called when succesful placement occurs
        arenaPlc.Place(pointer.transform, new Vector3(3f, 2f, 2f), true, false, InGameOnPlacementConfirm);
        //When the user pulls the trigger, attempt placement confirmation.
        cIScript.OnTriggerDown.AddListener(() => {
            arenaPlc.Confirm();
            if (!arenaPlc.IsRunning)
            {
                cIScript.OnTriggerDown.RemoveAllListeners();
                cIScript.OnHomeButtonTap.RemoveAllListeners();
            }
        });
        //If home is pressed, remove arena and return to placement instructions
        cIScript.OnHomeButtonTap.AddListener(() => {
            GameObject.Destroy(placementArena);
            //place arena back in old spot
            arena = Transmission.Spawn(arenaPrefabPath, lastPosition.position, lastPosition.rotation, lastPosition.localScale);
            headposeMenus.SetActive(true);
            returnMenu.SetActive(true);
            cIScript.OnTriggerDown.RemoveAllListeners();
            cIScript.OnHomeButtonTap.RemoveAllListeners();
        });
    }

    /// <summary>
    /// Called when player exits from pause menu; despawns transmission objects and goes back to opening menu
    /// </summary>
    public void ExitGame()
    {
        TransmissionObject[] tObjects = GameObject.FindObjectsOfType<TransmissionObject>();
        foreach (TransmissionObject to in tObjects)
        {
            to.Despawn();
        }

        //remove transmission objects specific to the owner
        if (gameOwner)
        {
            Transmission.SetGlobalBool("gamePaused", true);
            gameOwner = false;
        }

        //Remove event subscriptions and disable Transmission
        transmissionComp.OnGlobalFloatChanged.RemoveListener(CheckPauseGame);
        transmissionComp.OnGlobalFloatChanged.RemoveListener(CheckScoreChange);
        transmissionComp.OnPeerLost.RemoveListener(HandlePeerLost);
        transmissionComp.gameObject.SetActive(false);

        //Reset menus to welcome screen, unregister hometap event
        headposeMenus.transform.GetChild(1).GetChild(0).gameObject.SetActive(true);
        headposeMenus.transform.GetChild(1).gameObject.SetActive(false);
        headposeMenus.transform.GetChild(0).gameObject.SetActive(true);
        headposeMenus.transform.GetChild(0).GetChild(0).gameObject.SetActive(true);
        pointer.GetComponent<ControlInput>().OnHomeButtonTap.RemoveListener(TogglePauseGame);
    }

    public void ResetGame()
    {
        //disable pointer
        pointer.transform.GetChild(0).gameObject.SetActive(false);
        pointer.GetComponent<LineRenderer>().enabled = false;
        pointer.enabled = false;
        //enable components
        paddle.gameObject.SetActive(true);
        if (gameOwner)
        {
            ResetBall();
            Transmission.SetGlobalFloat("scoreRed", 0);
            Transmission.SetGlobalFloat("scoreBlue", 0);
            Transmission.SetGlobalBool("gamePaused", false);
        }
    }

    /// <summary>
    /// Called by keyboard shortcut to fake connecting to a peer
    /// </summary>
    public void FakeConnection()
    {
        OnJoinGame("null", 0);
    }
    #endregion //Public Functions

    #region Event Handlers
    /// <summary>
    /// Called by Placement when Confirm is called successfully.
    /// </summary>
    /// <param name="pos">final placement location</param>
    /// <param name="rot">final placement rotation</param>
    private void OnPlacementConfirm(Vector3 pos, Quaternion rot)
    {
        headposeMenus.SetActive(true);
        GameObject confirmPage = headposeMenus.transform.GetChild(0).Find("PlacementConfirmationPage")?.gameObject;
        if (!confirmPage)
            Debug.LogWarning("Could not find placement confirmation page");
        confirmPage.SetActive(true);
    }

    private void InGameOnPlacementConfirm(Vector3 pos, Quaternion rot)
    {
        headposeMenus.SetActive(true);
        GameObject confirmPage = headposeMenus.transform.GetChild(1).Find("InGameConfrimPage")?.gameObject;
        if (!confirmPage)
            Debug.LogWarning("Could not find placement confirmation page");
        confirmPage.SetActive(true);
    }

    /// <summary>
    /// Called when a peer is found
    /// </summary>
    /// <param name="peerLabel"></param>
    private void OnJoinGame(string peerLabel, long num)
    {
        //Notify other scripts of connection
        ConnectionEvent.Invoke(true);
        //Reset and disable start menus
        foreach(Transform page in headposeMenus.transform.GetChild(0))
        {
            if(page.name == "WelcomePage")
            {
                page.gameObject.SetActive(true);
            }
            else
            {
                page.gameObject.SetActive(false);
            }
        }
        headposeMenus.transform.GetChild(0).gameObject.SetActive(false);
        //Enable in game menus, show confirmation screen
        headposeMenus.transform.GetChild(1).gameObject.SetActive(true);
    }

    /// <summary>
    /// When a global bool updates, it checks if it is both players signalling readiness to begin
    /// </summary>
    /// <param name="key">What bool was changed</param>
    private void CheckReadiness(string key)
    {
        if (Transmission.HasGlobalBool("HostReady") && Transmission.HasGlobalBool("PeerReady") &&
            Transmission.GetGlobalBool("HostReady") && Transmission.GetGlobalBool("PeerReady"))
            StartGame();
    }

    /// <summary>
    /// Called when a Transmission global float changes, if it was a score, then it checks if it meets the win condition
    /// </summary>
    /// <param name="key"></param>
    private void CheckScoreChange(string key)
    {
        if((key == "scoreRed" || key == "scoreBlue") && Transmission.GetGlobalFloat(key) >= gameSettings.MaxScore)
        {
            //disable paddle
            paddle.gameObject.SetActive(false);
            //enable pointer
            pointer.enabled = true;
            pointer.GetComponent<LineRenderer>().enabled = true;
            pointer.transform.GetChild(0).gameObject.SetActive(true);
            gameOverMenu.SetActive(true);
            if (key == "scoreRed" && Transmission.GetGlobalFloat(key) == gameSettings.MaxScore)
            {
                foreach (Transform t in gameOverMenu.transform)
                {
                    if (t.name == "RedText")
                        t.gameObject.SetActive(true);
                    else if(t.name == "BlueText")
                        t.gameObject.SetActive(false);
                }
            }
            else if (key == "scoreBlue" && Transmission.GetGlobalFloat(key) == gameSettings.MaxScore)
            {
                foreach (Transform t in gameOverMenu.transform)
                {
                    if (t.name == "BlueText")
                        t.gameObject.SetActive(true);
                    else if(t.name == "RedText")
                        t.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Sets global pause state, called by Home Button tap event
    /// </summary>
    public void TogglePauseGame()
    {
        if (Transmission.HasGlobalBool("gamePaused"))
        {
            Transmission.SetGlobalBool("gamePaused", !Transmission.GetGlobalBool("gamePaused"));
        }
    }

    /// <summary>
    /// Called when global bool changes, updates objects if it was the gamePaused state bool
    /// </summary>
    private void CheckPauseGame(string key)
    {
        if(key == "gamePaused")
        {
            //If the game became paused:
            if (Transmission.GetGlobalBool(key))
            {
                //Enable pause menus, freeze ball if the owner
                pauseMenu.SetActive(true);
                if (!gameOwner)
                    pauseMenu.GetComponent<PauseUI>().DisableOwnerButtons();
                else
                {
                    pauseMenu.GetComponent<PauseUI>().EnableOwnerButtons();
                }
                //disable paddle
                paddle.gameObject.SetActive(false);
                //enable pointer
                pointer.enabled = true;
                pointer.GetComponent<LineRenderer>().enabled = true;
                pointer.transform.GetChild(0).gameObject.SetActive(true);
            }
            //If game became unpaused
            else if(!Transmission.GetGlobalBool(key))
            {
                //Disable menus
                foreach (Transform t in pauseMenu.transform.parent)
                {
                    t.gameObject.SetActive(false);
                }
                //disable pointer
                pointer.transform.GetChild(0).gameObject.SetActive(false);
                pointer.GetComponent<LineRenderer>().enabled = false;
                pointer.enabled = false;
                //enable components
                paddle.gameObject.SetActive(true);
            }
        }
    }

    private void HandlePeerLost(string peerLabel)
    {
        if (transmissionComp.Peers.Length == 0)
            ExitGame();
    }
    #endregion //Event Handlers
}
