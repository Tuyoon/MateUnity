//#define POOLMANAGER

using UnityEngine;
using System.Collections;

/*
Basic Framework would be something like this:
    protected override void OnDespawned() {
        //reset stuff here

        base.OnDespawned();
    }

    protected override void OnDestroy() {
        //dealloc here

        base.OnDestroy();
    }

    protected override void SpawnStart() {
         //start ai, player control, etc
    }

    protected override void Awake() {
        base.Awake();

        //initialize variables
    }

    // Use this for initialization
    protected override void Start() {
        base.Start();

        //initialize variables from other sources (for communicating with managers, etc.)
    }
*/

namespace M8 {
    [AddComponentMenu("M8/Entity/EntityBase")]
    public class EntityBase : MonoBehaviour {
        public const int StateInvalid = -1;

        public delegate void OnGenericCall(EntityBase ent);
        public delegate void OnSetBool(EntityBase ent, bool b);

        public float spawnDelay = 0.0f;

        /// <summary>
        /// if we want FSM/other stuff to activate on start (when placing entities on scene).
        /// Set this to true if the entity is not gonna be spawned via Pool
        /// </summary>
        public bool activateOnStart = false;

        public EntityActivator activator;

        public event OnGenericCall setStateCallback;
        public event OnGenericCall spawnCallback; //called after a slight delay during OnSpawned (at least after one fixed-update)
        public event OnGenericCall releaseCallback;

        private int mState = StateInvalid;
        private int mPrevState = StateInvalid;

        private bool mDoSpawnOnWake = false;

        private byte mStartedCounter = 0;

        private SceneSerializer mSerializer = null;

        public static T Spawn<T>(string spawnGroup, string typeName, Vector3 position) where T : EntityBase {
#if POOLMANAGER
        SpawnPool pool = PoolManager.Pools[spawnGroup];

        Transform spawned = pool.Spawn(pool.prefabs[typeName]);
        T ent = spawned != null ? spawned.GetComponent<T>() : null;

        if(ent != null) {
            ent.transform.position = position;

            //add pool data controller
        }

        return ent;
#else
            Transform spawned = PoolController.Spawn(spawnGroup, typeName, typeName, null, position);
            T ent = spawned != null ? spawned.GetComponent<T>() : null;

            return ent;
#endif
        }

        private PoolDataController mPoolData;
        protected PoolDataController poolData {
            get {
                if(mPoolData == null) mPoolData = GetComponent<PoolDataController>();
                return mPoolData;
            }
        }

        public string spawnType {
            get {
                if(poolData == null) {
                    return name;
                }

                return poolData.factoryKey;
            }
        }

        /// <summary>
        /// Set by call from Spawn, so use that instead of directly spawning via pool manager.
        /// </summary>
        public string spawnGroup { get { return poolData == null ? "" : poolData.group; } }

        ///<summary>entity is instantiated with activator set to disable on start, call spawn after activator sends a wakeup call.</summary>
        public bool doSpawnOnWake {
            get { return mDoSpawnOnWake; }
        }

        public int state {
            get { return mState; }

            set {
                if(mState != value) {
                    if(mState != StateInvalid)
                        mPrevState = mState;

                    mState = value;

                    if(setStateCallback != null) {
                        setStateCallback(this);
                    }

                    StateChanged();
                }
            }
        }

        public int prevState {
            get { return mPrevState; }
        }

        public bool isReleased {
            get {
#if POOLMANAGER
            return !PoolManager.Pools[mPoolData.group].IsSpawned(transform);
#else
                if(poolData == null)
                    return false;

                return poolData.claimed;
#endif
            }
        }

        public SceneSerializer serializer {
            get { return mSerializer; }
        }

        /// <summary>
        /// Manually perform start if it hasn't fully started yet.
        /// </summary>
        public virtual void Activate() {
            if(mStartedCounter < 2) {
                StopAllCoroutines();
                StartCoroutine(DoStart());
            }
        }

