using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.Playables;

namespace TimeControl
{
    [AddComponentMenu("TimeMachine")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    public class TimeMachine : UdonSharpBehaviour
    {
        public PlayableDirector timeline;
        public AudioSource ambisonic;
        public AudioSource scrollSpeedSound;
        public float maxScrollSpeedSoundVolume = 1f;
        public bool debug = false;

        DesktopUI _desktopUI;
        DesktopHandUI _desktopHandUI;
        VRHandGestureUI _vrGestureUI;
        TutorialUI _tutorialUI;
        private bool _tutorialHasBeenMoved = false;

        //syncing the timeline timestamp with smoothing
        [UdonSynced(UdonSyncMode.Smooth)]
        private double _currentNetworkTimelineTime = 0;

        [UdonSynced, FieldChangeCallback(nameof(CurrentTimelord))]
        private int _currentTimelord=-1;
        
        [UdonSynced, FieldChangeCallback(nameof(NetworkIsPlaying))]
        private bool _networkIsPlaying = false;

        [UdonSynced, FieldChangeCallback(nameof(NetworkIsScrubbing))]
        private bool _networkIsScrubbing = false;

        //sadly the state property of the Timeline (PlayableDirector) object isn't exposed in udon, so we have to keep track of it ourselves
        private bool _timelinePlayingState = false;

        //keep track of local scrubbing state
        private bool _localIsScrubbing = false;

        private float _nextCleanupTime = 0f;
        private float _cleanUpTimeInterval = 1f;

        private double _previousTimelineTime = 0f;
        private float _minPitch = 0.2f;
        private float _maxPitch = 1.2f;

        //for remote scrubbing audio feedback smoothing
        float[] _speedBuffer = {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
        int _currentSpeedBufferIndex = 0;

        private bool _debug = false;

        private bool _initDone = false;

        private bool _loopTimeline = false;

        VRCPlayerApi _playerLocal;

        void Start()
        {
            _desktopUI = GetComponentInChildren<DesktopUI>(true);
            _desktopHandUI = GetComponentInChildren<DesktopHandUI>(true);
            _vrGestureUI = GetComponentInChildren<VRHandGestureUI>(true);
            _tutorialUI = GetComponentInChildren<TutorialUI>(true);

            _nextCleanupTime = Time.time + _cleanUpTimeInterval;

            _previousTimelineTime = timeline.time;

            if( timeline.extrapolationMode == DirectorWrapMode.Loop ){
                _loopTimeline = true;
            }
            
            SetDebug(debug);
        }

        private void Init(){
            _playerLocal = Networking.LocalPlayer;
            if(Networking.IsOwner(gameObject)){
                Debug.Log("TimeMachine Start: isOwner TRUE");
                _currentNetworkTimelineTime = GetCurrentLocalTimelineTime();
                if( timeline.playOnAwake ) NetworkIsPlaying = true; Play();

            }else{
                Debug.Log("TimeMachine Start: isOwner FALSE");
                SeekTo(_currentNetworkTimelineTime);
                timeline.Evaluate();
                if( NetworkIsPlaying ) OnNetworkPlay();
                else OnNetworkPause();

                if( CurrentTimelord != -1 && CurrentTimelord != _playerLocal.playerId ){
                    _desktopUI.LockUI();
                }
            }
        }

        void Update(){
            
          if( _initDone == false && Networking.IsNetworkSettled  ){
            Init();
            _initDone = true;
          }

          if( _initDone ){
            //somebody else is scrubbing the timeline => update local timeline
            if(NetworkIsScrubbing && !AmITheCurrentTimelord()){
                SeekTo(_currentNetworkTimelineTime);
            }
            //make local timeline catch up to network timeline if there were some sync issues
            if(!NetworkIsPlaying && !NetworkIsScrubbing && _currentNetworkTimelineTime != GetCurrentLocalTimelineTime()){
                SeekTo(_currentNetworkTimelineTime);
            }

            //keep the network timeline up to date for all other cases. only do it when we are currently the owner of the object, so only 1 user is doing it
            if(Networking.IsOwner(gameObject)){
                _currentNetworkTimelineTime = GetCurrentLocalTimelineTime();
            }

            //when timeline is currently being scrubbed then we update the scrub speed audio feedback
            if( NetworkIsScrubbing ){
                SetScrollSpeedSoundPitch( ((float)(timeline.time - _previousTimelineTime)) / Time.deltaTime );
                _previousTimelineTime = timeline.time;
            }

            if( Time.time >= _nextCleanupTime ){
                //if the current timelord has left the instace then reset timelord
                ResetTimelordIfNotInInstanceAnymore();

                //make sure we are not getting out of sync during a longer normal play period
                if( NetworkIsPlaying && !Networking.IsOwner(gameObject) ){
                    timeline.time = _currentNetworkTimelineTime;
                }

                //if there are sync issues make sure that we aren't blocking the timelord slot when we are currently not scrubbing with desktop or VR
                //we don't need perfect sync, just eventual consistency
                if( !_localIsScrubbing && AmITheCurrentTimelord() ){
                    StepDownAsTimelord();
                }

                //make sure the desktop UI isn't locked up unnecessarily in case of sync issues
                if( !IsThereCurrentlyATimelord() ) _desktopUI.UnlockUI();

                _nextCleanupTime = Time.time + _cleanUpTimeInterval;
            }
          }
        }

        public bool NetworkIsPlaying
        {
            set
            {
                Debug.Log("TimeMachine NetworkIsPlaying State changed to: "+value);
                _networkIsPlaying = value;
                
                if(value){
                    OnNetworkPlay();
                }else{
                    OnNetworkPause();
                }
            }
            get => _networkIsPlaying;
        }

        public bool NetworkIsScrubbing
        {
            set
            {
                Debug.Log("TimeMachine NetworkIsScrubbing State changed to: "+value);
                _networkIsScrubbing = value;
                ResetSmoothedSpeedValue();
                SetScrollSpeedSoundPitch(0f);
                if(value == true){
                    scrollSpeedSound.Play();
                }else{
                    scrollSpeedSound.Pause();
                }
            }
            get => _networkIsScrubbing;
        }

        public int CurrentTimelord
        {
            set
            {
                Debug.Log("TimeMachine CurrentTimelord State changed to: "+value);
                _currentTimelord = value;
                //check if somebody else is currently timelord and inform the desktop UI
                if( value != -1 && _playerLocal != null && value != _playerLocal.playerId ){
                    _desktopUI.LockUI();
                }else{
                    _desktopUI.UnlockUI();
                }                
                
            }
            get => _currentTimelord;
        }

        public bool Play(){
            if(_debug) Debug.Log("TimeMachine Play()");
            if( BecomeTimelord() ){
                //let other users know (this also triggers the NetworkIsPlaying.set() function locally)
                _currentNetworkTimelineTime = timeline.time;
                NetworkIsPlaying = true;
                return true;
            }
            return false;
        }

        private void OnNetworkPlay(){
            timeline.time = _currentNetworkTimelineTime;
            timeline.Play();
            if(ambisonic != null) ambisonic.Play();
            _timelinePlayingState = true;
            _desktopUI.UpdateButtonUI();
        }

        public bool Pause(){
            if(_debug) Debug.Log("TimeMachine Pause()");
            if( BecomeTimelord() ){
                //let other users know (this also triggers the NetworkIsPlaying.set() function locally)
                _currentNetworkTimelineTime = timeline.time;
                NetworkIsPlaying = false;
                return true;
            }
            return false;
        }

        private void OnNetworkPause(){
            timeline.Pause();
            if(ambisonic != null) ambisonic.Pause();
            timeline.time = _currentNetworkTimelineTime;
            _timelinePlayingState = false;
            _desktopUI.UpdateButtonUI();
        }

        private void SetNetworkTimelineScrub(bool state){
            //set current user to owner if they aren't already
            if(!Networking.IsOwner(gameObject)){
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                RequestSerialization();
            }
            //let other users know (this also triggers the NetworkIsScrubbing.set() function locally)
            NetworkIsScrubbing = state;
        }

        public void OnLocalTimelineScrubStart(){
            _localIsScrubbing = true;
            SetNetworkTimelineScrub(true);
        }

        public void OnLocalTimelineScrubStop(){
            _localIsScrubbing = false;
            SetNetworkTimelineScrub(false);
        }

        public bool isPlaying(){
            return _timelinePlayingState;
        }

        public bool SeekTo(double newTime){
            bool success = true;
            if( newTime < 0f ){
                if( _loopTimeline ){
                    newTime = timeline.duration + newTime;
                }else{
                    newTime = 0f;
                    success = false;
                }
            }else if( newTime > timeline.duration ){
                if( _loopTimeline ){
                    newTime = newTime % timeline.duration;
                }else{
                    newTime = timeline.duration;
                    success = false;
                }
            }
            
            //when the first actual seek happens, move the tutorial out of the way
            if( _tutorialHasBeenMoved == false && timeline.time != newTime && AmITheCurrentTimelord() ){
                if( _tutorialUI != null ) _tutorialUI.MoveTutorial();
                _tutorialHasBeenMoved = true;
            }

            _currentNetworkTimelineTime = newTime;
            timeline.time = newTime;
            
            if(!_timelinePlayingState){
                timeline.DeferredEvaluate();
            }
            return success;
        }

        public bool AmITheCurrentTimelord(){
            if( _initDone && _playerLocal != null && CurrentTimelord == _playerLocal.playerId ){
                return true;
            }
            return false;
        }

        private void ResetTimelordIfNotInInstanceAnymore(){
            //check if the current time lord id is still in the instance and if not, reset it
            if( CurrentTimelord != -1 && VRCPlayerApi.GetPlayerById(CurrentTimelord) == null ){
                if(!Networking.IsOwner(gameObject)){
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    RequestSerialization();
                }
                CurrentTimelord = -1;
            }
        }

        public bool IsThereCurrentlyATimelord(){
            return CurrentTimelord != -1;
        }

        public bool BecomeTimelord(){
            //if udon networking isn't initialised yet then we can't yet determine this properly
            if( _initDone == false ) return false;
            //if we are already the timelord return true immediately
            if( AmITheCurrentTimelord() ) return true;

            //check if current time lord is not set to a player id
            if( CurrentTimelord == -1 ){
                if(!Networking.IsOwner(gameObject)){
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    RequestSerialization();
                }
                CurrentTimelord = _playerLocal.playerId;
                return true;
            }
            return false;
        }

        public bool StepDownAsTimelord(){
            if( CurrentTimelord == _playerLocal.playerId ){
                if(!Networking.IsOwner(gameObject)){
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    RequestSerialization();
                }
                CurrentTimelord = -1;
                return true;
            }else{
                if( CurrentTimelord == -1 ) return true;
                return false;
            }
        }

        public VRCPlayerApi GetCurrentTimelord(){
            VRCPlayerApi player = null;
            if( CurrentTimelord > 0 ){
                player = VRCPlayerApi.GetPlayerById(CurrentTimelord);
            }
            return player;
        }

        public string GetCurrentTimelordUsername(){
            string username = null;
            if( CurrentTimelord > 0 ){
                VRCPlayerApi timelord = VRCPlayerApi.GetPlayerById(CurrentTimelord);
                if( timelord != null ){
                    username = timelord.displayName;
                }
            }
            return username;
        }

        public double GetCurrentLocalTimelineTime(){
            return timeline.time;
        }

        public double GetCurrentNetworkTimelineTime(){
            return _currentNetworkTimelineTime;
        }

        public double GetTimelineLength(){
            return timeline.duration;
        }

        private void SetScrollSpeedSoundPitch(float speed){
            float absoluteSpeed = Mathf.Abs( speed );
            if( !AmITheCurrentTimelord() ) absoluteSpeed = GetSmoothedSpeedValue(absoluteSpeed);

            scrollSpeedSound.volume = Mathf.Clamp( absoluteSpeed * 10f, 0f, maxScrollSpeedSoundVolume);
            scrollSpeedSound.pitch = Mathf.Lerp( _minPitch, _maxPitch, absoluteSpeed );
        }

        private float GetSmoothedSpeedValue(float newValue){
            _speedBuffer[_currentSpeedBufferIndex] = newValue;
            _currentSpeedBufferIndex++;
            if(_currentSpeedBufferIndex == 10) _currentSpeedBufferIndex = 0;
            float smoothedRotation = 0.0f;
            for(int i=0; i<_speedBuffer.Length; i++){
                smoothedRotation += _speedBuffer[i];
            }
            return smoothedRotation/_speedBuffer.Length;
        }

        private void ResetSmoothedSpeedValue(){
            for(int i=0; i<_speedBuffer.Length; i++){
                _speedBuffer[i] = 0;
            }
        }

        public void SetDebug(bool state){
            _debug = state;
            _desktopUI.SetDebug(state);
            _desktopHandUI.SetDebug(state);
            _vrGestureUI.SetDebug(state);
        }

    }
}
