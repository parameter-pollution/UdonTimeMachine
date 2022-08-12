
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;
using UnityEngine.Animations;

namespace TimeControl
{
    [AddComponentMenu("TimeControl/DesktopHandUI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class DesktopHandUI : UdonSharpBehaviour
    {
        public TimeMachine timeMachine;
        public GameObject grip;
        public GameObject anker;
        public GameObject handUIInner;
        public GameObject handUIOuter;
        public GameObject handUIInactive;
        public GameObject playButton;
        public GameObject debugTracker;
        public GameObject errorMessage;
        public Text errorMessageText;
        public GameObject debugUI;
        public Text debugText;

        VRCPlayerApi _playerLocal;

        Vector3 _initialPickupPositionOffset = Vector3.zero;
        Quaternion _initialPickupRotation = Quaternion.identity;
        float _forearmLength = 0.0f;
        bool _isDesktopHandUIGestureActive = false;
        bool _isDesktopPlayer = false;

        float _initialRotationCalibrationEndTime = 0f;
        float _initialRotationCalibrationDelay = 1.0f;
        float _initialDistance = 0f;

        private ParentConstraint _tutorialUIConstraint;

        private bool _debug = false;

        
        HumanBodyBones RightHand = HumanBodyBones.RightHand;
        HumanBodyBones RightElbow = HumanBodyBones.RightLowerArm;
        HumanBodyBones Head = HumanBodyBones.Head;

        void Start()
        {
            _playerLocal = Networking.LocalPlayer;
            if( !_playerLocal.IsUserInVR() ) _isDesktopPlayer = true;
            _tutorialUIConstraint = gameObject.GetComponent<ParentConstraint>();
        }

        void Update()
        {
            //after pickup it takes some time for everything to settle in => update initial values during this calibration phase
            if( Time.time < _initialRotationCalibrationEndTime ){
                _initialPickupPositionOffset = grip.transform.position - _playerLocal.GetBonePosition(RightHand);
                _initialPickupRotation = _playerLocal.GetBoneRotation(RightHand); 
                _initialDistance = GetGripDistance();
            }

            Vector3 ankerPosition = GetAnkerPosition();

            if( _isDesktopPlayer && _isDesktopHandUIGestureActive ){

                float distance = GetGripDistance() - _initialDistance;

                if(timeMachine.IsThereCurrentlyATimelord() && timeMachine.AmITheCurrentTimelord()){
                    if(_debug) debugText.text = distance.ToString("F5");
                    if( Mathf.Abs(distance) < 0.01f ) distance = 0.0f;
                    float timeLineScrubSpeed = distance * distance * distance * 100f;
                    bool success = timeMachine.SeekTo( timeMachine.GetCurrentLocalTimelineTime() + timeLineScrubSpeed * Time.deltaTime );
                    if(success){
                        anker.transform.position = ankerPosition;
                        handUIInner.transform.localRotation = Quaternion.AngleAxis(distance * Mathf.Abs(distance) * 1000f, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.right);
                    }else{
                        //timeline limit reached if timeline loop mode is off
                        ResetUI();
                    }
                }else{
                    _isDesktopHandUIGestureActive = false;
                }

            }
            debugTracker.transform.SetPositionAndRotation( ankerPosition , _playerLocal.GetBoneRotation(RightHand));
        }

        private float GetGripDistance(){
            return Vector3.Distance(grip.transform.position, _playerLocal.GetBonePosition(Head));
        }

        private Vector3 GetAnkerPosition(){
            return _playerLocal.GetBonePosition(RightHand) + ( _playerLocal.GetBoneRotation(RightHand) * Quaternion.Inverse(_initialPickupRotation) ) * _initialPickupPositionOffset;
        }

        public void OnPickup(){
            //tell calibration system to start initial position/rotation calibration
            _initialRotationCalibrationEndTime = Time.time + _initialRotationCalibrationDelay;
            //get forearm length so we can use it for all scale related calculations
            _forearmLength = Vector3.Distance( _playerLocal.GetBonePosition(RightHand), _playerLocal.GetBonePosition(RightElbow) );
            _tutorialUIConstraint.constraintActive = false;
        }

        public void OnDrop(){
            _isDesktopHandUIGestureActive = false;
            if( timeMachine.StepDownAsTimelord() ){
                timeMachine.OnLocalTimelineScrubStop();
            }

            //show play button above the circle UI
            playButton.transform.position = anker.transform.position + new Vector3(0f, 0.25f, 0f);
            playButton.transform.LookAt( _playerLocal.GetBonePosition(HumanBodyBones.Head) );
            playButton.SetActive(true);

            ResetUI();
            ResetUICompletely();
            SetUIActive(false);
        }

        public void OnPickupUseDown(){
            ResetUI();
            if( _isDesktopHandUIGestureActive ){
                //it is active => deactivate
                if( timeMachine.StepDownAsTimelord() ){
                    timeMachine.OnLocalTimelineScrubStop();
                }
                _isDesktopHandUIGestureActive = false;
                SetUIActive(false);
            }else{
                //it is not active => activate it
                //check if we can become timelord
                if( timeMachine.BecomeTimelord() ){
                    timeMachine.Pause();
                    timeMachine.OnLocalTimelineScrubStart();
                    _isDesktopHandUIGestureActive = true;
                    
                    SetUIActive(true);
                }else{
                    //somebody else is currently timelord => show error
                    string username = timeMachine.GetCurrentTimelordUsername();
                    if( username != null ){
                        errorMessageText.text = username;
                    }else{
                        errorMessageText.text = "Error getting username!";
                    }
                    errorMessage.SetActive(true);
                }
            }
        }

        private void SetUIActive(bool state){
            handUIInner.SetActive(state);
            handUIOuter.SetActive(state);
            handUIInactive.SetActive(!state);
        }

        private void ResetUI(){
            grip.transform.position = GetAnkerPosition();
            handUIInner.transform.localRotation = Quaternion.AngleAxis(0f, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.right);
        }

        private void ResetUICompletely(){
            gameObject.transform.position = GetAnkerPosition();
            grip.transform.localPosition = Vector3.zero;
            anker.transform.localPosition = Vector3.zero;
        }

        public void SetDebug(bool state){
            _debug = state;
            debugUI.SetActive(state);
            debugTracker.SetActive(state);
        }
        
    }
}