        /// <summary>
        /// Explicit call to remove entity.
        /// </summary>
        public virtual void Release() {
            if(poolData != null) {
#if POOLMANAGER
            transform.parent = PoolManager.Pools[poolData.group].group;
            PoolManager.Pools[poolData.group].Despawn(transform);
#else
                PoolController.ReleaseByGroup(poolData.group, transform);
#endif
            }
            else {
                //just disable the object, really no need to destroy
                OnDespawned();
                gameObject.SetActive(false);
                /*
                if(gameObject.activeInHierarchy)
                    StartCoroutine(DestroyDelay());
                else {
                    Destroy(gameObject);
                }*/
            }
        }

        protected virtual void OnDespawned() {
            if(activator != null && activator.defaultParent == transform) {
                activator.Release(false);
            }

            if(releaseCallback != null) {
                releaseCallback(this);
            }

            mDoSpawnOnWake = false;

            StopAllCoroutines();
        }

        protected virtual void ActivatorWakeUp() {
            if(!gameObject.activeInHierarchy)
                return;

            if(activateOnStart && mStartedCounter == 1) { //we didn't get a chance to start properly before being inactivated
                StartCoroutine(DoStart());
            }
            else if(mDoSpawnOnWake) { //if we haven't properly spawned yet, do so now
                mDoSpawnOnWake = false;
                StartCoroutine(DoSpawn());
            }
        }

        protected virtual void ActivatorSleep() {
        }

        protected virtual void OnDestroy() {
            if(activator != null && activator.defaultParent == transform) {
                activator.Release(true);
            }

            setStateCallback = null;
            spawnCallback = null;
            releaseCallback = null;
        }

        protected virtual void OnEnable() {
            //we didn't get a chance to start properly before being inactivated
            if((activator == null || activator.isActive) && activateOnStart && mStartedCounter == 1) {
                StartCoroutine(DoStart());
            }
        }

        protected virtual void Awake() {
            mPoolData = GetComponent<PoolDataController>();

            mSerializer = GetComponent<SceneSerializer>();

            if(activator == null)
                activator = GetComponentInChildren<EntityActivator>();

            if(activator != null) {
                activator.awakeCallback += ActivatorWakeUp;
                activator.sleepCallback += ActivatorSleep;
            }
        }

        // Use this for initialization
        protected virtual void Start() {
            mStartedCounter = 1;

            //for when putting entities on scene, skip the spawning state
            if(activateOnStart) {
                StartCoroutine(DoStart());
            }
        }

        protected virtual void StateChanged() {
        }

        protected virtual void SpawnStart() {
        }

        //////////internal

        /// <summary>
        /// Spawn this entity, resets stats, set action to spawning, then later calls OnEntitySpawnFinish.
        /// NOTE: calls after an update to ensure Awake and Start is called.
        /// </summary>
        protected virtual void OnSpawned() {
            if(mPoolData == null)
                mPoolData = GetComponent<PoolDataController>();

            mState = mPrevState = StateInvalid; //avoid invalid updates

            //allow activator to start and check if we need to spawn now or later
            //ensure start is called before spawning if we are freshly allocated from entity manager
            if(activator != null) {
                activator.Start();

                if(activator.deactivateOnStart) {
                    mDoSpawnOnWake = true; //do it later when we wake up
                }
                else {
                    StartCoroutine(DoSpawn());
                }
            }
            else {
                StartCoroutine(DoSpawn());
            }
        }

        IEnumerator DoStart() {
            if(spawnDelay > 0.0f)
                yield return new WaitForSeconds(spawnDelay);
            else
                yield return null;

            SpawnStart();

            if(spawnCallback != null) {
                spawnCallback(this);
            }

            mStartedCounter = 2;
        }

        IEnumerator DoSpawn() {
            if(spawnDelay > 0.0f)
                yield return new WaitForSeconds(spawnDelay);
            else
                yield return null;

            SpawnStart();

            if(spawnCallback != null) {
                spawnCallback(this);
            }

            mStartedCounter = 2;
        }
    }
}