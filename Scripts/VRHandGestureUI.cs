using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace TimeControl
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class VRHandGestureUI : UdonSharpBehaviour
    {
        public TimeMachine timeMachine;
        public float vrUIScaleFactor = 1;
        public GameObject forearmUI;
        public GameObject handUI;
        public GameObject handUIInner;
        public GameObject handUIOuter;
        public GameObject vrPlayButton;
        public GameObject errorMessage;
        public Text errorMessageText;
        public GameObject debugUI;
        public Text[] debugText;
        public GameObject[] debugTracker;

        //Arm "enum"
        //udonsharp doesn't support enums (and static fields) yet
        int _NO_ARM = 0;
        int _LEFT_ARM = 1;
        int _RIGHT_ARM = 2;
        int _ANY_ARM = 3;

        //for input smoothing
        float[] _rotationBuffer = {0,0,0,0,0,0,0,0,0,0};
        int _currentRotationBufferIndex = 0;

        //track gesture state
        bool _VR_UI_ACTIVE = true;
        bool _VR_UI_INACTIVE = false;
        bool _lastVRUIState = false; //can't set this to _VR_UI_INACTIVE because _VR_UI_INACTIVE isn't a static field because udonsharp doesn't support that yet
        int _lastActiveArm = 0;
        //track initial hand rotation
        float _initialRotation = 0f;
        float _initialRotationCalibrationEndTime = 0f;
        float _initialRotationCalibrationDelay = 0.3f;
        Vector3 _lastPlayerLocation = new Vector3(0f,0f,0f);

        VRCPlayerApi _playerLocal;
        
        private float _deadRotationAngleToEachSide = 4f;

        private bool _debug = false;

        void Start()
        {
            _playerLocal = Networking.LocalPlayer;
        }

        void Update(){
            //check if we are even allowed to be the timelord
            if( !timeMachine.IsThereCurrentlyATimelord() || timeMachine.AmITheCurrentTimelord() ){
                //check if the local player is doing the VR UI gesture
                int activeArm;
                if( _lastVRUIState == _VR_UI_ACTIVE ){
                    activeArm = IsHandGestureActive(_GESTURE_RELAXED_THRESHOLD, _playerLocal, _ANY_ARM); 
                }else{
                    activeArm = IsHandGestureActive(_GESTURE_STRICT_THRESHOLD, _playerLocal, _ANY_ARM); 
                }
                if( activeArm != _NO_ARM  ){
                    //become the new time lord if possible
                    if( timeMachine.BecomeTimelord() ){
                    
                        float rawHandRotationAngle = GetHandRotation(_playerLocal, activeArm);
                        float smoothedRotation = GetSmoothedValue(rawHandRotationAngle);

                        //vr gesture was just enalbed => show VR UI
                        if( _lastVRUIState == _VR_UI_INACTIVE ){
                            _initialRotationCalibrationEndTime = Time.time + _initialRotationCalibrationDelay;

                            VibrateController(activeArm, 0.1f, 0.5f, 1000f);
                            
                            timeMachine.Pause();
                            timeMachine.OnLocalTimelineScrubStart(); 
                        }

                        //keep updating the initial rotation until the initial rotation calibration time is over
                        if( Time.time < _initialRotationCalibrationEndTime ){
                            _initialRotation = smoothedRotation;
                        }

                        float angle = smoothedRotation - _initialRotation;
                        if( Mathf.Abs(angle) <= _deadRotationAngleToEachSide ){
                            angle = 0f;
                        }else{
                            if(angle > 0f) angle = angle - _deadRotationAngleToEachSide;
                            else angle = angle + _deadRotationAngleToEachSide;
                        }
                        bool success = timeMachine.SeekTo( CalculateNewTimelineTimestamp(angle) );
                        if(!success){
                            angle = 0;
                            VibrateController(activeArm, 0.1f, 0.5f, 1000f);
                        }
                        DrawVRUI(_playerLocal,activeArm, angle);
                    }
                }else{
                    //gesture moved to disabled state
                    if(_lastVRUIState == _VR_UI_ACTIVE){
                        //hide VR UI
                        HideVRUI();

                        //give haptic feedback
                        VibrateController(_lastActiveArm, 0.1f, 0.5f, 1000.0f);
                        
                        ShowPlayButton(_lastActiveArm);
                        //inform time machine that we are done with timeline scrubbing
                        timeMachine.OnLocalTimelineScrubStop();
                        timeMachine.StepDownAsTimelord();
                    }
                    //_lastVRUIState = _VR_UI_INACTIVE;
                }

            }else  if( timeMachine.IsThereCurrentlyATimelord() ){
                //somebody else is currently the timelord
                VRCPlayerApi currentTimelord = timeMachine.GetCurrentTimelord();
                if( currentTimelord != null ){
                    //check if the current timelord is doing the VR UI gesture, but use the relaxed thresholds (in case of discrepancies because of bone data sync via network)
                    int activeArm = IsHandGestureActive(_GESTURE_RELAXED_THRESHOLD, currentTimelord, _ANY_ARM);
                    if( activeArm != _NO_ARM ){
                        float handRotation = GetHandRotation(currentTimelord, activeArm);
                        DrawVRUI(currentTimelord, activeArm, handRotation);
                    }else{
                        HideVRUI();
                    }

                    //check if the local player is doing the hand gesture and show them the error message if they are
                    activeArm = IsHandGestureActive(_GESTURE_STRICT_THRESHOLD, _playerLocal, _ANY_ARM);
                    if( activeArm != _NO_ARM ){
                        //vr gesture was detected, but somebody else is currently timelord => show error
                        if(!errorMessage.activeSelf){
                            //only show error if it's not already active
                            ShowErrorMessage(activeArm);
                        }
                    }
                }else{
                    HideVRUI();
                }
            }else{
                //nobody is currently timelord
                HideVRUI();
            }
            if(_debug) UpdateDebugTrackers(_playerLocal,_RIGHT_ARM);
        }

        private void VibrateController(int arm, float duration, float amplitude, float frequency){
            //PlayHapticEventInHand(PickupHand hand, float duration, float amplitude, float frequency)
            if( arm == _RIGHT_ARM ){
                _playerLocal.PlayHapticEventInHand(VRC.SDKBase.VRC_Pickup.PickupHand.Right, duration, amplitude, frequency);
            }else{
                _playerLocal.PlayHapticEventInHand(VRC.SDKBase.VRC_Pickup.PickupHand.Left, duration, amplitude, frequency);
            }            
        }

        private void HideVRUI(){
            handUI.SetActive(false);
            forearmUI.SetActive(false);
            _lastVRUIState = _VR_UI_INACTIVE;
        }

        private void ShowPlayButton(int arm){
            //show play button and put it in front of the hand on the forearm axis
            vrPlayButton.transform.position = handUI.transform.position + GetForearmVector(_playerLocal, arm);
            HumanBodyBones elbow = HumanBodyBones.RightLowerArm;
            if( arm == _LEFT_ARM ) elbow = HumanBodyBones.LeftLowerArm;
            Vector3 elbowPosition = _playerLocal.GetBonePosition(elbow);
            if( vrPlayButton.transform.position.y < elbowPosition.y ) vrPlayButton.transform.position = new Vector3(vrPlayButton.transform.position.x,elbowPosition.y,vrPlayButton.transform.position.z);
            vrPlayButton.transform.LookAt( _playerLocal.GetBonePosition(HumanBodyBones.Head) );
            vrPlayButton.SetActive(true);
        }

        private void ShowErrorMessage(int arm){
            string username = timeMachine.GetCurrentTimelordUsername();
            if( username != null ){
                errorMessageText.text = username;
            }else{
                errorMessageText.text = "Error getting Username!";
            }
            HumanBodyBones HAND;
            if( arm == _RIGHT_ARM ){
                HAND = HumanBodyBones.RightHand;
                _playerLocal.PlayHapticEventInHand(VRC.SDKBase.VRC_Pickup.PickupHand.Right, 0.2f, 0.7f, 500f);
            }else{
                HAND = HumanBodyBones.LeftHand;
                _playerLocal.PlayHapticEventInHand(VRC.SDKBase.VRC_Pickup.PickupHand.Left, 0.2f, 0.7f, 500f);
            }
            Vector3 forearmVector = GetForearmVector(_playerLocal, arm);
            Vector3 hand = _playerLocal.GetBonePosition(HAND);
            errorMessage.transform.position = hand + forearmVector/1.5f;
            errorMessage.transform.LookAt( _playerLocal.GetBonePosition(HumanBodyBones.Head) );
            float messageScale = forearmVector.magnitude * vrUIScaleFactor * 4f;
            errorMessage.transform.localScale = new Vector3(messageScale,messageScale,messageScale);
            errorMessage.SetActive(true);
        }

        private double CalculateNewTimelineTimestamp(float timeTuneAngle){
            if(_debug) debugText[5].text = timeTuneAngle.ToString("F2");

            //make it exponential instead of linear, so if you want to scroll faster you don't have to rotate your hand that much
            timeTuneAngle = timeTuneAngle * Mathf.Abs(timeTuneAngle) / 10.0f;
            return timeMachine.GetCurrentLocalTimelineTime() + 0.01f * timeTuneAngle * Time.deltaTime;
        }

        private float GetSmoothedValue(float newValue){
            _rotationBuffer[_currentRotationBufferIndex] = newValue;
            _currentRotationBufferIndex++;
            if(_currentRotationBufferIndex == 10) _currentRotationBufferIndex = 0;
            float smoothedRotation = 0.0f;
            for(int i=0; i<10; i++){
                smoothedRotation += _rotationBuffer[i];
            }
            return smoothedRotation/10.0f;
        }

        private Vector3 GetHandVector(VRCPlayerApi player, int arm){
            Vector3 indexFingerBasePosition;
            Vector3 handPosition;
            if( arm == _RIGHT_ARM ){
                indexFingerBasePosition = player.GetBonePosition(HumanBodyBones.RightIndexProximal);
                handPosition = player.GetBonePosition(HumanBodyBones.RightHand);
            }else{
                indexFingerBasePosition = player.GetBonePosition(HumanBodyBones.LeftIndexProximal);
                handPosition = player.GetBonePosition(HumanBodyBones.LeftHand);
            }
            return indexFingerBasePosition - handPosition;
        }

        private Vector3 GetForearmVector(VRCPlayerApi player, int arm){
            Vector3 handPosition;
            Vector3 elbowPosition;
            if( arm == _RIGHT_ARM ){
                handPosition = player.GetBonePosition(HumanBodyBones.RightHand);
                elbowPosition = player.GetBonePosition(HumanBodyBones.RightLowerArm);
            }else{
                handPosition = player.GetBonePosition(HumanBodyBones.LeftHand);
                elbowPosition = player.GetBonePosition(HumanBodyBones.LeftLowerArm);
            }
            return handPosition - elbowPosition; 
        }

        private float GetHandRotation(VRCPlayerApi player, int arm){
            return AngleWhenProjectedOnPlane( Vector3.up, GetHandVector(player,arm), -GetForearmVector(player,arm));
        }

        private void DrawVRUI(VRCPlayerApi player, int arm, float rotationInput){
            //place wristband close to end of forearm
            Vector3 handPosition;
            Vector3 handVector = GetHandVector(player,arm);
            Vector3 indexBasePosition;
            Vector3 littleBasePosition;
            Vector3 thumbBasePosition;
            Vector3 elbowPosition;
            Quaternion forearmRotation;
            if( arm == _RIGHT_ARM ){
                handPosition = player.GetBonePosition(HumanBodyBones.RightHand);
                littleBasePosition = player.GetBonePosition(HumanBodyBones.RightLittleProximal);
                indexBasePosition = player.GetBonePosition(HumanBodyBones.RightIndexProximal);
                thumbBasePosition = player.GetBonePosition(HumanBodyBones.RightThumbProximal);
                elbowPosition = player.GetBonePosition(HumanBodyBones.RightLowerArm);
                forearmRotation = player.GetBoneRotation(HumanBodyBones.RightLowerArm);
            }else{
                handPosition = player.GetBonePosition(HumanBodyBones.LeftHand);
                littleBasePosition = player.GetBonePosition(HumanBodyBones.LeftLittleProximal);
                indexBasePosition = player.GetBonePosition(HumanBodyBones.LeftIndexProximal);
                thumbBasePosition = player.GetBonePosition(HumanBodyBones.LeftThumbProximal);
                elbowPosition = player.GetBonePosition(HumanBodyBones.LeftLowerArm);
                forearmRotation = player.GetBoneRotation(HumanBodyBones.LeftLowerArm);
            }
            forearmUI.transform.SetPositionAndRotation( Vector3.Lerp(elbowPosition, handPosition, 0.9f), forearmRotation);
            forearmUI.transform.Rotate(90f,0f,0f);

            //scale VR ui based on forearm length
            float foreArmLength = (handPosition - elbowPosition).magnitude;
            float vrUIScale = foreArmLength * vrUIScaleFactor * 4f;
            transform.localScale = new Vector3(vrUIScale,vrUIScale,vrUIScale);

            //get hand plane
            Vector3 handMiddlePoint = new Vector3( (thumbBasePosition.x+littleBasePosition.x+indexBasePosition.x)/3, (thumbBasePosition.y+littleBasePosition.y+indexBasePosition.y)/3, (thumbBasePosition.z+littleBasePosition.z+indexBasePosition.z)/3 );
            Vector3 handNormal = (new Plane(thumbBasePosition,littleBasePosition,indexBasePosition)).normal;
            if( arm == _LEFT_ARM ){
                handNormal *= -1;
            }
            Vector3 finalHandUIPosition = handMiddlePoint + handNormal * handVector.magnitude;
            Vector3 newHandUIPosition;
            //handUI.transform.position = handMiddlePoint + (handMiddlePoint - _humanBody_head_pos)/3;
            if( _lastVRUIState == _VR_UI_INACTIVE ){
                _lastPlayerLocation = player.GetPosition();

                newHandUIPosition = finalHandUIPosition;
            }else{
                Vector3 newPlayerPosition = player.GetPosition();

                Vector3 moveVector = finalHandUIPosition - handUI.transform.position;
                float movementSpeed = 4f;
                //if the hand UI is getting close to the hand, then speed up the movement so you can't touch it
                float distanceHandUIToHand = (handUI.transform.position - handMiddlePoint).magnitude;
                if( distanceHandUIToHand < handVector.magnitude/2f ){
                    movementSpeed = movementSpeed * handVector.magnitude / distanceHandUIToHand ;
                }
                //the more the angle of the hand UI plane is wrong compared to the hand plane the faster we move the position
                float angleHandPlaneToUI = Vector3.Angle(handNormal,finalHandUIPosition - handMiddlePoint);
                movementSpeed = movementSpeed * ( 1 + angleHandPlaneToUI / 90f );
                newHandUIPosition = handUI.transform.position + (newPlayerPosition - _lastPlayerLocation) + moveVector * Time.deltaTime * movementSpeed;
                _lastPlayerLocation = newPlayerPosition;
            }
            if(_debug) debugTracker[3].transform.SetPositionAndRotation(handMiddlePoint,  Quaternion.FromToRotation(Vector3.up, handNormal) );
            handUI.transform.position = newHandUIPosition;
            handUI.transform.LookAt( handMiddlePoint );

            handUIInner.transform.localRotation = Quaternion.AngleAxis(rotationInput, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.right);
            handUIOuter.transform.localRotation = Quaternion.AngleAxis(-rotationInput, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.right);
            
            handUI.SetActive(true);
            forearmUI.SetActive(true);
            if( player == _playerLocal ) errorMessage.SetActive(false);

            _lastVRUIState = _VR_UI_ACTIVE;
            _lastActiveArm = arm;
        }

        bool _GESTURE_RELAXED_THRESHOLD = true;
        bool _GESTURE_STRICT_THRESHOLD = false;
        private int IsHandGestureActive(bool thresholdType, VRCPlayerApi player, int arm){
            Vector3 handVector;
            Vector3 forearmVector;

            if( arm == _ANY_ARM ){
                //check right arm first
                arm = IsHandGestureActive(thresholdType, player, _RIGHT_ARM);
                if( arm == _RIGHT_ARM ){
                    return _RIGHT_ARM;
                }else{
                    //either return left arm or no arm
                    return IsHandGestureActive(thresholdType, player, _LEFT_ARM);
                }
            }
            
            handVector = GetHandVector(player, arm);
            forearmVector = GetForearmVector(player, arm);
            
            HumanBodyBones FINGERBASE_INDEX;
            HumanBodyBones FINGERBASE_MIDDLE;
            HumanBodyBones FINGERBASE_RING;
            HumanBodyBones FINGERBASE_LITTLE;
            HumanBodyBones FINGERTIP_INDEX;
            HumanBodyBones FINGERTIP_MIDDLE;
            HumanBodyBones FINGERTIP_RING;
            HumanBodyBones FINGERTIP_LITTLE;
            HumanBodyBones FOREARM;

            if( arm == _RIGHT_ARM ){
                FINGERBASE_INDEX = HumanBodyBones.RightIndexProximal;
                FINGERBASE_MIDDLE = HumanBodyBones.RightMiddleProximal;
                FINGERBASE_RING = HumanBodyBones.RightRingProximal;
                FINGERBASE_LITTLE = HumanBodyBones.RightLittleProximal;
                FINGERTIP_INDEX = HumanBodyBones.RightIndexDistal;
                FINGERTIP_MIDDLE = HumanBodyBones.RightMiddleDistal;
                FINGERTIP_RING = HumanBodyBones.RightRingDistal;
                FINGERTIP_LITTLE = HumanBodyBones.RightLittleDistal;
                FOREARM = HumanBodyBones.RightLowerArm;
            }else{
                FINGERBASE_INDEX = HumanBodyBones.LeftIndexProximal;
                FINGERBASE_MIDDLE = HumanBodyBones.LeftMiddleProximal;
                FINGERBASE_RING = HumanBodyBones.LeftRingProximal;
                FINGERBASE_LITTLE = HumanBodyBones.LeftLittleProximal;
                FINGERTIP_INDEX = HumanBodyBones.LeftIndexDistal;
                FINGERTIP_MIDDLE = HumanBodyBones.LeftMiddleDistal;
                FINGERTIP_RING = HumanBodyBones.LeftRingDistal;
                FINGERTIP_LITTLE = HumanBodyBones.LeftLittleDistal;
                FOREARM = HumanBodyBones.LeftLowerArm;
            }

            //forearm and hand angles
            float forearmAngleToUp = Vector3.Angle(Vector3.up,forearmVector);
            float forearmAngleToHead = Vector3.Angle(forearmVector,player.GetBonePosition(HumanBodyBones.Head) - player.GetBonePosition(FOREARM));
            Vector3 indexToLittle = player.GetBonePosition(FINGERBASE_LITTLE) - player.GetBonePosition(FINGERBASE_INDEX);
            float handAngleToForearm = AngleWhenProjectedOnPlane(handVector,forearmVector,indexToLittle);
            if( arm == _LEFT_ARM ) handAngleToForearm *= -1;

            //determine how stretched the fingers are
            Vector3 indexFingerBaseVector = player.GetBoneRotation(FINGERBASE_INDEX) * Vector3.up;
            Vector3 middleFingerBaseVector = player.GetBoneRotation(FINGERBASE_MIDDLE) * Vector3.up;
            Vector3 ringFingerBaseVector = player.GetBoneRotation(FINGERBASE_RING) * Vector3.up;
            Vector3 littleFingerBaseVector = player.GetBoneRotation(FINGERBASE_LITTLE) * Vector3.up;

            float fingerBaseAngleThreshhold = 40f;
            
            bool fingerBaseFlat =  AngleWhenProjectedOnPlane(handVector, indexFingerBaseVector , indexToLittle) * ((arm == _LEFT_ARM) ? -1:1) < fingerBaseAngleThreshhold &&
                                    AngleWhenProjectedOnPlane(handVector, middleFingerBaseVector, indexToLittle) * ((arm == _LEFT_ARM) ? -1:1) < fingerBaseAngleThreshhold &&
                                    AngleWhenProjectedOnPlane(handVector, ringFingerBaseVector, indexToLittle) * ((arm == _LEFT_ARM) ? -1:1) < fingerBaseAngleThreshhold &&
                                    AngleWhenProjectedOnPlane(handVector, littleFingerBaseVector, indexToLittle) * ((arm == _LEFT_ARM) ? -1:1) < fingerBaseAngleThreshhold;

            float fingerTipSum =    AngleWhenProjectedOnPlane(indexFingerBaseVector, player.GetBoneRotation(FINGERTIP_INDEX) * Vector3.up, indexToLittle) +
                                    AngleWhenProjectedOnPlane(middleFingerBaseVector, player.GetBoneRotation(FINGERTIP_MIDDLE) * Vector3.up, indexToLittle) +
                                    AngleWhenProjectedOnPlane(ringFingerBaseVector, player.GetBoneRotation(FINGERTIP_RING) * Vector3.up, indexToLittle) +
                                    AngleWhenProjectedOnPlane(littleFingerBaseVector, player.GetBoneRotation(FINGERTIP_LITTLE) * Vector3.up, indexToLittle);
            if( arm == _LEFT_ARM ) fingerTipSum *= -1;

            if(_debug){
                debugText[0].text = forearmAngleToUp.ToString("F2");
                debugText[1].text = handAngleToForearm.ToString("F2");
                debugText[2].text = fingerBaseFlat.ToString();
                debugText[3].text = fingerTipSum.ToString("F2");
            }

            //if gesture was active during last run, then (almost) ignore the hand angle (when rotating the hand the IK sometimes moves the elbow, which results in < 50.0f hand to forearm angle and would stop the gesture)
            float handAngleToForearmThreshold = 50.0f;
            float forearmAngleToUpThresholdLower = 60.0f;
            float forearmAngleToUpThresholdUpper = 120.0f;
            if( thresholdType == _GESTURE_RELAXED_THRESHOLD ){
                handAngleToForearmThreshold = 20.0f;
                forearmAngleToUpThresholdLower = 40.0f;
                forearmAngleToUpThresholdUpper = 140.0f;
            }

            //gesture is active if:
            // - the arm is mostly horizontal
            // - the arm is not pointing towards the head
            // - the hand is angled up enough relative to the forearm
            // - the fingers are stretched (flat hand)
            if( forearmAngleToUp > forearmAngleToUpThresholdLower
                && forearmAngleToUp < forearmAngleToUpThresholdUpper
                && forearmAngleToHead > 30f
                && handAngleToForearm > handAngleToForearmThreshold
                && fingerBaseFlat && fingerTipSum < 100f ){
                return arm;
            }else{
                return _NO_ARM;
            }
        }

        public void UpdateDebugTrackers(VRCPlayerApi player, int arm){
            HumanBodyBones HAND;
            HumanBodyBones FINGERBASE_INDEX;
            HumanBodyBones FINGERBASE_LITTLE;
            HumanBodyBones FINGERTIP_INDEX;
            HumanBodyBones FINGERTIP_MIDDLE;
            HumanBodyBones FINGERTIP_RING;
            HumanBodyBones FINGERTIP_LITTLE;
            HumanBodyBones ELBOW;

            if( arm == _RIGHT_ARM ){
                HAND = HumanBodyBones.RightHand;
                ELBOW = HumanBodyBones.RightLowerArm;
                FINGERBASE_INDEX = HumanBodyBones.RightIndexProximal;
                FINGERBASE_LITTLE = HumanBodyBones.RightLittleProximal;
                FINGERTIP_INDEX = HumanBodyBones.RightIndexDistal;
                FINGERTIP_MIDDLE = HumanBodyBones.RightMiddleDistal;
                FINGERTIP_RING = HumanBodyBones.RightRingDistal;
                FINGERTIP_LITTLE = HumanBodyBones.RightLittleDistal;
            }else{
                ELBOW = HumanBodyBones.LeftLowerArm;
                HAND = HumanBodyBones.LeftHand;
                FINGERBASE_INDEX = HumanBodyBones.LeftIndexProximal;
                FINGERBASE_LITTLE = HumanBodyBones.LeftLittleProximal;
                FINGERTIP_INDEX = HumanBodyBones.LeftIndexDistal;
                FINGERTIP_MIDDLE = HumanBodyBones.LeftMiddleDistal;
                FINGERTIP_RING = HumanBodyBones.LeftRingDistal;
                FINGERTIP_LITTLE = HumanBodyBones.LeftLittleDistal;
            }

            debugTracker[0].transform.SetPositionAndRotation(player.GetBonePosition(ELBOW), player.GetBoneRotation(ELBOW));
            debugTracker[1].transform.SetPositionAndRotation(player.GetBonePosition(HAND), player.GetBoneRotation(HAND));
            debugTracker[2].transform.SetPositionAndRotation(player.GetBonePosition(FINGERBASE_INDEX), player.GetBoneRotation(FINGERBASE_INDEX));
            //debugTracker[3].transform.SetPositionAndRotation(handUIInner.transform.position, handUIInner.transform.rotation);
            debugTracker[4].transform.SetPositionAndRotation(player.GetBonePosition(FINGERTIP_LITTLE), player.GetBoneRotation(FINGERTIP_LITTLE));
            debugTracker[5].transform.SetPositionAndRotation(handUI.transform.position, handUI.transform.rotation);
        }

        //calculate angle between 2 vectors when they are projected on a plane defined by the axis vector (as the plane's normal)
        //based on the function from this post because i am not good at vector math :( https://forum.unity.com/threads/is-vector3-signedangle-working-as-intended.694105/#post-5546026
        private float AngleWhenProjectedOnPlane(Vector3 from, Vector3 to, Vector3 axis){
            Vector3 dir1 = Vector3.Normalize(from);
            Vector3 dir2 = Vector3.Normalize(to);
            Vector3 normal = Vector3.Normalize(axis);
            return Mathf.Atan2(Vector3.Dot(Vector3.Cross(dir1, dir2), normal), Vector3.Dot(dir1, dir2)) * Mathf.Rad2Deg;
        }

        public void SetDebug(bool state){
            _debug = state;
            debugUI.SetActive(state);
            for(int i=0; i < debugTracker.Length; i++){
                debugTracker[i].SetActive(state); 
            }
        }

    }
}
