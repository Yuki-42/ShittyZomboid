using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class SceneGlobals : MonoBehaviour
{
    /// <summary>
    /// Current state of game. Only applicable to single player game scenes.
    /// </summary>
    public bool paused = false;

    /// <summary>
    /// Is the current game multiplayer?
    /// </summary>
    public bool multiplayer = false;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
