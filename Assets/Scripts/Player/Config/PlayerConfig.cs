using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Config
{
    public class PlayerConfig : MonoBehaviour
    {
        [Header("Camera Config")]
        public float firstPersonFov = 90;
        public float mouseSensitivity = 3;
        public bool invertYAxis = false;

        public bool updated = false;

        public float FirstPersonFov
        {
            get => firstPersonFov;
            set
            {
                firstPersonFov = value;
                updated = true;
            }
        }

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set
            {
                mouseSensitivity = value;
                updated = true;
            }
        }

        public bool InvertYAxis
        {
            get => invertYAxis;
            set
            {
                invertYAxis = value;
                updated = true;
            }
        }


        void Start()
        {

        }

        void Update()
        {

        }
    }
}