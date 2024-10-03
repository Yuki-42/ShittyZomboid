using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Controls text in debug UI.
    /// </summary>
    public class DebugUiController : MonoBehaviour
    {
        [Header("Configuration")] 
        public int roundingFactor = 3;
        
        [Header("Components")] 
        public Canvas canvas;

        [Space(2)] 
        public TextMeshProUGUI positionDisplay;
        public TextMeshProUGUI velocityDisplay;
        
        private Vector3 _playerPosition;
        private Vector3 _previousPlayerPosition = Vector3.zero;
        private Vector3 _velocity;
        private Vector3 _playerFacing;
        
        // Display components
        
        
        private void FixedUpdate()
        {
            if (!canvas.enabled) return;
            
            // Do position display
            _playerPosition = transform.position;
            positionDisplay.text = $"{MathF.Round(_playerPosition.x, roundingFactor)}, {MathF.Round(_playerPosition.y, roundingFactor)}, {MathF.Round(_playerPosition.z, roundingFactor)}";
            
            // Do velocity display
            _velocity = _playerPosition - _previousPlayerPosition;
            velocityDisplay.text = $"{MathF.Round(_velocity.x, roundingFactor)}, {MathF.Round(_velocity.y, roundingFactor)}, {MathF.Round(_velocity.z, roundingFactor)}";
            
            // Do final assignments
            _previousPlayerPosition = _playerPosition;
        }
    }
}