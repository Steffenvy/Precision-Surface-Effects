﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This is used when you want to attach a CollisionSounds to a child collider of a rigidbody.
//It won't receive any collision callbacks, so this is the way to get different CollisionEffects variants among the colliders.

namespace PrecisionSurfaceEffects
{
    [DisallowMultipleComponent]
    public class CollisionEffectsMaker : MonoBehaviour
    {
        //Fields
        [Tooltip("If bigger than a colliding CollisionEffects, it will play instead of it")]
        public int priority;

        internal bool stayFrameBool;


        //Methods
        protected bool FrameBool()
        {
            return Time.frameCount % 2 == 0;
        }
    }

    public class CollisionEffectsParent : CollisionEffectsMaker
    {
        //Fields
        public int defaultType = -1;
        public Type[] types;


        //Datatypes
        [System.Serializable]
        public class Type
        {
            public Collider[] colliders;
            public CollisionEffects collisionEffects;
        }


        //Lifecycle
#if UNITY_EDITOR
        private void OnValidate()
        {
            defaultType = Mathf.Clamp(defaultType, -1, types.Length - 1);

            var rb = GetComponent<Rigidbody>();

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];

                for (int ii = 0; ii < t.colliders.Length; ii++)
                {
                    var c = t.colliders[ii];
                    if (c.attachedRigidbody != rb)
                    {
                        t.colliders[ii] = null;

                        Debug.Log(c.gameObject.name + " is not a part of the Rigidbody: " + rb.gameObject.name);
                    }
                }
            }
        }
#endif

        private void OnCollisionEnter(Collision collision)
        {
            stayFrameBool = FrameBool();

            var thisCollider = collision.GetContact(0).thisCollider;

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];

                for (int ii = 0; ii < t.colliders.Length; ii++)
                {
                    if (t.colliders[ii] == thisCollider)
                    {
                        t.collisionEffects.OnCollisionEnter(collision);

                        return;
                    }
                }
            }

            if(defaultType != -1)
            {
                types[defaultType].collisionEffects.OnCollisionEnter(collision);
                return;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            var thisCollider = collision.GetContact(0).thisCollider;

            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];

                for (int ii = 0; ii < t.colliders.Length; ii++)
                {
                    if (t.colliders[ii] == thisCollider)
                    {
                        t.collisionEffects.OnOnCollisionStay(collision);

                        return;
                    }
                }
            }

            if (defaultType != -1)
            {
                types[defaultType].collisionEffects.OnOnCollisionStay(collision);
                return;
            }
        }
    }
}