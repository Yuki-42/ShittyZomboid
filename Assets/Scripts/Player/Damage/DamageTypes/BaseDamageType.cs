
using System;
using UnityEngine;

namespace Player.Damage.DamageTypes
{
    public class BaseDamageType : MonoBehaviour
    {
        /// <summary>
        /// Damage type name. Used in console.
        /// </summary>
        public readonly string Name;
        
        /// <summary>
        /// Damage type friendly name. Displayed to user in UI.
        /// </summary>
        public readonly string UIName;

        public BaseDamageType(string name, string uiName)
        {
            Name = name;
            UIName = uiName;
        }
    }
}