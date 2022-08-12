# UdonTimeMachine (ALPHA)

A [Unity Timeline](https://docs.unity3d.com/Packages/com.unity.timeline@1.2/manual/index.html) controller for VRChat Worlds that supports Desktop and VR mode and has a synced timeline so everyone in the same world instance sees and hears the same. 
 
In VR mode you can use a time control gesture similar to the one used in [Doctor Strange](https://www.imdb.com/title/tt1211837/):

![WorldThumbnail](https://user-images.githubusercontent.com/4985522/184362326-331b0253-13a2-4f89-a13a-f749cdefa141.png)

### Try it in this VRChat World: [Dr. Strange Time Control](https://vrchat.com/home/world/wrld_7749aee8-a282-4445-8760-0eeb39ce2d7f/)

## Installation
* Import [VRC World SDK](https://vrchat.com/home/download) (tested with VRCSDK3-WORLD-2022.07.26.21.44_Public)
* Import [UdonSharp](https://github.com/vrchat-community/UdonSharp) (tested with UdonSharp_v0.20.3 )
* Import [latest UdonTimeMachine](https://github.com/parameter-pollution/UdonTimeMachine/releases)

## Usage
### Example Scene
Open (double click) the included example scene `TimeMachine\ExampleScene\ExampleScene.unity` in unity.

### Create your own Scene
1. Drag & Drop the `TimeMachine\TimeMachine.prefab` into your scene
2. Create a Unity Timeline
      1. Create an `Empty` GameObject in your scene
      2. Rename it to e.g. "Timeline"
      3. Add the Timeline Tab to your project by clicking on `Window -> Sequencing -> Timeline`
      4. Click on it. Then click on the Timeline Tab and click `Create`
3. Drag & Drop this "Timeline" Object into the "Timeline" Parameter of the TimeMachine Object

https://user-images.githubusercontent.com/4985522/184370198-79ed3798-e858-4489-b0f0-89359df80892.mp4

## Usage Ideas
* As a tool to inspect a Unity Timeline in a VRChat world that you are working on
* Allow visitors of your world to revisit specific parts of your Timeline/Animation
* Easily adjust/rewind the state of the VRChat World (e.g. Animations/Time of day/..) while recording a video
* Make still frame shots of Animations

## Unity Timeline
The Unity Timeline is a powerful feature.
It allows you to work on and replay/scrub the Timeline while in Editor Mode in Unity. You don't have to hit Play or even upload the VRChat World to play/scrub your Timeline.
You can also create camera tracks.

There is an interesting talk "[Cinemachine and Timeline in VRChat"](https://www.youtube.com/watch?v=4jLOZdg6blc) by MomoTheMonster from the 2020 VRChat World Developter Conference. 

## What to watch out for
* Keep Objects (Particle effects / Animations / Audio / ...) disabled when you want to control them via the Timeline (so they only show up when defined by the timeline)
* Colliders on Particle Systems can cause very bad performance (especially when scrolling backwards in time). Use plane colliders instead of world colliders if you have to use colliders
* When you put an Audio Source with Ambisonic audio on a Unity Timeline then it won't be played back as ambisonic anymore (at least not in VRChat). But a workaround is putting that Audio Source into the "Ambisonic" Slot on the "TimeMachine" Object (but this will only play/pause the ambisonic, no time scrubbing)

## Disclaimer
This is just a pet project of mine. I wanted it to exist and it didn't, so I decided to try to create it. But I don't have much time that I can put into it. So if you come up with a better system than this then send me a link and I will link to it here prominently. I don't need my name attached to this, I just want it to exist.

This is my first C#/UdonSharp and Udon Networking project. So be gentle ;-)

There are currently no big/annoying bugs that I know about (famous last words), but I am sure that there are bugs in this code and maybe this should all be rewritten from scratch. Feel free to submit bug reports, but as I said, I don't have much time to work on this, so no guarantees that they will be looked at/fixed.

USE AT YOUR OWN RISK

Not affiliated with VRChat/Udon.
