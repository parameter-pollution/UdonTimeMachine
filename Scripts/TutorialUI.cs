
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace TimeControl
{
    [AddComponentMenu("TimeControl/TutorialUI")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class TutorialUI : UdonSharpBehaviour
    {
        public GameObject VRTab;
        public GameObject VRTabButton;
        public GameObject VRTabButtonInactive;
        public GameObject DesktopTab;
        public GameObject DesktopTabButton;
        public GameObject DesktopTabButtonInactive;
        public GameObject HideButton;
        public GameObject DesktopHandUI;

        private Animator _animator;
        private bool _tutorialHasBeenMoved = false;

        void Start()
        {
            _animator = gameObject.GetComponent<Animator>();
            if( Networking.LocalPlayer.IsUserInVR() ){
                OnVRTabButtonClick();
            }else{
                OnDesktopTabButtonClick();
            }
        }

        public void OnVRTabButtonClick(){
            VRTab.SetActive(true);
            VRTabButton.SetActive(true);
            VRTabButtonInactive.SetActive(false);
            DesktopTab.SetActive(false);
            DesktopTabButton.SetActive(false);
            DesktopTabButtonInactive.SetActive(true);
            if( Networking.LocalPlayer.IsUserInVR() || _tutorialHasBeenMoved == false ) DesktopHandUI.SetActive(false);
        }

        public void OnDesktopTabButtonClick(){
            VRTab.SetActive(false);
            VRTabButton.SetActive(false);
            VRTabButtonInactive.SetActive(true);
            DesktopTab.SetActive(true);
            DesktopTabButton.SetActive(true);
            DesktopTabButtonInactive.SetActive(false);
            DesktopHandUI.SetActive(true);
        }

        public void MoveTutorial()
        {
            if( _tutorialHasBeenMoved == false ){
                _animator.Play("Base Layer.MoveTutorial", 0, 0);
                HideButton.SetActive(false);
                _tutorialHasBeenMoved = true;
            }
        }
    }
}