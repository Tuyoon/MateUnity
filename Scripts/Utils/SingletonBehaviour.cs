﻿using UnityEngine;
using System;

namespace M8 {
    /// <summary>
    /// Based on Kleber Lopes da Silva's solution:
    /// http://kleber-swf.com/singleton-monobehaviour-unity-projects/
    /// </summary>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour {
        private static T mInstance;
        private static bool mInstantiated;

        public static T instance {
            get {
                if(!mInstantiated) {
                    var type = typeof(T);
                    var attribute = Attribute.GetCustomAttribute(type, typeof(PrefabFromResourceAttribute)) as PrefabFromResourceAttribute;
                    if(attribute != null)
                        mInstance = attribute.Instantiate<T>();
                    else {
                        //manually grab
                        var objects = FindObjectsOfType<T>();

                        if(objects.Length > 0) {
                            mInstance = objects[0];
                            if(objects.Length > 1) {
                                Debug.LogWarning("There is more than one instance of Singleton of type \"" + type + "\". Keeping the first. Destroying the others.");
                                for(var i = 1; i < objects.Length; i++) DestroyImmediate(objects[i].gameObject);
                            }
                        }
                        else {
                            //just create a gameobject
                            GameObject go = new GameObject(type.ToString());
                            mInstance = go.AddComponent<T>();
                        }
                    }

                    mInstantiated = mInstance != null;
                }

                return mInstance;
            }
        }

        /// <summary>
        /// Use this during OnDestroy to prevent GameObject from being re-created (usu. when switching levels with non-persistent instances or stopping play mode in edit).
        /// </summary>
        public static bool instantiated { get { return mInstantiated; } }

        protected virtual void OnDestroy() {
            mInstance = null;
            mInstantiated = false;
        }
    }
}