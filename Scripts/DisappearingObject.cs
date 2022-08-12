
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace TimeControl
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DisappearingObject : UdonSharpBehaviour
    {
        public float stayActiveForSeconds = 3.0f;
        private float _startTime = 0;

        void Start()
        {
            _startTime = Time.time;
        }

        void OnEnable(){
            _startTime = Time.time;
        }

        void Update(){
            //disable own object if the configured time has passed
            if( Time.time - _startTime >= stayActiveForSeconds ){
                gameObject.SetActive(false);
            }
        }
    }
}