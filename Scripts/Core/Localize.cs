﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace M8 {
    [PrefabCore]
    [AddComponentMenu("M8/Core/Localize")]
    public class Localize : SingletonBehaviour<Localize> {
        public Language defaultLanguage = Language.English;

        public delegate string ParameterCallback(string paramKey);
        public delegate void LocalizeCallback();

        [System.Serializable]
        public class TableDataPlatform {
            public RuntimePlatform platform;
            public TextAsset file;
        }

        [System.Serializable]
        public class TableData {
            public Language language;
            public TextAsset file;
            public TableDataPlatform[] platforms; //these overwrite certain keys in the string table
        }

        public class Entry {
            public string key;
            public string text;
            public string[] param;
        }

        public TableData[] tables; //table info for each language

        public event LocalizeCallback localizeCallback;

        private Dictionary<string, string> mTable;
        private Dictionary<string, string[]> mTableParams;
        private bool mLoaded = false;

        private Language mCurLanguage = Language.English;

        private Dictionary<string, ParameterCallback> mParams = null;

        public Language language {
            get { return mCurLanguage; }
            set {
                if(mCurLanguage != value) {
                    mCurLanguage = value;
                    Load();
                }
            }
        }

        /// <summary>
        /// Register during Awake such that GetText will be able to fill params correctly
        /// </summary>
        public void RegisterParam(string paramKey, ParameterCallback cb) {
            if(mParams == null)
                mParams = new Dictionary<string, ParameterCallback>();

            if(mParams.ContainsKey(paramKey))
                mParams[paramKey] = cb;
            else
                mParams.Add(paramKey, cb);
        }

        /// <summary>
        /// Only call this after Load.
        /// </summary>
        public string GetText(string key) {
            string ret = "";

            if(mTable != null) {
                if(mTable.ContainsKey(key)) {
                    ret = mTable[key];

                    //see if there's params
                    string[] keyParams;

                    if(mTableParams.TryGetValue(key, out keyParams)) {
                        if(mParams != null) {
                            //convert parameters
                            string[] textParams = new string[keyParams.Length];
                            for(int i = 0; i < keyParams.Length; i++) {
                                ParameterCallback cb;
                                if(mParams.TryGetValue(keyParams[i], out cb)) {
                                    textParams[i] = cb(keyParams[i]);
                                }
                            }

                            ret = string.Format(ret, textParams);
                        }
                        else {
                            Debug.LogWarning("Parameters not initialized for: " + key);
                        }
                    }
                }
                else {
                    Debug.LogWarning("String table key not found: " + key);
                }
            }
            else {
                Debug.LogWarning("Attempting to access string table when not initialized! Key: " + key);
            }

            return ret;
        }

        public void Refresh() {
            if(mLoaded) {
                if(localizeCallback != null)
                    localizeCallback();
            }
        }

        void Load() {
            int langInd = (int)mCurLanguage;

            TableData dat = tables[langInd];

            if(dat.file) {
                fastJSON.JSON.Parameters.UseExtensions = false;
                List<Entry> tableEntries = fastJSON.JSON.ToObject<List<Entry>>(dat.file.text);

                mTable = new Dictionary<string, string>(tableEntries.Count);
                mTableParams = new Dictionary<string, string[]>(tableEntries.Count);

                foreach(Entry entry in tableEntries) {
                    mTable.Add(entry.key, entry.text);

                    if(entry.param != null && entry.param.Length > 0)
                        mTableParams.Add(entry.key, entry.param);
                }

                //append platform specific entries
                TableDataPlatform platform = null;
                foreach(TableDataPlatform platformDat in dat.platforms) {
                    if(platformDat.platform == Application.platform) {
                        platform = platformDat;
                        break;
                    }
                }

                //override entries based on platform
                if(platform != null) {
                    List<Entry> platformEntries = fastJSON.JSON.ToObject<List<Entry>>(platform.file.text);

                    foreach(Entry platformEntry in platformEntries) {
                        if(mTable.ContainsKey(platformEntry.key)) {
                            mTable[platformEntry.key] = platformEntry.text;
                        }
                    }
                }

                //already loaded before? then let everyone know it has changed
                if(mLoaded) {
                    if(localizeCallback != null)
                        localizeCallback();
                }
                else {
                    mLoaded = true;
                }
            }
            else
                Debug.LogWarning("File not found for language: " + mCurLanguage);
        }

        void Awake() {
            mCurLanguage = defaultLanguage;
            Load();
        }
    }
}