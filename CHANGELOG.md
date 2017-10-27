0.0.4.5
- Replaced reflection code with hard-coded offsets.  These will need to be checked for each KSP version, 
	see the comments at the beginning of the file in ManeuverQueue
- Added ReflectionDump.cs to dump the values to the log file, needed to get new values of the constants
- Added Reflection class 

Following thanks to @firda-cze:
- removed this-prefixes, used shortcuts like get => value, properties start with big letter (PascalCasing)
- main logic/change in Start() - will try 64bit first, then current 32bit with type check, full search with printout as last option
- fix for NullReference reworked: DefaultVessels can return null, Start will set delaySetMode = true for next Update()
- debug build includes more logs (see Log() with Conditional("DEBUG") at the end)


## 0.4.4
Bugfixes:
  - Fix for NullReference (in Start and Update), thanks @firda-cze

## 0.4.3
  - Adoption by LGG

## 0.4.2
   
Bugfixes:
 
  - Fixes an issue that caused the list to reset to MNV when terminating a vessel
  
## 0.4.1

Features:

  - Rebuilt for KSP 1.2.1
   
Bugfixes:
 
  - Fixed an out of bounds error when tracking zero vessels

## 0.4.0

Features:

  - Stopping time warp when a maneuver node approaches
 
Bugfixes:
 
  - Fixed an issue that caused the list to render incorrectly in maneuver mode when an asteroid track is ended
  
## 0.3.0

Features:

  - Unsetting vessel filters when entering MNV mode and restoring when returning to MET or A-Z mode
  - Brightened the colors

## 0.2.1

Features:

  - Updated to support KSP v1.2-pre

Bugfixes:

  - Fixed an issue that caused some vessel shading to fail when tracking station filters were applied

## 0.2

Features:

  - Color coded vessel icons
  - Remembers selected mode

Bugfixes:

  - Correctly updates list when tracking/untracking objects
  - Maintains selection state in list between modes

## 0.1
  - Initial release
