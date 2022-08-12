
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

namespace TimeControl
{
    [AddComponentMenu("TimeControl/DesktopUI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class DesktopUI : UdonSharpBehaviour
    {
        public TimeMachine timeMachine;

        public Button playPauseButton;
        public Text playPauseButtonText;
        public Slider timeSlider;
        public Text sliderCurrentValue;
        public Text sliderEndValue;
        public GameObject errorMessage;
        public Text errorMessageUsername;
        public Toggle debugToggle;
        public GameObject debugUI;
        public Text debugLocalTimelineTime;
        public Text debugNetworkTimelineTime;
        public Text debugCurrentTimelord;

        bool _ignoreOnSliderValueChangeEvent = false;
        bool _debug = false;

        void Start(){
            UpdateSliderPosition();
            UpdateSliderPositionText();
            UpdateSliderEndText();
        }

        void Update(){
            UpdateSliderUI();
            if(_debug){
                debugLocalTimelineTime.text = (timeMachine.GetCurrentLocalTimelineTime()).ToString("F3") + "s";
                debugNetworkTimelineTime.text = (timeMachine.GetCurrentNetworkTimelineTime()).ToString("F3") + "s";
                string timelord = timeMachine.GetCurrentTimelordUsername();
                if( timelord == null ) timelord = "no active timelord";
                debugCurrentTimelord.text = timelord;
            }
        }

        void UpdateSliderPositionText(){
            sliderCurrentValue.text = timeMachine.GetCurrentLocalTimelineTime().ToString("F3")+"s";
        }

        void UpdateSliderPosition(){
            timeSlider.value = (float)(timeMachine.GetCurrentLocalTimelineTime() / timeMachine.GetTimelineLength() );
        }

        public void UpdateSliderUI(){
            _ignoreOnSliderValueChangeEvent = true;
            UpdateSliderPosition();
            _ignoreOnSliderValueChangeEvent = false;
        }

        public void UpdateButtonUI(){
            if(timeMachine.isPlaying()){
                playPauseButtonText.text = "❚❚";
            }else{
                playPauseButtonText.text = "▶";
            }
        }

        public void PlayPauseClicked(){
            Debug.Log("Play/Pause clicked!");
            
            if( timeMachine.isPlaying() ){
                timeMachine.Pause();
            }else{
                timeMachine.Play(); 
            }
            UpdateButtonUI();
        }

        public void DebugCheckboxToggled(){
            timeMachine.SetDebug(debugToggle.isOn);
        }

        public void SetDebug(bool state){
            _debug = state;
            debugUI.SetActive(state);
        }

        public void LockUI(){
            timeSlider.interactable = false;
            playPauseButton.interactable = false;
         
            string username = timeMachine.GetCurrentTimelordUsername();
            if( username != null ){
                errorMessageUsername.text = username;
            }else{
                errorMessageUsername.text = "Error getting Username!";
            }
            errorMessage.SetActive(true);
        }

        public void UnlockUI(){
            timeSlider.interactable = true;
            playPauseButton.interactable = true;
            errorMessage.SetActive(false);
        }

        public void OnSliderBeginDrag()
        {
            //check if we can become timelord
            if( timeMachine.BecomeTimelord() ){
                timeMachine.OnLocalTimelineScrubStart();
            }else{
                //somebody else is currently timelord => show error
            }
        }

        public void OnSliderEndDrag()
        {
            if( timeMachine.StepDownAsTimelord() ){
                timeMachine.OnLocalTimelineScrubStop();
            }else{
                //todo??
            }
        }

        public void OnSliderValueChanged(){
            if(!_ignoreOnSliderValueChangeEvent){
                if( timeMachine.AmITheCurrentTimelord() ){
                    //we are already the timelord, so scrubbing is active => just seek to it
                    timeMachine.SeekTo( timeMachine.GetTimelineLength() * timeSlider.value );
                }else if( timeMachine.IsThereCurrentlyATimelord() == false ){
                    //we aren't the timelord and nobody else either => this was probably just a click on the slider timeline, so the slider drag event handler hasn't been called => become timelord before seeking and then stepping down again 
                    if(timeMachine.BecomeTimelord()){
                        timeMachine.SeekTo( timeMachine.GetTimelineLength() * timeSlider.value );
                        timeMachine.StepDownAsTimelord();
                    }       
                }
            }
            UpdateSliderPositionText();
        }

        void UpdateSliderEndText(){
            sliderEndValue.text = timeMachine.GetTimelineLength().ToString("F3") + "s";
        }
    }
}
